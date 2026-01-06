using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 胜利触发物：玩家接触后跳转到GameWin场景
/// </summary>
public class WinTrigger : MonoBehaviour
{
    // 胜利场景名（已按你的要求设为GameWin）
    public string winSceneName = "GameWin";

    // 玩家接触时触发
    private void OnTriggerEnter(Collider other)
    {
        // 只响应玩家（玩家Tag必须设为Player）
        if (other.CompareTag("Player"))
        {
            // 锁定鼠标（可选，和你角色脚本保持一致）
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // 跳转到胜利场景
            SceneManager.LoadScene(winSceneName);
        }
    }
}