using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

public class ModelLoader : MonoBehaviour
{
  [Header("Assign in Inspector")]
  [SerializeField] private Transform container;
  [SerializeField] private ImportedLayersMenuController importedLayersMenuController;

  [Header("Loading")]
  [SerializeField] private bool addBoxCollider = true;
  [SerializeField] private bool clearContainerBeforeLoad = true;

  private bool isLoading;

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

    string selectedPath = OpenModelFileDialog();
    if (string.IsNullOrEmpty(selectedPath))
    {
      return;
    }

    StartCoroutine(LoadModelCoroutine(selectedPath));
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

  private IEnumerator LoadModelCoroutine(string path)
  {
    isLoading = true;

    EnsureImportedLayersMenuController();

    if (importedLayersMenuController != null)
    {
      importedLayersMenuController.ClearList();
    }

    if (clearContainerBeforeLoad)
    {
      yield return ClearContainerCoroutine();
    }

    string extension = Path.GetExtension(path).ToLowerInvariant();
    bool success = false;

    if (extension == ".glb" || extension == ".gltf")
    {
      yield return TryLoadGltfCoroutine(path, loaded => success = loaded);
    }
    else if (extension == ".bundle" || extension == ".assetbundle")
    {
      yield return LoadAssetBundleCoroutine(path, loaded => success = loaded);
    }
    else
    {
      Debug.LogError($"Unsupported file format: {extension}. Supported: .glb, .gltf, .bundle, .assetbundle");
    }

    isLoading = false;

    if (!success)
    {
      Debug.LogError($"Failed to load model from path: {path}");
    }
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

  private IEnumerator TryLoadGltfCoroutine(string path, Action<bool> onComplete)
  {
    Type gltfImportType = FindGltfImportType();
    if (gltfImportType == null)
    {
      Debug.LogError("glTFast package is not installed or not compiled. Install/resolve glTFast to load .glb/.gltf files at runtime.");
      onComplete?.Invoke(false);
      yield break;
    }

    object gltfImport = CreateGltfImportInstance(gltfImportType);
    if (gltfImport == null)
    {
      Debug.LogError("Could not create GLTFast.GltfImport instance.");
      onComplete?.Invoke(false);
      yield break;
    }

    Task<bool> loadTask = InvokeLoadTask(gltfImport, gltfImportType, path);
    if (loadTask == null)
    {
      Debug.LogError("Could not find compatible glTFast load method (LoadFile/Load).");
      onComplete?.Invoke(false);
      yield break;
    }

    while (!loadTask.IsCompleted)
    {
      yield return null;
    }

    if (loadTask.IsFaulted || !loadTask.Result)
    {
      if (loadTask.Exception != null)
      {
        Debug.LogException(loadTask.Exception);
      }

      onComplete?.Invoke(false);
      yield break;
    }

    int childCountBeforeImport = container.childCount;

    Task<bool> instantiateTask = InvokeInstantiateTask(gltfImport, gltfImportType, container);
    if (instantiateTask == null)
    {
      Debug.LogError("Could not find compatible glTFast instantiate method.");
      onComplete?.Invoke(false);
      yield break;
    }

    while (!instantiateTask.IsCompleted)
    {
      yield return null;
    }

    bool success = !instantiateTask.IsFaulted && instantiateTask.Result;
    if (!success)
    {
      if (instantiateTask.Exception != null)
      {
        Debug.LogException(instantiateTask.Exception);
      }

      onComplete?.Invoke(false);
      yield break;
    }

    GameObject marker = new GameObject(Path.GetFileNameWithoutExtension(path));
    marker.transform.SetParent(container, false);

    for (int i = childCountBeforeImport; i < container.childCount - 1; i++)
    {
      Transform importedRoot = container.GetChild(i);
      importedRoot.SetParent(marker.transform, true);
    }

    if (addBoxCollider && marker.GetComponentInChildren<Collider>() == null)
    {
      marker.AddComponent<BoxCollider>();
    }

    if (importedLayersMenuController != null)
    {
      importedLayersMenuController.RebuildFromImportedRoot(marker.transform);
    }

    onComplete?.Invoke(true);
  }

  private Type FindGltfImportType()
  {
    Type importType = Type.GetType("GLTFast.GltfImport, glTFast");
    if (importType != null)
    {
      return importType;
    }

    Assembly[] loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
    foreach (Assembly assembly in loadedAssemblies)
    {
      importType = assembly.GetType("GLTFast.GltfImport");
      if (importType != null)
      {
        return importType;
      }
    }

    return null;
  }

  private object CreateGltfImportInstance(Type gltfImportType)
  {
    ConstructorInfo[] constructors = gltfImportType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
    if (constructors == null || constructors.Length == 0)
    {
      return null;
    }

    ConstructorInfo selectedConstructor = constructors[0];
    foreach (ConstructorInfo constructor in constructors)
    {
      if (constructor.GetParameters().Length > selectedConstructor.GetParameters().Length)
      {
        selectedConstructor = constructor;
      }
    }

    ParameterInfo[] parameters = selectedConstructor.GetParameters();
    object[] args = new object[parameters.Length];
    for (int i = 0; i < parameters.Length; i++)
    {
      if (parameters[i].HasDefaultValue)
      {
        args[i] = parameters[i].DefaultValue;
      }
      else if (parameters[i].ParameterType.IsValueType)
      {
        args[i] = Activator.CreateInstance(parameters[i].ParameterType);
      }
      else
      {
        args[i] = null;
      }
    }

    return selectedConstructor.Invoke(args);
  }

  private Task<bool> InvokeLoadTask(object gltfImport, Type gltfImportType, string path)
  {
    MethodInfo[] methods = gltfImportType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
    MethodInfo selectedMethod = null;

    foreach (MethodInfo method in methods)
    {
      if ((method.Name != "LoadFile" && method.Name != "Load") || method.ReturnType != typeof(Task<bool>))
      {
        continue;
      }

      ParameterInfo[] parameters = method.GetParameters();
      if (parameters.Length == 0 || parameters[0].ParameterType != typeof(string))
      {
        continue;
      }

      if (selectedMethod == null || method.Name == "LoadFile")
      {
        selectedMethod = method;
      }
    }

    if (selectedMethod == null)
    {
      return null;
    }

    object[] args = BuildMethodArguments(selectedMethod, path);
    return selectedMethod.Invoke(gltfImport, args) as Task<bool>;
  }

  private Task<bool> InvokeInstantiateTask(object gltfImport, Type gltfImportType, Transform parent)
  {
    MethodInfo[] methods = gltfImportType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
    foreach (MethodInfo method in methods)
    {
      if (method.Name != "InstantiateMainSceneAsync" || method.ReturnType != typeof(Task<bool>))
      {
        continue;
      }

      ParameterInfo[] parameters = method.GetParameters();
      if (parameters.Length == 0 || parameters[0].ParameterType != typeof(Transform))
      {
        continue;
      }

      object[] args = BuildMethodArguments(method, parent);
      return method.Invoke(gltfImport, args) as Task<bool>;
    }

    MethodInfo syncMethod = gltfImportType.GetMethod("InstantiateMainScene", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(Transform) }, null);
    if (syncMethod != null)
    {
      bool result = false;
      try
      {
        object invokeResult = syncMethod.Invoke(gltfImport, new object[] { parent });
        if (invokeResult is bool typedResult)
        {
          result = typedResult;
        }
      }
      catch (Exception exception)
      {
        Debug.LogException(exception);
      }

      return Task.FromResult(result);
    }

    return null;
  }

  private object[] BuildMethodArguments(MethodInfo method, object firstArgument)
  {
    ParameterInfo[] parameters = method.GetParameters();
    if (parameters.Length == 0)
    {
      return Array.Empty<object>();
    }

    object[] args = new object[parameters.Length];
    args[0] = firstArgument;

    for (int i = 1; i < parameters.Length; i++)
    {
      if (parameters[i].HasDefaultValue)
      {
        args[i] = parameters[i].DefaultValue;
      }
      else if (parameters[i].ParameterType.IsValueType)
      {
        args[i] = Activator.CreateInstance(parameters[i].ParameterType);
      }
      else
      {
        args[i] = null;
      }
    }

    return args;
  }

  private IEnumerator LoadAssetBundleCoroutine(string path, Action<bool> onComplete)
  {
    AssetBundleCreateRequest bundleRequest = AssetBundle.LoadFromFileAsync(path);
    yield return bundleRequest;

    AssetBundle bundle = bundleRequest.assetBundle;
    if (bundle == null)
    {
      onComplete?.Invoke(false);
      yield break;
    }

    string[] assetNames = bundle.GetAllAssetNames();
    foreach (string assetName in assetNames)
    {
      AssetBundleRequest loadRequest = bundle.LoadAssetAsync<GameObject>(assetName);
      yield return loadRequest;

      GameObject prefab = loadRequest.asset as GameObject;
      if (prefab == null)
      {
        continue;
      }

      GameObject instance = Instantiate(prefab, container);
      instance.name = Path.GetFileNameWithoutExtension(path);

      if (addBoxCollider && instance.GetComponentInChildren<Collider>() == null)
      {
        instance.AddComponent<BoxCollider>();
      }

      if (importedLayersMenuController != null)
      {
        importedLayersMenuController.RebuildFromImportedRoot(instance.transform);
      }

      bundle.Unload(false);
      onComplete?.Invoke(true);
      yield break;
    }

    bundle.Unload(false);
    onComplete?.Invoke(false);
  }

  private string OpenModelFileDialog()
  {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    return NativeFileDialog.OpenFileDialog("3D Models\0*.glb;*.gltf;*.bundle;*.assetbundle\0All files\0*.*\0\0");
#elif UNITY_EDITOR
		return UnityEditor.EditorUtility.OpenFilePanel("Select 3D model", string.Empty, "glb,gltf,bundle,assetbundle");
#else
		Debug.LogError("System file dialog is only implemented for Windows in this script.");
		return string.Empty;
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
      [MarshalAs(UnmanagedType.LPTStr)] public string file;
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

    public static string OpenFileDialog(string filter)
    {
      OpenFileName ofn = new OpenFileName
      {
        structSize = Marshal.SizeOf(typeof(OpenFileName)),
        filter = filter,
        file = new string('\0', 4096),
        maxFile = 4096,
        fileTitle = new string('\0', 256),
        maxFileTitle = 256,
        initialDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        title = "Select model file",
        flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR
      };

      bool result = GetOpenFileName(ref ofn);
      if (!result)
      {
        return string.Empty;
      }

      int nullTerminator = ofn.file.IndexOf('\0');
      return nullTerminator >= 0 ? ofn.file.Substring(0, nullTerminator) : ofn.file;
    }
#endif
  }
}
