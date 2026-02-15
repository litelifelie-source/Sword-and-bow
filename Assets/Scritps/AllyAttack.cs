using UnityEngine;

public class AllyAttack : MonoBehaviour
{
    [Header("Targeting")]
    public LayerMask enemyLayer;
    public float detectRange = 5f;
    public float attackRange = 1.5f;

    [Header("Attack")]
    public float attackCooldown = 0.6f;
    public int damage = 5;

    [Header("Hit")]
    public float attackOffset = 0.75f;
    public float hitRadius = 0.45f;

    [Header("Surround Move")]
    public float surroundStopDist = 0.12f;
    public float surroundRadiusScale = 0.95f; // attackRange * scale

    private AllyFollow follow;
    private Animator animator;
    private float nextAttackTime;

    private Transform target;
    private Transform lastTargetRoot;

    private void Awake()
    {
        follow = GetComponent<AllyFollow>();

        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void OnDisable()
    {
        if (lastTargetRoot != null)
            SurroundCoordinator.RemoveAgent(lastTargetRoot, GetInstanceID());
    }

    private void Update()
    {
        // 타겟이 Enemy가 아니게 되었거나, 비활성이면 갱신
        if (target == null || !target.gameObject.activeInHierarchy || !IsEnemyTarget(target))
            target = FindClosestEnemyOnly();

        // 타겟 없으면: 추격/정지 해제 + 슬롯 제거
        if (target == null)
        {
            if (lastTargetRoot != null)
            {
                SurroundCoordinator.RemoveAgent(lastTargetRoot, GetInstanceID());
                lastTargetRoot = null;
            }

            if (follow != null)
            {
                follow.StopChase();
                follow.SetBlockMove(false);
            }
            return;
        }

        // 타겟 루트가 바뀌면 이전 그룹에서 제거
        Transform targetRoot = target.root;
        if (lastTargetRoot != targetRoot)
        {
            if (lastTargetRoot != null)
                SurroundCoordinator.RemoveAgent(lastTargetRoot, GetInstanceID());
            lastTargetRoot = targetRoot;
        }

        float sqrDist = (target.position - transform.position).sqrMagnitude;
        float atkSqr = attackRange * attackRange;

        if (sqrDist <= atkSqr)
        {
            if (follow != null)
            {
                follow.StopChase();
                follow.SetBlockMove(true);
            }

            if (Time.time >= nextAttackTime)
            {
                DoAttackEnemyOnly();
                nextAttackTime = Time.time + attackCooldown;
            }
        }
        else
        {
            if (follow != null)
            {
                follow.SetBlockMove(false);

                float baseRadius = Mathf.Max(1.0f, attackRange * surroundRadiusScale);
                Vector2 slotPos = SurroundCoordinator.GetSlotPosition(targetRoot, GetInstanceID(), baseRadius);

                // ✅ 타겟이 아니라 "슬롯 좌표"로 이동 -> 포위진 펼쳐짐
                follow.StartChasePosition(slotPos, surroundStopDist);
            }
        }
    }

    private bool IsEnemyTarget(Transform t)
    {
        if (t == null) return false;

        UnitTeam ut = t.GetComponentInParent<UnitTeam>();
        if (ut == null) return false;
        if (ut.team != Team.Enemy) return false;

        if (t.root == transform.root) return false;

        Health hp = t.GetComponentInParent<Health>();
        if (hp != null && hp.IsDown) return false;

        return true;
    }

    private Transform FindClosestEnemyOnly()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectRange, enemyLayer);
        if (hits == null || hits.Length == 0) return null;

        Transform best = null;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit == null) continue;

            Transform t = hit.transform;
            if (t.root == transform.root) continue;

            UnitTeam other = hit.GetComponentInParent<UnitTeam>();
            if (other == null || other.team != Team.Enemy) continue;

            Health hp = hit.GetComponentInParent<Health>();
            if (hp != null && hp.IsDown) continue;

            float sqr = (t.position - transform.position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = t;
            }
        }

        return best;
    }

    private void DoAttackEnemyOnly()
{
    if (animator != null)
        animator.SetTrigger("Attack");

    Vector2 dir = Vector2.right;
    if (follow != null && follow.LastMoveDir.sqrMagnitude > 0.0001f)
        dir = follow.LastMoveDir.normalized;

    Vector2 center = (Vector2)transform.position + dir * attackOffset;

    Collider2D[] hits = Physics2D.OverlapCircleAll(center, hitRadius, enemyLayer);
    for (int i = 0; i < hits.Length; i++)
    {
        var hit = hits[i];
        if (hit == null) continue;

        // ✅ 자기 자신/같은 루트 제외
        if (hit.transform.root == transform.root) continue;

        // ✅ 플레이어는 무조건 제외 (레이어/마스크 꼬여도 안전)
        if (hit.CompareTag("Player") || hit.GetComponentInParent<PlayerController>() != null)
            continue;

        UnitTeam other = hit.GetComponentInParent<UnitTeam>();
        if (other == null || other.team != Team.Enemy) continue;

        Health hp = hit.GetComponentInParent<Health>();
        if (hp != null && !hp.IsDown)
            hp.TakeDamage(damage);
    }
}
}
