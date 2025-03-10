using ERPAPI.Model;

namespace ERPAPI.Services
{
    public interface IProjectProcessService
    {
        Task<IEnumerable<ProjectProcess>> GetProjectProcessesByProjectId(int projectId);
    }
}
