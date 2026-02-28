using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JeanneJudgmentBladeSkill : MonoBehaviour
{
    [Header("Refs")]
    public JeanneJudgmentProc proc;      // 기도/잠금
    public UnitTeam ownerTeam;           // 시전자 팀

    [Header("Prefabs")]
    public WarningCircle warningPrefab;  // 경고 원 프리팹
    public GiantBladeImpact bladePrefab; // 대형검 프리팹

    [Header("Config")]
    public float searchRadius = 12f;
    public float warnTime = 0.35f;
    public float dropInterval = 0.25f;
    public int maxDrops = 7;

    [Header("Scan")]
    public float noTargetTimeout = 5f;   // 타겟이 이 시간 연속 없으면 종료
    public float noTargetPoll = 0.2f;    // 타겟 없을 때 재스캔 간격

    [Header("Targeting (recommended)")]
    public LayerMask targetLayer;                 // ✅ 1차 후보 필터(성능/의도)
    public TargetRule targetRule = TargetRule.EnemiesOnly; // ✅ 최종 판정(팀)

    [Header("Optional")]
    public bool allowPlayerWithoutTeam = false; // ✅ Player에 UnitTeam 안 붙일 거면 켜기
    public string playerTag = "Player";

    private Transform ownerRoot;

    private void Awake()
    {
        if (proc == null) proc = GetComponent<JeanneJudgmentProc>();
        if (ownerTeam == null) ownerTeam = GetComponentInParent<UnitTeam>() ?? GetComponent<UnitTeam>();

        ownerRoot = (ownerTeam != null) ? ownerTeam.transform : transform;
    }

    public void StartSkill()
    {
        StartCoroutine(CoSkill());
    }

    private IEnumerator CoSkill()
    {
        float noTargetTime = 0f;
        int spawned = 0;

        while (spawned < maxDrops)
        {
            List<Health> targets = CollectTargets(ownerRoot.position);

            if (targets.Count == 0)
            {
                yield return new WaitForSeconds(noTargetPoll);
                noTargetTime += noTargetPoll;
                if (noTargetTime >= noTargetTimeout) break;
                continue;
            }

            noTargetTime = 0f;

            // 가까운 1명 고정(원하시면 Random / Player 우선으로 바꿔드릴게요)
            Health t = targets[0];
            if (t == null || t.IsDown)
                continue;

            Vector3 pos = t.transform.position;

            // 1) 경고 원
            if (warningPrefab != null)
            {
                WarningCircle warn = Instantiate(warningPrefab, pos, Quaternion.identity);
                warn.Init(ownerTeam);

                yield return new WaitForSeconds(warnTime);
                if (warn != null) Destroy(warn.gameObject);
            }
            else
            {
                yield return new WaitForSeconds(warnTime);
            }

            // 2) 중간에 죽었으면 스킵(드랍 카운트 증가 X)
            if (t == null || t.IsDown)
            {
                yield return new WaitForSeconds(dropInterval);
                continue;
            }

            // 3) 검 생성
            if (bladePrefab != null)
            {
                GiantBladeImpact blade = Instantiate(bladePrefab, pos, Quaternion.identity);
                blade.Init(ownerTeam);
                spawned++;
            }

            yield return new WaitForSeconds(dropInterval);
        }

        proc?.EndPrayer();
    }

    private List<Health> CollectTargets(Vector3 center)
    {
        var list = new List<Health>();

        // ✅ 1차 후보: targetLayer
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, searchRadius, targetLayer);

        foreach (var col in hits)
        {
            if (col == null) continue;

            Health hp = col.GetComponentInParent<Health>();
            if (hp == null || hp.IsDown) continue;

            UnitTeam otherTeam = col.GetComponentInParent<UnitTeam>();

            // ✅ Player UnitTeam 예외(선택)
            if (otherTeam == null)
            {
                if (!allowPlayerWithoutTeam) continue;
                if (!col.CompareTag(playerTag) && (hp != null && !hp.CompareTag(playerTag))) continue;

                // allowPlayerWithoutTeam = true 이고 Player라면, 팀판정 통과로 취급
                // (이 경우 ownerTeam이 null이면 안전하게 제외)
                if (ownerTeam == null) continue;
                if (targetRule == TargetRule.AlliesOnly) continue; // 동료만 타격이면 제외
            }
            else
            {
                if (!PassRule(ownerTeam, otherTeam, targetRule, ownerRoot)) continue;
            }

            if (!list.Contains(hp))
                list.Add(hp);
        }

        // 가까운 순 정렬
        list.Sort((a, b) =>
        {
            float da = (a.transform.position - center).sqrMagnitude;
            float db = (b.transform.position - center).sqrMagnitude;
            return da.CompareTo(db);
        });

        return list;
    }

    private bool PassRule(UnitTeam owner, UnitTeam other, TargetRule rule, Transform ownerRootTf)
    {
        if (rule == TargetRule.Everyone) return true;
        if (owner == null || other == null) return false;

        switch (rule)
        {
            case TargetRule.EnemiesOnly:
                return owner.team != other.team;

            case TargetRule.AlliesOnly:
                return owner.team == other.team;

            case TargetRule.AllExceptOwner:
                return other.transform != ownerRootTf;

            default:
                return false;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3 p = transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(p, searchRadius);
    }
#endif
}
