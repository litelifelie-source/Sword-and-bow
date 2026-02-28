using UnityEngine;

public class Capturable : MonoBehaviour
{
    [Header("Recruit Chance (0~1)")]
    [Tooltip("0이면 0%, 1이면 100% 확률입니다.")]
    [Range(0f, 1f)]
    public float recruitChance = 0.7f;

    [Header("Debug")]
    public bool debugLog = true;

    public bool TryRecruit()
    {
        float roll = Random.value; // 0.0 이상 1.0 미만
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