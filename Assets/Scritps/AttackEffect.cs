using UnityEngine;

public class AttackEffect : MonoBehaviour
{
    public int damage = 10;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 자기 자신은 무시
        if (other.transform.root == transform.root)
            return;

        // 맞은 대상의 팀 확인
        UnitTeam targetTeam = other.GetComponentInParent<UnitTeam>();
        if (targetTeam == null)
            return;

        // 플레이어 공격이므로 Enemy만 맞게
        if (targetTeam.team != Team.Enemy)
            return;

        Health enemyHealth = other.GetComponentInParent<Health>();
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(damage);
        }
    }
}
