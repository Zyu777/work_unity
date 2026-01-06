using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBar_Slider : MonoBehaviour
{
    [Header("Target")]
    public EnemyController enemy;          // 绑定 EnemyController
    public Transform followTarget;         // 跟随目标（默认 enemy.transform）
    public Vector3 worldOffset = new Vector3(0f, 2.2f, 0f);

    [Header("UI (Hierarchy)")]
    public string sliderPath = "Slider";   // enemy health 下的 Slider

    [Header("Billboard")]
    public bool faceCamera = true;
    public Camera cam;

    [Header("Smooth")]
    public bool smooth = true;
    public float smoothSpeed = 12f;

    private Slider hpSlider;
    private float hpShown = 1f;

    // 如果 EnemyController 没有 maxHP，用初始 HP 当 max
    private int cachedMaxHp = -1;

    void Awake()
    {
        if (cam == null) cam = Camera.main;

        // 自动找 enemy
        if (enemy == null)
            enemy = GetComponentInParent<EnemyController>();

        if (followTarget == null && enemy != null)
            followTarget = enemy.transform;

        // 自动找 Slider
        Transform s = transform.Find(sliderPath);
        if (s != null) hpSlider = s.GetComponent<Slider>();

        // 初始化 slider
        if (hpSlider != null)
        {
            hpSlider.minValue = 0f;
            hpSlider.wholeNumbers = false;
        }
    }

    void LateUpdate()
    {
        if (enemy == null || followTarget == null) return;

        // 1) 跟随到头顶
        transform.position = followTarget.position + worldOffset;

        // 2) 面向相机
        if (faceCamera && cam != null)
            transform.forward = cam.transform.forward;

        // 3) 更新血条
        int max = GetMaxHp(enemy);
        float hp01 = (max <= 0) ? 0f : Mathf.Clamp01((float)enemy.HP / max);

        // slider 用 0~1 更好（不受 maxHP 变动影响）
        if (hpSlider != null)
        {
            hpShown = smooth ? Mathf.Lerp(hpShown, hp01, Time.deltaTime * smoothSpeed) : hp01;
            hpSlider.maxValue = 1f;
            hpSlider.value = hpShown;
        }

        // 4) 死亡隐藏
        if (enemy.isDead)
            gameObject.SetActive(false);
    }

    private int GetMaxHp(EnemyController e)
    {
        // ✅ 如果你以后给 EnemyController 加了 maxHP，这里改成 return e.maxHP;
        if (cachedMaxHp > 0) return cachedMaxHp;
        cachedMaxHp = Mathf.Max(1, e.HP); // 把初始HP当上限
        return cachedMaxHp;
    }
}
