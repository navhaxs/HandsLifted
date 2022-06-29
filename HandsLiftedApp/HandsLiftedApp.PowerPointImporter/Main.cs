using NetOffice.PowerPointApi;
using System.Diagnostics;

namespace HandsLiftedApp.PowerPointImporter
{
    public class Main
    {

        private readonly object syncSlidesLock = new object();


        private static string GetTempDirPath()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PowerSocketTemp");
            System.IO.Directory.CreateDirectory(path);
            return path;
        }

        // example calling code:
        public void Run()
        {
            var progress = new Progress<PowerpointImportProgress>();
            progress.ProgressChanged += Progress_ProgressChanged;
            RunAsync(progress, new PowerpointImportTask() { PPTXFilePath = @"C:\VisionScreens\TestPresentations\ego.pptx", OutputDirectory = PowerPointImporter.TempDir.GetTempDirPath() });    
        }

        private void Progress_ProgressChanged(object? sender, PowerpointImportProgress e)
        {
            Debug.Print(e.ToString());
        }

        private async Task RunAsync(IProgress<PowerpointImportProgress> progress, PowerpointImportTask task)
        {
            lock (syncSlidesLock)
            {
                Application thisApplication = new Application();
                Presentation thisPresentation = thisApplication.Presentations.Open(task.PPTXFilePath);

                //Messenger.Default.Send(new SetIsExportingSlides() { IsExportingSlides = true });

                var temp = TempDir.GetTempDirPath();

                Directory.CreateDirectory(temp);
                DirectoryInfo di = new DirectoryInfo(temp);

                foreach (FileInfo file in di.GetFiles())
                {
                    file.Delete();
                }
                foreach (DirectoryInfo dir in di.GetDirectories())
                {
                    dir.Delete(true);
                }

                Slides? slides = null;
                try
                {
                    if (thisPresentation != null)
                    {
                        slides = thisPresentation.Slides;
                    }
                }
                catch (NetOffice.Exceptions.PropertyGetCOMException e)
                {
                    Debug.Print("Slides sync failed. No active presentation/slides available " + e.Message);
                    if (progress != null)
                        progress.Report(new PowerpointImportProgress() { JobStatus = PowerpointImportProgress.JobStatusEnum.CompletionFailure, CompletionTime = DateTime.Now });
                    return;
                }

                if (slides == null)
                {
                    if (progress != null)
                        progress.Report(new PowerpointImportProgress() { JobStatus = PowerpointImportProgress.JobStatusEnum.CompletionFailure, CompletionTime = DateTime.Now });
                    Debug.Print("Slides sync failed. No active presentation/slides available");
                    return;
                }

                foreach (Slide slide in slides)
                {
                    float slideHeight = thisPresentation.PageSetup.SlideHeight;
                    float slideWidth = thisPresentation.PageSetup.SlideWidth;

                    try
                    {
                        slide.Export(Path.Combine(temp, $"slide_{slide.SlideIndex}.png"), "PNG", slideWidth, slideHeight);
                    }
                    catch (Exception e)
                    {
                        System.Diagnostics.Debug.Print(e.ToString());
                    }
                    
                    double progressPercentage = ((double)(slide.SlideIndex + 1) / (thisPresentation.Slides.Count) * 100);

                    if (progress != null)
                        progress.Report(new PowerpointImportProgress() { JobStatus = PowerpointImportProgress.JobStatusEnum.Running, JobPercentage = progressPercentage });
                }

                // export job success

                if (progress != null)
                    progress.Report(new PowerpointImportProgress() { JobStatus = PowerpointImportProgress.JobStatusEnum.CompletionSuccess, JobPercentage = 1.0d, CompletionTime = DateTime.Now });
            }
        }

        public class PowerpointImportTask
        {
            public string PPTXFilePath { get; set; }

            public string OutputDirectory { get; set; }
        }

        public class PowerpointImportProgress
        {
            public double JobPercentage { get; set; }

            public JobStatusEnum JobStatus { get; set; }

            public DateTime CompletionTime { get; set; }

            public enum JobStatusEnum
            {
                Running,
                CompletionSuccess,
                CompletionFailure,
            }
        }
    }
}