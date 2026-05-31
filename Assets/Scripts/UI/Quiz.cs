using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controls the brain anatomy quiz flow:
///   1. Question screen  — number, text, two answer buttons
///   2. Feedback screen  — tick/cross icons, correct answer and explanation, Next button
///   3. Result screen    — score summary, contextual result text, Home / Restart buttons
///
/// Wire up all public fields in the Inspector.
/// Call OnAnswerSelected(0) and OnAnswerSelected(1) from the respective Button.OnClick() events.
/// Call OnNextClicked() from nextButton.OnClick().
/// Call OnRestartClicked() from restartButton.OnClick().
/// Call OnHomeClicked() from homeButton.OnClick() — it fires the onHome UnityEvent.
/// </summary>
public class Quiz : MonoBehaviour
{
  // ─────────────────────────────────────────────────────────────────────────
  // Inspector fields
  // ─────────────────────────────────────────────────────────────────────────

  [Header("Data")]
  [Tooltip("File name inside Assets/Resources/ without extension.")]
  public string jsonFileName = "quiz";

  [Header("Question Panel")]
  public GameObject questionPanel;
  public TMP_Text questionNumberText;
  public TMP_Text questionText;

  [Header("Answer Buttons (size = 2)")]
  public Button[] answerButtons;
  public TMP_Text[] answerButtonLabels;

  [Header("Feedback")]
  [Tooltip("Shown after the player picks an answer. Display correct answer + explanation.")]
  public TMP_Text feedbackText;
  public Button nextButton;

  [Header("Result Panel")]
  public GameObject resultPanel;
  public TMP_Text scoreText;
  public TMP_Text resultText;
  public Button homeButton;
  public Button restartButton;

  // ─────────────────────────────────────────────────────────────────────────
  // Private state
  // ─────────────────────────────────────────────────────────────────────────

  private QuizQuestion[] _questions;
  private int _currentIndex;
  private int _correctCount;
  private int _wrongCount;
  private bool _answered;

  // ─────────────────────────────────────────────────────────────────────────
  // Unity lifecycle
  // ─────────────────────────────────────────────────────────────────────────

  private void Start()
  {
    LoadQuestions();
    ResetState();
    ShowQuestion(0);
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Public API — wire these to Button.OnClick() in the Inspector
  // ─────────────────────────────────────────────────────────────────────────

  /// <summary>
  /// Called by an answer Button. Pass 0 for the first button, 1 for the second.
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

    // Disable all answer buttons so the player cannot click again
    foreach (var btn in answerButtons)
      btn.interactable = false;

    if (nextButton != null)
      nextButton.gameObject.SetActive(true);
  }

  /// <summary>Called by nextButton.</summary>
  public void OnNextClicked()
  {
    int next = _currentIndex + 1;
    if (next < _questions.Length)
      ShowQuestion(next);
    else
      ShowResult();
  }

  /// <summary>Called by restartButton — resets state and starts from the first question.</summary>
  public void OnRestartClicked()
  {
    ResetState();
    ShowQuestion(0);
  }

  /// <summary>Called by homeButton — loads scene 0 (main menu).</summary>
  public void OnHomeClicked()
  {
    SceneManager.LoadScene(0);
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Private helpers
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

    // Panel visibility
    SetPanelActive(questionPanel, true);
    SetPanelActive(resultPanel, false);

    // Question header
    if (questionNumberText != null)
      questionNumberText.text = $"Вопрос {index + 1} / {_questions.Length}";

    if (questionText != null)
    {
      // Build question text with numbered answers appended
      var sb = new System.Text.StringBuilder(q.question);
      if (q.answers != null)
        for (int i = 0; i < q.answers.Length; i++)
          sb.Append($"\n\n{i + 1}. {q.answers[i]}");
      questionText.text = sb.ToString();
    }

    // Answer buttons — show only the answer number
    for (int i = 0; i < answerButtons.Length; i++)
    {
      if (answerButtons[i] != null)
        answerButtons[i].interactable = true;

      if (answerButtonLabels != null && i < answerButtonLabels.Length && answerButtonLabels[i] != null)
        answerButtonLabels[i].text = (i + 1).ToString();
    }

    // Hide feedback
    if (feedbackText != null)
    {
      feedbackText.text = string.Empty;
      feedbackText.gameObject.SetActive(false);
    }

    // Hide Next button
    if (nextButton != null)
      nextButton.gameObject.SetActive(false);
  }

  private void ShowFeedback(int selectedIndex, int correctIndex, bool isCorrect)
  {
    // Append ✓/✗ symbol directly to each answer button label
    if (answerButtonLabels != null)
    {
      for (int i = 0; i < answerButtonLabels.Length; i++)
      {
        if (answerButtonLabels[i] == null) continue;
        if (i == correctIndex)
          answerButtonLabels[i].text = $"{i + 1} V";
        else if (i == selectedIndex)
          answerButtonLabels[i].text = $"{i + 1} X";
        // buttons that are neither selected nor correct keep their number
      }
    }

    // Feedback text
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
  // Utilities
  // ─────────────────────────────────────────────────────────────────────────

  private static void SetPanelActive(GameObject panel, bool active)
  {
    if (panel != null) panel.SetActive(active);
  }
}
