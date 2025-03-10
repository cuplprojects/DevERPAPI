using ERPAPI.Model;
namespace ERPAPI.Services
{
    public interface ITransactionService
    {
        Task<IEnumerable<Transaction>> GetTransactionsByProjectId(int projectId);
    }
}
