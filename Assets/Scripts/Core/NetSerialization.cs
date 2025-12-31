using System;
using System.IO;

/// <summary>
/// Helper methods for serialising and deserialising NetMessage instances
/// and common payload types (e.g., GameAction) to and from byte arrays.
/// </summary>
public static class NetSerialization
{
    public static byte[] SerializeNetMessage(NetMessage msg)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write((byte)msg.type);
        bw.Write(msg.sequenceId);

        int length = msg.payload != null ? msg.payload.Length : 0;
        bw.Write(length);
        if (length > 0)
            bw.Write(msg.payload);

        return ms.ToArray();
    }

    public static bool TryDeserializeNetMessage(byte[] data, out NetMessage msg)
    {
        msg = default;
        if (data == null || data.Length < 6) // type + seq + length (min)
            return false;

        try
        {
            using (var ms = new MemoryStream(data))
            using (var br = new BinaryReader(ms))
            {
                msg.type = (NetMessageType)br.ReadByte();
                msg.sequenceId = br.ReadInt32();
                int length = br.ReadInt32();
                if (length > 0)
                {
                    msg.payload = br.ReadBytes(length);
                }
                else
                {
                    msg.payload = Array.Empty<byte>();
                }
            }

            return true;
        }
        catch
        {
            msg = default;
            return false;
        }
    }

    /// <summary>
    /// Serialises a GameAction into a payload suitable for embedding inside
    /// a NetMessage of type InputAction.
    /// </summary>
    public static byte[] SerializeGameAction(GameAction action)
    {
        using (var ms = new MemoryStream())
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write((byte)action.type);
            bw.Write((int)action.owner);

            bool hasCardId = !string.IsNullOrEmpty(action.cardId);
            bw.Write(hasCardId);
            if (hasCardId)
                bw.Write(action.cardId);

            bw.Write(action.slotIndex);

            int targetCount = action.targetSlotIndices != null ? action.targetSlotIndices.Count : 0;
            bw.Write(targetCount);
            if (targetCount > 0)
            {
                for (int i = 0; i < targetCount; i++)
                {
                    bw.Write(action.targetSlotIndices[i]);
                }
            }

            return ms.ToArray();
        }
    }

    /// <summary>
    /// Deserialises a GameAction from a payload previously created by
    /// SerializeGameAction.
    /// </summary>
    public static GameAction DeserializeGameAction(byte[] payload)
    {
        var action = new GameAction();
        if (payload == null || payload.Length == 0)
            return action;

        using (var ms = new MemoryStream(payload))
        using (var br = new BinaryReader(ms))
        {
            action.type = (GameActionType)br.ReadByte();
            action.owner = (SlotOwner)br.ReadInt32();

            bool hasCardId = br.ReadBoolean();
            if (hasCardId)
                action.cardId = br.ReadString();
            else
                action.cardId = null;

            action.slotIndex = br.ReadInt32();

            int targetCount = br.ReadInt32();
            action.targetSlotIndices.Clear();
            for (int i = 0; i < targetCount; i++)
            {
                action.targetSlotIndices.Add(br.ReadInt32());
            }
        }

        return action;
    }

    // -------------------------------------------------------------------------
    // NetSessionHeader Serialization
    // -------------------------------------------------------------------------

    /// <summary>
    /// Serialises a NetSessionHeader including deck data into a byte array.
    /// </summary>
    public static byte[] SerializeNetSessionHeader(NetSessionHeader header)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        bw.Write(header.protocolVersion);
        bw.Write(header.rngSeed);
        bw.Write(header.hostId ?? "");
        bw.Write(header.guestId ?? "");
        bw.Write((int)header.localRole);
        bw.Write(header.hostDeckId ?? "");
        bw.Write(header.guestDeckId ?? "");

        // Host deck
        WriteDeckEntries(bw, header.hostDeck);
        // Guest deck
        WriteDeckEntries(bw, header.guestDeck);

        return ms.ToArray();
    }

    /// <summary>
    /// Deserialises a NetSessionHeader from a byte array.
    /// </summary>
    public static bool TryDeserializeNetSessionHeader(byte[] data, out NetSessionHeader header)
    {
        header = default;
        if (data == null || data.Length == 0)
            return false;

        try
        {
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            header.protocolVersion = br.ReadInt32();
            header.rngSeed = br.ReadInt32();
            header.hostId = br.ReadString();
            header.guestId = br.ReadString();
            header.localRole = (SlotOwner)br.ReadInt32();
            header.hostDeckId = br.ReadString();
            header.guestDeckId = br.ReadString();

            header.hostDeck = ReadDeckEntries(br);
            header.guestDeck = ReadDeckEntries(br);

            return true;
        }
        catch
        {
            header = default;
            return false;
        }
    }

    // -------------------------------------------------------------------------
    // DeckCardEntry Serialization (for SessionAck)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Serialises an array of DeckCardEntry into a byte array.
    /// Used for the SessionAck payload.
    /// </summary>
    public static byte[] SerializeDeckEntries(DeckCardEntry[] entries)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        WriteDeckEntries(bw, entries);
        return ms.ToArray();
    }

    /// <summary>
    /// Deserialises an array of DeckCardEntry from a byte array.
    /// </summary>
    public static DeckCardEntry[] DeserializeDeckEntries(byte[] data)
    {
        if (data == null || data.Length == 0)
            return Array.Empty<DeckCardEntry>();

        try
        {
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);
            return ReadDeckEntries(br);
        }
        catch
        {
            return Array.Empty<DeckCardEntry>();
        }
    }

    // -------------------------------------------------------------------------
    // InputActionAck Serialization
    // -------------------------------------------------------------------------

    /// <summary>
    /// Serialises an InputActionAck payload containing only the acknowledged sequence ID.
    /// </summary>
    public static byte[] SerializeInputActionAck(int acknowledgedSequenceId)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(acknowledgedSequenceId);
        return ms.ToArray();
    }

    /// <summary>
    /// Deserialises an InputActionAck payload to extract the acknowledged sequence ID.
    /// </summary>
    public static bool TryDeserializeInputActionAck(byte[] payload, out int acknowledgedSequenceId)
    {
        acknowledgedSequenceId = -1;
        if (payload == null || payload.Length < 4)
            return false;

        try
        {
            using var ms = new MemoryStream(payload);
            using var br = new BinaryReader(ms);
            acknowledgedSequenceId = br.ReadInt32();
            return true;
        }
        catch
        {
            return false;
        }
    }

    // -------------------------------------------------------------------------
    // StateRequest/StateSync Serialization (for reconnection)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Serialises a StateRequestPayload into a byte array.
    /// </summary>
    public static byte[] SerializeStateRequest(StateRequestPayload request)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(request.lastKnownRound);
        bw.Write(request.lastKnownChecksum);
        bw.Write(request.isReconnecting);
        return ms.ToArray();
    }

    /// <summary>
    /// Deserialises a StateRequestPayload from a byte array.
    /// </summary>
    public static bool TryDeserializeStateRequest(byte[] payload, out StateRequestPayload request)
    {
        request = default;
        if (payload == null || payload.Length < 9) // int + int + bool
            return false;

        try
        {
            using var ms = new MemoryStream(payload);
            using var br = new BinaryReader(ms);
            request.lastKnownRound = br.ReadInt32();
            request.lastKnownChecksum = br.ReadInt32();
            request.isReconnecting = br.ReadBoolean();
            return true;
        }
        catch
        {
            request = default;
            return false;
        }
    }

    /// <summary>
    /// Serialises a StateSyncPayload into a byte array.
    /// </summary>
    public static byte[] SerializeStateSync(StateSyncPayload sync)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(sync.round);
        bw.Write(sync.checksum);
        bw.Write(sync.success);
        bw.Write(sync.errorMessage ?? "");

        int dataLength = sync.stateData?.Length ?? 0;
        bw.Write(dataLength);
        if (dataLength > 0)
            bw.Write(sync.stateData);

        return ms.ToArray();
    }

    /// <summary>
    /// Deserialises a StateSyncPayload from a byte array.
    /// </summary>
    public static bool TryDeserializeStateSync(byte[] payload, out StateSyncPayload sync)
    {
        sync = default;
        if (payload == null || payload.Length < 13) // int + int + bool + string length + int
            return false;

        try
        {
            using var ms = new MemoryStream(payload);
            using var br = new BinaryReader(ms);
            sync.round = br.ReadInt32();
            sync.checksum = br.ReadInt32();
            sync.success = br.ReadBoolean();
            sync.errorMessage = br.ReadString();

            int dataLength = br.ReadInt32();
            if (dataLength > 0)
                sync.stateData = br.ReadBytes(dataLength);
            else
                sync.stateData = Array.Empty<byte>();

            return true;
        }
        catch
        {
            sync = default;
            return false;
        }
    }

    /// <summary>
    /// Serialises an empty heartbeat payload.
    /// </summary>
    public static byte[] SerializeHeartbeat()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(UnityEngine.Time.unscaledTime);
        return ms.ToArray();
    }

    // -------------------------------------------------------------------------
    // Private Helpers
    // -------------------------------------------------------------------------

    private static void WriteDeckEntries(BinaryWriter bw, DeckCardEntry[] entries)
    {
        int count = entries?.Length ?? 0;
        bw.Write(count);
        for (int i = 0; i < count; i++)
        {
            bw.Write(entries[i].cardId ?? "");
            bw.Write(entries[i].count);
        }
    }

    private static DeckCardEntry[] ReadDeckEntries(BinaryReader br)
    {
        int count = br.ReadInt32();
        if (count <= 0)
            return Array.Empty<DeckCardEntry>();

        var entries = new DeckCardEntry[count];
        for (int i = 0; i < count; i++)
        {
            entries[i] = new DeckCardEntry { cardId = br.ReadString(), count = br.ReadInt32() };
        }
        return entries;
    }
}
