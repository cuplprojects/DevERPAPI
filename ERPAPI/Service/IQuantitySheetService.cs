using ERPAPI.Model;

namespace ERPAPI.Services
{
    public interface IQuantitySheetService
    {
        Task<IEnumerable<QuantitySheet>> GetQuantitySheetsByProjectId(int projectId);
    }
}
