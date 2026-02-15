using UnityEngine;

public class JeanneJudgmentProc : MonoBehaviour
{
    [Header("Proc")]
    [Range(0f, 1f)] public float procChance = 0.05f;
    public float cooldown = 10f;
    private float nextProcTime;

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

    private RigidbodyConstraints2D prevConstraints;
    private float prevDamping;
    private JeanneJudgmentBladeSkill skill;   // 🔥 추가

    [Header("Audio")]
    public AudioSource voiceSource;
    public AudioSource musicSource;

    public AudioClip judgmentVoice;
    public AudioClip judgmentTheme;

    public bool IsCasting { get; private set; }

    private void Awake()
    {
        if (anim == null) anim = GetComponentInChildren<Animator>(true);
        if (rb == null) rb = GetComponent<Rigidbody2D>();

        if (health == null) health = GetComponentInChildren<Health>(true);
        if (health == null) health = GetComponentInParent<Health>();

        if (rb != null)
        {
            prevDamping = rb.linearDamping;
            prevConstraints = rb.constraints;
        }

        // 🔥 여기서 Skill 연결
        skill = GetComponent<JeanneJudgmentBladeSkill>();
        if (skill == null)
            skill = GetComponentInParent<JeanneJudgmentBladeSkill>();
    }

    private void FixedUpdate()
    {
        if (!IsCasting) return;
        if (!forceStopEachFixed) return;
        if (rb == null) return;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    public bool TryStartJudgment()
    {
        if (IsCasting) return false;
        if (Time.time < nextProcTime) return false;

        float roll = Random.value;
        Debug.Log($"🎲 Roll: {roll:F3} (<= {procChance})");

        if (roll > procChance)
            return false;

        Debug.Log("🔥 심판 발동!");

        nextProcTime = Time.time + cooldown;

        BeginPrayer();

        // 🔥🔥🔥 핵심 추가 부분
        if (skill != null)
            skill.StartSkill();
        else
            Debug.LogError("JeanneJudgmentBladeSkill 없음!", this);

        return true;
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
            rb.linearDamping = castingDamping;

            prevConstraints = rb.constraints;
            rb.constraints = RigidbodyConstraints2D.FreezePositionX |
                             RigidbodyConstraints2D.FreezePositionY |
                             RigidbodyConstraints2D.FreezeRotation;
        }

        SetLock(true);

        if (anim != null && !string.IsNullOrEmpty(animStatePrayer))
            anim.Play(animStatePrayer, 0, 0f);

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

        for (int i = 0; i < scriptsToLock.Length; i++)
        {
            if (scriptsToLock[i] == null) continue;
            scriptsToLock[i].enabled = !v;
        }
    }
}
