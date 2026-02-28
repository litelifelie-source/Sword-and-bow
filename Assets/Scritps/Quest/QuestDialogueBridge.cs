using System;
using System.Collections.Generic;
using UnityEngine;

public class QuestDialogueBridge : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("퀘스트 진행/인덱스 변경 이벤트를 제공하는 QuestManager 참조입니다. 비워두면 QuestManager.I를 사용합니다.")]
    public QuestManager questManager;

    [Tooltip("대화 재생을 담당하는 DialogueManager 참조입니다. 비워두면 DialogueManager.I를 사용합니다.")]
    public DialogueManager dialogueManager;

    [Tooltip("대화 말풍선/패널을 월드 타겟에 붙이는 Follow 컴포넌트입니다. 비워두면 씬에서 자동 탐색합니다.")]
    public DialogueFollow dialogueFollow;

    [Header("Dialogue Source")]
    [Tooltip("questId -> QuestDialoguePack을 찾아오는 레지스트리입니다. 비워두면 QuestDialogueRegistry.I를 사용합니다.")]
    public QuestDialogueRegistry registry;

    [Tooltip("레지스트리가 없거나 못 찾을 때의 레거시 fallback 리스트입니다. (권장: registry.packs만 사용)")]
    public List<QuestDialoguePack> packs = new List<QuestDialoguePack>();

    [Header("Behavior")]
    [Tooltip("대사가 이미 재생 중일 때 새 대사를 강제 종료 후 재생할지 여부입니다.")]
    public bool forceInterruptIfPlaying = true;

    [Tooltip("ON이면 AllyCrossJump 내부에서 판정/스킵/점프 이유를 상세 로그로 출력합니다.")]
    public bool debugAllyCrossJump = false;

    [Tooltip("ON이면 OnlyAlly 판정(ResolveSpeakerTag/UnitTeam/LayerMask) 과정을 브릿지에서 추가로 상세 로그로 출력합니다. (로그가 매우 많아질 수 있어요)")]
    public bool debugAllyDecision = true;

    // =====================================================
    // Ally Cross Jump (line-level audience + jump)
    // =====================================================
    [Header("Branch (Ally Cross Jump)")]
    [Tooltip("ON이면: main 진행 중 OnlyAlly 라인이 '실제로 출력'되는 순간 ally 시퀀스로 점프(락)합니다.")]
    public bool enableAllyCrossJump = true;

    [Tooltip("OnlyAlly 판정에 사용할 레이어 마스크입니다. (예: Ally 레이어만 체크)")]
    public LayerMask allyLayerMask;

    [Tooltip("ON이면: SpeakerIdTag.followAnchor의 레이어를 우선으로 보고 Ally 판정을 합니다. (권장 ON)")]
    public bool checkFollowAnchorLayerForAlly = true;

    [Tooltip("ON이면: SpeakerIdTag가 가진 UnitTeam(IsAlly)을 최우선으로 사용해 Ally를 판정합니다. (권장 ON)")]
    public bool preferUnitTeamFromSpeakerTag = true;

    [Header("SpeakerIdTag Lookup")]
    [Tooltip("ON이면: SpeakerIdTag.speakerId를 캐시로 구축해 빠르게 타겟/판정을 찾습니다. (권장 ON)")]
    public bool useSpeakerIdTagLookup = true;

    [Tooltip("ON이면: 캐시에 speakerKey가 없을 때 씬을 다시 스캔(RebuildSpeakerCache)하여 재시도합니다.")]
    public bool rebuildCacheIfMissing = true;

    [Tooltip("ON이면: 동일한 speakerId가 여러 SpeakerIdTag에 중복될 때 경고 로그를 출력합니다.")]
    public bool warnOnDuplicateSpeakerId = true;

    [Tooltip("ON이면: speakerKey 이름으로 GameObject.Find를 시도합니다. (권장 OFF: 느리고 오탐 가능)")]
    public bool fallbackFindByName = false;

    // ✅ speakerId -> SpeakerIdTag 캐시
    private readonly Dictionary<string, SpeakerIdTag> _speakerTagMap = new(64);

    // ✅ OnlyAlly 판정 로그 스팸 방지: 한 번의 BuildSequenceArrays 호출 동안 동일 speakerId는 1회만 출력
    private readonly HashSet<string> _allyDecisionLoggedThisBuild = new HashSet<string>(32);

    [Header("Debug")]
    [Tooltip("브릿지 내부 동작 로그(캐시 구축/탐색/플레이)를 더 자세히 출력합니다.")]
    public bool verboseLog = true;

    [Header("Index Receive Debug (Bridge-level)")]
    [Tooltip("브릿지가 OnStepIndexChanged를 받는 순간 무조건 찍는 로그입니다.")]
    public bool logIndexReceive = true;

    [Tooltip("Quest/Step/Key 결정 및 TryPlay 성공/실패 분기 로그입니다.")]
    public bool logIndexReceiveDetails = true;

    [Tooltip("수신 지점에서 스택트레이스까지 찍을지 여부입니다. (로그가 매우 많아질 수 있어요)")]
    public bool logStackTraceOnReceive = false;

    [Header("Advance Debug (Bridge-level)")]
    [Tooltip("대사 재생 중 StepIndexChanged가 들어오는지 추적하는 디버그를 켭니다.")]
    public bool enableAdvanceDebug = true;

    private string _inFlightQuestId = null;
    private string _inFlightNodeKey = null;
    private int _inFlightSourceStepIndex = int.MinValue;
    private int _inFlightStepIndexChangedTo = int.MinValue;
    private bool _stepIndexChangedDuringDialogue = false;
    private int _inFlightToken = 0;

    private void Awake()
    {
        if (!questManager) questManager = QuestManager.I;
        if (!dialogueManager) dialogueManager = DialogueManager.I;
        if (!dialogueFollow) dialogueFollow = FindFirstObjectByType<DialogueFollow>();
        if (!registry) registry = QuestDialogueRegistry.I;

        if (useSpeakerIdTagLookup)
            RebuildSpeakerCache();

        if (verboseLog)
        {
            Debug.Log($"[QDB] Awake questManager={(questManager ? questManager.name : "null")} " +
                      $"dialogueManager={(dialogueManager ? dialogueManager.name : "null")} " +
                      $"dialogueFollow={(dialogueFollow ? dialogueFollow.name : "null")} " +
                      $"registry={(registry ? registry.name : "null")}", this);
        }
    }

    private void OnEnable()
    {
        if (questManager != null)
        {
            questManager.OnStepIndexChanged += HandleStepIndexChanged;

            if (verboseLog)
                Debug.Log("[QDB] Subscribed: questManager.OnStepIndexChanged", this);
        }
        else
        {
            Debug.LogError("[QDB] OnEnable failed: questManager is NULL", this);
        }
    }

    private void OnDisable()
    {
        if (questManager != null)
            questManager.OnStepIndexChanged -= HandleStepIndexChanged;
    }

    [Tooltip("외부에서 강제로 특정 퀘스트의 특정 노드키를 시작할 때 사용합니다.")]
    public void PlayStart(string questId, string startNodeKey)
    {
        TryPlayNodeByKey(questId, startNodeKey, -1);
    }

    private void HandleStepIndexChanged(string questId, int stepIndex)
    {
        if (logIndexReceive)
        {
            Debug.Log($"[QDB][RECEIVE] questId={questId} stepIndex={stepIndex} " +
                      $"isPlaying={(dialogueManager ? dialogueManager.IsPlaying : false)}", this);

            if (logStackTraceOnReceive)
                Debug.Log(Environment.StackTrace, this);
        }

        if (enableAdvanceDebug && dialogueManager && dialogueManager.IsPlaying)
        {
            if (!string.IsNullOrEmpty(_inFlightQuestId) && _inFlightQuestId == questId)
            {
                _stepIndexChangedDuringDialogue = true;
                _inFlightStepIndexChangedTo = stepIndex;

                Debug.Log(
                    $"[QDB][ADV] StepIndexChanged DURING dialogue! questId={questId} " +
                    $"fromSource={_inFlightSourceStepIndex} toNow={stepIndex} nodeKey={_inFlightNodeKey}",
                    this
                );
            }
        }

        var q = questManager ? questManager.GetQuest(questId) : null;
        if (q == null)
        {
            if (logIndexReceiveDetails)
                Debug.LogWarning($"[QDB][RECEIVE] Quest NULL questId={questId} (GetQuest failed)", this);
            return;
        }

        if (q.CurrentStep == null)
        {
            if (logIndexReceiveDetails)
                Debug.LogWarning($"[QDB][RECEIVE] CurrentStep NULL questId={questId} (inactive? index out?)", this);
            return;
        }

        string key = q.CurrentStep.stepId;
        if (string.IsNullOrEmpty(key))
            key = $"S{stepIndex}";

        if (logIndexReceiveDetails)
            Debug.Log($"[QDB][KEY] questId={questId} resolvedKey={key} (stepId='{q.CurrentStep.stepId}', stepIndex={stepIndex})", this);

        bool ok = TryPlayNodeByKey(questId, key, stepIndex);

        if (!ok && logIndexReceiveDetails)
            Debug.LogWarning($"[QDB][PLAY-FAIL] questId={questId} key={key}", this);
    }

    [Tooltip("씬의 SpeakerIdTag들을 스캔하여 speakerId->tag 캐시를 다시 구축합니다.")]
    private void RebuildSpeakerCache()
    {
        _speakerTagMap.Clear();

        var tags = FindObjectsByType<SpeakerIdTag>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (var tag in tags)
        {
            if (!tag) continue;

            string id = tag.speakerId != null ? tag.speakerId.Trim() : null;
            if (string.IsNullOrEmpty(id)) continue;

            if (_speakerTagMap.ContainsKey(id))
            {
                if (warnOnDuplicateSpeakerId)
                    Debug.LogWarning($"[QDB] Duplicate speakerId '{id}' detected.", this);
                continue;
            }

            _speakerTagMap[id] = tag;

            if (verboseLog)
            {
                var anchor = tag.FollowAnchor;
                Debug.Log($"[QDB] SpeakerMap add id='{id}' -> tag='{tag.name}' " +
                          $"tagLayer={tag.gameObject.layer} anchor='{(anchor ? anchor.name : "null")}' " +
                          $"anchorLayer={(anchor ? anchor.gameObject.layer : -1)}", this);
            }
        }
    }

    private string ResolveElementSpeakerName(QuestDialoguePack.Node node, QuestDialoguePack.LineElement e)
    {
        if (e != null && !string.IsNullOrEmpty(e.speaker)) return e.speaker;
        return node != null && !string.IsNullOrEmpty(node.speaker) ? node.speaker : "NPC";
    }

    private string ResolveElementSpeakerKey(QuestDialoguePack.Node node, QuestDialoguePack.LineElement e)
    {
        if (e != null && !string.IsNullOrEmpty(e.speakerId)) return e.speakerId;
        if (node != null && !string.IsNullOrEmpty(node.speakerId)) return node.speakerId;
        // speakerId가 비어있으면 "표시 이름"을 키로도 사용(레거시 호환)
        return ResolveElementSpeakerName(node, e);
    }

    [Tooltip("speakerKey로 SpeakerIdTag를 찾아 반환합니다. (캐시→리빌드→옵션이면 이름찾기 순서)")]
    private SpeakerIdTag ResolveSpeakerTag(string speakerKey)
    {
        if (string.IsNullOrEmpty(speakerKey)) return null;

        if (useSpeakerIdTagLookup)
        {
            if (_speakerTagMap.TryGetValue(speakerKey, out var tag) && tag)
            {
                if (debugAllyCrossJump && debugAllyDecision)
                    Debug.Log($"[QDB][AllyChk] ResolveSpeakerTag HIT cache key='{speakerKey}' -> tag='{tag.name}'", this);
                return tag;
            }

            if (rebuildCacheIfMissing)
            {
                if (verboseLog)
                    Debug.LogWarning($"[QDB] speakerKey '{speakerKey}' not in cache -> rebuild", this);

                RebuildSpeakerCache();

                if (_speakerTagMap.TryGetValue(speakerKey, out tag) && tag)
                {
                    if (debugAllyCrossJump && debugAllyDecision)
                        Debug.Log($"[QDB][AllyChk] ResolveSpeakerTag HIT after rebuild key='{speakerKey}' -> tag='{tag.name}'", this);
                    return tag;
                }
            }
        }

        if (fallbackFindByName)
        {
            var go = GameObject.Find(speakerKey);
            if (go)
            {
                var byName = go.GetComponentInChildren<SpeakerIdTag>(true);
                if (debugAllyCrossJump && debugAllyDecision)
                    Debug.Log($"[QDB][AllyChk] ResolveSpeakerTag FIND-BY-NAME key='{speakerKey}' -> go='{go.name}' tag='{(byName ? byName.name : "null")}'", this);
                return byName;
            }
        }

        if (debugAllyCrossJump && debugAllyDecision)
            Debug.LogWarning($"[QDB][AllyChk] ResolveSpeakerTag MISS key='{speakerKey}' (cache={(useSpeakerIdTagLookup ? "ON" : "OFF")}, rebuild={(rebuildCacheIfMissing ? "ON" : "OFF")}, findByName={(fallbackFindByName ? "ON" : "OFF")})", this);

        return null;
    }

    // =====================================================
    // Ally 판정/필터
    // =====================================================
    private bool IsSpeakerOnMask(string speakerId, LayerMask mask, bool preferAnchorLayer)
    {
        if (string.IsNullOrEmpty(speakerId)) return false;

        var tag = ResolveSpeakerTag(speakerId);
        if (!tag) return false;

        int layer;
        if (preferAnchorLayer && tag.FollowAnchor)
            layer = tag.FollowAnchor.gameObject.layer;
        else
            layer = tag.gameObject.layer;

        bool pass = (mask.value & (1 << layer)) != 0;

        if (debugAllyCrossJump && debugAllyDecision && TryLogAllyDecisionOnce(speakerId))
        {
            string anchorName = tag.FollowAnchor ? tag.FollowAnchor.name : "null";
            int anchorLayer = tag.FollowAnchor ? tag.FollowAnchor.gameObject.layer : -1;
            Debug.Log(
                $"[QDB][AllyChk] LayerMask fallback sid='{speakerId}' tag='{tag.name}' " +
                $"tagLayer={tag.gameObject.layer} anchor='{anchorName}' anchorLayer={anchorLayer} " +
                $"preferAnchorLayer={preferAnchorLayer} usedLayer={layer} mask={mask.value} -> {(pass ? "PASS ✅" : "FAIL ❌")}",
                this
            );
        }

        return pass;
    }

    // =====================================================
    // ✅ NEW: SpeakerIdTag 기반 Ally 판정 (UnitTeam 우선)
    // =====================================================
    private bool IsSpeakerAlly(string speakerId)
    {
        if (string.IsNullOrEmpty(speakerId)) return false;

        var tag = ResolveSpeakerTag(speakerId);
        if (!tag)
        {
            if (debugAllyCrossJump && debugAllyDecision && TryLogAllyDecisionOnce(speakerId))
                Debug.LogWarning($"[QDB][AllyChk] FAIL ❌ sid='{speakerId}' SpeakerIdTag not found", this);
            return false;
        }

        // 1) UnitTeam이 있으면 그걸 최우선
        if (preferUnitTeamFromSpeakerTag)
        {
            var ut = tag.UnitTeam;
            if (ut != null)
            {
                bool pass = ut.IsAlly;
                if (debugAllyCrossJump && debugAllyDecision && TryLogAllyDecisionOnce(speakerId))
                {
                    Debug.Log(
                        $"[QDB][AllyChk] UnitTeam primary sid='{speakerId}' tag='{tag.name}' team={ut.team} -> {(pass ? "PASS ✅" : "FAIL ❌")}",
                        this
                    );
                }
                return pass;
            }
            else
            {
                if (debugAllyCrossJump && debugAllyDecision && TryLogAllyDecisionOnce(speakerId))
                    Debug.LogWarning($"[QDB][AllyChk] UnitTeam primary sid='{speakerId}' tag='{tag.name}' -> UnitTeam=NULL (fallback to LayerMask)", this);
            }
        }

        // 2) 없으면 레이어 마스크로 fallback
        return IsSpeakerOnMask(speakerId, allyLayerMask, checkFollowAnchorLayerForAlly);
    }

    private bool TryLogAllyDecisionOnce(string speakerId)
    {
        if (string.IsNullOrEmpty(speakerId)) return false;
        return _allyDecisionLoggedThisBuild.Add(speakerId);
    }

    private bool ShouldOutput(QuestDialoguePack.Node node, QuestDialoguePack.LineElement e)
    {
        if (e == null) return false;

        if (e.audience == QuestDialoguePack.LineAudience.Any)
            return true;

        if (e.audience == QuestDialoguePack.LineAudience.OnlyAlly)
        {
            string sid = ResolveElementSpeakerKey(node, e);
            return IsSpeakerAlly(sid);
        }

        return true;
    }

    // =====================================================
    // ✅ main + (조건 만족 시) ally로 "교차 점프(락)"하여 재생 배열 생성
    // =====================================================


    // ============================================================
    // Stream: element를 1개씩 공급하는 Next 함수 생성
    // ============================================================
    private Func<DialogueUI.StreamLine?> BuildSequenceStreamNext(
        QuestDialoguePack.Node node,
        out string defaultSpeaker,
        out string defaultSpeakerId)
    {
        if (debugAllyCrossJump && debugAllyDecision)
            _allyDecisionLoggedThisBuild.Clear();

        defaultSpeaker = node != null ? node.speaker : "NPC";
        defaultSpeakerId = node != null ? node.speakerId : null;

        // ✅ 람다 캡처 금지(out 파라미터) 회피용 로컬 복사
        string ds = defaultSpeaker;
        string dsid = defaultSpeakerId;

        IEnumerable<QuestDialoguePack.LineElement> seq;

        if (enableAllyCrossJump)
        {
            seq = AllyCrossJump.StreamResolvedSequence(
                node,
                ResolveSpeakerTag,
                IsSpeakerAlly,
                AllyCrossJump.JumpMode.LockToAlly,
                debugAllyCrossJump,
                this,
                "[QDB-ACJ]"
            );
        }
        else
        {
            // legacy: main만
            seq = node != null ? node.GetMainResolved() : new List<QuestDialoguePack.LineElement>();
        }

        var it = (seq ?? new List<QuestDialoguePack.LineElement>()).GetEnumerator();

        return () =>
        {
            if (it == null) return null;

            if (!it.MoveNext())
                return null;

            var elem = it.Current;
            if (elem == null)
                return new DialogueUI.StreamLine(ds, dsid, "");

            string speakerName = ResolveElementSpeakerName(node, elem);
            string speakerKey = ResolveElementSpeakerKey(node, elem);
            return new DialogueUI.StreamLine(speakerName, speakerKey, elem.text ?? "");
        };
    }

    private void BuildSequenceArrays(
            QuestDialoguePack.Node node,
            out string defaultSpeaker,
            out string defaultSpeakerId,
            out string[] outSpeakers,
            out string[] outSpeakerIds,
            out string[] outLines)
    {
        if (debugAllyCrossJump && debugAllyDecision)
            _allyDecisionLoggedThisBuild.Clear();

        defaultSpeaker = node != null ? node.speaker : "NPC";
        defaultSpeakerId = node != null ? node.speakerId : null;

        // ✅ 여기서부터 "크로스점프 호출" (이게 없으면 ACJ 디버그는 절대 안 찍힘)
        List<QuestDialoguePack.LineElement> seq;

        if (enableAllyCrossJump)
        {
            seq = AllyCrossJump.BuildResolvedSequence(
                node,
                ResolveSpeakerTag,
                IsSpeakerAlly,
                AllyCrossJump.JumpMode.LockToAlly,
                debugAllyCrossJump,
                this,
                "[QDB-ACJ]"
            );
        }
        else
        {
            seq = node != null ? node.GetMainResolved() : new List<QuestDialoguePack.LineElement>();
        }

        int count = seq != null ? seq.Count : 0;
        outLines = new string[count];
        outSpeakers = new string[count];
        outSpeakerIds = new string[count];

        for (int i = 0; i < count; i++)
        {
            var elem = seq[i];
            if (elem == null)
            {
                outLines[i] = "";
                outSpeakers[i] = defaultSpeaker;
                outSpeakerIds[i] = defaultSpeakerId;
                continue;
            }

            outLines[i] = elem.text ?? "";
            outSpeakers[i] = ResolveElementSpeakerName(node, elem);
            outSpeakerIds[i] = ResolveElementSpeakerKey(node, elem);
        }
    }

    [Tooltip("팔로우 타겟(연출): speakerId로 SpeakerIdTag를 찾고 FollowAnchor를 반환합니다.")]
    private Transform ResolveFollowTarget(string speakerId)
    {
        if (string.IsNullOrEmpty(speakerId)) return null;

        var tag = ResolveSpeakerTag(speakerId);
        if (tag && tag.FollowAnchor)
            return tag.FollowAnchor;

        if (fallbackFindByName)
        {
            var go = GameObject.Find(speakerId);
            if (go) return go.transform;
        }

        return null;
    }

    private bool TryPlayNodeByKey(string questId, string key, int sourceStepIndex)
    {
        if (string.IsNullOrEmpty(questId) || string.IsNullOrEmpty(key))
            return false;

        if (!dialogueManager)
            return false;

        QuestDialoguePack pack = registry ? registry.GetPack(questId) : null;
        if (!pack)
            pack = packs.Find(p => p && p.questId == questId);

        if (!pack)
        {
            if (verboseLog || logIndexReceiveDetails)
                Debug.LogWarning($"[QDB] Pack not found questId={questId}", this);
            return false;
        }

        var node = pack.nodes.Find(n => n != null && n.key == key);
        if (node == null)
        {
            if (verboseLog || logIndexReceiveDetails)
                Debug.LogWarning($"[QDB] Node not found questId={questId} key={key}", this);
            return false;
        }

        if (node.playOnlyOnce && node.played)
        {
            if (verboseLog)
                Debug.Log($"[QDB] Node already played (playOnlyOnce) questId={questId} key={key}", this);
            return false;
        }

        Transform followTarget = ResolveFollowTarget(node.speakerId);

        int token = ++_inFlightToken;

        if (enableAdvanceDebug)
        {
            _inFlightQuestId = questId;
            _inFlightNodeKey = node.key;
            _inFlightSourceStepIndex = sourceStepIndex;
            _stepIndexChangedDuringDialogue = false;
            _inFlightStepIndexChangedTo = int.MinValue;

            Debug.Log($"[QDB][PLAY] token={token} questId={questId} nodeKey={node.key} speakerId={node.speakerId} follow={(followTarget ? followTarget.name : "null")}", this);
        }

        questManager?.NotifyDialogueStarted(questId);
        string defaultSpeaker;
        string defaultSpeakerId;

        // ✅ 스트리밍: 라인을 미리 배열로 만들지 않고 1개씩 공급
        Func<DialogueUI.StreamLine?> streamNext = BuildSequenceStreamNext(node, out defaultSpeaker, out defaultSpeakerId);

        bool ok = PlayStreamWithSeparatedFollow(

                    questId,
                    node.key,
                    defaultSpeaker,
                    defaultSpeakerId,
                    streamNext,
                    followTarget,
                    finished =>
                    {
                        if (finished && node.playOnlyOnce)
                            node.played = true;

                        questManager?.NotifyDialogueCompleted(finished);

                        if (enableAdvanceDebug)
                        {
                            Debug.Log($"[QDB][END] token={token} questId={questId} nodeKey={node.key} finished={finished} " +
                                      $"stepChangedDuringDialogue={_stepIndexChangedDuringDialogue} to={_inFlightStepIndexChangedTo}", this);
                        }

                        _inFlightQuestId = null;
                        _inFlightNodeKey = null;
                        _inFlightSourceStepIndex = int.MinValue;
                        _inFlightStepIndexChangedTo = int.MinValue;
                        _stepIndexChangedDuringDialogue = false;
                    }
                );

        if (!ok)
        {
            questManager?.NotifyDialogueCompleted(false);

            if (enableAdvanceDebug)
                Debug.LogWarning($"[QDB][PLAY-START-FAIL] token={token} questId={questId} nodeKey={node.key}", this);
        }

        return ok;
    }

    // ============================================================
    // ✅ 분리된 Follow 대응 플레이 메서드 (DM 동기화 + Context + 공평콜백)
    // ============================================================
    private bool PlayStreamWithSeparatedFollow(
        string questId,
        string nodeKey,
        string defaultSpeaker,
        string defaultSpeakerId,
        Func<DialogueUI.StreamLine?> streamNext,
        Transform followTarget,
        Action<bool> onFinished
    )
    {
        if (DialogueManager.I != null && dialogueManager != DialogueManager.I)
            dialogueManager = DialogueManager.I;

        if (!dialogueManager)
        {
            onFinished?.Invoke(false);
            return false;
        }

        if (!dialogueFollow)
            dialogueFollow = FindFirstObjectByType<DialogueFollow>();

        dialogueManager.SetContext(questId, nodeKey);

        if (forceInterruptIfPlaying && dialogueManager.IsPlaying)
            dialogueManager.ForceClose();

        if (dialogueFollow)
            dialogueFollow.ClearTarget();

        bool started = dialogueManager.PlayStreamWithMeta(
            defaultSpeaker,
            defaultSpeakerId,
            streamNext,
            finished =>
            {
                if (dialogueFollow)
                    dialogueFollow.ClearTarget();

                onFinished?.Invoke(finished);
            }
        );

        if (!started)
        {
            if (dialogueFollow)
                dialogueFollow.ClearTarget();

            onFinished?.Invoke(false);
        }

        return started;
    }
}
