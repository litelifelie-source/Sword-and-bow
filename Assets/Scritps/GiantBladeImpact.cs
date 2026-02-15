using UnityEngine;

public class GiantBladeImpact : MonoBehaviour
{
    [Header("Damage")]
    public int damage = 80;
    public float hitRadius = 2.5f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip shockwaveClip;

    [Header("Shake")]
    public float shakeDuration = 0.15f;
    public float shakeAmplitude = 0.25f;

    private UnitTeam ownerTeam;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    public void Init(UnitTeam owner)
    {
        ownerTeam = owner;
    }

    // ✅ 1️⃣ 충격파 사운드 전용
public void PlayShockwaveSfx()
{
    if (shockwaveClip == null) return;

    // AudioSource가 붙은 임시 오브젝트 생성 후 재생, 끝나면 자동 삭제
    GameObject go = new GameObject("SFX_Shockwave");
    go.transform.position = transform.position;

    var src = go.AddComponent<AudioSource>();
    src.spatialBlend = 0f; // 2D면 0
    src.PlayOneShot(shockwaveClip);

    Destroy(go, shockwaveClip.length);
}

    // ✅ 2️⃣ 데미지 전용 (기존 그대로)
    public void OnImpact()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, hitRadius);

        foreach (var h in hits)
        {
            UnitTeam tTeam = h.GetComponentInParent<UnitTeam>();
            if (tTeam == null) continue;
            if (ownerTeam != null && ownerTeam.team == tTeam.team) continue;

            Health hp = h.GetComponentInParent<Health>();
            if (hp == null) continue;
            if (hp.IsDown) continue;

            hp.TakeDamage(damage);
        }
    }

    // ✅ 3️⃣ 진동(카메라 쉐이크) 전용
    public void ImpactShake()
    {
        var cam = Camera.main;
        if (cam == null) return;

        var root = cam.transform.parent;
        if (root == null) return;

        var shake = root.GetComponent<CameraShakeRoot>();
        if (shake != null)
            shake.Shake(shakeDuration, shakeAmplitude);
    }

    public void SelfDestroy()
    {
        Destroy(gameObject);
    }
}
