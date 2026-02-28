using UnityEngine;

[DisallowMultipleComponent]
public class UpkeepProvider : MonoBehaviour
{
    [Header("Base Upkeep (틱당)")]
    [Tooltip("이 네임드 1명의 유지비 (예: 25)")]
    [SerializeField] private int baseUpkeep = 25;

    [Header("Conditions")]
    [Tooltip("Ally 상태일 때만 유지비 발생")]
    public bool onlyWhenAlly = true;

    [Tooltip("비활성/비동행(오브젝트 꺼짐)이면 유지비 제외")]
    public bool requireActiveInHierarchy = true;

    [Tooltip("죽음/기절(Down) 상태면 유지비 제외 (Health.IsDead 사용)")]
    public bool disableWhenDead = true;

    [Header("References (자동 탐색)")]
    [SerializeField] private UnitTeam team;
    [SerializeField] private Health health;

    private void Awake()
    {
        if (team == null) team = GetComponent<UnitTeam>();
        if (health == null) health = GetComponent<Health>();
    }

    /// <summary>UpkeepManager가 합산할 값</summary>
    public int GetUpkeep()
    {
        if (!IsEligible()) return 0;
        return Mathf.Max(0, baseUpkeep);
    }

    private bool IsEligible()
    {
        if (requireActiveInHierarchy && !gameObject.activeInHierarchy)
            return false;

        if (onlyWhenAlly && team != null && team.team != Team.Ally)
            return false;

        // ✅ Health 쪽에 이미 있는 IsDead 사용 (currentHP 직접 접근 안 함)
        if (disableWhenDead && health != null && health.IsDead)
            return false;

        return true;
    }

    // ---- 외부 조절 API (선택) ----
    public void SetBaseUpkeep(int value) => baseUpkeep = Mathf.Max(0, value);
    public int GetBaseUpkeep() => baseUpkeep;
}
