using System.Collections;
using UnityEngine;

public class AllyAttack : MonoBehaviour
{
    [Header("Targeting")]
    public LayerMask enemyLayer;
    public float detectRange = 6f;
    public float attackRange = 1.5f;
    public float hysteresis = 0.15f;

    [Tooltip("추적 중 더 가까운 적으로 갈아탈지(0~1). 낮을수록 쉽게 갈아탐. 예: 0.6 = 40% 더 가까우면 교체")]
    [Range(0.1f, 1f)]
    public float retargetRatio = 0.6f;

    [Tooltip("타겟 재탐색 주기(초). 너무 자주 하면 성능↓, 너무 길면 멍청해짐")]
    public float retargetInterval = 0.12f;

    [Header("Damage")]
    public int damage = 5;
    public float attackOffset = 0.75f;
    public float hitRadius = 0.45f;

    [Header("Timing")]
    public float attackCooldown = 0.6f;
    public float hitDelay = 0.12f;
    public float attackEndDelay = 0.10f;

    [Header("Animator Params")]
    public string trigAttack = "Attack";
    public string boolAttackR = "AttackR";
    public string boolAttackL = "AttackL";

    // ✅ 애니메이션용(4방향 스냅)
    private Vector2 attackDir = Vector2.right;

    // ✅ 판정용(대각선 포함)
    private Vector2 hitDir = Vector2.right;

    private Animator anim;
    private Transform ownerRoot;
    private AllyFollow follow;
    private UnitTeam myTeam;

    public bool IsAttacking { get; private set; }

    private Transform target;

    private float nextAttackTime;
    private Coroutine attackCo;

    private int playerLayer;
    private float nextRetargetTime;

    private void Awake()
    {
        follow = GetComponentInParent<AllyFollow>();

        anim = GetComponent<Animator>();
        if (anim == null) anim = GetComponentInChildren<Animator>(true);

        ownerRoot = transform.root;
        myTeam = GetComponentInParent<UnitTeam>();

        playerLayer = LayerMask.NameToLayer("Player");

        if (anim == null)
            Debug.LogError("[AllyAttack] Animator를 찾지 못했습니다.", this);
    }

    private void Update()
    {
        if (myTeam != null && myTeam.team != Team.Ally)
            return;

        if (IsAttacking) return;

        if (!IsValidEnemyTarget(target))
            target = null;

        float enterRange = Mathf.Max(0.01f, attackRange - hysteresis);

        // ✅ 1) 근접 우선권: 공격 진입 범위 안의 적을 최우선
        Transform close = FindClosestEnemy(enterRange);
        if (IsValidEnemyTarget(close))
            target = close;

        // ✅ 2) 주기적으로 더 좋은 타겟으로 갱신
        if (Time.time >= nextRetargetTime)
        {
            nextRetargetTime = Time.time + Mathf.Max(0.02f, retargetInterval);

            if (target == null)
            {
                target = FindClosestEnemy(detectRange);
            }
            else
            {
                Transform best = FindClosestEnemy(detectRange);
                if (IsValidEnemyTarget(best) && best != target)
                {
                    float dOld = ((Vector2)target.position - (Vector2)ownerRoot.position).sqrMagnitude;
                    float dNew = ((Vector2)best.position - (Vector2)ownerRoot.position).sqrMagnitude;

                    if (dNew < dOld * retargetRatio)
                        target = best;
                }
            }
        }

        if (target == null)
        {
            if (follow != null)
            {
                follow.StopChase();
                follow.SetBlockMove(false);
            }
            return;
        }

        float dist = Vector2.Distance(ownerRoot.position, target.position);
        bool inAttackEnter = dist <= enterRange;

        if (inAttackEnter)
        {
            if (follow != null)
            {
                follow.StopChase();
                follow.SetBlockMove(true);
            }

            Vector2 dir = ((Vector2)target.position - (Vector2)ownerRoot.position);
            TryStartAttack(dir);
            return;
        }

        if (follow != null)
        {
            follow.SetBlockMove(false);
            follow.StartChase(target, attackRange * 0.9f);
        }
    }

    private bool IsValidEnemyTarget(Transform t)
    {
        if (t == null) return false;
        if (!t.gameObject.activeInHierarchy) return false;

        if (t.gameObject.layer == playerLayer) return false;
        if (t.transform.root == ownerRoot) return false;

        UnitTeam ut = t.GetComponentInParent<UnitTeam>();
        if (ut == null || ut.team != Team.Enemy) return false;

        Health hp = t.GetComponentInParent<Health>();
        if (hp != null && hp.IsDown) return false;

        return true;
    }

    private Transform FindClosestEnemy(float range)
    {
        if (range <= 0f) return null;

        Collider2D[] hits = Physics2D.OverlapCircleAll(ownerRoot.position, range, enemyLayer);
        if (hits == null || hits.Length == 0) return null;

        Transform best = null;
        float bestSqr = float.MaxValue;
        Vector2 me = ownerRoot.position;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D h = hits[i];
            if (h == null) continue;

            Transform cand = h.transform;
            if (!IsValidEnemyTarget(cand)) continue;

            float sqr = ((Vector2)cand.position - me).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = cand;
            }
        }
        return best;
    }

    private Vector2 Snap4(Vector2 d)
    {
        if (d.sqrMagnitude < 0.0001f) return attackDir;

        if (Mathf.Abs(d.x) > Mathf.Abs(d.y))
            return d.x > 0 ? Vector2.right : Vector2.left;
        else
            return d.y > 0 ? Vector2.up : Vector2.down;
    }

    private void TryStartAttack(Vector2 dir)
    {
        if (anim == null) return;
        if (Time.time < nextAttackTime) return;
        if (IsAttacking) return;

        nextAttackTime = Time.time + attackCooldown;

        // ✅ 판정용 방향(대각선 포함)
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        hitDir = dir.normalized;

        // ✅ 애니용 방향(4방향 스냅)
        attackDir = Snap4(dir);

        // (요구사항 유지) 위=오른쪽, 아래=왼쪽
        if (attackDir == Vector2.up) attackDir = Vector2.right;
        else if (attackDir == Vector2.down) attackDir = Vector2.left;

        anim.SetBool(boolAttackR, false);
        anim.SetBool(boolAttackL, false);
        if (attackDir == Vector2.left) anim.SetBool(boolAttackL, true);
        else anim.SetBool(boolAttackR, true);

        IsAttacking = true;

        anim.ResetTrigger(trigAttack);
        anim.SetTrigger(trigAttack);

        if (attackCo != null) StopCoroutine(attackCo);
        attackCo = StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        yield return new WaitForSeconds(hitDelay);

        if (myTeam != null && myTeam.team != Team.Ally)
        {
            EndAttackImmediate();
            yield break;
        }

        // 타겟이 유효하면 타격
        if (IsValidEnemyTarget(target))
            DoDamageNow();

        if (attackEndDelay > 0f)
            yield return new WaitForSeconds(attackEndDelay);

        EndAttackImmediate();
    }

    private void DoDamageNow()
    {
        // ✅ 판정은 hitDir(대각선 포함)로 전방 오프셋
        Vector2 center = (Vector2)ownerRoot.position + hitDir * attackOffset;

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, hitRadius, enemyLayer);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (col == null) continue;

            if (col.gameObject.layer == playerLayer) continue;
            if (col.transform.root == ownerRoot) continue;

            UnitTeam ut = col.GetComponentInParent<UnitTeam>();
            if (ut == null || ut.team != Team.Enemy) continue;

            Health hp = col.GetComponentInParent<Health>();
            if (hp != null && !hp.IsDown)
                hp.TakeDamage(damage);
        }
    }

    private void EndAttackImmediate()
    {
        IsAttacking = false;

        if (anim != null)
        {
            anim.SetBool(boolAttackR, false);
            anim.SetBool(boolAttackL, false);
        }

        if (follow != null)
            follow.SetBlockMove(false);
    }

    // === (호환용) 애니 이벤트가 남아있어도 안전 ===
    public void DealDamage() { /* intentionally empty - damage handled by coroutine */ }

    public void AnimEvent_EndAttack()
    {
        EndAttackImmediate();
    }

    public void PlayAttackSfx()
    {
        AudioSource audio = GetComponent<AudioSource>();
        if (audio != null && audio.clip != null)
            audio.PlayOneShot(audio.clip);
    }
}
