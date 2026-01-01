using System;
using HandsLiftedApp.Importer.FileFormatConvertTaskData;

namespace HandsLiftedApp.Core.Services
{
    public class ImportTaskReporter(Action<ImportStats> u) : IProgress<ImportStats>
    {
        public void Report(ImportStats value)
        {
            u(value);
        }
    }
}