using System;
using System.IO;
using HandsLiftedApp.Core.Utils;
using HandsLiftedApp.Importer.FileFormatConvertTaskData;
using HandsLiftedApp.Importer.PowerPointLib;
using Serilog;

namespace HandsLiftedApp.Core.Services;

public enum ToolboxConversion
{
    PptxToPdf,
    PptxToPngs,
    PdfToPngs
}

/// <summary>
/// Stand-alone file conversions behind the Welcome window's Toolbox menu. Reuses the same
/// converters as the playlist import pipeline, but writes to a user-chosen folder rather than the import cache.
/// </summary>
public static class ConversionToolbox
{
    public static string InputExtension(ToolboxConversion kind) =>
        kind == ToolboxConversion.PdfToPngs ? ".pdf" : ".pptx";

    /// <summary>Runs a conversion and returns the path of the file (PDF) or folder (PNGs) that was produced.</summary>
    public static string Run(ToolboxConversion kind, string inputFile, string outputParentDirectory,
        Action<ImportStats>? onProgress = null)
    {
        if (!File.Exists(inputFile))
            throw new FileNotFoundException("Input file not found.", inputFile);
        Directory.CreateDirectory(outputParentDirectory);

        return kind switch
        {
            ToolboxConversion.PptxToPdf => PptxToPdf(inputFile, outputParentDirectory, onProgress),
            ToolboxConversion.PptxToPngs => PptxToPngs(inputFile, outputParentDirectory, onProgress),
            ToolboxConversion.PdfToPngs => PdfToPngs(inputFile, outputParentDirectory, onProgress),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private static string PptxToPdf(string inputFile, string outputDirectory, Action<ImportStats>? onProgress)
    {
        var task = new ImportTask
        {
            InputFile = inputFile,
            OutputDirectory = outputDirectory,
            ExportFileFormat = ImportTask.ExportFileFormatType.PDF
        };
        var reporter = onProgress == null ? null : new ImportTaskReporter(onProgress);

        // Same engine choice as a PowerPoint item's Sync.
        if (PowerPointImportSettings.UseNativeInterop && OperatingSystem.IsWindows())
        {
            Log.Debug("Toolbox: PPTX -> PDF via native PowerPoint: {File}", inputFile);
            var result = NativePowerPointImportService.RunImport(task, reporter);
            if (result.JobStatus != ImportStats.JobStatusEnum.CompletionSuccess ||
                string.IsNullOrEmpty(result.OutputFilePath))
                throw new InvalidOperationException(
                    result.StatusMessage ?? "PowerPoint could not convert this file.");
            return result.OutputFilePath;
        }

        Log.Debug("Toolbox: PPTX -> PDF via Syncfusion: {File}", inputFile);
        return PresentationFileFormatConverter.Run(task, reporter);
    }

    private static string PptxToPngs(string inputFile, string outputParentDirectory, Action<ImportStats>? onProgress)
    {
        // Via PDF (not direct slide export): same route and slide look as playlist imports.
        var tempDirectory = Path.Combine(Path.GetTempPath(), "VisionScreens-Toolbox-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var pdf = PptxToPdf(inputFile, tempDirectory, onProgress);
            return PdfToPngs(pdf, outputParentDirectory, onProgress,
                folderName: Path.GetFileNameWithoutExtension(inputFile));
        }
        finally
        {
            try { Directory.Delete(tempDirectory, true); }
            catch (Exception e) { Log.Warning(e, "Toolbox: could not delete temp folder {Dir}", tempDirectory); }
        }
    }

    private static string PdfToPngs(string inputFile, string outputParentDirectory, Action<ImportStats>? onProgress,
        string? folderName = null)
    {
        var outputDirectory = UniqueDirectory(outputParentDirectory,
            folderName ?? Path.GetFileNameWithoutExtension(inputFile));
        Directory.CreateDirectory(outputDirectory);

        Log.Debug("Toolbox: PDF -> PNGs: {File} -> {Dir}", inputFile, outputDirectory);
        var ok = PdfRasterizer.Convert(new ImportTask
        {
            InputFile = inputFile,
            OutputDirectory = outputDirectory,
            ExportFileFormat = ImportTask.ExportFileFormatType.PNG
        }, onProgress);
        if (!ok)
            throw new InvalidOperationException("The PDF could not be converted to images.");
        return outputDirectory;
    }

    /// <summary>parent/name, or parent/name (2), (3)... if it already exists, so a run never mixes into old output.</summary>
    internal static string UniqueDirectory(string parent, string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        if (string.IsNullOrWhiteSpace(name)) name = "Converted";

        var candidate = Path.Combine(parent, name);
        for (var i = 2; Directory.Exists(candidate) || File.Exists(candidate); i++)
            candidate = Path.Combine(parent, $"{name} ({i})");
        return candidate;
    }
}
