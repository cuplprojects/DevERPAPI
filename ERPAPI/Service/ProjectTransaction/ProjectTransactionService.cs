using ERPAPI.Data;
using ERPAPI.Model;
using ERPAPI.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ERPAPI.Service.ProjectTransaction
{
    public class ProjectTransactionService : IProjectTransactionService
    {
        private readonly AppDbContext _context;

        public ProjectTransactionService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<object>> GetProjectTransactionsDataAsync(int projectId, int processId)
        {
            // Fetch quantity sheet data
            var quantitySheetData = await _context.QuantitySheets
                .Where(q => q.ProjectId == projectId && q.Status == 1)
                .ToListAsync();

            // Fetch transaction data
            var transactions = await (from t in _context.Transaction
                                      where t.ProjectId == projectId && t.ProcessId == processId
                                      select new
                                      {
                                          t.TransactionId,
                                          t.AlarmId,
                                          t.ZoneId,
                                          t.QuantitysheetId,
                                          t.TeamId,
                                          t.Remarks,
                                          t.LotNo,
                                          t.InterimQuantity,
                                          t.ProcessId,
                                          t.VoiceRecording,
                                          t.Status,
                                          t.MachineId
                                      }).ToListAsync();

            // Fetch alarm messages
            var alarms = await _context.Alarm.ToListAsync();

            // Fetch users, zones, and machines for all team members in advance to minimize the number of queries
            var allUsers = await _context.Users.ToListAsync();
            var allZone = await _context.Zone.ToListAsync();
            var allMachine = await _context.Machine.ToListAsync();

            // Fetch Process data for current process
            var process = await _context.Processes
                .Where(p => p.Id == processId)
                .FirstOrDefaultAsync();

            // Fetch ProjectProcess data and join with Process table to get process details and sequence
            var projectProcesses = await (from pp in _context.ProjectProcesses
                                          join proc in _context.Processes on pp.ProcessId equals proc.Id
                                          where pp.ProjectId == projectId
                                          select new
                                          {
                                              proc.Id,
                                              pp.ProjectId,
                                              pp.ProcessId,
                                              pp.Sequence,
                                              pp.Weightage,
                                              pp.FeaturesList,
                                              pp.UserId,
                                              pp.ThresholdQty,
                                              proc.Name,
                                              proc.Status,
                                              proc.ProcessType,
                                              proc.RangeStart,
                                              proc.RangeEnd
                                          }).ToListAsync();

            // Logic to find the previous process and independent process based on the rules you defined
            var previousProcess = (dynamic)null;
            var independent = (dynamic)null;

            if (process != null)
            {
                if (process.ProcessType == "Independent")
                {
                    // Rule 1: If current process is Independent, take the previous process with max Sequence (but with Sequence less than current process)
                    previousProcess = projectProcesses
                        .Where(pp => pp.ProjectId == projectId && pp.ProcessId == process.RangeStart)
                        .OrderBy(pp => pp.RangeStart)
                        .FirstOrDefault();
                }
                else if (process.ProcessType == "Dependent")
                {
                    // Rule 2.1: Check previous sequence process
                    previousProcess = projectProcesses
                        .Where(pp => pp.ProjectId == projectId && pp.ProcessType == "Dependent" && pp.Sequence < projectProcesses.FirstOrDefault(p => p.ProcessId == processId).Sequence)
                        .OrderByDescending(pp => pp.Sequence)
                        .FirstOrDefault(); // Get the dependent process with a smaller sequence

                    independent = projectProcesses
                        .Where(pp => pp.ProjectId == projectId && pp.RangeEnd == processId)
                        .OrderByDescending(pp => pp.RangeEnd)
                        .FirstOrDefault();
                }

                // Apply the additional rule here:
                // If the current process is 3, exclude 2 and 1 from being the previous process, and select the one with the smallest sequence
                if (processId == 3)
                {
                    // Filter out processes 2 and 1 from being the previous process
                    previousProcess = projectProcesses
                        .Where(pp => pp.ProjectId == projectId && pp.ProcessId != 2 && pp.ProcessId != 1)
                        .OrderBy(pp => pp.Sequence) // Get the previous process with the smallest sequence
                        .FirstOrDefault();
                }

                // If the current process is 2, exclude 3 from being the previous process, and select the one with the smallest sequence
                if (processId == 2)
                {
                    // Filter out process 3 from being the previous process
                    previousProcess = projectProcesses
                        .Where(pp => pp.ProjectId == projectId && pp.ProcessId != 3)
                        .OrderBy(pp => pp.Sequence) // Get the previous process with the smallest sequence
                        .FirstOrDefault();
                }
            }

            // Map transactions with their alarm messages and usernames
            var transactionsWithAlarms = transactions.Select(t =>
            {
                var parsedAlarmId = TryParseAlarmId(t.AlarmId);
                var alarm = parsedAlarmId is int parsedId
                    ? alarms.FirstOrDefault(a => a.AlarmId == parsedId)
                    : null;

                var userNames = allUsers
                    .Where(u => t.TeamId.Contains(u.UserId)) // Assuming TeamId is a list of userIds
                    .Select(u => u.FirstName + " " + u.LastName)
                    .ToList();

                var zone = allZone.FirstOrDefault(z => z.ZoneId == t.ZoneId);
                var zoneNo = zone != null ? zone.ZoneNo : null;

                var machine = allMachine.FirstOrDefault(z => z.MachineId == t.MachineId);
                var machinename = machine != null ? machine.MachineName : null;

                return new
                {
                    t.TransactionId,
                    AlarmId = t.AlarmId,
                    ZoneId = t.ZoneId,
                    zoneNo = zoneNo,
                    machinename = machinename,
                    QuantitysheetId = t.QuantitysheetId,
                    TeamId = t.TeamId,
                    Remarks = t.Remarks,
                    LotNo = t.LotNo,
                    TeamUserNames = userNames,
                    InterimQuantity = t.InterimQuantity,
                    ProcessId = t.ProcessId,
                    VoiceRecording = t.VoiceRecording,
                    Status = t.Status,
                    MachineId = t.MachineId,
                    AlarmMessage = alarm != null ? alarm.Message : null
                };
            }).ToList();

            // Fetch previousTransaction (transactions related to the previous process)
            var previousTransactions = transactionsWithAlarms
                .Where(t => previousProcess != null && t.ProcessId == previousProcess.ProcessId)
                .ToList();

            // Fetch independentTransaction (transactions related to the independent process)
            var independentTransactions = transactionsWithAlarms
                .Where(t => independent != null && t.ProcessId == independent.ProcessId)
                .ToList();

            // Include both the previous process (if it exists) and current process details
            var responseData = quantitySheetData.Select(q => new
            {
                q.QuantitySheetId,
                q.ProjectId,
                q.LotNo,
                q.CatchNo,
                q.Paper,
                q.ExamDate,
                q.ExamTime,
                q.Course,
                q.Subject,
                q.InnerEnvelope,
                q.OuterEnvelope,
                q.Quantity,
                q.PercentageCatch,
                ProcessIds = q.ProcessId,  // Assuming ProcessId is a list
                Transactions = transactionsWithAlarms
                    .Where(t => t.QuantitysheetId == q.QuantitySheetId)
                    .ToList(),

                Independent = independent != null ?
                new
                {
                    independent.Id,
                    independent.Name,
                    independent.Weightage,
                    independent.Status,
                    independent.ProcessType,
                    independent.RangeStart,
                    independent.RangeEnd,
                    IndependentTransactions = independentTransactions.Where(t => t.QuantitysheetId == q.QuantitySheetId)
                    .ToList()  // Include independent transactions

                } : null,

                // Return Previous Process if available
                PreviousProcess = previousProcess != null ? new
                {
                    previousProcess.Id,
                    previousProcess.Name,
                    previousProcess.Weightage,
                    previousProcess.Status,
                    previousProcess.ProcessType,
                    previousProcess.RangeStart,
                    previousProcess.RangeEnd,
                    PreviousTransactions = previousTransactions.Where(t => t.QuantitysheetId == q.QuantitySheetId)
                    .ToList()  // Include previous transactions
                } : null, // If no previous process, return null

                // Return Current Process
                CurrentProcess = process != null ? new
                {
                    process.Id,
                    process.Name,
                    process.Weightage,
                    process.Status,
                    process.ProcessType,
                    process.RangeStart,
                    process.RangeEnd,
                } : null  // If no current process, return null
            });

            return responseData;
        }

        private object TryParseAlarmId(string alarmId)
        {
            if (int.TryParse(alarmId, out int parsedId))
            {
                return parsedId;
            }
            return null;
        }
    }
}