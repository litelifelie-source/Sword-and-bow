using UnityEngine;

/// <summary>
/// DialogueIsOpenEdgeAdvancer (Minimal)
/// - 연결(Receiver/콜백) 없이, DialogueUI.IsOpen 또는 DialogueManager.IsPlaying 상태를 폴링해서
///   "열림 -> 닫힘" 하강 에지(falling edge)에서 퀘스트 인덱스를 +1 올립니다.
///
/// ✅ 철학:
/// - 패널 SetActive(false)가 아니라 "숨김" 처리여도 잡힘
/// - Done 판정/노드 매칭/이벤트 푸시 전부 제거
///
/// ✅ 컴파일 안정화:
/// - reflection/dynamic 없음
/// - Unity 버전 의존 API 없음
/// </summary>
public class DialogueIsOpenEdgeAdvancer : MonoBehaviour
{
    [Header("Refs")]
    public QuestManager questManager;
    public DialogueManager dialogueManager;
    public DialogueUI dialogueUI; // 있으면 이걸 우선 사용 (IsOpen)

    [Header("Target Quest")]
    [Tooltip("questManager.activeQuests 중 첫 번째만 올릴지(기본 ON 권장)")]
    public bool advanceFirstActiveQuestOnly = true;

    [Tooltip("activeQuests가 비어있으면 allQuests에서 IsActive인 것 찾아서 올릴지")]
    public bool fallbackScanAllQuestsIfActiveListEmpty = true;

    [Header("Mask Options")]
    [Tooltip("인덱스 올리기 실행 여부 마스크")]
    public bool enableAdvance = true;

    [Tooltip("닫힘 감지 후 1프레임 대기하고 올릴지(닫힘 처리 순서 충돌 방지)")]
    public bool advanceNextFrame = false;

    [Tooltip("중복 실행 방지(닫힘 상태가 연속 프레임 유지될 때 1회만)")]
    public bool preventDoubleFire = true;

    [Header("Debug")]
    public bool verboseLog = true;

    private bool _prevOpen;
    private bool _armed; // 열림을 한번이라도 봐야 닫힘에서 발사
    private bool _fired;

    private void Awake()
    {
        if (questManager == null) questManager = QuestManager.I;
        if (dialogueManager == null) dialogueManager = DialogueManager.I;

        // dialogueUI를 명시 안 하면 DM 안에서 찾는 것에 의존해야 하는데,
        // 여기서는 "있으면 우선" 정도로만 처리
        _prevOpen = ReadOpen();
        _armed = _prevOpen;
    }

    private void Update()
    {
        bool openNow = ReadOpen();

        // open 상태를 한번이라도 봐야 닫힘을 의미있게 처리
        if (openNow) _armed = true;

        // ✅ falling edge: true -> false
        if (_armed && _prevOpen && !openNow)
        {
            if (!enableAdvance)
            {
                if (verboseLog) Debug.Log("[DIOEA] Detected close but enableAdvance=false (skip)", this);
            }
            else if (preventDoubleFire && _fired)
            {
                if (verboseLog) Debug.Log("[DIOEA] Detected close but already fired (skip)", this);
            }
            else
            {
                if (advanceNextFrame) StartCoroutine(AdvanceNextFrame());
                else DoAdvance("IsOpen falling-edge");

                _fired = true;
            }
        }

        // 다시 열리면 다음 닫힘을 위해 재무장
        if (openNow)
            _fired = false;

        _prevOpen = openNow;
    }

    private System.Collections.IEnumerator AdvanceNextFrame()
    {
        yield return null;
        DoAdvance("IsOpen falling-edge (next frame)");
    }

    private bool ReadOpen()
    {
        // 1) dialogueUI.IsOpen 우선
        if (dialogueUI != null)
            return dialogueUI.IsOpen;

        // 2) 없으면 dialogueManager.IsPlaying
        if (dialogueManager != null)
            return dialogueManager.IsPlaying;

        return false;
    }

    private void DoAdvance(string reason)
    {
        if (questManager == null)
        {
            if (verboseLog) Debug.LogWarning($"[DIOEA] questManager is NULL ({reason})", this);
            return;
        }

        var q = PickTargetQuest();
        if (q == null)
        {
            if (verboseLog) Debug.LogWarning($"[DIOEA] No target quest found ({reason})", this);
            return;
        }

        int before = q.CurrentStepIndex;
        q.ForceAdvanceNext(null);

        if (verboseLog)
            Debug.Log($"[DIOEA] Advance ({reason}) questId={q.questId} idx {before} -> {q.CurrentStepIndex}", this);
    }

    private QuestLine PickTargetQuest()
    {
        if (questManager.activeQuests != null && questManager.activeQuests.Count > 0)
        {
            if (advanceFirstActiveQuestOnly)
                return questManager.activeQuests[0];

            for (int i = 0; i < questManager.activeQuests.Count; i++)
            {
                var q = questManager.activeQuests[i];
                if (q != null && q.IsActive && !q.IsCompleted)
                    return q;
            }
        }

        if (fallbackScanAllQuestsIfActiveListEmpty && questManager.allQuests != null)
        {
            for (int i = 0; i < questManager.allQuests.Count; i++)
            {
                var q = questManager.allQuests[i];
                if (q != null && q.IsActive && !q.IsCompleted)
                    return q;
            }
        }

        return null;
    }
}