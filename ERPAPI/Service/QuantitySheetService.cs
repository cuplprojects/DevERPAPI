using ERPAPI.Data;
using ERPAPI.Model;
using Microsoft.EntityFrameworkCore;

namespace ERPAPI.Services
{
    public class QuantitySheetService : IQuantitySheetService
    {
        private readonly AppDbContext _appDbContext;

        public QuantitySheetService(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;
        }

        public async Task<IEnumerable<QuantitySheet>> GetQuantitySheetsByProjectId(int projectId)
        {
            return await _appDbContext.QuantitySheets
                .Where(qs => qs.ProjectId == projectId)
                .ToListAsync();
        }
    }
}
