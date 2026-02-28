using System.Collections;
using UnityEngine;

public class JeanneSanctuaryProc : MonoBehaviour
{
    [Header("Domain Prefab")]
    public GameObject sanctuaryPrefab;         // JeanneSanctuaryDomain 붙은 프리팹
    public Transform spawnPoint;               // 비우면 본인 위치
    public bool followCaster = false;          // true면 시전자에 붙어서 따라다님

    [Header("Domain Spec")]
    public float domainDuration = 9.5f;
    public float radius = 6.0f;
    public float allyDamageReduction = 0.25f;
    public float allyRegenPercentPerSec = 0.0125f;
    public float enemyMoveSlow = 0.35f;

    [Header("Cast")]
    public float castTime = 0.25f;

    [Header("Invincible")]
    public Health health;
    public bool invincibleWhileCasting = true;

    [Header("Anim")]
    public Animator anim;
    public string animStateSanctuary = "named_잔느_성역전개";

    [Header("Physics Lock")]
    public Rigidbody2D rb;
    public bool freezeAllDuringCast = true;
    public bool zeroVelocityOnLock = true;

    [Header("Optional Script Lock")]
    public MonoBehaviour[] scriptsToLock;

    [Header("SFX (optional)")]
    public AudioSource sfxSource;
    public AudioClip castSfx;
    [Range(0f, 1f)] public float castSfxVolume = 1f;

    public bool IsCasting { get; private set; }

    private RigidbodyConstraints2D prevConstraints;
    private bool endRequested;
    private GameObject spawnedDomain;

    private UnitTeam unitTeam; // ✅ 시전자 팀 주입용

    private void Awake()
    {
        if (anim == null) anim = GetComponentInChildren<Animator>(true);
        if (health == null) health = GetComponentInChildren<Health>(true);
        if (rb == null) rb = GetComponentInParent<Rigidbody2D>() ?? GetComponent<Rigidbody2D>();
        if (spawnPoint == null) spawnPoint = transform;
        if (sfxSource == null) sfxSource = GetComponentInChildren<AudioSource>(true);

        unitTeam = GetComponentInParent<UnitTeam>(); // ✅ 시전자 팀
    }

    private void OnDisable()
    {
        if (IsCasting)
            EndCast_Safe();

        // 도메인 정리 정책은 프로젝트 취향:
        // (1) 캐스팅 취소 시 도메인도 같이 없애고 싶으면 아래 주석 해제
        // if (spawnedDomain != null) Destroy(spawnedDomain);
    }

    public bool StartSanctuary_FromDistributor()
    {
        if (IsCasting) return false;
        if (sanctuaryPrefab == null) return false;

        StartCoroutine(CoSanctuary());
        return true;
    }

    private IEnumerator CoSanctuary()
    {
        BeginCast();

        if (castTime > 0f)
            yield return new WaitForSeconds(castTime);

        SpawnDomain();

        // ✅ 애니 이벤트가 없어도 캐스팅이 영구 고정되지 않게 안전 종료
        yield return new WaitForSeconds(0.8f);

        if (IsCasting && !endRequested)
            EndCast_Safe();
    }

    private void BeginCast()
    {
        IsCasting = true;
        endRequested = false;

        SetInvincibleSafe(true);

        if (freezeAllDuringCast) LockPhysics();
        SetOptionalScriptLock(true);

        if (anim != null && !string.IsNullOrEmpty(animStateSanctuary))
            anim.Play(animStateSanctuary, 0, 0f);

        if (sfxSource != null && castSfx != null)
            sfxSource.PlayOneShot(castSfx, castSfxVolume);
    }

    private void SpawnDomain()
    {
        if (sanctuaryPrefab == null) return;

        Vector3 pos = (spawnPoint != null) ? spawnPoint.position : transform.position;

        spawnedDomain = Instantiate(sanctuaryPrefab, pos, Quaternion.identity);

        if (followCaster)
            spawnedDomain.transform.SetParent(transform, true);

        // 스펙 주입 + ✅ casterTeam 주입(핵심)
        var dom = spawnedDomain.GetComponent<JeanneSanctuaryDomain>();
        if (dom != null)
        {
            dom.duration = domainDuration;
            dom.radius = radius;
            dom.allyDamageReduction = allyDamageReduction;
            dom.allyRegenPercentPerSec = allyRegenPercentPerSec;
            dom.enemyMoveSlow = enemyMoveSlow;

            // ✅ "아군/적군"의 기준은 도메인이 아니라 "시전자 팀"
            dom.casterTeam = (unitTeam != null) ? unitTeam.team : Team.Ally;
        }
        // 도메인 컴포넌트가 없으면 조용히 실패하지 말고 바로 알게 하는 게 좋음(선택)
        // else Debug.LogWarning("[SanctuaryProc] sanctuaryPrefab에 JeanneSanctuaryDomain이 없습니다.");
    }

    // ✅ 애니메이션 마지막 프레임 이벤트에서 호출(권장)
    public void AnimEvent_SanctuaryFinish()
    {
        if (!IsCasting || endRequested) return;
        endRequested = true;
        EndCast_Safe();
    }

    private void EndCast_Safe()
    {
        if (!IsCasting) return;

        SetInvincibleSafe(false);

        if (freezeAllDuringCast) UnlockPhysics();
        SetOptionalScriptLock(false);

        IsCasting = false;
        endRequested = false;
    }

    private void SetInvincibleSafe(bool v)
    {
        if (!invincibleWhileCasting) return;
        if (health == null) return;
        health.SetInvincible(v);
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
    }

    private void SetOptionalScriptLock(bool v)
    {
        if (scriptsToLock == null || scriptsToLock.Length == 0) return;
        for (int i = 0; i < scriptsToLock.Length; i++)
        {
            if (scriptsToLock[i] != null)
                scriptsToLock[i].enabled = !v;
        }
    }
}
