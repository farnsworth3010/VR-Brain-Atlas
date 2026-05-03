using UnityEngine;

/// <summary>
/// Sets a global target frame rate on startup. Disables vSync so the target is honored.
/// This runs before any scene loads, no GameObject required.
/// </summary>
public static class FrameRateLimiter
{
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
  private static void OnBeforeSceneLoad()
  {
    QualitySettings.vSyncCount = 1; // disable vSync so targetFrameRate is used
    Application.targetFrameRate = 60;

#if UNITY_EDITOR
    Debug.Log($"FrameRateLimiter: vSyncCount={QualitySettings.vSyncCount}, targetFrameRate={Application.targetFrameRate}");
#endif
  }
}
