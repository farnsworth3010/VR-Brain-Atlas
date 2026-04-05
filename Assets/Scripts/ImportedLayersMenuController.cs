using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Строит список <see cref="Toggle"/> для слоёв импортированной модели
/// и синхронизирует состояние toggle с активностью соответствующего GameObject.
/// </summary>
public class ImportedLayersMenuController : MonoBehaviour
{
  private const string LogPrefix = "ImportedLayersMenuController";
  private const int DefaultPadding = 4;

  [Header("Assign in Inspector")]
  [SerializeField] private Transform scrollView;
  [SerializeField] private Toggle toggleItemPrefab;
  [SerializeField] private Transform modelRoot;

  [Header("Layout")]
  [Min(0f)]
  [SerializeField] private float itemSpacing = 8f;
  [Min(0f)]
  [SerializeField] private float itemHeight = 36f;
  [SerializeField] private RectOffset contentPadding;

  [Header("Debug")]
  [SerializeField] private bool debugPopulateOnStart = false;

  [Header("Startup")]
  [Min(0f)]
  [SerializeField] private float startDelaySeconds = 1f;

  private void Awake()
  {
    EnsureDefaultValues();
  }

  private void Start()
  {
    StartCoroutine(DelayedStart());
  }

  /// <summary>
  /// Запускает начальную инициализацию с опциональной задержкой.
  /// </summary>
  private IEnumerator DelayedStart()
  {
    if (startDelaySeconds > 0f)
    {
      yield return new WaitForSecondsRealtime(startDelaySeconds);
    }

    RunInitialBuild();
  }

  /// <summary>
  /// Выполняет начальное построение списка.
  /// </summary>
  private void RunInitialBuild()
  {
    if (debugPopulateOnStart)
    {
      DebugPopulateList(20);
      return;
    }

    if (modelRoot != null)
    {
      RebuildFromImportedRoot(modelRoot);
    }
  }

  /// <summary>
  /// Инициализирует значения по умолчанию для сериализуемых ссылочных полей,
  /// если они не заданы в инспекторе.
  /// </summary>
  private void EnsureDefaultValues()
  {
    if (contentPadding == null)
    {
      contentPadding = new RectOffset(DefaultPadding, DefaultPadding, DefaultPadding, DefaultPadding);
    }
  }

  /// <summary>
  /// Заполняет список тестовыми элементами. Используется для проверки UI без импортированной модели.
  /// </summary>
  /// <param name="count">Количество создаваемых элементов.</param>
  public void DebugPopulateList(int count)
  {
    if (!HasRequiredListReferences())
    {
      return;
    }

    EnsureListLayout();
    ClearList();

    for (int i = 0; i < count; i++)
    {
      Toggle toggleInstance = CreateToggleItem();
      SetToggleLabel(toggleInstance, $"Layer {i + 1}");
      toggleInstance.SetIsOnWithoutNotify(true);
    }

    Debug.Log($"{LogPrefix}: Debug populated {count} items.");
  }

  /// <summary>
  /// Удаляет все ранее созданные элементы списка, кроме самого префаба, если он является дочерним объектом контейнера.
  /// </summary>
  public void ClearList()
  {
    Transform itemsParent = GetItemsParent();
    if (itemsParent == null)
    {
      Debug.LogError($"{LogPrefix}: List container is not assigned.");
      return;
    }

    for (int i = itemsParent.childCount - 1; i >= 0; i--)
    {
      Transform child = itemsParent.GetChild(i);
      if (toggleItemPrefab != null && child == toggleItemPrefab.transform)
      {
        continue;
      }

      Destroy(child.gameObject);
    }
  }

  /// <summary>
  /// Полностью перестраивает список слоёв из переданного корня импортированной модели.
  /// </summary>
  /// <param name="importedRoot">Корень импортированной иерархии.</param>
  public void RebuildFromImportedRoot(Transform importedRoot)
  {
    if (!HasRequiredListReferences())
    {
      return;
    }

    EnsureListLayout();
    ClearList();

    if (importedRoot == null)
    {
      return;
    }

    Transform layersRoot = FindFirstLayersRoot(importedRoot);
    if (layersRoot == null)
    {
      Debug.LogWarning($"{LogPrefix}: No layers root found (node with more than one child).");
      return;
    }

    int addedCount = 0;

    for (int i = 0; i < layersRoot.childCount; i++)
    {
      Transform layer = layersRoot.GetChild(i);

      if (!layer.gameObject.activeSelf)
      {
        continue;
      }

      Toggle toggleInstance = CreateToggleItem();
      SetToggleLabel(toggleInstance, layer.name);
      toggleInstance.SetIsOnWithoutNotify(layer.gameObject.activeSelf);

      Transform capturedLayer = layer;
      // Toggle напрямую управляет видимостью соответствующего слоя модели.
      toggleInstance.onValueChanged.AddListener(isOn => capturedLayer.gameObject.SetActive(isOn));
      addedCount++;
    }

    Debug.Log($"{LogPrefix}: Built {addedCount} visible layer toggles from '{layersRoot.name}'.");
  }

  /// <summary>
  /// Настраивает вертикальную раскладку контейнера списка.
  /// Метод управляет только контейнером элементов (обычно ScrollRect.content).
  /// </summary>
  private void EnsureListLayout()
  {
    Transform itemsParent = GetItemsParent();
    RectTransform rectContainer = itemsParent as RectTransform;
    if (rectContainer == null)
    {
      Debug.LogWarning($"{LogPrefix}: List container should be RectTransform.");
      return;
    }

    VerticalLayoutGroup layoutGroup = rectContainer.GetComponent<VerticalLayoutGroup>();
    if (layoutGroup == null)
    {
      layoutGroup = rectContainer.gameObject.AddComponent<VerticalLayoutGroup>();
    }

    layoutGroup.spacing = itemSpacing;
    layoutGroup.padding = contentPadding;
    layoutGroup.childAlignment = TextAnchor.UpperLeft;
    layoutGroup.childControlWidth = true;
    layoutGroup.childControlHeight = false;
    layoutGroup.childForceExpandWidth = true;
    layoutGroup.childForceExpandHeight = false;

    ContentSizeFitter sizeFitter = rectContainer.GetComponent<ContentSizeFitter>();
    if (sizeFitter == null)
    {
      sizeFitter = rectContainer.gameObject.AddComponent<ContentSizeFitter>();
    }

    sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
    sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
  }

  /// <summary>
  /// Ищет первый узел в дереве, у которого больше одного дочернего элемента.
  /// Это эвристика для определения корня слоёв импортированной модели.
  /// </summary>
  private Transform FindFirstLayersRoot(Transform root)
  {
    if (root == null)
    {
      return null;
    }

    Stack<Transform> stack = new Stack<Transform>();
    stack.Push(root);

    while (stack.Count > 0)
    {
      Transform current = stack.Pop();

      if (current.childCount > 1)
      {
        return current;
      }

      for (int i = current.childCount - 1; i >= 0; i--)
      {
        stack.Push(current.GetChild(i));
      }
    }

    return null;
  }

  /// <summary>
  /// Устанавливает подпись у toggle (TMP_Text приоритетнее legacy Text).
  /// </summary>
  private void SetToggleLabel(Toggle toggleInstance, string labelText)
  {
    TMP_Text tmpLabel = toggleInstance.GetComponentInChildren<TMP_Text>(true);
    if (tmpLabel != null)
    {
      tmpLabel.text = labelText;
      return;
    }

    Text uiTextLabel = toggleInstance.GetComponentInChildren<Text>(true);
    if (uiTextLabel != null)
    {
      uiTextLabel.text = labelText;
    }
  }

  /// <summary>
  /// Проверяет, что необходимые ссылки для построения списка назначены.
  /// </summary>
  private bool HasRequiredListReferences()
  {
    if (scrollView == null)
    {
      Debug.LogError($"{LogPrefix}: Scroll view is not assigned.");
      return false;
    }

    if (toggleItemPrefab == null)
    {
      Debug.LogError($"{LogPrefix}: Toggle item prefab is not assigned.");
      return false;
    }

    return true;
  }

  /// <summary>
  /// Создаёт элемент toggle внутри контейнера элементов списка.
  /// Если scrollView указывает на ScrollRect (uGUI), элементы добавляются в ScrollRect.content.
  /// </summary>
  private Toggle CreateToggleItem()
  {
    Transform itemsParent = GetItemsParent();
    Toggle toggleInstance = Instantiate(toggleItemPrefab, itemsParent, false);
    toggleInstance.gameObject.SetActive(true);

    LayoutElement layoutElement = toggleInstance.GetComponent<LayoutElement>();
    if (layoutElement == null)
    {
      layoutElement = toggleInstance.gameObject.AddComponent<LayoutElement>();
    }

    layoutElement.preferredHeight = itemHeight;
    layoutElement.flexibleHeight = 0f;
    return toggleInstance;
  }

  /// <summary>
  /// Возвращает контейнер, в который нужно добавлять элементы списка.
  /// Для ScrollView (uGUI) это ScrollRect.content, иначе сам scrollView.
  /// </summary>
  private Transform GetItemsParent()
  {
    if (scrollView == null)
    {
      return null;
    }

    ScrollRect scrollRect = scrollView.GetComponent<ScrollRect>();
    if (scrollRect != null && scrollRect.content != null)
    {
      return scrollRect.content;
    }

    return scrollView;
  }
}
