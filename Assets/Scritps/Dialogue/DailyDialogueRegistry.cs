using System.Collections.Generic;
using UnityEngine;

public class DailyDialogueRegistry : MonoBehaviour
{
    [Header("Daily Packs")]
    public List<DailyDialoguePack> dailyPacks = new();

    private Dictionary<string, DailyDialoguePack> _map;

    private void Awake()
    {
        _map = new Dictionary<string, DailyDialoguePack>();

        foreach (var pack in dailyPacks)
        {
            if (pack == null || string.IsNullOrEmpty(pack.questId))
                continue;

            if (!_map.ContainsKey(pack.questId))
                _map.Add(pack.questId, pack);
        }
    }

    public DailyDialoguePack GetPack(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        _map.TryGetValue(id, out var pack);
        return pack;
    }
}