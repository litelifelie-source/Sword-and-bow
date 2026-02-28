using System;
using System.Collections.Generic;
using UnityEngine;

public class QuestManager : MonoBehaviour
{
    public static QuestManager I { get; private set; }

    [Header("Registry")]
    public List<QuestLine> allQuests = new();

    [Header("Runtime")]
    public List<QuestLine> activeQuests = new();

    [Header("Reward (optional)")]
    public RewardManager rewardManager;

    [Header("Dialogue Rule")]
    [Tooltip("대사 종료(success=true) 시 자동으로 다음 Step으로 넘길지 (기본 OFF 권장)")]
    public bool autoAdvanceOnDialogueComplete = false;

    // ✅✅✅ (복구) startKey + bridge 참조
    [Header("Start Dialogue Node")]
    [Tooltip("퀘스트 시작 직후 재생할 노드 키 (예: S0 또는 START)")]
    public string startNodeKey = "S0";

    [Header("Dialogue Bridge (optional)")]
    public QuestDialogueBridge dialogueBridge;

    [Header("Debug")]
    public bool verboseLog = true;

    // ✅ Bridge가 구독하는 이벤트 (questId, stepIndex)
    public event Action<string, int> OnStepIndexChanged;

    // --- dialogue context ---
    private string _dialogueQuestId = null;
    private bool _dialogueInFlight = false;

    private void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;

        HookAllQuests();

        if (!rewardManager) rewardManager = RewardManager.I ? RewardManager.I : FindFirstObjectByType<RewardManager>();

        // ✅✅✅ bridge 자동 바인딩
        if (!dialogueBridge) dialogueBridge = FindFirstObjectByType<QuestDialogueBridge>();

        if (verboseLog)
            Debug.Log("[QM] Awake", this);
    }

    private void OnEnable()
    {
        HookAllQuests();
    }

    private void OnDisable()
    {
        UnhookAllQuests();
    }

    // ------------------------------------------------------------
    // Quest lookup
    // ------------------------------------------------------------
    public QuestLine GetQuest(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return null;
        return allQuests.Find(q => q != null && q.questId == questId);
    }

    // ------------------------------------------------------------
    // StepId helpers (for QuestFlowDirector, etc.)
    // ------------------------------------------------------------

    /// <summary>
    /// stepId -> index 변환. 없으면 -1.
    /// </summary>
    public int GetStepIndex(string questId, string stepId)
    {
        if (string.IsNullOrEmpty(questId) || string.IsNullOrEmpty(stepId))
            return -1;

        var q = GetQuest(questId);
        if (q == null || q.steps == null) return -1;

        for (int i = 0; i < q.steps.Count; i++)
        {
            var s = q.steps[i];
            if (s == null) continue;
            if (string.Equals(s.stepId, stepId, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    /// <summary>
    /// index -> stepId 변환. 유효하지 않으면 null.
    /// </summary>
    public string GetStepId(string questId, int index)
    {
        if (string.IsNullOrEmpty(questId)) return null;

        var q = GetQuest(questId);
        if (q == null || q.steps == null) return null;
        if (index < 0 || index >= q.steps.Count) return null;

        var s = q.steps[index];
        return s != null ? s.stepId : null;
    }

    // ------------------------------------------------------------
    // Start / Stop
    // ------------------------------------------------------------
    public bool TryStartQuestById(string questId, bool resetSteps = true)
    {
        var q = GetQuest(questId);
        return TryStartQuest(q, resetSteps);
    }

    public bool TryStartQuest(QuestLine q, bool resetSteps = true)
    {
        if (!q) return false;

        if (q.IsCompleted)
        {
            if (verboseLog) Debug.Log($"[QM] Start blocked (completed) questId={q.questId}", q);
            return false;
        }

        if (q.IsActive)
        {
            if (verboseLog) Debug.Log($"[QM] Start ignored (already active) questId={q.questId}", q);
            return true;
        }

        bool started = q.StartQuest(resetSteps);
        if (!started) return false;

        if (!activeQuests.Contains(q))
            activeQuests.Add(q);

        if (verboseLog)
            Debug.Log($"[QM] Quest started questId={q.questId} stepIndex={q.CurrentStepIndex} stepId={q.CurrentStep?.stepId}", q);

        // ✅✅✅ (복구) 시작 직후 startNodeKey 재생
        // - StepChanged로 S0가 바로 나오면 중복 재생될 수 있어서 "같은 키면 스킵" 안전장치
        if (dialogueBridge != null && !string.IsNullOrEmpty(startNodeKey))
        {
            string curKey = q.CurrentStep != null ? q.CurrentStep.stepId : null;
            if (string.IsNullOrEmpty(curKey)) curKey = $"S{q.CurrentStepIndex}";

            // startNodeKey가 현재 스텝 키와 다를 때만 강제 재생
            if (!string.Equals(curKey, startNodeKey, StringComparison.Ordinal))
                dialogueBridge.PlayStart(q.questId, startNodeKey);
        }

        return true;
    }

    public void StopQuestById(string questId)
    {
        var q = GetQuest(questId);
        if (q) StopQuest(q);
    }

    public void StopQuest(QuestLine q)
    {
        if (!q) return;

        q.StopQuest();

        if (activeQuests.Contains(q))
            activeQuests.Remove(q);

        if (verboseLog)
            Debug.Log($"[QM] Quest stopped questId={q.questId}", q);
    }

    // ------------------------------------------------------------
    // Events -> QuestLine.OnEvent 라우팅
    // ------------------------------------------------------------
    public void PushEvent(QuestEventType type, string key = null, int value = 1)
    {
        var e = new QuestEvent(type, key, value);
        DispatchEventToActiveQuests(e);

        if (verboseLog)
            Debug.Log($"[QM] PushEvent type={type} key={key} value={value} activeQuests={activeQuests.Count}", this);
    }

    private void DispatchEventToActiveQuests(in QuestEvent e)
    {
        if (activeQuests == null || activeQuests.Count == 0) return;

        for (int i = activeQuests.Count - 1; i >= 0; i--)
        {
            var q = activeQuests[i];
            if (!q)
            {
                activeQuests.RemoveAt(i);
                continue;
            }

            q.OnEvent(e);

            if (!q.IsActive || q.IsCompleted)
            {
                if (activeQuests.Contains(q))
                    activeQuests.Remove(q);
            }
        }
    }

    // ------------------------------------------------------------
    // ✅ Bridge callbacks
    // ------------------------------------------------------------
    public void NotifyDialogueStarted(string questId)
    {
        _dialogueQuestId = questId;
        _dialogueInFlight = true;

        if (verboseLog)
            Debug.Log($"[QM] NotifyDialogueStarted questId={questId}", this);
    }

    public void NotifyDialogueCompleted(bool success)
    {
        if (verboseLog)
            Debug.Log($"[QM] NotifyDialogueCompleted success={success} questId={_dialogueQuestId}", this);

        if (!_dialogueInFlight)
            return;

        string qid = _dialogueQuestId;

        _dialogueQuestId = null;
        _dialogueInFlight = false;

        if (!success) return;

        PushEvent(QuestEventType.DialogueEnded, qid, 1);

        if (autoAdvanceOnDialogueComplete && !string.IsNullOrEmpty(qid))
        {
            var q = GetQuest(qid);
            if (q != null)
            {
                q.ForceAdvanceNext();
            }
        }
    }

    // ------------------------------------------------------------
    // Hook QuestLine -> Relay to Bridge
    // ------------------------------------------------------------
    private void HookAllQuests()
    {
        if (allQuests == null) return;

        foreach (var q in allQuests)
        {
            if (!q) continue;

            q.OnStarted -= OnQuestStarted;
            q.OnStepChanged -= OnQuestStepChanged;
            q.OnCompleted -= OnQuestCompleted;
            q.OnStopped -= OnQuestStopped;
            q.OnRewardFired -= OnQuestRewardFired;

            q.OnStarted += OnQuestStarted;
            q.OnStepChanged += OnQuestStepChanged;
            q.OnCompleted += OnQuestCompleted;
            q.OnStopped += OnQuestStopped;
            q.OnRewardFired += OnQuestRewardFired;
        }
    }

    private void UnhookAllQuests()
    {
        if (allQuests == null) return;

        foreach (var q in allQuests)
        {
            if (!q) continue;

            q.OnStarted -= OnQuestStarted;
            q.OnStepChanged -= OnQuestStepChanged;
            q.OnCompleted -= OnQuestCompleted;
            q.OnStopped -= OnQuestStopped;
            q.OnRewardFired -= OnQuestRewardFired;
        }
    }

    private void OnQuestStarted(QuestLine q)
    {
        if (!q) return;

        if (!activeQuests.Contains(q))
            activeQuests.Add(q);

        if (verboseLog)
            Debug.Log($"[QM] OnQuestStarted questId={q.questId} idx={q.CurrentStepIndex} stepId={q.CurrentStep?.stepId}", q);
    }

    private void OnQuestStepChanged(QuestLine q, int newIndex)
    {
        if (!q) return;

        if (verboseLog)
            Debug.Log($"[QM] Relay StepChanged -> Bridge questId={q.questId} index={newIndex} stepId={q.CurrentStep?.stepId}", q);

        OnStepIndexChanged?.Invoke(q.questId, newIndex);
    }

    private void OnQuestRewardFired(QuestLine q, string reasonKey, RewardBundle bundle)
    {
        if (!q) return;
        if (bundle == null || bundle.IsEmpty) return;

        if (!rewardManager) rewardManager = RewardManager.I ? RewardManager.I : FindFirstObjectByType<RewardManager>();
        if (rewardManager)
        {
            string ctx = $"{q.questId}:{reasonKey}";
            rewardManager.Give(bundle, ctx);
        }

        if (verboseLog)
            Debug.Log($"[QM] RewardFired questId={q.questId} reason={reasonKey} bundle={bundle}", q);
    }

    private void OnQuestCompleted(QuestLine q)
    {
        if (!q) return;

        if (activeQuests.Contains(q))
            activeQuests.Remove(q);

        if (verboseLog)
            Debug.Log($"[QM] OnQuestCompleted questId={q.questId}", q);
    }

    private void OnQuestStopped(QuestLine q)
    {
        if (!q) return;

        if (activeQuests.Contains(q))
            activeQuests.Remove(q);

        if (verboseLog)
            Debug.Log($"[QM] OnQuestStopped questId={q.questId}", q);
    }
}