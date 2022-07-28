using ImageMagick;

namespace HandsLiftedApp.Importer.PDF
{
    // https://github.com/dlemstra/Magick.NET/blob/main/docs/ConvertPDF.md

    // end user must install  Ghostscript x.xx.x for Windows (64 bit)  https://ghostscript.com/releases/gsdnld.html
    public class Class1
    {

        public void Convert()
        {
            var settings = new MagickReadSettings();
            // Settings the density to 300 dpi will create an image with a better quality
            settings.Density = new Density(300, 300);

            using (var images = new MagickImageCollection())
            {
                // Add all the pages of the pdf file to the collection
                images.Read(@"c:\path\to\Snakeware.pdf", settings);

                var page = 1;
                foreach (var image in images)
                {
                    // Write page to file that contains the page number
                    image.Write(@"c:\path\to\Snakeware.Page" + page + ".png");
                    // Writing to a specific format works the same as for a single image
                    image.Format = MagickFormat.Ptif;
                    image.Write(@"c:\path\to\Snakeware.Page" + page + ".tif");
                    page++;
                }
            }
        }
    }
}