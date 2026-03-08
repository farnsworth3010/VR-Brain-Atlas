using UnityEngine;
using UnityEngine.UI;

public class PathologyViewer : MonoBehaviour
{
    public GameObject tumor;
    public Toggle tumorToggle;

    void Start()
    {
        if (ViewerState.SceneName == "Мозг с опухолью")
        {
            tumor.SetActive(true);
            tumorToggle.transform.gameObject.SetActive(true);
        }
    }
}
