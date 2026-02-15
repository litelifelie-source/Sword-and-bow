using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArcherSpawner : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject archerPrefab;

    [Header("Spawn")]
    public int spawnCountPerWave = 3;
    public float spawnRadius = 2.0f;

    [Header("Auto Spawn")]
    public bool autoSpawn = true;
    public float startDelay = 1.0f;
    public float waveInterval = 5.0f;

    [Header("Area Limit (ENEMY ONLY)")]
    public float areaRadius = 6.0f;
    public int maxEnemiesInArea = 10;
    public LayerMask enemyMask;

    [Header("Team (Spawn Team)")]
    public Team spawnTeam = Team.Enemy;
    public FormationManager formation;

    [Header("Optional: Input Test")]
    public bool allowManualSpawn = true;
    public KeyCode spawnKey = KeyCode.T;

    [Header("Pooling (Optional)")]
    [Tooltip("있으면 풀링 사용. 없으면 Instantiate 사용.")]
    public MonoBehaviour poolProvider; // ArcherPoolManager를 여기 넣어도 됨(타입 몰라도 됨)

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
        if (archerPrefab == null)
        {
            Debug.LogError("ArcherSpawner: archerPrefab이 비어있습니다.");
            return;
        }

        int currentEnemies = CountEnemiesInArea();
        int availableSlots = maxEnemiesInArea - currentEnemies;
        if (availableSlots <= 0)
            return;

        int spawnAmount = Mathf.Min(count, availableSlots);

        for (int i = 0; i < spawnAmount; i++)
        {
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            Vector3 pos = transform.position + new Vector3(offset.x, offset.y, 0f);

            GameObject go = SpawnOne(pos);

            if (go == null) continue;

            // 1) 팀 세팅
            UnitTeam team = go.GetComponent<UnitTeam>();
            if (team != null)
                team.team = spawnTeam;

            // 2) 레이어 세팅
            ApplyLayerByTeam(go, spawnTeam);

            // 3) 아군이면 진형 등록
            if (spawnTeam == Team.Ally && formation != null)
            {
                AllyFollow follow = go.GetComponent<AllyFollow>();
                if (follow == null)
                    follow = go.AddComponent<AllyFollow>();

                formation.Register(follow);
            }

            // 4) 풀 재사용 대비 최소 리셋
            ResetCommon(go);
        }
    }

    GameObject SpawnOne(Vector3 pos)
    {
        // ✅ 풀링이 연결되어 있으면, 리플렉션으로 Get(pos, rot) 호출
        if (poolProvider != null)
        {
            var t = poolProvider.GetType();
            var m = t.GetMethod("Get", new System.Type[] { typeof(Vector3), typeof(Quaternion) });
            if (m != null)
            {
                object obj = m.Invoke(poolProvider, new object[] { pos, Quaternion.identity });
                return obj as GameObject;
            }
        }

        // ✅ 풀 없으면 일반 Instantiate
        return Instantiate(archerPrefab, pos, Quaternion.identity);
    }

    private void ResetCommon(GameObject go)
    {
        EnemyAI ai = go.GetComponent<EnemyAI>();
        if (ai != null) ai.enabled = true;

        EnemyArcherAttack atk = go.GetComponent<EnemyArcherAttack>();
        if (atk != null) atk.enabled = true;
    }

    private int CountEnemiesInArea()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, areaRadius, enemyMask);

        HashSet<int> uniqueEnemyRoots = new HashSet<int>();

        for (int i = 0; i < hits.Length; i++)
        {
            UnitTeam ut = hits[i].GetComponentInParent<UnitTeam>();
            if (ut == null) continue;
            if (ut.team != Team.Enemy) continue;

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
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, areaRadius);
    }
}
