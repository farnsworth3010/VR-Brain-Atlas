using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using Dummiesman;

public class ModelLoader : MonoBehaviour
{
  [Header("Assign in Inspector")]
  [SerializeField] private Transform container;
  [SerializeField] private ImportedLayersMenuController importedLayersMenuController;
  [SerializeField] private ModelStateController modelStateController;
  [SerializeField] private Material brainMaterial;
  [SerializeField] private Material tumorMaterial;

  [Header("Loading")]
  [SerializeField] private bool addBoxCollider = true;
  [SerializeField] private bool clearContainerBeforeLoad = true;

  private bool isLoading;
  private static readonly HashSet<string> AllowedModelNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
  {
    "arteria.obj",
    "tumor.obj",
    "brain.obj",
    "skull.obj"
  };

  private void Awake()
  {
    EnsureImportedLayersMenuController();
  }

  public void OpenAndLoadModel()
  {
    if (isLoading)
    {
      Debug.LogWarning("Model loading is already in progress.");
      return;
    }

    if (container == null)
    {
      Debug.LogError("ModelLoader: Container is not assigned.");
      return;
    }

    EnsureImportedLayersMenuController();

    string[] selectedPaths = OpenModelFileDialog();
    if (selectedPaths == null || selectedPaths.Length == 0)
    {
      return;
    }

    StartCoroutine(LoadModelsCoroutine(selectedPaths));
  }

  public void ClearContainer()
  {
    if (container == null)
    {
      Debug.LogError("ModelLoader: Container is not assigned.");
      return;
    }

    StartCoroutine(ClearContainerCoroutine());
  }

  private IEnumerator ClearContainerCoroutine()
  {
    if (container == null)
    {
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

    isLoading = true;

    EnsureImportedLayersMenuController();

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

  private void EnsureImportedLayersMenuController()
  {
    if (importedLayersMenuController != null)
    {
      return;
    }

    importedLayersMenuController = FindFirstObjectByType<ImportedLayersMenuController>();
    if (importedLayersMenuController == null)
    {
      Debug.LogWarning("ModelLoader: ImportedLayersMenuController is not assigned and was not found in scene.");
    }
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
    loadedObj.transform.localEulerAngles = new Vector3(-90f, 0f, 70f);
    loadedObj.transform.localPosition = new Vector3(3.216f, 3.2f, 0.403f);

    // Per-model overrides requested by user
    switch (fileBase.ToLowerInvariant())
    {
      case "brain":
        // Values from first screenshot: position (3.52, 3.26, 0.03), rotation (-90, 75.7, 0), scale 0.05
        loadedObj.transform.localPosition = new Vector3(3.52f, 3.26f, 0.03f);
        loadedObj.transform.localEulerAngles = new Vector3(-90f, 75.7f, 0f);
        loadedObj.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
        break;
      case "tumor":
        // Values from second screenshot: position (1.859, 4.753, 2.325), rotation (-22.1, 149.6, -48.4), scale 0.01
        loadedObj.transform.localPosition = new Vector3(1.859f, 4.753f, 2.325f);
        loadedObj.transform.localEulerAngles = new Vector3(-22.1f, 149.6f, -48.4f);
        loadedObj.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
        break;
    }

    // Select material based on file base-name (allow tumor-specific material)
    string fileBaseLower = fileBase.ToLowerInvariant();
    Material materialToApply = null;
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

    if (materialToApply == null)
    {
      Debug.LogWarning("Material not assigned in inspector. Attempting to load from resources.");
      if (fileBaseLower == "tumor")
      {
        materialToApply = Resources.Load<Material>("Materials/Tumor/Tumor") ?? Resources.Load<Material>("Materials/Brain/Brain");
      }
      else
      {
        materialToApply = Resources.Load<Material>("Materials/Brain/Brain");
      }

      if (materialToApply == null)
      {
        Debug.LogWarning("Could not load material from Resources. Using fallback material and trying multiple shaders.");
        Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Unlit/Texture");
        if (shader == null)
        {
          Debug.LogError("No compatible shader found on target platform. Creating material with default shader settings.");
          materialToApply = new Material(Shader.Find("Standard"));
        }
        else
        {
          materialToApply = new Material(shader);
        }
      }
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

  private static class NativeFileDialog
  {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct OpenFileName
    {
      public int structSize;
      public IntPtr dlgOwner;
      public IntPtr instance;
      [MarshalAs(UnmanagedType.LPTStr)] public string filter;
      [MarshalAs(UnmanagedType.LPTStr)] public string customFilter;
      public int maxCustFilter;
      public int filterIndex;
      public IntPtr file;
      public int maxFile;
      [MarshalAs(UnmanagedType.LPTStr)] public string fileTitle;
      public int maxFileTitle;
      [MarshalAs(UnmanagedType.LPTStr)] public string initialDir;
      [MarshalAs(UnmanagedType.LPTStr)] public string title;
      public int flags;
      public short fileOffset;
      public short fileExtension;
      [MarshalAs(UnmanagedType.LPTStr)] public string defExt;
      public IntPtr custData;
      public IntPtr hook;
      [MarshalAs(UnmanagedType.LPTStr)] public string templateName;
      public IntPtr reservedPtr;
      public int reservedInt;
      public int flagsEx;
    }

    [DllImport("Comdlg32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GetOpenFileName(ref OpenFileName ofn);

    private const int OFN_FILEMUSTEXIST = 0x00001000;
    private const int OFN_PATHMUSTEXIST = 0x00000800;
    private const int OFN_NOCHANGEDIR = 0x00000008;
    private const int OFN_ALLOWMULTISELECT = 0x00000200;
    private const int OFN_EXPLORER = 0x00080000;

    public static string[] OpenFileDialog(string filter, bool allowMultiSelect)
    {
      int flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR;
      if (allowMultiSelect)
      {
        flags |= OFN_ALLOWMULTISELECT | OFN_EXPLORER;
      }

      IntPtr fileBufferPtr = IntPtr.Zero;

      try
      {
        const int maxFileChars = 16384;
        fileBufferPtr = Marshal.AllocHGlobal(maxFileChars * sizeof(char));
        for (int i = 0; i < maxFileChars; i++)
        {
          Marshal.WriteInt16(fileBufferPtr, i * sizeof(char), 0);
        }

        OpenFileName ofn = new OpenFileName
        {
          structSize = Marshal.SizeOf(typeof(OpenFileName)),
          filter = filter,
          file = fileBufferPtr,
          maxFile = maxFileChars,
          fileTitle = new string('\0', 256),
          maxFileTitle = 256,
          initialDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
          title = "Select model file",
          flags = flags
        };

        bool result = GetOpenFileName(ref ofn);
        if (!result)
        {
          return Array.Empty<string>();
        }

        string rawBuffer = Marshal.PtrToStringAuto(fileBufferPtr, maxFileChars) ?? string.Empty;
        string[] rawParts = rawBuffer
          .Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries)
          .Where(part => !string.IsNullOrWhiteSpace(part))
          .ToArray();

        if (rawParts.Length == 0)
        {
          return Array.Empty<string>();
        }

        if (rawParts.Length == 1)
        {
          return new[] { rawParts[0] };
        }

        string directory = rawParts[0];
        string[] files = new string[rawParts.Length - 1];
        for (int i = 1; i < rawParts.Length; i++)
        {
          files[i - 1] = Path.Combine(directory, rawParts[i]);
        }

        return files;
      }
      finally
      {
        if (fileBufferPtr != IntPtr.Zero)
        {
          Marshal.FreeHGlobal(fileBufferPtr);
        }
      }
    }
#endif
  }
}