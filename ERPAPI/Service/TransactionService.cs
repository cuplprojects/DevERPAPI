using ERPAPI.Data;
using ERPAPI.Model;
using Microsoft.EntityFrameworkCore;

namespace ERPAPI.Services
{
    public class TransactionService : ITransactionService
    {
        private readonly AppDbContext _appDbContext;

        public TransactionService(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;
        }

        public async Task<IEnumerable<Transaction>> GetTransactionsByProjectId(int projectId)
        {
            return await _appDbContext.Transaction
                .Where(t => t.ProjectId == projectId)
                .ToListAsync();
        }
    }
}
