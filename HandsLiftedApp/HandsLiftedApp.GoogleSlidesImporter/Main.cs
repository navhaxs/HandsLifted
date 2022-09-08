using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Slides.v1.Data;
using Google.Apis.Slides.v1;
using Google.Apis.Util.Store;
using Google;
using System.Diagnostics;
using System.Text.RegularExpressions;
using static Google.Apis.Drive.v3.FilesResource;
using static HandsLiftedApp.Importer.GoogleSlides.Program;

namespace HandsLiftedApp.Importer.GoogleSlides
{
    public class Main
    {
        static string[] Scopes = { SlidesService.Scope.PresentationsReadonly, DriveService.Scope.DriveFile, DriveService.Scope.DriveReadonly };
        static string ApplicationName = "Google Slides API .NET Quickstart";

        static string PDF_MIME_TYPE = "application/pdf";


        private static readonly object syncSlidesLock = new object();


        public static ImportStats RunGoogleSlidesImportTask(IProgress<ImportStats> progress, ImportTask task)
        {
            ImportStats stats = new ImportStats() { Task = task };
            lock (syncSlidesLock)
            {
                try
                {
                    UserCredential credential;
                    // Load client secrets.
                    using (var stream =
                           new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
                    {
                        /* The file token.json stores the user's access and refresh tokens, and is created
                         automatically when the authorization flow completes for the first time. */
                        string credPath = "token.json";
                        credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                            GoogleClientSecrets.FromStream(stream).Secrets,
                            Scopes,
                            "user",
                            CancellationToken.None,
                            new FileDataStore(credPath, true)).Result;
                        Console.WriteLine("Credential file saved to: " + credPath);
                    }

                    // Create Google Slides API service.
                    var service = new SlidesService(new BaseClientService.Initializer
                    {
                        HttpClientInitializer = credential,
                        ApplicationName = ApplicationName
                    });

                    // Define request parameters.
                    var regex = new Regex(@"[-\w]{25,}");
                    //String fileId = regex.Match("https://docs.google.com/presentation/d/1-EGlDIgKK8cnAD_L77JI_hFNL_RZqHPAkR-rvnezmz0/edit?usp=sharing").Value;
                    //String fileId = regex.Match("https://docs.google.com/presentation/d/1IiBBcLgvc9YprZTpv9CdDHgO1p358j71KXbZj347V58/edit#slide=id.p1").Value;
                    String fileId = regex.Match(task.GoogleSlidesPresentationId).Value;
                    PresentationsResource.GetRequest request = service.Presentations.Get(fileId);

                    Presentation presentation = request.Execute();


                    double progressPercentage = 10.0d;

                    stats.JobStatus = ImportStats.JobStatusEnum.Running;
                    stats.JobPercentage = progressPercentage;

                    if (progress != null)
                        progress.Report(stats);


                    var outputFileName = ReplaceInvalidChars(presentation.Title) + ".pdf";

                    //Result result = new Result()
                    //{
                    //    Title = presentation.Title,
                    //    OutputFileName = outputFileName,
                    //    OutputFullFilePath
                    //};
                    stats.Title = presentation.Title;
                    stats.OutputFileName = outputFileName;
                    stats.OutputFullFilePath = Path.Join(task.OutputDirectory, outputFileName);

                    IList<Page> slides = presentation.Slides;
                    Console.WriteLine("The presentation contains {0} slides:", slides.Count);
                    for (var i = 0; i < slides.Count; i++)
                    {
                        var slide = slides[i];
                        Debug.Print($"Slide {i}: ObjectId={slide.ObjectId}");
                        Console.WriteLine("- Slide #{0} contains {1} elements.", i + 1, slide.PageElements?.Count);
                    }

                    var x = service.HttpClient.GetAsync($"https://docs.google.com/presentation/d/{fileId}/export/png");

                    x.ContinueWith(t =>
                    {
                        var c = t;
                    });

                    //https://docs.google.com/presentation/d/<FileID>/export/<format>


                    progressPercentage = 30.0d;

                    stats.JobStatus = ImportStats.JobStatusEnum.Running;
                    stats.JobPercentage = progressPercentage;

                    if (progress != null)
                        progress.Report(stats);


                    var driveService = new DriveService(new BaseClientService.Initializer
                    {
                        HttpClientInitializer = credential,
                        ApplicationName = ApplicationName
                    });
                    ExportRequest response = driveService.Files.Export(fileId, PDF_MIME_TYPE);
                    using (var stream = new MemoryStream())
                    {
                        var d = response.DownloadWithStatus(stream);

                        if (d.Status == Google.Apis.Download.DownloadStatus.Failed)
                        {
                            Debug.Print(d.Exception.Message);
                        }

                        Directory.CreateDirectory(task.OutputDirectory);
                        using (FileStream file = new FileStream(stats.OutputFullFilePath, FileMode.Create, FileAccess.Write))
                        {
                            stream.Position = 0;
                            stream.CopyTo(file);
                            file.Flush();
                        }
                    }

                    

                    progressPercentage = 90.0d;

                    stats.JobStatus = ImportStats.JobStatusEnum.Running;
                    stats.JobPercentage = progressPercentage;

                    if (progress != null)
                        progress.Report(stats);


                    return stats;
                }
                catch (GoogleApiException e)
                {
                    Console.WriteLine(e.Message);
                    throw new ImportFailureException();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    throw new ImportFailureException();
                }
            }
        }

        public class ImportTask
        {
            public string GoogleSlidesPresentationId { get; set; }
            public string OutputDirectory { get; set; }
        }

        public static string ReplaceInvalidChars(string filename)
        {
            return string.Join("_", filename.Split(Path.GetInvalidFileNameChars()));
        }

        public class ImportStats
        {
            public String Title;
            public String OutputFileName;
            public String OutputFullFilePath;

            public ImportTask Task { get; set; }
            public double JobPercentage { get; set; }

            public JobStatusEnum JobStatus { get; set; }

            public DateTime CompletionTime { get; set; }

            public string FileName { get; set; }

            public enum JobStatusEnum
            {
                Running,
                CompletionSuccess,
                CompletionFailure,
            }
        }
    }

}