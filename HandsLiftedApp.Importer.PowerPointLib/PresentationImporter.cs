﻿using Syncfusion.Pdf;
using Syncfusion.Presentation;
using Syncfusion.PresentationRenderer;

namespace HandsLiftedApp.Importer.PowerPointLib;

public static class PresentationImporter
{
    public static void Run(string pptxFile)
    {
        string outputFileName = Path.GetFileNameWithoutExtension(pptxFile);
        //Opens the specified presentation
        using (IPresentation presentation = Presentation.Open(pptxFile)) // TODO handle file permission error
        {
            //To set each slide in a pdf page.
            PresentationToPdfConverterSettings settings = new PresentationToPdfConverterSettings();
            settings.ShowHiddenSlides = false;
            settings.EmbedFonts = true;
            //Instance to create pdf document from presentation
            using (PdfDocument doc = PresentationToPdfConverter.Convert(presentation, settings))
            {
                //Saves the pdf document
                MemoryStream stream = new MemoryStream();
                doc.Save(stream);
                
                // serialization was successful - only now do we write to disk
                using (FileStream file = new FileStream(pptxFile + ".pdf", FileMode.Create, FileAccess.Write))
                    stream.WriteTo(file);
            }
        }
    }
}