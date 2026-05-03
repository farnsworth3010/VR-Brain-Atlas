using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PathologyViewer : MonoBehaviour
{
    public GameObject tumor;
    public ModelLoader modelLoader;

    void Start()
    {
        if (ViewerState.SceneName == "Мозг с опухолью")
        {
            tumor.SetActive(true);
        }

        if (ViewerState.SceneName == "Импортированная модель")
        {
            Debug.Log("PathologyViewer: Detected scene 'Импортированная модель'. Scheduling model load after 100ms.");
            StartCoroutine(LoadModelWithDelay(0.1f));
        }
    }

    IEnumerator LoadModelWithDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        modelLoader.OpenAndLoadModel();
    }
}
