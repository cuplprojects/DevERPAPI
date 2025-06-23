using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ERPAPI.Model;
using ERPAPI.Data;
using ERPGenericFunctions.Model;
using System.Globalization;

namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReportsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ReportsController(AppDbContext context)
        {
            _context = context;
        }



        [HttpPost("CreateReport")]
        public async Task<IActionResult> CreateReport([FromBody] Reports report)
        {
            try
            {
                if (report == null)
                {
                    return BadRequest(new { Message = "Invalid report data." });
                }

                await _context.Set<Reports>().AddAsync(report);
                await _context.SaveChangesAsync();

                return Ok(new { Message = "Report created successfully.", ReportId = report.ReportId });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "An error occurred while creating the report.", Details = ex.Message });
            }
        }


        // GET: api/Reports/GetAllGroups
        [HttpGet("GetAllGroups")]
        public async Task<IActionResult> GetAllGroups()
        {
            try
            {
                // Query the database for all groups and select the required fields
                var groups = await _context.Set<Group>()
                    .Select(g => new
                    {
                        g.Id,
                        g.Name,
                        g.Status
                    })
                    .ToListAsync();

                // Check if groups exist
                if (groups == null || groups.Count == 0)
                {
                    return NotFound(new { Message = "No groups found." });
                }

                return Ok(groups);
            }
            catch (Exception ex)
            {
                // Handle errors gracefully
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "An error occurred.", Details = ex.Message });
            }
        }





        // GET: api/Reports/GetProjectsByGroupId/{groupId}
        [HttpGet("GetProjectsByGroupId/{groupId}")]
        public async Task<IActionResult> GetProjectsByGroupId(int groupId)
        {
            try
            {
                // Query the database for projects with the given GroupId
                var projects = await _context.Set<Project>()
                    .Where(p => p.GroupId == groupId)
                    .Select(p => new { p.ProjectId, p.Name })
                    .ToListAsync();

                // Check if any projects exist for the given GroupId
                if (!projects.Any())
                {
                    return NotFound(new { Message = "No projects found for the given GroupId." });
                }

                return Ok(projects);
            }
            catch (Exception ex)
            {
                // Handle errors gracefully
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "An error occurred.", Details = ex.Message });
            }
        }


        // GET: api/Reports/GetLotNosByProjectId/{projectId}
        [HttpGet("GetLotNosByProjectId/{projectId}")]
        public async Task<IActionResult> GetLotNosByProjectId(int projectId)
        {
            try
            {
                // Query the database for unique LotNos of the given ProjectId
                var lotNos = await _context.Set<QuantitySheet>()
                    .Where(q => q.ProjectId == projectId && !string.IsNullOrEmpty(q.LotNo)) // Filter by ProjectId and non-null LotNo
                    .Select(q => q.LotNo)
                    .Distinct() // Ensure uniqueness
                    .ToListAsync();

                // Check if any LotNos exist for the given ProjectId
                if (lotNos == null || lotNos.Count == 0)
                {
                    return NotFound(new { Message = "No LotNos found for the given ProjectId." });
                }

                return Ok(lotNos);
            }
            catch (Exception ex)
            {
                // Handle errors gracefully
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "An error occurred.", Details = ex.Message });
            }
        }



        [HttpGet("GetQuantitySheetsByProjectId/{projectId}/LotNo/{lotNo}")]
        public async Task<IActionResult> GetQuantitySheetsByProjectId(int projectId, string lotNo)
        {
            try
            {
                // Fetch QuantitySheet data by ProjectId and LotNo
                var quantitySheets = await _context.Set<QuantitySheet>()
                    .Where(q => q.ProjectId == projectId && q.LotNo == lotNo)
                    .ToListAsync();

                if (quantitySheets == null || quantitySheets.Count == 0)
                {
                    return NotFound(new { Message = "No data found for the given ProjectId and LotNo." });
                }

                // Fetch all necessary data
                var allProcesses = await _context.Set<Process>().ToListAsync();
                var transactions = await _context.Set<Transaction>()
                    .Where(t => t.ProjectId == projectId)
                    .ToListAsync();
                var eventLogs = await _context.EventLogs
                    .Where(e => transactions.Select(t => t.TransactionId).Contains(e.TransactionId.Value)) // Remove e.Event from Where clause
                    .Select(e => new { e.TransactionId, e.LoggedAT, e.EventID }) // Only select TransactionId and LoggedAT
                    .ToListAsync();
                var allMachines = await _context.Set<Machine>().ToListAsync();
                var allZones = await _context.Set<Zone>().ToListAsync();
                var allTeams = await _context.Set<Team>().ToListAsync();
                var allUsers = await _context.Set<User>().ToListAsync();
                var dispatches = await _context.Set<Dispatch>()
                    .Where(d => d.ProjectId == projectId && d.LotNo == lotNo)
                    .ToListAsync(); // Fetch dispatch data

                var projectprocess = await _context.ProjectProcesses.Where(p => p.ProjectId == projectId).OrderBy(p => p.Sequence).FirstOrDefaultAsync();
                Console.WriteLine(projectprocess.Sequence);
                // Map QuantitySheet data with required details
                var result = quantitySheets.Select(q =>
                {
                    var relatedTransactions = transactions
                        .Where(t => t.QuantitysheetId == q.QuantitySheetId)
                        .ToList();

                    DateTime? startLogs = relatedTransactions.Select(t => eventLogs
                        .Where(e => e.TransactionId == t.TransactionId)
                        .OrderBy(e => e.EventID)
                        .Select(e => (DateTime?)e.LoggedAT)
                        .FirstOrDefault()).Min();

                    DateTime? endLogs = relatedTransactions.Select(t => eventLogs
                        .Where(e => e.TransactionId == t.TransactionId)
                        .OrderByDescending(e => e.EventID)
                        .Select(e => (DateTime?)e.LoggedAT)
                        .FirstOrDefault()).Max();

                    string duration = "N/A";
                    if (startLogs.HasValue && endLogs.HasValue)
                    {
                        var timeSpan = endLogs.Value - startLogs.Value;
                        duration = $"{timeSpan.Days} Days:{timeSpan.Hours:D2} Hours:{timeSpan.Minutes:D2} Minutes";
                    }

                    string catchStatus;
                    if (!relatedTransactions.Any())
                    {
                        catchStatus = "Pending";
                    }
                    else
                    {
                        var process12Transaction = relatedTransactions.FirstOrDefault(t => t.ProcessId == 12);
                        if (process12Transaction != null && process12Transaction.Status == 2)
                        {
                            catchStatus = "Completed";
                        }
                        else if (relatedTransactions.Any(t => t.ProcessId != 12))
                        {
                            catchStatus = "Running";
                        }
                        else
                        {
                            catchStatus = "Pending";
                        }
                    }

                    var lastTransactionProcessId = relatedTransactions
                        .OrderByDescending(t => t.TransactionId)
                        .Select(t => t.ProcessId)
                        .FirstOrDefault();

                    var lastTransactionProcessName = allProcesses
                        .FirstOrDefault(p => p.Id == lastTransactionProcessId)?.Name;

                    var dispatchEntry = dispatches.FirstOrDefault(d => d.LotNo == q.LotNo);
                    var dispatchDate = dispatchEntry?.UpdatedAt.HasValue == true
                        ? dispatchEntry.UpdatedAt.Value.ToString("yyyy-MM-dd")
                        : "Not Available";

                    return new
                    {
                        q.CatchNo,
                        q.PaperTitle,
                        q.ExamDate,
                        q.ExamTime,
                        q.CourseId,
                        q.SubjectId,
                        q.InnerEnvelope,
                        q.OuterEnvelope,
                        q.LotNo,
                        q.Quantity,
                        q.Pages,
                        q.Status,
                        ProcessNames = q.ProcessId != null
                            ? allProcesses
                                .Where(p => q.ProcessId.Contains(p.Id))
                                .Select(p => p.Name)
                                .ToList()
                            : null,
                        CatchStatus = catchStatus,
                        StartTime = startLogs,
                        EndTime = catchStatus == "Completed" ? endLogs : null,
                        // Apply condition here
                        Duration = duration,
                        TwelvethProcess = relatedTransactions.Any(t => t.ProcessId == 12),
                        CurrentProcessName = lastTransactionProcessName,
                        DispatchDate = dispatchDate,
                        TransactionData = new
                        {
                            ZoneDescriptions = relatedTransactions
                                .Select(t => t.ZoneId)
                                .Distinct()
                                .Select(zoneId => allZones.FirstOrDefault(z => z.ZoneId == zoneId)?.ZoneDescription)
                                .Where(description => description != null)
                                .ToList(),
                            TeamDetails = relatedTransactions
                                .SelectMany(t => t.TeamId ?? new List<int>())
                                .Distinct()
                                .Select(teamId => new
                                {
                                    TeamName = allTeams.FirstOrDefault(t => t.TeamId == teamId)?.TeamName,
                                    UserNames = allTeams.FirstOrDefault(t => t.TeamId == teamId)?.UserIds
                                        .Select(userId => allUsers.FirstOrDefault(u => u.UserId == userId)?.UserName)
                                        .Where(userName => userName != null)
                                        .ToList()
                                })
                                .Where(team => team.TeamName != null)
                                .ToList(),
                            MachineNames = relatedTransactions
                                .Select(t => t.MachineId)
                                .Distinct()
                                .Select(machineId => allMachines.FirstOrDefault(m => m.MachineId == machineId)?.MachineName)
                                .Where(name => name != null)
                                .ToList()
                        }
                    };
                });


                return Ok(result);
            }
            catch (Exception ex)
            {
                // Handle errors gracefully
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "An error occurred.", Details = ex.Message });
            }
        }




        [HttpGet("search")]
        public async Task<IActionResult> SearchQuantitySheet(
    [FromQuery] string query,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 5,
    [FromQuery] int? groupId = null,
    [FromQuery] int? projectId = null)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("Search query cannot be empty.");
            }

            var queryable = _context.QuantitySheets.AsQueryable();

            if (groupId.HasValue)
            {
                var projectIdsInGroup = _context.Projects
                    .Where(p => p.GroupId == groupId)
                    .Select(p => p.ProjectId);

                queryable = queryable.Where(q => projectIdsInGroup.Contains(q.ProjectId));
            }

            if (projectId.HasValue)
            {
                queryable = queryable.Where(q => q.ProjectId == projectId);
            }

            

            var totalRecords = await queryable
    .CountAsync(q => q.CatchNo.StartsWith(query) ||
                    q.SubjectId.ToString().StartsWith(query) ||
                    q.CourseId.ToString().StartsWith(query) ||
                    (q.PaperTitle != null && q.PaperTitle.StartsWith(query)));

            var results = await queryable
                .Where(q => q.CatchNo.StartsWith(query) ||
                            q.SubjectId.ToString().StartsWith(query) ||
                            q.CourseId.ToString().StartsWith(query) ||
                            (q.PaperTitle != null && q.PaperTitle.StartsWith(query)))
                .Select(q => new
                {
                    q.CatchNo,
                    MatchedColumn = q.CatchNo.StartsWith(query) ? "CatchNo" :
                                    q.SubjectId.ToString().StartsWith(query) ? "SubjectId" :
                                    q.CourseId.ToString().StartsWith(query) ? "CourseId" : "PaperTitle",
                    MatchedValue = q.CatchNo.StartsWith(query) ? q.CatchNo :
                                   q.SubjectId.ToString().StartsWith(query) ? q.SubjectId.ToString() :
                                   q.CourseId.ToString().StartsWith(query) ? q.CourseId.ToString() : q.PaperTitle,
                    q.ProjectId,
                    q.LotNo
                })
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new { TotalRecords = totalRecords, Results = results });
        }





        [HttpGet("GetQuantitySheetsByCatchNo/{projectId}/{catchNo}")]
        public async Task<IActionResult> GetQuantitySheetsByCatchNo(string catchNo, int projectId)
        {
            try
            {
                // Fetch QuantitySheet data by CatchNo
                var quantitySheets = await _context.Set<QuantitySheet>()
                    .Where(q => q.CatchNo == catchNo && q.ProjectId == projectId)
                    .ToListAsync();

                if (quantitySheets == null || quantitySheets.Count == 0)
                {
                    return NotFound(new { Message = "No data found for the given CatchNo." });
                }

                // Fetch all necessary data
                var allProcesses = await _context.Set<Process>().ToListAsync();
                var transactions = await _context.Set<Transaction>()
                    .Where(t => quantitySheets.Select(q => q.QuantitySheetId).Contains(t.QuantitysheetId))
                    .ToListAsync();
                var allMachines = await _context.Set<Machine>().ToListAsync();
                var allZones = await _context.Set<Zone>().ToListAsync();
                var allTeams = await _context.Set<Team>().ToListAsync();
                var allUsers = await _context.Set<User>().ToListAsync();
                var dispatches = await _context.Set<Dispatch>()
                    .Where(d => quantitySheets.Select(q => q.LotNo).Contains(d.LotNo))
                    .ToListAsync(); // Fetch dispatch data

                // Map QuantitySheet data with required details
                var result = quantitySheets.Select(q =>
                {
                    // Get transactions related to this QuantitySheetId
                    var relatedTransactions = transactions
                        .Where(t => t.QuantitysheetId == q.QuantitySheetId)
                        .ToList();

                    string catchStatus;
                    if (!relatedTransactions.Any())
                    {
                        catchStatus = "Pending";
                    }
                    else
                    {
                        // Check if any transaction has ProcessId == 12
                        var process12Transaction = relatedTransactions.FirstOrDefault(t => t.ProcessId == 12);
                        if (process12Transaction != null && process12Transaction.Status == 2)
                        {
                            catchStatus = "Completed";
                        }
                        else if (relatedTransactions.Any(t => t.ProcessId != 12))
                        {
                            catchStatus = "Running";
                        }
                        else
                        {
                            catchStatus = "Pending";
                        }
                    }

                    var lastTransactionProcessId = relatedTransactions
                        .OrderByDescending(t => t.TransactionId) // Get the latest transaction based on TransactionId
                        .Select(t => t.ProcessId)
                        .FirstOrDefault();

                    var lastTransactionProcessName = allProcesses
                        .FirstOrDefault(p => p.Id == lastTransactionProcessId)?.Name;

                    // Get Dispatch Date if available, else return "Not Available"
                    var dispatchEntry = dispatches.FirstOrDefault(d => d.LotNo == q.LotNo);
                    var dispatchDate = dispatchEntry?.UpdatedAt.HasValue == true
                        ? dispatchEntry.UpdatedAt.Value.ToString("yyyy-MM-dd")
                        : "Not Available";

                    return new
                    {
                        q.CatchNo,
                        q.PaperTitle,
                        q.PaperNumber,
                        q.ExamDate,
                        q.ExamTime,
                        q.CourseId,
                        q.SubjectId,
                        q.InnerEnvelope,
                        q.OuterEnvelope,
                        q.LotNo,
                        q.Quantity,
                        q.Pages,
                        q.Status,
                        ProcessNames = q.ProcessId != null
                            ? allProcesses
                                .Where(p => q.ProcessId.Contains(p.Id))
                                .Select(p => p.Name)
                                .ToList()
                            : null,
                        CatchStatus = catchStatus, // Updated logic
                        TwelvethProcess = relatedTransactions.Any(t => t.ProcessId == 12),
                        CurrentProcessName = lastTransactionProcessName,
                        DispatchDate = dispatchDate, // Added Dispatch Date
                                                     // Grouped Transaction Data
                        TransactionData = new
                        {
                            ZoneDescriptions = relatedTransactions
                                .Select(t => t.ZoneId)
                                .Distinct()
                                .Select(zoneId => allZones.FirstOrDefault(z => z.ZoneId == zoneId)?.ZoneDescription)
                                .Where(description => description != null)
                                .ToList(),
                            TeamDetails = relatedTransactions
                                .SelectMany(t => t.TeamId ?? new List<int>())
                                .Distinct()
                                .Select(teamId => new
                                {
                                    TeamName = allTeams.FirstOrDefault(t => t.TeamId == teamId)?.TeamName,
                                    UserNames = allTeams.FirstOrDefault(t => t.TeamId == teamId)?.UserIds
                                        .Select(userId => allUsers.FirstOrDefault(u => u.UserId == userId)?.UserName)
                                        .Where(userName => userName != null)
                                        .ToList()
                                })
                                .Where(team => team.TeamName != null)
                                .ToList(),
                            MachineNames = relatedTransactions
                                .Select(t => t.MachineId)
                                .Distinct()
                                .Select(machineId => allMachines.FirstOrDefault(m => m.MachineId == machineId)?.MachineName)
                                .Where(name => name != null)
                                .ToList()
                        }
                    };
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                // Handle errors gracefully
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "An error occurred.", Details = ex.Message });
            }
        }



        [HttpGet("process-wise/{catchNo}")]
        public async Task<IActionResult> GetProcessWiseData(string catchNo)
        {
            // Get the ProjectId of the entered CatchNo from the QuantitySheet table
            var quantitySheet = await _context.QuantitySheets
                .Where(q => q.CatchNo == catchNo)
                .Select(q => new { q.QuantitySheetId, q.ProcessId, q.ProjectId })
                .FirstOrDefaultAsync();

            if (quantitySheet == null)
            {
                return NotFound("No data found for the given CatchNo.");
            }

            // Get the sequence of the ProjectId from the ProjectProcess table
            var projectProcesses = await _context.ProjectProcesses
                .Where(pp => pp.ProjectId == quantitySheet.ProjectId)
                .OrderBy(pp => pp.Sequence)
                .ToListAsync();

            var transactions = await _context.Transaction
                .Where(t => t.QuantitysheetId == quantitySheet.QuantitySheetId)
                .ToListAsync();

            var eventLogs = await _context.EventLogs
                .Where(e => transactions.Select(t => t.TransactionId).Contains(e.TransactionId.Value) && e.Event == "Status updated")
                .Select(e => new { e.TransactionId, e.LoggedAT, e.EventTriggeredBy })
                .ToListAsync();

            var supervisorLogs = await _context.EventLogs
        .Where(e => transactions.Select(t => t.TransactionId).Contains(e.TransactionId.Value))
        .GroupBy(e => e.TransactionId)
        .Select(g => new
        {
            TransactionId = g.Key,
            EventTriggeredBy = g.Select(e => e.EventTriggeredBy).FirstOrDefault()
        })
        .ToListAsync();

            var users = await _context.Users.ToListAsync();

            var filteredProjectProcesses = projectProcesses
    .Where(pp => transactions.Any(t => t.ProcessId == pp.ProcessId))
    .OrderBy(pp => pp.Sequence)
    .ToList();

            var processWiseData = filteredProjectProcesses
    .OrderBy(pp => pp.Sequence) // Ensure ordering before transformation
    .Select(pp => new
    {
        ProcessId = pp.ProcessId,
        Transactions = transactions
            .Where(t => t.ProcessId == pp.ProcessId)
            .Select(t => new
            {
                TransactionId = t.TransactionId,
                ZoneName = _context.Zone
                    .Where(z => z.ZoneId == t.ZoneId)
                    .Select(z => z.ZoneNo)
                    .FirstOrDefault(),
                TeamMembers = _context.Users
                    .Where(u => t.TeamId.Contains(u.UserId))
                    .Select(u => new { FullName = u.FirstName + " " + u.LastName })
                    .ToList(),

                Supervisor = users
                        .Where(u => u.UserId == supervisorLogs
                            .Where(s => s.TransactionId == t.TransactionId)
                            .Select(s => s.EventTriggeredBy)
                            .FirstOrDefault())
                        .Select(u => u.FirstName + " " + u.LastName)
                        .FirstOrDefault(),
                MachineName = _context.Machine
                    .Where(m => m.MachineId == t.MachineId)
                    .Select(m => m.MachineName)
                    .FirstOrDefault(),
                StartTime = eventLogs
                    .Where(e => e.TransactionId == t.TransactionId)
                    .OrderBy(e => e.LoggedAT)
                    .Select(e => (DateTime?)e.LoggedAT)
                    .FirstOrDefault(),
                EndTime = eventLogs
                    .Where(e => e.TransactionId == t.TransactionId)
                    .OrderByDescending(e => e.LoggedAT)
                    .Select(e => (DateTime?)e.LoggedAT)
                    .FirstOrDefault(),

            }).ToList()


    })
    .ToList(); // Convert to List to maintain order

            return Ok(processWiseData);
        }





        [HttpGet("DailyProductionReport")]
        public async Task<IActionResult> GetDailyProductionReport(string? date = null, string? startDate = null, string? endDate = null)
        {
            try
            {
                DateTime? parsedDate = null;
                DateTime? parsedStartDate = null;
                DateTime? parsedEndDate = null;

                if (!string.IsNullOrEmpty(date))
                {
                    if (!DateTime.TryParseExact(date, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
                        return BadRequest("Invalid date format. Use dd-MM-yyyy.");
                    parsedDate = parsed.Date;
                }

                if (!string.IsNullOrEmpty(startDate))
                {
                    if (!DateTime.TryParseExact(startDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedStart))
                        return BadRequest("Invalid startDate format. Use dd-MM-yyyy.");
                    parsedStartDate = parsedStart.Date;
                }

                if (!string.IsNullOrEmpty(endDate))
                {
                    if (!DateTime.TryParseExact(endDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedEnd))
                        return BadRequest("Invalid endDate format. Use dd-MM-yyyy.");
                    parsedEndDate = parsedEnd.Date;
                }

                var logQuery = _context.EventLogs.AsQueryable();

                // Filter by date range
                if (parsedStartDate.HasValue && parsedEndDate.HasValue)
                {
                    logQuery = logQuery.Where(el => el.LoggedAT.Date >= parsedStartDate.Value && el.LoggedAT.Date <= parsedEndDate.Value);
                }
                else if (parsedDate.HasValue)
                {
                    logQuery = logQuery.Where(el => el.LoggedAT.Date == parsedDate.Value);
                }

                // Filter by event type and new value = 2
                logQuery = logQuery.Where(el => el.Event == "Status updated" && el.NewValue == "2");

                var transactionIds = await logQuery
                    .Where(el => el.TransactionId.HasValue)
                    .Select(el => el.TransactionId.Value)
                    .Distinct()
                    .ToListAsync();

                var transactions = await _context.Transaction
                    .Where(t => transactionIds.Contains(t.TransactionId))
                    .ToListAsync();

                var quantitySheetIds = transactions.Select(t => t.QuantitysheetId).Distinct().ToList();
                var quantitySheets = await _context.QuantitySheets
                    .Where(q => quantitySheetIds.Contains(q.QuantitySheetId))
                    .ToListAsync();

                var projectIds = transactions.Select(t => t.ProjectId).Distinct().ToList();
                var projects = await _context.Projects
                    .Where(p => projectIds.Contains(p.ProjectId))
                    .ToListAsync();

                var groupIds = projects.Select(p => p.GroupId).Distinct().ToList();
                var groups = await _context.Groups
                    .Where(g => groupIds.Contains(g.Id))
                    .ToDictionaryAsync(g => g.Id, g => g.Name);

                var report = transactions
                    .Join(projects, t => t.ProjectId, p => p.ProjectId, (t, p) => new { t, p })
                    .Join(quantitySheets, tp => tp.t.QuantitysheetId, qs => qs.QuantitySheetId, (tp, qs) => new { tp.t, tp.p, qs })
                    .GroupBy(x => new { x.t.ProjectId, x.p.TypeId, x.p.GroupId, x.t.LotNo })
                    .Select(g =>
                    {
                        // All sheets in this group
                        var allSheets = g
                            .GroupBy(x => x.qs.QuantitySheetId)
                            .Select(x => x.First())
                            .ToList();

                        // Sheets where ProcessId == 12
                        var completedSheets = g
                            .Where(x => x.t.ProcessId == 12)
                            .GroupBy(x => x.qs.QuantitySheetId)
                            .Select(x => x.First())
                            .ToList();

                        // Get date range from all sheets (not just completed)
                        var examDates = allSheets
                            .Select(x =>
                            {
                                DateTime.TryParse(x.qs.ExamDate, out var dt);
                                return dt;
                            })
                            .Where(d => d != default)
                            .ToList();

                        var minExamDate = examDates.Any() ? examDates.Min().ToString("dd-MM-yyyy") : null;
                        var maxExamDate = examDates.Any() ? examDates.Max().ToString("dd-MM-yyyy") : null;

                        return new
                        {
                            GroupName = groups.ContainsKey(g.Key.GroupId) ? groups[g.Key.GroupId] : "Unknown",
                            ProjectId = g.Key.ProjectId,
                            TypeId = g.Key.TypeId,
                            LotNo = g.Key.LotNo,
                            From = minExamDate,
                            To = maxExamDate,
                            CompletedCountOfCatches = completedSheets.Count,
                            CompletedTotalQuantity = completedSheets.Sum(x => x.qs.Quantity),
                            TotalQuantity = allSheets.Sum(x => x.qs.Quantity),
                            TotalCountOfCatches = allSheets.Count
                        };
                    })
                    .ToList();

                return Ok(report);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred", error = ex.Message });
            }
        }





        [HttpGet("DailyProductionSummaryReport")]
        public async Task<IActionResult> GetDailyProductionSummaryReport(string? date = null, string? startDate = null, string? endDate = null)
        {
            try
            {
                DateTime? parsedDate = null;
                DateTime? parsedStartDate = null;
                DateTime? parsedEndDate = null;

                if (!string.IsNullOrEmpty(date))
                {
                    if (!DateTime.TryParseExact(date, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
                        return BadRequest("Invalid date format. Use dd-MM-yyyy.");
                    parsedDate = parsed.Date;
                }

                if (!string.IsNullOrEmpty(startDate))
                {
                    if (!DateTime.TryParseExact(startDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedStart))
                        return BadRequest("Invalid startDate format. Use dd-MM-yyyy.");
                    parsedStartDate = parsedStart.Date;
                }

                if (!string.IsNullOrEmpty(endDate))
                {
                    if (!DateTime.TryParseExact(endDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedEnd))
                        return BadRequest("Invalid endDate format. Use dd-MM-yyyy.");
                    parsedEndDate = parsedEnd.Date;
                }

                var logQuery = _context.EventLogs.AsQueryable();

                if (parsedStartDate.HasValue && parsedEndDate.HasValue)
                {
                    logQuery = logQuery.Where(el => el.LoggedAT.Date >= parsedStartDate.Value && el.LoggedAT.Date <= parsedEndDate.Value);
                }
                else if (parsedDate.HasValue)
                {
                    logQuery = logQuery.Where(el => el.LoggedAT.Date == parsedDate.Value);
                }

                logQuery = logQuery.Where(el => el.Event == "Status updated" && el.NewValue == "2");

                var transactionIds = await logQuery
                    .Where(el => el.TransactionId.HasValue)
                    .Select(el => el.TransactionId.Value)
                    .Distinct()
                    .ToListAsync();

                var transactions = await _context.Transaction
                    .Where(t => transactionIds.Contains(t.TransactionId))
                    .ToListAsync();

                var quantitySheetIds = transactions.Select(t => t.QuantitysheetId).Distinct().ToList();
                var quantitySheets = await _context.QuantitySheets
                    .Where(q => quantitySheetIds.Contains(q.QuantitySheetId))
                    .ToListAsync();

                var projectIds = transactions.Select(t => t.ProjectId).Distinct().ToList();
                var projects = await _context.Projects
                    .Where(p => projectIds.Contains(p.ProjectId))
                    .ToListAsync();

                // Join and group
                var joinedData = transactions
                    .Join(projects, t => t.ProjectId, p => p.ProjectId, (t, p) => new { t, p })
                    .Join(quantitySheets, tp => tp.t.QuantitysheetId, qs => qs.QuantitySheetId, (tp, qs) => new { tp.t, tp.p, qs });

                // Grouped to calculate final summary
                var grouped = joinedData
                    .GroupBy(x => new { x.t.ProjectId, x.p.TypeId, x.p.GroupId, x.t.LotNo })
                    .ToList();

                var totalGroups = grouped.Select(g => g.Key.GroupId).Distinct().Count();
                var totalLots = grouped.Count(); // Total number of grouped lot entries (not distinct)
                var totalProjects = grouped.Select(g => g.Key.ProjectId).Distinct().Count();
                var totalCatches = grouped.Sum(g => g.Count());
                var totalQuantity = grouped.Sum(g => g.Sum(x => x.qs.Quantity));

                return Ok(new
                {
                    TotalGroups = totalGroups,
                    TotalLots = totalLots,
                    TotalCountOfCatches = totalCatches,
                    TotalProjects = totalProjects,
                    TotalQuantity = totalQuantity
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred", error = ex.Message });
            }
        }



        [HttpGet("lot-numbers-count")]
        public async Task<ActionResult<int>> GetLotNumbersCount([FromQuery] string date)
        {
            try
            {
                // Validate and parse the date parameter
                if (string.IsNullOrEmpty(date) || !DateTime.TryParseExact(date, "dd-MM-yyyy",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out DateTime targetDate))
                {
                    return BadRequest("Invalid date format. Please use dd-MM-yyyy format.");
                }

                // LINQ query to get count
                var lotNumbersCount = await (from el in _context.EventLogs
                                             join t in _context.Transaction on el.TransactionId equals t.TransactionId
                                             join qs in _context.QuantitySheets on t.QuantitysheetId equals qs.QuantitySheetId
                                             where el.Category == "Transaction"
                                             && el.LoggedAT.Date == targetDate
                                             && el.Event == "Status Updated"
                                             && el.OldValue == "1"
                                             && el.NewValue == "2"
                                             select t.LotNo)
                                           .CountAsync();

                return Ok(lotNumbersCount);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while retrieving lot numbers count: {ex.Message}");
            }
        }




        [HttpGet("Process-Production-Report")]
        public async Task<IActionResult> GetProcessWiseDataByDateRange(
            [FromQuery] string? date,
            [FromQuery] string? startDate,
            [FromQuery] string? endDate)
        {
            try
            {
                DateTime? parsedDate = null;
                DateTime? parsedStartDate = null;
                DateTime? parsedEndDate = null;

                if (!string.IsNullOrEmpty(date))
                {
                    if (!DateTime.TryParseExact(date, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
                        return BadRequest("Invalid date format. Use dd-MM-yyyy.");
                    parsedDate = parsed.Date;
                }

                if (!string.IsNullOrEmpty(startDate))
                {
                    if (!DateTime.TryParseExact(startDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedStart))
                        return BadRequest("Invalid startDate format. Use dd-MM-yyyy.");
                    parsedStartDate = parsedStart.Date;
                }

                if (!string.IsNullOrEmpty(endDate))
                {
                    if (!DateTime.TryParseExact(endDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedEnd))
                        return BadRequest("Invalid endDate format. Use dd-MM-yyyy.");
                    parsedEndDate = parsedEnd.Date;
                }

                // Step 1: Filter EventLogs
                var eventLogQuery = _context.EventLogs
                    .Where(el => el.Category == "Transaction"
                        && el.Event == "Status Updated"
                        && el.OldValue == "1"
                        && el.NewValue == "2"
                        && el.TransactionId != null);

                if (parsedStartDate.HasValue && parsedEndDate.HasValue)
                {
                    eventLogQuery = eventLogQuery.Where(el => el.LoggedAT.Date >= parsedStartDate.Value && el.LoggedAT.Date <= parsedEndDate.Value);
                }
                else if (parsedDate.HasValue)
                {
                    eventLogQuery = eventLogQuery.Where(el => el.LoggedAT.Date == parsedDate.Value);
                }

                var filteredLogs = await eventLogQuery.ToListAsync();
                var validTransactionIds = filteredLogs.Select(el => el.TransactionId.Value).Distinct().ToList();

                // Step 2: Get Transactions and their ProjectIds
                var transactions = await _context.Transaction
                    .Where(t => validTransactionIds.Contains(t.TransactionId))
                    .ToListAsync();

                var projectIds = transactions.Select(t => t.ProjectId).Distinct().ToList();

                // Step 3: Get Project with TypeId
                var projects = await _context.Projects
                    .Where(p => projectIds.Contains(p.ProjectId))
                    .ToDictionaryAsync(p => p.ProjectId, p => p.TypeId); // 1 = Booklet, 2 = Paper

                // Step 4: Segregate transactions by TypeId
                var bookletTransactionIds = transactions
                    .Where(t => projects.ContainsKey(t.ProjectId) && projects[t.ProjectId] == 1)
                    .Select(t => new { t.TransactionId, t.QuantitysheetId, t.ProcessId })
                    .ToList();

                var paperTransactionIds = transactions
                    .Where(t => projects.ContainsKey(t.ProjectId) && projects[t.ProjectId] == 2)
                    .Select(t => new { t.TransactionId, t.QuantitysheetId, t.ProcessId })
                    .ToList();

                var bookletSheetIds = bookletTransactionIds.Select(x => x.QuantitysheetId).Distinct().ToList();
                var paperSheetIds = paperTransactionIds.Select(x => x.QuantitysheetId).Distinct().ToList();

                var quantitySheets = await _context.QuantitySheets
                    .Where(qs => bookletSheetIds.Contains(qs.QuantitySheetId) || paperSheetIds.Contains(qs.QuantitySheetId))
                    .ToListAsync();

                var quantitySheetDict = quantitySheets.ToDictionary(qs => qs.QuantitySheetId, qs => qs.Quantity);

                // Step 5: Group and project
                var result = new List<object>();

                double totalCompletedBookletQuantity = 0;
                int totalCompletedBookletCatch = 0;

                double totalCompletedPaperQuantity = 0;
                int totalCompletedPaperCatch = 0;

                var allProcessIds = transactions.Select(t => t.ProcessId).Distinct();

                foreach (var processId in allProcessIds)
                {
                    var bookletCatches = bookletTransactionIds
                        .Where(t => t.ProcessId == processId)
                        .GroupBy(t => t.QuantitysheetId)
                        .Select(g => g.Key)
                        .Distinct()
                        .ToList();

                    var paperCatches = paperTransactionIds
                        .Where(t => t.ProcessId == processId)
                        .GroupBy(t => t.QuantitysheetId)
                        .Select(g => g.Key)
                        .Distinct()
                        .ToList();

                    var bookletQuantity = bookletCatches
                        .Where(qid => quantitySheetDict.ContainsKey(qid))
                        .Sum(qid => quantitySheetDict[qid]);

                    var paperQuantity = paperCatches
                        .Where(qid => quantitySheetDict.ContainsKey(qid))
                        .Sum(qid => quantitySheetDict[qid]);

                    // Accumulate overall totals
                    totalCompletedBookletCatch += bookletCatches.Count;
                    totalCompletedBookletQuantity += bookletQuantity;

                    totalCompletedPaperCatch += paperCatches.Count;
                    totalCompletedPaperQuantity += paperQuantity;

                    result.Add(new
                    {
                        ProcessId = processId,
                        CompletedTotalCatchesInBooklet = bookletCatches.Count,
                        CompletedTotalQuantityInBooklet = bookletQuantity,
                        CompletedTotalCatchesInPaper = paperCatches.Count,
                        CompletedTotalQuantityInPaper = paperQuantity
                    });
                }

                // Add grand total object at the end
                result.Add(new
                {
                    ProcessId = "Total",
                    CompletedTotalCatchesInBooklet = totalCompletedBookletCatch,
                    CompletedTotalQuantityInBooklet = totalCompletedBookletQuantity,
                    CompletedTotalCatchesInPaper = totalCompletedPaperCatch,
                    CompletedTotalQuantityInPaper = totalCompletedPaperQuantity
                });

                return Ok(result.OrderBy(x => ((dynamic)x).ProcessId.ToString()).ToList());
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred", error = ex.Message });
            }
        }



        [HttpGet("Process-Production-Report-Project-Wise")]
        public async Task<IActionResult> GetProcessWiseProjectReportByDateRange(
    [FromQuery] string? date,
    [FromQuery] string? startDate,
    [FromQuery] string? endDate,
    [FromQuery] int? processId)
        {
            try
            {
                if (!processId.HasValue || processId.Value <= 0)
                    return BadRequest("processId is required and must be greater than 0.");

                DateTime? parsedDate = null;
                DateTime? parsedStartDate = null;
                DateTime? parsedEndDate = null;

                if (!string.IsNullOrEmpty(date))
                {
                    if (!DateTime.TryParseExact(date, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
                        return BadRequest("Invalid date format. Use dd-MM-yyyy.");
                    parsedDate = parsed.Date;
                }

                if (!string.IsNullOrEmpty(startDate))
                {
                    if (!DateTime.TryParseExact(startDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedStart))
                        return BadRequest("Invalid startDate format. Use dd-MM-yyyy.");
                    parsedStartDate = parsedStart.Date;
                }

                if (!string.IsNullOrEmpty(endDate))
                {
                    if (!DateTime.TryParseExact(endDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedEnd))
                        return BadRequest("Invalid endDate format. Use dd-MM-yyyy.");
                    parsedEndDate = parsedEnd.Date;
                }

                // Step 1: Filter EventLogs
                var eventLogQuery = _context.EventLogs
                    .Where(el => el.Category == "Transaction"
                        && el.Event == "Status Updated"
                        && el.OldValue == "1"
                        && el.NewValue == "2"
                        && el.TransactionId != null);

                if (parsedStartDate.HasValue && parsedEndDate.HasValue)
                {
                    eventLogQuery = eventLogQuery.Where(el => el.LoggedAT.Date >= parsedStartDate.Value && el.LoggedAT.Date <= parsedEndDate.Value);
                }
                else if (parsedDate.HasValue)
                {
                    eventLogQuery = eventLogQuery.Where(el => el.LoggedAT.Date == parsedDate.Value);
                }

                var filteredLogs = await eventLogQuery.ToListAsync();
                var validTransactionIds = filteredLogs.Select(el => el.TransactionId.Value).Distinct().ToList();

                // Step 2: Get Transactions and their ProjectIds
                var transactions = await _context.Transaction
                    .Where(t => validTransactionIds.Contains(t.TransactionId) && t.ProcessId == processId)
                    .ToListAsync();

                if (!transactions.Any())
                    return Ok(new List<object>());

                var projectIds = transactions.Select(t => t.ProjectId).Distinct().ToList();

                // Step 3: Get Project with TypeId
                var projects = await _context.Projects
                    .Where(p => projectIds.Contains(p.ProjectId))
                    .ToDictionaryAsync(p => p.ProjectId, p => p.TypeId); // 1 = Booklet, 2 = Paper

                // Step 4: Segregate transactions by TypeId
                var bookletTransactions = transactions
                    .Where(t => projects.ContainsKey(t.ProjectId) && projects[t.ProjectId] == 1)
                    .Select(t => new { t.TransactionId, t.QuantitysheetId, t.ProjectId })
                    .ToList();

                var paperTransactions = transactions
                    .Where(t => projects.ContainsKey(t.ProjectId) && projects[t.ProjectId] == 2)
                    .Select(t => new { t.TransactionId, t.QuantitysheetId, t.ProjectId })
                    .ToList();

                var allSheetIds = bookletTransactions.Select(t => t.QuantitysheetId)
                    .Concat(paperTransactions.Select(t => t.QuantitysheetId))
                    .Distinct()
                    .ToList();

                var quantitySheets = await _context.QuantitySheets
                    .Where(qs => allSheetIds.Contains(qs.QuantitySheetId))
                    .ToDictionaryAsync(qs => qs.QuantitySheetId, qs => qs.Quantity);

                // Step 5: Group by Project
                var result = new List<object>();

                var allProjectIds = transactions.Select(t => t.ProjectId).Distinct();

                foreach (var projId in allProjectIds)
                {
                    var bookletCatches = bookletTransactions
                        .Where(t => t.ProjectId == projId)
                        .GroupBy(t => t.QuantitysheetId)
                        .Select(g => g.Key)
                        .Distinct()
                        .ToList();

                    var paperCatches = paperTransactions
                        .Where(t => t.ProjectId == projId)
                        .GroupBy(t => t.QuantitysheetId)
                        .Select(g => g.Key)
                        .Distinct()
                        .ToList();

                    var bookletQuantity = bookletCatches
                        .Where(qid => quantitySheets.ContainsKey(qid))
                        .Sum(qid => quantitySheets[qid]);

                    var paperQuantity = paperCatches
                        .Where(qid => quantitySheets.ContainsKey(qid))
                        .Sum(qid => quantitySheets[qid]);

                    result.Add(new
                    {
                        ProjectId = projId,
                        CompletedTotalCatchesInBooklet = bookletCatches.Count,
                        CompletedTotalQuantityInBooklet = bookletQuantity,
                        CompletedTotalCatchesInPaper = paperCatches.Count,
                        CompletedTotalQuantityInPaper = paperQuantity
                    });
                }

                return Ok(result.OrderBy(x => ((dynamic)x).ProjectId).ToList());
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred", error = ex.Message });
            }
        }







        [HttpGet("Process-Pending-Report-Project-Wise")]
        public async Task<IActionResult> GetProcessWiseProjectsReportByDateRange(
           [FromQuery] string? date,
           [FromQuery] string? startDate,
           [FromQuery] string? endDate,
           [FromQuery] int? processId)
        {
            try
            {
                if (!processId.HasValue || processId.Value <= 0)
                    return BadRequest("processId is required and must be greater than 0.");

                DateTime? parsedDate = null;
                DateTime? parsedStartDate = null;
                DateTime? parsedEndDate = null;

                if (!string.IsNullOrEmpty(date))
                {
                    if (!DateTime.TryParseExact(date, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
                        return BadRequest("Invalid date format. Use dd-MM-yyyy.");
                    parsedDate = parsed.Date;
                }

                if (!string.IsNullOrEmpty(startDate))
                {
                    if (!DateTime.TryParseExact(startDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedStart))
                        return BadRequest("Invalid startDate format. Use dd-MM-yyyy.");
                    parsedStartDate = parsedStart.Date;
                }

                if (!string.IsNullOrEmpty(endDate))
                {
                    if (!DateTime.TryParseExact(endDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedEnd))
                        return BadRequest("Invalid endDate format. Use dd-MM-yyyy.");
                    parsedEndDate = parsedEnd.Date;
                }

                var eventLogQuery = _context.EventLogs
                    .Where(el => el.Category == "Transaction"
                        && el.Event == "Status Updated"
                        && el.OldValue == "1"
                        && el.NewValue == "2"
                        && el.TransactionId != null);

                if (parsedStartDate.HasValue && parsedEndDate.HasValue)
                {
                    eventLogQuery = eventLogQuery.Where(el => el.LoggedAT.Date >= parsedStartDate.Value && el.LoggedAT.Date <= parsedEndDate.Value);
                }
                else if (parsedDate.HasValue)
                {
                    eventLogQuery = eventLogQuery.Where(el => el.LoggedAT.Date == parsedDate.Value);
                }

                var filteredLogs = await eventLogQuery.ToListAsync();
                var validTransactionIds = filteredLogs.Select(el => el.TransactionId.Value).Distinct().ToList();

                var transactions = await _context.Transaction
                    .Where(t => validTransactionIds.Contains(t.TransactionId) && t.ProcessId == processId)
                    .ToListAsync();

                if (!transactions.Any())
                    return Ok(new List<object>());

                var projectIds = transactions.Select(t => t.ProjectId).Distinct().ToList();

                var projects = await _context.Projects
                    .Where(p => projectIds.Contains(p.ProjectId))
                    .ToDictionaryAsync(p => p.ProjectId, p => p.TypeId); // 1 = Booklet, 2 = Paper

                var bookletTransactions = transactions
                    .Where(t => projects.ContainsKey(t.ProjectId) && projects[t.ProjectId] == 1)
                    .Select(t => new { t.TransactionId, t.QuantitysheetId, t.ProjectId })
                    .ToList();

                var paperTransactions = transactions
                    .Where(t => projects.ContainsKey(t.ProjectId) && projects[t.ProjectId] == 2)
                    .Select(t => new { t.TransactionId, t.QuantitysheetId, t.ProjectId })
                    .ToList();

                var quantitySheets = await _context.QuantitySheets
                    .Where(qs => projectIds.Contains(qs.ProjectId))
                    .ToDictionaryAsync(qs => qs.QuantitySheetId, qs => new { qs.Quantity, qs.ProjectId });

                var result = new List<object>();

                foreach (var projId in projectIds)
                {
                    var typeId = projects.ContainsKey(projId) ? projects[projId] : (int?)null;

                    var bookletCatches = bookletTransactions
                        .Where(t => t.ProjectId == projId)
                        .Select(t => t.QuantitysheetId)
                        .Distinct()
                        .ToList();

                    var bookletQuantity = bookletCatches
                        .Where(qid => quantitySheets.ContainsKey(qid))
                        .Sum(qid => (int)quantitySheets[qid].Quantity);

                    var paperCatches = paperTransactions
                        .Where(t => t.ProjectId == projId)
                        .Select(t => t.QuantitysheetId)
                        .Distinct()
                        .ToList();

                    var paperQuantity = paperCatches
                        .Where(qid => quantitySheets.ContainsKey(qid))
                        .Sum(qid => (int)quantitySheets[qid].Quantity);

                    var allSheetIdsForProject = quantitySheets
                        .Where(q => q.Value.ProjectId == projId)
                        .Select(q => q.Key)
                        .Distinct()
                        .ToList();

                    var totalProjectQuantity = allSheetIdsForProject
                        .Sum(qid => (int)quantitySheets[qid].Quantity);

                    var totalProjectCatchCount = allSheetIdsForProject.Count;

                    int? pendingCountOfCatchesInPaper = null;
                    int? pendingQuantityInPaper = null;
                    if (paperCatches.Count > 0 && paperQuantity > 0)
                    {
                        pendingCountOfCatchesInPaper = totalProjectCatchCount - paperCatches.Count;
                        pendingQuantityInPaper = totalProjectQuantity - paperQuantity;
                    }

                    int? pendingCountOfCatchesInBooklet = null;
                    int? pendingQuantityInBooklet = null;
                    if (bookletCatches.Count > 0 && bookletQuantity > 0)
                    {
                        pendingCountOfCatchesInBooklet = totalProjectCatchCount - bookletCatches.Count;
                        pendingQuantityInBooklet = totalProjectQuantity - bookletQuantity;
                    }

                    Dictionary<int, double> pendingCatches = new();

                    if (typeId == 1)
                    {
                        var used = new HashSet<int>(paperCatches);
                        pendingCatches = quantitySheets
                            .Where(q => q.Value.ProjectId == projId && !used.Contains(q.Key))
                            .ToDictionary(q => q.Key, q => q.Value.Quantity);
                    }
                    else if (typeId == 2)
                    {
                        var used = new HashSet<int>(bookletCatches);
                        pendingCatches = quantitySheets
                            .Where(q => q.Value.ProjectId == projId && !used.Contains(q.Key))
                            .ToDictionary(q => q.Key, q => q.Value.Quantity);
                    }

                    result.Add(new
                    {
                        ProjectId = projId,
                        TypeId = typeId,
                        CompletedTotalCatchesInBooklet = bookletCatches.Count,
                        CompletedTotalQuantityInBooklet = bookletQuantity,
                        CompletedTotalCatchesInPaper = paperCatches.Count,
                        CompletedTotalQuantityInPaper = paperQuantity,
                        ProjectTotalCatchCount = totalProjectCatchCount,
                        ProjectTotalQuantity = totalProjectQuantity,
                        PendingCountOfCatchesInPaper = pendingCountOfCatchesInPaper,
                        PendingQuantityInPaper = pendingQuantityInPaper,
                        PendingCountOfCatchesInBooklet = pendingCountOfCatchesInBooklet,
                        PendingQuantityInBooklet = pendingQuantityInBooklet,
                        //PendingCatches = pendingCatches
                    });
                }

                return Ok(result.OrderBy(x => ((dynamic)x).ProjectId).ToList());
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred", error = ex.Message });
            }
        }









        [HttpGet("pending-process-report")]
        public async Task<IActionResult> GetProcessesWiseDataByDateRange(
           [FromQuery] string? date,
           [FromQuery] string? startDate,
           [FromQuery] string? endDate)
        {
            try
            {
                DateTime? parsedDate = null;
                DateTime? parsedStartDate = null;
                DateTime? parsedEndDate = null;

                if (!string.IsNullOrEmpty(date))
                {
                    if (!DateTime.TryParseExact(date, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
                        return BadRequest("Invalid date format. Use dd-MM-yyyy.");
                    parsedDate = parsed.Date;
                }

                if (!string.IsNullOrEmpty(startDate))
                {
                    if (!DateTime.TryParseExact(startDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedStart))
                        return BadRequest("Invalid startDate format. Use dd-MM-yyyy.");
                    parsedStartDate = parsedStart.Date;
                }

                if (!string.IsNullOrEmpty(endDate))
                {
                    if (!DateTime.TryParseExact(endDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedEnd))
                        return BadRequest("Invalid endDate format. Use dd-MM-yyyy.");
                    parsedEndDate = parsedEnd.Date;
                }

                var eventLogQuery = _context.EventLogs
                    .Where(el => el.Category == "Transaction"
                        && el.Event == "Status Updated"
                        && el.OldValue == "1"
                        && el.NewValue == "2"
                        && el.TransactionId != null);

                if (parsedStartDate.HasValue && parsedEndDate.HasValue)
                {
                    eventLogQuery = eventLogQuery.Where(el => el.LoggedAT.Date >= parsedStartDate.Value && el.LoggedAT.Date <= parsedEndDate.Value);
                }
                else if (parsedDate.HasValue)
                {
                    eventLogQuery = eventLogQuery.Where(el => el.LoggedAT.Date == parsedDate.Value);
                }

                var filteredLogs = await eventLogQuery.ToListAsync();
                var validTransactionIds = filteredLogs.Select(el => el.TransactionId.Value).Distinct().ToList();

                var transactions = await _context.Transaction
                    .Where(t => validTransactionIds.Contains(t.TransactionId))
                    .ToListAsync();

                var projectIds = transactions.Select(t => t.ProjectId).Distinct().ToList();

                var projects = await _context.Projects
                    .Where(p => projectIds.Contains(p.ProjectId))
                    .ToDictionaryAsync(p => p.ProjectId, p => p.TypeId);

                var bookletTransactionIds = transactions
                    .Where(t => projects.ContainsKey(t.ProjectId) && projects[t.ProjectId] == 1)
                    .Select(t => new { t.TransactionId, t.QuantitysheetId, t.ProcessId, t.ProjectId })
                    .ToList();

                var paperTransactionIds = transactions
                    .Where(t => projects.ContainsKey(t.ProjectId) && projects[t.ProjectId] == 2)
                    .Select(t => new { t.TransactionId, t.QuantitysheetId, t.ProcessId, t.ProjectId })
                    .ToList();

                var allQuantitySheets = await _context.QuantitySheets.ToListAsync();

                var result = new List<object>();
                var allProcessIds = transactions.Select(t => t.ProcessId).Distinct();

                int totalPendingCatchInBooklet = 0;
                int totalPendingCatchInPaper = 0;
                double totalPendingQtyInBooklet = 0;
                double totalPendingQtyInPaper = 0;

                foreach (var processId in allProcessIds)
                {
                    var processBookletTxns = bookletTransactionIds.Where(t => t.ProcessId == processId).ToList();
                    var processPaperTxns = paperTransactionIds.Where(t => t.ProcessId == processId).ToList();

                    int completedBookletCatch = processBookletTxns.Select(t => t.QuantitysheetId).Distinct().Count();
                    double completedBookletQty = processBookletTxns.Select(t => t.QuantitysheetId).Distinct()
                        .Where(qid => allQuantitySheets.Any(q => q.QuantitySheetId == qid))
                        .Sum(qid => (double)allQuantitySheets.First(q => q.QuantitySheetId == qid).Quantity);

                    int completedPaperCatch = processPaperTxns.Select(t => t.QuantitysheetId).Distinct().Count();
                    double completedPaperQty = processPaperTxns.Select(t => t.QuantitysheetId).Distinct()
                        .Where(qid => allQuantitySheets.Any(q => q.QuantitySheetId == qid))
                        .Sum(qid => (double)allQuantitySheets.First(q => q.QuantitySheetId == qid).Quantity);

                    var projectIdsInProcess = transactions.Where(t => t.ProcessId == processId).Select(t => t.ProjectId).Distinct().ToList();

                    int totalBookletCatch = allQuantitySheets.Where(q => projectIdsInProcess.Contains(q.ProjectId) && projects[q.ProjectId] == 1).Count();
                    double totalBookletQty = allQuantitySheets.Where(q => projectIdsInProcess.Contains(q.ProjectId) && projects[q.ProjectId] == 1).Sum(q => (double)q.Quantity);

                    int totalPaperCatch = allQuantitySheets.Where(q => projectIdsInProcess.Contains(q.ProjectId) && projects[q.ProjectId] == 2).Count();
                    double totalPaperQty = allQuantitySheets.Where(q => projectIdsInProcess.Contains(q.ProjectId) && projects[q.ProjectId] == 2).Sum(q => (double)q.Quantity);

                    int pendingCatchBooklet = totalBookletCatch - completedBookletCatch;
                    double pendingQtyBooklet = totalBookletQty - completedBookletQty;
                    int pendingCatchPaper = totalPaperCatch - completedPaperCatch;
                    double pendingQtyPaper = totalPaperQty - completedPaperQty;

                    totalPendingCatchInBooklet += pendingCatchBooklet;
                    totalPendingQtyInBooklet += pendingQtyBooklet;
                    totalPendingCatchInPaper += pendingCatchPaper;
                    totalPendingQtyInPaper += pendingQtyPaper;

                    result.Add(new
                    {
                        processId = processId,
                        PendingCatchInBooklet = pendingCatchBooklet,
                        PendingQuantityInBooklet = pendingQtyBooklet,
                        PendingCatchInPaper = pendingCatchPaper,
                        PendingQuantityInPaper = pendingQtyPaper
                    });
                }

                result.Add(new
                {
                    processId = "Total",
                    PendingCatchInBooklet = totalPendingCatchInBooklet,
                    PendingQuantityInBooklet = totalPendingQtyInBooklet,
                    PendingCatchInPaper = totalPendingCatchInPaper,
                    PendingQuantityInPaper = totalPendingQtyInPaper
                });

                return Ok(result.OrderBy(r => ((dynamic)r).processId.ToString() == "Total" ? int.MaxValue : Convert.ToInt32(((dynamic)r).processId)).ToList());
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred", error = ex.Message });
            }
        }

        [HttpGet("Process-Production-Report-Group-Wise")]
        public async Task<IActionResult> GetGroupProcessWiseProjectReportByDateRange(
            [FromQuery] string? date,
            [FromQuery] string? startDate,
            [FromQuery] string? endDate,
            [FromQuery] int? processId,
            [FromQuery] int? groupId,
            [FromQuery] int? projectId)
        {
            try
            {
                DateTime? parsedDate = null;
                DateTime? parsedStartDate = null;
                DateTime? parsedEndDate = null;

                if (!string.IsNullOrEmpty(date) && DateTime.TryParseExact(date, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
                    parsedDate = parsed.Date;
                else if (!string.IsNullOrEmpty(date))
                    return BadRequest("Invalid date format. Use dd-MM-yyyy.");

                if (!string.IsNullOrEmpty(startDate) && DateTime.TryParseExact(startDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedStart))
                    parsedStartDate = parsedStart.Date;
                else if (!string.IsNullOrEmpty(startDate))
                    return BadRequest("Invalid startDate format. Use dd-MM-yyyy.");

                if (!string.IsNullOrEmpty(endDate) && DateTime.TryParseExact(endDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedEnd))
                    parsedEndDate = parsedEnd.Date;
                else if (!string.IsNullOrEmpty(endDate))
                    return BadRequest("Invalid endDate format. Use dd-MM-yyyy.");

                var eventLogQuery = _context.EventLogs
                    .Where(el => el.Category == "Transaction"
                        && el.Event == "Status Updated"
                        && el.OldValue == "1"
                        && el.NewValue == "2"
                        && el.TransactionId != null);

                if (parsedStartDate.HasValue && parsedEndDate.HasValue)
                    eventLogQuery = eventLogQuery.Where(el => el.LoggedAT.Date >= parsedStartDate.Value && el.LoggedAT.Date <= parsedEndDate.Value);
                else if (parsedDate.HasValue)
                    eventLogQuery = eventLogQuery.Where(el => el.LoggedAT.Date == parsedDate.Value);

                var filteredLogs = await eventLogQuery.ToListAsync();
                var validTransactionIds = filteredLogs.Select(el => el.TransactionId.Value).Distinct().ToList();

                var transactionQuery = _context.Transaction.Where(t => validTransactionIds.Contains(t.TransactionId));

                if (processId.HasValue && processId.Value > 0)
                    transactionQuery = transactionQuery.Where(t => t.ProcessId == processId.Value);

                if (projectId.HasValue && projectId.Value > 0)
                    transactionQuery = transactionQuery.Where(t => t.ProjectId == projectId.Value);

                var allMatchingTransactions = await transactionQuery.ToListAsync();

                if (!allMatchingTransactions.Any())
                    return Ok(new List<object>());

                var involvedProjectIds = allMatchingTransactions.Select(t => t.ProjectId).Distinct().ToList();

                var projectQuery = _context.Projects.Where(p => involvedProjectIds.Contains(p.ProjectId));

                if (groupId.HasValue && groupId.Value > 0)
                    projectQuery = projectQuery.Where(p => p.GroupId == groupId.Value);

                var projects = await projectQuery
                    .ToDictionaryAsync(p => p.ProjectId, p => new { p.TypeId, p.GroupId });

                var validTransactions = allMatchingTransactions
                    .Where(t => projects.ContainsKey(t.ProjectId))
                    .ToList();

                if (!validTransactions.Any())
                    return Ok(new List<object>());

                var bookletTransactions = validTransactions
                    .Where(t => projects[t.ProjectId].TypeId == 1)
                    .Select(t => new { t.TransactionId, t.QuantitysheetId, t.ProjectId, t.LotNo })
                    .ToList();

                var paperTransactions = validTransactions
                    .Where(t => projects[t.ProjectId].TypeId == 2)
                    .Select(t => new { t.TransactionId, t.QuantitysheetId, t.ProjectId, t.LotNo })
                    .ToList();

                var allSheetIds = bookletTransactions.Select(t => t.QuantitysheetId)
                    .Concat(paperTransactions.Select(t => t.QuantitysheetId))
                    .Distinct()
                    .ToList();

                var quantitySheets = await _context.QuantitySheets
                    .Where(qs => allSheetIds.Contains(qs.QuantitySheetId))
                    .ToDictionaryAsync(qs => qs.QuantitySheetId, qs => qs.Quantity);

                var result = new List<object>();

                if (groupId.HasValue || projectId.HasValue)
                {
                    foreach (var projId in validTransactions.Select(t => t.ProjectId).Distinct())
                    {
                        var bookletCatches = bookletTransactions
                            .Where(t => t.ProjectId == projId)
                            .Select(t => t.QuantitysheetId)
                            .Distinct()
                            .ToList();

                        var paperCatches = paperTransactions
                            .Where(t => t.ProjectId == projId)
                            .Select(t => t.QuantitysheetId)
                            .Distinct()
                            .ToList();

                        var bookletQuantity = bookletCatches
                            .Where(qid => quantitySheets.ContainsKey(qid))
                            .Sum(qid => quantitySheets[qid]);

                        var paperQuantity = paperCatches
                            .Where(qid => quantitySheets.ContainsKey(qid))
                            .Sum(qid => quantitySheets[qid]);

                        var lotNos = bookletTransactions
                            .Where(t => t.ProjectId == projId)
                            .Select(t => t.LotNo)
                            .Concat(
                                paperTransactions
                                    .Where(t => t.ProjectId == projId)
                                    .Select(t => t.LotNo)
                            )
                            .Distinct()
                            .ToList();

                        result.Add(new
                        {
                            ProjectId = projId,
                            GroupId = projects[projId].GroupId,
                            CompletedTotalCatchesInBooklet = bookletCatches.Count,
                            CompletedTotalQuantityInBooklet = bookletQuantity,
                            CompletedTotalCatchesInPaper = paperCatches.Count,
                            CompletedTotalQuantityInPaper = paperQuantity,
                            BookletCatchList = bookletCatches,
                            PaperCatchList = paperCatches,
                            LotNos = lotNos
                        });
                    }
                }
                else
                {
                    foreach (var group in projects.GroupBy(p => p.Value.GroupId))
                    {
                        var groupIdValue = group.Key;
                        var projectsInGroup = group.ToList();

                        var bookletCatches = bookletTransactions
                            .Where(t => projectsInGroup.Any(p => p.Key == t.ProjectId))
                            .Select(t => t.QuantitysheetId)
                            .Distinct()
                            .ToList();

                        var paperCatches = paperTransactions
                            .Where(t => projectsInGroup.Any(p => p.Key == t.ProjectId))
                            .Select(t => t.QuantitysheetId)
                            .Distinct()
                            .ToList();

                        var bookletQuantity = bookletCatches
                            .Where(qid => quantitySheets.ContainsKey(qid))
                            .Sum(qid => quantitySheets[qid]);

                        var paperQuantity = paperCatches
                            .Where(qid => quantitySheets.ContainsKey(qid))
                            .Sum(qid => quantitySheets[qid]);

                        result.Add(new
                        {
                            GroupId = groupIdValue,
                            CompletedTotalCatchesInBooklet = bookletCatches.Count,
                            CompletedTotalQuantityInBooklet = bookletQuantity,
                            CompletedTotalCatchesInPaper = paperCatches.Count,
                            CompletedTotalQuantityInPaper = paperQuantity
                        });
                    }
                }

                return Ok(result.OrderBy(x => ((dynamic)x).GroupId).ToList());
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred", error = ex.Message });
            }
        }






        //[HttpGet("Process-Production-Report-Group-Wise")]
        //public async Task<IActionResult> GetGroupProcessWiseDataByDateRange(
        //   [FromQuery] string? date,
        //   [FromQuery] string? startDate,
        //   [FromQuery] string? endDate,
        //   [FromQuery] List<int>? groupId) // 👈 groupId as array
        //{
        //    try
        //    {
        //        DateTime? parsedDate = null;
        //        DateTime? parsedStartDate = null;
        //        DateTime? parsedEndDate = null;

        //        if (!string.IsNullOrEmpty(date))
        //        {
        //            if (!DateTime.TryParseExact(date, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
        //                return BadRequest("Invalid date format. Use dd-MM-yyyy.");
        //            parsedDate = parsed.Date;
        //        }

        //        if (!string.IsNullOrEmpty(startDate))
        //        {
        //            if (!DateTime.TryParseExact(startDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedStart))
        //                return BadRequest("Invalid startDate format. Use dd-MM-yyyy.");
        //            parsedStartDate = parsedStart.Date;
        //        }

        //        if (!string.IsNullOrEmpty(endDate))
        //        {
        //            if (!DateTime.TryParseExact(endDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedEnd))
        //                return BadRequest("Invalid endDate format. Use dd-MM-yyyy.");
        //            parsedEndDate = parsedEnd.Date;
        //        }

        //        // Step 1: Filter EventLogs
        //        var eventLogQuery = _context.EventLogs
        //            .Where(el => el.Category == "Transaction"
        //                && el.Event == "Status Updated"
        //                && el.OldValue == "1"
        //                && el.NewValue == "2"
        //                && el.TransactionId != null);

        //        if (parsedStartDate.HasValue && parsedEndDate.HasValue)
        //        {
        //            eventLogQuery = eventLogQuery.Where(el => el.LoggedAT.Date >= parsedStartDate.Value && el.LoggedAT.Date <= parsedEndDate.Value);
        //        }
        //        else if (parsedDate.HasValue)
        //        {
        //            eventLogQuery = eventLogQuery.Where(el => el.LoggedAT.Date == parsedDate.Value);
        //        }

        //        var filteredLogs = await eventLogQuery.ToListAsync();
        //        var validTransactionIds = filteredLogs.Select(el => el.TransactionId.Value).Distinct().ToList();

        //        // Step 2: Get Transactions
        //        var transactions = await _context.Transaction
        //            .Where(t => validTransactionIds.Contains(t.TransactionId))
        //            .ToListAsync();

        //        var projectIds = transactions.Select(t => t.ProjectId).Distinct().ToList();

        //        // Step 3: Get Projects with TypeId and GroupId
        //        var projectQuery = _context.Projects
        //            .Where(p => projectIds.Contains(p.ProjectId));

        //        if (groupId != null && groupId.Any()) // 👈 filter by groupId list
        //            projectQuery = projectQuery.Where(p => groupId.Contains(p.GroupId));

        //        var projectDict = await projectQuery
        //            .ToDictionaryAsync(p => p.ProjectId, p => new { p.TypeId, p.GroupId });

        //        if (!projectDict.Any())
        //            return Ok(new List<object>()); // No matching projects

        //        // Step 4: Filter Transactions by valid projects
        //        transactions = transactions
        //            .Where(t => projectDict.ContainsKey(t.ProjectId))
        //            .ToList();

        //        // Step 5: Segregate transactions by TypeId
        //        var bookletTransactions = transactions
        //            .Where(t => projectDict[t.ProjectId].TypeId == 1)
        //            .Select(t => new { t.TransactionId, t.QuantitysheetId, t.ProcessId, t.ProjectId })
        //            .ToList();

        //        var paperTransactions = transactions
        //            .Where(t => projectDict[t.ProjectId].TypeId == 2)
        //            .Select(t => new { t.TransactionId, t.QuantitysheetId, t.ProcessId, t.ProjectId })
        //            .ToList();

        //        var bookletSheetIds = bookletTransactions.Select(x => x.QuantitysheetId).Distinct().ToList();
        //        var paperSheetIds = paperTransactions.Select(x => x.QuantitysheetId).Distinct().ToList();

        //        var quantitySheets = await _context.QuantitySheets
        //            .Where(qs => bookletSheetIds.Contains(qs.QuantitySheetId) || paperSheetIds.Contains(qs.QuantitySheetId))
        //            .ToListAsync();

        //        var quantitySheetDict = quantitySheets.ToDictionary(qs => qs.QuantitySheetId, qs => qs.Quantity);

        //        // Step 6: Group and project
        //        var result = new List<object>();

        //        double totalCompletedBookletQuantity = 0;
        //        int totalCompletedBookletCatch = 0;

        //        double totalCompletedPaperQuantity = 0;
        //        int totalCompletedPaperCatch = 0;

        //        var allProcessIds = transactions.Select(t => t.ProcessId).Distinct();

        //        foreach (var processId in allProcessIds)
        //        {
        //            var bookletCatches = bookletTransactions
        //                .Where(t => t.ProcessId == processId)
        //                .GroupBy(t => t.QuantitysheetId)
        //                .Select(g => g.Key)
        //                .Distinct()
        //                .ToList();

        //            var paperCatches = paperTransactions
        //                .Where(t => t.ProcessId == processId)
        //                .GroupBy(t => t.QuantitysheetId)
        //                .Select(g => g.Key)
        //                .Distinct()
        //                .ToList();

        //            var bookletQuantity = bookletCatches
        //                .Where(qid => quantitySheetDict.ContainsKey(qid))
        //                .Sum(qid => quantitySheetDict[qid]);

        //            var paperQuantity = paperCatches
        //                .Where(qid => quantitySheetDict.ContainsKey(qid))
        //                .Sum(qid => quantitySheetDict[qid]);

        //            totalCompletedBookletCatch += bookletCatches.Count;
        //            totalCompletedBookletQuantity += bookletQuantity;

        //            totalCompletedPaperCatch += paperCatches.Count;
        //            totalCompletedPaperQuantity += paperQuantity;

        //            var groupIds = transactions
        //                .Where(t => t.ProcessId == processId && projectDict.ContainsKey(t.ProjectId))
        //                .Select(t => projectDict[t.ProjectId].GroupId)
        //                .Distinct()
        //                .ToList();

        //            result.Add(new
        //            {
        //                ProcessId = processId,
        //                GroupIds = groupIds,
        //                CompletedTotalCatchesInBooklet = bookletCatches.Count,
        //                CompletedTotalQuantityInBooklet = bookletQuantity,
        //                CompletedTotalCatchesInPaper = paperCatches.Count,
        //                CompletedTotalQuantityInPaper = paperQuantity
        //            });
        //        }

        //        // Add grand total
        //        result.Add(new
        //        {
        //            ProcessId = "Total",
        //            GroupIds = groupId ?? new List<int>(), // pass groupId array back
        //            CompletedTotalCatchesInBooklet = totalCompletedBookletCatch,
        //            CompletedTotalQuantityInBooklet = totalCompletedBookletQuantity,
        //            CompletedTotalCatchesInPaper = totalCompletedPaperCatch,
        //            CompletedTotalQuantityInPaper = totalCompletedPaperQuantity
        //        });

        //        return Ok(result.OrderBy(x => ((dynamic)x).ProcessId.ToString()).ToList());
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { message = "An error occurred", error = ex.Message });
        //    }
        //}



        //[HttpGet("Process-Pending-Report-Project-Wise")]
        //public async Task<IActionResult> GetProcessWiseProjectsReportByDateRange(
        //    [FromQuery] string? date,
        //    [FromQuery] string? startDate,
        //    [FromQuery] string? endDate,
        //    [FromQuery] int? processId)
        //{
        //    try
        //    {
        //        if (!processId.HasValue || processId.Value <= 0)
        //            return BadRequest("processId is required and must be greater than 0.");

        //        DateTime? parsedDate = null;
        //        DateTime? parsedStartDate = null;
        //        DateTime? parsedEndDate = null;

        //        if (!string.IsNullOrEmpty(date))
        //        {
        //            if (!DateTime.TryParseExact(date, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
        //                return BadRequest("Invalid date format. Use dd-MM-yyyy.");
        //            parsedDate = parsed.Date;
        //        }

        //        if (!string.IsNullOrEmpty(startDate))
        //        {
        //            if (!DateTime.TryParseExact(startDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedStart))
        //                return BadRequest("Invalid startDate format. Use dd-MM-yyyy.");
        //            parsedStartDate = parsedStart.Date;
        //        }

        //        if (!string.IsNullOrEmpty(endDate))
        //        {
        //            if (!DateTime.TryParseExact(endDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedEnd))
        //                return BadRequest("Invalid endDate format. Use dd-MM-yyyy.");
        //            parsedEndDate = parsedEnd.Date;
        //        }

        //        var eventLogQuery = _context.EventLogs
        //            .Where(el => el.Category == "Transaction"
        //                && el.Event == "Status Updated"
        //                && el.OldValue == "1"
        //                && el.NewValue == "2"
        //                && el.TransactionId != null);

        //        if (parsedStartDate.HasValue && parsedEndDate.HasValue)
        //        {
        //            eventLogQuery = eventLogQuery.Where(el => el.LoggedAT.Date >= parsedStartDate.Value && el.LoggedAT.Date <= parsedEndDate.Value);
        //        }
        //        else if (parsedDate.HasValue)
        //        {
        //            eventLogQuery = eventLogQuery.Where(el => el.LoggedAT.Date == parsedDate.Value);
        //        }

        //        var filteredLogs = await eventLogQuery.ToListAsync();
        //        var validTransactionIds = filteredLogs.Select(el => el.TransactionId.Value).Distinct().ToList();

        //        var transactions = await _context.Transaction
        //            .Where(t => validTransactionIds.Contains(t.TransactionId) && t.ProcessId == processId)
        //            .ToListAsync();

        //        if (!transactions.Any())
        //            return Ok(new List<object>());

        //        var projectIds = transactions.Select(t => t.ProjectId).Distinct().ToList();

        //        var projects = await _context.Projects
        //            .Where(p => projectIds.Contains(p.ProjectId))
        //            .ToDictionaryAsync(p => p.ProjectId, p => p.TypeId); // 1 = Booklet, 2 = Paper

        //        var bookletTransactions = transactions
        //            .Where(t => projects.ContainsKey(t.ProjectId) && projects[t.ProjectId] == 1)
        //            .Select(t => new { t.TransactionId, t.QuantitysheetId, t.ProjectId })
        //            .ToList();

        //        var paperTransactions = transactions
        //            .Where(t => projects.ContainsKey(t.ProjectId) && projects[t.ProjectId] == 2)
        //            .Select(t => new { t.TransactionId, t.QuantitysheetId, t.ProjectId })
        //            .ToList();

        //        var quantitySheets = await _context.QuantitySheets
        //            .Where(qs => projectIds.Contains(qs.ProjectId))
        //            .ToDictionaryAsync(qs => qs.QuantitySheetId, qs => new
        //            {
        //                qs.Quantity,
        //                qs.ProjectId,
        //                qs.CatchNo // Ensure CatchNo is a valid column
        //            });

        //        var result = new List<object>();

        //        foreach (var projId in projectIds)
        //        {
        //            var typeId = projects.ContainsKey(projId) ? projects[projId] : (int?)null;

        //            var bookletCatches = bookletTransactions
        //                .Where(t => t.ProjectId == projId)
        //                .Select(t => t.QuantitysheetId)
        //                .Distinct()
        //                .ToList();

        //            var bookletQuantity = bookletCatches
        //                .Where(qid => quantitySheets.ContainsKey(qid))
        //                .Sum(qid => (int)quantitySheets[qid].Quantity);

        //            var paperCatches = paperTransactions
        //                .Where(t => t.ProjectId == projId)
        //                .Select(t => t.QuantitysheetId)
        //                .Distinct()
        //                .ToList();

        //            var paperQuantity = paperCatches
        //                .Where(qid => quantitySheets.ContainsKey(qid))
        //                .Sum(qid => (int)quantitySheets[qid].Quantity);

        //            var allSheetIdsForProject = quantitySheets
        //                .Where(q => q.Value.ProjectId == projId)
        //                .Select(q => q.Key)
        //                .Distinct()
        //                .ToList();

        //            var totalProjectQuantity = allSheetIdsForProject
        //                .Sum(qid => (int)quantitySheets[qid].Quantity);

        //            var totalProjectCatchCount = allSheetIdsForProject.Count;

        //            int? pendingCountOfCatchesInPaper = null;
        //            int? pendingQuantityInPaper = null;
        //            if (paperCatches.Count > 0 && paperQuantity > 0)
        //            {
        //                pendingCountOfCatchesInPaper = totalProjectCatchCount - paperCatches.Count;
        //                pendingQuantityInPaper = totalProjectQuantity - paperQuantity;
        //            }

        //            int? pendingCountOfCatchesInBooklet = null;
        //            int? pendingQuantityInBooklet = null;
        //            if (bookletCatches.Count > 0 && bookletQuantity > 0)
        //            {
        //                pendingCountOfCatchesInBooklet = totalProjectCatchCount - bookletCatches.Count;
        //                pendingQuantityInBooklet = totalProjectQuantity - bookletQuantity;
        //            }

        //            var pendingCatchList = quantitySheets
        //                .Where(q =>
        //                    q.Value.ProjectId == projId &&
        //                    !bookletCatches.Contains(q.Key) &&
        //                    !paperCatches.Contains(q.Key))
        //                .Select(q => new
        //                {
        //                    QuantitySheetId = q.Key,
        //                    CatchNo = q.Value.CatchNo
        //                })
        //                .ToList();

        //            result.Add(new
        //            {
        //                ProjectId = projId,
        //                TypeId = typeId,
        //                CompletedTotalCatchesInBooklet = bookletCatches.Count,
        //                CompletedTotalQuantityInBooklet = bookletQuantity,
        //                CompletedTotalCatchesInPaper = paperCatches.Count,
        //                CompletedTotalQuantityInPaper = paperQuantity,
        //                ProjectTotalCatchCount = totalProjectCatchCount,
        //                ProjectTotalQuantity = totalProjectQuantity,
        //                PendingCountOfCatchesInPaper = pendingCountOfCatchesInPaper,
        //                PendingQuantityInPaper = pendingQuantityInPaper,
        //                PendingCountOfCatchesInBooklet = pendingCountOfCatchesInBooklet,
        //                PendingQuantityInBooklet = pendingQuantityInBooklet,
        //                PendingCatches = pendingCatchList
        //            });
        //        }

        //        return Ok(result.OrderBy(x => ((dynamic)x).ProjectId).ToList());
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { message = "An error occurred", error = ex.Message });
        //    }
        //}



    }
}