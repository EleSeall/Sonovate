using System;

namespace Sonovate.CodeTest.Domain
{
    public class InvoiceTransaction
    {
        public DateTime? InvoiceDate { get; set; }
        public string InvoiceId { get; set; }
        public string SupplierId { get; set; }
        public decimal Gross { get; set; }
        public string InvoiceRef { get; set; }
    }
}