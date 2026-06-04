using System;

[Serializable]
public class QuizQuestion
{
    public string question;

    public string[] answers;

    public int correctIndex;

    public string explanation;
}

[Serializable]
public class QuizData
{
    public QuizQuestion[] questions;
}
