using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Dummiesman;
using Newtonsoft.Json;

public class ModelLoader : MonoBehaviour
{
  public Transform container;
  public ImportedLayersMenuController importedLayersMenuController;
  public ModelStateController modelStateController;
  public Material brainMaterial;
  public Material tumorMaterial;

  private bool addBoxCollider = true;
  private bool clearContainerBeforeLoad = true;

  private bool isLoading;
  private Dictionary<string, ModelInfo> modelDataByName = new Dictionary<string, ModelInfo>(System.StringComparer.OrdinalIgnoreCase);
  private static readonly HashSet<string> AllowedModelNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
  {
    "arteria.obj",
    "tumor.obj",
    "brain.obj",
    "skull.obj"
  };

  // Preferred order for automatic loading from the default folder
  private static readonly string[] PreferredModelOrder = new[]
  {
    "arteria.obj",
    "tumor.obj",
    "brain.obj",
    "skull.obj"
  };

  private void Awake()
  {
    // Ensure container exists. Prefer an existing child named ModelContainer/Container/Models,
    // otherwise create a new child transform to hold loaded models.
    if (container == null)
    {
      Transform found = transform.Find("ModelContainer") ?? transform.Find("Container") ?? transform.Find("Models");
      if (found != null)
      {
        container = found;
      }
      else
      {
        GameObject go = new GameObject("ModelContainer");
        go.transform.SetParent(transform, false);
        container = go.transform;
        Debug.Log("ModelLoader: created ModelContainer child because none was assigned.");
      }
    }

    // Try to auto-wire optional collaborators if they were not assigned in the inspector
    if (importedLayersMenuController == null)
    {
      importedLayersMenuController = GetComponentInChildren<ImportedLayersMenuController>();
    }

    if (modelStateController == null)
    {
      modelStateController = GetComponent<ModelStateController>() ?? FindFirstObjectByType<ModelStateController>();
    }
  }

  public void OpenAndLoadModel()
  {
    if (isLoading)
    {
      Debug.LogWarning("Model loading is already in progress.");
      return;
    }

    // Try to auto-load models from the default Documents/VR Brain Atlas folder first
    try
    {
      string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

      if (!string.IsNullOrEmpty(docs))
      {
        string defaultDir = Path.Combine(docs, "VR Brain Atlas");
        if (Directory.Exists(defaultDir))
        {
          List<string> found = new List<string>();
          foreach (string name in PreferredModelOrder)
          {
            string candidate = Path.Combine(defaultDir, name);
            if (File.Exists(candidate))
            {
              found.Add(candidate);
            }
          }

          if (found.Count > 0)
          {
            StartCoroutine(LoadModelsCoroutine(found));
            return;
          }
        }
      }
    }
    catch (Exception ex)
    {
      Debug.LogException(ex);
    }

    string[] selectedPaths = OpenModelFileDialog();
    if (selectedPaths == null || selectedPaths.Length == 0)
    {
      return;
    }

    StartCoroutine(LoadModelsCoroutine(selectedPaths));
  }

  public void ClearContainer()
  {
    StartCoroutine(ClearContainerCoroutine());
  }

  private IEnumerator ClearContainerCoroutine()
  {
    if (container == null)
    {
      Debug.LogWarning("ModelLoader: container is null in ClearContainerCoroutine. Nothing to clear.");
      yield break;
    }

    int childCount = container.childCount;

    if (childCount == 0)
    {
      yield break;
    }

    Transform[] children = new Transform[childCount];
    for (int i = 0; i < childCount; i++)
    {
      children[i] = container.GetChild(i);
    }

    for (int i = children.Length - 1; i >= 0; i--)
    {
      if (children[i] == null)
      {
        continue;
      }

      GameObject child = children[i].gameObject;

#if UNITY_EDITOR
      if (!Application.isPlaying)
      {
        DestroyImmediate(child);
        continue;
      }
#endif

      child.transform.SetParent(null);
      Destroy(child);
    }

    yield return null;
  }

  private IEnumerator LoadModelsCoroutine(IReadOnlyList<string> paths)
  {
    if (paths == null || paths.Count == 0)
    {
      yield break;
    }

    List<string> validPaths = new List<string>();
    for (int i = 0; i < paths.Count; i++)
    {
      string path = paths[i];
      if (string.IsNullOrWhiteSpace(path))
      {
        continue;
      }

      string fileName = Path.GetFileName(path);
      if (!AllowedModelNames.Contains(fileName))
      {
        Debug.LogError($"Unsupported model name: '{fileName}'. Allowed: arteria.obj, tumor.obj, brain.obj, skull.obj");
        continue;
      }

      string extension = Path.GetExtension(path).ToLowerInvariant();
      if (extension != ".obj")
      {
        Debug.LogError($"Unsupported file format: {extension}. Supported: .obj");
        continue;
      }

      validPaths.Add(path);
    }

    if (validPaths.Count == 0)
    {
      yield break;
    }

    // Attempt to load model_data.json from Documents/VR Brain Atlas
    try
    {
      string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
      if (!string.IsNullOrEmpty(docs))
      {
        string dataPath = Path.Combine(docs, "VR Brain Atlas", "model_data.json");
        if (File.Exists(dataPath))
        {
          try
          {
            string json = File.ReadAllText(dataPath);
            ModelDataRoot root = JsonConvert.DeserializeObject<ModelDataRoot>(json);
            if (root?.models != null)
            {
              modelDataByName.Clear();
              foreach (var kv in root.models)
              {
                if (kv.Key != null && kv.Value != null)
                {
                  modelDataByName[kv.Key.ToLowerInvariant()] = kv.Value;
                }
              }
              Debug.Log($"ModelLoader: loaded model_data.json with {modelDataByName.Count} entries from '{dataPath}'.");
            }
          }
          catch (System.Exception ex)
          {
            Debug.LogException(ex);
          }
        }
      }
    }
    catch (System.Exception ex)
    {
      Debug.LogException(ex);
    }

    isLoading = true;

    if (importedLayersMenuController != null)
    {
      importedLayersMenuController.ClearList();
    }

    bool loadingSingleModel = validPaths.Count == 1;
    bool shouldClearContainer = loadingSingleModel || clearContainerBeforeLoad;

    if (shouldClearContainer)
    {
      yield return ClearContainerCoroutine();
    }

    bool allLoaded = true;
    for (int i = 0; i < validPaths.Count; i++)
    {
      string path = validPaths[i];
      bool success = false;
      yield return TryLoadObjCoroutine(path, loaded => success = loaded);
      if (!success)
      {
        allLoaded = false;
        Debug.LogError($"Failed to load model from path: {path}");
      }
    }

    if (importedLayersMenuController != null)
    {
      importedLayersMenuController.RebuildList();
    }

    isLoading = false;

    if (!allLoaded)
    {
      Debug.LogWarning("Some selected models were not loaded.");
    }
    else
    {
      ViewerState.SetSceneName("Импортированная модель");
      ViewerState.SetSceneDescription("На этой сцене показана модель, импортированная пользователем. Вы можете включать и выключать видимость отдельных слоёв модели через меню слоёв. Для взаимодействия с моделью используйте контроллеры или геймпад.");
      modelStateController.ResetModelTransform();
    }

    Cursor.lockState = CursorLockMode.Locked;
    Cursor.visible = false;
  }

  private IEnumerator TryLoadObjCoroutine(string path, Action<bool> onComplete)
  {
    GameObject loadedObj = null;

    try
    {
      OBJLoader loader = new OBJLoader();
      loadedObj = loader.Load(path);
    }
    catch (Exception e)
    {
      Debug.LogException(e);
      onComplete?.Invoke(false);
      yield break;
    }

    if (loadedObj == null)
    {
      Debug.LogError("Failed to load OBJ file: returned object is null");
      onComplete?.Invoke(false);
      yield break;
    }

    // Keep original hierarchy; do not combine meshes into a single Mesh
    MeshRenderer[] childMeshRenderers = loadedObj.GetComponentsInChildren<MeshRenderer>();

    string fileBase = Path.GetFileNameWithoutExtension(path);

    // Map certain model base-names to localized display names
    switch (fileBase.ToLowerInvariant())
    {
      case "brain":
        loadedObj.name = "Мозг";
        break;
      case "tumor":
        loadedObj.name = "Опухоль";
        break;
      default:
        loadedObj.name = fileBase;
        break;
    }

    // Set the loaded object as a child of the container
    loadedObj.transform.SetParent(container, false);

    // Default transform (if no special per-model override)
    loadedObj.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
    // loadedObj.transform.localEulerAngles = new Vector3(-90f, 0f, 70f);
    // loadedObj.transform.localPosition = new Vector3(3.216f, 3.2f, 0.403f);

    // Per-model overrides requested by user
    switch (fileBase.ToLowerInvariant())
    {
      case "brain":
        // gets done below
        loadedObj.transform.localEulerAngles = new Vector3(0f, 180f, 0f);
        loadedObj.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
        break;
      case "tumor":
        // gets done below
        // loadedObj.transform.localPosition = new Vector3(1.786f, 4.478f, 2.152f);
        loadedObj.transform.localEulerAngles = new Vector3(0f, 180f, 0f);
        loadedObj.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
        break;
    }

    // Apply position/scale from model_data.json (if available) using the same scale convention
    try
    {
      string key = fileBase.ToLowerInvariant();
      if (modelDataByName != null && modelDataByName.TryGetValue(key, out ModelInfo info) && info != null)
      {
        if (info.position != null && info.position.Length >= 3)
        {
          // JSON positions are in millimeters; convert to meters and apply the current localScale factor
          float px = (float)info.position[0];
          float py = (float)info.position[1];
          float pz = (float)info.position[2];
          Vector3 posMeters = new Vector3(px, py, pz) * 0.05f;
          // Apply same numeric factor as localScale to keep relative sizing consistent
          float scaleFactor = loadedObj.transform.localScale.x;
          // loadedObj.transform.localPosition = posMeters * scaleFactor;
          loadedObj.transform.localPosition = posMeters;
          Vector3 appliedPosition = loadedObj.transform.localPosition;
          Debug.Log($"ModelLoader: '{fileBase}' raw JSON position mm = ({px:F6}, {py:F6}, {pz:F6}), meters = ({posMeters.x:F6}, {posMeters.y:F6}, {posMeters.z:F6}), scaleFactor = {scaleFactor:F6}, applied localPosition = ({appliedPosition.x:F6}, {appliedPosition.y:F6}, {appliedPosition.z:F6})");
          Debug.Log($"ModelLoader: applied position from model_data.json for '{fileBase}': ({appliedPosition.x:F6}, {appliedPosition.y:F6}, {appliedPosition.z:F6})");
        }
      }
    }
    catch (System.Exception ex)
    {
      Debug.LogException(ex);
    }

    // Select material based on file base-name
    string fileBaseLower = fileBase.ToLowerInvariant();
    Material materialToApply;

    if (fileBaseLower == "tumor")
    {
      materialToApply = tumorMaterial ?? brainMaterial;
    }
    else if (fileBaseLower == "brain")
    {
      materialToApply = brainMaterial;
    }
    else
    {
      materialToApply = brainMaterial;
    }

    MeshRenderer[] renderers = loadedObj.GetComponentsInChildren<MeshRenderer>();
    Debug.Log($"Found {renderers.Length} renderers to apply material to for '{fileBase}' at path '{path}'.");
    foreach (MeshRenderer renderer in renderers)
    {
      renderer.material = materialToApply;
      Debug.Log($"Applied material to renderer: {renderer.gameObject.name} (enabled={renderer.enabled}, bounds={renderer.bounds})");
    }

    // Ensure root is active and visible
    if (!loadedObj.activeInHierarchy)
    {
      loadedObj.SetActive(true);
      Debug.Log("Activated loaded object root.");
    }

    // Add box collider if needed and none exists
    if (addBoxCollider && loadedObj.GetComponentInChildren<Collider>() == null)
    {
      loadedObj.AddComponent<BoxCollider>();
    }

    yield return null;

    onComplete?.Invoke(true);
  }

  private string[] OpenModelFileDialog()
  {
    Cursor.lockState = CursorLockMode.None;
    Cursor.visible = true;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    return NativeFileDialog.OpenFileDialog("3D Models\0*.obj\0All files\0*.*\0\0", true);
#elif UNITY_EDITOR
		string singlePath = UnityEditor.EditorUtility.OpenFilePanel("Select 3D model", string.Empty, "obj");
		return string.IsNullOrEmpty(singlePath) ? Array.Empty<string>() : new[] { singlePath };
#else
		Debug.LogError("System file dialog is only implemented for Windows in this script.");
		return Array.Empty<string>();
#endif
  }

}