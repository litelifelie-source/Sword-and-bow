using UnityEngine;

public class ArrowProjectile : MonoBehaviour
{
    public float speed = 10f;
    public float lifeTime = 3f;

    private Vector2 dir;
    private int damage;

    private Transform owner;
    private Team ownerTeam;

    private Collider2D myCol;

    private void Awake()
    {
        myCol = GetComponent<Collider2D>();
        Destroy(gameObject, lifeTime);
    }

    public void Initialize(Vector2 direction, int dmg, Transform ownerTransform)
    {
        dir = direction.normalized;
        damage = dmg;
        owner = ownerTransform;

        // ✅ 발사자 팀 저장
        UnitTeam ut = owner.GetComponentInParent<UnitTeam>();
        if (ut != null)
            ownerTeam = ut.team;

        // 발사자 충돌 무시
        if (owner != null && myCol != null)
        {
            Collider2D[] ownerCols = owner.GetComponentsInChildren<Collider2D>(true);
            foreach (var c in ownerCols)
                Physics2D.IgnoreCollision(myCol, c, true);
        }

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void Update()
    {
        transform.position += (Vector3)(dir * speed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (owner != null && other.transform.IsChildOf(owner)) return;

        UnitTeam targetTeam = other.GetComponentInParent<UnitTeam>();
        if (targetTeam == null) return;

        // ✅ 같은 팀이면 무시
        if (targetTeam.team == ownerTeam) return;

        Health hp =
            other.GetComponentInParent<Health>() ??
            targetTeam.GetComponentInChildren<Health>(true);

        if (hp == null) return;

        hp.TakeDamage(damage);
        Destroy(gameObject);
    }
}
