using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class PlayerHealthHUD_Width : MonoBehaviour
{
    public PlayerController player;

    [Header("Hierarchy Paths")]
    public string hpFillPath = "health/Fill Area/Fill";
    public string staminaSliderPath = "Slider";

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
        // 查找 UI 组件引用
        Transform hpTf = transform.Find(hpFillPath);
        if (hpTf != null) hpFillRect = hpTf.GetComponent<RectTransform>();

        Transform stTf = transform.Find(staminaSliderPath);
        if (stTf != null) staminaSlider = stTf.GetComponent<Slider>();
    }

    void Start()
    {
        if (hpFillRect != null)
            hpWidthMax = hpFillRect.rect.width;

        // 尝试初始绑定
        FindLocalPlayer();
    }

    void Update()
    {
        // --- 核心修改：如果 player 丢失（或单机刚加载场景还没找到），持续寻找 ---
        if (player == null)
        {
            FindLocalPlayer();
            return; // 还没找到人之前，不执行后续逻辑，防止报错
        }

        // -------- HP 宽度缩放逻辑 --------
        // 确保不会除以 0
        float hp01Target = (player.maxHP <= 0) ? 0f : (float)player.HP / player.maxHP;
        hp01Target = Mathf.Clamp01(hp01Target);

        hp01Shown = smooth ? Mathf.Lerp(hp01Shown, hp01Target, Time.deltaTime * smoothSpeed) : hp01Target;

        if (hpFillRect != null && hpWidthMax > 0.01f)
        {
            float newW = hpWidthMax * hp01Shown;
            hpFillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, newW);
        }

        // -------- Stamina Slider 逻辑 --------
        float staTarget = Mathf.Clamp(player.stamina, 0f, player.maxStamina);
        staShown = smooth ? Mathf.Lerp(staShown, staTarget, Time.deltaTime * smoothSpeed) : staTarget;

        if (staminaSlider != null)
        {
            staminaSlider.minValue = 0f;
            staminaSlider.maxValue = player.maxStamina;
            staminaSlider.value = staShown;
        }
    }

    void FindLocalPlayer()
    {
        PlayerController[] allPlayers = FindObjectsOfType<PlayerController>();

        // 场景中还没生成任何玩家时直接跳出
        if (allPlayers.Length == 0) return;

        // 情况 1：联机模式下 (Mirror 正在运行)
        if (NetworkClient.active)
        {
            foreach (var p in allPlayers)
            {
                NetworkIdentity ni = p.GetComponent<NetworkIdentity>();
                // 只有本地控制的那个人才是 UI 的目标
                if (ni != null && ni.isLocalPlayer)
                {
                    player = p;
                    Debug.Log("<color=green>联机模式：UI 成功绑定本地玩家！</color>");
                    ForceRefresh();
                    break;
                }
            }
        }
        // 情况 2：单机模式下 (Mirror 未启动)
        else
        {
            // 单机场景里通常只有一个玩家，直接取第一个
            player = allPlayers[0];
            Debug.Log("<color=cyan>单机模式：UI 已绑定场景预放玩家！</color>");
            ForceRefresh();
        }
    }

    public void ForceRefresh()
    {
        if (player == null) return;

        // 强制初始化显示数值，防止 lerp 从 0 开始慢慢滑动
        float hp01 = (player.maxHP <= 0) ? 0f : Mathf.Clamp01((float)player.HP / player.maxHP);
        hp01Shown = hp01;

        if (hpFillRect != null)
        {
            if (hpWidthMax <= 0.01f) hpWidthMax = hpFillRect.rect.width;
            hpFillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, hpWidthMax * hp01Shown);
        }

        staShown = player.stamina;
    }
}