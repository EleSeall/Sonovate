using Sonovate.CodeTest.Domain;

namespace Sonovate.CodeTest
{
    internal interface ICandidateRepository
    {
        Candidate GetById(string supplierId);
    }
}