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
    public float skillDuration = 5f;     // ✅ 5초 동안 스캔하다가 종료
    public float noTargetPoll = 0.2f;    // ✅ 타겟 없을 때 재스캔 간격

    private void Awake()
    {
        if (proc == null) proc = GetComponent<JeanneJudgmentProc>();
        if (ownerTeam == null) ownerTeam = GetComponentInParent<UnitTeam>();
        if (ownerTeam == null) ownerTeam = GetComponent<UnitTeam>();
    }

    public void StartSkill()
    {
        StartCoroutine(CoSkill());
    }

private IEnumerator CoSkill()
{
    float noTargetTimeout = 5f;   // ✅ 타겟이 5초 연속 없으면 종료
    float noTargetTime = 0f;

    int spawned = 0;

    while (spawned < maxDrops)
    {
        List<Health> targets = CollectTargets();

        if (targets.Count == 0)
        {
            // 타겟 없음 시간 누적
            float wait = 0.2f;
            yield return new WaitForSeconds(wait);
            noTargetTime += wait;

            if (noTargetTime >= noTargetTimeout)
                break;

            continue;
        }

        // ✅ 타겟을 찾았으면 "타겟 없음 타이머" 리셋
        noTargetTime = 0f;

        // 가장 가까운 1명만 계속 때리기 싫으면 랜덤으로 분산도 가능
        Health t = targets[0]; // 가까운 1명
        // Health t = targets[Random.Range(0, targets.Count)]; // 분산 타격

        if (t == null || t.IsDown)
            continue;

        Vector3 pos = t.transform.position;

        // 1) 경고 원
        WarningCircle warn = Instantiate(warningPrefab, pos, Quaternion.identity);
        warn.Init(ownerTeam);

        yield return new WaitForSeconds(warnTime);
        if (warn != null) Destroy(warn.gameObject);

        // 2) 중간에 죽었으면 스킵(드랍 카운트 증가 X)
        if (t == null || t.IsDown)
        {
            yield return new WaitForSeconds(dropInterval);
            continue;
        }

        // 3) 검 생성
        GiantBladeImpact blade = Instantiate(bladePrefab, pos, Quaternion.identity);
        blade.Init(ownerTeam);
        spawned++; // ✅ 실제로 생성했을 때만 카운트

        yield return new WaitForSeconds(dropInterval);
    }

    proc?.EndPrayer();
}

    private List<Health> CollectTargets()
    {
        var list = new List<Health>();
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, searchRadius);

        foreach (var h in hits)
        {
            if (h == null) continue;

            UnitTeam tTeam = h.GetComponentInParent<UnitTeam>();
            if (tTeam == null) continue;

            // 적만
            if (ownerTeam != null && ownerTeam.team == tTeam.team) continue;

            Health hp = h.GetComponentInParent<Health>();
            if (hp == null) continue;
            if (hp.IsDown) continue;

            if (!list.Contains(hp))
                list.Add(hp);
        }

        // 가까운 순
        list.Sort((a, b) =>
        {
            float da = (a.transform.position - transform.position).sqrMagnitude;
            float db = (b.transform.position - transform.position).sqrMagnitude;
            return da.CompareTo(db);
        });

        return list;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, searchRadius);
    }
}
