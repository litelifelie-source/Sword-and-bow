using System;
using UnityEngine;

public enum Team { NPC, Enemy, Ally }

public class UnitTeam : MonoBehaviour
{
    public Team team = Team.Enemy;
    public bool IsAlly => team == Team.Ally;
    public bool IsEnemy => team == Team.Enemy;
    public bool IsNPC => team == Team.NPC;

    // ✅✅✅ [추가] Ally 전환 신호 (questId/nodeKey 포함)
    public static event Action<string, string, UnitTeam> OnConvertedToAllySignal;

    [Header("Layer Names")]
    public string npcLayerName = "NPC";
    public string allyLayerName = "Ally";
    public string enemyLayerName = "Enemy";

    [Header("Apply Scope (IMPORTANT)")]
    [Tooltip("팀 변경을 적용할 루트. 비우면 '이 오브젝트(transform)' 기준으로만 적용합니다.\n※ transform.root 쓰면 'Units 컨테이너'까지 먹어서 전체가 바뀔 수 있습니다.")]
    public Transform applyRoot;

    [Header("What to change")]
    public bool changeLayer = true;
    public bool toggleScriptsByPrefix = true;
    public bool freezeAllWhenNPC = true;

    [Header("Layer Change Filter")]
    [Tooltip("✅ 레이어 재귀 변경에서 제외할 레이어들.\n- HP Canvas 같은 UI는 여기 포함시키세요.\n- 예: UI, WorldUI\n- 체크된 레이어는 ConvertToAlly/Enemy/NPC 시에도 레이어가 절대 바뀌지 않습니다.")]
    public LayerMask excludeFromLayerChange;

    [Header("Optional - Follow Toggle")]
    public MonoBehaviour followScript;
    public bool enableFollowOnlyWhenAlly = true;

    private struct RbPrev { public Rigidbody2D rb; public RigidbodyConstraints2D constraints; }
    private RbPrev[] _prevConstraints;

    private Transform Root => (applyRoot != null) ? applyRoot : transform;

    // ✅ 기존 API 유지
    public void ConvertToAlly() => ApplyTeam(Team.Ally, reviveFull: true);

    // ✅✅✅ [추가] 신호 포함 버전 (브릿지에 “나 Ally 됐다” 알려줄 때 이걸 쓰세요)
    [Tooltip("Ally 전환 후 QuestDialogueBridge에 신호를 보냅니다. (questId/nodeKey 기반)")]
    public void ConvertToAlly(string questId, string nodeKey)
    {
        ApplyTeam(Team.Ally, reviveFull: true);

        if (!string.IsNullOrEmpty(questId) && !string.IsNullOrEmpty(nodeKey))
            OnConvertedToAllySignal?.Invoke(questId, nodeKey, this);
    }

    public void ConvertToEnemy() => ApplyTeam(Team.Enemy, reviveFull: false);
    public void ConvertToNPC() => ApplyTeam(Team.NPC, reviveFull: false);

    public void ApplyTeam(Team newTeam, bool reviveFull)
    {
        team = newTeam;
        Transform root = Root;

        if (changeLayer)
        {
            string layerName = newTeam switch
            {
                Team.Ally => allyLayerName,
                Team.Enemy => enemyLayerName,
                Team.NPC => npcLayerName,
                _ => enemyLayerName
            };

            int layer = LayerMask.NameToLayer(layerName);
            if (layer != -1)
                SetLayerRecursively(root.gameObject, layer);
        }

        Health hp = root.GetComponentInChildren<Health>(true);
        if (hp != null)
        {
            if (reviveFull) hp.ReviveFull();
            hp.RefreshBarColor();
        }

        if (toggleScriptsByPrefix)
            ToggleScriptsByPrefix(root);

        if (freezeAllWhenNPC)
            ApplyNPCFreeze(root, newTeam == Team.NPC);

        if (followScript != null)
        {
            if (newTeam == Team.Enemy) followScript.enabled = false;
            else if (newTeam == Team.NPC) followScript.enabled = false;
            else followScript.enabled = enableFollowOnlyWhenAlly;
        }
    }

    private void ApplyNPCFreeze(Transform root, bool freeze)
    {
        var rbs = root.GetComponentsInChildren<Rigidbody2D>(true);

        if (freeze)
        {
            _prevConstraints = new RbPrev[rbs.Length];
            for (int i = 0; i < rbs.Length; i++)
                _prevConstraints[i] = new RbPrev { rb = rbs[i], constraints = rbs[i].constraints };
        }

        for (int i = 0; i < rbs.Length; i++)
        {
            var rb = rbs[i];
            if (rb == null) continue;

            if (freeze)
            {
                // ✅✅✅ [수정] 구버전 호환: velocity 사용
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.constraints = RigidbodyConstraints2D.FreezeAll;
            }
            else
            {
                bool restored = false;
                if (_prevConstraints != null)
                {
                    for (int k = 0; k < _prevConstraints.Length; k++)
                    {
                        if (_prevConstraints[k].rb == rb)
                        {
                            rb.constraints = _prevConstraints[k].constraints;
                            restored = true;
                            break;
                        }
                    }
                }

                if (!restored)
                    rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            }
        }
    }

    private void ToggleScriptsByPrefix(Transform root) { /* 원본 그대로 */ }
    private void SetLayerRecursively(GameObject obj, int layer)
    {
        if (!obj) return;

        // ✅ 제외 레이어면: 이 오브젝트 + 자식은 레이어 변경 금지
        int cur = obj.layer;
        if ((excludeFromLayerChange.value & (1 << cur)) != 0)
            return;

        obj.layer = layer;

        Transform t = obj.transform;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i).gameObject, layer);
    }
}