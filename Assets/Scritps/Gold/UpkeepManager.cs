using TMPro;
using UnityEngine;

public class UpkeepManager : MonoBehaviour
{
    [Header("Tick Interval")]
    [Tooltip("15분 = 900초")]
    public float tickInterval = 15f * 60f;

    [Header("Refs")]
    public Gold gold;

    [Header("UI")]
    public TMP_Text countdownTMP; // 흰색
    public TMP_Text goldTMP;      // 노란색
    public TMP_Text upkeepTMP;    // 빨간색

    [Header("Upkeep Recalc")]
    [Tooltip("유지비(빨간색) 재계산 주기(초). 0이면 매 프레임 재계산")]
    public float recalcInterval = 1f;

    private float remain;
    private float recalcRemain;
    private int cachedTotalUpkeep;

    private void Awake()
    {
        if (gold == null) gold = FindFirstObjectByType<Gold>();
    }

    private void Start()
    {
        remain = tickInterval;
        recalcRemain = 0f;

        RefreshGoldUI();
        RecalcAndRefreshUpkeepUI(); // 시작 시 1회
        RefreshCountdownUI();
    }

    private void Update()
    {
        // 1) 타이머 감소
        remain -= Time.deltaTime;

        // 2) 유지비 합계(빨간색) 갱신
        if (recalcInterval <= 0f)
        {
            RecalcAndRefreshUpkeepUI(); // 매 프레임
        }
        else
        {
            recalcRemain -= Time.deltaTime;
            if (recalcRemain <= 0f)
            {
                RecalcAndRefreshUpkeepUI();
                recalcRemain = recalcInterval;
            }
        }

        // 3) 0되면 정산
        if (remain <= 0f)
        {
            PayUpkeepOnce();
            remain = tickInterval;
        }

        // 4) 카운트다운 표시
        RefreshCountdownUI();
    }

    private void PayUpkeepOnce()
    {
        // 00:00 시점의 유지비(캐시값)를 사용
        int total = cachedTotalUpkeep;

        if (total > 0 && gold != null)
            gold.TrySpend(total);

        RefreshGoldUI();
        // 지불 직후에도 빨간값 갱신(혹시 네임드가 죽었거나 전환됐을 수 있음)
        RecalcAndRefreshUpkeepUI();
    }

    private void RecalcAndRefreshUpkeepUI()
    {
        var providers = FindObjectsByType<UpkeepProvider>(FindObjectsSortMode.None);

        int sum = 0;
        foreach (var p in providers)
        {
            if (p == null) continue;
            sum += p.GetUpkeep();
        }

        cachedTotalUpkeep = Mathf.Max(0, sum);

        if (upkeepTMP != null)
            upkeepTMP.text = cachedTotalUpkeep > 0 ? $"(-{cachedTotalUpkeep})" : "(0)";
    }

    private void RefreshCountdownUI()
    {
        if (countdownTMP == null) return;

        int s = Mathf.CeilToInt(Mathf.Max(0f, remain));
        int m = s / 60;
        int r = s % 60;

        countdownTMP.text = $"{m:00}:{r:00}";
    }

    private void RefreshGoldUI()
    {
        if (goldTMP == null || gold == null) return;
        goldTMP.text = gold.Current.ToString();
    }
}
