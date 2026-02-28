using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QuestFlowDirector : MonoBehaviour
{
    public enum MatchKeyMode
    {
        ByIndex,
        ByStepId
    }

    public enum TriggerMode
    {
        Exact,          // now == target
        ReachedOrPassed,// now >= target
        Crossed,        // prev < target && now >= target
        Range           // now in [min, max]
    }

    [Flags]
    public enum ActionMask
    {
        None            = 0,
        ToggleObjects   = 1 << 0,
        Spawns          = 1 << 1,
        Timelines       = 1 << 2,
        Audio           = 1 << 3,
        Animator        = 1 << 4,
        UnityEvents     = 1 << 5,
        Instantiate     = 1 << 6,
        All             = ~0
    }

    [Serializable]
    public class SpawnCall
    {
        [Tooltip("SpawnAdapter 컴포넌트를 넣어주세요 (아래 제공되는 SpawnAdapter 계열).")]
        public SpawnAdapter adapter;

        [Tooltip("스폰 수량(SpawnBatch에 전달).")]
        [Min(1)] public int count = 3;
    }

    [Serializable]
    public class AnimatorCall
    {
        [Tooltip("대상 Animator")]
        public Animator animator;

        [Tooltip("Trigger 파라미터 이름")]
        public string triggerName;

        [Tooltip("Bool 파라미터 이름(비우면 무시)")]
        public string boolName;

        [Tooltip("boolValue 적용 값")]
        public bool boolValue = true;
    }

    [Serializable]
    public class Rule
    {
        [Header("Match")]
        [Tooltip("대상 퀘스트 ID")]
        public string questId;

        [Tooltip("매칭 기준: Index(기존) / StepId(추천)")]
        public MatchKeyMode matchKeyMode = MatchKeyMode.ByStepId;

        [Tooltip("트리거 방식")]
        public TriggerMode mode = TriggerMode.Crossed;

        [Header("Index Match (ByIndex)")]
        [Tooltip("target index (Exact/ReachedOrPassed/Crossed에서 사용)")]
        public int targetIndex = 0;

        [Tooltip("Range 최소 인덱스 (Range에서 사용)")]
        public int minIndex = 0;

        [Tooltip("Range 최대 인덱스 (Range에서 사용)")]
        public int maxIndex = 0;

        [Header("StepId Match (ByStepId)")]
        [Tooltip("target StepId (예: \"S2\", \"S2_T1\", \"talk_to_npc\")")]
        public string targetStepId;

        [Tooltip("Range 최소 StepId (ByStepId + Range에서 사용, 내부적으로 index로 변환해서 처리)")]
        public string minStepId;

        [Tooltip("Range 최대 StepId (ByStepId + Range에서 사용, 내부적으로 index로 변환해서 처리)")]
        public string maxStepId;

        [Header("Fire Control")]
        [Tooltip("이 룰은 한 번만 실행")]
        public bool fireOnce = true;

        [Tooltip("지연 실행(초)")]
        [Min(0f)] public float delay = 0f;

        [Header("Mask")]
        [Tooltip("어떤 액션 그룹을 실행할지 마스크로 제어합니다. (필요한 것만 켜두시면 관리가 쉬워져요!)")]
        public ActionMask actionMask = ActionMask.All;

        [NonSerialized] public bool fired;

        [Header("Actions: Toggle Objects")]
        [Tooltip("발동 시 활성화할 오브젝트")]
        public List<GameObject> enableObjects = new();

        [Tooltip("발동 시 비활성화할 오브젝트")]
        public List<GameObject> disableObjects = new();

        [Header("Actions: Spawns")]
        [Tooltip("발동 시 SpawnBatch 호출 목록")]
        public List<SpawnCall> spawns = new();

        [Header("Actions: Instantiate Prefab")]
        [Tooltip("즉시 생성할 프리팹(선택)")]
        public GameObject prefab;

        [Tooltip("프리팹 생성 지점(비우면 Director 위치)")]
        public Transform prefabSpawnPoint;

        [Tooltip("프리팹 생성 개수")]
        [Min(0)] public int prefabCount = 0;

        [Tooltip("랜덤 분산 반경(0이면 고정)")]
        [Min(0f)] public float prefabScatterRadius = 0f;

        [Header("Actions: Animator")]
        [Tooltip("애니메이터 파라미터/트리거 호출 목록")]
        public List<AnimatorCall> animatorCalls = new();

        [Header("Actions: UnityEvents")]
        [Tooltip("인스펙터에서 원하는 함수를 직접 연결해서 호출할 수 있어요.")]
        public List<UnityEngine.Events.UnityEvent> unityEvents = new();
    }

    [Header("Rules")]
    public List<Rule> rules = new();

    [Header("Debug")]
    public bool verboseLog = true;

    [Header("Behavior")]
    [Tooltip("각 questId의 첫 StepIndexChanged 이벤트에서는 prev를 now로 프라이밍합니다. (Crossed가 첫 이벤트에 갑자기 터지는 것을 방지)")]
    public bool primePrevOnFirstEvent = true;

    // questId -> prevIndex
    private readonly Dictionary<string, int> _prevIndex = new();

    // ✅ 내가 실제로 구독한 매니저 인스턴스 (QuestManager.I를 직접 신뢰하지 않음)
    private QuestManager _boundManager;
    private Coroutine _bindCoroutine;

    private void OnEnable()
    {
        // ✅ QuestManager가 늦게 생겨도 구독을 반드시 보장
        _bindCoroutine = StartCoroutine(CoBindWhenReady());
    }

    private void OnDisable()
    {
        if (_bindCoroutine != null)
        {
            StopCoroutine(_bindCoroutine);
            _bindCoroutine = null;
        }

        Unbind();
    }

    private IEnumerator CoBindWhenReady()
    {
        // 중복 방지
        if (_boundManager != null) yield break;

        // QuestManager.I 준비될 때까지 대기
        while (QuestManager.I == null)
            yield return null;

        Bind(QuestManager.I);
    }

    private void Bind(QuestManager manager)
    {
        if (manager == null) return;
        if (_boundManager == manager) return;

        _boundManager = manager;
        _boundManager.OnStepIndexChanged += OnStepIndexChanged;

        if (verboseLog)
            Debug.Log("[QuestFlowDirector] Bound to QuestManager.", this);
    }

    private void Unbind()
    {
        if (_boundManager == null) return;

        _boundManager.OnStepIndexChanged -= OnStepIndexChanged;
        _boundManager = null;

        if (verboseLog)
            Debug.Log("[QuestFlowDirector] Unbound from QuestManager.", this);
    }

    private void OnStepIndexChanged(string questId, int newIndex)
    {
        // ✅ 첫 이벤트에서 prev=-1 때문에 Crossed가 원치 않게 발동하는 걸 방지(옵션)
        if (!_prevIndex.TryGetValue(questId, out int prev))
        {
            prev = primePrevOnFirstEvent ? newIndex : -1;
        }

        _prevIndex[questId] = newIndex;

        if (verboseLog)
        {
            string nowStep = QuestManager.I != null ? QuestManager.I.GetStepId(questId, newIndex) : null;
            Debug.Log($"[QuestFlowDirector] {questId}: prev={prev} -> new={newIndex} (stepId={nowStep})", this);
        }

        for (int i = 0; i < rules.Count; i++)
        {
            var r = rules[i];
            if (r == null) continue;
            if (!string.Equals(r.questId, questId, StringComparison.Ordinal)) continue;

            if (r.fireOnce && r.fired) continue;
            if (!IsTriggered(r, questId, prev, newIndex)) continue;

            if (r.delay > 0f) StartCoroutine(RunDelayed(r));
            else RunNow(r);
        }
    }

    private bool IsTriggered(Rule r, string questId, int prevIndex, int nowIndex)
    {
        // StepId 기반이면 index로 변환해서 TriggerMode 전부 지원
        if (r.matchKeyMode == MatchKeyMode.ByStepId)
        {
            if (QuestManager.I == null) return false;

            int target = !string.IsNullOrEmpty(r.targetStepId) ? QuestManager.I.GetStepIndex(questId, r.targetStepId) : -1;
            int min = !string.IsNullOrEmpty(r.minStepId) ? QuestManager.I.GetStepIndex(questId, r.minStepId) : -1;
            int max = !string.IsNullOrEmpty(r.maxStepId) ? QuestManager.I.GetStepIndex(questId, r.maxStepId) : -1;

            switch (r.mode)
            {
                case TriggerMode.Exact:
                    if (target < 0) return false;
                    return nowIndex == target;

                case TriggerMode.ReachedOrPassed:
                    if (target < 0) return false;
                    return nowIndex >= target;

                case TriggerMode.Crossed:
                    if (target < 0) return false;
                    return prevIndex < target && nowIndex >= target;

                case TriggerMode.Range:
                    if (min < 0 || max < 0) return false;
                    if (min > max) { var t = min; min = max; max = t; }
                    return nowIndex >= min && nowIndex <= max;

                default:
                    return false;
            }
        }

        // Index 기반
        switch (r.mode)
        {
            case TriggerMode.Exact:
                return nowIndex == r.targetIndex;

            case TriggerMode.ReachedOrPassed:
                return nowIndex >= r.targetIndex;

            case TriggerMode.Crossed:
                return prevIndex < r.targetIndex && nowIndex >= r.targetIndex;

            case TriggerMode.Range:
                return nowIndex >= r.minIndex && nowIndex <= r.maxIndex;

            default:
                return false;
        }
    }

    private IEnumerator RunDelayed(Rule r)
    {
        yield return new WaitForSeconds(r.delay);
        RunNow(r);
    }

    private void RunNow(Rule r)
    {
        if (r.fireOnce) r.fired = true;

        var m = r.actionMask;

        // Toggle
        if ((m & ActionMask.ToggleObjects) != 0)
        {
            if (r.enableObjects != null)
                foreach (var go in r.enableObjects)
                    if (go) go.SetActive(true);

            if (r.disableObjects != null)
                foreach (var go in r.disableObjects)
                    if (go) go.SetActive(false);
        }

        // Spawns
        if ((m & ActionMask.Spawns) != 0)
        {
            if (r.spawns != null)
            {
                foreach (var s in r.spawns)
                {
                    if (s == null || !s.adapter) continue;
                    s.adapter.SpawnBatch(s.count);
                }
            }
        }

        // Instantiate
        if ((m & ActionMask.Instantiate) != 0)
        {
            if (r.prefab && r.prefabCount > 0)
            {
                var basePos = r.prefabSpawnPoint ? r.prefabSpawnPoint.position : transform.position;

                for (int i = 0; i < r.prefabCount; i++)
                {
                    Vector3 pos = basePos;

                    if (r.prefabScatterRadius > 0f)
                    {
                        var v = UnityEngine.Random.insideUnitCircle * r.prefabScatterRadius;
                        pos += new Vector3(v.x, v.y, 0f);
                    }

                    Instantiate(r.prefab, pos, Quaternion.identity);
                }
            }
        }

        // Animator
        if ((m & ActionMask.Animator) != 0)
        {
            if (r.animatorCalls != null)
            {
                foreach (var a in r.animatorCalls)
                {
                    if (a == null || !a.animator) continue;

                    if (!string.IsNullOrEmpty(a.boolName))
                        a.animator.SetBool(a.boolName, a.boolValue);

                    if (!string.IsNullOrEmpty(a.triggerName))
                        a.animator.SetTrigger(a.triggerName);
                }
            }
        }

        // UnityEvents
        if ((m & ActionMask.UnityEvents) != 0)
        {
            if (r.unityEvents != null)
                foreach (var e in r.unityEvents)
                    e?.Invoke();
        }

        if (verboseLog)
            Debug.Log($"[QuestFlowDirector] Fired Rule questId={r.questId} matchKeyMode={r.matchKeyMode} mode={r.mode}", this);
    }
}