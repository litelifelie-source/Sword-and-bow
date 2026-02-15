using UnityEngine;
using UnityEngine.UI;

public class HeartsUI : MonoBehaviour
{
    [Header("Hearts (3 Images in order)")]
    public Image[] hearts; // 0,1,2

    public void SetHearts(int count)
    {
        if (hearts == null) return;

        for (int i = 0; i < hearts.Length; i++)
        {
            if (hearts[i] != null)
                hearts[i].enabled = (i < count);
        }
    }
}
