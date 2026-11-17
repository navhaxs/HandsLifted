using System;
using HandsLiftedApp.Importer.FileFormatConvertTaskData;
using HandsLiftedApp.Importer.PDF;

namespace HandsLiftedApp.Core.Services;

/// <summary>Renders a PDF to page images and says whether it finished (ConvertPDF itself swallows failures).</summary>
internal static class PdfRasterizer
{
    public static bool Convert(ImportTask task, Action<ImportStats>? onProgress = null)
    {
        var succeeded = false;
        ConvertPDF.Convert(task, new ImportTaskReporter(stats =>
        {
            if (stats.JobStatus == ImportStats.JobStatusEnum.CompletionSuccess) succeeded = true;
            onProgress?.Invoke(stats);
        }));
        return succeeded;
    }
}
