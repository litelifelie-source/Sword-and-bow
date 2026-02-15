using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAI_Archer : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 2f;
    public float detectionRange = 8f;

    [Header("Range")]
    public float attackRange = 4f;        // 사격 거리
    public float keepAwayRange = 1.8f;    // 너무 가까우면 뒤로 빠짐(0이면 비활성)

    public LayerMask targetLayer;

    private Rigidbody2D rb;
    private Animator anim;
    private EnemyArcherAttack archerAttack;
    private Transform target;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<Animator>(true);

        // 궁수 공격 필수
        archerAttack = GetComponentInChildren<EnemyArcherAttack>(true);
        if (archerAttack == null) archerAttack = GetComponent<EnemyArcherAttack>();

        if (anim == null) Debug.LogError("[EnemyAI_Archer] Animator를 찾지 못했습니다.", this);
        if (archerAttack == null) Debug.LogError("[EnemyAI_Archer] EnemyArcherAttack을 찾지 못했습니다.", this);

        rb.freezeRotation = true;
        rb.gravityScale = 0f;
    }

    void Update()
    {
        if (rb == null || anim == null || archerAttack == null) return;

        target = FindTarget();
        if (target == null) { Stop(); return; }

        Vector2 to = (Vector2)(target.position - transform.position);
        float dist = to.magnitude;

        // 1) 너무 가까우면 뒤로 빠짐(선택)
        if (keepAwayRange > 0f && dist < keepAwayRange)
        {
            Vector2 away = (-to).normalized;
            Move(away);
            return;
        }

        // 2) 사거리 밖: 접근
        if (dist > attackRange)
        {
            Move(to.normalized);
            return;
        }

        // 3) 사거리 안: 멈추고 발사 시도
        Stop();
        archerAttack.StartAttack(target); // 기존 궁수 스크립트 함수명 유지
    }

    void Move(Vector2 dir)
    {
        rb.linearVelocity = dir * moveSpeed;
        anim.SetBool("IsMoving", true);
        anim.SetFloat("MoveX", dir.x);
        anim.SetFloat("MoveY", dir.y);
    }

    void Stop()
    {
        rb.linearVelocity = Vector2.zero;
        anim.SetBool("IsMoving", false);
    }

    Transform FindTarget()
{
    Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRange, targetLayer);

    float best = Mathf.Infinity;
    Transform result = null;

    foreach (var col in hits)
    {
        if (col == null) continue;

        // ✅ 유닛 루트를 팀 기준으로 고정
        UnitTeam ut = col.GetComponentInParent<UnitTeam>();
        if (ut == null) continue;

        // ✅ Health도 루트 기준으로 고정
        Health hp = ut.GetComponentInChildren<Health>(true);
        if (hp == null) continue;

        // ✅ 다운이면 무시
        if (hp.IsDown) continue;

        Transform t = ut.transform.root;
        if (!t || !t.gameObject.activeInHierarchy) continue;

        float d = Vector2.Distance(transform.position, t.position);
        if (d < best) { best = d; result = t; }
    }

    return result;
}
}
