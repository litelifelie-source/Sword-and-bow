using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoldierSpawner : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject soldierPrefab;

    [Header("Spawn")]
    public int spawnCountPerWave = 3;
    public float spawnRadius = 2.0f;        // 스폰 위치 랜덤 반경

    [Header("Auto Spawn")]
    public bool autoSpawn = true;
    public float startDelay = 1.0f;
    public float waveInterval = 5.0f;

    [Header("Area Limit (ENEMY ONLY)")]
    public float areaRadius = 6.0f;         // ✅ 배치 영역 반경
    public int maxEnemiesInArea = 10;       // ✅ 배치 영역 내 적군 최대 수
    public LayerMask enemyMask;             // ✅ 적군 레이어(Enemy)만 체크

    [Header("Team (Spawn Team)")]
    public Team spawnTeam = Team.Enemy;     // 보통 적 스포너면 Enemy로
    public FormationManager formation;      // 아군일 때만 필요(그대로 둠)

    [Header("Optional: Input Test")]
    public bool allowManualSpawn = true;
    public KeyCode spawnKey = KeyCode.T;

    private Coroutine spawnRoutine;

    private void Awake()
    {
        if (formation == null)
            formation = FindFirstObjectByType<FormationManager>();
    }

    private void OnEnable()
    {
        if (autoSpawn)
            spawnRoutine = StartCoroutine(AutoSpawnLoop());
    }

    private void OnDisable()
    {
        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);
        spawnRoutine = null;
    }

    private void Update()
    {
        if (allowManualSpawn && Input.GetKeyDown(spawnKey))
            SpawnBatch(spawnCountPerWave);
    }

    private IEnumerator AutoSpawnLoop()
    {
        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        while (true)
        {
            SpawnBatch(spawnCountPerWave);
            yield return new WaitForSeconds(waveInterval);
        }
    }

    public void SpawnBatch(int count)
    {
        if (soldierPrefab == null)
        {
            Debug.LogError("SoldierSpawner: soldierPrefab이 비어있습니다.");
            return;
        }

        // ✅ 배치 영역 안의 적군 수만 계산
        int currentEnemies = CountEnemiesInArea();

        int availableSlots = maxEnemiesInArea - currentEnemies;
        if (availableSlots <= 0)
            return;

        int spawnAmount = Mathf.Min(count, availableSlots);

        for (int i = 0; i < spawnAmount; i++)
        {
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            Vector3 pos = transform.position + new Vector3(offset.x, offset.y, 0f);

            GameObject go = Instantiate(soldierPrefab, pos, Quaternion.identity);

            // 1) 팀 세팅
            UnitTeam team = go.GetComponent<UnitTeam>();
            if (team != null)
                team.team = spawnTeam;

            // 2) 레이어 세팅(스폰팀 기준)
            ApplyLayerByTeam(go, spawnTeam);

            // 3) 아군이면 진형 등록 (적 스포너면 보통 안 탐)
            if (spawnTeam == Team.Ally && formation != null)
            {
                AllyFollow follow = go.GetComponent<AllyFollow>();
                if (follow == null)
                    follow = go.AddComponent<AllyFollow>();

                formation.Register(follow);
            }
        }
    }

    private int CountEnemiesInArea()
    {
        // enemyMask 레이어에 걸린 콜라이더들만 탐색
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, areaRadius, enemyMask);

        // ✅ 콜라이더 여러 개여도 같은 적을 1명으로 처리
        HashSet<int> uniqueEnemyRoots = new HashSet<int>();

        for (int i = 0; i < hits.Length; i++)
        {
            UnitTeam ut = hits[i].GetComponentInParent<UnitTeam>();
            if (ut == null) continue;

            if (ut.team != Team.Enemy) continue; // ✅ 적군만

            uniqueEnemyRoots.Add(ut.gameObject.GetInstanceID());
        }

        return uniqueEnemyRoots.Count;
    }

    private void ApplyLayerByTeam(GameObject go, Team team)
    {
        string layerName = (team == Team.Ally) ? "Ally" : "Enemy";
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0) return;

        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }

    private void OnDrawGizmosSelected()
    {
        // 스폰 반경
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);

        // 배치 영역(적 제한)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, areaRadius);
    }
}
