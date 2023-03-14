using UnityEngine;
using UnityEngine.SceneManagement;

public class ResetButton : MonoBehaviour
{

    public void OnClickStartButton()
    {
        SceneManager.LoadScene("Scenes/dc2_demo");
    }

}