using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class JeanneEnemyAI : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 2.4f;
    public float detectionRange = 6f;

    [Tooltip("이 값은 'AttackAI.attackRange'와 보통 동일하게 맞추는 걸 추천")]
    public float stopDistance = 1.2f;

    [Tooltip("1차 후보 필터(성능용). 최종 판정은 TargetRule/UnitTeam")]
    public LayerMask targetLayer;

    [Header("Target Rule (final filter)")]
    public TargetRule targetRule = TargetRule.EnemiesOnly;

    [Header("Refs")]
    public Rigidbody2D rb;
    public Animator anim;
    public JeanneAttackAI attackAI;   // ✅ 잔느 평타/분배기/애니메이션은 기존 AttackAI를 그대로 사용

    [Header("Animator Params")]
    public string paramIsMoving = "IsMoving";
    public string paramMoveX = "MoveX";
    public string paramMoveY = "MoveY";

    [Header("Tuning")]
    public float axisDeadzone = 0.10f;
    public float diagonalHold = 0.18f;   // 대각선 흔들림 방지

    private Transform ownerRoot;         // ✅ 유닛 루트(UnitTeam 기준)
    private UnitTeam myTeam;

    private Transform target;
    private Vector2 lastMoveDir = Vector2.down;

    void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (anim == null) anim = GetComponentInChildren<Animator>(true);
        if (attackAI == null) attackAI = GetComponent<JeanneAttackAI>();

        myTeam = GetComponentInParent<UnitTeam>();
        ownerRoot = (myTeam != null) ? myTeam.transform : transform;

        if (rb != null) rb.freezeRotation = true;

        if (anim == null)
            Debug.LogError("[JeanneEnemyAI] Animator를 자식에서 찾지 못했습니다.", this);

        if (attackAI == null)
            Debug.LogError("[JeanneEnemyAI] JeanneAttackAI를 찾지 못했습니다.", this);
    }

    void OnEnable()
    {
        // EnemyAI 켜질 때 기존 이동(velocity) 초기화
        StopMove();
    }

    void Update()
    {
        if (rb == null || anim == null || attackAI == null) return;

        // ✅ 타겟 유니티 null 방어
        if (!target || !target.gameObject.activeInHierarchy)
            target = null;

        // ✅ 공격 중이면 이동 정지
        if (attackAI.IsAttacking)
        {
            StopMove();
            return;
        }

        // ✅ 타겟 갱신
        target = FindTarget();
        if (!target)
        {
            StopMove();
            return;
        }

        Vector2 to = (Vector2)(target.position - ownerRoot.position);
        float dist = to.magnitude;

        if (dist <= stopDistance)
        {
            StopMove();
            attackAI.RequestAttack(to); // ✅ 공격 판단/판정은 AttackAI에 위임
        }
        else
        {
            Move(Snap4(to));
        }
    }

    void Move(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.0001f)
        {
            StopMove();
            return;
        }

        rb.linearVelocity = dir * moveSpeed;

        anim.SetBool(paramIsMoving, true);
        anim.SetFloat(paramMoveX, dir.x);
        anim.SetFloat(paramMoveY, dir.y);

        lastMoveDir = dir;
    }

    void StopMove()
    {
        rb.linearVelocity = Vector2.zero;
        anim.SetBool(paramIsMoving, false);

        // 마지막 방향 유지(Idle 방향 유지가 필요하면 이걸 유지하는 게 좋음)
        anim.SetFloat(paramMoveX, lastMoveDir.x);
        anim.SetFloat(paramMoveY, lastMoveDir.y);
    }

    // ✅ 4방향 스냅 + 흔들림 방지
    Vector2 Snap4(Vector2 to)
    {
        float x = to.x;
        float y = to.y;

        // deadzone
        if (Mathf.Abs(x) < axisDeadzone) x = 0f;
        if (Mathf.Abs(y) < axisDeadzone) y = 0f;

        if (x == 0f && y == 0f) return lastMoveDir;

        float ax = Mathf.Abs(x);
        float ay = Mathf.Abs(y);

        // 대각선 근처 흔들림 방지: 이전 축을 일정 구간 유지
        float diff = Mathf.Abs(ax - ay);
        if (diff < diagonalHold)
        {
            // 이전에 수평으로 움직였으면 수평 유지, 수직이면 수직 유지
            if (Mathf.Abs(lastMoveDir.x) > Mathf.Abs(lastMoveDir.y))
                return x >= 0 ? Vector2.right : Vector2.left;
            else
                return y >= 0 ? Vector2.up : Vector2.down;
        }

        if (ax > ay)
            return x >= 0 ? Vector2.right : Vector2.left;
        else
            return y >= 0 ? Vector2.up : Vector2.down;
    }

    Transform FindTarget()
    {
        // ✅ 1차 후보: targetLayer
        Collider2D[] hits = Physics2D.OverlapCircleAll(ownerRoot.position, detectionRange, targetLayer);

        float best = Mathf.Infinity;
        Transform result = null;

        foreach (Collider2D col in hits)
        {
            if (col == null) continue;

            Health hp = col.GetComponentInParent<Health>();
            if (hp == null || hp.IsDown) continue;

            UnitTeam ut = col.GetComponentInParent<UnitTeam>();

            // ✅ 최종 판정: UnitTeam + TargetRule
            if (!PassRule(myTeam, ut, targetRule, ownerRoot)) continue;

            // ✅ 유닛 루트 기준
            Transform t = (ut != null) ? ut.transform : hp.transform;
            if (!t || !t.gameObject.activeInHierarchy) continue;

            float d = Vector2.Distance(ownerRoot.position, t.position);
            if (d < best)
            {
                best = d;
                result = t;
            }
        }

        return result;
    }

    bool PassRule(UnitTeam owner, UnitTeam other, TargetRule rule, Transform ownerRootTf)
    {
        if (rule == TargetRule.Everyone) return true;
        if (other == null) return false;

        switch (rule)
        {
            case TargetRule.EnemiesOnly:
                return owner != null && owner.team != other.team;

            case TargetRule.AlliesOnly:
                return owner != null && owner.team == other.team;

            case TargetRule.AllExceptOwner:
                return other.transform != ownerRootTf;

            default:
                return false;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Vector3 p = Application.isPlaying
            ? ((GetComponentInParent<UnitTeam>() != null) ? GetComponentInParent<UnitTeam>().transform.position : transform.position)
            : transform.position;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(p, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(p, stopDistance);
    }
#endif
}
