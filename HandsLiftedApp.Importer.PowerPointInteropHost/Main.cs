using System;
using NetOffice.OfficeApi.Enums;
using NetOffice.PowerPointApi;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HandsLiftedApp.Importer.FileFormatConvertTaskData;
using NetOffice;
using NetOffice.PowerPointApi.Enums;

namespace HandsLiftedApp.Importer.PowerPoint
{
    public static class Converter
    {
        // NOTE: waiting for https://github.com/NetOfficeFw/NetOffice/issues/343

        private static readonly object syncSlidesLock = new object();

        public static ImportStats RunPowerPointImportTask(ImportTask task, IProgress<ImportStats>? progress = null)
        {
            ImportStats stats = new ImportStats() { Task = task };
            lock (syncSlidesLock)
            {
                Application thisApplication = new Application();


                Presentation thisPresentation = thisApplication.Presentations.Open(task.InputFile,
                    MsoTriState.msoTrue, MsoTriState.msoFalse, MsoTriState.msoFalse);

                Directory.CreateDirectory(task.OutputDirectory);
                DirectoryInfo di = new DirectoryInfo(task.OutputDirectory);

                // foreach (FileInfo file in di.GetFiles())
                // {
                //     file.Delete();
                // }
                // foreach (DirectoryInfo dir in di.GetDirectories())
                // {
                //     dir.Delete(true);
                // }
                //

                if (task.ExportFileFormat == ImportTask.ExportFileFormatType.PDF)
                {
                    try
                    {
                        stats.StatusMessage = "Exporting slides...";
                        
                        if (progress != null)
                            progress.Report(stats); 

                        string pdfFile = Path.Combine(task.OutputDirectory,
                            Path.GetFileNameWithoutExtension(task.InputFile)) + ".pdf";
                        stats.OutputFilePath = pdfFile;
                        thisPresentation.SaveAs(pdfFile, PpSaveAsFileType.ppSaveAsPDF, MsoTriState.msoCTrue);

                        // *ExportAsFixedFormat* would not work...
                        
                        // thisPresentation.ExportAsFixedFormat(
                        //     pdfFile,
                        //     PpFixedFormatType.ppFixedFormatTypePDF
                        // );
                    }
                    catch (Exception e)
                    {
                        Debug.Print("Presentation SaveAs PDF failed. " + e.Message);

                        stats.JobStatus = ImportStats.JobStatusEnum.CompletionFailure;
                        stats.CompletionTime = DateTime.Now;

                        if (progress != null)
                            progress.Report(stats);

                        if (thisPresentation != null)
                            thisPresentation.Close();

                        return stats;
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
                else
                {
                    stats.OutputFilePath = task.OutputDirectory;
                    
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
                                slide.Export(Path.Combine(task.OutputDirectory, $"slide_{slide.SlideIndex}.png"), "PNG",
                                    newWidth, newHeight);
                            }
                            catch (Exception e)
                            {
                                // possible: out of disk space
                                Debug.Print(e.ToString());
                                Debugger.Break();
                            }

                            double progressPercentage =
                                Math.Ceiling((double)slide.SlideIndex / thisPresentation.Slides.Count * 100);

                            stats.JobStatus = ImportStats.JobStatusEnum.Running;
                            stats.JobPercentage = progressPercentage;
                            stats.StatusMessage = $"Exporting slide {slide.SlideIndex} of {slides.Count}";

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
        }
    }
}