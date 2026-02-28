using UnityEngine;

/// <summary>
/// 협동기 전용 마탄:
/// - Incoming(슈바르트->잔느 방패)로 날아감
/// - 방패 트리거(CoopShieldReflector)에 닿으면 Reflected로 전환되어 바깥으로 발사
/// - Reflected 상태에서는 적(Enemy)에게 데미지
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CoopMagicBullet : MonoBehaviour
{
    public enum BulletState { Incoming, Reflected }

    [Header("Runtime (ReadOnly)")]
    [Tooltip("현재 탄 상태(인커밍/도탄)")]
    public BulletState state = BulletState.Incoming;

    [Tooltip("현재 이동 속도")]
    public float speed;

    [Tooltip("현재 데미지")]
    public int damage;

    [Tooltip("수명 만료 시간(초)")]
    public float lifeTime = 2.2f;

    private Vector2 _dir;
    private float _dieAt;

    private Transform _owner;
    private Transform _ignoreRoot;
    private Team _ownerTeam;

    private Collider2D _col;
    private Rigidbody2D _rb;

    private void Awake()
    {
        _col = GetComponent<Collider2D>();
        _col.isTrigger = true;

        _rb = GetComponent<Rigidbody2D>();
        if (_rb == null)
        {
            // Rigidbody가 없으면 transform 이동으로 fallback 가능하지만,
            // 스피드/물리 안정성을 위해 Rigidbody 사용을 권장합니다.
            // 여기서는 자동으로 추가하지 않고 경고만 띄웁니다.
            Debug.LogWarning("[CoopMagicBullet] Rigidbody2D is missing. (Recommended: Kinematic + GravityScale 0)", this);
        }
        else
        {
            // 권장 세팅 강제(스피드 적용 안 되는 이슈 방지)
            _rb.gravityScale = 0f;
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }
    }

    private void Update()
    {
        if (Time.time >= _dieAt)
        {
            Destroy(gameObject);
            return;
        }

        // Rigidbody가 없다면 fallback 이동(권장 X)
        if (_rb == null)
        {
            transform.position += (Vector3)(_dir * speed * Time.deltaTime);
        }
    }

    public void InitIncoming(Vector2 direction, float bulletSpeed, float lt, int dmg, Transform owner, Transform ignoreRoot)
    {
        state = BulletState.Incoming;

        _dir = direction.normalized;
        speed = bulletSpeed;
        lifeTime = Mathf.Max(0.1f, lt);
        damage = dmg;

        _owner = owner;
        _ignoreRoot = ignoreRoot;

        _dieAt = Time.time + lifeTime;

        CacheOwnerTeam();
        IgnoreOwnerCollisions(owner);

        ApplyVelocity();

        RotateToDir();
    }

    public void Reflect(Vector2 newDirection, Transform newOwner)
    {
        state = BulletState.Reflected;

        _dir = newDirection.normalized;
        _owner = newOwner;

        CacheOwnerTeam();
        IgnoreOwnerCollisions(newOwner);

        ApplyVelocity();
        RotateToDir();
    }

    private void ApplyVelocity()
    {
        if (_rb != null)
        {
            _rb.linearVelocity = _dir * speed;
        }
    }

    private void RotateToDir()
    {
        float angle = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void CacheOwnerTeam()
    {
        _ownerTeam = Team.Ally;
        if (_owner == null) return;

        UnitTeam ut = _owner.GetComponentInParent<UnitTeam>();
        if (ut != null) _ownerTeam = ut.team;
    }

    private void IgnoreOwnerCollisions(Transform owner)
    {
        if (owner == null || _col == null) return;

        Collider2D[] ownerCols = owner.GetComponentsInChildren<Collider2D>(true);
        foreach (var c in ownerCols)
            Physics2D.IgnoreCollision(_col, c, true);

        if (_ignoreRoot != null)
        {
            Collider2D[] ignoreCols = _ignoreRoot.GetComponentsInChildren<Collider2D>(true);
            foreach (var c in ignoreCols)
                Physics2D.IgnoreCollision(_col, c, true);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Incoming은 데미지 처리 안 함(방패에서 Reflect가 처리)
        if (state == BulletState.Incoming) return;

        UnitTeam targetTeam = other.GetComponentInParent<UnitTeam>();
        if (targetTeam == null) return;

        if (targetTeam.team == _ownerTeam) return;

        Health hp =
            other.GetComponentInParent<Health>() ??
            targetTeam.GetComponentInChildren<Health>(true);

        if (hp == null) return;

        hp.TakeDamage(damage);
        Destroy(gameObject);
    }
}