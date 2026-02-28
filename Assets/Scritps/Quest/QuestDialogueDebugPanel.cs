using UnityEngine;

public class QuestDialogueDebugPanel : MonoBehaviour
{
    [Header("Hotkeys")]
    public KeyCode keyLogQuestState = KeyCode.F7;
    public KeyCode keyTracePack = KeyCode.F8;

    [Header("Target Quest")]
    public string questId = "JEANNE_RECRUIT";

    [Header("Optional Refs (비우면 자동 탐색)")]
    public QuestManager questManager;
    public QuestDialogueRegistry registry;
    public QuestDialogueBridge bridge;

    [Header("UI")]
    public bool showPanel = true;
    public Rect panelRect = new Rect(15, 15, 520, 220);

    [Header("Debug")]
    public bool verbose = true;

    private string _lastMsg = "";
    private float _lastMsgTime;

    private void Awake()
    {
        if (questManager == null) questManager = FindFirstObjectByType<QuestManager>();
        if (registry == null) registry = FindFirstObjectByType<QuestDialogueRegistry>();
        if (bridge == null) bridge = FindFirstObjectByType<QuestDialogueBridge>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(keyLogQuestState))
            LogQuestState();

        if (Input.GetKeyDown(keyTracePack))
            TracePackConnection();
    }

    private void OnGUI()
    {
        if (!showPanel) return;
        panelRect = GUI.Window(GetInstanceID(), panelRect, DrawWindow, "Quest / Dialogue Debug");
    }

    private void DrawWindow(int id)
    {
        GUILayout.Label("QuestId");
        questId = GUILayout.TextField(questId);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button($"Log Quest State ({keyLogQuestState})")) LogQuestState();
        if (GUILayout.Button($"Trace Pack ({keyTracePack})")) TracePackConnection();
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        if (!string.IsNullOrEmpty(_lastMsg))
        {
            float dt = Time.unscaledTime - _lastMsgTime;
            GUILayout.Label(dt < 3f ? $"(recent) {_lastMsg}" : _lastMsg);
        }

        GUI.DragWindow();
    }

    // --------------------------
    // 현재 퀘스트 상태 로그
    // --------------------------
    public void LogQuestState()
    {
        var q = ResolveQuest(questId);
        if (q == null)
        {
            SetMsg($"Quest not found: {questId}");
            Debug.LogWarning($"[QDBG] Quest not found questId={questId}", this);
            return;
        }

        string stepId = (q.CurrentStep != null) ? q.CurrentStep.stepId : "(null)";
        string desc = (q.CurrentStep != null) ? q.CurrentStep.description : "(no step)";
        string status = q.IsCompleted ? "COMPLETED" : (q.IsActive ? "ACTIVE" : "INACTIVE");

        Debug.Log($"[QDBG] questId={q.questId} status={status} stepIndex={q.CurrentStepIndex} stepId={stepId} desc='{desc}'", q);

        // 브릿지가 실제로 틀 "키" 예측 (최종 브릿지 규칙: inactive=first, active=current, completed=last)
        string willPlayKey = PredictBridgeKey(q);
        Debug.Log($"[QDBG] BridgePlayPredict questId={q.questId} -> key='{willPlayKey}'", q);

        SetMsg($"[{status}] idx={q.CurrentStepIndex} cur={stepId} (predict='{willPlayKey}')");
    }

    // --------------------------
    // 대사팩 연결 추적
    // --------------------------
    public void TracePackConnection()
    {
        var q = ResolveQuest(questId);
        if (q == null)
        {
            SetMsg($"Quest not found: {questId}");
            Debug.LogWarning($"[QDBG] TracePack: quest not found questId={questId}", this);
            return;
        }

        if (registry == null)
        {
            SetMsg("Registry not found");
            Debug.LogWarning("[QDBG] TracePack: QuestDialogueRegistry not found", this);
            return;
        }

        var pack = registry.GetPack(q.questId);
        if (pack == null)
        {
            SetMsg($"Pack not found for {q.questId}");
            Debug.LogWarning($"[QDBG] Pack not found questId={q.questId} (registry packs count={registry.packs?.Count ?? 0})", registry);
            return;
        }

        Debug.Log($"[QDBG] Pack OK questId={q.questId} pack='{pack.name}' pack.questId='{pack.questId}' nodes={pack.nodes?.Count ?? 0}", pack);

        // 브릿지 규칙 기반 키들
        string firstKey = GetFirstStepKey(q);
        string curKey = (q.CurrentStep != null) ? q.CurrentStep.stepId : null;
        string lastKey = GetLastStepKey(q);
        string predictedKey = PredictBridgeKey(q);

        // 1) FIRST / CURRENT / LAST 체크
        if (!string.IsNullOrEmpty(firstKey)) CheckNode(pack, firstKey, label: "FIRST");
        else Debug.LogWarning($"[QDBG] FIRST key is null (no steps?) questId={q.questId}", q);

        if (!string.IsNullOrEmpty(curKey)) CheckNode(pack, curKey, label: "CURRENT");
        else Debug.LogWarning($"[QDBG] CURRENT key is null (CurrentStep null?) questId={q.questId}", q);

        if (!string.IsNullOrEmpty(lastKey)) CheckNode(pack, lastKey, label: "LAST");
        else Debug.LogWarning($"[QDBG] LAST key is null (no steps?) questId={q.questId}", q);

        // 2) 예측 키가 위 3개와 다르면 추가 체크
        if (!string.IsNullOrEmpty(predictedKey) &&
            predictedKey != firstKey &&
            predictedKey != curKey &&
            predictedKey != lastKey)
        {
            CheckNode(pack, predictedKey, label: "PREDICT");
        }

        SetMsg($"Pack='{pack.name}' first='{firstKey}' cur='{curKey}' last='{lastKey}' predict='{predictedKey}'");
    }

    private void CheckNode(QuestDialoguePack pack, string key, string label)
    {
        var node = pack.FindNode(key);

        if (node == null)
        {
            Debug.LogWarning($"[QDBG] Node MISSING [{label}] key='{key}' pack='{pack.name}'", pack);
            return;
        }

        int lines = (node.main != null && node.main.Count > 0) ? node.main.Count : ((node.lines != null) ? node.lines.Length : 0);

        if (lines <= 0)
        {
            Debug.LogWarning($"[QDBG] Node EMPTY [{label}] key='{key}' speaker='{node.speaker}' (lines=0) pack='{pack.name}'", pack);
            return;
        }

        Debug.Log($"[QDBG] Node OK [{label}] key='{key}' speaker='{node.speaker}' lines={lines} playOnce={node.playOnlyOnce} played={node.played}", pack);
    }

    private QuestLine ResolveQuest(string id)
    {
        if (questManager == null) questManager = FindFirstObjectByType<QuestManager>();
        if (questManager == null || questManager.allQuests == null) return null;

        return questManager.allQuests.Find(x => x != null && x.questId == id);
    }

    // 최종 브릿지 로직 기준 예측:
    // - completed -> last step
    // - inactive  -> first step
    // - active    -> current step
    private string PredictBridgeKey(QuestLine q)
    {
        if (q == null) return null;

        if (q.IsCompleted)
            return GetLastStepKey(q);

        if (!q.IsActive)
            return GetFirstStepKey(q);

        var step = q.CurrentStep;
        return step != null ? step.stepId : null;
    }

    private string GetFirstStepKey(QuestLine q)
    {
        if (q == null || q.steps == null || q.steps.Count == 0) return null;
        var s = q.steps[0];
        return s != null ? s.stepId : null;
    }

    private string GetLastStepKey(QuestLine q)
    {
        if (q == null || q.steps == null || q.steps.Count == 0) return null;
        var s = q.steps[q.steps.Count - 1];
        return s != null ? s.stepId : null;
    }

    private void SetMsg(string s)
    {
        _lastMsg = s;
        _lastMsgTime = Time.unscaledTime;
        if (verbose) Debug.Log($"[QDBG] {s}", this);
    }
}