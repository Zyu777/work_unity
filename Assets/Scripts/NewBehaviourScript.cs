using UnityEngine;
using Mirror;
using UnityEngine.UI;

public class CustomNetworkUI : MonoBehaviour
{
    public Button hostButton;
    public Button clientButton;
    public GameObject menuPanel; // 整个菜单面板

    void Start()
    {
        // 绑定按钮逻辑
        hostButton.onClick.AddListener(StartHost);
        clientButton.onClick.AddListener(StartClient);
    }

    void Update()
    {
        // 如果已经连接成功，就隐藏这个菜单，不挡镜头
        if (NetworkServer.active || NetworkClient.active)
        {
            if (menuPanel.activeSelf) menuPanel.SetActive(false);
        }
        else
        {
            if (!menuPanel.activeSelf) menuPanel.SetActive(true);
        }
    }

    void StartHost()
    {
        NetworkManager.singleton.StartHost();
    }

    void StartClient()
    {
        NetworkManager.singleton.StartClient();
    }
}