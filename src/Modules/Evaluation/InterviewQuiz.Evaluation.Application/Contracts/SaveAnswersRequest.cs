namespace InterviewQuiz.Evaluation.Application.Contracts;

public sealed class SaveAnswersRequest
{
    public List<AnswerDto> Answers { get; set; } = [];
}
