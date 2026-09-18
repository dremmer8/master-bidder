using System;

namespace MasterBidder.Core
{
    public enum ExamQuestionKind
    {
        DropdownRestore = 0,
        Quiz4 = 1,
        TextInput = 2,
        PickPainting = 3
    }

    [Serializable]
    public class ExamQuestion
    {
        public ExamQuestionKind Kind;
        public string TargetArtworkId;
        public string Prompt;
        public string FieldId;
        public string CorrectAnswer;
        public string[] Options = Array.Empty<string>();
        public string AutofillAnswer;
        public bool HasAutofill;
    }

    public class ExamSession
    {
        public string TierId;
        public ExamQuestion[] Questions = Array.Empty<ExamQuestion>();
        public string[] PlayerAnswers = Array.Empty<string>();
        public int CurrentIndex;
        public bool FeePaid;
        public bool Finished;
        public bool Passed;
        public int CorrectCount;

        public ExamQuestion Current =>
            Questions != null && CurrentIndex >= 0 && CurrentIndex < Questions.Length
                ? Questions[CurrentIndex]
                : null;

        public bool IsLastCard =>
            Questions != null && CurrentIndex >= Questions.Length - 1;
    }
}
