using UnityEngine;
using UnityEngine.UI;

public class PathologyViewer : MonoBehaviour
{
    public GameObject tumor;

    void Start()
    {
        if (ViewerState.SceneName == "Мозг с опухолью")
        {
            tumor.SetActive(true);
        }
    }
}
