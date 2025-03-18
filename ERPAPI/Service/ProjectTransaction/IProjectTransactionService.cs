namespace ERPAPI.Service.ProjectTransaction
{
    public interface IProjectTransactionService
    {
        Task<IEnumerable<object>> GetProjectTransactionsDataAsync(int projectId, int processId);
    }

}
