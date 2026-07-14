namespace HandsLiftedApp.Importer.GoogleSlides
{
    public class ImportFailureException : Exception
    {
    }

    public class TokenExpiredImportException : ImportFailureException
    {
    }
}
