using UnityEngine;
using Mirror;

public class GameStartButton : MonoBehaviour
{
    // 在 NetworkManager 组件中设置好 Online Scene 为 "GameScene"
    public void OnSinglePlayerStart()
    {
        if (NetworkManager.singleton != null)
        {
            Debug.Log("正在以单人 Host 模式启动游戏...");
            // 启动 Host 模式：自己既是服务器也是客户端
            NetworkManager.singleton.StartHost();
        }
        else
        {
            Debug.LogError("找不到 NetworkManager！请确保当前场景有一个 NetworkManager 物体。");
        }
    }

    // 重启按钮逻辑
    public void OnRestartClicked()
    {
        if (NetworkManager.singleton != null)
        {
            Debug.Log("停止当前游戏并返回离线场景...");
            // 停止 Host 会自动切回到 NetworkManager 里的 Offline Scene
            NetworkManager.singleton.StopHost();
        }
    }
}