using UnityEngine;

public class JeanneModeTestKey : MonoBehaviour
{
    [Header("Target")]
    public NamedUnitModeSwitcher jeanne;     // 잔느 오브젝트에 붙은 스위처

    [Header("Keys")]
    public KeyCode toAllyKey  = KeyCode.F1;  // 잔느를 Ally로
    public KeyCode toEnemyKey = KeyCode.F2;  // 잔느를 Enemy로
    public KeyCode toggleKey  = KeyCode.F3;  // Ally<->Enemy 토글

    [Header("Options")]
    public bool allowNPCKey = true;
    public KeyCode toNpcKey = KeyCode.F4;

    private void Awake()
    {
        if (jeanne == null)
        {
            // 씬에 1명만 있으면 자동으로 잡힘 (여러 명이면 인스펙터 연결 권장)
            jeanne = FindFirstObjectByType<NamedUnitModeSwitcher>();
        }
    }

    private void Update()
    {
        if (jeanne == null) return;

        if (Input.GetKeyDown(toAllyKey))
            SetMode(UnitMode.Ally);

        if (Input.GetKeyDown(toEnemyKey))
            SetMode(UnitMode.Enemy);

        if (Input.GetKeyDown(toggleKey))
            ToggleEnemyAlly();

        if (allowNPCKey && Input.GetKeyDown(toNpcKey))
            SetMode(UnitMode.NPC);
    }

    private void SetMode(UnitMode mode)
    {
        jeanne.Apply(mode);
        Debug.Log($"[JeanneModeTestKey] Jeanne -> {mode}");
    }

    private void ToggleEnemyAlly()
    {
        var next = (jeanne.mode == UnitMode.Ally) ? UnitMode.Enemy : UnitMode.Ally;
        SetMode(next);
    }
}
