using TMPro;
using UnityEngine;

public class TV : MonoBehaviour
{
    public TMP_Text sceneName;
    public TMP_Text sceneDescription;

    void OnEnable()
    {
        ViewerState.SceneUpdated += UpdateTexts;
        UpdateTexts();
    }

    void OnDisable()
    {
        ViewerState.SceneUpdated -= UpdateTexts;
    }

    private void UpdateTexts()
    {
        if (sceneName != null)
        {
            sceneName.text = ViewerState.SceneName ?? string.Empty;
        }

        if (sceneDescription != null)
        {
            sceneDescription.text = ViewerState.SceneDescription ?? string.Empty;
        }
    }
}
