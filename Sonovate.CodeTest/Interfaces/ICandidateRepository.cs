using Sonovate.CodeTest.Domain;

namespace Sonovate.CodeTest
{
    public interface ICandidateRepository
    {
        Candidate GetById(string supplierId);
    }
}