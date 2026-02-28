using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JeanneKnightSwordProc : MonoBehaviour
{
    [Header("Spec")]
    [Tooltip("AttackAI.damage를 기반으로 피해를 만들지 여부")]
    public bool useAttackAIDamage = true;

    [Tooltip("useAttackAIDamage=false일 때 사용할 기본 데미지")]
    public int baseDamage = 30;

    [Tooltip("1타 데미지 계수(기본 0.65)")]
    public float m1 = 0.65f;
    [Tooltip("2타 데미지 계수(기본 0.75)")]
    public float m2 = 0.75f;
    [Tooltip("3타 데미지 계수(기본 0.90)")]
    public float m3 = 0.90f;

    [Header("Hit Timings")]
    public float hit1Delay = 0.00f;
    public float hit2Delay = 0.10f;
    public float hit3Delay = 0.22f;

    [Header("Targeting (final filter by UnitTeam/TargetRule)")]
    public LayerMask targetLayer;                 // 1차 후보 필터(성능용)
    public TargetRule targetRule = TargetRule.EnemiesOnly;

    [Header("Hit Range (owner-centered, FIXED)")]
    [Tooltip("시전자 기준 단일타 판정 반경")]
    public float singleHitRadius = 0.35f;

    [Header("Optional: 3rd splash (owner-centered, FIXED)")]
    public bool useSplashOn3rd = false;
    [Tooltip("시전자 기준 스플래시 판정 반경 (0이면 AttackAI.hitRadius 우선, 없으면 splashRadius 사용)")]
    public float splashRadius = 0.45f;
    [Tooltip("주변 최대 피격 수(메인 타겟 제외)")]
    public int splashMax = 2;
    [Tooltip("주변 데미지 계수(AttackAI.damage 기준)")]
    public float splashMul = 0.30f;

    [Header("Melee Gate (fix long-range bug)")]
    [Tooltip("0이면 AttackAI.attackRange 사용. 근접 스킬이면 1.2~1.8 정도가 보통")]
    public float maxMeleeDistance = 0f;

    [Header("Cast")]
    [Tooltip("스킬 시작 후 첫 판정까지 대기(모션 맞출 때 사용). 0이면 즉시 1타 딜레이로 감")]
    public float castTime = 0.0f;

    [Header("Refs")]
    public UnitTeam ownerTeam;
    public JeanneAttackAI attackAI;      // damage/targetLayer 참조
    public Animator anim;

    [Header("Anim (optional)")]
    public string animStateKnightSword = "named_잔느_기사의 검술(일반 스킬)";

    [Header("Lock scripts during cast (optional)")]
    public MonoBehaviour[] lockScripts;

    [Header("Debug")]
    public bool debugLog = false;

    public bool IsCasting { get; private set; }

    private void Awake()
    {
        if (ownerTeam == null) ownerTeam = GetComponentInParent<UnitTeam>() ?? GetComponent<UnitTeam>();

        if (attackAI == null) attackAI = GetComponent<JeanneAttackAI>() ??
                                         GetComponentInChildren<JeanneAttackAI>(true) ??
                                         GetComponentInParent<JeanneAttackAI>();

        if (anim == null) anim = GetComponentInChildren<Animator>(true);

        // targetLayer가 비어있으면 AttackAI의 타겟 마스크를 재사용
        if (targetLayer.value == 0 && attackAI != null)
            targetLayer = attackAI.targetLayer;

        // targetRule도 AttackAI랑 맞추고 싶으면(선택)
        if (attackAI != null)
            targetRule = attackAI.targetRule;
    }

    private void OnDisable()
    {
        IsCasting = false;
        SetLocks(false);
    }

    public bool StartKnightSword_FromDistributor()
    {
        if (IsCasting) return false;

        // NPC면 금지(전환 설계 보호)
        if (ownerTeam != null && ownerTeam.team == Team.NPC) return false;

        StartCoroutine(CoKnightSword());
        return true;
    }

    private IEnumerator CoKnightSword()
    {
        IsCasting = true;
        SetLocks(true);

        // 애니 재생
        if (anim != null && !string.IsNullOrEmpty(animStateKnightSword))
            anim.Play(animStateKnightSword, 0, 0f);

        if (castTime > 0f)
            yield return new WaitForSeconds(castTime);

        // 타겟 확보(시전자 기준 탐색)
        Transform target = AcquireTarget();
        if (target == null)
        {
            if (debugLog) Debug.Log("[KnightSword] target null -> cancel", this);
            SetLocks(false);
            IsCasting = false;
            yield break;
        }

        // 1타
        if (hit1Delay > 0f) yield return new WaitForSeconds(hit1Delay);
        DoSingleHit(target, m1);

        // 2타
        if (hit2Delay > 0f) yield return new WaitForSeconds(hit2Delay);
        DoSingleHit(target, m2);

        // 3타
        if (hit3Delay > 0f) yield return new WaitForSeconds(hit3Delay);
        DoSingleHit(target, m3);

        if (useSplashOn3rd && splashMax > 0)
            DoSplash(target);

        SetLocks(false);
        IsCasting = false;
    }

    // --------------------
    // Targeting / Damage
    // --------------------

    private Transform AcquireTarget()
    {
        Transform ownerRoot = (ownerTeam != null) ? ownerTeam.transform : transform;
        Vector2 center = ownerRoot.position;

        float r = (attackAI != null) ? attackAI.detectRange : 6f;
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, r, targetLayer);
        if (hits == null || hits.Length == 0) return null;

        Transform best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            var c = hits[i];
            if (c == null) continue;

            Health hp = c.GetComponentInParent<Health>();
            if (hp == null || hp.IsDown) continue;

            UnitTeam ut = c.GetComponentInParent<UnitTeam>();
            if (!PassRule(ownerTeam, ut, targetRule, ownerRoot)) continue;

            Transform root = (ut != null) ? ut.transform : hp.transform;
            float d = Vector2.Distance(center, root.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = root;
            }
        }

        if (debugLog) Debug.Log(best ? $"[KnightSword] target={best.name}" : "[KnightSword] target=null", this);
        return best;
    }

    private float GetGate()
    {
        if (maxMeleeDistance > 0f) return maxMeleeDistance;
        if (attackAI != null) return attackAI.attackRange;
        return 1.2f;
    }

    private void DoSingleHit(Transform target, float mul)
    {
        if (target == null) return;

        Transform ownerRoot = (ownerTeam != null) ? ownerTeam.transform : transform;

        // ✅ 거리 게이트: 멀면 아무것도 안 함 (원거리 맞는 버그 차단)
        float gate = GetGate();
        float dist = Vector2.Distance(ownerRoot.position, target.position);
        if (dist > gate)
        {
            if (debugLog) Debug.Log($"[KnightSword] too far dist={dist:F2} gate={gate:F2} -> skip", this);
            return;
        }

        // ✅ 시전자 기준 판정 중심 (AttackAI 느낌)
        Vector2 dir = (Vector2)target.position - (Vector2)ownerRoot.position;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.down;
        dir.Normalize();

        float offset = (attackAI != null) ? attackAI.attackOffset : 0.6f;
        Vector2 center = (Vector2)ownerRoot.position + dir * offset;

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, singleHitRadius, targetLayer);
        if (hits == null || hits.Length == 0) return;

        int dmg = Mathf.Max(0, Mathf.RoundToInt(CalcBaseDamage() * mul));

        // 유효 1명만 타격
        for (int i = 0; i < hits.Length; i++)
        {
            var c = hits[i];
            if (c == null) continue;

            Health hp = c.GetComponentInParent<Health>();
            if (hp == null || hp.IsDown) continue;

            UnitTeam ut = c.GetComponentInParent<UnitTeam>();
            if (!PassRule(ownerTeam, ut, targetRule, ownerRoot)) continue;

            hp.TakeDamage(dmg);
            if (debugLog) Debug.Log($"[KnightSword] hit {ut?.name ?? c.name} dmg={dmg}", this);
            break;
        }
    }

    private void DoSplash(Transform mainTarget)
    {
        if (mainTarget == null) return;

        Transform ownerRoot = (ownerTeam != null) ? ownerTeam.transform : transform;

        // ✅ 거리 게이트
        float gate = GetGate();
        float dist = Vector2.Distance(ownerRoot.position, mainTarget.position);
        if (dist > gate)
        {
            if (debugLog) Debug.Log($"[KnightSword] splash too far dist={dist:F2} gate={gate:F2} -> skip", this);
            return;
        }

        // ✅ 시전자 기준 스플래시 판정
        Vector2 dir = (Vector2)mainTarget.position - (Vector2)ownerRoot.position;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.down;
        dir.Normalize();

        float offset = (attackAI != null) ? attackAI.attackOffset : 0.6f;
        float radius = (attackAI != null) ? attackAI.hitRadius : splashRadius;

        Vector2 center = (Vector2)ownerRoot.position + dir * offset;

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius, targetLayer);
        if (hits == null || hits.Length == 0) return;

        int dmg = Mathf.Max(0, Mathf.RoundToInt(CalcBaseDamage() * splashMul));

        int count = 0;
        HashSet<Transform> done = new HashSet<Transform>();

        for (int i = 0; i < hits.Length && count < splashMax; i++)
        {
            var c = hits[i];
            if (c == null) continue;

            Health hp = c.GetComponentInParent<Health>();
            if (hp == null || hp.IsDown) continue;

            UnitTeam ut = c.GetComponentInParent<UnitTeam>();
            if (!PassRule(ownerTeam, ut, targetRule, ownerRoot)) continue;

            Transform root = (ut != null) ? ut.transform : hp.transform;

            if (root == mainTarget) continue;      // 메인 타겟 제외
            if (done.Contains(root)) continue;

            hp.TakeDamage(dmg);
            done.Add(root);
            count++;

            if (debugLog) Debug.Log($"[KnightSword] splash {root.name} dmg={dmg}", this);
        }
    }

    private int CalcBaseDamage()
    {
        if (useAttackAIDamage && attackAI != null)
            return attackAI.damage;
        return baseDamage;
    }

    private bool PassRule(UnitTeam owner, UnitTeam other, TargetRule rule, Transform ownerRootTf)
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

    private void SetLocks(bool on)
    {
        if (lockScripts == null) return;
        for (int i = 0; i < lockScripts.Length; i++)
        {
            if (lockScripts[i] == null) continue;
            lockScripts[i].enabled = !on;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Transform ownerRoot = (ownerTeam != null) ? ownerTeam.transform : transform;
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.35f);
        Gizmos.DrawWireSphere(ownerRoot.position, (attackAI != null ? attackAI.detectRange : 6f));
    }
#endif
}
