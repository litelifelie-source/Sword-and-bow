using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class NamedQuestStart : MonoBehaviour
{
    [Header("Quest")]
    [Tooltip("QuestLine.questId와 반드시 일치해야 합니다.")]
    public string questId;

    [Header("Filter")]
    [Tooltip("플레이어 오브젝트 Tag")]
    public string playerTag = "Player";

    [Header("Debug")]
    public bool verboseLog = false;

    private void Reset()
    {
        // 최소규격: 트리거로 쓰는 전제라서 자동으로 켜줍니다.
        var col = GetComponent<Collider2D>();
        if (col) col.isTrigger = true;
    }

    private void Awake()
    {
        // 최소규격: 실수 방지로 Trigger 강제
        var col = GetComponent<Collider2D>();
        if (col && !col.isTrigger) col.isTrigger = true;

        if (verboseLog)
            Debug.Log($"[NQS] Awake questId={(string.IsNullOrEmpty(questId) ? "EMPTY" : questId)}", this);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other) return;

        // 1) 플레이어만
        if (!other.CompareTag(playerTag)) return;

        // 2) questId 필수
        if (string.IsNullOrEmpty(questId))
        {
            if (verboseLog) Debug.LogWarning("[NQS] questId is EMPTY. Start blocked.", this);
            return;
        }

        // 3) QuestManager 필수
        var qm = QuestManager.I;
        if (qm == null)
        {
            if (verboseLog) Debug.LogWarning("[NQS] QuestManager.I is null. Start blocked.", this);
            return;
        }

        // 4) 시작
        bool ok = qm.TryStartQuestById(questId, resetSteps: false);

        if (verboseLog)
            Debug.Log($"[NQS] TryStartQuestById questId={questId} ok={ok}", this);
    }
}