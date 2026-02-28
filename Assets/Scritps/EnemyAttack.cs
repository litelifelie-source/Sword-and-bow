using System.Collections;
using UnityEngine;

public class EnemyAttack : MonoBehaviour
{
    [Header("Damage")]
    public int damage = 10;
    public float attackOffset = 0.6f;
    public float hitRadius = 0.4f;

    [Header("Timing")]
    public float attackCooldown = 1f;
    public float hitDelay = 0.12f;       // 타격 타이밍
    public float attackEndDelay = 0.1f;

    [Header("Targeting")]
    public LayerMask targetLayer;

    [Header("Animator Params")]
    public string trigAttack = "Attack";
    public string boolAttackR = "AttackR";
    public string boolAttackL = "AttackL";

    private Vector2 attackDir = Vector2.right;

    private Animator anim;
    private Transform ownerRoot;
    private UnitTeam myTeam;

    private float nextAttackTime;
    private Coroutine attackCo;

    public bool IsAttacking { get; private set; }

    private int playerLayer;

    void Awake()
    {
        anim = GetComponent<Animator>();
        if (anim == null) anim = GetComponentInChildren<Animator>(true);

        ownerRoot = transform.root;
        myTeam = GetComponentInParent<UnitTeam>();

        playerLayer = LayerMask.NameToLayer("Player");

        if (anim == null)
            Debug.LogError("[EnemyAttack] Animator를 찾지 못했습니다.", this);
    }

    private Vector2 Snap4(Vector2 d)
    {
        if (Mathf.Abs(d.x) > Mathf.Abs(d.y))
            return d.x > 0 ? Vector2.right : Vector2.left;
        else
            return d.y > 0 ? Vector2.up : Vector2.down;
    }

    public void TryStartAttack(Vector2 dir)
    {
        if (anim == null) return;
        if (IsAttacking) return;
        if (Time.time < nextAttackTime) return;

        // ✅ 내가 Enemy가 아니면 공격 자체 금지 (전환 버그 방지 핵심)
        if (myTeam != null && myTeam.team != Team.Enemy)
            return;

        nextAttackTime = Time.time + attackCooldown;

        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector2.right;

        attackDir = Snap4(dir);

        if (attackDir == Vector2.up) attackDir = Vector2.right;
        else if (attackDir == Vector2.down) attackDir = Vector2.left;

        anim.SetBool(boolAttackR, false);
        anim.SetBool(boolAttackL, false);

        if (attackDir == Vector2.left)
            anim.SetBool(boolAttackL, true);
        else
            anim.SetBool(boolAttackR, true);

        IsAttacking = true;

        anim.ResetTrigger(trigAttack);
        anim.SetTrigger(trigAttack);

        if (attackCo != null) StopCoroutine(attackCo);
        attackCo = StartCoroutine(AttackRoutine());
    }

    IEnumerator AttackRoutine()
    {
        yield return new WaitForSeconds(hitDelay);

        DoDamageNow();

        if (attackEndDelay > 0f)
            yield return new WaitForSeconds(attackEndDelay);

        EndAttack();
    }

    private void DoDamageNow()
    {
        if (myTeam != null && myTeam.team != Team.Enemy)
            return;

        Vector2 center = (Vector2)ownerRoot.position + attackDir * attackOffset;
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, hitRadius, targetLayer);

        foreach (Collider2D col in hits)
        {
            if (col == null) continue;
            if (col.transform.root == ownerRoot) continue;

            UnitTeam ut = col.GetComponentInParent<UnitTeam>();
            if (ut == null) continue;

            // ✅ Enemy는 Ally만 공격
            if (ut.team != Team.Ally) continue;

            Health hp = col.GetComponentInParent<Health>();
            if (hp != null && !hp.IsDown)
                hp.TakeDamage(damage);
        }
    }

    private void EndAttack()
    {
        IsAttacking = false;

        if (anim != null)
        {
            anim.SetBool(boolAttackR, false);
            anim.SetBool(boolAttackL, false);
        }
    }

    // 🔒 혹시 애니 이벤트가 남아있어도 안전
    public void DealDamage() { }
    public void AnimEvent_EndAttack() { EndAttack(); }

    public void PlayAttackSfx()
    {
        AudioSource audio = GetComponent<AudioSource>();
        if (audio != null && audio.clip != null)
            audio.PlayOneShot(audio.clip);
    }
}
