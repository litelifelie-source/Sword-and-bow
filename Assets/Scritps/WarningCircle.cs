using UnityEngine;

public class WarningCircle : MonoBehaviour
{
    public SpriteRenderer sr;
    public Sprite redSprite;
    public Sprite blueSprite;

    public void Init(UnitTeam owner)
    {
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>(true);
        if (owner == null || sr == null) return;

        // ✅ owner가 Ally면 파랑, Enemy면 빨강 (원하시면 반대로도 가능)
        sr.sprite = (owner.team == Team.Ally) ? blueSprite : redSprite;
    }
}
