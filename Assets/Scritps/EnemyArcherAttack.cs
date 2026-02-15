using UnityEngine;

public class EnemyArcherAttack : MonoBehaviour
{
    [Header("Projectile")]
    public ArrowProjectile projectilePrefab;
    public float projectileSpawnOffset = 0.9f;

    [Header("Attack")]
    public float attackRange = 4f;
    public float attackCooldown = 1.0f;
    public int damage = 5;

    [Header("Anim")]
    public Animator animator;
    public SpriteRenderer sr;

    private float nextAttackTime;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>(true);

        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>(true);
    }

    // EnemyAI_Archer가 호출
    public void StartAttack(Transform target)
    {
        // ✅ Destroy된 Transform(유니티 null) 방어
        if (!target) return;
        if (!target.gameObject.activeInHierarchy) return;

    Health thp = target.GetComponentInChildren<Health>(true);
    if (thp != null && thp.IsDown) return;

        if (Time.time < nextAttackTime) return;

        float dist = Vector2.Distance(transform.position, target.position);
        if (dist > attackRange) return;

        Shoot(target);
    }

    private void Shoot(Transform target)
    {
        // ✅ Shoot 들어오는 순간에도 한번 더 방어 (여기가 핵심)
        if (!target) return;
        if (!target.gameObject.activeInHierarchy) return;

        Vector2 raw = (Vector2)(target.position - transform.position);
        if (raw.sqrMagnitude < 0.0001f) return;

        // 이제부터 “정상 샷”이니 쿨다운 갱신
        nextAttackTime = Time.time + attackCooldown;

        // ✅ 발사 방향: 타겟 방향 그대로(대각선 포함)
        Vector2 shotDir = raw.normalized;

        // ✅ 애니 방향 판정: 상/하/좌/우 중 하나로만
        Vector2 animDir = Snap4(raw);

        // ✅ 발사 위치: 타겟 방향으로 오프셋
        Vector3 spawnPos = transform.position + (Vector3)(shotDir * projectileSpawnOffset);

        // ✅ 발사
        if (projectilePrefab == null)
        {
            Debug.LogError("[EnemyArcherAttack] projectilePrefab is NULL", this);
            return;
        }

        ArrowProjectile arrow = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
        if (!arrow)
        {
            Debug.LogError("[EnemyArcherAttack] Instantiate failed", this);
            return;
        }

        arrow.Initialize(shotDir, damage, transform);

        // ✅ 애니/플립 규칙
        if (animator != null)
        {
            animator.SetFloat("MoveX", animDir.x);
            animator.SetFloat("MoveY", animDir.y);

            // - 위로 쏠 때만 UP 공격모션 (AttackV)
            // - 그 외(아래/좌/우)는 전부 RIGHT 공격모션 (AttackH)
            if (animDir.y > 0.01f)
                animator.SetTrigger("AttackV");  // UP
            else
                animator.SetTrigger("AttackH");  // RIGHT (DOWN 포함)
        }

        // 좌/우일 때만 flip 처리 (오른쪽 모션 재사용)
        if (sr != null)
        {
            if (animDir.x < -0.01f) sr.flipX = true;      // LEFT -> flip
            else if (animDir.x > 0.01f) sr.flipX = false; // RIGHT -> normal
            // UP/DOWN은 flip 건드리지 않음
        }
    }

    // ✅ 애니 판정용 4방향 스냅
    private Vector2 Snap4(Vector2 delta)
    {
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            return delta.x > 0 ? Vector2.right : Vector2.left;
        else
            return delta.y > 0 ? Vector2.up : Vector2.down;
    }

    public void EndAttack() { }
}
