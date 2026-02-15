using System.Collections;
using UnityEngine;

public class CameraShakeRoot : MonoBehaviour
{
    Coroutine co;

    public void Shake(float duration, float amplitude)
    {
        if (co != null) StopCoroutine(co);
        co = StartCoroutine(ShakeCo(duration, amplitude));
    }

    IEnumerator ShakeCo(float duration, float amplitude)
    {
        float t = 0f;
        Vector3 origin = transform.localPosition;

        while (t < duration)
        {
            t += Time.deltaTime;
            Vector2 r = Random.insideUnitCircle * amplitude;
            transform.localPosition = origin + new Vector3(r.x, r.y, 0f);
            yield return null;
        }

        transform.localPosition = origin;
        co = null;
    }
}
