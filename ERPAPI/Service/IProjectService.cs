using ERPAPI.Model;
namespace ERPAPI.Services
{
    public interface IProjectService
    {
        Task<IEnumerable<Project>> GetAllProjects();
    }
}

