using ERPAPI.Data;
using ERPAPI.Model;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace ERPAPI.Service
{
    public class ProcessService
    {
        private readonly AppDbContext _context;

        public ProcessService(AppDbContext context)
        {
            _context = context;
        }

        public void ProcessCatch(QuantitySheet catchData)
        {
            Console.WriteLine("Starting ProcessCatch for catchData: " + Newtonsoft.Json.JsonConvert.SerializeObject(catchData));

            // Fetch the project processes related to the catchData's project
            var projectProcesses = _context.ProjectProcesses
                .Where(pp => pp.ProjectId == catchData.ProjectId)
                .Select(pp => new
                {
                    pp.ProcessId,
                    ProcessName = _context.Processes
                        .Where(p => p.Id == pp.ProcessId)
                        .Select(p => p.Name)
                        .FirstOrDefault()
                })
                .ToList();

            Console.WriteLine("Fetched projectProcesses: " + Newtonsoft.Json.JsonConvert.SerializeObject(projectProcesses));

            // Clear existing ProcessIds for fresh processing
            catchData.ProcessId.Clear();
            Console.WriteLine("Cleared existing ProcessId. Current ProcessId: " + Newtonsoft.Json.JsonConvert.SerializeObject(catchData.ProcessId));

            // Identify ProcessIds for CTP, Offset Printing, and Digital Printing
            var ctpProcessId = projectProcesses.FirstOrDefault(p => p.ProcessName == "CTP")?.ProcessId;
            var offsetPrintingProcessId = projectProcesses.FirstOrDefault(p => p.ProcessName == "Offset Printing")?.ProcessId;
            var digitalPrintingProcessId = projectProcesses.FirstOrDefault(p => p.ProcessName == "Digital Printing")?.ProcessId;
            var cuttingProcessId = projectProcesses.FirstOrDefault(p => p.ProcessName == "Cutting")?.ProcessId;

            Console.WriteLine("Identified ProcessIds - CTP: " + ctpProcessId + ", Offset: " + offsetPrintingProcessId + ", Digital: " + digitalPrintingProcessId + ", Cutting : " + cuttingProcessId + "");
            Console.WriteLine(catchData.ProjectId);

            // Fetch the project to get the threshold JSON string
            var project = _context.Projects
                .Where(p => p.ProjectId == catchData.ProjectId)
                .FirstOrDefault();

            if (project == null)
            {
                Console.WriteLine($"Project not found for ProjectId {catchData.ProjectId}");
                return;
            }
            Console.WriteLine(project);

            // Handle null or empty QuantityThreshold
            if (string.IsNullOrWhiteSpace(project.QuantityThreshold))
            {
                Console.WriteLine("QuantityThreshold is null or empty. Skipping threshold processing.");
                return;
            }

            List<Threshold> thresholds;
            try
            {
                // Parse the threshold JSON string
                thresholds = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Threshold>>(project.QuantityThreshold);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to deserialize QuantityThreshold: " + ex.Message);
                return;
            }

            if (thresholds == null || !thresholds.Any())
            {
                Console.WriteLine("No valid thresholds found in project.QuantityThreshold.");

                // If no valid thresholds are found, add all ProcessIds except for Digital Printing
                foreach (var process in projectProcesses)
                {
                    if (process.ProcessId != digitalPrintingProcessId)
                    {
                        catchData.ProcessId.Add(process.ProcessId);
                        Console.WriteLine("Added additional ProcessId (except Digital Printing): " + process.ProcessId);
                    }
                }

                // Final log for catchData ProcessIds after no thresholds were found
                Console.WriteLine("Final ProcessId for catchData after threshold not found: " + Newtonsoft.Json.JsonConvert.SerializeObject(catchData.ProcessId));
                return;

            }

            // Find the threshold matching the Pages value in catchData
            var matchingThreshold = thresholds.FirstOrDefault(t => t.Pages == catchData.Pages);
            if (matchingThreshold == null)
            {
                Console.WriteLine($"No threshold found for Pages value {catchData.Pages}.");
                foreach (var process in projectProcesses)
                {
                    if (process.ProcessId != digitalPrintingProcessId)
                    {
                        catchData.ProcessId.Add(process.ProcessId);
                        Console.WriteLine("Added additional ProcessId (except Digital Printing): " + process.ProcessId);
                    }
                }

                // Final log for catchData ProcessIds after no thresholds were found
                Console.WriteLine("Final ProcessId for catchData after threshold not found: " + Newtonsoft.Json.JsonConvert.SerializeObject(catchData.ProcessId));
                return;
            }

            var quantityThreshold = matchingThreshold.Quantity;
            Console.WriteLine($"Matching threshold found: Pages = {matchingThreshold.Pages}, Quantity = {quantityThreshold}");

            // Determine which processes to include in ProcessId based on the threshold
            if (catchData.Quantity > quantityThreshold)
            {
                Console.WriteLine($"Quantity is greater than threshold ({quantityThreshold}), adding CTP and Offset Printing.");

                // Include CTP and Offset Printing if the quantity is greater than the threshold
                if (ctpProcessId.HasValue)
                {
                    catchData.ProcessId.Add(ctpProcessId.Value);
                    Console.WriteLine("Added CTP ProcessId: " + ctpProcessId);
                }

                if (offsetPrintingProcessId.HasValue)
                {
                    catchData.ProcessId.Add(offsetPrintingProcessId.Value);
                    Console.WriteLine("Added Offset Printing ProcessId: " + offsetPrintingProcessId);
                }
                if (cuttingProcessId.HasValue)
                {
                    catchData.ProcessId.Add(cuttingProcessId.Value);
                    Console.WriteLine("Added Cutting ProcessId: " + cuttingProcessId);
                }
            }
            else
            {
                Console.WriteLine($"Quantity is less than or equal to threshold ({quantityThreshold}), adding Digital Printing.");

                // If the quantity is less than or equal to the threshold, include only Digital Printing
                if (digitalPrintingProcessId.HasValue)
                {
                    catchData.ProcessId.Add(digitalPrintingProcessId.Value);
                    Console.WriteLine("Added Digital Printing ProcessId: " + digitalPrintingProcessId);
                }
            }

            // Add all remaining ProcessIds except CTP, Offset Printing, and Digital Printing
            foreach (var process in projectProcesses)
            {
                // Skip adding if it’s already included
                if (process.ProcessId != ctpProcessId &&
                    process.ProcessId != offsetPrintingProcessId &&
                    process.ProcessId != digitalPrintingProcessId &&
                    process.ProcessId != cuttingProcessId)
                {
                    catchData.ProcessId.Add(process.ProcessId);
                    Console.WriteLine("Added additional ProcessId: " + process.ProcessId);
                }
            }

            Console.WriteLine("Final ProcessId for catchData: " + Newtonsoft.Json.JsonConvert.SerializeObject(catchData.ProcessId));
        }

    }
    public class Threshold
    {
        public int Pages { get; set; }
        public int Quantity { get; set; }
    }
}
