using UnityEngine;

/// <summary>
/// 잔느 방패(트리거 콜라이더)에 닿은 Incoming 마탄을 도탄(Reflected)으로 바깥 방향 발사.
/// </summary>
public class CoopShieldReflector : MonoBehaviour
{
    [Header("References")]
    [Tooltip("도탄 트리거로 사용할 콜라이더(필수). CircleCollider2D 권장, IsTrigger=true")]
    public Collider2D reflectTrigger;

    [Tooltip("도탄 탄의 소유자(보통 잔느 Transform). 비우면 이 컴포넌트의 Transform 사용")]
    public Transform reflectOwner;

    [Header("Reflect Behavior")]
    [Tooltip("도탄 발사 방향 랜덤 각도(도 단위). 예: 35면 좌우로 +-35도 퍼짐")]
    [Range(0f, 180f)] public float reflectSpreadAngle = 35f;

    [Tooltip("도탄은 기본적으로 '방패 중심 -> 바깥' 방향으로 나갑니다. 여기에 추가로 Y를 조금 올리고 싶으면 사용")]
    public float extraUpBias = 0.10f;

    [Header("Debug")]
    [Tooltip("도탄 로그")]
    public bool debugLog = false;

    private void Awake()
    {
        if (reflectOwner == null) reflectOwner = transform;

        if (reflectTrigger == null)
            reflectTrigger = GetComponent<Collider2D>();

        if (reflectTrigger == null)
            Debug.LogWarning("[CoopShieldReflector] reflectTrigger is null. Please assign a Trigger collider.", this);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (reflectTrigger == null) return;

        CoopMagicBullet b = other.GetComponent<CoopMagicBullet>();
        if (b == null) return;

        if (b.state != CoopMagicBullet.BulletState.Incoming) return;

        // 도탄 방향: (탄 위치 - 방패 중심) 방향 기반
        Vector2 baseDir = (other.transform.position - transform.position);
        if (baseDir.sqrMagnitude < 0.0001f)
            baseDir = Vector2.right;

        baseDir.Normalize();
        baseDir.y += extraUpBias;
        baseDir.Normalize();

        // 퍼짐 적용
        float ang = Random.Range(-reflectSpreadAngle, reflectSpreadAngle);
        Vector2 outDir = Rotate(baseDir, ang);

        // 도탄 전환
        b.Reflect(outDir, reflectOwner);

        if (debugLog) Debug.Log("[CoopShieldReflector] Reflect!", this);
    }

    private Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cs = Mathf.Cos(rad);
        float sn = Mathf.Sin(rad);
        return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
    }
}