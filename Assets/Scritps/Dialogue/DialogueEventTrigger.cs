using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using StackTrace = System.Diagnostics.StackTrace;

public class DialogueEventTrigger : MonoBehaviour
{
    // =====================================================
    // Trigger Mode
    // - ✅ RangeAutoTrigger 유지
    // - ✅ Manual 강화
    // - ❌ OnEnterTrigger 제거
    // - ❌ OnInteractInRange 제거
    // =====================================================
    public enum TriggerMode
    {
        RangeAutoTrigger,
        Manual
    }

    // =====================================================
    // Manual Condition Engine
    // =====================================================
    public enum ConditionCombine { All, Any }

    [Serializable]
    public class RuleCondition
    {
        public enum Type
        {
            AlwaysTrue = 0,

            [Tooltip("로컬 bool 플래그를 체크합니다.")]
            LocalBoolFlag = 10,

            [Tooltip("확률(0~1)로 통과합니다.")]
            RandomChance = 20,
        }

        [Tooltip("조건 타입")]
        public Type type = Type.AlwaysTrue;

        [Tooltip("LocalBoolFlag 타입일 때 사용할 키")]
        public string key;

        [Tooltip("LocalBoolFlag 타입일 때 기대값")]
        public bool expectedBool = true;

        [Tooltip("RandomChance 타입일 때 확률(0~1)")]
        [Range(0f, 1f)] public float chance01 = 1f;
    }


    // =====================================================
    // RangeAutoTrigger Completion Conditions
    // - 특정 (questId, nodeKey)가 'played'인지 검사
    // =====================================================
    [Serializable]
    public class NodeCompletionCondition
    {
        [Tooltip("검사할 DailyDialoguePack.questId 입니다.\n비워두면: 이 TriggerRule이 재생할 프리셋의 questId를 사용합니다.")]
        public string questId;

        [Tooltip("검사할 노드 키(DailyDialoguePack.Node.key) 입니다.")]
        public string nodeKey;

        public enum RequiredState
        {
            [Tooltip("해당 노드가 아직 played=false 이어야 통과합니다.")]
            NotPlayed = 0,

            [Tooltip("해당 노드가 played=true 이어야 통과합니다.")]
            Played = 1,
        }

        [Tooltip("요구되는 완료 상태입니다.")]
        public RequiredState requiredState = RequiredState.Played;

        [Tooltip("questId/노드가 존재하지 않을 때 통과시킬지 여부입니다. (보통은 false 권장)")]
        public bool treatMissingAsPass = false;
    }
    [Serializable]
    public struct StringBoolPair
    {
        [Tooltip("플래그 키")]
        public string key;

        [Tooltip("플래그 값")]
        public bool value;
    }

    [Header("Manual Condition State")]
    [Tooltip("Manual 조건(LocalBoolFlag)에서 참조하는 로컬 플래그 저장소입니다.")]
    public List<StringBoolPair> localFlags = new();

    [Header("Manual Condition Optimization")]
    [Tooltip("ON이면: localFlags(List)를 Dictionary로 캐싱해서 LocalBoolFlag 조회를 O(1)로 최적화합니다.\n- Inspector에서 localFlags를 수정하면 OnValidate/Awake 시점에 캐시가 갱신됩니다.")]
    public bool useLocalFlagMapCache = true;

    // 런타임 캐시: LocalBoolFlag 빠른 조회
    Dictionary<string, bool> _localFlagMap;

    bool GetLocalFlag(string key, out bool v)
    {
        v = false;
        if (string.IsNullOrEmpty(key)) return false;
        if (localFlags == null) return false;

        // ✅ 최적화: Dictionary 캐시 사용
        if (useLocalFlagMapCache && _localFlagMap != null)
        {
            if (_localFlagMap.TryGetValue(key, out v)) return true;
            return false;
        }

        for (int i = 0; i < localFlags.Count; i++)
        {
            if (string.Equals(localFlags[i].key, key, StringComparison.Ordinal))
            {
                v = localFlags[i].value;
                return true;
            }
        }
        return false;
    }

    void RebuildLocalFlagMap()
    {
        if (!useLocalFlagMapCache)
        {
            _localFlagMap = null;
            return;
        }

        if (_localFlagMap == null) _localFlagMap = new Dictionary<string, bool>(32, StringComparer.Ordinal);
        else _localFlagMap.Clear();

        if (localFlags == null) return;
        for (int i = 0; i < localFlags.Count; i++)
        {
            var k = localFlags[i].key;
            if (string.IsNullOrEmpty(k)) continue;
            _localFlagMap[k] = localFlags[i].value;
        }
    }

    // =====================================================
    // Trigger Rules (Multi)
    // =====================================================
    [Serializable]
    public class TriggerRule
    {
        [Tooltip("이 규칙을 사용할지 여부")]
        public bool enabled = true;

        [Tooltip("이 규칙이 만족되면 재생할 Pack Preset 인덱스")]
        [Min(0)] public int presetIndex = 0;

        [Tooltip("대화 시작 트리거 방식")]
        public TriggerMode triggerMode = TriggerMode.RangeAutoTrigger;

        [Header("Once / Repeat")]
        [Tooltip("이 규칙을 한 번만 발동할지 여부 (대화 완료 여부(isCompleted)와는 별개로, 이 규칙의 발동만 1회 제한합니다.)")]
        public bool triggerOnce = true;

        [Tooltip("대화가 완료(isCompleted)되어도 다시 발동을 허용할지")]
        public bool allowRepeatAfterComplete = false;

        [Header("Range Auto Trigger")]
        [Tooltip("RangeAutoTrigger 모드에서 사용할 타겟 (보통 Player). 비워두면 Tag로 자동 탐색합니다.")]
        public Transform rangeTarget;

        [Tooltip("rangeTarget이 비었을 때 찾을 Tag")]
        public string rangeTargetTag = "Player";

        [Tooltip("이 거리 이내에 들어오면 자동 시작")]
        [Min(0f)] public float autoTriggerRange = 3f;

        [Tooltip("연속 발동 방지용 쿨다운(초). 0이면 쿨다운 없음")]
        [Min(0f)] public float cooldownSeconds = 0f;

        [Header("Chain / Completion Gate")]
        [Tooltip("ON이면: 이 트리거 컴포넌트가 isCompleted=true 상태일 때도 룰 발동을 허용합니다.\n- 체인 재생(연속 룰 발동) 구조에서는 ON을 권장합니다.\n- OFF면 기존처럼 완료 상태에서 룰이 막힐 수 있습니다.")]
        public bool allowFireWhenTriggerCompleted = true;



        [Header("Range Auto Completion Conditions")]
        [Tooltip("RangeAutoTrigger 모드에서만 사용하는 '완주(played) 상태' 조건 목록입니다.\n비어있으면 조건 없이 통과합니다.\n각 항목은 (questId, nodeKey)의 played 여부를 검사합니다.")]
        public List<NodeCompletionCondition> completionConditions = new();

        [Tooltip("completionConditions 조합 방식입니다. All=전부 만족, Any=하나라도 만족.")]
        public ConditionCombine completionCombine = ConditionCombine.All;
        [Header("Conditions")]
        [Tooltip("ON이면: DialogueManager가 재생 중일 때(=다이얼로그 UI 열려있을 때) 이 규칙의 발동을 막습니다.")]
        public bool blockIfDialogueManagerPlaying = true;

        [Tooltip("ON이면: 대화가 이미 active일 때는 발동을 막습니다.")]
        public bool blockIfAlreadyActive = true;

        [Header("Manual Conditions")]
        [Tooltip("이 규칙이 발동되기 위한 조건 목록입니다. 비어있으면 조건 없이 발동됩니다.")]
        public List<RuleCondition> conditions = new();

        [Tooltip("조건 조합 방식입니다. All=전부 만족, Any=하나라도 만족.")]
        public ConditionCombine combine = ConditionCombine.All;

        [Header("Runtime (Debug)")]
        [SerializeField, Tooltip("마지막으로 발동된 시각(Time.time). 디버그용")]
        private float _lastFiredTime = -999999f;

        // ✅ completionConditions 디버그 로그 최적화용 (런타임 전용)
        [NonSerialized] public bool _ccLastResult;
        [NonSerialized] public bool _ccHasLast;
        [NonSerialized] public float _ccLastLogTime;

        public bool IsCooldownReady()
        {
            if (cooldownSeconds <= 0f) return true;
            return Time.time >= _lastFiredTime + cooldownSeconds;
        }

        public void MarkFiredNow()
        {
            _lastFiredTime = Time.time;
        }
    }

    [Header("Trigger Rules (Multi)")]
    [Tooltip("여러 트리거 규칙을 한 컴포넌트에 등록합니다.\n각 규칙은 triggerMode/조건/발동 1회 제한/재생할 대사팩 프리셋(presetIndex)까지 독립적으로 설정됩니다.\n(100개 이상도 List로 관리 가능)")]
    public List<TriggerRule> triggerRules = new();

    // =====================================================
    // Dialogue Source
    // =====================================================
    [Header("Dialogue Source")]
    [Tooltip("✅ 데일리 레지스트리. questId 키로 DailyDialoguePack을 찾습니다.\n(비워두면 씬에서 DailyDialogueRegistry를 자동 탐색합니다.)")]
    public DailyDialogueRegistry dailyRegistry;

    [Tooltip("✅ 데일리 레지스트리 배열(선택).\nDailyDialogueRegistry가 여러 개면 여기에 넣고 순서대로 검색합니다.")]
    public DailyDialogueRegistry[] dailyRegistries;

    // -----------------------------------------------------
    // ✅ Quest Packs (NEW)
    // -----------------------------------------------------
    [Header("Quest Dialogue Source")]
    [Tooltip("✅ 퀘스트 레지스트리. questId 키로 QuestDialoguePack을 찾습니다.\n(비워두면 씬에서 QuestDialogueRegistry를 자동 탐색합니다.)")]
    public QuestDialogueRegistry questRegistry;

    [Tooltip("✅ 퀘스트 레지스트리 배열(선택).\nQuestDialogueRegistry가 여러 개면 여기에 넣고 순서대로 검색합니다.")]
    public QuestDialogueRegistry[] questRegistries;

    [Tooltip("✅ 레지스트리가 없거나 못 찾을 때의 로컬 fallback 리스트입니다.\n(권장: QuestDialogueRegistry.packs만 사용)")]
    public System.Collections.Generic.List<QuestDialoguePack> questPacks = new();


    [Tooltip("(기본) DailyDialoguePack.questId (데일리팩 '찾기 키'로 사용).\nPack Presets가 비어있을 때만 사용됩니다.")]
    public string questId;

    [Tooltip("(기본) 시작 노드 키 (DailyDialoguePack.Node.key).\nPack Presets가 비어있을 때만 사용됩니다.")]
    public string startNodeKey = "S1_T1";

    [Serializable]
    public class PackPreset
    {
        public enum PackType
        {
            [Tooltip("DailyDialoguePack을 재생합니다.")]
            Daily = 0,

            [Tooltip("QuestDialoguePack을 재생합니다.")]
            Quest = 1,
        }

        [Tooltip("이 프리셋이 사용할 Pack 타입입니다. (Daily/Quest)")]
        public PackType packType = PackType.Daily;

        [Tooltip("Pack의 questId 입니다. (DailyDialoguePack.questId / QuestDialoguePack.questId)")]
        public string questId;

        [Tooltip("이 프리셋의 시작 노드 키")]
        public string startNodeKey = "S1_T1";
    }

    [Header("Pack Presets (Optional)")]
    [Tooltip("여러 대사팩을 한 트리거에 등록하고 필요할 때 선택해서 재생합니다.\n(100개 이상도 List로 관리 가능)")]
    public List<PackPreset> packPresets = new();

    [Tooltip("자동 시작(TriggerStart) 시 사용할 프리셋 인덱스.\npackPresets가 비어있으면 기본 questId/startNodeKey를 사용합니다.")]
    public int defaultPresetIndex = 0;

    // =====================================================
    // Node Flow
    // =====================================================
    [Header("Node Flow")]
    [Tooltip("노드에 nextKey/nextNodeKey/next/nextNode가 없을 때, 'S1_T1 -> S1_T2' 같은 규칙으로 자동 진행할지")]
    public bool enableAutoIncrementKey = false;

    [Tooltip("자동 증가 규칙에서 증가시킬 숫자 폭 (보통 1)")]
    public int autoIncrementStep = 1;

    [Tooltip("Node.playOnlyOnce=true인데 이미 played면, 이 노드를 스킵하고 다음으로 넘길지")]
    public bool skipPlayedOnceNodes = true;

    // =====================================================
    // Panel Selection
    // =====================================================
    [Header("Target Panel (Forced)")]
    [Tooltip("✅ 강제 출력 패널입니다. (useInputMask=true일 땐 maskPriorityDialogueUI가 더 우선일 수 있습니다.)")]
    public DialogueUI targetDialogueUI;

    [Header("Mask Panel (Priority)")]
    [Tooltip("✅ useInputMask=true일 때 최우선으로 사용할 DialogueUI 패널입니다. (마스크 배치 전용 패널)")]
    public DialogueUI maskPriorityDialogueUI;

    // =====================================================
    // Playback
    // =====================================================
    [Header("Playback")]
    [Tooltip("DialogueManager. (트리거 차단/재생중 판정용) 비워두면 DialogueManager.I를 사용합니다.")]
    public DialogueManager dialogueManager;

    // =====================================================
    // Input Gate / Mask Options
    // =====================================================
    [Header("Advance Input Gate")]
    [Tooltip("ON이면: 대사가 닫혀도 자동으로 다음 노드로 안 넘어가고, 사용자가 키 입력을 해야만 진행합니다.")]
    public bool requireUserAdvanceInput = false;

    [Tooltip("requireUserAdvanceInput=true일 때, 다음 노드 진행 키")]
    public KeyCode advanceKey = KeyCode.Space;

    [Header("Mask Options")]
    [Tooltip("ON이면: 대화 진행 중 '외부 입력 차단(마스크)'를 걸어야 하는 경우를 위한 옵션입니다. (프로젝트에 마스크 시스템이 있을 때만 연결하세요)")]
    public bool useInputMask = false;

    // =====================================================
    // Runtime State
    // =====================================================
    [Header("Runtime State")]
    [SerializeField] private bool isActive;
    [SerializeField] private bool isCompleted;
    [SerializeField] private string currentNodeKey;
    [SerializeField] private bool pendingAdvance;

    [SerializeField] private List<bool> _ruleTriggeredOnce = new();

    [SerializeField] private string _runtimeQuestId;
    [SerializeField] private int _activePresetIndex = -1;


    private DailyDialoguePack _runtimeDailyPack;
    private QuestDialoguePack _runtimeQuestPack;

    [SerializeField] private PackPreset.PackType _runtimePackType = PackPreset.PackType.Daily;


    public bool IsActive => isActive;
    public bool IsCompleted => isCompleted;
    public string CurrentNodeKey => currentNodeKey;

    // =====================================================
    // Events
    // =====================================================
    public event Action<DialogueEventTrigger> OnStarted;
    public event Action<DialogueEventTrigger, string> OnNodeChanged;
    public event Action<DialogueEventTrigger> OnCompleted;
    public event Action<DialogueEventTrigger> OnStopped;

    // =====================================================
    // Advance Debug Trace
    // =====================================================
    [Header("Debug - Advance Trace")]
    public bool debugAdvanceTrace = true;

    [Tooltip("ON이면 로그에 StackTrace를 함께 출력합니다.")]
    public bool debugAdvanceIncludeStack = false;

    [Header("Debug - CompletionCondition Trace")]
    [Tooltip("ON이면: 멀티 룰의 completionConditions(questId, nodeKey, played)를 평가하는 과정을 상세 로그로 출력합니다.")]
    public bool debugCompletionTrace = true;

    [Tooltip("ON이면: completionConditions 로그를 '결과가 바뀔 때'만 1회 출력합니다.\n(연속 디버깅 방지용)\nOFF이면 debugCompletionTraceMinInterval 초마다 반복 출력될 수 있습니다.")]
    public bool debugCompletionTraceOnlyOnChange = true;

    [Tooltip("debugCompletionTraceOnlyOnChange=OFF일 때만 의미가 있습니다.\ncompletionConditions 상세 로그를 최소 이 간격(초)로만 출력합니다.")]
    [Min(0f)] public float debugCompletionTraceMinInterval = 0.75f;

    [Tooltip("ON이면: completionConditions 평가 시 pack/node 누락(missing)도 상세히 출력합니다.")]
    public bool debugCompletionTraceIncludeMissing = true;

    [Tooltip("ON이면: completionConditions 평가 로그에 Registry 검색 경로(dailyRegistries -> dailyRegistry)를 같이 출력합니다.")]
    public bool debugCompletionTraceIncludeRegistryRoute = false;

    // =====================================================
    // Element utilities
    // =====================================================
    private string ResolveElementSpeakerName(DailyDialoguePack.Node node, DailyDialoguePack.LineElement e)
    {
        if (e != null && !string.IsNullOrEmpty(e.speaker)) return e.speaker;
        return node != null && !string.IsNullOrEmpty(node.speaker) ? node.speaker : "NPC";
    }

    private string ResolveElementSpeakerKey(DailyDialoguePack.Node node, DailyDialoguePack.LineElement e)
    {
        if (e != null && !string.IsNullOrEmpty(e.speakerId)) return e.speakerId;
        if (node != null && !string.IsNullOrEmpty(node.speakerId)) return node.speakerId;
        return ResolveElementSpeakerName(node, e);
    }


    private string ResolveQuestElementSpeakerName(QuestDialoguePack.Node node, QuestDialoguePack.LineElement e)
    {
        if (e != null && !string.IsNullOrEmpty(e.speaker)) return e.speaker;
        return node != null && !string.IsNullOrEmpty(node.speaker) ? node.speaker : "NPC";
    }

    private string ResolveQuestElementSpeakerKey(QuestDialoguePack.Node node, QuestDialoguePack.LineElement e)
    {
        if (e != null && !string.IsNullOrEmpty(e.speakerId)) return e.speakerId;
        if (node != null && !string.IsNullOrEmpty(node.speakerId)) return node.speakerId;
        return ResolveQuestElementSpeakerName(node, e);
    }

    // =====================================================
    // ✅ BuildSequenceArrays
    // - Any만 출력
    // - OnlyAlly는 스킵 (동현님 현재 구조 유지)
    // =====================================================
    private void BuildSequenceArrays(
        DailyDialoguePack.Node node,
        out string defaultSpeaker,
        out string defaultSpeakerId,
        out string[] outSpeakers,
        out string[] outSpeakerIds,
        out string[] outLines,
        out float defaultSeconds,
        out float[] perLineSeconds)
    {
        defaultSpeaker = node != null ? node.speaker : "NPC";
        defaultSpeakerId = node != null ? node.speakerId : null;

        defaultSeconds = node != null ? Mathf.Max(0f, node.defaultAutoAdvanceSeconds) : 0f;

        var main = node != null ? node.GetMainResolved() : new List<DailyDialoguePack.LineElement>();
        var seq = new List<DailyDialoguePack.LineElement>();

        for (int i = 0; i < main.Count; i++)
        {
            var e = main[i];
            if (e == null) continue;

            if (e.audience != DailyDialoguePack.LineAudience.Any)
                continue;

            seq.Add(e);
        }

        outLines = new string[seq.Count];
        outSpeakers = new string[seq.Count];
        outSpeakerIds = new string[seq.Count];
        perLineSeconds = new float[seq.Count];

        for (int i = 0; i < seq.Count; i++)
        {
            var e = seq[i];
            var sName = ResolveElementSpeakerName(node, e);
            var sKey = ResolveElementSpeakerKey(node, e);

            outLines[i] = e != null ? e.text : "";
            outSpeakers[i] = sName;
            outSpeakerIds[i] = sKey;

            float sec = 0f;
            if (e != null && e.autoAdvanceSeconds > 0f)
                sec = e.autoAdvanceSeconds;
            else if (node != null && node.perLineAutoAdvanceSeconds != null && i >= 0 && i < node.perLineAutoAdvanceSeconds.Length)
                sec = node.perLineAutoAdvanceSeconds[i];

            if (sec <= 0f) sec = defaultSeconds;
            perLineSeconds[i] = Mathf.Max(0f, sec);
        }
    }



    // =====================================================
    // ✅ BuildSequenceArraysQuest
    // - QuestDialoguePack.Node -> NodeData 변환용
    // - Any만 출력 (OnlyAlly는 스킵: 동현님 현재 구조 유지)
    // - Quest는 autoAdvanceSeconds가 없으므로, UI 자동넘김은 사용하지 않습니다.
    // =====================================================
    private void BuildSequenceArraysQuest(
        QuestDialoguePack.Node node,
        out string defaultSpeaker,
        out string defaultSpeakerId,
        out string[] outSpeakers,
        out string[] outSpeakerIds,
        out string[] outLines)
    {
        defaultSpeaker = node != null ? node.speaker : "NPC";
        defaultSpeakerId = node != null ? node.speakerId : null;

        var main = node != null ? node.GetMainResolved() : new System.Collections.Generic.List<QuestDialoguePack.LineElement>();
        var seq = new System.Collections.Generic.List<QuestDialoguePack.LineElement>();

        for (int i = 0; i < main.Count; i++)
        {
            var e = main[i];
            if (e == null) continue;
            if (e.audience != QuestDialoguePack.LineAudience.Any)
                continue;
            seq.Add(e);
        }

        outLines = new string[seq.Count];
        outSpeakers = new string[seq.Count];
        outSpeakerIds = new string[seq.Count];

        for (int i = 0; i < seq.Count; i++)
        {
            var e = seq[i];
            outLines[i] = e != null ? e.text : "";
            outSpeakers[i] = ResolveQuestElementSpeakerName(node, e);
            outSpeakerIds[i] = ResolveQuestElementSpeakerKey(node, e);
        }
    }
    // =====================================================
    // Debug trace
    // =====================================================
    void ADV_Log(string tag, string reason, string src, string fromKey, string toKey)
    {
        if (!debugAdvanceTrace) return;

        string msg =
            $"[DLG-ADV] {tag} questId={_runtimeQuestId} active={isActive} completed={isCompleted} " +
            $"from={fromKey} to={toKey} src={src} reason={reason}";

        if (debugAdvanceIncludeStack)
            Debug.Log(msg + "\n" + new StackTrace(2, true), this);
        else
            Debug.Log(msg, this);
    }

    void CC_Log(string msg)
    {
        if (!debugCompletionTrace) return;
        Debug.Log("[DLG-CC] " + msg, this);
    }

    void CC_LogMissing(string msg)
    {
        if (!debugCompletionTrace) return;
        if (!debugCompletionTraceIncludeMissing) return;
        Debug.LogWarning("[DLG-CC] " + msg, this);
    }

    bool ADV_CanAdvance(string fromKey, string toKey, out string reason)
    {
        if (!isActive) { reason = "not_active"; return false; }
        if (isCompleted) { reason = "completed"; return false; }
        if (string.IsNullOrEmpty(fromKey)) { reason = "fromKey_empty"; return false; }
        reason = "ok";
        return true;
    }

    // =====================================================
    // Unity Lifecycle
    // =====================================================
    private void Awake()
    {
        if (dialogueManager == null) dialogueManager = DialogueManager.I;

        if (dailyRegistry == null)
            dailyRegistry = FindFirstObjectByType<DailyDialogueRegistry>();

        isActive = false;
        isCompleted = false;
        pendingAdvance = false;
        currentNodeKey = "";
        _runtimeQuestId = "";

        // ✅ 레지스트리 참조가 Awake 시점에 확정되므로, 이전 캐시가 있으면 제거
        _completionNodeCache.Clear();

        RebuildLocalFlagMap();

        SyncRuleRuntimeLists();
    }

    private void OnValidate()
    {
        SyncRuleRuntimeLists();
        RebuildLocalFlagMap();

        // ✅ Inspector에서 registry/questId/nodeKey 등을 바꿨을 때 캐시 무효화
        _completionNodeCache.Clear();
    }

    /// <summary>
    /// completionConditions 평가용 노드 resolve 캐시를 강제로 비웁니다.
    /// - 런타임 중 Registry 교체/팩 동적 로딩이 있는 프로젝트에서 유용합니다.
    /// </summary>
    public void ClearCompletionNodeCache()
    {
        _completionNodeCache.Clear();
    }

    // ✅ + 눌러서 Rule 추가 시 기본값 자동 주입 (enabled + Player tag)
    void SyncRuleRuntimeLists()
    {
        if (triggerRules == null) triggerRules = new List<TriggerRule>();
        if (_ruleTriggeredOnce == null) _ruleTriggeredOnce = new List<bool>();

        int old = _ruleTriggeredOnce.Count;

        if (old < triggerRules.Count)
        {
            for (int i = old; i < triggerRules.Count; i++)
            {
                _ruleTriggeredOnce.Add(false);

                var r = triggerRules[i];
                if (r != null)
                {
                    r.enabled = true;

                    // RangeAuto 기본 Player
                    if (string.IsNullOrEmpty(r.rangeTargetTag))
                        r.rangeTargetTag = "Player";
                }
            }
            return;
        }

        if (old > triggerRules.Count)
        {
            _ruleTriggeredOnce.RemoveRange(triggerRules.Count, old - triggerRules.Count);
            return;
        }
    }

    private void Update()
    {
        if (!Application.isPlaying) return;

        // ✅ Multi TriggerRules: RangeAutoTrigger만 자동 평가
        if (triggerRules != null && triggerRules.Count > 0)
        {
            EvaluateTriggerRules_RangeAutoOnly();
        }

        // 입력 게이트: 닫힌 뒤 사용자 입력으로 다음 진행
        if (isActive && requireUserAdvanceInput && pendingAdvance && Input.GetKeyDown(advanceKey))
        {
            pendingAdvance = false;
            TryAdvance("AdvanceKey");
        }
    }

    void EvaluateTriggerRules_RangeAutoOnly()
    {
        for (int i = 0; i < triggerRules.Count; i++)
        {
            var rule = triggerRules[i];
            if (rule == null || !rule.enabled) continue;

            if (rule.triggerMode != TriggerMode.RangeAutoTrigger)
                continue;

            if (!CanFireRule(i, rule)) continue;   // ✅ 추가 (completed/once/DM playing/active 차단 정상화)
            if (!rule.IsCooldownReady()) continue;
            if (!IsInAutoTriggerRange(rule)) continue;
            if (!EvaluateRangeAutoCompletionConditions(rule)) continue;
            if (!EvaluateConditions(rule)) continue;

            FireRule(i, rule, "RangeAutoTrigger(Update)");
        }
    }

    bool CanFireRule(int index, TriggerRule rule)
    {
        if (rule == null) return false;

        // ✅ 현재 대화 재생 중이면(Active) 룰 발동 차단 (원래 의도 유지)
        if (rule.blockIfAlreadyActive && isActive)
            return false;

        // ✅ 체인 구조에서는 "완료(isCompleted)"로 룰을 막으면 안 됩니다.
        // - triggerOnce / completionConditions / cooldown이 이미 발동 제어를 담당합니다.
        // - 그래도 완료 상태에서 막고 싶으면 allowFireWhenTriggerCompleted를 OFF로 두세요.
        if (isCompleted && !rule.allowFireWhenTriggerCompleted)
            return false;

        // ✅ DialogueManager 재생 중이면 차단 (원래 의도 유지)
        if (rule.blockIfDialogueManagerPlaying && dialogueManager != null && dialogueManager.IsPlaying)
            return false;

        // ✅ 1회 발동 제한(룰 단위) (원래 의도 유지)
        if (rule.triggerOnce)
        {
            SyncRuleRuntimeLists();
            if (index >= 0 && index < _ruleTriggeredOnce.Count && _ruleTriggeredOnce[index])
                return false;
        }

        return true;
    }

    bool EvaluateConditions(TriggerRule rule)
    {
        if (rule == null) return false;
        if (rule.conditions == null || rule.conditions.Count == 0) return true;

        bool anyTrue = false;

        for (int i = 0; i < rule.conditions.Count; i++)
        {
            var c = rule.conditions[i];
            bool ok = EvaluateCondition(c);

            if (rule.combine == ConditionCombine.All)
            {
                if (!ok) return false;
            }
            else
            {
                if (ok) anyTrue = true;
            }
        }

        return rule.combine == ConditionCombine.All ? true : anyTrue;
    }

    bool EvaluateCondition(RuleCondition c)
    {
        if (c == null) return false;

        switch (c.type)
        {
            case RuleCondition.Type.AlwaysTrue:
                return true;

            case RuleCondition.Type.LocalBoolFlag:
                {
                    bool v;
                    if (!GetLocalFlag(c.key, out v)) return false;
                    return v == c.expectedBool;
                }

            case RuleCondition.Type.RandomChance:
                return UnityEngine.Random.value <= Mathf.Clamp01(c.chance01);

            default:
                return false;
        }
    }

    // =====================================================
    // RangeAutoTrigger 전용: (questId, nodeKey) played 기반 조건 평가
    // =====================================================
    bool EvaluateRangeAutoCompletionConditions(TriggerRule rule)
    {
        if (rule == null) return false;
        if (rule.completionConditions == null || rule.completionConditions.Count == 0)
        {
            CC_Log($"pass(no_conditions) presetIndex={rule.presetIndex}");
            return true;
        }

        // completionConditions.questId가 비어있을 때 사용할 기본 questId = 이 규칙이 재생할 preset의 questId
        string defaultQid;
        string _unusedStartKey;
        ResolvePackAndStartKey(rule.presetIndex, out defaultQid, out _unusedStartKey);

        // ✅ 연속 디버그 방지: (1) 결과 변화가 있을 때만 출력하거나 (2) 최소 간격 유지
        bool allowLogThisTime = true;
        if (debugCompletionTrace)
        {
            if (debugCompletionTraceOnlyOnChange && rule._ccHasLast)
            {
                // 일단 평가 후 결과 비교
                allowLogThisTime = false;
            }
            else if (!debugCompletionTraceOnlyOnChange && debugCompletionTraceMinInterval > 0f)
            {
                allowLogThisTime = (Time.time - rule._ccLastLogTime) >= debugCompletionTraceMinInterval;
            }
        }

        if (debugCompletionTrace && allowLogThisTime)
            CC_Log($"BEGIN presetIndex={rule.presetIndex} combine={rule.completionCombine} defaultQid='{defaultQid}' condCount={rule.completionConditions.Count}");

        bool anyTrue = false;

        for (int i = 0; i < rule.completionConditions.Count; i++)
        {
            var c = rule.completionConditions[i];
            if (c == null)
            {
                if (debugCompletionTrace && allowLogThisTime) CC_LogMissing($"cond[{i}] is null -> SKIP");
                continue;
            }

            string qid = string.IsNullOrEmpty(c.questId) ? defaultQid : c.questId;

            // 사전 정보 로그
            if (debugCompletionTrace && allowLogThisTime)
                CC_Log($"cond[{i}] qid='{qid}' nodeKey='{c.nodeKey}' required={c.requiredState} treatMissingAsPass={c.treatMissingAsPass}");

            bool ok = CheckNodeCompletion_DebugCached(
                qid, c.nodeKey, c.requiredState, c.treatMissingAsPass,
                out bool playedValue, out string detail);

            // 결과 로그
            if (debugCompletionTrace && allowLogThisTime)
            {
                if (ok)
                    CC_Log($"cond[{i}] -> PASS played={playedValue} detail={detail}");
                else
                    CC_Log($"cond[{i}] -> FAIL played={playedValue} detail={detail}");
            }

            if (rule.completionCombine == ConditionCombine.All)
            {
                if (!ok)
                {
                    if (debugCompletionTrace && allowLogThisTime) CC_Log($"END -> FAIL(ALL) at cond[{i}]");
                    return false;
                }
            }
            else // Any
            {
                if (ok) anyTrue = true;
            }
        }

        bool result = (rule.completionCombine == ConditionCombine.All) ? true : anyTrue;

        // ✅ 변화 기반 로그(권장)
        if (debugCompletionTrace)
        {
            if (debugCompletionTraceOnlyOnChange)
            {
                if (!rule._ccHasLast || rule._ccLastResult != result)
                {
                    CC_Log($"BEGIN presetIndex={rule.presetIndex} combine={rule.completionCombine} defaultQid='{defaultQid}' condCount={rule.completionConditions.Count}");
                    CC_Log($"END -> {(result ? "PASS" : "FAIL")} combine={rule.completionCombine} anyTrue={anyTrue}");
                    rule._ccLastLogTime = Time.time;
                }
            }
            else if (allowLogThisTime)
            {
                CC_Log($"END -> {(result ? "PASS" : "FAIL")} combine={rule.completionCombine} anyTrue={anyTrue}");
                rule._ccLastLogTime = Time.time;
            }
        }

        rule._ccLastResult = result;
        rule._ccHasLast = true;
        return result;
    }

    // =====================================================
    // CompletionCondition Debug (Cached)
    // - Daily 우선 -> Quest fallback (기존 정책 유지)
    // - Pack/Node resolve 결과를 캐시해서 Update 부하 감소
    // =====================================================
    struct CompletionNodeRef
    {
        public bool valid;
        public bool isDaily;
        public DailyDialoguePack.Node dnode;
        public QuestDialoguePack.Node qnode;
        public string packName;
    }

    struct NodeKey3 : IEquatable<NodeKey3>
    {
        public string questId;
        public string nodeKey;
        public int hint; // 0=unknown, 1=daily, 2=quest

        public bool Equals(NodeKey3 other)
        {
            return hint == other.hint
                && string.Equals(questId, other.questId, StringComparison.Ordinal)
                && string.Equals(nodeKey, other.nodeKey, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            if (obj is NodeKey3 k) return Equals(k);
            return false;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + hint.GetHashCode();
                h = h * 31 + (questId != null ? questId.GetHashCode() : 0);
                h = h * 31 + (nodeKey != null ? nodeKey.GetHashCode() : 0);
                return h;
            }
        }
    }

    readonly Dictionary<NodeKey3, CompletionNodeRef> _completionNodeCache = new Dictionary<NodeKey3, CompletionNodeRef>(256);

    bool CheckNodeCompletion_DebugCached(
        string qid,
        string nodeKey,
        NodeCompletionCondition.RequiredState required,
        bool treatMissingAsPass,
        out bool playedValue,
        out string detail)
    {
        playedValue = false;

        if (string.IsNullOrEmpty(qid) || string.IsNullOrEmpty(nodeKey))
        {
            detail = $"invalid_args(qid_empty={string.IsNullOrEmpty(qid)}, nodeKey_empty={string.IsNullOrEmpty(nodeKey)}) -> {(treatMissingAsPass ? "PASS" : "FAIL")}";
            return treatMissingAsPass;
        }

        // Daily 힌트 캐시 조회 (정책상 Daily 우선이므로 hint=1만 사용)
        var keyDaily = new NodeKey3 { questId = qid, nodeKey = nodeKey, hint = 1 };
        if (!_completionNodeCache.TryGetValue(keyDaily, out var nref))
        {
            nref = ResolveCompletionNodeRef(qid, nodeKey);
            _completionNodeCache[keyDaily] = nref;
        }

        if (!nref.valid)
        {
            detail = $"pack_or_node_missing(qid='{qid}', nodeKey='{nodeKey}') -> {(treatMissingAsPass ? "PASS" : "FAIL")}";
            if (debugCompletionTraceIncludeMissing) CC_LogMissing(detail);
            return treatMissingAsPass;
        }

        if (nref.isDaily)
            playedValue = (nref.dnode != null) && nref.dnode.played;
        else
            playedValue = (nref.qnode != null) && nref.qnode.played;

        bool ok = (required == NodeCompletionCondition.RequiredState.Played) ? playedValue : !playedValue;
        detail = $"found(pack='{nref.packName}') node.played={playedValue} required={required}";
        return ok;
    }

    CompletionNodeRef ResolveCompletionNodeRef(string qid, string nodeKey)
    {
        // Daily 우선
        if (debugCompletionTraceIncludeRegistryRoute)
        {
            CC_Log($"ResolveDailyPack route: dailyRegistries={(dailyRegistries != null ? dailyRegistries.Length : 0)} dailyRegistry={(dailyRegistry != null ? dailyRegistry.name : "null")}");
        }

        var dpack = ResolveDailyPack(qid);
        if (dpack != null)
        {
            var dnode = dpack.FindNode(nodeKey);
            if (dnode != null)
            {
                return new CompletionNodeRef
                {
                    valid = true,
                    isDaily = true,
                    dnode = dnode,
                    qnode = null,
                    packName = dpack.name
                };
            }
        }

        // Quest fallback
        var qpack = ResolveQuestPack(qid);
        if (qpack != null)
        {
            var qnode = qpack.FindNode(nodeKey);
            if (qnode != null)
            {
                return new CompletionNodeRef
                {
                    valid = true,
                    isDaily = false,
                    dnode = null,
                    qnode = qnode,
                    packName = qpack.name
                };
            }
        }

        return new CompletionNodeRef
        {
            valid = false,
            isDaily = true,
            dnode = null,
            qnode = null,
            packName = "null"
        };
    }

    bool CheckNodeCompletion(string qid, string nodeKey, NodeCompletionCondition.RequiredState required, bool treatMissingAsPass)
    {
        if (string.IsNullOrEmpty(qid) || string.IsNullOrEmpty(nodeKey))
            return treatMissingAsPass;

        // Daily 우선
        var dpack = ResolveDailyPack(qid);
        if (dpack != null)
        {
            var dnode = dpack.FindNode(nodeKey);
            if (dnode == null) return treatMissingAsPass;

            bool played = dnode.played;
            return (required == NodeCompletionCondition.RequiredState.Played) ? played : !played;
        }

        // Quest fallback
        var qpack = ResolveQuestPack(qid);
        if (qpack != null)
        {
            var qnode = qpack.FindNode(nodeKey);
            if (qnode == null) return treatMissingAsPass;

            bool played = qnode.played;
            return (required == NodeCompletionCondition.RequiredState.Played) ? played : !played;
        }

        return treatMissingAsPass;
    }


    void FireRule(int index, TriggerRule rule, string src)
    {
        if (rule == null) return;

        if (rule.triggerOnce)
        {
            SyncRuleRuntimeLists();
            if (index >= 0 && index < _ruleTriggeredOnce.Count)
                _ruleTriggeredOnce[index] = true;
        }

        rule.MarkFiredNow();

        ADV_Log("FIRE_RULE", src, "TriggerRule", currentNodeKey, $"presetIndex={rule.presetIndex}");
        StartDialogueFromPreset(rule.presetIndex, resetRuntime: false);
    }

    bool IsInAutoTriggerRange(TriggerRule rule)
    {
        if (rule == null) return false;

        var t = rule.rangeTarget;
        if (t == null && !string.IsNullOrEmpty(rule.rangeTargetTag))
        {
            var go = GameObject.FindGameObjectWithTag(rule.rangeTargetTag);
            if (go != null) t = rule.rangeTarget = go.transform;
        }
        if (t == null) return false;

        float d = Vector3.Distance(transform.position, t.position);
        return d <= rule.autoTriggerRange;
    }
    // =====================================================
    // Manual API
    // - Manual 규칙을 인덱스로 발동 시도합니다.
    // - 조건/쿨다운/차단 조건을 통과하면 재생합니다.
    // =====================================================
    public bool TryFireManualRule(int ruleIndex, string src = "ManualCall")
    {
        if (triggerRules == null || ruleIndex < 0 || ruleIndex >= triggerRules.Count)
            return false;

        var rule = triggerRules[ruleIndex];
        if (rule == null || !rule.enabled) return false;

        if (rule.triggerMode != TriggerMode.Manual)
            return false;

        if (!CanFireRule(ruleIndex, rule)) return false;
        if (!rule.IsCooldownReady()) return false;
        if (!EvaluateConditions(rule)) return false;

        FireRule(ruleIndex, rule, src);
        return true;
    }

    // =====================================================
    // Public API (기존 유지)
    // =====================================================
    public bool StartDialogue(bool resetRuntime = false)
    {
        _activePresetIndex = defaultPresetIndex;

        ResolvePackAndStartKey(defaultPresetIndex, out var qid, out var skey, out var ptype);
        return StartDialogueInternal(qid, skey, ptype, resetRuntime);
    }

    public bool StartDialogue(string questIdToPlay, string startNodeKeyToPlay, bool resetRuntime = false)
    {
        // ✅ 기존 시그니처 호환 유지: 기본은 Daily로 재생
        return StartDialogueInternal(questIdToPlay, startNodeKeyToPlay, PackPreset.PackType.Daily, resetRuntime);
    }

    private bool StartDialogueInternal(string questIdToPlay, string startNodeKeyToPlay, PackPreset.PackType packType, bool resetRuntime)
    {
        _activePresetIndex = -1;

        if (isActive)
        {
            ADV_Log("FAIL_START", "already_active", "StartDialogue", currentNodeKey, currentNodeKey);
            return false;
        }

        if (string.IsNullOrEmpty(questIdToPlay))
        {
            ADV_Log("FAIL_START", "questId_empty", "StartDialogue", currentNodeKey, currentNodeKey);
            return false;
        }

        if (!ResolveRuntimePack(questIdToPlay, packType))
        {
            ADV_Log("FAIL_START", $"pack_not_found({packType})", "StartDialogue", currentNodeKey, currentNodeKey);
            return false;
        }

        if (resetRuntime)
        {
            if (_runtimePackType == PackPreset.PackType.Daily) _runtimeDailyPack?.ResetRuntimeFlags();
            else _runtimeQuestPack?.ResetRuntimeFlags();
        }

        isActive = true;
        isCompleted = false;
        pendingAdvance = false;

        _runtimeQuestId = questIdToPlay;
        _runtimePackType = packType;
        currentNodeKey = string.IsNullOrEmpty(startNodeKeyToPlay) ? startNodeKey : startNodeKeyToPlay;

        ADV_Log("OK_START", $"started({packType})", "StartDialogue", "null", currentNodeKey);

        PlayCurrentNode("StartDialogue");
        OnStarted?.Invoke(this);
        OnNodeChanged?.Invoke(this, currentNodeKey);

        return true;
    }

    public void StopDialogue()
    {
        if (!isActive)
        {
            ADV_Log("FAIL_STOP", "not_active", "StopDialogue", currentNodeKey, currentNodeKey);
            return;
        }

        isActive = false;
        pendingAdvance = false;

        OnStopped?.Invoke(this);
        ADV_Log("OK_STOP", "stopped", "StopDialogue", currentNodeKey, currentNodeKey);
    }

    public void TriggerStart()
    {
        StartDialogue(resetRuntime: false);
    }

    public bool StartDialogueFromPreset(int presetIndex, bool resetRuntime = false)
    {
        _activePresetIndex = presetIndex;

        ResolvePackAndStartKey(presetIndex, out var qid, out var skey, out var ptype);
        return StartDialogueInternal(qid, skey, ptype, resetRuntime);
    }

    // =====================================================
    // Core: Play / Advance / Complete (기존 유지)
    // =====================================================
    void PlayCurrentNode(string src)
    {
        if (!TryGetNode(currentNodeKey, out var rawNode, out var node))
        {
            ADV_Log("FAIL_PLAY", "node_null", src, currentNodeKey, "null");
            CompleteDialogue("node_null");
            return;
        }

        if (node.playOnlyOnce && node.played)
        {
            ADV_Log("SKIP_NODE", "playOnlyOnce_already_played", src, currentNodeKey, "(auto_next)");
            if (skipPlayedOnceNodes)
            {
                TryAdvance("SkipPlayedOnceNode");
                return;
            }

            CompleteDialogue("playOnlyOnce_blocked");
            return;
        }

        SetNodePlayed(rawNode, true);

        if (dialogueManager == null)
            dialogueManager = DialogueManager.I;

        if (dialogueManager == null)
        {
            ADV_Log("FAIL_PLAY", "dialogueManager_null", src, currentNodeKey, currentNodeKey);
            CompleteDialogue("dialogueManager_null");
            return;
        }

        // ✅ 패널 우선순위: 마스크 > 강제 > DM 기본(null)
        DialogueUI uiToUse = null;
        if (useInputMask && maskPriorityDialogueUI != null)
            uiToUse = maskPriorityDialogueUI;
        else if (targetDialogueUI != null)
            uiToUse = targetDialogueUI;

        dialogueManager.SetContext(_runtimeQuestId, currentNodeKey);

        ADV_Log("PLAY_UI_FORCED",
            useInputMask ? "dm(mask_priority_ui)" : (uiToUse != null ? "dm(forced_ui)" : "dm(default_ui)"),
            src, currentNodeKey, currentNodeKey);

        bool ok;

        if (_runtimePackType == PackPreset.PackType.Quest)
        {
            // ✅ QuestDialoguePack은 autoAdvanceSeconds 메타가 없으므로 수동 진행(UI Next)로 재생합니다.
            ok = dialogueManager.PlayWithMetaOnUI(
                uiToUse,
                node.speaker,
                node.speakerId,
                node.speakersPerLine,
                node.speakerIdsPerLine,
                node.lines,
                (finishedAllLines) =>
                {
                    if (!isActive || isCompleted) return;

                    if (requireUserAdvanceInput)
                    {
                        pendingAdvance = true;
                        ADV_Log("PENDING_ADV", $"closed finished={finishedAllLines}", "OnDMClosed", currentNodeKey, "(wait_input)");
                        return;
                    }

                    TryAdvance("OnDMClosed");
                });
        }
        else
        {
            ok = dialogueManager.PlayAutoAdvanceWithMetaOnUI(
                uiToUse,
                node.speaker,
                node.speakerId,
                node.speakersPerLine,
                node.speakerIdsPerLine,
                node.lines,
                node.autoDefaultSeconds,
                node.autoPerLineSeconds,
                (finishedAllLines) =>
                {
                    if (!isActive || isCompleted) return;

                    if (requireUserAdvanceInput)
                    {
                        pendingAdvance = true;
                        ADV_Log("PENDING_ADV", $"closed finished={finishedAllLines}", "OnDMClosed", currentNodeKey, "(wait_input)");
                        return;
                    }

                    TryAdvance("OnDMClosed");
                });
        }

        if (!ok)

        {
            ADV_Log("FAIL_PLAY", "dm_blocked_or_failed", src, currentNodeKey, currentNodeKey);
            CompleteDialogue("dm_blocked_or_failed");
            return;
        }

        ADV_Log("OK_PLAY", "playing(via_dm)", src, currentNodeKey, currentNodeKey);
    }

    bool TryAdvance(string src)
    {
        string fromKey = currentNodeKey;

        if (!TryGetNode(fromKey, out var rawNode, out var node))
        {
            ADV_Log("FAIL_ADV", "node_null", src, fromKey, "COMPLETE");
            CompleteDialogue("node_null");
            return false;
        }

        string nextKey = ExtractNextKey(rawNode);
        if (string.IsNullOrEmpty(nextKey) && enableAutoIncrementKey)
            nextKey = AutoIncrementKey(fromKey, autoIncrementStep);

        string toKey = nextKey;

        ADV_Log("TRY_ADV", "called", src, fromKey, string.IsNullOrEmpty(toKey) ? "COMPLETE" : toKey);

        if (!ADV_CanAdvance(fromKey, toKey, out string reason))
        {
            ADV_Log("FAIL_ADV", reason, src, fromKey, toKey);
            return false;
        }

        if (string.IsNullOrEmpty(toKey))
        {
            ADV_Log("OK_ADV", "complete", src, fromKey, "COMPLETE");
            CompleteDialogue("no_next");
            return true;
        }

        currentNodeKey = toKey;
        pendingAdvance = false;

        OnNodeChanged?.Invoke(this, currentNodeKey);
        PlayCurrentNode(src);

        ADV_Log("OK_ADV", "advanced", src, fromKey, currentNodeKey);
        return true;
    }

    void CompleteDialogue(string reason)
    {
        if (isCompleted) return;

        isCompleted = true;
        isActive = false;
        pendingAdvance = false;

        OnCompleted?.Invoke(this);
        ADV_Log("OK_COMPLETE", reason, "CompleteDialogue", currentNodeKey, currentNodeKey);
    }

    // =====================================================
    // Node Lookup
    // =====================================================
    struct NodeData
    {
        public string speaker;
        public string speakerId;
        public string[] speakersPerLine;
        public string[] speakerIdsPerLine;
        public string[] lines;
        public bool playOnlyOnce;
        public bool played;

        public float autoDefaultSeconds;
        public float[] autoPerLineSeconds;
    }

    bool TryGetNode(string key, out object rawNode, out NodeData data)
    {
        rawNode = null;
        data = default;

        if (string.IsNullOrEmpty(key)) return false;
        if (string.IsNullOrEmpty(_runtimeQuestId)) return false;

        // 런타임 팩이 비어있으면 현재 타입에 맞춰 다시 resolve
        if (_runtimePackType == PackPreset.PackType.Quest)
        {
            if (_runtimeQuestPack == null)
                ResolveRuntimePack(_runtimeQuestId, PackPreset.PackType.Quest);

            if (_runtimeQuestPack == null) return false;

            var n = _runtimeQuestPack.FindNode(key);
            if (n == null) return false;

            rawNode = n;

            string defaultSpeaker;
            string defaultSpeakerId;
            string[] speakersPerLine;
            string[] speakerIdsPerLine;
            string[] linesPerLine;

            BuildSequenceArraysQuest(n,
                out defaultSpeaker, out defaultSpeakerId,
                out speakersPerLine, out speakerIdsPerLine, out linesPerLine);

            data.speaker = defaultSpeaker;
            data.speakerId = defaultSpeakerId;
            data.speakersPerLine = speakersPerLine;
            data.speakerIdsPerLine = speakerIdsPerLine;
            data.lines = linesPerLine;
            data.playOnlyOnce = n.playOnlyOnce;
            data.played = n.played;

            // Quest는 자동넘김 메타가 없으므로 0/빈값
            data.autoDefaultSeconds = 0f;
            data.autoPerLineSeconds = (linesPerLine != null) ? new float[linesPerLine.Length] : System.Array.Empty<float>();

            return true;
        }
        else
        {
            if (_runtimeDailyPack == null)
                ResolveRuntimePack(_runtimeQuestId, PackPreset.PackType.Daily);

            if (_runtimeDailyPack == null) return false;

            var n = _runtimeDailyPack.FindNode(key);
            if (n == null) return false;

            rawNode = n;

            string defaultSpeaker;
            string defaultSpeakerId;
            string[] speakersPerLine;
            string[] speakerIdsPerLine;
            string[] linesPerLine;
            float defaultSeconds;
            float[] perLineSeconds;

            BuildSequenceArrays(n,
                out defaultSpeaker, out defaultSpeakerId,
                out speakersPerLine, out speakerIdsPerLine, out linesPerLine,
                out defaultSeconds, out perLineSeconds);

            data.speaker = defaultSpeaker;
            data.speakerId = defaultSpeakerId;
            data.speakersPerLine = speakersPerLine;
            data.speakerIdsPerLine = speakerIdsPerLine;
            data.lines = linesPerLine;
            data.playOnlyOnce = n.playOnlyOnce;
            data.played = n.played;
            data.autoDefaultSeconds = defaultSeconds;
            data.autoPerLineSeconds = perLineSeconds;

            return true;
        }
    }

    void SetNodePlayed(object rawNode, bool v)
    {
        if (rawNode == null) return;
        if (rawNode is DailyDialoguePack.Node d) d.played = v;
        else if (rawNode is QuestDialoguePack.Node q) q.played = v;
    }

    bool ResolveRuntimePack(string id, PackPreset.PackType packType)
    {
        _runtimeDailyPack = null;
        _runtimeQuestPack = null;

        if (packType == PackPreset.PackType.Quest)
        {
            var qp = ResolveQuestPack(id);
            if (qp != null)
            {
                _runtimeQuestPack = qp;
                _runtimePackType = PackPreset.PackType.Quest;
                return true;
            }
            return false;
        }
        else
        {
            var dp = ResolveDailyPack(id);
            if (dp != null)
            {
                _runtimeDailyPack = dp;
                _runtimePackType = PackPreset.PackType.Daily;
                return true;
            }
            return false;
        }
    }

    DailyDialoguePack ResolveDailyPack(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        if (dailyRegistries != null && dailyRegistries.Length > 0)
        {
            for (int i = 0; i < dailyRegistries.Length; i++)
            {
                var r = dailyRegistries[i];
                if (!r) continue;
                var p = r.GetPack(id);
                if (p) return p;
            }
        }

        if (dailyRegistry == null)
            dailyRegistry = FindFirstObjectByType<DailyDialogueRegistry>();

        if (dailyRegistry == null) return null;
        return dailyRegistry.GetPack(id);
    }


    QuestDialoguePack ResolveQuestPack(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        if (questRegistries != null && questRegistries.Length > 0)
        {
            for (int i = 0; i < questRegistries.Length; i++)
            {
                var r = questRegistries[i];
                if (!r) continue;
                var p = r.GetPack(id);
                if (p) return p;
            }
        }

        if (questRegistry == null)
            questRegistry = FindFirstObjectByType<QuestDialogueRegistry>();

        if (questRegistry != null)
        {
            var p = questRegistry.GetPack(id);
            if (p) return p;
        }

        // 마지막 fallback: 로컬 리스트
        if (questPacks != null && questPacks.Count > 0)
        {
            for (int i = 0; i < questPacks.Count; i++)
            {
                var p = questPacks[i];
                if (!p) continue;
                if (string.Equals(p.questId, id, System.StringComparison.Ordinal))
                    return p;
            }
        }

        return null;
    }

    // =====================================================
    // Pack Presets
    // =====================================================
    void ResolvePackAndStartKey(int presetIndex, out string qid, out string skey)
    {
        qid = questId;
        skey = startNodeKey;

        if (packPresets == null || packPresets.Count == 0)
            return;

        if (presetIndex < 0) presetIndex = 0;
        if (presetIndex >= packPresets.Count) presetIndex = packPresets.Count - 1;

        var p = packPresets[presetIndex];
        if (p == null) return;

        if (!string.IsNullOrEmpty(p.questId)) qid = p.questId;
        if (!string.IsNullOrEmpty(p.startNodeKey)) skey = p.startNodeKey;
    }

    void ResolvePackAndStartKey(int presetIndex, out string qid, out string skey, out PackPreset.PackType ptype)
    {
        ptype = PackPreset.PackType.Daily;
        ResolvePackAndStartKey(presetIndex, out qid, out skey);

        if (packPresets == null || packPresets.Count == 0)
            return;

        if (presetIndex < 0) presetIndex = 0;
        if (presetIndex >= packPresets.Count) presetIndex = packPresets.Count - 1;

        var p = packPresets[presetIndex];
        if (p == null) return;

        ptype = p.packType;
    }


    // =====================================================
    // nextKey extraction (reflection)
    // =====================================================
    static readonly Dictionary<Type, Func<object, string>> _nextKeyGetterCache = new();

    string ExtractNextKey(object node)
    {
        if (node == null) return null;

        var t = node.GetType();

        if (_nextKeyGetterCache.TryGetValue(t, out var getter))
            return getter(node);

        string[] names = { "nextKey", "nextNodeKey", "next", "nextNode" };

        foreach (var n in names)
        {
            var f = t.GetField(n, BindingFlags.Public | BindingFlags.Instance);
            if (f != null && f.FieldType == typeof(string))
            {
                Func<object, string> g = (obj) => (string)f.GetValue(obj);
                _nextKeyGetterCache[t] = g;
                return g(node);
            }
        }

        foreach (var n in names)
        {
            var p = t.GetProperty(n, BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.PropertyType == typeof(string) && p.GetGetMethod() != null)
            {
                Func<object, string> g = (obj) => (string)p.GetValue(obj);
                _nextKeyGetterCache[t] = g;
                return g(node);
            }
        }

        _nextKeyGetterCache[t] = (obj) => null;
        return null;
    }

    // =====================================================
    // Auto increment: S1_T1 -> S1_T2
    // =====================================================
    string AutoIncrementKey(string key, int step)
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (step <= 0) step = 1;

        int i = key.Length - 1;
        while (i >= 0 && char.IsDigit(key[i])) i--;

        int digitStart = i + 1;
        if (digitStart >= key.Length) return null;

        string prefix = key.Substring(0, digitStart);
        string numStr = key.Substring(digitStart);

        if (!int.TryParse(numStr, out int n)) return null;

        int next = n + step;
        return prefix + next.ToString();
    }
}