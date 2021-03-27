using CsvHelper;
using Sonovate.CodeTest.Domain;
using Sonovate.CodeTest.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Sonovate.CodeTest
{
    /// <summary>
    /// Hides CsvWriter dependency from BacsExportService
    /// </summary>
    public class ExportFileWriter : IExportFileWriter
    {
        void IExportFileWriter.CreateExportFile(string fileName, IEnumerable records)
        {
            using (var csv = new CsvWriter(new StreamWriter(new FileStream(fileName, FileMode.Create))))
            {
                csv.WriteRecords(records);
            }
        }
    }
}
