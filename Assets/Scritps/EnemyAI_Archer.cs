using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAI_Archer : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 2f;
    public float detectionRange = 8f;

    [Header("Range")]
    public float attackRange = 4f;
    public float keepAwayRange = 1.8f;

    [Tooltip("keepAway 경계에서 앞뒤로 떨리는 것 방지(권장 0.2~0.4)")]
    public float keepAwayHysteresis = 0.3f;

    public LayerMask targetLayer;

    private Rigidbody2D rb;
    private Animator anim;
    private EnemyArcherAttack archerAttack;
    private Transform target;

    // ✅ 물리 적용용(최종 의도 속도)
    private Vector2 desiredVel;
    private Vector2 desiredDir;
    private bool desiredMove;

    // ✅ keep-away 상태 래치(토글 방지)
    private bool isKeepingAway;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<Animator>(true);

        archerAttack = GetComponentInChildren<EnemyArcherAttack>(true);
        if (archerAttack == null) archerAttack = GetComponent<EnemyArcherAttack>();

        rb.freezeRotation = true;
        rb.gravityScale = 0f;

        // ✅ 안정화 옵션(권장)
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (anim == null) Debug.LogError("[EnemyAI_Archer] Animator를 찾지 못했습니다.", this);
        if (archerAttack == null) Debug.LogError("[EnemyAI_Archer] EnemyArcherAttack을 찾지 못했습니다.", this);
    }

    void Update()
    {
        if (rb == null || anim == null || archerAttack == null) return;

        target = FindTarget();
        if (target == null)
        {
            desiredMove = false;
            desiredVel = Vector2.zero;
            anim.SetBool("IsMoving", false);
            isKeepingAway = false;
            return;
        }

        Vector2 to = (Vector2)(target.position - transform.position);
        float dist = to.magnitude;

        // ✅ keep-away 토글 방지(들어갈 때/나올 때 범위를 다르게)
        float enter = keepAwayRange;
        float exit  = keepAwayRange + keepAwayHysteresis;

        if (keepAwayRange > 0f)
        {
            if (!isKeepingAway && dist < enter) isKeepingAway = true;
            else if (isKeepingAway && dist > exit) isKeepingAway = false;
        }
        else
        {
            isKeepingAway = false;
        }

        // 1) 너무 가까우면 뒤로 빠짐
        if (isKeepingAway)
        {
            Vector2 away = (-to).normalized;
            SetMoveIntent(away);
            return;
        }

        // 2) 사거리 밖: 접근
        if (dist > attackRange)
        {
            SetMoveIntent(to.normalized);
            return;
        }

        // 3) 사거리 안: 멈추고 발사
        desiredMove = false;
        desiredVel = Vector2.zero;
        anim.SetBool("IsMoving", false);

        archerAttack.StartAttack(target);
    }

    void FixedUpdate()
    {
        // ✅ 물리는 FixedUpdate에서만
        rb.linearVelocity = desiredVel;

        // ✅ 혹시라도 다른 요인으로 튀면 즉시 클램프(안전장치)
        float max = moveSpeed;
        if (rb.linearVelocity.sqrMagnitude > max * max)
            rb.linearVelocity = rb.linearVelocity.normalized * max;

        if (desiredMove)
        {
            anim.SetBool("IsMoving", true);
            anim.SetFloat("MoveX", desiredDir.x);
            anim.SetFloat("MoveY", desiredDir.y);
        }
    }

    void SetMoveIntent(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        dir.Normalize();

        desiredMove = true;
        desiredDir = dir;
        desiredVel = dir * moveSpeed;
    }

    void Stop()
    {
        desiredMove = false;
        desiredVel = Vector2.zero;
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

            UnitTeam ut = col.GetComponentInParent<UnitTeam>();
            if (ut == null) continue;

            Health hp = ut.GetComponentInChildren<Health>(true);
            if (hp == null) continue;
            if (hp.IsDown) continue;

            Transform t = ut.transform.root;
            if (!t || !t.gameObject.activeInHierarchy) continue;

            float d = Vector2.Distance(transform.position, t.position);
            if (d < best) { best = d; result = t; }
        }

        return result;
    }
}
