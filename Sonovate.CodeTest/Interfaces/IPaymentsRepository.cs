using Sonovate.CodeTest.Domain;
using System;
using System.Collections.Generic;

namespace Sonovate.CodeTest
{
    public interface IPaymentsRepository
    {
        IList<Payment> GetBetweenDates(DateTime start, DateTime end);
    }
}