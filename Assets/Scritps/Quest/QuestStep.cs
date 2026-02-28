using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[Serializable]
public class QuestStep
{
    [Header("Meta")]
    public string stepId;
    [TextArea(2, 6)] public string description;

    
    // =========================
    // Rewards (optional)
    // - '도중 보상'을 스텝 단위로 넣고 싶을 때 사용
    // - 지급 타이밍은 QuestLine이 '스텝 변경' 순간에 처리합니다.
    // =========================
    [Header("Rewards (optional)")]
    public RewardBundle rewardOnEnter;
    public RewardBundle rewardOnExit;

    [Header("Reward Runtime (auto)")]
    [NonSerialized] public bool enterRewardFired;
    [NonSerialized] public bool exitRewardFired;

// =========================
    // Legacy single condition
    // =========================
    [Header("Condition (Legacy)")]
    public QuestEventType listenType;
    public string listenKey;       // 비우면 key 무시
    public int requiredAmount = 1; // 목표치

    // =========================
    // Multi requirements (new)
    // =========================
    [Header("Multi Requirements (비어있으면 Legacy 사용)")]
    public List<Requirement> requirements = new List<Requirement>();

    [Serializable]
    public class Requirement
    {
        [Header("Condition")]
        public QuestEventType listenType;
        public string listenKey;        // 비우면 key 무시
        public int requiredAmount = 1;  // 목표치

        [Header("Runtime (auto)")]
        [SerializeField] private int currentAmount;
        [SerializeField] private bool isCompleted;

        public bool IsCompleted => isCompleted;
        public int CurrentAmount => currentAmount;

        public void ResetRuntime()
        {
            currentAmount = 0;
            isCompleted = false;
        }

        public void OnEvent(in QuestEvent e)
        {
            if (isCompleted) return;
            if (e.type != listenType) return;

            if (!string.IsNullOrEmpty(listenKey) &&
                !string.Equals(listenKey, e.key, StringComparison.Ordinal))
                return;

            currentAmount += Mathf.Max(1, e.value);

            if (currentAmount >= requiredAmount)
                isCompleted = true;
        }

        public string GetProgressText()
        {
            if (requiredAmount <= 1) return isCompleted ? "완료" : "진행중";
            return $"{Mathf.Min(currentAmount, requiredAmount)}/{requiredAmount}";
        }
    }

    // =========================
    // Runtime (auto)
    // =========================
    [Header("Runtime (auto)")]
    [SerializeField] private int currentAmount;
    [SerializeField] private bool isCompleted;

    public bool IsCompleted => isCompleted;
    public int CurrentAmount => currentAmount;

    private bool UseMulti => requirements != null && requirements.Count > 0;

    /// <summary>QuestLine이 이 Step을 '현재 Step'으로 채택할 때 1회 호출</summary>
    public virtual void Enter()
    {
        // 필요하면 여기서 연출/로그/사운드 트리거용 훅으로 확장 가능
    }

    /// <summary>QuestLine이 Step을 넘길 때 1회 호출</summary>
    public virtual void Exit()
    {
        // 필요하면 여기서 정리 훅
    }

    public virtual void ResetRuntime()
    {
        currentAmount = 0;
        isCompleted = false;

        
        enterRewardFired = false;
        exitRewardFired = false;
if (requirements != null)
        {
            for (int i = 0; i < requirements.Count; i++)
                requirements[i]?.ResetRuntime();
        }
    }

    /// <summary>QuestManager -> QuestLine -> 현재 Step에게 들어오는 이벤트</summary>
    public virtual void OnEvent(in QuestEvent e)
    {
        if (isCompleted) return;

        // ✅ 멀티 조건 우선 처리
        if (UseMulti)
        {
            // 이벤트를 requirements에 분배
            for (int i = 0; i < requirements.Count; i++)
            {
                var r = requirements[i];
                if (r == null) continue;
                r.OnEvent(e);
            }

            // 전부 완료면 스텝 완료
            for (int i = 0; i < requirements.Count; i++)
            {
                var r = requirements[i];
                if (r == null) continue;
                if (!r.IsCompleted) return;
            }

            isCompleted = true;
            return;
        }

        // ✅ Legacy 단일 조건
        if (e.type != listenType) return;

        if (!string.IsNullOrEmpty(listenKey) &&
            !string.Equals(listenKey, e.key, StringComparison.Ordinal))
            return;

        currentAmount += Mathf.Max(1, e.value);

        if (currentAmount >= requiredAmount)
            isCompleted = true;
    }

    public virtual string GetProgressText()
    {
        if (UseMulti)
        {
            // 예: Soldier 3/20, Archer 5/20
            var sb = new StringBuilder(64);

            for (int i = 0; i < requirements.Count; i++)
            {
                var r = requirements[i];
                if (r == null) continue;

                if (sb.Length > 0) sb.Append(", ");

                // 키가 비어있으면 타입만 표기
                if (!string.IsNullOrEmpty(r.listenKey)) sb.Append(r.listenKey);
                else sb.Append(r.listenType.ToString());

                sb.Append(' ');
                sb.Append(r.GetProgressText());
            }

            if (sb.Length == 0) return isCompleted ? "완료" : "진행중";
            return sb.ToString();
        }

        if (requiredAmount <= 1) return isCompleted ? "완료" : "진행중";
        return $"{Mathf.Min(currentAmount, requiredAmount)}/{requiredAmount}";
    }
}