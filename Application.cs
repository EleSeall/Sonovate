using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Sonovate.CodeTest.Domain;

namespace Sonovate.CodeTest
{
    public static class Application
    {
        public static void Main()
        {
            var builder = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                { "EnableAgencyPayments", "True" }
            });

            Settings = builder.Build();

            new BacsExportService().ExportZip(BacsExportType.Agency).Wait();
            new BacsExportService().ExportZip(BacsExportType.Supplier).Wait();
        }

        public static IConfigurationRoot Settings { get; private set; }
    }
}
