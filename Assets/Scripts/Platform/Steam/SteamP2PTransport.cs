using System;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

/// <summary>
/// IMatchTransport implementation using SteamNetworkingSockets with Steam Datagram Relay.
/// This provides NAT traversal and lower latency via Valve's relay network.
///
/// For host: Creates a relay socket that listens for connections.
/// For guest: Connects to the host's relay socket using their SteamId.
///
/// References:
/// - https://wiki.facepunch.com/steamworks/SteamNetworkingSockets
/// </summary>
public class SteamP2PTransport : MonoBehaviour, IMatchTransport
{
    [Header("Settings")]
    [Tooltip("Virtual port for the relay connection (can be 0 for default).")]
    [SerializeField]
    private int virtualPort = 0;

    [Tooltip("Enable debug logging for network events.")]
    [SerializeField]
    private bool debugLogging = true;

    private bool _isHost;
    private SteamId _remoteSteamId;

    // Host-side: socket manager that accepts connections
    private SocketManager _socketManager;

    // Guest-side: connection to the host
    private ConnectionManager _connectionManager;

    // The active connection (both host and guest will have one after connecting)
    private Connection? _activeConnection;

    public bool IsHost => _isHost;
    public bool IsConnected { get; private set; }

    public event Action<byte[]> OnDataReceived;
    public event Action OnDisconnected;

    private void OnEnable()
    {
        if (SteamManager.Instance == null || !SteamManager.Instance.IsInitialized)
        {
            if (debugLogging)
            {
                Debug.LogWarning(
                    "SteamP2PTransport: SteamManager not initialised; transport will remain disconnected."
                );
            }
        }
    }

    private void Update()
    {
        // Poll for messages and connection state changes
        _socketManager?.Receive();
        _connectionManager?.Receive();
    }

    private void OnDisable()
    {
        Disconnect();
    }

    private void OnDestroy()
    {
        Disconnect();
    }

    // -------------------------------------------------------------------------
    // Configuration (called by SteamLobbyManager)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Configures this transport as the host and starts listening for connections.
    /// </summary>
    public void ConfigureAsHost(SteamId guestId)
    {
        if (IsConnected)
        {
            Debug.LogWarning("SteamP2PTransport: Already connected. Disconnect first.");
            return;
        }

        _isHost = true;
        _remoteSteamId = guestId;

        if (debugLogging)
        {
            Debug.Log($"SteamP2PTransport: Configuring as HOST, expecting guest {guestId}");
        }

        StartHostSocket();
    }

    /// <summary>
    /// Configures this transport as a guest and connects to the host.
    /// </summary>
    public void ConfigureAsGuest(SteamId hostId)
    {
        if (IsConnected)
        {
            Debug.LogWarning("SteamP2PTransport: Already connected. Disconnect first.");
            return;
        }

        _isHost = false;
        _remoteSteamId = hostId;

        if (debugLogging)
        {
            Debug.Log($"SteamP2PTransport: Configuring as GUEST, connecting to host {hostId}");
        }

        StartGuestConnection();
    }

    /// <summary>
    /// Legacy configuration method for backwards compatibility.
    /// </summary>
    public void ConfigureRemote(string remoteIdString, bool isHost)
    {
        if (!ulong.TryParse(remoteIdString, out ulong id))
        {
            Debug.LogError($"SteamP2PTransport: Invalid remote ID string '{remoteIdString}'");
            return;
        }

        if (isHost)
            ConfigureAsHost(id);
        else
            ConfigureAsGuest(id);
    }

    // -------------------------------------------------------------------------
    // Host Socket
    // -------------------------------------------------------------------------

    private void StartHostSocket()
    {
        try
        {
            _socketManager = SteamNetworkingSockets.CreateRelaySocket<GameSocketManager>(
                virtualPort
            );
            var gameSocketManager = _socketManager as GameSocketManager;
            if (gameSocketManager != null)
            {
                gameSocketManager.Initialize(this);
            }

            if (debugLogging)
            {
                Debug.Log($"SteamP2PTransport: Relay socket created on virtual port {virtualPort}");
            }

            // Mark as connected once socket is created (guest will connect to us)
            IsConnected = true;
        }
        catch (Exception e)
        {
            Debug.LogError($"SteamP2PTransport: Failed to create relay socket: {e.Message}");
            IsConnected = false;
        }
    }

    // -------------------------------------------------------------------------
    // Guest Connection
    // -------------------------------------------------------------------------

    private void StartGuestConnection()
    {
        try
        {
            _connectionManager = SteamNetworkingSockets.ConnectRelay<GameConnectionManager>(
                _remoteSteamId,
                virtualPort
            );
            var gameConnectionManager = _connectionManager as GameConnectionManager;
            if (gameConnectionManager != null)
            {
                gameConnectionManager.Initialize(this);
            }

            if (debugLogging)
            {
                Debug.Log(
                    $"SteamP2PTransport: Connecting to host {_remoteSteamId} on virtual port {virtualPort}"
                );
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"SteamP2PTransport: Failed to connect to host: {e.Message}");
            IsConnected = false;
        }
    }

    // -------------------------------------------------------------------------
    // IMatchTransport Implementation
    // -------------------------------------------------------------------------

    public void Send(byte[] data)
    {
        if (data == null || data.Length == 0)
            return;

        if (!IsConnected)
        {
            Debug.LogWarning("SteamP2PTransport.Send called while not connected.");
            return;
        }

        if (_isHost)
        {
            // Host sends to all connected clients (just one for our 1v1 case)
            if (_activeConnection.HasValue)
            {
                _activeConnection.Value.SendMessage(data, SendType.Reliable);
                if (debugLogging)
                {
                    Debug.Log($"SteamP2PTransport: Host sent {data.Length} bytes");
                }
            }
            else
            {
                Debug.LogWarning("SteamP2PTransport: Host has no active connection to send to.");
            }
        }
        else
        {
            // Guest sends via connection manager
            if (_connectionManager != null && _connectionManager.Connected)
            {
                _connectionManager.Connection.SendMessage(data, SendType.Reliable);
                if (debugLogging)
                {
                    Debug.Log($"SteamP2PTransport: Guest sent {data.Length} bytes");
                }
            }
            else
            {
                Debug.LogWarning("SteamP2PTransport: Guest connection not ready.");
            }
        }
    }

    public void Disconnect()
    {
        if (debugLogging)
        {
            Debug.Log("SteamP2PTransport: Disconnecting...");
        }

        if (_socketManager != null)
        {
            _socketManager.Close();
            _socketManager = null;
        }

        if (_connectionManager != null)
        {
            _connectionManager.Close();
            _connectionManager = null;
        }

        _activeConnection = null;
        IsConnected = false;

        OnDisconnected?.Invoke();
    }

    // -------------------------------------------------------------------------
    // Internal Callbacks (called by socket/connection managers)
    // -------------------------------------------------------------------------

    internal void HandleClientConnected(Connection connection, ConnectionInfo info)
    {
        if (debugLogging)
        {
            Debug.Log($"SteamP2PTransport: Client connected - {info.Identity.SteamId}");
        }

        _activeConnection = connection;
        IsConnected = true;
    }

    internal void HandleClientDisconnected(Connection connection, ConnectionInfo info)
    {
        if (debugLogging)
        {
            Debug.Log($"SteamP2PTransport: Client disconnected - {info.Identity.SteamId}");
        }

        if (_activeConnection.HasValue && _activeConnection.Value.Id == connection.Id)
        {
            _activeConnection = null;
            IsConnected = false;
            OnDisconnected?.Invoke();
        }
    }

    internal void HandleConnectedToServer(ConnectionInfo info)
    {
        if (debugLogging)
        {
            Debug.Log($"SteamP2PTransport: Connected to server - {info.Identity.SteamId}");
        }

        IsConnected = true;
    }

    internal void HandleDisconnectedFromServer(ConnectionInfo info)
    {
        if (debugLogging)
        {
            Debug.Log($"SteamP2PTransport: Disconnected from server");
        }

        IsConnected = false;
        OnDisconnected?.Invoke();
    }

    internal void HandleMessageReceived(
        IntPtr data,
        int size,
        long messageNum,
        long recvTime,
        int channel
    )
    {
        if (size <= 0)
            return;

        byte[] buffer = new byte[size];
        System.Runtime.InteropServices.Marshal.Copy(data, buffer, 0, size);

        if (debugLogging)
        {
            Debug.Log($"SteamP2PTransport: Received {size} bytes (msg #{messageNum})");
        }

        OnDataReceived?.Invoke(buffer);
    }
}

// =============================================================================
// Socket Manager (Host-side)
// =============================================================================

/// <summary>
/// SocketManager implementation for the host. Handles incoming connections
/// and messages from guests.
/// </summary>
public class GameSocketManager : SocketManager
{
    private SteamP2PTransport _transport;

    public void Initialize(SteamP2PTransport transport)
    {
        _transport = transport;
    }

    public override void OnConnecting(Connection connection, ConnectionInfo info)
    {
        base.OnConnecting(connection, info);
        Debug.Log($"GameSocketManager: Client connecting - {info.Identity.SteamId}");

        // Accept all incoming connections (for now - could add validation)
        connection.Accept();
    }

    public override void OnConnected(Connection connection, ConnectionInfo info)
    {
        base.OnConnected(connection, info);
        _transport?.HandleClientConnected(connection, info);
    }

    public override void OnDisconnected(Connection connection, ConnectionInfo info)
    {
        base.OnDisconnected(connection, info);
        _transport?.HandleClientDisconnected(connection, info);
    }

    public override void OnMessage(
        Connection connection,
        NetIdentity identity,
        IntPtr data,
        int size,
        long messageNum,
        long recvTime,
        int channel
    )
    {
        _transport?.HandleMessageReceived(data, size, messageNum, recvTime, channel);
    }
}

// =============================================================================
// Connection Manager (Guest-side)
// =============================================================================

/// <summary>
/// ConnectionManager implementation for guests. Handles the connection to
/// the host and incoming messages.
/// </summary>
public class GameConnectionManager : ConnectionManager
{
    private SteamP2PTransport _transport;

    public void Initialize(SteamP2PTransport transport)
    {
        _transport = transport;
    }

    public override void OnConnecting(ConnectionInfo info)
    {
        base.OnConnecting(info);
        Debug.Log($"GameConnectionManager: Connecting to server...");
    }

    public override void OnConnected(ConnectionInfo info)
    {
        base.OnConnected(info);
        _transport?.HandleConnectedToServer(info);
    }

    public override void OnDisconnected(ConnectionInfo info)
    {
        base.OnDisconnected(info);
        _transport?.HandleDisconnectedFromServer(info);
    }

    public override void OnMessage(
        IntPtr data,
        int size,
        long messageNum,
        long recvTime,
        int channel
    )
    {
        _transport?.HandleMessageReceived(data, size, messageNum, recvTime, channel);
    }
}
