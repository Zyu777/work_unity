using System.Collections;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    private Vector3 originLocalPos;
    private Coroutine co;

    void Awake()
    {
        originLocalPos = transform.localPosition;
    }

    public void Shake(float duration, float strength)
    {
        if (co != null) StopCoroutine(co);
        co = StartCoroutine(CoShake(duration, strength));
    }

    private IEnumerator CoShake(float duration, float strength)
    {
        float t = 0f;
        originLocalPos = transform.localPosition;

        while (t < duration)
        {
            t += Time.deltaTime;
            Vector3 offset = Random.insideUnitSphere * strength;
            transform.localPosition = originLocalPos + offset;
            yield return null;
        }

        transform.localPosition = originLocalPos;
        co = null;
    }
}