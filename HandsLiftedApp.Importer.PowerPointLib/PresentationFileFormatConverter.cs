using HandsLiftedApp.Extensions;
using HandsLiftedApp.Importer.FileFormatConvertTaskData;
using Syncfusion.Presentation;
using Syncfusion.PresentationRenderer;

namespace HandsLiftedApp.Importer.PowerPointLib;

public static class PresentationFileFormatConverter
{
    public static void Run(ImportTask task, IProgress<ImportStats>? progress = null)
    {
        using var presentation = Presentation.Open(task.InputFile);
        string outputFilePath;
        if (task.ExportFileFormat == ImportTask.ExportFileFormatType.PNG)
        {
            presentation.PresentationRenderer = new PresentationRenderer();
            outputFilePath = task.OutputDirectory;
            foreach (var (slide, slideIndex) in presentation.Slides.WithIndex())
            {
                progress?.Report(new ImportStats
                {
                    Task = task,
                    JobStatus = ImportStats.JobStatusEnum.Running,
                    JobPercentage = Math.Ceiling((double)slideIndex / presentation.Slides.Count * 100),
                    StatusMessage = $"Exporting slide {slideIndex} of {presentation.Slides.Count}"
                });

                using var stream = slide.ConvertToImage(ExportImageFormat.Png);
                using var fileStreamOutput =
                    File.Create(Path.Combine(outputFilePath, $"slide_{slideIndex}.png"));
                stream.CopyTo(fileStreamOutput);
            }
        }
        else
        {
            progress?.Report(new ImportStats
            {
                Task = task,
                JobStatus = ImportStats.JobStatusEnum.Running,
                StatusMessage = "Exporting slides..."
            });
            
            using var convertedPdfDoc = PresentationToPdfConverter.Convert(presentation, new PresentationToPdfConverterSettings
                { ShowHiddenSlides = false, EmbedFonts = true });
            var stream = new MemoryStream();
            convertedPdfDoc.Save(stream);

            // save to disk once conversion succeeded
            string targetPdfFile = Path.Combine(task.OutputDirectory,
                Path.GetFileNameWithoutExtension(task.InputFile)) + ".pdf";
            outputFilePath = targetPdfFile;
            using var file = new FileStream(targetPdfFile, FileMode.Create, FileAccess.Write);
            stream.WriteTo(file);
        }
        
        progress?.Report(new ImportStats
        {
            Task = task,
            JobStatus = ImportStats.JobStatusEnum.CompletionSuccess,
            OutputFilePath = outputFilePath,
            JobPercentage = 100d
        });
    }
}