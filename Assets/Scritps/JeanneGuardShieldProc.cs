using System.Collections;
using UnityEngine;

public class JeanneGuardShieldProc : MonoBehaviour
{
    [Header("Shield Config")]
    public int shieldAmount = 40;
    public float shieldDuration = 6f;

    [Header("Targeting")]
    public bool applyToSelf = true;
    public bool applyToNearbyAllies = true;
    public float radius = 6f;

    [Header("Cast")]
    public float castTime = 0.25f;

    [Header("Invincible")]
    public Health health;
    public bool invincibleWhileCasting = true;

    [Header("Team")]
    public UnitTeam ownerTeam;

    [Header("Anim")]
    public Animator anim;
    public string animStateShield = "named_잔느_수호의 방패(심화 스킬)";

    [Header("Physics Lock")]
    public Rigidbody2D rb;
    public bool freezeAllDuringCast = true;
    public bool zeroVelocityOnLock = true;

    [Header("Optional Script Lock (leave empty if you don't want)")]
    public MonoBehaviour[] scriptsToLock;

    // =========================
    // ✅ Anim Events: VFX / SFX (ONLY TWO EVENTS)
    // =========================
    [Header("Anim Event - Shield VFX")]
    [Tooltip("애니메이션으로 만든 방패 이펙트 프리팹")]
    public GameObject shieldVfxPrefab;

    [Tooltip("이펙트가 붙을 앵커(잔느 위). 비우면 자동 생성")]
    public Transform shieldVfxAnchor;

    [Tooltip("앵커 자동 생성 시 로컬 오프셋(잔느 위로)")]
    public Vector3 shieldVfxOffset = new Vector3(0f, 0.45f, 0f);

    [Header("Anim Event - Shield SFX")]
    [Tooltip("사운드 재생용 AudioSource (비우면 자동 탐색)")]
    public AudioSource sfxSource;

    [Tooltip("방패 사운드 클립(1회 재생)")]
    public AudioClip shieldSfxClip;

    [Range(0f, 1f)] public float shieldSfxVolume = 1f;

    // runtime
    private GameObject shieldVfxInstance;

    public bool IsCasting { get; private set; }

    private RigidbodyConstraints2D prevConstraints;
    private bool endRequested;

    private void Awake()
    {
        if (anim == null) anim = GetComponentInChildren<Animator>(true);
        if (health == null) health = GetComponentInChildren<Health>(true);
        if (ownerTeam == null) ownerTeam = GetComponentInParent<UnitTeam>() ?? GetComponent<UnitTeam>();
        if (rb == null) rb = GetComponentInParent<Rigidbody2D>() ?? GetComponent<Rigidbody2D>();

        // ✅ 방패 이펙트 앵커 자동 생성(잔느 위)
        if (shieldVfxAnchor == null)
        {
            var a = new GameObject("ShieldVFX_Anchor");
            a.transform.SetParent(transform, false);
            a.transform.localPosition = shieldVfxOffset;
            shieldVfxAnchor = a.transform;
        }

        // ✅ SFX 소스 자동 탐색
        if (sfxSource == null)
        {
            sfxSource = GetComponentInChildren<AudioSource>(true);
            if (sfxSource == null) sfxSource = GetComponent<AudioSource>();
        }
    }

    private void OnDisable()
    {
        // 캐스팅 중 비활성화되면 고정이 남지 않도록 안전 복구
        if (IsCasting)
        {
            UnlockPhysics();
            SetInvincibleSafe(false);
            SetOptionalScriptLock(false);
            IsCasting = false;
            endRequested = false;
        }

        // ✅ 이펙트 남는 거 방지
        ClearShieldVfxInstance();
    }

    public bool StartShield_FromDistributor()
    {
        if (IsCasting) return false;
        StartCoroutine(CoShield());
        return true;
    }

    private IEnumerator CoShield()
    {
        BeginCast();

        if (castTime > 0f)
            yield return new WaitForSeconds(castTime);

        ApplyShield();

        // ✅ 해제는 애니 이벤트(AnimEvent_ShieldFinish)에서만
    }

    private void BeginCast()
    {
        IsCasting = true;
        endRequested = false;

        SetInvincibleSafe(true);

        if (freezeAllDuringCast) LockPhysics();
        SetOptionalScriptLock(true);

        if (anim != null && !string.IsNullOrEmpty(animStateShield))
            anim.Play(animStateShield, 0, 0f);
    }

    // ✅ (기존) 애니메이션 마지막 프레임 이벤트에서 호출
    public void AnimEvent_ShieldFinish()
    {
        if (!IsCasting || endRequested) return;
        endRequested = true;

        EndCast();
    }

    private void EndCast()
    {
        if (!IsCasting) return;

        SetInvincibleSafe(false);

        if (freezeAllDuringCast) UnlockPhysics();
        SetOptionalScriptLock(false);

        // ✅ 캐스팅 끝나면 방패 이펙트 제거
        ClearShieldVfxInstance();

        IsCasting = false;
    }

    private void LockPhysics()
    {
        if (rb == null) return;

        prevConstraints = rb.constraints;

        if (zeroVelocityOnLock)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        rb.constraints = RigidbodyConstraints2D.FreezeAll;
    }

    private void UnlockPhysics()
    {
        if (rb == null) return;

        rb.constraints = prevConstraints;

        if (zeroVelocityOnLock)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    private void SetInvincibleSafe(bool v)
    {
        if (invincibleWhileCasting && health != null)
            health.SetInvincible(v);
    }

    private void SetOptionalScriptLock(bool lockOn)
    {
        if (scriptsToLock == null || scriptsToLock.Length == 0) return;

        for (int i = 0; i < scriptsToLock.Length; i++)
        {
            var s = scriptsToLock[i];
            if (s == null) continue;
            s.enabled = !lockOn;
        }
    }

    private void ApplyShield()
    {
        if (applyToSelf)
        {
            var selfHp = health ?? GetComponentInChildren<Health>(true);
            if (selfHp != null && !selfHp.IsDown)
                selfHp.GrantShield(shieldAmount, shieldDuration);
        }

        if (!applyToNearbyAllies) return;

        UnitTeam myTeam = ownerTeam;
        if (myTeam == null) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);
        foreach (var h in hits)
        {
            if (h == null) continue;

            Health hp = h.GetComponentInParent<Health>();
            if (hp == null || hp.IsDown) continue;

            UnitTeam t = h.GetComponentInParent<UnitTeam>();
            if (t == null) continue;
            if (t.team != myTeam.team) continue;

            hp.GrantShield(shieldAmount, shieldDuration);
        }
    }

    // =========================================================
    // ✅ Anim Events (YOU ADD ONLY THESE TWO EVENTS IN CLIP)
    // =========================================================

    // 1) 방패 이펙트 생성 이벤트
    public void AnimEvent_ShieldVFX()
    {
        if (shieldVfxPrefab == null) return;

        // 중복 방지
        ClearShieldVfxInstance();

        Transform anchor = shieldVfxAnchor != null ? shieldVfxAnchor : transform;
        shieldVfxInstance = Instantiate(shieldVfxPrefab, anchor.position, Quaternion.identity, anchor);
    }

    // 2) 방패 사운드 1회 재생 이벤트
    public void AnimEvent_ShieldSFX()
    {
        if (shieldSfxClip == null) return;

        if (sfxSource != null)
            sfxSource.PlayOneShot(shieldSfxClip, shieldSfxVolume);
        else
            AudioSource.PlayClipAtPoint(shieldSfxClip, transform.position, shieldSfxVolume);
    }

    private void ClearShieldVfxInstance()
    {
        if (shieldVfxInstance != null)
        {
            Destroy(shieldVfxInstance);
            shieldVfxInstance = null;
        }
    }
}
