using UnityEngine;
using UnityEngine.UI;

public class GameQuitButton : MonoBehaviour
{
    // 可以直接拖入退出按钮，也可以通过代码查找
    [Header("按钮引用")]
    public Button quitButton;

    void Awake()
    {
        // 自动获取按钮组件（如果没拖入）
        if (quitButton == null)
        {
            quitButton = GetComponent<Button>();
        }

        // 绑定按钮点击事件
        if (quitButton != null)
        {
            quitButton.onClick.AddListener(OnQuitButtonClick);
        }
    }

    // 按钮点击时执行的退出逻辑
    public void OnQuitButtonClick()
    {
        // 编辑器中：停止游戏运行
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // 打包后：退出应用程序
        Application.Quit();
#endif
    }
}