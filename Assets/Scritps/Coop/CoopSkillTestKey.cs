using UnityEngine;

/// <summary>
/// 테스트용: 키 누르면 협동기 발동.
/// </summary>
public class CoopSkillTestKey : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("협동기 스크립트")]
    public CoopJeanneSchwarzSkill coop;

    [Header("Key")]
    [Tooltip("발동 키")]
    public KeyCode triggerKey = KeyCode.F5;

    private void Awake()
    {
        if (coop == null)
            coop = FindFirstObjectByType<CoopJeanneSchwarzSkill>();
    }

    private void Update()
    {
        if (coop == null) return;

        if (Input.GetKeyDown(triggerKey))
            coop.TriggerCoop();
    }
}