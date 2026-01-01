using PDFiumCore;
using SkiaSharp;
using System.Diagnostics;
using HandsLiftedApp.Importer.FileFormatConvertTaskData;

namespace HandsLiftedApp.Importer.PDF
{
    // https://github.com/dlemstra/Magick.NET/blob/main/docs/ConvertPDF.md
    // end user must install  Ghostscript x.xx.x for Windows (64 bit)  https://ghostscript.com/releases/gsdnld.html

    public class ConvertPDF
    {
        const int VIEWPORT_X = 0;
        const int VIEWPORT_Y = 0;

        public static void Convert(ImportTask task, IProgress<ImportStats>? progress = null)
        {
            ImportStats stats = new ImportStats() { Task = task };
            
            try
            {
                fpdfview.FPDF_InitLibrary();
            
                // White color.
                uint color = uint.MaxValue;
                // Load the document.
                var document = fpdfview.FPDF_LoadDocument(task.InputFile, null);
            
                if (document == null)
                {
                    // throw error
                }
            
                var pageCount = fpdfview.FPDF_GetPageCount(document);
            
                for (int i = 0; i < pageCount; i++)
                {
                    double pageWidth = 0;
                    double pageHeight = 0;
                    var page = fpdfview.FPDF_LoadPage(document, i);
                    fpdfview.FPDF_GetPageSizeByIndex(document, i, ref pageWidth, ref pageHeight);
                    float scale = Math.Max((int)(1920 / pageHeight), (int)(1080 / pageWidth));
                    int scaledPageWidth = (int)(pageWidth * scale);
                    int scaledPageHeight = (int)(pageHeight * scale);
            
                    var bitmap = fpdfview.FPDFBitmapCreateEx(
                        scaledPageWidth,
                        scaledPageHeight,
                        (int)FPDFBitmapFormat.BGRA,
                        IntPtr.Zero,
                        0);
            
                    if (bitmap == null)
                        throw new Exception("failed to create a bitmap object");
            
                    // Leave out if you want to make the background transparent.
                    fpdfview.FPDFBitmapFillRect(bitmap, 0, 0, (int)pageWidth, (int)pageHeight, color);
            
                    // |          | a b 0 |
                    // | matrix = | c d 0 |
                    // |          | e f 1 |
                    using var matrix = new FS_MATRIX_();
                    using var clipping = new FS_RECTF_();
            
                    matrix.A = scale;
                    matrix.B = 0;
                    matrix.C = 0;
                    matrix.D = scale;
                    matrix.E = (float)-VIEWPORT_X;
                    matrix.F = (float)-VIEWPORT_Y;
            
                    clipping.Left = 0;
                    clipping.Right = (float)scaledPageWidth;
                    clipping.Bottom = 0;
                    clipping.Top = (float)scaledPageHeight;
            
                    fpdfview.FPDF_RenderPageBitmapWithMatrix(bitmap, page, matrix, clipping,
                        (int)RenderFlags.RenderAnnotations);
            
                    // create an empty bitmap
                    using (SKBitmap skBitmap = new SKBitmap())
                    {
                        // install the pixels with the color type of the pixel data
                        var info = new SKImageInfo(scaledPageWidth, scaledPageHeight, OperatingSystem.IsMacOS() ? SKColorType.Bgra8888 : SKImageInfo.PlatformColorType,
                            SKAlphaType.Unpremul);
            
                        var scan0 = fpdfview.FPDFBitmapGetBuffer(bitmap);
                        var stride = fpdfview.FPDFBitmapGetStride(bitmap);
            
                        skBitmap.InstallPixels(info, scan0, info.RowBytes, delegate { }, null);
            
                        // TODO pad leading 0's
                        int maxPageNumberDigits = (int)Math.Floor(Math.Log10(pageCount) + 1);
                        string thisPageNumberPadded = (i + 1).ToString(new string('0', maxPageNumberDigits));
                        string slideOutFile = Path.Join(task.OutputDirectory, $"Slide.{thisPageNumberPadded}.png");
            
                        using (Stream s = File.Create(slideOutFile))
                        {
                            using (SKData d = SKImage.FromBitmap(skBitmap).Encode(SKEncodedImageFormat.Png, 100))
                            {
                                d.SaveTo(s);
                            }
                        }
            
                        fpdfview.FPDFBitmapDestroy(bitmap);
                    }
            
                    fpdfview.FPDF_ClosePage(page);

                    if (progress != null)
                    {
                        stats.JobPercentage = (double)i / pageCount * 100;
                        stats.StatusMessage = $"Exporting slide {i + 1} of {pageCount}";
                        progress.Report(stats);
                    }
                }
            
                fpdfview.FPDF_CloseDocument(document);
                fpdfview.FPDF_DestroyLibrary();
                GC.Collect();
            
                if (progress != null)
                {
                    stats.JobStatus = ImportStats.JobStatusEnum.CompletionSuccess;
                    stats.CompletionTime = DateTime.Now;
                    stats.JobPercentage = 100d;
                    progress.Report(stats);
                }
            }
            catch (Exception e)
            {
                Debug.Print(e.ToString());
                
                stats.JobStatus = ImportStats.JobStatusEnum.CompletionFailure;
                stats.CompletionTime = DateTime.Now;

                if (progress != null)
                    progress.Report(stats);
            }
        }
    }
}