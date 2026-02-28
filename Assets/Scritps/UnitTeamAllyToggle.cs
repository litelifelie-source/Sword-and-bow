using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(UnitTeam))]
public class UnitTeamAllyToggle : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("비워두면 같은 오브젝트에서 UnitTeam을 자동으로 가져옵니다.")]
    public UnitTeam unitTeam;

    [Header("Scope")]
    [Tooltip("토글을 적용할 루트 Transform입니다.\n비우면 unitTeam.applyRoot가 있으면 그걸, 없으면 이 오브젝트(transform)를 사용합니다.")]
    public Transform applyRootOverride;

    [Header("Auto Find")]
    [Tooltip("ON이면 루트 이하에서 AllyFollow/AllyAttack을 자동으로 찾아서 토글합니다.")]
    public bool autoFindAllyScripts = true;

    [Tooltip("ON이면 루트 이하에서 EnemyAI/EnemyAttack/EnemyArcherAttack 등을 자동으로 찾아서 토글합니다.")]
    public bool autoFindEnemyScripts = true;

    [Header("Manual List (Optional)")]
    [Tooltip("자동 찾기 대신, 직접 지정한 컴포넌트만 토글하고 싶을 때 사용하세요.\n(여기에 넣은 것만 적용됩니다)")]
    public MonoBehaviour[] allyEnableList;

    [Tooltip("자동 찾기 대신, 직접 지정한 컴포넌트만 토글하고 싶을 때 사용하세요.\n(여기에 넣은 것만 적용됩니다)")]
    public MonoBehaviour[] enemyEnableList;

    [Header("Debug")]
    [Tooltip("팀 변경 감지 로그를 출력합니다.")]
    public bool debugLog = false;

    private Team _lastTeam;
    private Transform _root;

    void Awake()
    {
        if (!unitTeam) unitTeam = GetComponent<UnitTeam>();

        _root = ResolveRoot();
        _lastTeam = unitTeam.team;

        ApplyByTeam(_lastTeam);
    }

    void Update()
    {
        if (!unitTeam) return;

        if (unitTeam.team != _lastTeam)
        {
            _lastTeam = unitTeam.team;
            _root = ResolveRoot(); // applyRoot가 런타임에 바뀌는 경우도 대비
            ApplyByTeam(_lastTeam);

            if (debugLog)
                Debug.Log($"[UnitTeamAllyToggle] {name} team changed -> {_lastTeam}", this);
        }
    }

    private Transform ResolveRoot()
    {
        if (applyRootOverride) return applyRootOverride;
        if (unitTeam && unitTeam.applyRoot) return unitTeam.applyRoot;
        return transform;
    }

    private void ApplyByTeam(Team team)
    {
        bool isAlly = (team == Team.Ally);
        bool isEnemy = (team == Team.Enemy);

        // 1) 수동 리스트 우선 처리 (지정돼 있으면 그걸로만 처리)
        bool useManual =
            (allyEnableList != null && allyEnableList.Length > 0) ||
            (enemyEnableList != null && enemyEnableList.Length > 0);

        if (useManual)
        {
            ToggleList(allyEnableList, isAlly);
            ToggleList(enemyEnableList, isEnemy);
            return;
        }

        // 2) 자동 찾기 모드
        if (_root == null) _root = transform;

        if (autoFindAllyScripts)
        {
            var follows = _root.GetComponentsInChildren<AllyFollow>(true);
            for (int i = 0; i < follows.Length; i++)
                follows[i].enabled = isAlly;

            var attacks = _root.GetComponentsInChildren<AllyAttack>(true);
            for (int i = 0; i < attacks.Length; i++)
                attacks[i].enabled = isAlly;
        }

        if (autoFindEnemyScripts)
        {
            // 이 프로젝트 ZIP에 실제 존재하는 타입들 기준으로 토글
            var enemyAIs = _root.GetComponentsInChildren<EnemyAI>(true);
            for (int i = 0; i < enemyAIs.Length; i++)
                enemyAIs[i].enabled = isEnemy;

            var enemyArchers = _root.GetComponentsInChildren<EnemyAI_Archer>(true);
            for (int i = 0; i < enemyArchers.Length; i++)
                enemyArchers[i].enabled = isEnemy;

            var enemyAtk = _root.GetComponentsInChildren<EnemyAttack>(true);
            for (int i = 0; i < enemyAtk.Length; i++)
                enemyAtk[i].enabled = isEnemy;

            var enemyArcherAtk = _root.GetComponentsInChildren<EnemyArcherAttack>(true);
            for (int i = 0; i < enemyArcherAtk.Length; i++)
                enemyArcherAtk[i].enabled = isEnemy;
        }
    }

    private void ToggleList(MonoBehaviour[] list, bool enabled)
    {
        if (list == null) return;
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i]) list[i].enabled = enabled;
        }
    }
}