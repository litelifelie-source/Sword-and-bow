using System.Collections.Generic;
using UnityEngine;

public enum FormationType
{
    LineBehind,   // 뒤로 한 줄(행렬)
    Grid          // 격자(추천)
}

public class FormationManager : MonoBehaviour
{
    [Header("Formation")]
    public FormationType formation = FormationType.Grid;

    public float spacingX = 0.8f;   // 좌우 간격
    public float spacingY = 0.8f;   // 앞뒤 간격(뒤쪽으로)
    public int gridColumns = 3;     // Grid일 때 한 줄에 몇 명

    [Header("Rotation 기준(2D 탑다운)")]
    public bool useFacingFromMovement = true;
    public Vector2 facing = Vector2.down; // 기본 바라보는 방향

    private readonly List<AllyFollow> allies = new();

    // 플레이어가 이동 스크립트에서 facing을 갱신해주면 진형도 그 방향 기준으로 회전합니다.
    public void SetFacing(Vector2 dir)
    {
        if (dir.sqrMagnitude > 0.0001f) facing = dir.normalized;
    }

    public void Register(AllyFollow ally)
    {
        if (ally == null) return;
        if (!allies.Contains(ally)) allies.Add(ally);
        ally.formation = this;
        ally.slotIndex = allies.IndexOf(ally);
    }

    public void Unregister(AllyFollow ally)
    {
        if (ally == null) return;
        int idx = allies.IndexOf(ally);
        if (idx < 0) return;
        allies.RemoveAt(idx);

        // 슬롯 인덱스 재정렬
        for (int i = 0; i < allies.Count; i++)
            allies[i].slotIndex = i;
    }

    public Vector2 GetSlotWorldPosition(int slotIndex)
    {
        // “뒤쪽” 방향 = -facing
        Vector2 f = facing.sqrMagnitude > 0.0001f ? facing.normalized : Vector2.down;
        Vector2 back = -f;
        Vector2 right = new Vector2(f.y, -f.x); // 2D에서 오른쪽 벡터

        int row, col;

        if (formation == FormationType.LineBehind)
        {
            row = slotIndex; // 한 줄로 뒤로만
            col = 0;
        }
        else // Grid
        {
            col = slotIndex % Mathf.Max(1, gridColumns);
            row = slotIndex / Mathf.Max(1, gridColumns);

            // 가운데 정렬
            // columns=3이면 col=0,1,2 -> -1,0,1 로 맞추기
            col -= (Mathf.Max(1, gridColumns) - 1) / 2;
        }

        Vector2 offset = right * (col * spacingX) + back * (row * spacingY);
        return (Vector2)transform.position + offset;
    }
}
