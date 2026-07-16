using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Slides.v1;
using Google.Apis.Slides.v1.Data;
using Google.Apis.Util;
using Google.Apis.Util.Store;
using Serilog;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using static Google.Apis.Drive.v3.FilesResource;

namespace HandsLiftedApp.Importer.GoogleSlides
{
    public class Main
    {
        static string[] Scopes = { SlidesService.Scope.PresentationsReadonly, DriveService.Scope.DriveFile, DriveService.Scope.DriveReadonly };
        static string ApplicationName = "Google Slides API .NET Quickstart";

        static string PDF_MIME_TYPE = "application/pdf";


        private static readonly object syncSlidesLock = new object();

        public static ImportStats RunGoogleSlidesImportTask(IProgress<ImportStats>? progress, GoogleSlidesPresentationImporter task, string clientId, string clientSecret)
        {
            Log.Information($"Running Google Slides import for {task}");
            ImportStats stats = new ImportStats() { Task = task };
            lock (syncSlidesLock)
            {
                try
                {
                                       
                    string credPath = "token.json";

                    // NOTE: deliberately not GoogleWebAuthorizationBroker.AuthorizeAsync here — when the
                    // stored token is missing/revoked, that call silently launches the interactive
                    // browser sign-in itself (no exception thrown), which bypasses the
                    // GoogleSlidesReauthWindow confirmation dialog entirely. Load/refresh the stored
                    // token non-interactively instead, and surface TokenExpiredImportException so the
                    // caller can show the dialog before any browser window opens.
                    var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
                    {
                        ClientSecrets = new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret },
                        Scopes = Scopes,
                        DataStore = new FileDataStore(credPath, true)
                    });

                    TokenResponse storedToken = flow.LoadTokenAsync("user", CancellationToken.None).GetAwaiter().GetResult();
                    if (storedToken == null)
                    {
                        throw new TokenExpiredImportException();
                    }

                    UserCredential credential = new UserCredential(flow, "user", storedToken);

                    if (credential.Token.IsExpired(SystemClock.Default))
                    {
                        bool refreshed;
                        try
                        {
                            refreshed = credential.RefreshTokenAsync(CancellationToken.None).GetAwaiter().GetResult();
                        }
                        catch (TokenResponseException)
                        {
                            refreshed = false;
                        }

                        if (!refreshed)
                        {
                            throw new TokenExpiredImportException();
                        }
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
                        Debug.Print($"Slide {i}: ObjectId={slide.ObjectId}"); // TODO: store this slide.ObjectId so that subsequent 'syncs' can determine and maintain current active/relative slide selection
                        Console.WriteLine("- Slide #{0} contains {1} elements.", i + 1, slide.PageElements?.Count);
                    }

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
                catch (TokenExpiredImportException)
                {
                    throw;
                }
                catch (GoogleApiException e)
                {
                    Log.Warning(e, "[GoogleSlidesImport] GoogleApiException HttpStatusCode={HttpStatusCode}", e.HttpStatusCode);
                    if (e.HttpStatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        throw new TokenExpiredImportException();
                    }
                    // fails for pptx
                    throw new ImportFailureException();
                }
                catch (TokenResponseException e)
                {
                    Log.Warning(e, "[GoogleSlidesImport] TokenResponseException");
                    throw new TokenExpiredImportException();
                }
                catch (Exception e)
                {
                    Log.Warning(e, "[GoogleSlidesImport] Unhandled exception type {ExceptionType}", e.GetType().FullName);
                    throw new ImportFailureException();
                }
            }
        }

        public class GoogleSlidesPresentationImporter
        {
            public string GoogleSlidesPresentationId { get; set; }
            public string OutputDirectory { get; set; }
        }

        public static void RevokeAndReauth(string clientId, string clientSecret)
        {
            string credPath = "token.json";
            if (File.Exists(credPath))
                File.Delete(credPath);

            GoogleWebAuthorizationBroker.AuthorizeAsync(
                new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret },
                Scopes,
                "user",
                CancellationToken.None,
                new FileDataStore(credPath, true)).GetAwaiter().GetResult();
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

            public GoogleSlidesPresentationImporter Task { get; set; }
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