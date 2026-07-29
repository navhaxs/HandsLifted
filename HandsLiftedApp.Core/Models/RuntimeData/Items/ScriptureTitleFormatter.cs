namespace HandsLiftedApp.Core.Models.RuntimeData.Items
{
    public static class ScriptureTitleFormatter
    {
        public static string Format(string bookName, int startChapter, int startVerse, int endChapter, int endVerse) =>
            startChapter == endChapter
                ? (startVerse == endVerse
                    ? $"{bookName} {startChapter}:{startVerse}"
                    : $"{bookName} {startChapter}:{startVerse}-{endVerse}")
                : $"{bookName} {startChapter}:{startVerse}-{endChapter}:{endVerse}";
    }
}
