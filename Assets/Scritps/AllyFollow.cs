using UnityEngine;

public class AllyFollow : MonoBehaviour
{
    [HideInInspector] public FormationManager formation;
    [HideInInspector] public int slotIndex = -1;

    [Header("Move")]
    public float moveSpeed = 2.5f;
    public float stopDistance = 0.25f;

    [Header("State")]
    public bool blockMove = false;

    [Header("Combat Chase")]
    public bool chaseMode = false;
    public Transform chaseTarget;
    public float chaseStopDistance = 1.2f;

    // ✅ 추가: 좌표(포위 슬롯) 추적 모드
    private bool chasePointMode = false;
    private Vector2 chasePoint;

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer sr;

    private Vector2 lastMoveDir = Vector2.down;
    public Vector2 LastMoveDir => lastMoveDir;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        sr = GetComponentInChildren<SpriteRenderer>();
    }

    public void SetBlockMove(bool v)
    {
        blockMove = v;
        if (blockMove) StopNow();
    }

    public void StartChase(Transform target, float stopDist)
    {
        if (target == null) return;

        chaseMode = true;
        chaseTarget = target;
        chasePointMode = false; // ✅ 중요
        chaseStopDistance = Mathf.Max(0.1f, stopDist);
        blockMove = false;
    }

    // ✅ 추가: "좌표"를 추적
    public void StartChasePosition(Vector2 point, float stopDist)
    {
        chaseMode = true;
        chaseTarget = null;
        chasePointMode = true;
        chasePoint = point;
        chaseStopDistance = Mathf.Max(0.05f, stopDist);
        blockMove = false;
    }

    public void StopChase()
    {
        chaseMode = false;
        chaseTarget = null;
        chasePointMode = false; // ✅ 중요
    }

    public void StopNow()
    {
        if (rb) rb.linearVelocity = Vector2.zero;

        if (animator != null)
        {
            animator.SetBool("IsMoving", false);
            SetMoveParamsAndFlip(lastMoveDir);
        }
    }

    private void FixedUpdate()
    {
        if (blockMove)
        {
            StopNow();
            return;
        }

        if (chaseMode)
        {
            Vector2 pos = rb ? rb.position : (Vector2)transform.position;

            Vector2 targetPos;

            if (chasePointMode)
            {
                targetPos = chasePoint;
            }
            else
            {
                if (chaseTarget == null || !chaseTarget.gameObject.activeInHierarchy)
                {
                    StopChase();
                    return;
                }
                targetPos = chaseTarget.position;
            }

            Vector2 delta = targetPos - pos;
            float dist = delta.magnitude;

            if (dist <= chaseStopDistance)
            {
                StopNow();
                return;
            }

            Vector2 dir = delta / dist;
            lastMoveDir = dir;

            if (rb)
                rb.linearVelocity = dir * moveSpeed;
            else
                transform.position = Vector2.MoveTowards(transform.position, targetPos, moveSpeed * Time.fixedDeltaTime);

            if (animator != null)
            {
                animator.SetBool("IsMoving", true);
                SetMoveParamsAndFlip(dir);
            }

            return;
        }

        if (formation == null || slotIndex < 0) return;

        Vector2 slotPos = formation.GetSlotWorldPosition(slotIndex);
        Vector2 pos2 = rb ? rb.position : (Vector2)transform.position;

        Vector2 delta2 = slotPos - pos2;
        float dist2 = delta2.magnitude;

        if (dist2 <= stopDistance)
        {
            StopNow();
            return;
        }

        Vector2 dir2 = delta2 / dist2;
        lastMoveDir = dir2;

        if (rb)
            rb.linearVelocity = dir2 * moveSpeed;
        else
            transform.position = Vector2.MoveTowards(transform.position, slotPos, moveSpeed * Time.fixedDeltaTime);

        if (animator != null)
        {
            animator.SetBool("IsMoving", true);
            SetMoveParamsAndFlip(dir2);
        }
    }

    private void SetMoveParamsAndFlip(Vector2 direction)
    {
        if (animator == null) return;

        Vector2 d = direction;
        if (d.sqrMagnitude < 0.0001f)
            d = lastMoveDir;

        if (Mathf.Abs(d.x) > Mathf.Abs(d.y))
            d = d.x > 0 ? Vector2.right : Vector2.left;
        else
            d = d.y > 0 ? Vector2.up : Vector2.down;

        if (d.x != 0f)
        {
            animator.SetFloat("MoveX", 1f);
            animator.SetFloat("MoveY", 0f);
            if (sr != null) sr.flipX = (d == Vector2.left);
        }
        else
        {
            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", d.y);
        }
    }

    private void OnDisable()
    {
        if (formation != null)
            formation.Unregister(this);
    }
}
