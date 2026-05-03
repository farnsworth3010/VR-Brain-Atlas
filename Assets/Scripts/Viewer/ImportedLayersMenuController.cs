using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Строит список для слоёв импортированной модели
// и синхронизирует состояние toggle с активностью соответствующего GameObject.
public class ImportedLayersMenuController : MonoBehaviour
{
  private const string LogPrefix = "ImportedLayersMenuController";
  private const int DefaultPadding = 4;

  public Transform scrollView;
  public Toggle toggleItemPrefab;
  public Transform modelRoot;

  private float itemSpacing = 8f;
  private float itemHeight = 36f;
  private RectOffset contentPadding;

  private float startDelaySeconds = 1f;

  private void Awake()
  {
    EnsureDefaultValues();
  }

  private void Start()
  {
    StartCoroutine(DelayedStart());
  }

  private IEnumerator DelayedStart()
  {
    if (startDelaySeconds > 0f)
    {
      yield return new WaitForSecondsRealtime(startDelaySeconds);
    }

    RunInitialBuild();
  }

  private void RunInitialBuild()
  {
    if (modelRoot != null)
    {
      RebuildList();
    }
  }

  private void EnsureDefaultValues()
  {
    if (contentPadding == null)
    {
      contentPadding = new RectOffset(DefaultPadding, DefaultPadding, DefaultPadding, DefaultPadding);
    }
  }

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

  public void RebuildList()
  {
    if (!HasRequiredListReferences() || modelRoot == null)
    {
      return;
    }

    EnsureListLayout();
    ClearList();

    for (int i = 0; i < modelRoot.childCount; i++)
    {
      Transform layer = modelRoot.GetChild(i);
      Toggle toggleInstance = CreateToggleItem();
      SetToggleLabel(toggleInstance, layer.name);
      toggleInstance.SetIsOnWithoutNotify(layer.gameObject.activeSelf);

      Transform capturedLayer = layer;
      toggleInstance.onValueChanged.AddListener(isOn => capturedLayer.gameObject.SetActive(isOn));
    }

    Debug.Log($"{LogPrefix}: Rebuilt menu from '{modelRoot.name}'.");
  }

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
