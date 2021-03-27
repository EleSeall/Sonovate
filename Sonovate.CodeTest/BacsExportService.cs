using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using Raven.Client.Documents;
using Sonovate.CodeTest.Domain;
using Sonovate.CodeTest.Helpers;
using Sonovate.CodeTest.Interfaces;

namespace Sonovate.CodeTest
{
    public class BacsExportService
    {
        //would suggest moving these to config too.
        private const string SUPPLIER_BACS_EXPORT_NAME = "Supplier_BACSExport.csv";
        private const string PAYMENTS_BACS_EXPORT_NAME = "Agency_BACSExport.csv";

        private readonly IDocumentStore documentStore;
        private readonly IPaymentsRepository PaymentsRepository;
        private readonly ICandidateRepository CandidateRepository;
        private readonly IInvoiceTransactionRepository InvoiceTransactionRepository;
        private readonly IExportFileWriter ExportFileWriter;

        private readonly bool EnableAgencyPayments = false;

        public BacsExportService()
        {
            //moved hard coded url into config
            var docStoreUrl = Application.Settings["DocumentStoreUrl"];
            var docStoreDatabase = Application.Settings["DocumentStoreDatabase"];

            if (string.IsNullOrWhiteSpace(docStoreUrl) || string.IsNullOrWhiteSpace(docStoreDatabase))
            {
                documentStore = new DocumentStore { Urls = new[] { "http://localhost" }, Database = "Export" };
            }
            else
            {
                documentStore = new DocumentStore {Urls = new[]{docStoreUrl}, Database = docStoreDatabase};
            }

            if (Application.Settings["EnableAgencyPayments"] == "true")
            {
                EnableAgencyPayments = true;
            }

            documentStore.Initialize();
            PaymentsRepository = new PaymentsRepository();
            CandidateRepository = new CandidateRepository();
            InvoiceTransactionRepository = new InvoiceTransactionRepository();
            ExportFileWriter = new ExportFileWriter();
        }

        /// <summary>
        /// Application.cs will still call the constructor with no parameters, but this overload could be used for unit testing.
        /// </summary>
        /// <param name="docStore"></param>
        /// <param name="paymentsRepository"></param>
        /// <param name="candidateRepository"></param>
        /// <param name="invoiceTransactionRepository"></param>
        /// <param name="exportFileWriter"></param>
        public BacsExportService(IDocumentStore docStore, 
            IPaymentsRepository paymentsRepository, 
            ICandidateRepository candidateRepository,
            IInvoiceTransactionRepository invoiceTransactionRepository,
            IExportFileWriter exportFileWriter,
            bool enableAgencyPayments)
        {
            documentStore = docStore;
            documentStore.Initialize();
            PaymentsRepository = paymentsRepository;
            CandidateRepository = candidateRepository;
            InvoiceTransactionRepository = invoiceTransactionRepository;
            ExportFileWriter = exportFileWriter;
            EnableAgencyPayments = enableAgencyPayments;
        }

        public async Task ExportZip(BacsExportType bacsExportType)
        {
            if (bacsExportType == BacsExportType.None)
            {
                throw new ArgumentException("No export type provided.");
            }

            var startDate = DateTime.Now.AddMonths(-1);
            var endDate = DateTime.Now;

            switch (bacsExportType)
            {
                case BacsExportType.Agency:
                    if (EnableAgencyPayments)
                    {
                        await SavePayments(startDate, endDate);
                    }
                        
                    break;
                case BacsExportType.Supplier:
                    var supplierPaymentsHelper = new SupplierPaymentsHelper(InvoiceTransactionRepository, CandidateRepository, ExportFileWriter, SUPPLIER_BACS_EXPORT_NAME);
                    supplierPaymentsHelper.SaveSupplierBacsExport(startDate, endDate);
                    break;
                default:
                    //already handled above since there are no other BacsExportTypes currently
                    throw new ArgumentException("Invalid BACS Export Type.");
            }
        }

        private async Task<List<BacsResult>> GetAgencyPayments(DateTime startDate, DateTime endDate)
        {
            var payments = PaymentsRepository.GetBetweenDates(startDate, endDate);
            
            if (!payments.Any())
            {
                throw new InvalidOperationException(string.Format("No agency payments found between dates {0:dd/MM/yyyy} to {1:dd/MM/yyyy}", startDate, endDate));
            }

            var agencies = await GetAgenciesForPayments(payments);

            return BuildAgencyPayments(payments, agencies);
        }

        private async Task<List<Agency>> GetAgenciesForPayments(IList<Payment> payments)
        {
            var agencyIds = payments.Select(x => x.AgencyId).Distinct().ToList();

            using (var session = documentStore.OpenAsyncSession())
            {
                return (await session.LoadAsync<Agency>(agencyIds)).Values.ToList();
            }
        }

        private List<BacsResult> BuildAgencyPayments(IEnumerable<Payment> payments, List<Agency> agencies)
        {
            return (from p in payments
                let agency = agencies.FirstOrDefault(x => x.Id == p.AgencyId)
                where agency != null && agency.BankDetails != null
                let bank = agency.BankDetails
                select new BacsResult
                {
                    AccountName = bank.AccountName,
                    AccountNumber = bank.AccountNumber,
                    SortCode = bank.SortCode,
                    Amount = p.Balance,
                    Ref = string.Format("SONOVATE{0}", p.PaymentDate.ToString("ddMMyyyy"))
                }).ToList();
        }

        /// <summary>
        /// Retrieve agency payments between the supplied dates and export to csv
        /// </summary>
        /// <param name="startDate">start date</param>
        /// <param name="endDate">end date</param>
        /// <returns></returns>
        private async Task SavePayments(DateTime startDate, DateTime endDate)
        {
            var payments = await GetAgencyPayments(startDate, endDate);
            ExportFileWriter.CreateExportFile(PAYMENTS_BACS_EXPORT_NAME, payments);
        }

        
    }
}