using ERPAPI.Data;
using ERPAPI.Model;
using Microsoft.EntityFrameworkCore;

namespace ERPAPI.Services
{
    public class ProjectService : IProjectService
    {
        private readonly AppDbContext _appDbContext;

        public ProjectService(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;
        }

        public async Task<IEnumerable<Project>> GetAllProjects()
        {
            return await _appDbContext.Projects.ToListAsync();
        }
    }
}
