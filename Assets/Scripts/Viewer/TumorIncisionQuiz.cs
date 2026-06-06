using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Викторина «Выбор точки разреза опухоли».
///
/// Как использовать:
///   1. Добавьте этот компонент на любой GameObject в сцене.
///   2. Привяжите в Inspector: modelLoader, feedbackPanel, feedbackText, closeButton, startButton.
///   3. Назначьте материалы (необязательно — создадутся автоматически).
///   4. Кнопка startButton.onClick → вызывает StartQuiz().
///   5. Кнопка closeButton.onClick  → вызывает CloseQuiz().
///
/// Данные точек берутся из model_data.json → секция incision_quiz.
/// </summary>
public class TumorIncisionQuiz : MonoBehaviour
{
  // ── Inspector ──────────────────────────────────────────────────────────

  [Header("Scene References")]
  [Tooltip("ModelLoader, у которого после загрузки есть IncisionQuizData")]
  public ModelLoader modelLoader;

  [Header("UI")]
  [Tooltip("Панель с кнопкой старта. Скрывается при показе фидбэка.")]
  public GameObject mainPanel;
  public GameObject feedbackPanel;
  public TMP_Text feedbackText;
  public Button closeButton;
  public Button startButton;
  [Tooltip("Текст кнопки в режиме запуска.")]
  public string startButtonText = "Точки разреза";
  [Tooltip("Текст кнопки в режиме выхода.")]
  public string stopButtonText = "Отмена";

  [Header("Point Visuals")]
  [Tooltip("Размер сферы-маркера в Unity units")]
  public float pointSize = 0.03f;

  public Material defaultPointMaterial;
  public Material hoverPointMaterial;
  public Material correctPointMaterial;
  public Material wrongPointMaterial;

  // ── Private state ──────────────────────────────────────────────────────

  private readonly List<GameObject> _spawnedPoints = new List<GameObject>();
  private readonly List<Collider> _disabledColliders = new List<Collider>();

  private bool _quizActive;
  private bool _answered;

  // ── Unity lifecycle ────────────────────────────────────────────────────

  private void Start()
  {
    HideFeedback();

    if (closeButton != null)
      closeButton.onClick.AddListener(CloseQuiz);

    if (startButton != null)
    {
      startButton.gameObject.SetActive(false);
      startButton.onClick.AddListener(ToggleQuiz);
    }
  }

  // ── Public API ─────────────────────────────────────────────────────────

  /// <summary>Переключает викторину: запуск / выход. Привязывается к startButton.</summary>
  public void ToggleQuiz()
  {
    if (_quizActive)
      CloseQuiz();
    else
      StartQuiz();
  }

  /// <summary>Запускает викторину.</summary>
  public void StartQuiz()
  {
    if (_quizActive) return;

    // Убеждаемся, что данные JSON уже загружены
    if (modelLoader == null || modelLoader.IncisionQuizData == null ||
        modelLoader.IncisionQuizData.points == null ||
        modelLoader.IncisionQuizData.points.Count == 0)
    {
      ShowFeedbackError("Данные о точках разреза не найдены.\nЗагрузите model_data.json с секцией incision_quiz.");
      return;
    }

    // Ищем объект опухоли в контейнере модели
    Transform tumorTransform = FindTumorTransform();
    if (tumorTransform == null)
    {
      ShowFeedbackError("Модель опухоли не загружена.\nЗагрузите файл tumor.obj перед запуском викторины.");
      return;
    }

    _quizActive = true;
    _answered = false;

    SetStartButtonText(stopButtonText);

    // Спавним маркеры
    SpawnPoints(tumorTransform);
    DisableModelColliders();

    // UI
    HideFeedback();
  }

  /// <summary>Вызывается кнопкой «Закрыть».</summary>
  public void CloseQuiz()
  {
    _quizActive = false;
    _answered = false;

    DestroyPoints();
    RestoreModelColliders();

    SetStartButtonText(startButtonText);
    HideFeedback();
  }

  // ── Private helpers ────────────────────────────────────────────────────

  private void DisableModelColliders()
  {
    _disabledColliders.Clear();
    if (modelLoader?.container == null) return;

    // Собираем все коллайдеры контейнера, кроме тех, что на сфёрах-маркерах
    var pointSet = new HashSet<GameObject>(_spawnedPoints);
    foreach (Collider col in modelLoader.container.GetComponentsInChildren<Collider>())
    {
      if (col.enabled && !pointSet.Contains(col.gameObject))
      {
        col.enabled = false;
        _disabledColliders.Add(col);
      }
    }
  }

  private void RestoreModelColliders()
  {
    foreach (Collider col in _disabledColliders)
    {
      if (col != null) col.enabled = true;
    }
    _disabledColliders.Clear();
  }

  private void SetStartButtonText(string text)
  {
    if (startButton == null) return;
    TMP_Text label = startButton.GetComponentInChildren<TMP_Text>();
    if (label != null) label.text = text;
  }

  private void ShowFeedbackError(string message)
  {
    if (feedbackText != null)
      feedbackText.text = message;

    ShowFeedback();

    if (closeButton != null)
      closeButton.gameObject.SetActive(true);
  }

  private void ShowFeedback()
  {
    if (mainPanel != null) mainPanel.SetActive(false);
    if (feedbackPanel != null) feedbackPanel.SetActive(true);
  }

  private void HideFeedback()
  {
    if (feedbackPanel != null) feedbackPanel.SetActive(false);
    if (mainPanel != null) mainPanel.SetActive(true);
  }

  private Transform FindTumorTransform()
  {
    if (modelLoader == null || modelLoader.container == null) return null;

    for (int i = 0; i < modelLoader.container.childCount; i++)
    {
      Transform child = modelLoader.container.GetChild(i);
      if (child.name == "Опухоль" || child.name.ToLowerInvariant() == "tumor")
        return child;
    }
    return null;
  }

  private void SpawnPoints(Transform tumorTransform)
  {
    IncisionQuizData data = modelLoader.IncisionQuizData;
    EnsureMaterials();

    float markerSize = pointSize > 0f ? pointSize : 0.03f;

    for (int i = 0; i < data.points.Count; i++)
    {
      IncisionPointData pt = data.points[i];
      if (pt.position == null || pt.position.Length < 3) continue;

      // position — мировые координаты точки в Unity (world position, метры)
      Vector3 worldPosition = new Vector3(
        (float)pt.position[0],
        (float)pt.position[1],
        (float)pt.position[2]);

      // Создаём сферу, выставляем мировую позицию, затем крепим к tumor
      GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      sphere.name = $"IncisionPoint_{i}_{pt.label}";
      sphere.transform.position = worldPosition;
      sphere.transform.localScale = Vector3.one * markerSize;
      sphere.transform.SetParent(tumorTransform, false); // worldPositionStays = true

      // Материал
      MeshRenderer mr = sphere.GetComponent<MeshRenderer>();
      if (mr != null) mr.material = defaultPointMaterial;

      // XRSimpleInteractable — позволяет Ray Interactor выбирать точки
      XRSimpleInteractable interactable = sphere.AddComponent<XRSimpleInteractable>();

      // Нужен Rigidbody для XR Interaction Toolkit
      Rigidbody rb = sphere.AddComponent<Rigidbody>();
      rb.isKinematic = true;
      rb.useGravity = false;

      int capturedIndex = i;
      MeshRenderer capturedMr = mr;

      interactable.hoverEntered.AddListener(_ =>
      {
        if (_answered) return;
        // Сбрасываем все точки, затем подсвечиваем только текущую
        foreach (GameObject pt in _spawnedPoints)
        {
          if (pt == null) continue;
          MeshRenderer r = pt.GetComponent<MeshRenderer>();
          if (r != null) r.material = defaultPointMaterial;
        }
        if (capturedMr != null)
          capturedMr.material = hoverPointMaterial;
      });

      interactable.hoverExited.AddListener(_ =>
      {
        if (!_answered && capturedMr != null)
          capturedMr.material = defaultPointMaterial;
      });

      interactable.selectEntered.AddListener(_ => OnPointSelected(capturedIndex));

      _spawnedPoints.Add(sphere);
    }
  }

  private void OnPointSelected(int selectedIndex)
  {
    if (_answered) return;
    _answered = true;

    IncisionQuizData data = modelLoader.IncisionQuizData;
    bool isCorrect = selectedIndex == data.correct_point_index;

    // Обновляем материалы точек
    for (int i = 0; i < _spawnedPoints.Count; i++)
    {
      if (_spawnedPoints[i] == null) continue;
      MeshRenderer mr = _spawnedPoints[i].GetComponent<MeshRenderer>();
      if (mr == null) continue;

      if (i == data.correct_point_index)
        mr.material = correctPointMaterial;
      else if (i == selectedIndex && !isCorrect)
        mr.material = wrongPointMaterial;
      else
        mr.material = defaultPointMaterial;
    }

    // Отключаем все Interactable после ответа
    foreach (GameObject pt in _spawnedPoints)
    {
      if (pt == null) continue;
      XRSimpleInteractable xri = pt.GetComponent<XRSimpleInteractable>();
      if (xri != null) xri.enabled = false;
    }

    // Показываем фидбэк
    ShowFeedback();

    if (feedbackText != null)
    {
      string label = data.points[selectedIndex].label;
      if (isCorrect)
      {
        feedbackText.text = $"<color=#00CC44>V Правильно!</color>\n<b>{label}</b>\n\n{data.correct_explanation}";
      }
      else
      {
        string correctLabel = data.points[data.correct_point_index].label;
        feedbackText.text =
            $"<color=#CC2200>X Неправильно.</color>\n<b>Вы выбрали:</b> {label}\n<b>Правильный ответ:</b> {correctLabel}\n\n{data.wrong_explanation}";
      }
    }
  }

  private void DestroyPoints()
  {
    foreach (GameObject pt in _spawnedPoints)
    {
      if (pt != null) Destroy(pt);
    }
    _spawnedPoints.Clear();
  }

  // ── Material fallbacks ─────────────────────────────────────────────────

  private void EnsureMaterials()
  {
    Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

    if (defaultPointMaterial == null)
    {
      defaultPointMaterial = new Material(lit);
      defaultPointMaterial.color = new Color(1f, 1f, 1f, 0.85f);
    }
    if (hoverPointMaterial == null)
    {
      hoverPointMaterial = new Material(lit);
      hoverPointMaterial.color = new Color(1f, 0.9f, 0f);
    }
    if (correctPointMaterial == null)
    {
      correctPointMaterial = new Material(lit);
      correctPointMaterial.color = new Color(0f, 0.8f, 0.26f);
    }
    if (wrongPointMaterial == null)
    {
      wrongPointMaterial = new Material(lit);
      wrongPointMaterial.color = new Color(0.8f, 0.13f, 0.05f);
    }
  }
}
