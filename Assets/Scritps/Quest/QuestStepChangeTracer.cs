using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// QuestLine의 Step 변경을 추적해서
/// "누가(step 변경을 트리거한 호출자) 바꿨는지" 콜스택으로 역추적하는 스크립트.
/// - OnStarted / OnStepChanged / OnCompleted / OnStopped 훅
/// - stepId 이전/이후, index, questId, 오브젝트명, 콜스택 출력
/// </summary>
public class QuestStepChangeTracer : MonoBehaviour
{
    [Header("Targets")]
    public QuestManager questManager;          // 비우면 자동 탐색
    public bool hookOnStart = true;            // Start에서 자동 훅

    [Header("Logging")]
    public bool logStarted = true;
    public bool logCompleted = true;
    public bool logStopped = true;

    [Tooltip("스텝 변경 시 콜스택을 출력합니다(가장 중요)")]
    public bool logStackTraceOnStepChanged = true;

    [Tooltip("콜스택이 너무 길면 앞부분만 잘라냅니다(0이면 전체 출력)")]
    public int maxStackChars = 4000;

    [Tooltip("퀘스트 오브젝트가 Destroy/비활성화 되면 자동으로 훅 대상에서 제외")]
    public bool autoCleanDeadRefs = true;

    // questId -> last stepId 기록 (변경 전/후 비교용)
    private readonly Dictionary<string, string> _lastStepId = new();

    // 훅한 퀘스트 기억 (중복 훅 방지)
    private readonly HashSet<QuestLine> _hooked = new();

    private void Awake()
    {
        if (!questManager) questManager = FindFirstObjectByType<QuestManager>();
        if (!questManager)
            Debug.LogWarning("[StepTracer] QuestManager not found. Assign it in inspector.", this);
    }

    private void Start()
    {
        if (hookOnStart) HookAll();
    }

    [ContextMenu("Hook All Quests Now")]
    public void HookAll()
    {
        if (!questManager || questManager.allQuests == null)
        {
            Debug.LogWarning("[StepTracer] HookAll failed: questManager/allQuests null", this);
            return;
        }

        foreach (var q in questManager.allQuests)
        {
            if (!q) continue;
            HookQuest(q);
        }

        Debug.Log($"[StepTracer] HookAll OK. hooked={_hooked.Count}", this);
    }

    private void Update()
    {
        if (!autoCleanDeadRefs) return;

        // Destroy된 참조가 있으면 정리 (선택)
        _hooked.RemoveWhere(q => q == null);
    }

    private void HookQuest(QuestLine q)
    {
        if (!q) return;
        if (_hooked.Contains(q)) return;

        // 중복 방지 위해 먼저 제거 후 등록
        q.OnStarted -= OnQuestStarted;
        q.OnStepChanged -= OnQuestStepChanged;
        q.OnCompleted -= OnQuestCompleted;
        q.OnStopped -= OnQuestStopped;

        q.OnStarted += OnQuestStarted;
        q.OnStepChanged += OnQuestStepChanged;
        q.OnCompleted += OnQuestCompleted;
        q.OnStopped += OnQuestStopped;

        _hooked.Add(q);

        // 초기 상태 저장
        var cur = q.CurrentStep != null ? q.CurrentStep.stepId : null;
        _lastStepId[q.questId] = cur;

        Debug.Log($"[StepTracer] Hooked questId={q.questId}, obj={q.name}, curIndex={q.CurrentStepIndex}, curStepId={cur}", q);
    }

    private void OnQuestStarted(QuestLine q)
    {
        if (!logStarted || !q) return;

        var cur = q.CurrentStep != null ? q.CurrentStep.stepId : null;
        _lastStepId[q.questId] = cur;

        Debug.Log($"[StepTracer] START questId={q.questId} active={q.IsActive} completed={q.IsCompleted} index={q.CurrentStepIndex} stepId={cur}", q);
    }

    private void OnQuestCompleted(QuestLine q)
    {
        if (!logCompleted || !q) return;

        var cur = q.CurrentStep != null ? q.CurrentStep.stepId : null;
        Debug.Log($"[StepTracer] COMPLETE questId={q.questId} index={q.CurrentStepIndex} stepId={cur}", q);
    }

    private void OnQuestStopped(QuestLine q)
    {
        if (!logStopped || !q) return;

        var cur = q.CurrentStep != null ? q.CurrentStep.stepId : null;
        Debug.Log($"[StepTracer] STOP questId={q.questId} index={q.CurrentStepIndex} stepId={cur}", q);
    }

    private void OnQuestStepChanged(QuestLine q, int newIndex)
    {
        if (!q) return;

        string before = null;
        _lastStepId.TryGetValue(q.questId, out before);

        string after = (q.CurrentStep != null) ? q.CurrentStep.stepId : null;
        _lastStepId[q.questId] = after;

        Debug.Log(
            $"[StepTracer] STEP CHANGED questId={q.questId} obj={q.name}\n" +
            $"    beforeStepId={before} -> afterStepId={after}\n" +
            $"    newIndex={newIndex} curIndex={q.CurrentStepIndex} active={q.IsActive} completed={q.IsCompleted}",
            q
        );

        if (logStackTraceOnStepChanged)
        {
            var st = Environment.StackTrace;

            if (maxStackChars > 0 && st.Length > maxStackChars)
                st = st.Substring(0, maxStackChars) + "\n... (truncated)";

            Debug.Log("[StepTracer] CALLSTACK:\n" + st, q);
        }
    }
}