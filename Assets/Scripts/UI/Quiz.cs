using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Управляет прохождением викторины по анатомии мозга:
///   1. Экран вопроса  — номер, текст, две кнопки ответа
///   2. Экран обратной связи  — иконки верно/неверно, правильный ответ и пояснение, кнопка «Далее»
///   3. Экран результата    — итоговый счёт, текст оценки, кнопки «Домой» / «Заново»
///
/// Подключите все публичные поля в Инспекторе.
/// Вызовите OnAnswerSelected(0) и OnAnswerSelected(1) из событий Button.OnClick() соответствующих кнопок.
/// Вызовите OnNextClicked() из nextButton.OnClick().
/// Вызовите OnRestartClicked() из restartButton.OnClick().
/// Вызовите OnHomeClicked() из homeButton.OnClick().
/// </summary>
public class Quiz : MonoBehaviour
{
  // ─────────────────────────────────────────────────────────────────────────
  // Поля Инспектора
  // ─────────────────────────────────────────────────────────────────────────

  [Header("Data")]
  public string jsonFileName = "quiz";

  [Header("Question Panel")]
  public GameObject questionPanel;
  public TMP_Text questionNumberText;
  public TMP_Text questionText;

  [Header("Answer Buttons (size = 2)")]
  public Button[] answerButtons;
  public TMP_Text[] answerButtonLabels;

  [Header("Feedback")]
  [Tooltip("Отображается после выбора ответа игроком. Показывает правильный ответ и пояснение.")]
  public TMP_Text feedbackText;
  public Button nextButton;

  [Header("Result Panel")]
  public GameObject resultPanel;
  public TMP_Text scoreText;
  public TMP_Text resultText;
  public Button homeButton;
  public Button restartButton;

  // ─────────────────────────────────────────────────────────────────────────
  // Приватное состояние
  // ─────────────────────────────────────────────────────────────────────────

  private QuizQuestion[] _questions;
  private int _currentIndex;
  private int _correctCount;
  private int _wrongCount;
  private bool _answered;

  private void Start()
  {
    LoadQuestions();
    ResetState();
    ShowQuestion(0);
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Публичный API — подключить к Button.OnClick() в Инспекторе
  // ─────────────────────────────────────────────────────────────────────────

  /// <summary>
  /// Вызывается кнопкой ответа. Передайте 0 для первой кнопки, 1 для второй.
  /// </summary>
  public void OnAnswerSelected(int answerIndex)
  {
    if (_answered) return;
    _answered = true;

    bool isCorrect = answerIndex == _questions[_currentIndex].correctIndex;

    if (isCorrect)
      _correctCount++;
    else
      _wrongCount++;

    ShowFeedback(answerIndex, _questions[_currentIndex].correctIndex, isCorrect);

    // Отключаем все кнопки ответов, чтобы игрок не мог кликнуть повторно
    foreach (var btn in answerButtons)
      btn.interactable = false;

    if (nextButton != null)
      nextButton.gameObject.SetActive(true);
  }

  /// <summary>Вызывается кнопкой nextButton.</summary>
  public void OnNextClicked()
  {
    int next = _currentIndex + 1;

    if (next < _questions.Length)
      ShowQuestion(next);
    else
      ShowResult();
  }

  /// <summary>Вызывается кнопкой restartButton — сбрасывает состояние и начинает с первого вопроса.</summary>
  public void OnRestartClicked()
  {
    ResetState();
    ShowQuestion(0);
  }

  /// <summary>Вызывается кнопкой homeButton — загружает сцену 0 (главное меню).</summary>
  public void OnHomeClicked()
  {
    SceneManager.LoadScene(0);
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Вспомогательные методы
  // ─────────────────────────────────────────────────────────────────────────

  private void LoadQuestions()
  {
    TextAsset asset = Resources.Load<TextAsset>(jsonFileName);
    if (asset == null)
    {
      Debug.LogError($"[Quiz] Could not load Resources/{jsonFileName}.json");
      _questions = new QuizQuestion[0];
      return;
    }

    QuizData data = JsonUtility.FromJson<QuizData>(asset.text);
    _questions = (data?.questions != null) ? data.questions : new QuizQuestion[0];

    if (_questions.Length == 0)
      Debug.LogWarning($"[Quiz] Loaded 0 questions from {jsonFileName}.json");
  }

  private void ResetState()
  {
    _currentIndex = 0;
    _correctCount = 0;
    _wrongCount = 0;
    _answered = false;
  }

  private void ShowQuestion(int index)
  {
    if (_questions == null || index >= _questions.Length) return;

    _currentIndex = index;
    _answered = false;
    QuizQuestion q = _questions[index];

    // Видимость панелей
    SetPanelActive(questionPanel, true);
    SetPanelActive(resultPanel, false);

    // Заголовок вопроса
    if (questionNumberText != null)
      questionNumberText.text = $"Вопрос {index + 1} / {_questions.Length}";

    if (questionText != null)
    {
      // Формируем текст вопроса с пронумерованными вариантами ответов
      var sb = new System.Text.StringBuilder(q.question);
      if (q.answers != null)
        for (int i = 0; i < q.answers.Length; i++)
          sb.Append($"\n\n{i + 1}. {q.answers[i]}");
      questionText.text = sb.ToString();
    }

    // Кнопки ответов — отображаем только номер ответа
    for (int i = 0; i < answerButtons.Length; i++)
    {
      if (answerButtons[i] != null)
        answerButtons[i].interactable = true;

      if (answerButtonLabels != null && i < answerButtonLabels.Length && answerButtonLabels[i] != null)
        answerButtonLabels[i].text = (i + 1).ToString();
    }

    // Скрываем обратную связь
    if (feedbackText != null)
    {
      feedbackText.text = string.Empty;
      feedbackText.gameObject.SetActive(false);
    }

    // Скрываем кнопку «Далее»
    if (nextButton != null)
      nextButton.gameObject.SetActive(false);
  }

  private void ShowFeedback(int selectedIndex, int correctIndex, bool isCorrect)
  {
    // Добавляем символ V/X непосредственно к тексту каждой кнопки ответа
    if (answerButtonLabels != null)
    {
      for (int i = 0; i < answerButtonLabels.Length; i++)
      {
        if (answerButtonLabels[i] == null) continue;
        if (i == correctIndex)
          answerButtonLabels[i].text = $"{i + 1} V";
        else if (i == selectedIndex)
          answerButtonLabels[i].text = $"{i + 1} X";
        // кнопки, которые не выбраны и не являются правильными, сохраняют свой номер
      }
    }

    // Текст обратной связи
    if (feedbackText != null)
    {
      QuizQuestion q = _questions[_currentIndex];
      string correctAnswerText = (q.answers != null && correctIndex < q.answers.Length)
          ? $"{correctIndex + 1}. {q.answers[correctIndex]}"
          : (correctIndex + 1).ToString();

      string prefix = isCorrect ? "V Верно!" : "X Неверно.";
      feedbackText.text = $"{prefix}\n\n" +
                          $"<b>Правильный ответ:</b> {correctAnswerText}\n\n" +
                          $"{q.explanation}";
      feedbackText.gameObject.SetActive(true);
    }
  }

  private void ShowResult()
  {
    SetPanelActive(questionPanel, false);
    SetPanelActive(resultPanel, true);

    int total = _correctCount + _wrongCount;

    if (scoreText != null)
      scoreText.text = $"Правильно: {_correctCount}   Неверно: {_wrongCount}   Всего: {total}";

    if (resultText != null)
      resultText.text = GetResultMessage(_correctCount, total);

    if (homeButton != null) homeButton.gameObject.SetActive(true);
    if (restartButton != null) restartButton.gameObject.SetActive(true);
  }

  private static string GetResultMessage(int correct, int total)
  {
    if (total == 0) return "Нет данных.";

    float ratio = (float)correct / total;

    if (ratio >= 1f)
      return "Отлично! Вы знаете строение мозга превосходно!";
    if (ratio >= 0.7f)
      return "Хороший результат! Но кое-что ещё можно повторить.";
    if (ratio >= 0.4f)
      return "Неплохо, но стоит ещё раз изучить материал.";

    return "Попробуйте ещё раз — мозг этого стоит!";
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Утилиты
  // ─────────────────────────────────────────────────────────────────────────

  private static void SetPanelActive(GameObject panel, bool active)
  {
    if (panel != null) panel.SetActive(active);
  }
}
