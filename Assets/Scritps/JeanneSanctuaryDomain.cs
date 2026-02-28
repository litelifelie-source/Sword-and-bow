using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 성역전개:
/// - 지속시간 9.5초
/// - 아군(시전자와 같은 팀): 피해 25% 감소 (damageTakenMultiplier=0.75), 초당 체력 1.25% 회복
/// - 적군(시전자와 다른 팀): 이동속도 35% 감소
/// </summary>
public class JeanneSanctuaryDomain : MonoBehaviour
{
    [Header("Duration")]
    public float duration = 9.5f;

    [Header("Area")]
    public float radius = 5.5f;

    [Header("Caster (중요)")]
    public Team casterTeam = Team.Ally; // ✅ Proc에서 시전자 팀을 주입하세요.

    [Header("Ally Buff")]
    [Range(0f, 1f)] public float allyDamageReduction = 0.25f;        // 25%
    [Range(0f, 0.2f)] public float allyRegenPercentPerSec = 0.0125f; // 1.25% = 0.0125

    [Header("Enemy Debuff")]
    [Range(0f, 1f)] public float enemyMoveSlow = 0.35f;              // 35%

    [Header("Scan")]
    public float tickInterval = 0.2f;
    public LayerMask scanLayer; // 비워두면 Ally/Enemy/Player 레이어 자동

    // ─────────────────────────────────────────────────────────────
    // 내부 상태 캐시 (원복용)
    class AllyState
    {
        public float originalDamageMul;
        public float healRemainder; // 소수 누적
    }

    class EnemyMoveState
    {
        public bool hasAllyFollow;
        public float allyFollowOriginal;

        public bool hasJeanneFollow;
        public float jeanneFollowOriginal;

        public bool hasEnemyAI;
        public float enemyAIOriginal;

        public bool hasEnemyArcherAI;
        public float enemyArcherAIOriginal;

        // ✅ 플레이어 이동
        public bool hasPlayerController;
        public float playerControllerOriginal;
    }

    private readonly Dictionary<Health, AllyState> allyStates = new();
    private readonly Dictionary<Transform, EnemyMoveState> enemyStates = new();

    private readonly HashSet<Health> alliesThisTick = new();
    private readonly HashSet<Transform> enemiesThisTick = new();

    private Coroutine co;

    private void OnEnable()
    {
        // ✅ scanLayer 자동 구성: Ally/Enemy + Player 포함(플레이어가 Player 레이어를 쓴다면 필수급)
        if (scanLayer.value == 0)
        {
            int ally = LayerMask.NameToLayer("Ally");
            int enemy = LayerMask.NameToLayer("Enemy");
            int player = LayerMask.NameToLayer("Player");

            int mask = 0;
            if (ally != -1) mask |= (1 << ally);
            if (enemy != -1) mask |= (1 << enemy);
            if (player != -1) mask |= (1 << player);

            scanLayer = mask;
        }

        co = StartCoroutine(Run());
    }

    private void OnDisable()
    {
        if (co != null) StopCoroutine(co);
        CleanupAll();
    }

    private IEnumerator Run()
    {
        float endTime = Time.time + duration;

        while (Time.time < endTime)
        {
            TickApply();
            yield return new WaitForSeconds(tickInterval);
        }

        CleanupAll();
        Destroy(gameObject);
    }

    private void TickApply()
    {
        alliesThisTick.Clear();
        enemiesThisTick.Clear();

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius, scanLayer);

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null) continue;

            // collider가 자식일 수 있으니 parent에서 찾기
            Health hp = hits[i].GetComponentInParent<Health>();
            if (hp == null) continue;
            if (hp.IsDown) continue;

            UnitTeam team = hits[i].GetComponentInParent<UnitTeam>();
            if (team == null) continue;

            // ✅ 핵심: "시전자 팀"과 같으면 아군 버프, 다르면 적군 디버프
            if (team.team == casterTeam)
            {
                ApplyAlly(hp);
                alliesThisTick.Add(hp);
            }
            else
            {
                // 원하면 NPC는 제외 가능:
                // if (team.team == Team.NPC) continue;

                Transform root = hp.transform.root;
                ApplyEnemySlow(root);
                enemiesThisTick.Add(root);
            }
        }

        RestoreMissingAllies();
        RestoreMissingEnemies();
    }

    // ─────────────────────────────────────────────────────────────
    // Ally
    private void ApplyAlly(Health hp)
    {
        if (!allyStates.TryGetValue(hp, out AllyState st))
        {
            st = new AllyState
            {
                originalDamageMul = hp.damageTakenMultiplier,
                healRemainder = 0f
            };
            allyStates.Add(hp, st);
        }

        float targetMul = 1f - allyDamageReduction; // 0.75
        hp.damageTakenMultiplier = Mathf.Min(hp.damageTakenMultiplier, targetMul);

        float healFloat = hp.maxHP * allyRegenPercentPerSec * tickInterval;
        healFloat += st.healRemainder;

        int healInt = Mathf.FloorToInt(healFloat);
        st.healRemainder = healFloat - healInt;

        if (healInt > 0)
            hp.Heal(healInt);
    }

    private void RestoreMissingAllies()
    {
        if (allyStates.Count == 0) return;

        List<Health> toRemove = null;

        foreach (var kv in allyStates)
        {
            Health hp = kv.Key;
            if (hp == null)
            {
                (toRemove ??= new List<Health>()).Add(hp);
                continue;
            }

            if (!alliesThisTick.Contains(hp))
            {
                hp.damageTakenMultiplier = kv.Value.originalDamageMul;
                (toRemove ??= new List<Health>()).Add(hp);
            }
        }

        if (toRemove != null)
        {
            for (int i = 0; i < toRemove.Count; i++)
                allyStates.Remove(toRemove[i]);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Enemy slow
    private void ApplyEnemySlow(Transform root)
    {
        if (root == null) return;

        if (!enemyStates.TryGetValue(root, out EnemyMoveState st))
        {
            st = new EnemyMoveState();

            EnemyAI ai = root.GetComponentInChildren<EnemyAI>(true);
            if (ai != null)
            {
                st.hasEnemyAI = true;
                st.enemyAIOriginal = ai.moveSpeed;
            }

            EnemyAI_Archer aai = root.GetComponentInChildren<EnemyAI_Archer>(true);
            if (aai != null)
            {
                st.hasEnemyArcherAI = true;
                st.enemyArcherAIOriginal = aai.moveSpeed;
            }

            AllyFollow af = root.GetComponentInChildren<AllyFollow>(true);
            if (af != null)
            {
                st.hasAllyFollow = true;
                st.allyFollowOriginal = af.moveSpeed;
            }

            JeanneFollow jf = root.GetComponentInChildren<JeanneFollow>(true);
            if (jf != null)
            {
                st.hasJeanneFollow = true;
                st.jeanneFollowOriginal = jf.moveSpeed;
            }

            // ✅ 플레이어 이동 (PlayerController.speed)
            PlayerController pc = root.GetComponentInChildren<PlayerController>(true);
            if (pc != null)
            {
                st.hasPlayerController = true;
                st.playerControllerOriginal = pc.speed;
            }

            enemyStates.Add(root, st);
        }

        float mul = 1f - enemyMoveSlow; // 0.65

        if (st.hasEnemyAI)
        {
            EnemyAI ai = root.GetComponentInChildren<EnemyAI>(true);
            if (ai != null) ai.moveSpeed = st.enemyAIOriginal * mul;
        }

        if (st.hasEnemyArcherAI)
        {
            EnemyAI_Archer aai = root.GetComponentInChildren<EnemyAI_Archer>(true);
            if (aai != null) aai.moveSpeed = st.enemyArcherAIOriginal * mul;
        }

        if (st.hasAllyFollow)
        {
            AllyFollow af = root.GetComponentInChildren<AllyFollow>(true);
            if (af != null) af.moveSpeed = st.allyFollowOriginal * mul;
        }

        if (st.hasJeanneFollow)
        {
            JeanneFollow jf = root.GetComponentInChildren<JeanneFollow>(true);
            if (jf != null) jf.moveSpeed = st.jeanneFollowOriginal * mul;
        }

        // ✅ 플레이어 슬로우 적용
        if (st.hasPlayerController)
        {
            PlayerController pc = root.GetComponentInChildren<PlayerController>(true);
            if (pc != null) pc.speed = st.playerControllerOriginal * mul;
        }
    }

    private void RestoreMissingEnemies()
    {
        if (enemyStates.Count == 0) return;

        List<Transform> toRemove = null;

        foreach (var kv in enemyStates)
        {
            Transform root = kv.Key;
            if (root == null)
            {
                (toRemove ??= new List<Transform>()).Add(root);
                continue;
            }

            if (!enemiesThisTick.Contains(root))
            {
                RestoreEnemy(root, kv.Value);
                (toRemove ??= new List<Transform>()).Add(root);
            }
        }

        if (toRemove != null)
        {
            for (int i = 0; i < toRemove.Count; i++)
                enemyStates.Remove(toRemove[i]);
        }
    }

    private void RestoreEnemy(Transform root, EnemyMoveState st)
    {
        if (root == null) return;

        if (st.hasEnemyAI)
        {
            EnemyAI ai = root.GetComponentInChildren<EnemyAI>(true);
            if (ai != null) ai.moveSpeed = st.enemyAIOriginal;
        }

        if (st.hasEnemyArcherAI)
        {
            EnemyAI_Archer aai = root.GetComponentInChildren<EnemyAI_Archer>(true);
            if (aai != null) aai.moveSpeed = st.enemyArcherAIOriginal;
        }

        if (st.hasAllyFollow)
        {
            AllyFollow af = root.GetComponentInChildren<AllyFollow>(true);
            if (af != null) af.moveSpeed = st.allyFollowOriginal;
        }

        if (st.hasJeanneFollow)
        {
            JeanneFollow jf = root.GetComponentInChildren<JeanneFollow>(true);
            if (jf != null) jf.moveSpeed = st.jeanneFollowOriginal;
        }

        // ✅ 플레이어 원복
        if (st.hasPlayerController)
        {
            PlayerController pc = root.GetComponentInChildren<PlayerController>(true);
            if (pc != null) pc.speed = st.playerControllerOriginal;
        }
    }

    private void CleanupAll()
    {
        foreach (var kv in allyStates)
        {
            if (kv.Key != null)
                kv.Key.damageTakenMultiplier = kv.Value.originalDamageMul;
        }
        allyStates.Clear();

        foreach (var kv in enemyStates)
        {
            if (kv.Key != null)
                RestoreEnemy(kv.Key, kv.Value);
        }
        enemyStates.Clear();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, radius);
    }
#endif
}
