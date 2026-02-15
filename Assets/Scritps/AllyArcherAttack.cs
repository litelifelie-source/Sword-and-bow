using UnityEngine;

public class AllyArcherAttack : MonoBehaviour
{
    [Header("Projectile")]
    public ArrowProjectile projectilePrefab;
    public float projectileSpawnOffset = 0.9f;

    [Header("Attack")]
    public float detectRange = 6f;
    public float attackRange = 4f;
    public float attackCooldown = 1.0f;
    public int damage = 5;

    [Header("Target Layer (ENEMY ONLY)")]
    public LayerMask enemyLayer;

    [Header("Anim")]
    public Animator animator;
    public SpriteRenderer sr;

    private float nextAttackTime;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
    }

    private void Update()
    {
        if (Time.time < nextAttackTime) return;

        Transform target = FindClosestEnemy();
        if (target == null) return;

        float dist = Vector2.Distance(transform.position, target.position);
        if (dist > attackRange) return;

        Shoot(target);
    }

    private Transform FindClosestEnemy()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectRange, enemyLayer);
        if (hits == null || hits.Length == 0) return null;

        float best = Mathf.Infinity;
        Transform bestT = null;

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null) continue;

            Transform t = hits[i].transform;

            // 자기 자신 제외 (부모/자식 구조에서도 안전하게)
            if (t == transform || t.root == transform.root) continue;

            // ✅ 팀 확인: Enemy만 (레이어가 꼬여도 방어)
            UnitTeam team = hits[i].GetComponentInParent<UnitTeam>();
            if (team != null && team.team != Team.Enemy) continue;

            // ✅ HP0(Down) 제외: 피0이면 더 이상 타겟으로 안 잡음 (핵심)
            Health hp = hits[i].GetComponentInParent<Health>();
            if (hp != null && hp.IsDown) continue;

            float d = Vector2.Distance(transform.position, t.position);
            if (d < best)
            {
                best = d;
                bestT = t;
            }
        }

        return bestT;
    }

    private void Shoot(Transform target)
    {
        if (target == null) return;

        // ✅ 혹시 Update~Shoot 사이에 Down 되면 공격 취소(추가 안전장치)
        Health hp = target.GetComponentInParent<Health>();
        if (hp != null && hp.IsDown) return;

        nextAttackTime = Time.time + attackCooldown;

        Vector2 raw = (Vector2)(target.position - transform.position);
        if (raw.sqrMagnitude < 0.0001f) return;

        Vector2 shotDir = raw.normalized;
        Vector2 animDir = Snap4(raw);

        Vector3 spawnPos = transform.position + (Vector3)(shotDir * projectileSpawnOffset);

        if (projectilePrefab == null)
        {
            Debug.LogError("[AllyArcherAttack] projectilePrefab is NULL");
            return;
        }

        ArrowProjectile arrow = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
        if (arrow == null)
        {
            Debug.LogError("[AllyArcherAttack] Instantiate failed");
            return;
        }

        arrow.Initialize(shotDir, damage, transform);

        if (animator != null)
        {
            animator.SetFloat("MoveX", animDir.x);
            animator.SetFloat("MoveY", animDir.y);

            if (Mathf.Abs(animDir.x) > 0.01f) animator.SetTrigger("AttackH");
            else animator.SetTrigger("AttackV");
        }

        if (sr != null && Mathf.Abs(animDir.x) > 0.01f)
            sr.flipX = animDir.x < 0f;
    }

    private Vector2 Snap4(Vector2 delta)
    {
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            return delta.x > 0 ? Vector2.right : Vector2.left;
        else
            return delta.y > 0 ? Vector2.up : Vector2.down;
    }

    public void EndAttack() { }
}
