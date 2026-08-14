namespace HandsLiftedApp.Importer.GoogleSlides
{
    public class ImportFailureException : Exception
    {
        public ImportFailureException()
        {
        }

        public ImportFailureException(string message) : base(message)
        {
        }
    }

    public class TokenExpiredImportException : ImportFailureException
    {
    }
}
