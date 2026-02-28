using System;
using System.Collections.Generic;
using UnityEngine;

public enum UnitMode
{
    NPC,
    Ally,
    Enemy
}

public class NamedUnitModeSwitcher : MonoBehaviour
{
    [Header("Current Mode")]
    public UnitMode mode = UnitMode.NPC;

    [Header("Init Behavior")]
    [Tooltip("Awake에서 mode를 즉시 적용할지")]
    public bool applyOnAwake = true;

    [Tooltip("Awake에서 현재 상태(Team/Layer)로 mode를 추론해서 덮어쓸지")]
    public bool autoInferModeOnAwake = true;

    [Header("Mode Scripts (행동을 만드는 스크립트만 넣으세요)")]
    public MonoBehaviour[] npcEnable;
    public MonoBehaviour[] allyEnable;
    public MonoBehaviour[] enemyEnable;

    [Header("Optional - Disable Lists (해당 모드에서 '끄고 싶은 것'만 넣으세요)")]
    public MonoBehaviour[] npcDisable;
    public MonoBehaviour[] allyDisable;
    public MonoBehaviour[] enemyDisable;

    [Header("Never Disable (절대 꺼지면 안 되는 것)")]
    [Tooltip("SkillDistributor / *Proc / Health / UnitTeam 등은 여기로 빼세요.")]
    public MonoBehaviour[] alwaysOn;

    [Header("Safety")]
    [Tooltip("Enable 리스트에 없는 스크립트를 '자동으로 끄는' 반대처리. 실수하면 스킬까지 꺼질 수 있어 기본 OFF 추천.")]
    public bool disableOthersNotInEnableList = false;

    [Header("Optional - Physics Lock")]
    public Rigidbody2D rb;
    public bool freezeAllWhenNPC = true;
    public bool zeroVelocityOnSwitch = true;
    private RigidbodyConstraints2D _prevConstraints;
    private bool _prevConstraintsCaptured;

    [Header("Optional - Layer Switching")]
    public bool switchLayer = false;
    public string npcLayerName = "NPC";
    public string allyLayerName = "Ally";
    public string enemyLayerName = "Enemy";

    [Header("Optional - Team Component (있으면 같이 전환)")]
    public UnitTeam team;

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (team == null) team = GetComponent<UnitTeam>();

        // 기존 제약 보관(한 번만)
        if (rb != null && !_prevConstraintsCaptured)
        {
            _prevConstraints = rb.constraints;
            _prevConstraintsCaptured = true;
        }

        if (!applyOnAwake) return;

        if (autoInferModeOnAwake)
            mode = InferModeFromCurrentState(mode);

        Apply(mode);
    }

    private UnitMode InferModeFromCurrentState(UnitMode fallback)
    {
        // 1) UnitTeam이 있으면 team 값이 최우선
        if (team != null)
        {
            if (team.team == Team.Ally)  return UnitMode.Ally;
            if (team.team == Team.Enemy) return UnitMode.Enemy;
            if (team.team == Team.NPC)   return UnitMode.NPC;
        }

        // 2) 레이어 이름으로 추론
        string ln = LayerMask.LayerToName(gameObject.layer);
        if (!string.IsNullOrEmpty(ln))
        {
            if (ln == npcLayerName)   return UnitMode.NPC;
            if (ln == allyLayerName)  return UnitMode.Ally;
            if (ln == enemyLayerName) return UnitMode.Enemy;
        }

        // 3) 모르겠으면 인스펙터 mode 유지
        return fallback;
    }

    /// <summary>외부에서 호출: NPC/Ally/Enemy 전환</summary>
    public void Apply(UnitMode newMode)
    {
        mode = newMode;

        ApplyScriptsForMode(newMode);
        ApplyPhysicsForMode(newMode);

        if (switchLayer) ApplyLayerForMode(newMode);

        ApplyTeamForMode(newMode);

        // ✅ 마지막 안전장치: 항상 켜둘 것 강제 ON
        SetEnabled(alwaysOn, true);
    }

    private void ApplyScriptsForMode(UnitMode m)
    {
        // 1) 우선: 항상 켜둘 것 먼저 ON
        SetEnabled(alwaysOn, true);

        // 2) Enable 목록은 "한 번 모두 끈 뒤 해당 모드만 켠다" (기존 패턴 유지)
        SetEnabled(npcEnable, false);
        SetEnabled(allyEnable, false);
        SetEnabled(enemyEnable, false);

        // 3) Disable 목록은 현재 모드에서 끄고 싶은 것만 끈다.
        switch (m)
        {
            case UnitMode.NPC:
                SetEnabled(npcEnable, true);
                SetEnabled(npcDisable, false);
                break;

            case UnitMode.Ally:
                SetEnabled(allyEnable, true);
                SetEnabled(allyDisable, false);
                break;

            case UnitMode.Enemy:
                SetEnabled(enemyEnable, true);
                SetEnabled(enemyDisable, false);
                break;
        }

        // 4) (선택) enable 리스트에 없는 행동 스크립트를 자동 OFF 하고 싶으면 이 옵션을 켬
        if (disableOthersNotInEnableList)
        {
            // 안전하게 하려면 actionScripts를 따로 구성해서 거기만 대상으로 하세요.
            // 기본 OFF 권장.
        }

        // 5) 마지막으로 항상 켜둘 것 재확인
        SetEnabled(alwaysOn, true);
    }

    private void ApplyPhysicsForMode(UnitMode m)
    {
        if (rb == null) return;

        if (zeroVelocityOnSwitch)
        {
            // ✅ 호환성: linearVelocity가 없는 버전이면 velocity로 바꾸세요.
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (!freezeAllWhenNPC) return;

        if (m == UnitMode.NPC)
        {
            _prevConstraints = rb.constraints;
            _prevConstraintsCaptured = true;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }
        else
        {
            if (_prevConstraintsCaptured)
                rb.constraints = _prevConstraints;
            else
                rb.constraints = RigidbodyConstraints2D.FreezeRotation; // fallback
        }
    }

    private void ApplyLayerForMode(UnitMode m)
    {
        string target = m switch
        {
            UnitMode.NPC   => npcLayerName,
            UnitMode.Ally  => allyLayerName,
            UnitMode.Enemy => enemyLayerName,
            _              => npcLayerName
        };

        int layer = LayerMask.NameToLayer(target);
        if (layer == -1)
        {
            Debug.LogWarning($"[NamedUnitModeSwitcher] Layer '{target}' not found.", this);
            return;
        }

        int oldLayer = gameObject.layer;
        string oldName = LayerMask.LayerToName(oldLayer);

        gameObject.layer = layer;

        Debug.Log(
            $"[ModeSwitcher] {gameObject.name} Layer: {oldName} ➜ {LayerMask.LayerToName(layer)}",
            this
        );
    }

    private void ApplyTeamForMode(UnitMode m)
    {
        if (team == null) return;

        if (m == UnitMode.Ally)       team.ConvertToAlly();
        else if (m == UnitMode.Enemy) team.ConvertToEnemy();
        else
        {
            // NPC로 돌리고 싶으면 UnitTeam에 ConvertToNPC() 추가 추천.
        }
    }

    private void SetEnabled(MonoBehaviour[] list, bool on)
    {
        if (list == null) return;

        for (int i = 0; i < list.Length; i++)
        {
            var mb = list[i];
            if (mb == null) continue;

            // ✅ alwaysOn에 포함된 스크립트는 어떤 경우에도 끄지 않음
            if (!on && IsInAlwaysOn(mb)) continue;

            mb.enabled = on;
        }
    }

    private bool IsInAlwaysOn(MonoBehaviour mb)
    {
        if (alwaysOn == null || mb == null) return false;
        for (int i = 0; i < alwaysOn.Length; i++)
        {
            if (alwaysOn[i] == mb) return true;
        }
        return false;
    }

    // =========================================================
    // ✅ Auto Fill Buttons (Context Menu) - 공용 버전
    // =========================================================

    [ContextMenu("Auto Fill/AlwaysOn (SkillDistributor + Proc + Core)")]
    private void AutoFillAlwaysOn_DistributorProcCore()
    {
        // 공용 규칙:
        // - 타입명 끝이 "SkillDistributor" 인 것 (예: JeanneSkillDistributor, SchwarzSkillDistributor ...)
        // - 타입명 끝이 "Proc" 인 것 (예: JeanneLightWaveProc, SchwarzSomethingProc ...)
        // - 코어: Health, UnitTeam
        var list = new List<MonoBehaviour>();
        var seen = new HashSet<MonoBehaviour>();

        var all = GetComponentsInChildren<MonoBehaviour>(true); // 비활성 포함
        foreach (var mb in all)
        {
            if (mb == null) continue;

            string tn = mb.GetType().Name;

            bool isDistributor = tn.EndsWith("SkillDistributor", StringComparison.Ordinal);
            bool isProc        = tn.EndsWith("Proc", StringComparison.Ordinal);
            bool isHealth      = tn == nameof(Health);
            bool isUnitTeam    = tn == nameof(UnitTeam);

            if (!(isDistributor || isProc || isHealth || isUnitTeam))
                continue;

            if (seen.Add(mb))
                list.Add(mb);
        }

        // 보기 좋게 정렬
        list.Sort((a, b) =>
        {
            int c = string.CompareOrdinal(a.GetType().Name, b.GetType().Name);
            if (c != 0) return c;
            return string.CompareOrdinal(a.gameObject.name, b.gameObject.name);
        });

        alwaysOn = list.ToArray();

        Debug.Log($"[ModeSwitcher] alwaysOn AutoFill 완료: {alwaysOn.Length}개", this);
        for (int i = 0; i < alwaysOn.Length; i++)
            Debug.Log($"  - {alwaysOn[i].GetType().Name} ({alwaysOn[i].gameObject.name})", this);

        // 안전하게 즉시 ON
        SetEnabled(alwaysOn, true);
    }

    [ContextMenu("Auto Fill/AlwaysOn (Clear)")]
    private void AutoFillAlwaysOn_Clear()
    {
        alwaysOn = Array.Empty<MonoBehaviour>();
        Debug.Log("[ModeSwitcher] alwaysOn 비움", this);
    }

    // =========================================================

    [ContextMenu("Mode/NPC")]   private void ToNPC()  => Apply(UnitMode.NPC);
    [ContextMenu("Mode/Ally")]  private void ToAlly() => Apply(UnitMode.Ally);
    [ContextMenu("Mode/Enemy")] private void ToEnemy()=> Apply(UnitMode.Enemy);
}
