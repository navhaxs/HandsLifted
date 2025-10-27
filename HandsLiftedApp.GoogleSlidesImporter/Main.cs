using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Slides.v1;
using Google.Apis.Slides.v1.Data;
using Google.Apis.Util;
using Google.Apis.Util.Store;
using Newtonsoft.Json.Linq;
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

        public static ImportStats RunGoogleSlidesImportTask(IProgress<ImportStats>? progress, GoogleSlidesPresentationImporter task)
        {
            Log.Information($"Running Google Slides import for {task}");
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

                    if (credential.Token.IsExpired(SystemClock.Default))
                    {
                        var m = credential.GetAccessTokenForRequestAsync().Result;
                        //If the token is expired recreate the token
                        TokenResponse token = credential.Flow.RefreshTokenAsync(credential.UserId, credential.Token.RefreshToken, CancellationToken.None).Result;
                        credential.Token = token;
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

                        //{
                        //                        "width": 1600,
                        //  "height": 900,
                        //  "contentUrl": "https://lh4.googleusercontent.com/xRcaengA36WI6Oa8OKuAFpKq7ijKkI1ddv_FrJ4HsFDgaPmpa-TldIm_KmQbCMkrFfqkEVfSrfWayB42GrFhyF1nWn3g_StknLwJt0cGwgLPO35fFGYhw7BPBF3iAH8G1PMQtOjeTs-GpwC7Akxo0zymMe0aLcIb8TkWTjwbQPbVHXSlx2V5bsWMr49mvH2AcCN1M5dUYIlYZpIFKGLmH9WLhl1WKyo0Yfdfnr_IwOmwi3ko0oOQQ98PPq36KoZN4qLx7iSS54DcpKnBTwgr7BQ6WMV58A=s1600"
                        //}

                        service.HttpClient.GetAsync($"https://slides.googleapis.com/v1/presentations/{fileId}/pages/{slide.ObjectId}/thumbnail").ContinueWith(r =>

                        {

                            r.Result.Content.ReadFromJsonAsync<JObject>().ContinueWith(r =>
                            {

                            });
                            //JObject joResponse = JObject.Parse();
                            //JObject ojObject = (JObject)joResponse["contentUrl"];

                            //using (var fs = new FileStream($"R:\\{slide.ObjectId}.png", FileMode.CreateNew))
                            //    {
                            //        r.Result.Content.CopyToAsync(fs);
                            //    }
                            //});

                        });
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
                    // fails for pptx
                    Console.WriteLine(e.Message);
                    throw new ImportFailureException();
                }
                catch (TokenResponseException e)
                {
                    // fails here if token is expired. solution: retry calling this method
                    Console.WriteLine(e.Message);
                    // TODO: prompt re-auth instead of aborting workflow
                    //if (e.Error.Error == "invalid_grant" && e.Error.ErrorDescription == "Bad Request" && e.Error.ErrorUri == null)
                    throw new ImportFailureException();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    throw new ImportFailureException();
                }
            }
        }

        public class GoogleSlidesPresentationImporter
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