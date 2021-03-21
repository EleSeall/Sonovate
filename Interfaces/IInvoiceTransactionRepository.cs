using Sonovate.CodeTest.Domain;
using System;
using System.Collections.Generic;

namespace Sonovate.CodeTest
{
    internal interface IInvoiceTransactionRepository
    {
        List<InvoiceTransaction> GetBetweenDates(DateTime startDate, DateTime endDate);
    }
}