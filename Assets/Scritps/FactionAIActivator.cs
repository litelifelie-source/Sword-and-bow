using UnityEngine;

/// <summary>
/// FactionAIActivator (Auto)
/// - UnitTeam.team 변화를 자동 감지해서 AI/Attack 스크립트를 ON/OFF 합니다.
/// - ✅ NPC일 때만 OFF
/// - ✅ Ally/Enemy면 ON
/// - ✅ 외부에서 Refresh 호출 필요 없음
/// </summary>
public class FactionAIActivator : MonoBehaviour
{
    [Header("Team Ref")]
    [Tooltip("이 유닛의 팀 정보. 비워두면 부모에서 자동 탐색합니다.")]
    public UnitTeam myTeam;

    [Header("Controlled Scripts")]
    [Tooltip("AI 스크립트(예: EnemyAIPro). NPC일 때만 비활성화됩니다.")]
    public Behaviour enemyAIPRO;

    [Tooltip("공격 스크립트(예: EnemyRangeAttackPRO). NPC일 때만 비활성화됩니다.")]
    public Behaviour enemyRangeAttackPro;

    [Header("Auto Detect")]
    [Tooltip("팀 변경 감지 주기(초). 0이면 매 프레임 감지(권장: 0.05~0.2).")]
    [Min(0f)] public float checkInterval = 0.1f;

    private Team _lastTeam;
    private bool _hasLast;
    private float _nextCheckTime;

    private void Awake()
    {
        if (!myTeam) myTeam = GetComponentInParent<UnitTeam>();

        // 인스펙터에서 안 넣었으면 자동 탐색
        if (!enemyAIPRO) enemyAIPRO = GetComponentInChildren<EnemyAIPro>(true);
        if (!enemyRangeAttackPro) enemyRangeAttackPro = GetComponentInChildren<EnemyRangeAttackPRO>(true);

        ForceApply(); // 시작 상태 반영
    }

    private void OnEnable()
    {
        _nextCheckTime = 0f;
    }

    private void Update()
    {
        if (myTeam == null) return;

        if (checkInterval > 0f && Time.time < _nextCheckTime)
            return;

        _nextCheckTime = Time.time + checkInterval;

        Team t = myTeam.team;

        if (_hasLast && t == _lastTeam)
            return;

        _lastTeam = t;
        _hasLast = true;

        ApplyByTeam(t);
    }

    private void ForceApply()
    {
        if (myTeam == null) return;
        _lastTeam = myTeam.team;
        _hasLast = true;
        ApplyByTeam(_lastTeam);
    }

    private void ApplyByTeam(Team team)
    {
        bool enableCombat = team != Team.NPC;

        if (enemyAIPRO) enemyAIPRO.enabled = enableCombat;
        if (enemyRangeAttackPro) enemyRangeAttackPro.enabled = enableCombat;
    }
}