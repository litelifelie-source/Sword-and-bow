using System.Collections.Generic;
using UnityEngine;

public class DebtHPDrainManager : MonoBehaviour
{
    [Header("Refs")]
    public Gold gold;

    [Header("Rule")]
    [Tooltip("-100 골드마다 1단계 (예: -100~ -199 = 1단계, -200~ -299 = 2단계)")]
    public int debtStepGold = 100;

    [Tooltip("단계당 1초마다 maxHP의 0.01% 감소 = 0.0001")]
    public float hpPercentPerSecondPerLevel = 0.0001f;

    [Header("Tick")]
    [Tooltip("1초마다 적용")]
    public float tickInterval = 1f;

    // 소수점 누적(HP가 작을 때도 정확히 깎이게)
    private readonly Dictionary<Health, float> damageRemainder = new();

    private float tickRemain;

    private void Awake()
    {
        if (gold == null) gold = FindFirstObjectByType<Gold>();
        tickRemain = tickInterval;
    }

    private void Update()
    {
        if (gold == null) return;

        tickRemain -= Time.deltaTime;
        if (tickRemain > 0f) return;
        tickRemain += tickInterval;

        int debtLevel = GetDebtLevel(gold.Current);
        if (debtLevel <= 0) return;

        ApplyDrainToNamedAllies(debtLevel);
    }

    private int GetDebtLevel(int currentGold)
    {
        if (currentGold > -debtStepGold) return 0; // -1~-99는 0단계
        // 예: -100 => 1, -199 => 1, -200 => 2
        return Mathf.FloorToInt((-currentGold) / (float)debtStepGold);
    }

    private void ApplyDrainToNamedAllies(int debtLevel)
    {
        // 네임드만 = UpkeepProvider가 붙은 애들만
        var providers = FindObjectsByType<UpkeepProvider>(FindObjectsSortMode.None);

        float levelMultiplier = debtLevel;

        foreach (var p in providers)
        {
            if (p == null) continue;

            // 네임드/아군만 깎고 싶으면: UpkeepProvider의 필터(onlyWhenAlly)가 이미 걸려있음
            // 단, GetUpkeep()이 0이어도(유지비 0 설정) 네임드로 간주하고 싶으면 아래처럼 직접 팀 체크가 더 안전함.
            var team = p.GetComponent<UnitTeam>();
            if (team != null && team.team != Team.Ally) continue;

            var h = p.GetComponent<Health>();
            if (h == null) continue;
            if (h.IsDead) continue;

            // 1초당 감소량(소수)
            float dmgFloat = h.maxHP * hpPercentPerSecondPerLevel * levelMultiplier;

            if (!damageRemainder.TryGetValue(h, out float rem))
                rem = 0f;

            rem += dmgFloat;

            // Health.TakeDamage가 int라서 정수로 변환하되,
            // 소수점은 remainder에 누적해서 장기적으로 정확히 깎이게 함.
            int dmgInt = Mathf.FloorToInt(rem);
            if (dmgInt > 0)
            {
                h.TakeDamage(dmgInt);
                rem -= dmgInt;
            }

            damageRemainder[h] = rem;
        }
    }
}
