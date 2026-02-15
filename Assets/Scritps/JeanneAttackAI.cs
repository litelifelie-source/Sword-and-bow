using System.Collections;
using UnityEngine;

public class JeanneAttackAI : MonoBehaviour
{
    [Header("Targeting")]
    public float detectRange = 6f;
    public float attackRange = 1.2f;
    public LayerMask targetLayer; // Enemy 레이어

    [Header("Attack Timing")]
    public float attackCooldown = 1.0f;
    public float hitDelay = 0.12f;
    public float attackEndDelay = 0.35f;

    [Header("Damage")]
    public int damage = 35;
    public float attackOffset = 0.6f;
    public float hitRadius = 0.4f;

    [Header("Refs")]
    public Animator anim;                 // Sprite 자식
    public JeanneFollow follow;           // 추적(선택)
    public bool chaseTarget = true;

    [Header("Animator Params")]
    public string trigAttack = "Attack";
    public string paramIsMoving = "IsMoving";
    public string paramMoveX = "MoveX";
    public string paramMoveY = "MoveY";

    public bool IsAttacking { get; private set; }

    private float nextAttackTime;
    private Transform ownerRoot;
    private Transform currentTarget;
    private Vector2 attackDir = Vector2.down;
    private Transform defaultFollowTarget;
    private Coroutine attackCo;

    private JeanneJudgmentProc judgmentProc;

    void Awake()
    {
        ownerRoot = transform.root;

        if (anim == null) anim = GetComponentInChildren<Animator>(true);
        if (follow == null) follow = GetComponent<JeanneFollow>();
        if (follow != null) defaultFollowTarget = follow.followTarget;

        if (anim == null) Debug.LogError("[JeanneAttackAI] Animator를 찾지 못했습니다.", this);

        // ✅ 심판 Proc 연결
        judgmentProc = GetComponent<JeanneJudgmentProc>();
        if (judgmentProc == null) judgmentProc = GetComponentInParent<JeanneJudgmentProc>();
    }

    void Update()
    {
        if (IsAttacking) return;

        AcquireTarget();

        if (currentTarget == null)
        {
            if (follow != null) follow.followTarget = defaultFollowTarget;
            return;
        }

        if (chaseTarget && follow != null)
            follow.followTarget = currentTarget;

        float dist = Vector2.Distance(ownerRoot.position, currentTarget.position);
        if (dist > attackRange) return;

        TryStartAttack((Vector2)(currentTarget.position - ownerRoot.position));
    }

    void AcquireTarget()
    {
        // 기존 타겟 유지
        if (currentTarget != null)
        {
            var hp = currentTarget.GetComponentInParent<Health>();
            if (hp == null || hp.IsDown) { currentTarget = null; return; }

            float d = Vector2.Distance(ownerRoot.position, currentTarget.position);
            if (d <= detectRange) return;
        }

        currentTarget = null;
        float best = float.MaxValue;

        Collider2D[] hits = Physics2D.OverlapCircleAll(ownerRoot.position, detectRange, targetLayer);
        for (int i = 0; i < hits.Length; i++)
        {
            Health hp = hits[i].GetComponentInParent<Health>();
            if (hp == null || hp.IsDown) continue;

            float d = Vector2.Distance(ownerRoot.position, hits[i].transform.position);
            if (d < best)
            {
                best = d;
                currentTarget = hp.transform; // Health 루트
            }
        }
    }

    Vector2 Snap4(Vector2 d)
    {
        if (d.sqrMagnitude < 0.0001f) return attackDir;

        if (Mathf.Abs(d.x) > Mathf.Abs(d.y))
            return d.x >= 0 ? Vector2.right : Vector2.left;
        else
            return d.y >= 0 ? Vector2.up : Vector2.down;
    }

    void TryStartAttack(Vector2 dir)
    {
        if (anim == null) return;
        if (Time.time < nextAttackTime) return;

        nextAttackTime = Time.time + attackCooldown;

        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.down;

        // ✅ 방향 고정 (4방향)
        attackDir = Snap4(dir);

        IsAttacking = true;

        ApplyAttackFacing(attackDir);

        anim.SetBool(paramIsMoving, false);
        anim.ResetTrigger(trigAttack);
        anim.SetTrigger(trigAttack);

        if (attackCo != null) StopCoroutine(attackCo);
        attackCo = StartCoroutine(AttackRoutine());
    }

    void ApplyAttackFacing(Vector2 dir)
    {
        anim.SetFloat(paramMoveX, dir.x);
        anim.SetFloat(paramMoveY, dir.y);
    }

    IEnumerator AttackRoutine()
    {
        yield return new WaitForSeconds(hitDelay);
        DealDamage();

        float remain = Mathf.Max(0f, attackEndDelay - hitDelay);
        yield return new WaitForSeconds(remain);
        EndAttack();
    }

    void DealDamage()
    {
        Vector2 center = (Vector2)ownerRoot.position + attackDir * attackOffset;
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, hitRadius, targetLayer);

        bool didHit = false;

        foreach (Collider2D col in hits)
        {
            if (col.transform.root == ownerRoot) continue;

            Health hp = col.GetComponentInParent<Health>();
            if (hp != null && !hp.IsDown)
            {
                hp.TakeDamage(damage);
                didHit = true;
            }
        }

        // ✅ 평타 적중 시에만 확률 체크
        if (didHit && judgmentProc != null)
            judgmentProc.TryStartJudgment();
    }

    void EndAttack()
    {
        IsAttacking = false;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
#endif
}
