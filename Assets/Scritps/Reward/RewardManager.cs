using System;
using UnityEngine;

public class RewardManager : MonoBehaviour
{
    public static RewardManager I { get; private set; }

    [Header("Refs (optional)")]
    public Gold gold;                 // Gold.cs 컴포넌트
    public QuestManager questManager;  // QuestEvent 보상용

    [Header("Debug")]
    [Tooltip("ON이면 보상 적용 로그를 출력합니다. (보상은 보통 1회성이라 스팸이 거의 없습니다)")]
    public bool verboseLog = true;

    /// <summary>보상 적용 후 외부가 후처리(팝업/UI/사운드)할 수 있는 훅</summary>
    public event Action<string, RewardEntry> OnRewardApplied;

    private void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;

        if (!gold) gold = FindFirstObjectByType<Gold>();
        if (!questManager) questManager = QuestManager.I ? QuestManager.I : FindFirstObjectByType<QuestManager>();

        if (verboseLog)
            Debug.Log($"[RewardManager] Awake gold={(gold ? gold.name : "null")} qm={(questManager ? questManager.name : "null")}", this);
    }

    /// <summary>
    /// RewardBundle을 실제로 적용합니다.
    /// context: 로그/추적용 문자열 (예: "JEANNE_RECRUIT:QUEST:COMPLETE")
    /// </summary>
    public void Give(RewardBundle bundle, string context = "")
    {
        if (bundle == null || bundle.entries == null || bundle.entries.Count == 0)
            return;

        for (int i = 0; i < bundle.entries.Count; i++)
        {
            var e = bundle.entries[i];
            if (e == null || !e.IsValid()) continue;
            ApplyEntry(e, context);
        }
    }

    private void ApplyEntry(RewardEntry e, string context)
    {
        switch (e.type)
        {
            case RewardType.Gold:
                {
                    if (!gold) gold = FindFirstObjectByType<Gold>();
                    if (gold) gold.Add(e.amount);
                    break;
                }

            case RewardType.QuestEvent:
                {
                    if (!questManager) questManager = QuestManager.I ? QuestManager.I : FindFirstObjectByType<QuestManager>();
                    if (questManager)
                    {
                        // key를 그대로 QuestEvent의 key로 사용합니다.
                        questManager.PushEvent(QuestEventType.Reward, e.key, Mathf.Max(1, e.value));
                    }
                    break;
                }

            case RewardType.RecruitAllyBySpeakerId:
                {
                    ApplyRecruitAllyBySpeakerId(e.key, context, e);
                    break;
                }
        }

        if (verboseLog)
            Debug.Log($"[RewardManager] Apply context={context} entry={e}", this);

        OnRewardApplied?.Invoke(context, e);
    }

    // =====================================================
    // ✅ NEW: Recruit Ally
    // =====================================================
    private void ApplyRecruitAllyBySpeakerId(string speakerId, string context, RewardEntry entry)
    {
        if (string.IsNullOrEmpty(speakerId))
            return;

        // 비활성 오브젝트 포함해서 탐색 (씬 전환/비활성 대기 상태 대비)
        var tags = GameObject.FindObjectsByType<SpeakerIdTag>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        SpeakerIdTag found = null;
        for (int i = 0; i < tags.Length; i++)
        {
            var t = tags[i];
            if (t == null) continue;
            if (string.Equals(t.speakerId, speakerId, StringComparison.Ordinal))
            {
                found = t;
                break;
            }
        }

        if (found == null)
        {
            Debug.LogWarning($"[RewardManager] RecruitAllyBySpeakerId FAIL context={context} speakerId='{speakerId}' reason=SpeakerIdTag_not_found", this);
            return;
        }

        // 1) ModeSwitcher 우선 (행동 스크립트 토글까지 가장 안전하게 보장)
        var switcher = found.GetComponentInParent<NamedUnitModeSwitcher>();
        if (switcher != null)
        {
            switcher.Apply(UnitMode.Ally);
            if (verboseLog)
                Debug.Log($"[RewardManager] RecruitAllyBySpeakerId OK (ModeSwitcher) speakerId='{speakerId}' obj='{found.name}'", found);
            return;
        }

        // 2) fallback: UnitTeam 직접 전환
        var ut = found.GetComponentInParent<UnitTeam>();
        if (ut != null)
        {
            ut.ConvertToAlly();
            if (verboseLog)
                Debug.Log($"[RewardManager] RecruitAllyBySpeakerId OK (UnitTeam) speakerId='{speakerId}' obj='{found.name}'", found);
            return;
        }

        Debug.LogWarning($"[RewardManager] RecruitAllyBySpeakerId FAIL context={context} speakerId='{speakerId}' reason=no_ModeSwitcher_or_UnitTeam", found);
    }
}