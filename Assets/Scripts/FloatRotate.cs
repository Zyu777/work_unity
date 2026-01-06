using UnityEngine;

public class FloatRotate : MonoBehaviour
{
    [Header("Rotate")]
    public float rotateSpeed = 120f; // 每秒旋转角度

    [Header("Float")]
    public float floatAmplitude = 0.25f; // 浮动高度
    public float floatFrequency = 1.5f;  // 浮动速度

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        // 360° 自转（绕Y轴）
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);

        // 上下浮动（正弦）
        float yOffset = Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
        transform.position = startPos + new Vector3(0f, yOffset, 0f);
    }
}