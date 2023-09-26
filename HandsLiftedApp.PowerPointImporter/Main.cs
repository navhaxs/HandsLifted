using NetOffice.OfficeApi.Enums;
using NetOffice.PowerPointApi;
using System.Diagnostics;

namespace HandsLiftedApp.Importer.PowerPoint
{
    public static class Main
    {

        // NOTE: waiting for https://github.com/NetOfficeFw/NetOffice/issues/343

        private static readonly object syncSlidesLock = new object();

        public static string GetTempDirPath()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PowerSocketTemp");
            System.IO.Directory.CreateDirectory(path);
            return path;
        }

        public static Task<T> StartSTATask<T>(Func<T> func)
        {
            var tcs = new TaskCompletionSource<T>();
            Thread thread = new Thread(() =>
            {
                try
                {
                    tcs.SetResult(func());
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return tcs.Task;
        }
        public static ImportStats RunPowerPointImportTask(IProgress<ImportStats> progress, ImportTask task)
        {
            ImportStats stats = new ImportStats() { Task = task };
            lock (syncSlidesLock)
            {
                Application thisApplication = new Application();

                stats.FileName = Path.GetFileName(task.PPTXFilePath);

                Presentation thisPresentation = thisApplication.Presentations.Open(task.PPTXFilePath, MsoTriState.msoTrue, MsoTriState.msoFalse, MsoTriState.msoFalse);

                Directory.CreateDirectory(task.OutputDirectory);
                DirectoryInfo di = new DirectoryInfo(task.OutputDirectory);

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

                    stats.JobStatus = ImportStats.JobStatusEnum.CompletionFailure;
                    stats.CompletionTime = DateTime.Now;

                    if (progress != null)
                        progress.Report(stats);

                    if (thisPresentation != null)
                        thisPresentation.Close();

                    return stats;
                }

                if (thisPresentation == null || slides == null)
                {
                    Debug.Print("Slides sync failed. No active presentation/slides available");

                    stats.JobStatus = ImportStats.JobStatusEnum.CompletionFailure;
                    stats.CompletionTime = DateTime.Now;

                    if (progress != null)
                        progress.Report(stats);

                    if (thisPresentation != null)
                        thisPresentation.Close();

                    return stats;
                }

                try
                {
                    foreach (Slide slide in slides)
                    {
                        // https://stackoverflow.com/a/2001692
                        double canvasWidth = 1920;
                        double canvasHeight = 1080;
                        double originalWidth = (double)thisPresentation.PageSetup.SlideWidth;
                        double originalHeight = (double)thisPresentation.PageSetup.SlideHeight;

                        double ratioX = (double)canvasWidth / (double)originalWidth;
                        double ratioY = (double)canvasHeight / (double)originalHeight;
                        // use whichever multiplier is smaller
                        double ratio = ratioX < ratioY ? ratioX : ratioY;

                        // now we can get the new height and width
                        int newHeight = Convert.ToInt32(originalHeight * ratio);
                        int newWidth = Convert.ToInt32(originalWidth * ratio);

                        try
                        {
                            slide.Export(Path.Combine(task.OutputDirectory, $"slide_{slide.SlideIndex}.png"), "PNG", newWidth, newHeight);
                        }
                        catch (Exception e)
                        {
                            // possible: out of disk space
                            Debug.Print(e.ToString());
                            Debugger.Break();
                        }

                        double progressPercentage = ((double)slide.SlideIndex / thisPresentation.Slides.Count * 100);

                        stats.JobStatus = ImportStats.JobStatusEnum.Running;
                        stats.JobPercentage = progressPercentage;

                        if (progress != null)
                            progress.Report(stats);
                    }

                    // export job success
                    stats.JobStatus = ImportStats.JobStatusEnum.CompletionSuccess;
                    stats.JobPercentage = 100.0d;
                    stats.CompletionTime = DateTime.Now;

                    if (progress != null)
                        progress.Report(stats);

                    thisPresentation.Close();
                    return stats;
                }
                catch (Exception e)
                {
                    Debug.Print(e.Message);
                    stats.JobStatus = ImportStats.JobStatusEnum.CompletionFailure;
                    stats.JobPercentage = 100.0d;
                    stats.CompletionTime = DateTime.Now;

                    if (progress != null)
                        progress.Report(stats);

                    thisPresentation.Close();
                    return stats;
                }
            }
        }

        public class ImportTask
        {
            public string PPTXFilePath { get; set; }

            public string OutputDirectory { get; set; }
        }

        public class ImportStats
        {

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