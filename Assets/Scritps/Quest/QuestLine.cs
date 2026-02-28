using System;
using System.Collections.Generic;
using UnityEngine;
using StackTrace = System.Diagnostics.StackTrace;

public class QuestLine : MonoBehaviour
{
    [Header("Identity")]
    public string questId;
    public string questName;

    [Header("Steps")]
    public List<QuestStep> steps = new();

    [Header("Rewards (optional)")]
    public RewardBundle questCompletionReward;

    [Header("Runtime State")]
    [SerializeField] private bool isActive;
    [SerializeField] private bool isCompleted;
    [SerializeField] private int currentStepIndex = -1;

    [SerializeField] private bool completionRewardFired;

    public bool IsActive => isActive;
    public bool IsCompleted => isCompleted;
    public int CurrentStepIndex => currentStepIndex;

    public QuestStep CurrentStep =>
        (steps != null && currentStepIndex >= 0 && currentStepIndex < steps.Count)
            ? steps[currentStepIndex]
            : null;

    public event Action<QuestLine> OnStarted;
    public event Action<QuestLine, int> OnStepChanged;
    public event Action<QuestLine> OnCompleted;
    public event Action<QuestLine> OnStopped;
    public event Action<QuestLine, string, RewardBundle> OnRewardFired;

    // =====================================================
    // 🔥 Advance Debug Trace
    // =====================================================
    [Header("Debug - Advance Trace")]
    public bool debugAdvanceTrace = true;
    public bool debugAdvanceIncludeStack = true;

    void ADV_Log(string tag, string reason, string src, int from, int to)
    {
        if (!debugAdvanceTrace) return;

        string stepId = CurrentStep != null ? CurrentStep.stepId : "null";
        string msg =
            $"[QL-ADV] {tag} questId={questId} active={isActive} completed={isCompleted} " +
            $"stepId={stepId} from={from} to={to} src={src} reason={reason}";

        if (debugAdvanceIncludeStack)
            UnityEngine.Debug.Log(msg + "\n" + new StackTrace(2, true), this);
        else
            UnityEngine.Debug.Log(msg, this);
    }

    bool ADV_CanAdvance(int from, int to, out string reason)
    {
        if (!isActive) { reason = "quest_not_active"; return false; }
        if (isCompleted) { reason = "quest_completed"; return false; }
        if (steps == null) { reason = "steps_null"; return false; }
        if (steps.Count == 0) { reason = "steps_empty"; return false; }
        if (from < 0 || from >= steps.Count) { reason = "current_index_out_of_range"; return false; }

        // ✅ 치명 수정:
        // to == steps.Count 는 "마지막 스텝 다음" = 퀘스트 완료로 넘어가는 합법 상태라 허용합니다.
        // (기존: to >= steps.Count 면 실패라서 마지막 스텝에서 절대 CompleteQuest로 못 갔음)
        if (to < 0 || to > steps.Count) { reason = "next_index_out_of_range"; return false; }

        reason = "ok";
        return true;
    }

    // =====================================================
    // Lifecycle
    // =====================================================

    public bool StartQuest(bool resetSteps = true)
    {
        if (isCompleted)
        {
            ADV_Log("FAIL_START", "already_completed", "StartQuest", currentStepIndex, currentStepIndex);
            return false;
        }

        if (resetSteps && steps != null)
            foreach (var s in steps)
                s?.ResetRuntime();

        completionRewardFired = false;

        isActive = true;
        isCompleted = false;
        currentStepIndex = 0;

        ADV_Log("OK_START", "started", "StartQuest", -1, 0);

        CurrentStep?.Enter();
        TryFireStepEnterReward(CurrentStep);

        OnStarted?.Invoke(this);
        OnStepChanged?.Invoke(this, currentStepIndex);

        return true;
    }

    public void StopQuest()
    {
        if (!isActive)
        {
            ADV_Log("FAIL_STOP", "not_active", "StopQuest", currentStepIndex, currentStepIndex);
            return;
        }

        CurrentStep?.Exit();
        isActive = false;

        OnStopped?.Invoke(this);
        ADV_Log("OK_STOP", "stopped", "StopQuest", currentStepIndex, currentStepIndex);
    }

    public void OnEvent(in QuestEvent e)
    {
        if (!isActive || isCompleted)
        {
            ADV_Log("FAIL_EVENT", (!isActive ? "not_active" : "completed"), "OnEvent", currentStepIndex, currentStepIndex);
            return;
        }

        var step = CurrentStep;
        if (step == null)
        {
            ADV_Log("FAIL_EVENT", "CurrentStep_null", "OnEvent", currentStepIndex, currentStepIndex);
            CompleteQuest();
            return;
        }

        step.OnEvent(e);

        if (step.IsCompleted)
        {
            ADV_Log("TRY_ADV", "step_completed", "OnEvent", currentStepIndex, currentStepIndex + 1);
            AdvanceStep("OnEvent");
        }
    }

    public bool ForceAdvanceNext(string expectedCurrentStepId = null)
    {
        int from = currentStepIndex;
        int to = from + 1;

        ADV_Log("TRY_FORCE", "called", "ForceAdvanceNext", from, to);

        if (!string.IsNullOrEmpty(expectedCurrentStepId))
        {
            var cur = CurrentStep != null ? CurrentStep.stepId : null;
            if (!string.Equals(cur, expectedCurrentStepId, StringComparison.Ordinal))
            {
                ADV_Log("FAIL_FORCE", $"expected_mismatch cur={cur}", "ForceAdvanceNext", from, to);
                return false;
            }
        }

        bool ok = AdvanceStep("ForceAdvanceNext");
        if (!ok)
            ADV_Log("FAIL_FORCE", "AdvanceStep_failed", "ForceAdvanceNext", from, to);

        return ok;
    }

    bool AdvanceStep(string src)
    {
        int from = currentStepIndex;
        int to = from + 1;

        ADV_Log("TRY_ADV", "called", src, from, to);

        if (!ADV_CanAdvance(from, to, out string reason))
        {
            ADV_Log("FAIL_ADV", reason, src, from, to);
            return false;
        }

        TryFireStepExitReward(CurrentStep);
        CurrentStep?.Exit();

        currentStepIndex++;

        if (currentStepIndex >= steps.Count)
        {
            ADV_Log("OK_ADV", "complete", src, from, currentStepIndex);
            CompleteQuest();
            return true;
        }

        CurrentStep?.Enter();
        TryFireStepEnterReward(CurrentStep);

        OnStepChanged?.Invoke(this, currentStepIndex);

        ADV_Log("OK_ADV", "advanced", src, from, currentStepIndex);
        return true;
    }

    void CompleteQuest()
    {
        if (isCompleted) return;

        CurrentStep?.Exit();
        TryFireQuestCompletionReward();

        isCompleted = true;
        isActive = false;

        UnityEngine.Debug.Log($"[Quest] Completed: {questName} ({questId})", this);
        OnCompleted?.Invoke(this);

        ADV_Log("OK_COMPLETE", "completed", "CompleteQuest", currentStepIndex, currentStepIndex);
    }

    // =====================================================
    // Reward helpers
    // =====================================================

    void TryFireStepEnterReward(QuestStep step)
    {
        if (step == null || step.enterRewardFired || step.rewardOnEnter == null || step.rewardOnEnter.IsEmpty) return;
        step.enterRewardFired = true;
        OnRewardFired?.Invoke(this, $"{step.stepId}:ENTER", step.rewardOnEnter);
    }

    void TryFireStepExitReward(QuestStep step)
    {
        if (step == null || step.exitRewardFired || step.rewardOnExit == null || step.rewardOnExit.IsEmpty) return;
        step.exitRewardFired = true;
        OnRewardFired?.Invoke(this, $"{step.stepId}:EXIT", step.rewardOnExit);
    }

    void TryFireQuestCompletionReward()
    {
        if (completionRewardFired || questCompletionReward == null || questCompletionReward.IsEmpty) return;
        completionRewardFired = true;
        OnRewardFired?.Invoke(this, "QUEST:COMPLETE", questCompletionReward);
    }
}