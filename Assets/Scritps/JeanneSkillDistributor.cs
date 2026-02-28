using System.Collections;
using UnityEngine;

public class JeanneSkillDistributor : MonoBehaviour
{
    [Header("Dice Chance (누적)")]
    [Range(0f, 1f)] public float knightSwordChance = 0.38f;   // ✅ 기사검술(주력)
    [Range(0f, 1f)] public float judgmentChance   = 0.05f;
    [Range(0f, 1f)] public float shieldChance     = 0.20f;
    [Range(0f, 1f)] public float lightWaveChance  = 0.25f;
    [Range(0f, 1f)] public float sanctuaryChance  = 0.12f;

    [Header("Cooldowns (Distributor-managed)")]
    public float knightSwordCooldown = 4f;                     // ✅
    public float judgmentCooldown   = 120f;
    public float shieldCooldown     = 10f;
    public float lightWaveCooldown  = 12f;
    public float sanctuaryCooldown  = 20f;

    private float nextKnightSwordTime;                         // ✅
    private float nextJudgmentTime;
    private float nextShieldTime;
    private float nextLightWaveTime;
    private float nextSanctuaryTime;

    [Header("Skill Executors")]
    public JeanneKnightSwordProc knightSwordProc;              // ✅
    public JeanneJudgmentProc judgmentProc;
    public JeanneGuardShieldProc shieldProc;
    public JeanneLightWaveProc lightWaveProc;
    public JeanneSanctuaryProc sanctuaryProc;

    [Header("Options")]
    public bool debugLogRoll = true;

    [Header("Attack Tracking (No AttackAI edit)")]
    [Tooltip("공격 시작을 감지할 JeanneAttackAI (비워도 자동으로 찾음)")]
    public JeanneAttackAI attackAI;
    [Tooltip("공격 시작 후 타격 타이밍. 비워두면 attackAI.hitDelay를 사용")]
    public float hitDelayOverride = -1f;

    // 내부 상태
    private bool prevIsAttacking;
    private Coroutine rollCo;
    private float lastAttackStartTime = -999f;
    private const float startDebounce = 0.03f; // 프레임 튐 방지용

    private void Awake()
    {
        if (knightSwordProc == null) // ✅ 기사검술 자동 탐색
            knightSwordProc = GetComponent<JeanneKnightSwordProc>() ?? GetComponentInParent<JeanneKnightSwordProc>();

        if (judgmentProc == null)
            judgmentProc = GetComponent<JeanneJudgmentProc>() ?? GetComponentInParent<JeanneJudgmentProc>();

        if (shieldProc == null)
            shieldProc = GetComponent<JeanneGuardShieldProc>() ?? GetComponentInParent<JeanneGuardShieldProc>();

        if (lightWaveProc == null)
            lightWaveProc = GetComponent<JeanneLightWaveProc>() ?? GetComponentInParent<JeanneLightWaveProc>();

        if (sanctuaryProc == null)
            sanctuaryProc = GetComponent<JeanneSanctuaryProc>() ?? GetComponentInParent<JeanneSanctuaryProc>();

        // ✅ 어택AI 자동 탐색(같은 오브젝트/자식/부모)
        if (attackAI == null)
            attackAI = GetComponent<JeanneAttackAI>() ??
                       GetComponentInChildren<JeanneAttackAI>(true) ??
                       GetComponentInParent<JeanneAttackAI>();

        if (debugLogRoll)
        {
            Debug.Log($"✅ Distributor Awake on {gameObject.name} | " +
                      $"attackAI={(attackAI ? attackAI.name : "NULL")} | " +
                      $"knightSwordProc={(knightSwordProc ? "OK" : "NULL")} | " +
                      $"judgmentProc={(judgmentProc ? "OK" : "NULL")} | " +
                      $"shieldProc={(shieldProc ? "OK" : "NULL")} | " +
                      $"lightWaveProc={(lightWaveProc ? "OK" : "NULL")} | " +
                      $"sanctuaryProc={(sanctuaryProc ? "OK" : "NULL")}", this);
        }
    }

    private void OnEnable()
    {
        prevIsAttacking = attackAI != null && attackAI.IsAttacking;
    }

    private void Update()
    {
        if (attackAI == null) return;

        bool now = attackAI.IsAttacking;

        // ✅ 공격 시작 Edge 감지: false -> true
        if (now && !prevIsAttacking)
        {
            // 디바운스(아주 짧은 시간에 튀는 경우 방지)
            if (Time.time - lastAttackStartTime > startDebounce)
            {
                lastAttackStartTime = Time.time;

                float d = (hitDelayOverride >= 0f) ? hitDelayOverride : attackAI.hitDelay;

                if (debugLogRoll)
                    Debug.Log($"🗡 Attack START 감지 (IsAttacking true) → {d:F2}s 뒤 다이스", this);

                if (rollCo != null) StopCoroutine(rollCo);
                rollCo = StartCoroutine(CoRollAfterDelay(d));
            }
        }

        prevIsAttacking = now;
    }

    private IEnumerator CoRollAfterDelay(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        if (debugLogRoll)
            Debug.Log("🎯 타격 타이밍 도달 → TryProc()", this);

        TryProc();
    }

    public bool TryProc()
    {
        if (knightSwordProc == null && judgmentProc == null && shieldProc == null && lightWaveProc == null && sanctuaryProc == null)
        {
            if (debugLogRoll)
                Debug.LogWarning("⚠ JeanneSkillDistributor: 실행기 연결 안됨", this);
            return false;
        }

        // ✅ 캐스팅 중 체크(주력기가 최우선이므로 가장 먼저)
        if (knightSwordProc != null && knightSwordProc.IsCasting)
        {
            if (debugLogRoll) Debug.Log("🔒 기사검술 캐스팅 중 → 다이스 무효", this);
            return false;
        }

        if (judgmentProc != null && judgmentProc.IsCasting)
        {
            if (debugLogRoll) Debug.Log("🔒 심판 캐스팅 중 → 다이스 무효", this);
            return false;
        }

        if (shieldProc != null && shieldProc.IsCasting)
        {
            if (debugLogRoll) Debug.Log("🔒 방패 캐스팅 중 → 다이스 무효", this);
            return false;
        }

        if (lightWaveProc != null && lightWaveProc.IsCasting)
        {
            if (debugLogRoll) Debug.Log("🔒 빛의 파동 캐스팅 중 → 다이스 무효", this);
            return false;
        }

        if (sanctuaryProc != null && sanctuaryProc.IsCasting)
        {
            if (debugLogRoll) Debug.Log("🔒 성역 캐스팅 중 → 다이스 무효", this);
            return false;
        }

        float roll = Random.value;

        // 누적 경계값 계산(가독성 + 실수 방지)
        float tKnight = knightSwordChance;
        float tJudg   = tKnight + judgmentChance;
        float tShield = tJudg   + shieldChance;
        float tWave   = tShield + lightWaveChance;
        float tSanct  = tWave   + sanctuaryChance;

        if (debugLogRoll)
        {
            Debug.Log(
                $"🎲 Jeanne Dice Roll: {roll:F3}\n" +
                $"   ├─ KnightSword < {tKnight:F2}\n" +
                $"   ├─ Judgment    < {tJudg:F2}\n" +
                $"   ├─ Shield      < {tShield:F2}\n" +
                $"   ├─ LightWave   < {tWave:F2}\n" +
                $"   └─ Sanctuary   < {tSanct:F2}",
                this
            );
        }

        // =========================================
        // ✅ 주력기 “기사의 검술” (다이스 잡아먹기)
        // roll이 이 구간이면: 쿨이면 그냥 실패(return false) → 다른 스킬로 안 넘어감
        // =========================================
        if (roll < tKnight)
        {
            if (Time.time < nextKnightSwordTime)
            {
                if (debugLogRoll)
                    Debug.Log($"⏳ 기사검술 쿨타임 남음: {(nextKnightSwordTime - Time.time):F2}s", this);
                return false; // ✅ 잡아먹기 유지
            }

            bool ok = knightSwordProc != null && knightSwordProc.StartKnightSword_FromDistributor();

            if (ok)
            {
                nextKnightSwordTime = Time.time + knightSwordCooldown;
                if (debugLogRoll) Debug.Log("🗡 기사검술 발동 성공!", this);
            }
            else if (debugLogRoll) Debug.Log("❌ 기사검술 발동 실패 (StartKnightSword false 반환)", this);

            return ok;
        }

        // -------- 심판 --------
        if (roll < tJudg)
        {
            if (Time.time < nextJudgmentTime)
            {
                if (debugLogRoll)
                    Debug.Log($"⏳ 심판 쿨타임 남음: {(nextJudgmentTime - Time.time):F2}s", this);
                return false;
            }

            bool ok = judgmentProc != null && judgmentProc.StartJudgment_FromDistributor();

            if (ok)
            {
                nextJudgmentTime = Time.time + judgmentCooldown;
                if (debugLogRoll) Debug.Log("🔥 심판 발동 성공!", this);
            }
            else if (debugLogRoll) Debug.Log("❌ 심판 발동 실패 (StartJudgment false 반환)", this);

            return ok;
        }

        // -------- 방패 --------
        if (roll < tShield)
        {
            if (Time.time < nextShieldTime)
            {
                if (debugLogRoll)
                    Debug.Log($"⏳ 방패 쿨타임 남음: {(nextShieldTime - Time.time):F2}s", this);
                return false;
            }

            bool ok = shieldProc != null && shieldProc.StartShield_FromDistributor();

            if (ok)
            {
                nextShieldTime = Time.time + shieldCooldown;
                if (debugLogRoll) Debug.Log("🛡 방패 발동 성공!", this);
            }
            else if (debugLogRoll) Debug.Log("❌ 방패 발동 실패 (StartShield false 반환)", this);

            return ok;
        }

        // -------- 빛의 파동 --------
        if (roll < tWave)
        {
            if (Time.time < nextLightWaveTime)
            {
                if (debugLogRoll)
                    Debug.Log($"⏳ 빛의 파동 쿨타임 남음: {(nextLightWaveTime - Time.time):F2}s", this);
                return false;
            }

            bool ok = lightWaveProc != null && lightWaveProc.StartLightWave_FromDistributor();

            if (ok)
            {
                nextLightWaveTime = Time.time + lightWaveCooldown;
                if (debugLogRoll) Debug.Log("🌊 빛의 파동 발동 성공!", this);
            }
            else if (debugLogRoll) Debug.Log("❌ 빛의 파동 발동 실패 (StartLightWave false 반환)", this);

            return ok;
        }

        // -------- 성역 --------
        if (roll < tSanct)
        {
            if (Time.time < nextSanctuaryTime)
            {
                if (debugLogRoll)
                    Debug.Log($"⏳ 성역 쿨타임 남음: {(nextSanctuaryTime - Time.time):F2}s", this);
                return false;
            }

            bool ok = sanctuaryProc != null && sanctuaryProc.StartSanctuary_FromDistributor();

            if (ok)
            {
                nextSanctuaryTime = Time.time + sanctuaryCooldown;
                if (debugLogRoll) Debug.Log("✨ 성역 발동 성공!", this);
            }
            else if (debugLogRoll) Debug.Log("❌ 성역 발동 실패 (StartSanctuary false 반환)", this);

            return ok;
        }

        if (debugLogRoll)
            Debug.Log("❌ 아무 스킬도 발동되지 않음", this);

        return false;
    }

    // 쿨다운 Remaining
    public float KnightSwordCooldownRemaining => Mathf.Max(0f, nextKnightSwordTime - Time.time);
    public float JudgmentCooldownRemaining    => Mathf.Max(0f, nextJudgmentTime - Time.time);
    public float ShieldCooldownRemaining      => Mathf.Max(0f, nextShieldTime - Time.time);
    public float LightWaveCooldownRemaining   => Mathf.Max(0f, nextLightWaveTime - Time.time);
    public float SanctuaryCooldownRemaining   => Mathf.Max(0f, nextSanctuaryTime - Time.time);
}
