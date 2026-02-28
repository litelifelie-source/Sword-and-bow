using System.Collections;
using UnityEngine;

public class JeanneAttackAI : MonoBehaviour
{
    public event System.Action OnAttackStarted;

    [Header("Targeting")]
    public float detectRange = 6f;
    public float attackRange = 1.2f;
    public LayerMask targetLayer; // 1차 후보 필터(성능용). 최종 판정은 TargetRule/UnitTeam.

    [Header("Auto Target Layer By Team (optional)")]
    public bool autoTargetLayerByTeam = false;

    [Tooltip("아군 유닛(병사) 레이어 이름")]
    public string allyLayerName = "Ally";

    [Tooltip("적 유닛 레이어 이름")]
    public string enemyLayerName = "Enemy";

    [Tooltip("플레이어 레이어 이름(플레이어를 Ally 레이어로 쓰지 않을 때 필요)")]
    public string playerLayerName = "Player";

    [Tooltip("내가 Enemy일 때 Ally 타겟 마스크에 Player 레이어도 포함")]
    public bool includePlayerWhenTargetingAllies = true;

    [Header("Target Rule (final filter)")]
    public TargetRule targetRule = TargetRule.EnemiesOnly;

    [Header("Attack Timing")]
    public float attackCooldown = 1.0f;
    public float hitDelay = 0.12f;
    public float attackEndDelay = 0.35f;

    [Header("Damage")]
    public int damage = 35;
    public float attackOffset = 0.6f;
    public float hitRadius = 0.4f;

    [Header("Refs")]
    public Animator anim;
    public JeanneFollow follow;
    public bool chaseTarget = true;

    [Header("Animator Params")]
    public string trigAttack = "Attack";
    public string paramIsMoving = "IsMoving";
    public string paramMoveX = "MoveX";
    public string paramMoveY = "MoveY";

    [Header("Input Feel (4-way stabilization)")]
    [Tooltip("0.15~0.25 추천. 클수록 45도 근처에서 방향이 덜 튐(축 전환이 더 어려움).")]
    [Range(0f, 0.5f)]
    public float axisHysteresis = 0.20f;

    [Tooltip("입력/스냅 로그(문제 확인용)")]
    public bool debugSnapLog = false;

    public bool IsAttacking { get; private set; }

    private float nextAttackTime;
    private Transform ownerRoot;
    private Transform currentTarget;
    private Vector2 attackDir = Vector2.down;
    private Transform defaultFollowTarget;
    private Coroutine attackCo;

    private JeanneSkillDistributor distributor;
    private UnitTeam myTeam;
    private Team _lastTeam = (Team)(-999);

    void Awake()
    {
        if (anim == null) anim = GetComponentInChildren<Animator>(true);
        if (follow == null) follow = GetComponent<JeanneFollow>();
        if (follow != null) defaultFollowTarget = follow.followTarget;

        distributor = GetComponent<JeanneSkillDistributor>() ?? GetComponentInParent<JeanneSkillDistributor>();
        myTeam = GetComponentInParent<UnitTeam>();

        ownerRoot = (myTeam != null) ? myTeam.transform : transform;

        if (autoTargetLayerByTeam && myTeam != null)
        {
            _lastTeam = myTeam.team;
            RefreshTargetLayer();
        }
    }

    void Update()
    {
        if (IsAttacking) return;

        if (autoTargetLayerByTeam && myTeam != null && myTeam.team != _lastTeam)
        {
            _lastTeam = myTeam.team;
            RefreshTargetLayer();
        }

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

    void RefreshTargetLayer()
    {
        if (myTeam == null) return;

        // NPC면 타겟 없음
        if (myTeam.team == Team.NPC)
        {
            targetLayer = 0;
            return;
        }

        // ✅ 내가 Ally면 Enemy만, 내가 Enemy면 Ally(+Player 옵션)로
        if (myTeam.team == Team.Ally)
        {
            targetLayer = MaskFromLayerName(enemyLayerName);
            return;
        }

        // myTeam.team == Team.Enemy
        int mask = MaskFromLayerName(allyLayerName);

        if (includePlayerWhenTargetingAllies)
            mask |= MaskFromLayerName(playerLayerName);

        targetLayer = mask;
    }

    int MaskFromLayerName(string layerName)
    {
        if (string.IsNullOrEmpty(layerName)) return 0;
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0) return 0;
        return 1 << layer;
    }

    void AcquireTarget()
    {
        if (currentTarget != null)
        {
            var hp = currentTarget.GetComponentInParent<Health>();
            if (hp == null || hp.IsDown) { currentTarget = null; return; }

            var keepTeam = currentTarget.GetComponentInParent<UnitTeam>();
            if (!PassRule(myTeam, keepTeam, targetRule, ownerRoot))
            {
                currentTarget = null;
                return;
            }

            float dKeep = Vector2.Distance(ownerRoot.position, currentTarget.position);
            if (dKeep <= detectRange) return;
        }

        currentTarget = null;
        float best = float.MaxValue;

        Collider2D[] hits = Physics2D.OverlapCircleAll(ownerRoot.position, detectRange, targetLayer);

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null) continue;

            Health hp = hits[i].GetComponentInParent<Health>();
            if (hp == null || hp.IsDown) continue;

            UnitTeam ut = hits[i].GetComponentInParent<UnitTeam>();
            if (!PassRule(myTeam, ut, targetRule, ownerRoot)) continue;

            Transform t = (ut != null) ? ut.transform : hp.transform;

            float d = Vector2.Distance(ownerRoot.position, t.position);
            if (d < best)
            {
                best = d;
                currentTarget = t;
            }
        }
    }

    // ✅ 4방향 유지 + 45도 근처 튐 방지(히스테리시스)
    Vector2 Snap4Stable(Vector2 d)
    {
        if (d.sqrMagnitude < 0.0001f) return attackDir;

        float ax = Mathf.Abs(d.x);
        float ay = Mathf.Abs(d.y);

        bool prevWasHorizontal = Mathf.Abs(attackDir.x) > Mathf.Abs(attackDir.y);

        Vector2 result;

        if (prevWasHorizontal)
        {
            // 수평 -> 수직 전환은 더 "확실히" 수직이 우세할 때만 허용
            if (ay > ax * (1f + axisHysteresis))
                result = d.y >= 0 ? Vector2.up : Vector2.down;
            else
                result = d.x >= 0 ? Vector2.right : Vector2.left;
        }
        else
        {
            // 수직 -> 수평 전환은 더 "확실히" 수평이 우세할 때만 허용
            if (ax > ay * (1f + axisHysteresis))
                result = d.x >= 0 ? Vector2.right : Vector2.left;
            else
                result = d.y >= 0 ? Vector2.up : Vector2.down;
        }

        if (debugSnapLog)
            Debug.Log($"[JeanneAttackAI] raw={d} ax={ax:F3} ay={ay:F3} prevH={prevWasHorizontal} -> {result}");

        return result;
    }

    public void RequestAttack(Vector2 dir) => TryStartAttack(dir);

    void TryStartAttack(Vector2 dir)
    {
        if (anim == null) return;
        if (Time.time < nextAttackTime) return;

        nextAttackTime = Time.time + attackCooldown;

        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.down;

        // ✅ 여기 핵심 교체
        attackDir = Snap4Stable(dir);
        IsAttacking = true;

        OnAttackStarted?.Invoke();

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

        // ✅ 타겟 기준 재스냅도 안정 버전 사용(여기가 은근히 튐을 만듦)
        if (currentTarget != null)
            attackDir = Snap4Stable((Vector2)(currentTarget.position - ownerRoot.position));

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
            if (col == null) continue;

            Health hp = col.GetComponentInParent<Health>();
            if (hp == null || hp.IsDown) continue;

            UnitTeam ut = col.GetComponentInParent<UnitTeam>();

            Transform otherRoot = (ut != null) ? ut.transform : hp.transform;
            if (otherRoot == ownerRoot) continue;

            if (!PassRule(myTeam, ut, targetRule, ownerRoot)) continue;

            hp.TakeDamage(damage);
            didHit = true;
        }

        if (didHit && distributor != null)
            distributor.TryProc();
    }

    void EndAttack()
    {
        IsAttacking = false;
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
        Vector3 p;

        if (Application.isPlaying)
        {
            var ut = GetComponentInParent<UnitTeam>();
            p = (ut != null) ? ut.transform.position : transform.position;
        }
        else
        {
            p = transform.position;
        }

        Gizmos.DrawWireSphere(p, detectRange);
        Gizmos.DrawWireSphere(p, attackRange);

        Vector3 c = p + (Vector3)(attackDir.normalized * attackOffset);
        Gizmos.DrawWireSphere(c, hitRadius);
    }
#endif
}
