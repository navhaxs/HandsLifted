using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Slides.v1;
using Google.Apis.Slides.v1.Data;
using Google.Apis.Util.Store;
using System.Diagnostics;
using System.Text.RegularExpressions;
using static Google.Apis.Drive.v3.FilesResource;

namespace HandsLiftedApp.Importer.GoogleSlides
{
    // Class to demonstrate use of Slides get presentation API
    public class Program
    {
        /* Global instance of the scopes required by this quickstart.
         If modifying these scopes, delete your previously saved token.json/ folder. */
        static string[] Scopes = { SlidesService.Scope.PresentationsReadonly, DriveService.Scope.DriveFile, DriveService.Scope.DriveReadonly };
        static string ApplicationName = "Google Slides API .NET Quickstart";

        static string PDF_MIME_TYPE = "application/pdf";

        public static void Main(string[] args)
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
                String fileId = regex.Match("https://docs.google.com/presentation/d/1AjkGrL1NzOR5gVWeJ_YLlPtRd19OEaT4ZnW7Y2qkNPM/edit?usp=sharing").Value;
                PresentationsResource.GetRequest request = service.Presentations.Get(fileId);

                // Prints the number of slides and elements in a sample presentation:
                // https://docs.google.com/presentation/d/1EAYk18WDjIG-zp_0vLm3CsfQh_i8eXc67Jo2O9C6Vuc/edit
                Presentation presentation = request.Execute();
                IList<Page> slides = presentation.Slides;
                Console.WriteLine("The presentation contains {0} slides:", slides.Count);
                for (var i = 0; i < slides.Count; i++)
                {
                    var slide = slides[i];
                    Debug.Print($"Slide {i}: ObjectId={slide.ObjectId}");
                    Console.WriteLine("- Slide #{0} contains {1} elements.", i + 1, slide.PageElements?.Count);
                }

                //presentation.Title;
                var x = service.HttpClient.GetAsync($"https://docs.google.com/presentation/d/{fileId}/export/png");

                x.ContinueWith(t =>
                {
                    var c = t;
                });

                //https://docs.google.com/presentation/d/<FileID>/export/<format>

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

                    using (FileStream file = new FileStream("my-exported-slides.pdf", FileMode.Create, System.IO.FileAccess.Write))
                    {
                        stream.Position = 0;
                        stream.CopyTo(file);
                        file.Flush();
                    }
                    //using (FileStream file = new FileStream("file.bin", FileMode.Open, FileAccess.Read))
                    //    file.CopyTo(stream);
                }
            }
            catch (GoogleApiException e)
            {
                Console.WriteLine(e.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}