using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sonovate.CodeTest.Helpers;
using Moq;
using Sonovate.CodeTest.Interfaces;
using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sonovate.CodeTest.UnitTests
{
    [TestClass]
    public class SupplierPaymentsHelperTests
    {
        private SupplierPaymentsHelper SupplierPaymentsHelper;

        private Domain.Candidate TestCandidate = new Domain.Candidate {  BankDetails = new Domain.BankDetails { AccountName = "test", AccountNumber = "12345678", SortCode = "000000" } };
        private Domain.Candidate Candidate2 = new Domain.Candidate { BankDetails = new Domain.BankDetails { AccountName = "fives", AccountNumber = "55555555", SortCode = "555555" } };

        private Domain.InvoiceTransaction TestInvoiceTransaction = new Domain.InvoiceTransaction { Gross = 123.45M, InvoiceDate = new DateTime(2021, 2, 25), InvoiceId = "1", InvoiceRef = "TestRef", SupplierId = "1" };
        private Domain.InvoiceTransaction TestInvoiceTransaction2 = new Domain.InvoiceTransaction { Gross = 45678.10M, InvoiceDate = new DateTime(2021, 3, 26), InvoiceId = "2", InvoiceRef = "TestRef2", SupplierId = "1" };
        private Domain.InvoiceTransaction TestInvoiceTransaction3 = new Domain.InvoiceTransaction { Gross = 777777.77M, InvoiceDate = new DateTime(2021, 3, 27), InvoiceId = "3", InvoiceRef = "TestRef3", SupplierId = "1" };

        [TestInitialize]
        public void Setup()
        {
            var invoiceTransactionRepository = new Mock<IInvoiceTransactionRepository>();
            invoiceTransactionRepository.Setup(x => x.GetBetweenDates(It.IsAny<DateTime>(), It.IsAny<DateTime>())).Returns(new List<Domain.InvoiceTransaction> { TestInvoiceTransaction, TestInvoiceTransaction2, TestInvoiceTransaction3 });

            var candidateRepository = new Mock<ICandidateRepository>();
            candidateRepository.Setup(x => x.GetById("1")).Returns(TestCandidate);
            candidateRepository.Setup(x => x.GetById("2")).Returns(Candidate2);

            var exportFileWriter = new Mock<IExportFileWriter>();
            exportFileWriter.Setup(x => x.CreateExportFile(It.IsAny<string>(), It.IsAny<IEnumerable>()));

            SupplierPaymentsHelper = new SupplierPaymentsHelper(invoiceTransactionRepository.Object, candidateRepository.Object, exportFileWriter.Object, "ExportFileName.csv");
        }

        [TestMethod]
        public void SupplierPaymentsHelper_BuildSupplierPayments_ValidSupplier_Generates3Results()
        {
            //set up 
            var testInvoiceTransactions = new List<Domain.InvoiceTransaction> { TestInvoiceTransaction, TestInvoiceTransaction2, TestInvoiceTransaction3 };
            var result = SupplierPaymentsHelper.BuildSupplierPayments(testInvoiceTransactions);

            Assert.IsNotNull(result);
            //expect 3 results
            Assert.AreEqual(3, result.Count);
            Assert.IsTrue(result.All(x => x.PaymentReference.StartsWith("SONOVATE")));
            //should all have matched the single candidate so have the same bank details
            Assert.IsTrue(result.All(x => x.AccountNumber == "12345678"));

        }

        [TestMethod]
        public void SupplierPaymentsHelper_BuildSupplierPayments_MixedSupplier_Generates3Results()
        {
            //set up 
            TestInvoiceTransaction2.SupplierId = "2";
            var testInvoiceTransactions = new List<Domain.InvoiceTransaction> { TestInvoiceTransaction, TestInvoiceTransaction2, TestInvoiceTransaction3 };
            var result = SupplierPaymentsHelper.BuildSupplierPayments(testInvoiceTransactions);

            Assert.IsNotNull(result);
            //expect 3 results
            Assert.AreEqual(3, result.Count);
            Assert.IsTrue(result.All(x => x.PaymentReference.StartsWith("SONOVATE")));
            //we should have a mix of bank account details
            Assert.IsFalse(result.All(x => x.AccountNumber == "12345678"));

        }

        [TestMethod]
        public void SupplierPaymentsHelper_BuildSupplierPayments_SingleInvoices_Generates1Result()
        {
            var total = TestInvoiceTransaction.Gross + TestInvoiceTransaction2.Gross + TestInvoiceTransaction3.Gross;

            //set up 
            TestInvoiceTransaction2.InvoiceId = "1";
            TestInvoiceTransaction3.InvoiceId = "1";
            var testInvoiceTransactions = new List<Domain.InvoiceTransaction> { TestInvoiceTransaction, TestInvoiceTransaction2, TestInvoiceTransaction3 };
            var result = SupplierPaymentsHelper.BuildSupplierPayments(testInvoiceTransactions);

            Assert.IsNotNull(result);
            //expect 1 grouped
            Assert.AreEqual(1, result.Count);
            //we should have a sum of all values
            Assert.AreEqual(total, result.First().PaymentAmount);

        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException), "Could not load candidate with Id 5")]
        public void SupplierPaymentsHelper_BuildSupplierPayments_InvalidSupplier_ThrowsInvalidOperationException()
        {
            //set up 
            TestInvoiceTransaction.SupplierId = "5";
            var testInvoiceTransactions = new List<Domain.InvoiceTransaction> { TestInvoiceTransaction, TestInvoiceTransaction2, TestInvoiceTransaction3 };
            
            //should throw exception since there is no candidate with id 5
            var result = SupplierPaymentsHelper.BuildSupplierPayments(testInvoiceTransactions);
        }

        [TestMethod]
        public void SupplierPaymentsHelper_BuildSupplierPayments_BlankInvoiceNumber_Ignored()
        {
            //set up 
            TestInvoiceTransaction.InvoiceId = "";
            var testInvoiceTransactions = new List<Domain.InvoiceTransaction> { TestInvoiceTransaction, TestInvoiceTransaction2, TestInvoiceTransaction3 };

            var result = SupplierPaymentsHelper.BuildSupplierPayments(testInvoiceTransactions);

            Assert.IsNotNull(result);
            //blank invoice id shouldn't affect it - expect 3 results
            Assert.AreEqual(3, result.Count);
        }

        [TestMethod]
        public void SupplierPaymentsHelper_BuildSupplierPayments_InvoiceRefBlank_InvoiceReferenceSetToNotAvailable()
        {
            //set up 
            TestInvoiceTransaction.InvoiceRef = "";
            var testInvoiceTransactions = new List<Domain.InvoiceTransaction> { TestInvoiceTransaction, TestInvoiceTransaction2, TestInvoiceTransaction3 };

            var result = SupplierPaymentsHelper.BuildSupplierPayments(testInvoiceTransactions);

            //should be a result with the reference of not available
            Assert.AreEqual(1, result.Count(x => x.InvoiceReference == "NOT AVAILABLE"));
        }
    }
}
