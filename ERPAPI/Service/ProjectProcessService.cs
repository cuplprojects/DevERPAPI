using ERPAPI.Data;
using ERPAPI.Model;
using Microsoft.EntityFrameworkCore;

namespace ERPAPI.Services
{
    public class ProjectProcessService : IProjectProcessService
    {
        private readonly AppDbContext _appDbContext;

        public ProjectProcessService(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;
        }

        public async Task<IEnumerable<ProjectProcess>> GetProjectProcessesByProjectId(int projectId)
        {
            return await _appDbContext.ProjectProcesses
                .Where(p => p.ProjectId == projectId)
                .ToListAsync();
        }
    }
}
