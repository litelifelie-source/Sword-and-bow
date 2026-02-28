using System.Collections.Generic;
using UnityEngine;

public class QuestDialogueRegistry : MonoBehaviour
{
    public static QuestDialogueRegistry I { get; private set; }

    [Header("All Quest Packs")]
    public List<QuestDialoguePack> packs = new();

    private readonly Dictionary<string, QuestDialoguePack> _byQuestId = new();

    private void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        RebuildIndex();
    }

    [ContextMenu("Rebuild Index")]
    public void RebuildIndex()
    {
        _byQuestId.Clear();

        for (int i = 0; i < packs.Count; i++)
        {
            var p = packs[i];
            if (!p) continue;
            if (string.IsNullOrEmpty(p.questId)) continue;

            _byQuestId[p.questId] = p;
        }
    }

    public QuestDialoguePack GetPack(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return null;
        _byQuestId.TryGetValue(questId, out var p);
        return p;
    }
}