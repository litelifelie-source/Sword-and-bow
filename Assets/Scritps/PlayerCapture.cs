using UnityEngine;
using System.Collections.Generic;

public class PlayerCapture : MonoBehaviour
{
    [Header("Capture")]
    [Tooltip("플레이어 기준 원형 반경 안의 대상만 탐색합니다.")]
    public float captureRadius = 1.2f;

    [Tooltip("영입 대상으로 판정할 레이어 마스크입니다. (Physics2D OverlapCircleAll 필터)")]
    public LayerMask targetMask;

    [Tooltip("영입 시도 키")]
    public KeyCode captureKey = KeyCode.F;

    [Header("Rules")]
    [Tooltip("true이면 다운 상태(Health.IsDown)일 때만 영입 가능합니다.")]
    public bool requireDownState = true;

    [Header("Fail Behavior")]
    [Tooltip("영입 실패 시 대상 유닛을 제거(사라지게)할지 여부입니다.")]
    public bool destroyOnFail = true;

    [Tooltip("destroyOnFail=true일 때, 비활성화로 숨길지(체크) / Destroy로 제거할지(해제)")]
    public bool disableInsteadOfDestroy = false;

    [Header("Debug")]
    [Tooltip("디버그 로그 출력")]
    public bool debugLog = true;

    private readonly HashSet<Transform> _processedRoots = new();

    private void Update()
    {
        if (!Input.GetKeyDown(captureKey))
            return;

        var hits = Physics2D.OverlapCircleAll(transform.position, captureRadius, targetMask);

        if (hits == null || hits.Length == 0)
        {
            if (debugLog) Debug.Log("[Capture] 범위 내 대상 없음");
            return;
        }

        _processedRoots.Clear();

        foreach (var col in hits)
        {
            if (col == null) continue;

            Transform root = col.transform.root;

            // 동일 유닛 중복 판정 방지
            if (_processedRoots.Contains(root))
                continue;

            _processedRoots.Add(root);

            UnitTeam team = root.GetComponent<UnitTeam>();
            if (team == null || team.team != Team.Enemy)
                continue;

            Health hp = root.GetComponent<Health>();
            if (hp == null)
                continue;

            if (requireDownState && !hp.IsDown)
            {
                if (debugLog) Debug.Log($"[Capture] {root.name} 다운 상태 아님 -> 스킵");
                continue;
            }

            // ✅ root 기준으로 Capturable 찾기 (콜라이더가 자식에 있어도 안정)
            Capturable capturable = root.GetComponentInChildren<Capturable>();
            if (capturable == null)
            {
                if (debugLog) Debug.LogWarning($"[Capture] {root.name} Capturable 없음 -> 스킵");
                continue;
            }

            // ✅ 확률 굴리기
            bool success = capturable.TryRecruit();

            if (!success)
            {
                if (debugLog) Debug.Log($"[Capture] {root.name} 영입 실패");

                if (destroyOnFail)
                {
                    if (disableInsteadOfDestroy)
                    {
                        // “사라지게”만 하고 오브젝트는 남김(풀링/리젠 등에 유리)
                        root.gameObject.SetActive(false);
                        if (debugLog) Debug.Log($"[Capture] {root.name} 비활성화 처리");
                    }
                    else
                    {
                        // 완전 제거
                        Destroy(root.gameObject);
                        if (debugLog) Debug.Log($"[Capture] {root.name} Destroy 처리");
                    }
                }

                return; // 한 번만 시도하고 종료
            }

            // ✅ 성공 처리
            team.ConvertToAlly();
            hp.ReviveFull();

            if (debugLog) Debug.Log($"[Capture] {root.name} → Ally 전환 + Full Revive 완료");
            return;
        }

        if (debugLog) Debug.Log("[Capture] 조건 충족 대상 없음");
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, captureRadius);
    }
#endif
}