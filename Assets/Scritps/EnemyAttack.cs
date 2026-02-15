using UnityEngine;

public class EnemyAttack : MonoBehaviour
{
    public int damage = 10;
    public float attackOffset = 0.6f;
    public float hitRadius = 0.4f;
    public float attackCooldown = 1f;
    public LayerMask targetLayer;

    private float nextAttackTime;
    private Vector2 attackDir = Vector2.right;

    private Animator anim;
    private SpriteRenderer sr;
    private Transform ownerRoot;

    public bool IsAttacking { get; private set; }

    void Awake()
    {
        anim = GetComponent<Animator>();
        if (anim == null) anim = GetComponentInChildren<Animator>(true);

        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>(true);

        ownerRoot = transform.root;

        if (anim == null) Debug.LogError("[EnemyAttack] Animator를 찾지 못했습니다.", this);
        if (sr == null) Debug.LogError("[EnemyAttack] SpriteRenderer를 찾지 못했습니다.", this);
    }

    // 4방향 스냅(선택이지만 추천: 방향 흔들림/깜빡임 방지)
    private Vector2 Snap4(Vector2 d)
    {
        if (Mathf.Abs(d.x) > Mathf.Abs(d.y))
            return d.x > 0 ? Vector2.right : Vector2.left;
        else
            return d.y > 0 ? Vector2.up : Vector2.down;
    }

    public void TryStartAttack(Vector2 dir)
    {
        if (anim == null || sr == null) return;
        if (IsAttacking) return;
        if (Time.time < nextAttackTime) return;

        nextAttackTime = Time.time + attackCooldown;

        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;

        // ✅ 방향 고정
        attackDir = Snap4(dir);

        // ✅ 왼쪽 공격이 필요할 때만 flip (오른쪽 공격 클립 1개로 좌/우 대응)
        //    이동에는 flip 영향 0이 목표니까 공격 종료 시 무조건 false로 되돌릴 것.
        if (attackDir == Vector2.left) sr.flipX = true;
        else sr.flipX = false;

        // ✅ 한 프레임 끼는 이동 방지: 트리거보다 먼저 켠다
        IsAttacking = true;

        anim.ResetTrigger("Attack");
        anim.SetTrigger("Attack");
    }

    // 애니 이벤트(히트 프레임)
    public void DealDamage()
    {
        if (ownerRoot == null) ownerRoot = transform.root;

        Vector2 center = (Vector2)ownerRoot.position + attackDir * attackOffset;
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, hitRadius, targetLayer);

        foreach (Collider2D col in hits)
        {
            if (col.transform.root == ownerRoot) continue;

            Health hp = col.GetComponentInParent<Health>();
            if (hp != null && !hp.IsDown)
                hp.TakeDamage(damage);
        }
    }

    // 애니 이벤트(진짜 마지막 프레임)
    public void AnimEvent_EndAttack()
    {
        IsAttacking = false;

        // ✅ 핵심: 공격 끝나면 무조건 원상복구(이동이 flip 영향 받지 않게)
        if (sr != null) sr.flipX = false;
    }
public void PlayAttackSfx()
{
    AudioSource audio = GetComponent<AudioSource>();
    if (audio != null)
        audio.PlayOneShot(audio.clip);
}
}
