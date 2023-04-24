using EmbedIO;
using EmbedIO.Actions;
using EmbedIO.Files;
using Swan.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace HandsLiftedApp.Logic
{
    internal static class HandsLiftedWebServer
    {
        private static int PORT = 8979;
        private static CancellationTokenSource ctSource;

        public static void Start()
        {
            Run();
        }

        public static int getPort()
        { return PORT; }

        private static async Task Run()
        {
            var url = $"http://*:{PORT}";

            ctSource = new CancellationTokenSource();

      
            using (var server = CreateWebServer(url))
            {
                if (!ctSource.IsCancellationRequested)
                    await server.RunAsync(ctSource.Token);
            }
        }

        // Create and configure our web server.
        private static WebServer CreateWebServer(string url)
        {
            var UseFileCache = false;
            return new WebServer(o => o
                    .WithUrlPrefix(url)
                    .WithMode(HttpListenerMode.EmbedIO))
                //.WithLocalSessionManager()
                .WithModule(new HandsLiftedWebSocketsModule("/control"))
            //.WithStaticFolder("/slides", HtmlRootPath, true, m => m.WithContentCaching(UseFileCache))
            //.WithModule(new FileModule("/", new ResourceFileProvider(typeof(App).Assembly, "HandsLiftedApp.Public")))
            //.WithModule(new ActionModule("/", HttpVerbs.Any, ctx => ctx.SendDataAsync(new { Message = "Error" })));
            ;
        }

        public static void Stop()
        {
            if (ctSource != null)
                ctSource.Cancel();

        }
    }
}
