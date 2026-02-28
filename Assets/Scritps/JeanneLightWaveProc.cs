using System.Collections;
using UnityEngine;

public class JeanneLightWaveProc : MonoBehaviour
{
    [Header("Spec")]
    public float radius = 3.0f;

    [Tooltip("AttackAI.damage를 기반으로 피해를 만들지 여부")]
    public bool useAttackAIDamage = true;

    [Tooltip("useAttackAIDamage=false일 때 사용할 기본 데미지")]
    public int baseDamage = 30;

    [Tooltip("최종 데미지 계수 (추천 0.8~1.0, 기본 0.9)")]
    public float damageMultiplier = 0.9f;

    [Header("Knockback (Physics Impulse)")]
    [Tooltip("Rigidbody2D.AddForce(dir * force, Impulse) 의 force 값")]
    public float knockbackForce = 2.6f;

    [Tooltip("너무 큰/보스급은 넉백을 줄이고 싶을 때(선택). 1이면 보정 없음.")]
    [Range(0f, 1f)] public float heavyKnockbackMultiplier = 0.5f;

    [Tooltip("질량이 무거운 대상/보스 판정용 최소 질량(이 이상이면 heavyMultiplier 적용)")]
    public float heavyMassThreshold = 3.0f;

    [Header("Cast")]
    public float castTime = 0.05f;

    [Header("Targeting (final filter by UnitTeam/TargetRule)")]
    public LayerMask targetLayer; // 1차 후보 필터(성능용)
    public TargetRule targetRule = TargetRule.EnemiesOnly;

    [Header("Refs")]
    public UnitTeam ownerTeam;
    public JeanneAttackAI attackAI; // damage 참조용(선택)
    public Rigidbody2D rb;          // 필요하면(현재는 물리락 안 함)
    public Animator anim;

    [Header("Anim (optional)")]
    public string animStateLightWave = "named_잔느_빛의 파동(고급 스킬)";

    [Header("VFX (optional)")]
    public GameObject lightWaveVfxPrefab;
    public Transform vfxAnchor;
    public Vector3 vfxOffset = new Vector3(0f, 0.1f, 0f);
    public float vfxLifeTime = 1.2f;

    [Header("SFX (optional)")]
    public AudioSource sfxSource;
    public AudioClip sfxClip;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    public bool IsCasting { get; private set; }

    private void Awake()
    {
        if (ownerTeam == null) ownerTeam = GetComponentInParent<UnitTeam>() ?? GetComponent<UnitTeam>();
        if (attackAI == null) attackAI = GetComponent<JeanneAttackAI>() ??
                                         GetComponentInChildren<JeanneAttackAI>(true) ??
                                         GetComponentInParent<JeanneAttackAI>();

        if (anim == null) anim = GetComponentInChildren<Animator>(true);
        if (rb == null) rb = GetComponentInParent<Rigidbody2D>() ?? GetComponent<Rigidbody2D>();

        // targetLayer가 비어있으면 AttackAI의 타겟 마스크를 최대한 재사용
        if (targetLayer.value == 0 && attackAI != null)
            targetLayer = attackAI.targetLayer;

        // VFX 앵커 자동 생성
        if (vfxAnchor == null)
        {
            var a = new GameObject("LightWaveVFX_Anchor");
            a.transform.SetParent(transform, false);
            a.transform.localPosition = vfxOffset;
            vfxAnchor = a.transform;
        }

        // SFX 소스 자동 탐색
        if (sfxSource == null)
        {
            sfxSource = GetComponentInChildren<AudioSource>(true);
            if (sfxSource == null) sfxSource = GetComponent<AudioSource>();
        }
    }

    private void OnDisable()
    {
        // 캐스팅 중 비활성화 안전 복구
        IsCasting = false;
    }

    public bool StartLightWave_FromDistributor()
    {
        if (IsCasting) return false;
        StartCoroutine(CoLightWave());
        return true;
    }

    private IEnumerator CoLightWave()
    {
        IsCasting = true;

        // (선택) 애니 재생
        if (anim != null && !string.IsNullOrEmpty(animStateLightWave))
            anim.Play(animStateLightWave, 0, 0f);

        // (선택) VFX / SFX
        SpawnVfxOnce();
        PlaySfxOnce();

        if (castTime > 0f)
            yield return new WaitForSeconds(castTime);

        ExecuteWave();

        IsCasting = false;
    }

    private void ExecuteWave()
    {
        Transform ownerRoot = (ownerTeam != null) ? ownerTeam.transform : transform;
        Vector2 center = ownerRoot.position;

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius, targetLayer);
        if (hits == null || hits.Length == 0) return;

        int finalDamage = CalcDamage();

        for (int i = 0; i < hits.Length; i++)
        {
            var c = hits[i];
            if (c == null) continue;

            Health hp = c.GetComponentInParent<Health>();
            if (hp == null || hp.IsDown) continue;

            UnitTeam ut = c.GetComponentInParent<UnitTeam>();
            if (!PassRule(ownerTeam, ut, targetRule, ownerRoot)) continue;

            // 1) 데미지
            if (finalDamage > 0)
                hp.TakeDamage(finalDamage);

            // 2) 물리 넉백
            Rigidbody2D trb = c.attachedRigidbody != null ? c.attachedRigidbody : c.GetComponentInParent<Rigidbody2D>();
            if (trb == null) continue;

            Vector2 dir = ((Vector2)trb.worldCenterOfMass - center);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            dir.Normalize();

            float force = knockbackForce;

            // 무거운 대상은 넉백 약화(선택)
            if (heavyKnockbackMultiplier < 1f && trb.mass >= heavyMassThreshold)
                force *= heavyKnockbackMultiplier;

            trb.AddForce(dir * force, ForceMode2D.Impulse);
        }
    }

    private int CalcDamage()
    {
        int raw = baseDamage;

        if (useAttackAIDamage && attackAI != null)
            raw = attackAI.damage;

        float v = raw * damageMultiplier;
        return Mathf.Max(0, Mathf.RoundToInt(v));
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

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.35f);
        Transform ownerRoot = (ownerTeam != null) ? ownerTeam.transform : transform;
        Gizmos.DrawWireSphere(ownerRoot.position, radius);
    }
#endif

    private void SpawnVfxOnce()
    {
        if (lightWaveVfxPrefab == null) return;

        Transform t = (vfxAnchor != null) ? vfxAnchor : transform;
        var go = Instantiate(lightWaveVfxPrefab, t.position, Quaternion.identity);
        Destroy(go, Mathf.Max(0.1f, vfxLifeTime));
    }

    private void PlaySfxOnce()
    {
        if (sfxSource == null || sfxClip == null) return;
        sfxSource.PlayOneShot(sfxClip, sfxVolume);
    }
}
