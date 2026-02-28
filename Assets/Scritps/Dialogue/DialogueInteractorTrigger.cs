using UnityEngine;

/// <summary>
/// DialogueInteractorTrigger (Minimal + NPC range condition)
/// - E를 눌렀을 때:
///   1) DialogueManager 존재
///   2) Dialogue가 재생 중(IsPlaying)
///   3) (옵션) 주변 반경 내 npcLayer Collider2D가 존재
///   => 조건 만족 시 Next()
///
/// 안정성:
/// - GetKeyDown 사용 (프레임 연타 방지 기본)
/// - 추가로 쿨다운(디바운스) 옵션 제공 (키보드 반복입력/버튼 바운스 방지)
/// </summary>
public class DialogueInteractorTrigger : MonoBehaviour
{
    [Header("Refs (optional)")]
    public DialogueManager dialogueManager;

    [Header("Input")]
    public KeyCode interactKey = KeyCode.E;

    [Header("Debounce (optional)")]
    [Tooltip("Next 연타 방지(초). 0이면 비활성. 0.08~0.15 권장")]
    [Min(0f)] public float nextCooldown = 0.12f;

    [Header("NPC Range Filter (2D)")]
    public LayerMask npcLayer = ~0;
    [Min(0f)] public float npcRange = 1.5f;
    public bool requireNpcInRange = true;

    [Header("Debug")]
    public bool debugLog = false;

    float _nextReadyTime;

    private void Awake()
    {
        if (dialogueManager == null)
            dialogueManager = DialogueManager.I != null ? DialogueManager.I : FindFirstObjectByType<DialogueManager>();

        if (!dialogueManager && debugLog)
            Debug.LogWarning("[DIT-Min] DialogueManager not found.", this);
    }

    private bool HasNpcInRange()
    {
        if (!requireNpcInRange) return true;

        // ✅ 2D 기준: 주변에 npcLayer Collider2D가 하나라도 있으면 OK
        Collider2D hit = Physics2D.OverlapCircle(transform.position, npcRange, npcLayer);
        return hit != null;
    }

    private bool CooldownReady()
    {
        if (nextCooldown <= 0f) return true;

        float t = Time.unscaledTime;
        if (t < _nextReadyTime) return false;

        _nextReadyTime = t + nextCooldown;
        return true;
    }

    private void Update()
    {
        if (!Input.GetKeyDown(interactKey)) return;
        if (!dialogueManager) return;

        // ✅ 대화 중이 아니면 Next 금지
        if (!dialogueManager.IsPlaying)
        {
            if (debugLog) Debug.Log("[DIT-Min] E ignored (dialogue not playing)", this);
            return;
        }

        // ✅ 주변 NPC 조건
        if (!HasNpcInRange())
        {
            if (debugLog) Debug.Log("[DIT-Min] E ignored (no NPC in range)", this);
            return;
        }

        // ✅ 디바운스(선택)
        if (!CooldownReady())
        {
            if (debugLog) Debug.Log("[DIT-Min] E ignored (cooldown)", this);
            return;
        }

        dialogueManager.Next();
        if (debugLog) Debug.Log("[DIT-Min] E -> Next()", this);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Gizmos는 3D지만, 반경 표시로는 충분
        Gizmos.DrawWireSphere(transform.position, npcRange);
    }
#endif
}