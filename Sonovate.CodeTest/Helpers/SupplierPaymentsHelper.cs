using Sonovate.CodeTest.Domain;
using Sonovate.CodeTest.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sonovate.CodeTest.Helpers
{
    public class SupplierPaymentsHelper
    {
        private const string NOT_AVAILABLE = "NOT AVAILABLE";

        private readonly string ExportFileName;

        private readonly IInvoiceTransactionRepository InvoiceTransactionRepository;
        private readonly ICandidateRepository CandidateRepository;
        private readonly IExportFileWriter ExportFileWriter;

        public SupplierPaymentsHelper(IInvoiceTransactionRepository invoiceTransactionRepository, 
            ICandidateRepository candidateRepository,
            IExportFileWriter exportFileWriter,
            string exportFileName = "Supplier_BACSExport.csv")
        {
            InvoiceTransactionRepository = invoiceTransactionRepository;
            CandidateRepository = candidateRepository;
            ExportFileWriter = exportFileWriter;
            ExportFileName = exportFileName;
        }


        public SupplierBacsExport GetSupplierPayments(DateTime startDate, DateTime endDate)
        {
            var candidateInvoiceTransactions = InvoiceTransactionRepository.GetBetweenDates(startDate, endDate);

            if (!candidateInvoiceTransactions.Any())
            {
                throw new InvalidOperationException(string.Format("No supplier invoice transactions found between dates {0} to {1}", startDate, endDate));
            }

            return CreateCandidateBacxExportFromSupplierPayments(candidateInvoiceTransactions);
        }

        public SupplierBacsExport CreateCandidateBacxExportFromSupplierPayments(IList<InvoiceTransaction> supplierPayments)
        {
            var candidateBacsExport = new SupplierBacsExport
            {
                SupplierPayment = new List<SupplierBacs>()
            };

            candidateBacsExport.SupplierPayment = BuildSupplierPayments(supplierPayments);

            return candidateBacsExport;
        }

        public List<SupplierBacs> BuildSupplierPayments(IEnumerable<InvoiceTransaction> invoiceTransactions)
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

                var invoiceRef = string.IsNullOrEmpty(transactionGroup.First().InvoiceRef)
                    ? NOT_AVAILABLE
                    : transactionGroup.First().InvoiceRef;

                var paymentRef = string.Format("SONOVATE{0}",
                    transactionGroup.First()?.InvoiceDate.GetValueOrDefault().ToString("ddMMyyyy"));

                result.AccountName = bank.AccountName;
                result.AccountNumber = bank.AccountNumber;
                result.SortCode = bank.SortCode;
                result.PaymentAmount = transactionGroup.Sum(invoiceTransaction => invoiceTransaction.Gross);
                result.InvoiceReference = invoiceRef;
                result.PaymentReference = paymentRef;

                results.Add(result);
            }

            return results;
        }

        /// <summary>
        /// Retrieve supplier payments between the supplied dates and export to csv file
        /// </summary>
        /// <param name="startDate">start date</param>
        /// <param name="endDate">end date</param>
        public void SaveSupplierBacsExport(DateTime startDate, DateTime endDate)
        {
            var supplierBacsExport = GetSupplierPayments(startDate, endDate);
            ExportFileWriter.CreateExportFile(ExportFileName, supplierBacsExport.SupplierPayment);
        }
    }
}
