using UnityEngine;

/// <summary>
/// DialogueFollow
/// - DialogueManager를 참고해서 현재 대화 타겟을 자동으로 따라가는 World Space Follow 컴포넌트
/// - 기본 타겟 소스: DialogueManager.CurrentFollowTarget (DialogueUI 내부 Resolve 결과)
/// - 수동 오버라이드도 가능(SetTarget/ClearTarget)
/// </summary>
public class DialogueFollow : MonoBehaviour
{
    [Header("Refs")]
    public DialogueManager dialogueManager;

    [Tooltip("✅ 이 Follow가 따라갈 DialogueUI를 지정합니다.\n비워두면: DialogueManager의 기본 UI(dialogueUI)를 따라갑니다.\n\n[분리 팁] Quest UI와 Daily UI를 분리했다면, Quest Follow에는 Quest UI를 넣어주세요.")]
    public DialogueUI followDialogueUI;

    [Header("Follow")]
    public bool enableFollow = true;

    [Tooltip("이 RectTransform을 월드 좌표로 이동시켜 말풍선을 따라가게 합니다. (보통 DialoguePanel 루트)")]
    public RectTransform dialogueUIRoot;

    [Tooltip("타겟 위치에 더해줄 오프셋 (머리 위)")]
    public Vector3 worldOffset = new Vector3(0f, 1.8f, 0f);

    [Tooltip("true면 DialogueManager가 잡은 타겟을 매 프레임 뜯어와서 따라갑니다.")]
    public bool pullTargetFromDialogueManager = true;

    [Header("Occlusion (Optional)")]
    public bool useOcclusionRaycast = false;

    [Tooltip("Occlusion Raycast에 사용할 레이어 마스크입니다. (벽/지형 레이어만 포함 권장)")]
    public LayerMask occlusionMask = ~0;

    [Tooltip("Raycast가 맞았을 때 표면에서 살짝 띄울 거리입니다.")]
    public float occlusionSurfaceOffset = 0.02f;

    [Header("Debug")]
    public bool verboseLog = true;
    public bool traceOnlyOnChange = true;
    public float minMoveToLog = 0.0005f;

    // 수동 오버라이드 타겟 (SetTarget으로 지정)
    Transform _manualTarget;

    string _prevKey = "";
    Transform _prevPulledTarget;
    Vector3 _prevPos;

    Camera _cam;

    void Awake()
    {
        if (!dialogueManager)
            dialogueManager = DialogueManager.I != null ? DialogueManager.I : FindFirstObjectByType<DialogueManager>();

        if (dialogueUIRoot == null)
        {
            var ui = FindFirstObjectByType<DialogueUI>();
            if (ui) dialogueUIRoot = ui.transform as RectTransform;
        }

        _cam = Camera.main;
        _prevPos = dialogueUIRoot ? dialogueUIRoot.position : Vector3.zero;
    }

    /// <summary>수동 오버라이드: Follow 대상 지정</summary>
    public void SetTarget(Transform target)
    {
        _manualTarget = target;

        if (verboseLog)
            Debug.Log($"[DM-FOLLOW] SetTarget (manual) target={(target ? target.name : "null")} path={(target ? GetPath(target) : "null")}", this);

        TraceTick("SET_TARGET_manual");
    }

    /// <summary>수동 오버라이드 제거 (다시 DM 타겟을 사용)</summary>
    public void ClearTarget()
    {
        _manualTarget = null;

        if (verboseLog)
            Debug.Log("[DM-FOLLOW] ClearTarget (manual)", this);

        TraceTick("CLEAR_TARGET_manual");
    }

    public Transform CurrentTarget
    {
        get
        {
            if (_manualTarget) return _manualTarget;
            if (!pullTargetFromDialogueManager) return null;
            return dialogueManager ? dialogueManager.CurrentFollowTargetOnUI(followDialogueUI) : null;
        }
    }

    void LateUpdate()
    {
        if (!enableFollow) { TraceTick("RET_enableFollow_false"); return; }
        if (dialogueManager == null) { TraceTick("RET_dm_null"); return; }
        if (!dialogueManager.IsPlayingOnUI(followDialogueUI)) { TraceTick("RET_dm_not_playing"); _prevPulledTarget = null; return; }
        if (dialogueUIRoot == null) { TraceTick("RET_root_null"); return; }

        var target = CurrentTarget;
        if (target == null) { TraceTick("RET_target_null"); return; }

        // DM에서 뜯어온 타겟 변경 감지 로그
        if (pullTargetFromDialogueManager && !_manualTarget && target != _prevPulledTarget)
        {
            _prevPulledTarget = target;

            if (verboseLog)
            {
                Debug.Log(
                    $"[DM-FOLLOW] Pulled target changed -> {(target ? target.name : "null")} " +
                    $"(speakerId={dialogueManager.CurrentSpeakerIdOnUI(followDialogueUI)}) path={(target ? GetPath(target) : "null")}",
                    this);
            }
        }

        var from = dialogueUIRoot.position;
        var desired = target.position + worldOffset;

        // (옵션) 오클루전 레이캐스트
        if (useOcclusionRaycast && _cam != null)
        {
            var camPos = _cam.transform.position;
            var dir = desired - camPos;
            float dist = dir.magnitude;

            if (dist > 0.0001f)
            {
                dir /= dist;

                if (Physics.Raycast(camPos, dir, out var hit, dist, occlusionMask, QueryTriggerInteraction.Ignore))
                {
                    desired = hit.point + hit.normal * occlusionSurfaceOffset;
                }
            }
        }

        dialogueUIRoot.position = desired;

        float sqr = (desired - from).sqrMagnitude;
        if (sqr >= (minMoveToLog * minMoveToLog))
            TraceMove(from, desired);
        else
            TraceTick("TICK_no_visible_move");

        _prevPos = dialogueUIRoot.position;
    }

    void TraceTick(string key)
    {
        if (!verboseLog) return;
        if (traceOnlyOnChange && _prevKey == key) return;
        _prevKey = key;

        string root = dialogueUIRoot ? dialogueUIRoot.name : "null";
        string dm = dialogueManager ? dialogueManager.name : "null";
        var target = CurrentTarget;
        string tgt = target ? target.name : "null";
        string path = target ? GetPath(target) : "null";

        Debug.Log(
            $"[DM-FOLLOW] {key}\n" +
            $"  dm={dm} playing={(dialogueManager != null ? dialogueManager.IsPlayingOnUI(followDialogueUI) : false)}\n" +
            $"  root={root}\n" +
            $"  target={tgt} path={path}\n" +
            $"  source={(_manualTarget ? "manual" : (pullTargetFromDialogueManager ? "dm" : "none"))}",
            this);
    }

    void TraceMove(Vector3 from, Vector3 to)
    {
        if (!verboseLog) return;
        Debug.Log($"[DM-FOLLOW] MOVE from={from} -> to={to}", this);
    }

    static string GetPath(Transform t)
    {
        if (t == null) return "null";
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}