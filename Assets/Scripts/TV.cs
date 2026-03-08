using TMPro;
using UnityEngine;

public class TV : MonoBehaviour
{
    public TMP_Text sceneName;
    public TMP_Text sceneDescription;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        sceneName.text = ViewerState.SceneName;
        sceneDescription.text = ViewerState.SceneDescription;
    }

    // Update is called once per frame
    void Update()
    {

    }
}
