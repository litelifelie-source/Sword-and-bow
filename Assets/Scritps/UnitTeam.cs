using UnityEngine;

public enum Team { Enemy, Ally }

public class UnitTeam : MonoBehaviour
{
    public Team team = Team.Enemy;

    public void ConvertToAlly()
    {
        team = Team.Ally;

        // ✅ 루트 기준으로 레이어 변경 (중요)
        int allyLayer = LayerMask.NameToLayer("Ally");
        if (allyLayer != -1)
            SetLayerRecursively(transform.root.gameObject, allyLayer);

        // ✅ 기절 해제까지 (중요)
        Health hp = GetComponentInChildren<Health>(true);
        if (hp != null)
        {
            hp.ReviveFull();
            hp.RefreshBarColor();
        }

        // ✅ 루트 기준으로 스크립트 토글(안전)
        MonoBehaviour[] mbs = transform.root.GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < mbs.Length; i++)
        {
            MonoBehaviour mb = mbs[i];
            if (mb == null) continue;

            if (mb is UnitTeam) continue;
            if (mb is Health) continue;

            string n = mb.GetType().Name;

            if (n.StartsWith("Enemy"))
                mb.enabled = false;
            else if (n.StartsWith("Ally"))
                mb.enabled = true;
        }

        Debug.Log($"{transform.root.name} 영입됨! (Ally) rootLayer={LayerMask.LayerToName(transform.root.gameObject.layer)}");
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        for (int i = 0; i < obj.transform.childCount; i++)
            SetLayerRecursively(obj.transform.GetChild(i).gameObject, layer);
    }
}
