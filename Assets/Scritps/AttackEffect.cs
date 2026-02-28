using UnityEngine;

public class AttackEffect : MonoBehaviour
{
    public int damage = 10;
    public bool debugLog = true;

    [HideInInspector] public Transform ownerRoot;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (debugLog)
            Debug.Log($"[Attack] Trigger hit: {other.name} | layer={LayerMask.LayerToName(other.gameObject.layer)}");

        // 자기 자신 무시
        if (ownerRoot != null && other.transform.root == ownerRoot)
        {
            if (debugLog) Debug.Log("[Attack] ❌ same root - ignored");
            return;
        }

        UnitTeam team = other.GetComponentInParent<UnitTeam>();
        if (team == null)
        {
            if (debugLog) Debug.Log("[Attack] ❌ UnitTeam 없음");
            return;
        }

        if (debugLog)
            Debug.Log($"[Attack] team = {team.team}");

        if (team.team != Team.Enemy)
        {
            if (debugLog) Debug.Log("[Attack] ❌ Enemy 아님");
            return;
        }

        Health hp = other.GetComponentInParent<Health>();
        if (hp == null)
        {
            if (debugLog) Debug.Log("[Attack] ❌ Health 없음");
            return;
        }

        hp.TakeDamage(damage);

        if (debugLog) Debug.Log("[Attack] ✅ 데미지 적용 완료");
    }
}
