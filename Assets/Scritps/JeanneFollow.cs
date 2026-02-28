using UnityEngine;
using System;

[RequireComponent(typeof(Rigidbody2D))]
public class JeanneFollow : MonoBehaviour
{
    [Header("Follow Target")]
    public Transform followTarget;
    public float stopDistance = 0.6f;
    public float moveSpeed = 2.8f;

    [Header("Refs")]
    public Animator animator;
    public Rigidbody2D rb;

    void OnEnable()
{
    Debug.Log($"[ENABLED] {GetType().Name} on {name}\n{Environment.StackTrace}");
}

    [Header("Animator Params")]
    public string paramIsMoving = "IsMoving";
    public string paramMoveX = "MoveX";
    public string paramMoveY = "MoveY";

    [Header("Tuning")]
    public float axisDeadzone = 0.10f;
    public bool freezeMoveWhenAttacking = true;

    [Header("Anti Jitter")]
    [Tooltip("대각선 근처에서 좌/상(또는 우/하) 번갈아 흔들리는 걸 막는 구간. 0.12~0.25 추천")]
    public float diagonalHold = 0.18f;

    // ✅ 공격 스크립트(IsAttacking 프로퍼티) 자동 감지
    private MonoBehaviour attackScript;
    private System.Reflection.PropertyInfo isAttackingProp;

    // 마지막 바라보는 방향(정지 시 방향 유지)
    private Vector2 lastFaceDir = Vector2.down;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (animator == null) animator = GetComponentInChildren<Animator>(true);

        // IsAttacking 프로퍼티 자동 탐색
        var monos = GetComponents<MonoBehaviour>();
        foreach (var m in monos)
        {
            if (m == null) continue;
            var prop = m.GetType().GetProperty("IsAttacking");
            if (prop != null && prop.PropertyType == typeof(bool))
            {
                attackScript = m;
                isAttackingProp = prop;
                break;
            }
        }
    }

    private void FixedUpdate()
    {
        if (followTarget == null)
        {
            StopMove();
            SetAnimIdle(lastFaceDir);
            return;
        }

        if (freezeMoveWhenAttacking && IsAttacking())
        {
            StopMove();
            SetAnimIdle(lastFaceDir);
            return;
        }

        Vector2 to = (Vector2)(followTarget.position - transform.position);
        float dist = to.magnitude;

        if (dist <= stopDistance)
        {
            StopMove();
            SetAnimIdle(lastFaceDir);
            return;
        }

        Vector2 dir = to / dist;

        // 데드존
        if (Mathf.Abs(dir.x) < axisDeadzone) dir.x = 0f;
        if (Mathf.Abs(dir.y) < axisDeadzone) dir.y = 0f;

        // ✅ 4방향 스냅 + 대각선 흔들림 방지
        Vector2 moveDir = Snap4Stable(dir);

        rb.linearVelocity = moveDir * moveSpeed;

        // ✅ 이동 애니
        SetAnimMove(moveDir);
        lastFaceDir = moveDir;
    }

    // ✅ 대각선에서 축이 비슷하면 lastFaceDir 유지해서 흔들림 제거
    private Vector2 Snap4Stable(Vector2 d)
    {
        if (d.sqrMagnitude < 0.0001f) return lastFaceDir;

        float ax = Mathf.Abs(d.x);
        float ay = Mathf.Abs(d.y);

        // 대각선 근처면(거의 비슷하면) 방향을 바꾸지 않고 유지
        if (Mathf.Abs(ax - ay) < diagonalHold)
            return lastFaceDir;

        if (ax > ay)
            return d.x >= 0 ? Vector2.right : Vector2.left;
        else
            return d.y >= 0 ? Vector2.up : Vector2.down;
    }

    private void SetAnimMove(Vector2 dirForAnim)
    {
        if (animator == null) return;

        animator.SetBool(paramIsMoving, true);
        animator.SetFloat(paramMoveX, dirForAnim.x);
        animator.SetFloat(paramMoveY, dirForAnim.y);
    }

    private void SetAnimIdle(Vector2 faceDir)
    {
        if (animator == null) return;

        animator.SetBool(paramIsMoving, false);
        animator.SetFloat(paramMoveX, faceDir.x);
        animator.SetFloat(paramMoveY, faceDir.y);
    }

    private void StopMove()
    {
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    private bool IsAttacking()
    {
        if (attackScript == null || isAttackingProp == null) return false;
        try { return (bool)isAttackingProp.GetValue(attackScript); }
        catch { return false; }
    }
}
