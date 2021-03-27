using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Raven.Client.Documents;
using Sonovate.CodeTest.Helpers;
using Sonovate.CodeTest.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Sonovate.CodeTest.UnitTests
{
    [TestClass]
    public class BacsExportServiceTests
    {
        private BacsExportService BacsExportService;

        private Domain.Candidate TestCandidate = new Domain.Candidate { BankDetails = new Domain.BankDetails { AccountName = "test", AccountNumber = "12345678", SortCode = "000000" } };

        private Domain.InvoiceTransaction TestInvoiceTransaction = new Domain.InvoiceTransaction { Gross = 123.45M, InvoiceDate = new DateTime(2021, 2, 25), InvoiceId = "1", InvoiceRef = "TestRef", SupplierId = "1" };
        private Domain.InvoiceTransaction TestInvoiceTransaction2 = new Domain.InvoiceTransaction { Gross = 45678.10M, InvoiceDate = new DateTime(2021, 3, 26), InvoiceId = "2", InvoiceRef = "TestRef2", SupplierId = "1" };
        private Domain.InvoiceTransaction TestInvoiceTransaction3 = new Domain.InvoiceTransaction { Gross = 777777.77M, InvoiceDate = new DateTime(2021, 3, 27), InvoiceId = "3", InvoiceRef = "TestRef3", SupplierId = "1" };

        private Domain.Payment TestPayment = new Domain.Payment { AgencyId = "1", Balance = 123.45M, PaymentDate = new DateTime(2021, 03, 25) };

        [TestInitialize]
        public void Setup()
        {
            var invoiceTransactionRepository = new Mock<IInvoiceTransactionRepository>();
            invoiceTransactionRepository.Setup(x => x.GetBetweenDates(It.IsAny<DateTime>(), It.IsAny<DateTime>())).Returns(new List<Domain.InvoiceTransaction> { TestInvoiceTransaction, TestInvoiceTransaction2, TestInvoiceTransaction3 });

            var candidateRepository = new Mock<ICandidateRepository>();
            candidateRepository.Setup(x => x.GetById(It.IsAny<string>())).Returns(TestCandidate);

            var exportFileWriter = new Mock<IExportFileWriter>();
            exportFileWriter.Setup(x => x.CreateExportFile(It.IsAny<string>(), It.IsAny<IEnumerable>()));

            var docStore = new Mock<IDocumentStore>();

            var paymentsRepository = new Mock<IPaymentsRepository>();
            paymentsRepository.Setup(x => x.GetBetweenDates(It.IsAny<DateTime>(), It.IsAny<DateTime>())).Returns(new List<Domain.Payment> { TestPayment });

            BacsExportService = new BacsExportService(docStore.Object,
                paymentsRepository.Object,
                candidateRepository.Object,
                invoiceTransactionRepository.Object,
                exportFileWriter.Object,
                true
                );
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException), "No export type provided.")]
        public async Task BacsExportService_ExportZip_InvalidExportType_ThrowsArgumentException()
        {
            //should throw exception
            await BacsExportService.ExportZip(Domain.BacsExportType.None);        
        }

        [TestMethod]
        public async Task BacsExportService_ExportZip_SupplierExportType_NoException()
        {
            await BacsExportService.ExportZip(Domain.BacsExportType.Supplier);
        }


    }
}
