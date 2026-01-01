// See https://aka.ms/new-console-template for more information

using HandsLiftedApp.Importer.FileFormatConvertTaskData;
using HandsLiftedApp.Importer.PDF;

Console.WriteLine("Hello, World!");

var inputFile = @"D:\PDFiumCore-master\src\PDFiumCoreDemo\bin\Debug\net6.0\pdf-sample.pdf";
ConvertPDF.Convert(new ImportTask
{
    InputFile = inputFile,
    OutputDirectory = Path.GetDirectoryName(inputFile),
    ExportFileFormat = ImportTask.ExportFileFormatType.PNG
});
