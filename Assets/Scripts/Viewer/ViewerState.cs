using System;
using UnityEngine;

public static class ViewerState
{
    static private string sceneName = null;
    static private string sceneDescription = null;

    // Event invoked when either scene name or description changes
    public static event Action SceneUpdated;

    public static string SceneName
    {
        get
        {
            return sceneName;
        }
    }

    public static void SetSceneName(string name)
    {
        if (sceneName != name)
        {
            sceneName = name;
            SceneUpdated?.Invoke();
        }
    }

    public static string SceneDescription
    {
        get
        {
            return sceneDescription;
        }
    }

    public static void SetSceneDescription(string description)
    {
        if (sceneDescription != description)
        {
            sceneDescription = description;
            SceneUpdated?.Invoke();
        }
    }
}
