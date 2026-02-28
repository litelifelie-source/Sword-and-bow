using UnityEngine;
using System.Collections;               // ✅ 추가
using System.Collections.Generic;

public class JeanneJudgmentProc : MonoBehaviour
{
    [Header("Anim")]
    public Animator anim;
    public string animStatePrayer = "Ultimate_Prayer";

    [Header("Lock")]
    public MonoBehaviour[] scriptsToLock;
    public Rigidbody2D rb;

    [Header("Physics Lock")]
    public float castingDamping = 50f;
    public bool forceStopEachFixed = true;

    [Header("Invincible")]
    public Health health;
    public bool invincibleWhileCasting = true;

    [Header("Skill")]
    public JeanneJudgmentBladeSkill skill;

    [Header("Enter Delay")]             // ✅ 추가
    public float enterDelay = 0.5f;     // ✅ 추가: 0.5초 뒤에 Prayer 애니+스킬 시작

    [Header("Audio")]
    public AudioSource voiceSource;
    public AudioSource musicSource;
    public AudioClip judgmentVoice;
    public AudioClip judgmentTheme;

    public bool IsCasting { get; private set; }

    private RigidbodyConstraints2D prevConstraints;
    private float prevDamping;

    private Dictionary<MonoBehaviour, bool> _prevEnabled = new Dictionary<MonoBehaviour, bool>();

    private Coroutine _enterRoutine;     // ✅ 추가: 중복 방지

    private void Awake()
    {
        if (anim == null) anim = GetComponentInChildren<Animator>(true);
        if (rb == null) rb = GetComponent<Rigidbody2D>();

        if (health == null) health = GetComponentInChildren<Health>(true);
        if (health == null) health = GetComponentInParent<Health>();

        if (skill == null) skill = GetComponent<JeanneJudgmentBladeSkill>() ?? GetComponentInParent<JeanneJudgmentBladeSkill>();
    }

    private void FixedUpdate()
    {
        if (!IsCasting || !forceStopEachFixed || rb == null) return;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    public bool StartJudgment_FromDistributor()
    {
        if (IsCasting) return false;

        BeginPrayer(); // ✅ 잠금/무적/물리락은 즉시

        // ✅ Prayer 애니 + 스킬 시작은 enterDelay 뒤에
        if (_enterRoutine != null) StopCoroutine(_enterRoutine);
        _enterRoutine = StartCoroutine(EnterPrayerAndStartSkill_AfterDelay());

        return true;
    }

    private IEnumerator EnterPrayerAndStartSkill_AfterDelay() // ✅ 추가
    {
        if (enterDelay > 0f)
            yield return new WaitForSeconds(enterDelay);

        // (안전) 지연 중에 캐스팅이 취소됐다면 중단
        if (!IsCasting)
            yield break;

        if (anim != null && !string.IsNullOrEmpty(animStatePrayer))
            anim.Play(animStatePrayer, 0, 0f);

        if (skill != null) skill.StartSkill();
        else Debug.LogError("JeanneJudgmentBladeSkill 없음!", this);

        _enterRoutine = null;
    }

    public void BeginPrayer()
    {
        IsCasting = true;

        if (invincibleWhileCasting && health != null)
            health.SetInvincible(true);

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            prevDamping = rb.linearDamping;
            prevConstraints = rb.constraints;

            rb.linearDamping = castingDamping;
            rb.constraints = RigidbodyConstraints2D.FreezePositionX |
                             RigidbodyConstraints2D.FreezePositionY |
                             RigidbodyConstraints2D.FreezeRotation;
        }

        SetLock(true);

        // ✅ 여기서 anim.Play/skill.StartSkill을 바로 하지 않음(지연 코루틴에서 함)

        if (voiceSource != null && judgmentVoice != null)
            voiceSource.PlayOneShot(judgmentVoice);

        if (musicSource != null && judgmentTheme != null)
        {
            if (!(musicSource.isPlaying && musicSource.clip == judgmentTheme))
            {
                musicSource.clip = judgmentTheme;
                musicSource.loop = false;
                musicSource.Play();
            }
        }
    }

    public void EndPrayer()
    {
        if (!IsCasting) return;

        // ✅ 지연 코루틴이 남아있으면 취소
        if (_enterRoutine != null)
        {
            StopCoroutine(_enterRoutine);
            _enterRoutine = null;
        }

        if (anim != null)
            anim.Play("named_잔느_Idle", 0, 0f);

        if (invincibleWhileCasting && health != null)
            health.SetInvincible(false);

        if (rb != null)
        {
            rb.linearDamping = prevDamping;
            rb.constraints = prevConstraints;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        SetLock(false);
        IsCasting = false;
    }

    private void SetLock(bool v)
    {
        if (scriptsToLock == null) return;

        if (v)
        {
            _prevEnabled.Clear();
            for (int i = 0; i < scriptsToLock.Length; i++)
            {
                var s = scriptsToLock[i];
                if (s == null) continue;

                _prevEnabled[s] = s.enabled;
                s.enabled = false;
            }
        }
        else
        {
            for (int i = 0; i < scriptsToLock.Length; i++)
            {
                var s = scriptsToLock[i];
                if (s == null) continue;

                if (_prevEnabled.TryGetValue(s, out bool wasEnabled))
                    s.enabled = wasEnabled;
            }
            _prevEnabled.Clear();
        }
    }
}
