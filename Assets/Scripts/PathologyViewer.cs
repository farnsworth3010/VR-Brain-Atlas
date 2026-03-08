using UnityEngine;
using UnityEngine.UI;

public class PathologyViewer : MonoBehaviour
{
    public GameObject tumor;
    public Toggle tumorToggle;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (ViewerState.SceneName == "Мозг с опухолью")
        {
            tumor.SetActive(true);
            tumorToggle.transform.gameObject.SetActive(true);
        }
    }

    // Update is called once per frame
    void Update()
    {

    }
}
