using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Sonovate.CodeTest.Interfaces
{
    public interface IExportFileWriter
    {

        void CreateExportFile(string fileName, IEnumerable records);

    }
}
