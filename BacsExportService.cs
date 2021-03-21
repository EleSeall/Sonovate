using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using Raven.Client.Documents;
using Sonovate.CodeTest.Domain;
using Sonovate.CodeTest.Interfaces;

namespace Sonovate.CodeTest
{
    public class BacsExportService
    {
        private const string NOT_AVAILABLE = "NOT AVAILABLE";

        //would suggest moving these to config too.
        private const string SUPPLIER_BACS_EXPORT_NAME = "Supplier_BACSExport.csv";
        private const string PAYMENTS_BACS_EXPORT_NAME = "Agency_BACSExport.csv";

        private readonly IDocumentStore documentStore;
        private readonly IPaymentsRepository PaymentsRepository;
        private readonly ICandidateRepository CandidateRepository;
        private readonly IInvoiceTransactionRepository InvoiceTransactionRepository;
        private readonly IExportFileWriter ExportFileWriter;

        public BacsExportService()
        {
            //moved hard coded url into config
            var docStoreUrl = Application.Settings["DocumentStoreUrl"];
            var docStoreDatabase = Application.Settings["DocumentStoreDatabase"];
            documentStore = new DocumentStore {Urls = new[]{docStoreUrl}, Database = docStoreDatabase};
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
        internal BacsExportService(IDocumentStore docStore, 
            IPaymentsRepository paymentsRepository, 
            ICandidateRepository candidateRepository,
            IInvoiceTransactionRepository invoiceTransactionRepository,
            IExportFileWriter exportFileWriter)
        {
            documentStore = docStore;
            documentStore.Initialize();
            PaymentsRepository = paymentsRepository;
            CandidateRepository = candidateRepository;
            InvoiceTransactionRepository = invoiceTransactionRepository;
            ExportFileWriter = exportFileWriter;
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
                    if (Application.Settings["EnableAgencyPayments"] == "true")
                    {
                        await SavePayments(startDate, endDate);
                    }
                        
                    break;
                case BacsExportType.Supplier:
                    SaveSupplierBacsExport(startDate, endDate);
                    break;
                default:
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

        private SupplierBacsExport GetSupplierPayments(DateTime startDate, DateTime endDate)
        {
            var candidateInvoiceTransactions = InvoiceTransactionRepository.GetBetweenDates(startDate, endDate);

            if (!candidateInvoiceTransactions.Any())
            {
                throw new InvalidOperationException(string.Format("No supplier invoice transactions found between dates {0} to {1}", startDate, endDate));
            }

            return CreateCandidateBacxExportFromSupplierPayments(candidateInvoiceTransactions);
        }
        private SupplierBacsExport CreateCandidateBacxExportFromSupplierPayments(IList<InvoiceTransaction> supplierPayments)
        {
            var candidateBacsExport = new SupplierBacsExport
            {
                SupplierPayment = new List<SupplierBacs>()
            };

            candidateBacsExport.SupplierPayment = BuildSupplierPayments(supplierPayments);
                
            return candidateBacsExport;
        }

        private List<SupplierBacs> BuildSupplierPayments(IEnumerable<InvoiceTransaction> invoiceTransactions)
        {
            var results = new List<SupplierBacs>();

            var transactionsByCandidateAndInvoiceId = invoiceTransactions.GroupBy(transaction => new
            {
                transaction.InvoiceId,
                transaction.SupplierId
            });

            foreach (var transactionGroup in transactionsByCandidateAndInvoiceId)
            {
                var candidate = CandidateRepository.GetById(transactionGroup.Key.SupplierId);

                if (candidate == null)
                {
                    throw new InvalidOperationException(string.Format("Could not load candidate with Id {0}",
                        transactionGroup.Key.SupplierId));
                }

                var result = new SupplierBacs();
                
                var bank = candidate.BankDetails;

                result.AccountName = bank.AccountName;
                result.AccountNumber = bank.AccountNumber;
                result.SortCode = bank.SortCode;
                result.PaymentAmount = transactionGroup.Sum(invoiceTransaction => invoiceTransaction.Gross);
                result.InvoiceReference = string.IsNullOrEmpty(transactionGroup.FirstOrDefault()?.InvoiceRef)
                    ? NOT_AVAILABLE
                    : transactionGroup.First().InvoiceRef;
                result.PaymentReference = string.Format("SONOVATE{0}",
                    transactionGroup.First()?.InvoiceDate.GetValueOrDefault().ToString("ddMMyyyy"));

                results.Add(result);
            }

            return results;
        }

        /// <summary>
        /// Retrieve supplier payments between the supplied dates and export to csv file
        /// </summary>
        /// <param name="startDate">start date</param>
        /// <param name="endDate">end date</param>
        private void SaveSupplierBacsExport(DateTime startDate, DateTime endDate)
        {
            var supplierBacsExport = GetSupplierPayments(startDate, endDate);
            ExportFileWriter.CreateExportFile(SUPPLIER_BACS_EXPORT_NAME, supplierBacsExport.SupplierPayment);
        }
    }
}