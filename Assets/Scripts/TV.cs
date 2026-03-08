using TMPro;
using UnityEngine;

public class TV : MonoBehaviour
{
    public TMP_Text sceneName;
    public TMP_Text sceneDescription;

    void Start()
    {
        sceneName.text = ViewerState.SceneName;
        sceneDescription.text = ViewerState.SceneDescription;
    }
}
