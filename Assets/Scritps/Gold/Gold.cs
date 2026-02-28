using UnityEngine;
using TMPro;

public class Gold : MonoBehaviour
{
    [SerializeField] private int gold = 1200;

    [Header("UI (Optional)")]
    [Tooltip("골드 표시용 TMP 텍스트")]
    public TMP_Text goldText;

    [Header("Clamp")]
    [Tooltip("true면 gold가 minGold 아래로 내려가지 않음")]
    public bool clampMin = true;

    [Tooltip("최소 허용 골드 값")]
    public int minGold = 0;

    public int Current => gold;

    private void Awake()
    {
        if (clampMin)
            gold = Mathf.Max(minGold, gold);

        RefreshUI();
    }

    public void Set(int value)
    {
        gold = value;

        if (clampMin)
            gold = Mathf.Max(minGold, gold);

        RefreshUI();
    }

    public bool CanSpend(int amount)
    {
        int next = gold - amount;
        return next >= (clampMin ? minGold : int.MinValue);
    }

    public bool TrySpend(int amount)
    {
        if (!CanSpend(amount))
            return false;

        Set(gold - amount);
        return true;
    }

    public void Add(int amount)
    {
        if (amount == 0)
            return;

        Set(gold + amount);
    }

    private void RefreshUI()
    {
        if (goldText != null)
            goldText.text = gold.ToString();
    }
}