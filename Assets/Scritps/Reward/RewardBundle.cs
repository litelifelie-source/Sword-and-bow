using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RewardBundle
{
    public List<RewardEntry> entries = new();

    public bool IsEmpty => entries == null || entries.Count == 0;

    public override string ToString()
    {
        if (IsEmpty) return "(empty)";
        return string.Join(", ", entries);
    }
}
