using UnityEngine;

public static class ViewerState
{
    static private string sceneName = null;

    public static string SceneName
    {
        get
        {
            return sceneName;
        }
    }

    public static void SetSceneName(string name)
    {
        sceneName = name;
    }

    static private string sceneDescription = null;

    public static string SceneDescription
    {
        get
        {
            return sceneDescription;
        }
    }

    public static void SetSceneDescription(string description)
    {
        sceneDescription = description;
    }
}
