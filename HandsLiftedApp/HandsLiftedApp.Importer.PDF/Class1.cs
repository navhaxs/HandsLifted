using PDFiumCore;
using SkiaSharp;
using System.Diagnostics;

namespace HandsLiftedApp.Importer.PDF
{
    // https://github.com/dlemstra/Magick.NET/blob/main/docs/ConvertPDF.md

    // end user must install  Ghostscript x.xx.x for Windows (64 bit)  https://ghostscript.com/releases/gsdnld.html

    public class ConvertPDF
    {

        const int VIEWPORT_X = 0;
        const int VIEWPORT_Y = 0;

        public static void Convert(string inputPdfFile, string outputDir)
        {
            try
            {

                fpdfview.FPDF_InitLibrary();

                // White color.
                uint color = uint.MaxValue;
                // Load the document.
                var document = fpdfview.FPDF_LoadDocument(inputPdfFile, null);

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

                    fpdfview.FPDF_RenderPageBitmapWithMatrix(bitmap, page, matrix, clipping, (int)RenderFlags.RenderAnnotations);

                    // create an empty bitmap
                    using (SKBitmap skBitmap = new SKBitmap())
                    {
                        // install the pixels with the color type of the pixel data
                        var info = new SKImageInfo(scaledPageWidth, scaledPageHeight, SKImageInfo.PlatformColorType, SKAlphaType.Unpremul);

                        var scan0 = fpdfview.FPDFBitmapGetBuffer(bitmap);
                        var stride = fpdfview.FPDFBitmapGetStride(bitmap);

                        skBitmap.InstallPixels(info, scan0, info.RowBytes, delegate { }, null);

                        string slideOutFile = Path.Join(outputDir, "Slide." + i + ".bmp");

                        using (Stream s = File.Create(slideOutFile))
                        {
                            using (SKData d = SKImage.FromBitmap(skBitmap).Encode(SKEncodedImageFormat.Jpeg, 100))
                            {
                                d.SaveTo(s);
                            }
                        }

                        fpdfview.FPDFBitmapDestroy(bitmap);
                    }

                    fpdfview.FPDF_ClosePage(page);
                }
                fpdfview.FPDF_CloseDocument(document);
                fpdfview.FPDF_DestroyLibrary();
                GC.Collect();
            }
            catch (Exception e)
            {
                Debug.Print(e.ToString());
            }

        }
    }
}