using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERPAPI.Services
{
    public interface IProjectCompletionService
    {
        Task<List<dynamic>> CalculateProjectCompletionPercentages();
    }
}
