using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

public class DebugManager : MonoBehaviour
{
    public static DebugManager Instance { get; private set; }

    [Header("General")]
    [Tooltip("If true, card summaries are logged once on game start.")]
    public bool logCardsOnStart = true;

    [Tooltip("Optional prefix to help filter these logs in the console.")]
    public string logPrefix = "[CardAudit]";

    [Tooltip("If true, also copy the full card report to the system clipboard on log.")]
    public bool copyToClipboard = true;

    [Tooltip("If true, also write the full card report to a text file for offline audit.")]
    public bool writeToFile = true;

    [Tooltip(
        "File name (inside Application.persistentDataPath) to write when writeToFile is true."
    )]
    public string outputFileName = "CardAudit.txt";

    [Header("Card Sources")]
    [Tooltip(
        "Primary source of card assets. If not assigned, card logging will be skipped gracefully."
    )]
    public DeckManager deckManager;

    [Header("Status Sources")]
    [Tooltip("Library of status definitions to include in the debug report.")]
    public StatusDefinitionLibrary statusDefinitionLibrary;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        if (!logCardsOnStart)
            return;

        if (deckManager == null)
            deckManager = DeckManager.Instance;

        LogGameData();
    }

    public void LogGameData()
    {
        var sb = new StringBuilder(4096);

        bool hasSection = false;

        // Cards
        if (deckManager == null || deckManager.allCards == null || deckManager.allCards.Count == 0)
        {
            sb.AppendLine($"{logPrefix} No deckManager or allCards configured; skipping card log.");
        }
        else
        {
            var creatures = new List<CreatureCard>();
            var effects = new List<EffectCard>();

            foreach (var so in deckManager.allCards)
            {
                if (so == null)
                    continue;

                if (so is CreatureCard cc && !creatures.Contains(cc))
                    creatures.Add(cc);
                else if (so is EffectCard ec && !effects.Contains(ec))
                    effects.Add(ec);
            }

            AppendCreatureCards(creatures, sb);
            sb.AppendLine();
            AppendEffectCards(effects, sb);
            hasSection = true;
        }

        // Statuses
        if (
            statusDefinitionLibrary == null
            || statusDefinitionLibrary.entries == null
            || statusDefinitionLibrary.entries.Count == 0
        )
        {
            sb.AppendLine(
                $"{logPrefix} No statusDefinitionLibrary or entries configured; skipping status log."
            );
        }
        else
        {
            if (hasSection)
                sb.AppendLine();

            AppendStatuses(sb);
            hasSection = true;
        }

        string report = sb.ToString();
        if (string.IsNullOrWhiteSpace(report))
        {
            Debug.Log($"{logPrefix} Game data report is empty.");
            return;
        }

        // Single big log entry so it can be copied at once
        Debug.Log(report);

        if (copyToClipboard)
        {
            GUIUtility.systemCopyBuffer = report;
            Debug.Log($"{logPrefix} Full game data report copied to clipboard.");
        }

        if (writeToFile)
        {
            try
            {
                string fileName = string.IsNullOrEmpty(outputFileName)
                    ? "GameDataAudit.txt"
                    : outputFileName;
                string path = Path.Combine(Application.persistentDataPath, fileName);
                File.WriteAllText(path, report);
                Debug.Log($"{logPrefix} Game data report written to file: {path}");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"{logPrefix} Failed to write game data report file: {ex}");
            }
        }
    }

    // Backwards-compatible entry point if anything else still calls this
    public void LogAllCards()
    {
        LogGameData();
    }

    void AppendCreatureCards(List<CreatureCard> creatures, StringBuilder sb)
    {
        if (creatures == null || creatures.Count == 0)
        {
            sb.AppendLine($"{logPrefix} No creature cards found.");
            return;
        }

        sb.AppendLine($"{logPrefix} === CREATURE CARDS ({creatures.Count}) ===");

        foreach (var c in creatures.OrderBy(c => c.momentumCost).ThenBy(c => c.cardName))
        {
            if (c == null)
                continue;

            string name = string.IsNullOrEmpty(c.cardName) ? "(Unnamed Creature)" : c.cardName;
            string dinoName = string.IsNullOrEmpty(c.dinosaurName) ? "" : $" ({c.dinosaurName})";

            string header =
                $"{logPrefix} [CREATURE] {name}{dinoName} | Type={c.type} | COST={c.momentumCost}";
            string stats = $"HP={c.maxHealth} | SPD={c.speed} | SIZE={c.size}";

            string traitBlock = "";
            if (c.baseTraits != null && c.baseTraits.Length > 0)
            {
                var parts = new List<string>();
                foreach (var t in c.baseTraits)
                {
                    if (t == null)
                        continue;
                    string tName = string.IsNullOrEmpty(t.traitName) ? t.name : t.traitName;
                    string tDesc = string.IsNullOrEmpty(t.description)
                        ? "(no description)"
                        : t.description;
                    parts.Add($"{tName}: {tDesc}");
                }
                if (parts.Count > 0)
                    traitBlock = "Traits: " + string.Join(" | ", parts);
            }
            else
            {
                traitBlock = "Traits: (none)";
            }

            sb.AppendLine(header);
            sb.AppendLine($"{logPrefix}    {stats}");
            sb.AppendLine($"{logPrefix}    {traitBlock}");
        }
    }

    void AppendEffectCards(List<EffectCard> effects, StringBuilder sb)
    {
        if (effects == null || effects.Count == 0)
        {
            sb.AppendLine($"{logPrefix} No effect cards found.");
            return;
        }

        sb.AppendLine($"{logPrefix} === EFFECT CARDS ({effects.Count}) ===");

        foreach (var e in effects.OrderBy(e => e.minEraAllowed).ThenBy(e => e.effectName))
        {
            if (e == null)
                continue;

            string name = string.IsNullOrEmpty(e.effectName) ? "(Unnamed Effect)" : e.effectName;
            string header = $"{logPrefix} [EFFECT] {name} | COST={e.momentumCost}";

            // High-level targeting summary
            string targeting =
                $"Targeting: Side={e.targetSide} | Type={e.targetType} | Count={e.targetCount}"
                + (e.isGlobal ? " | GLOBAL" : "")
                + (e.multiSelect ? $" | MultiSelect(max={e.maxTargets})" : "");

            string desc = string.IsNullOrEmpty(e.description) ? "(no description)" : e.description;

            // Trait effect descriptions (EffectTraitBase)
            string traitBlock = "";
            if (e.traitsToAttachToTargets != null && e.traitsToAttachToTargets.Length > 0)
            {
                var parts = new List<string>();
                foreach (var t in e.traitsToAttachToTargets)
                {
                    if (t == null)
                        continue;
                    string tName = string.IsNullOrEmpty(t.traitName) ? t.name : t.traitName;
                    parts.Add($"{tName}");
                }
                if (parts.Count > 0)
                    traitBlock = "EffectTraits: " + string.Join(" | ", parts);
            }
            else
            {
                traitBlock = "EffectTraits: (none)";
            }

            sb.AppendLine(header);
            sb.AppendLine($"{logPrefix}    {targeting}");
            sb.AppendLine($"{logPrefix}    Text: {desc}");
            sb.AppendLine($"{logPrefix}    {traitBlock}");
        }
    }

    void AppendStatuses(StringBuilder sb)
    {
        if (
            statusDefinitionLibrary == null
            || statusDefinitionLibrary.entries == null
            || statusDefinitionLibrary.entries.Count == 0
        )
        {
            sb.AppendLine($"{logPrefix} No status definitions found.");
            return;
        }

        sb.AppendLine(
            $"{logPrefix} === STATUS DEFINITIONS ({statusDefinitionLibrary.entries.Count}) ==="
        );

        foreach (var e in statusDefinitionLibrary.entries.OrderBy(e => e.displayName))
        {
            string name = string.IsNullOrEmpty(e.displayName) ? e.tag.ToString() : e.displayName;
            string desc = string.IsNullOrEmpty(e.description) ? "(no description)" : e.description;

            sb.AppendLine($"{logPrefix} [STATUS] {name} ({e.tag})");
            sb.AppendLine($"{logPrefix}    {desc}");
        }
    }
}
