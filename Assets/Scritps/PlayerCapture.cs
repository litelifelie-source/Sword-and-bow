using UnityEngine;

public class PlayerCapture : MonoBehaviour
{
    public float captureRadius = 1.5f;
    public LayerMask enemyLayer;
    public FormationManager formationManager;

    void Awake()
    {
        if (formationManager == null)
            formationManager = GetComponent<FormationManager>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
            TryCapture();
    }

    void TryCapture()
    {
        int stun = LayerMask.NameToLayer("Stun");

        LayerMask recruitMask = enemyLayer;
        if (stun != -1) recruitMask |= (1 << stun);

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, captureRadius, recruitMask);

        if (hits == null || hits.Length == 0)
        {
            Debug.Log("근처에 적 없음");
            return;
        }

        UnitTeam team = null;
        Health hp = null;
        Capturable cap = null;

        for (int i = 0; i < hits.Length; i++)
        {
            UnitTeam t = hits[i].GetComponentInParent<UnitTeam>();
            if (t == null || t.team != Team.Enemy) continue;

            Health h = t.GetComponentInChildren<Health>(true);
            if (h == null || !h.IsDown) continue;

            Capturable c = t.GetComponentInChildren<Capturable>(true);
            if (c == null) continue;

            team = t; hp = h; cap = c;
            break;
        }

        if (team == null)
        {
            Debug.Log("기절한 적이 없음 (체력이 0일 때만 영입 가능)");
            return;
        }

        GameObject root = team.transform.root.gameObject; // ✅ 모든 처리는 루트 기준

        if (!cap.TryRecruit())
        {
            Debug.Log("영입 실패!");
            Destroy(root); // ✅ 루트 삭제
            return;
        }

        // ✅ 영입 성공
        team.ConvertToAlly();

        // ✅ AllyFollow도 루트에 붙이기
        AllyFollow follow = root.GetComponent<AllyFollow>();
        if (follow == null) follow = root.AddComponent<AllyFollow>();
        follow.enabled = true;

        if (formationManager != null)
            formationManager.Register(follow);

        if (follow.formation == null || follow.slotIndex < 0)
        {
            Debug.LogWarning($"[Capture] Register 후에도 formation/slotIndex 미세팅 -> chase로 임시 전환: formation={(follow.formation ? follow.formation.name : "null")}, slot={follow.slotIndex}");
            follow.StartChase(transform, 1.2f);
        }
        else
        {
            follow.StopChase();
        }

        Debug.Log($"영입 성공! root={root.name} layer={LayerMask.LayerToName(root.layer)}");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, captureRadius);
    }
}
