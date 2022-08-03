using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Slides.v1;
using Google.Apis.Slides.v1.Data;
using Google.Apis.Util.Store;

namespace HandsLiftedApp.Importer.GoogleSlides
{
    // Class to demonstrate use of Slides get presentation API
    public class Program
    {
        /* Global instance of the scopes required by this quickstart.
         If modifying these scopes, delete your previously saved token.json/ folder. */
        static string[] Scopes = { SlidesService.Scope.PresentationsReadonly };
        static string ApplicationName = "Google Slides API .NET Quickstart";

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
                String presentationId = "1EAYk18WDjIG-zp_0vLm3CsfQh_i8eXc67Jo2O9C6Vuc";
                PresentationsResource.GetRequest request = service.Presentations.Get(presentationId);

                // Prints the number of slides and elements in a sample presentation:
                // https://docs.google.com/presentation/d/1EAYk18WDjIG-zp_0vLm3CsfQh_i8eXc67Jo2O9C6Vuc/edit
                Presentation presentation = request.Execute();
                IList<Page> slides = presentation.Slides;
                Console.WriteLine("The presentation contains {0} slides:", slides.Count);
                for (var i = 0; i < slides.Count; i++)
                {
                    var slide = slides[i];
                    Console.WriteLine("- Slide #{0} contains {1} elements.", i + 1, slide.PageElements.Count);
                }

                //presentation.Title;
                var x = service.HttpClient.GetAsync("https://docs.google.com/presentation/d/1EAYk18WDjIG-zp_0vLm3CsfQh_i8eXc67Jo2O9C6Vuc/export/png");

                x.ContinueWith(t =>
                {
                    var c = t;
                });

                //https://docs.google.com/presentation/d/<FileID>/export/<format>

            }
            catch (FileNotFoundException e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}