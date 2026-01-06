using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthHUD_Width : MonoBehaviour
{
    public PlayerController player;

    [Header("Hierarchy Paths")]
    public string hpFillPath = "health/Fill Area/Fill"; // 你的红条 Fill
    public string staminaSliderPath = "Slider";         // 你的绿条 Slider

    [Header("Smooth")]
    public bool smooth = true;
    public float smoothSpeed = 12f;

    private RectTransform hpFillRect;
    private float hpWidthMax = 0f;

    private Slider staminaSlider;

    private float hp01Shown = 1f;
    private float staShown = 0f;

    void Awake()
    {
        if (player == null) player = FindObjectOfType<PlayerController>();

        // 找红条 Fill Rect
        Transform hpTf = transform.Find(hpFillPath);
        if (hpTf != null) hpFillRect = hpTf.GetComponent<RectTransform>();

        // 找耐力 Slider
        Transform stTf = transform.Find(staminaSliderPath);
        if (stTf != null) staminaSlider = stTf.GetComponent<Slider>();
    }

    void Start()
    {
        // 记录血条满血时的宽度（max width）
        if (hpFillRect != null)
            hpWidthMax = hpFillRect.rect.width;

        ForceRefresh();
    }

    void Update()
    {
        if (player == null) return;

        // -------- HP 宽度缩放 --------
        float hp01Target = (player.maxHP <= 0) ? 0f : (float)player.HP / player.maxHP;
        hp01Target = Mathf.Clamp01(hp01Target);
        hp01Shown = smooth ? Mathf.Lerp(hp01Shown, hp01Target, Time.deltaTime * smoothSpeed) : hp01Target;

        if (hpFillRect != null && hpWidthMax > 0.01f)
        {
            // 保持高度不变，只改宽度
            float newW = hpWidthMax * hp01Shown;
            hpFillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, newW);
        }

        // -------- Stamina Slider --------
        float staTarget = Mathf.Clamp(player.stamina, 0f, player.maxStamina);
        staShown = smooth ? Mathf.Lerp(staShown, staTarget, Time.deltaTime * smoothSpeed) : staTarget;

        if (staminaSlider != null)
        {
            staminaSlider.minValue = 0f;
            staminaSlider.maxValue = player.maxStamina;
            staminaSlider.value = staShown;
        }
    }

    public void ForceRefresh()
    {
        if (player == null) return;

        float hp01 = (player.maxHP <= 0) ? 0f : Mathf.Clamp01((float)player.HP / player.maxHP);
        hp01Shown = hp01;

        if (hpFillRect != null)
        {
            if (hpWidthMax <= 0.01f) hpWidthMax = hpFillRect.rect.width;
            hpFillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, hpWidthMax * hp01Shown);
        }

        staShown = Mathf.Clamp(player.stamina, 0f, player.maxStamina);

        if (staminaSlider != null)
        {
            staminaSlider.minValue = 0f;
            staminaSlider.maxValue = player.maxStamina;
            staminaSlider.value = staShown;
        }
    }
}
