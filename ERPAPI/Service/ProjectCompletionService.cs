using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ERPAPI.Model;

namespace ERPAPI.Services
{
    public class ProjectCompletionService : IProjectCompletionService
    {
        private readonly IProjectService _projectService;
        private readonly IProjectProcessService _projectProcessService;
        private readonly IQuantitySheetService _quantitySheetService;
        private readonly ITransactionService _transactionService;


        public ProjectCompletionService(
            IProjectService projectService,
            IProjectProcessService projectProcessService,
            IQuantitySheetService quantitySheetService,
            ITransactionService transactionService)
        {
            _projectService = projectService;
            _projectProcessService = projectProcessService;
            _quantitySheetService = quantitySheetService;
            _transactionService = transactionService;
        }

        public async Task<List<dynamic>> CalculateProjectCompletionPercentages()
        {
            var projects = await _projectService.GetAllProjects();
            var projectCompletionPercentages = new List<dynamic>();

            foreach (var project in projects)
            {
                var projectId = project.ProjectId;
                var projectProcesses = await _projectProcessService.GetProjectProcessesByProjectId(projectId);
                var quantitySheets = await _quantitySheetService.GetQuantitySheetsByProjectId(projectId);
                var transactions = await _transactionService.GetTransactionsByProjectId(projectId);
                var totalLotPercentages = new Dictionary<string, double>();
                var lotQuantities = new Dictionary<string, double>();
                double projectTotalQuantity = 0;

                foreach (var quantitySheet in quantitySheets)
                {
                    var processIdWeightage = new Dictionary<int, double>();
                    double totalWeightageSum = 0;

                    // Exclude ProcessId 14 from weightage calculation
                    foreach (var processId in quantitySheet.ProcessId)
                    {
                        if (processId == 14) continue; // Skip ProcessId 14

                        var process = projectProcesses.FirstOrDefault(p => p.ProcessId == processId);
                        if (process != null)
                        {
                            processIdWeightage[processId] = Math.Round(process.Weightage, 2);
                            totalWeightageSum += process.Weightage;
                        }
                    }

                    if (totalWeightageSum < 100)
                    {
                        double deficit = 100 - totalWeightageSum;
                        double adjustment = deficit / processIdWeightage.Count;
                        foreach (var key in processIdWeightage.Keys.ToList())
                        {
                            processIdWeightage[key] = Math.Round(processIdWeightage[key] + adjustment, 2);
                        }
                    }

                    double completedWeightageSum = 0;

                    // Exclude ProcessId 14 from the completed process calculation
                    foreach (var kvp in processIdWeightage)
                    {
                        var processId = kvp.Key;
                        var weightage = kvp.Value;
                        var completedProcess = transactions
                            .Any(t => t.QuantitysheetId == quantitySheet.QuantitySheetId
                                      && t.ProcessId == processId
                                      && t.Status == 2);

                        if (completedProcess)
                        {
                            completedWeightageSum += weightage;
                        }
                    }

                    double lotPercentage = Math.Round(quantitySheet.PercentageCatch * (completedWeightageSum / 100), 2);
                    var lotNumber = quantitySheet.LotNo;

                    totalLotPercentages[lotNumber] = Math.Round(totalLotPercentages.GetValueOrDefault(lotNumber) + lotPercentage, 2);
                    lotQuantities[lotNumber] = lotQuantities.GetValueOrDefault(lotNumber) + quantitySheet.Quantity;
                    projectTotalQuantity += quantitySheet.Quantity;
                }

                double totalProjectLotPercentage = 0;

                // Exclude ProcessId 14 when calculating the total project completion percentage
                foreach (var lot in totalLotPercentages)
                {
                    var lotNumber = lot.Key;
                    var quantity = lotQuantities[lotNumber];
                    var lotWeightage = projectTotalQuantity > 0 ? (quantity / projectTotalQuantity) * 100 : 0;

                    totalProjectLotPercentage += totalLotPercentages[lotNumber] * (lotWeightage / 100);
                }

                totalProjectLotPercentage = Math.Round(totalProjectLotPercentage, 2);

                projectCompletionPercentages.Add(new
                {
                    ProjectId = projectId,
                    CompletionPercentage = totalProjectLotPercentage,
                    ProjectTotalQuantity = projectTotalQuantity
                });
            }

            return projectCompletionPercentages;
        }
    }
}
