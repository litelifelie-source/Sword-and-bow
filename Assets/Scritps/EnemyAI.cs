using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAI : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 2f;
    public float detectionRange = 6f;
    public float attackRange = 1.2f;
    public LayerMask targetLayer;

    private Rigidbody2D rb;
    private Animator anim;
    private EnemyAttack attack;

    private Transform target;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<Animator>(true);

        attack = GetComponentInChildren<EnemyAttack>(true);
        if (attack == null) attack = GetComponent<EnemyAttack>();

        if (anim == null) Debug.LogError("[EnemyAI] Animator를 자식에서 찾지 못했습니다.", this);

        rb.freezeRotation = true;
    }

    void Update()
    {
        if (rb == null || anim == null || attack == null) return;

        // ✅ 타겟이 Destroy/비활성화되면 즉시 비우기 (유니티 null 방어)
        if (!target || !target.gameObject.activeInHierarchy)
            target = null;

        if (attack.IsAttacking)
        {
            Stop();
            return;
        }

        target = FindTarget();
        if (!target)
        {
            Stop();
            return;
        }

        Vector2 to = (Vector2)(target.position - transform.position);
        float dist = to.magnitude;

        if (dist <= attackRange)
        {
            Stop();
            attack.TryStartAttack(to);
        }
        else
        {
            Move(to.normalized);
        }
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

        foreach (Collider2D col in hits)
        {
            if (col == null) continue;

            Health hp = col.GetComponentInParent<Health>();
            if (hp == null || hp.IsDown) continue;

            Transform t = hp.transform; // Health 붙은 루트 기준
            if (!t || !t.gameObject.activeInHierarchy) continue;

            float d = Vector2.Distance(transform.position, t.position);
            if (d < best)
            {
                best = d;
                result = t;
            }
        }
        return result;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
