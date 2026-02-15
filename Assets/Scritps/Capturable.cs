using UnityEngine;

public class Capturable : MonoBehaviour
{
    [Header("Recruit Chance (0~1)")]
    [Range(0f, 1f)]
    public float recruitChance = 0.7f; // 0.85 = 85%

    [Header("Debug")]
    public bool debugLog = true;   // 확률이 진짜 적용되는지 확인용

    public bool TryRecruit()
    {
        float roll = Random.value;     // 0.0 ~ 1.0
        bool success = roll < recruitChance;

        if (debugLog)
        {
            Debug.Log(
                $"[TryRecruit] roll={roll:F3} / chance={recruitChance:F2} ({recruitChance * 100f:F0}%) -> " +
                (success ? "SUCCESS" : "FAIL")
            );
        }

        return success;
    }
}
