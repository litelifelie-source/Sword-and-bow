using System.Collections.Generic;
using UnityEngine;

// ✅ 타겟(Transform.root) 기준으로 에이전트(Ally/Enemy)가 "포위 슬롯"을 나눠 갖게 함
public static class SurroundCoordinator
{
    private class Group
    {
        public readonly List<int> ids = new();
        public readonly Dictionary<int, Vector2> slotPosById = new();
        public float nextRecalcTime;
    }

    private static readonly Dictionary<Transform, Group> groups = new();

    private const float RECALC_INTERVAL = 0.12f;
    private const int RING_CAPACITY = 6;   // 링 당 최대 인원
    private const float RING_STEP = 1.15f; // 링 간격

    public static Vector2 GetSlotPosition(Transform targetRoot, int agentId, float baseRadius)
    {
        if (targetRoot == null) return Vector2.zero;

        CleanupDeadGroups();

        if (!groups.TryGetValue(targetRoot, out Group g))
        {
            g = new Group();
            groups[targetRoot] = g;
        }

        if (!g.ids.Contains(agentId))
        {
            g.ids.Add(agentId);
            g.ids.Sort(); // ✅ 슬롯 떨림 최소화
        }

        if (Time.time >= g.nextRecalcTime || !g.slotPosById.ContainsKey(agentId))
        {
            g.nextRecalcTime = Time.time + RECALC_INTERVAL;
            RecalcGroupSlots(targetRoot, g, baseRadius);
        }

        if (g.slotPosById.TryGetValue(agentId, out Vector2 pos))
            return pos;

        RecalcGroupSlots(targetRoot, g, baseRadius);
        return g.slotPosById.TryGetValue(agentId, out pos) ? pos : (Vector2)targetRoot.position;
    }

    public static void RemoveAgent(Transform targetRoot, int agentId)
    {
        if (targetRoot == null) return;
        if (!groups.TryGetValue(targetRoot, out Group g)) return;

        g.ids.Remove(agentId);
        g.slotPosById.Remove(agentId);

        if (g.ids.Count == 0)
            groups.Remove(targetRoot);
    }

    private static void RecalcGroupSlots(Transform targetRoot, Group g, float baseRadius)
    {
        if (targetRoot == null || !targetRoot.gameObject.activeInHierarchy) return;

        Vector2 center = targetRoot.position;
        float baseAngle = 0f; // 필요하면 "타겟->플레이어" 기준으로 바꿀 수 있음

        g.slotPosById.Clear();

        int total = g.ids.Count;
        for (int i = 0; i < total; i++)
        {
            int ring = i / RING_CAPACITY;
            int slot = i % RING_CAPACITY;

            int slotsInRing = Mathf.Min(RING_CAPACITY, total - ring * RING_CAPACITY);
            float radius = Mathf.Max(0.8f, baseRadius) + ring * RING_STEP;

            float t = (slotsInRing <= 1) ? 0f : (slot / (float)slotsInRing);
            float angle = baseAngle + t * Mathf.PI * 2f;

            Vector2 pos = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            g.slotPosById[g.ids[i]] = pos;
        }
    }

    private static void CleanupDeadGroups()
    {
        if (groups.Count == 0) return;

        List<Transform> dead = null;
        foreach (var kv in groups)
        {
            Transform t = kv.Key;
            if (t == null || !t.gameObject.activeInHierarchy)
            {
                dead ??= new List<Transform>();
                dead.Add(t);
            }
        }

        if (dead != null)
        {
            for (int i = 0; i < dead.Count; i++)
                groups.Remove(dead[i]);
        }
    }
}
