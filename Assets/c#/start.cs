using UnityEngine;
using UnityEngine.SceneManagement;

public class GameStartButton : MonoBehaviour
{
    public void OnStartGameClicked()
    {
        SceneManager.LoadScene("GameScene");
    }
}