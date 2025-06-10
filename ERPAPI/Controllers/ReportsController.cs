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
                        // Deduplicate QuantitySheets
                        var uniqueSheets = g
                            .GroupBy(x => x.qs.QuantitySheetId)
                            .Select(x => x.First())
                            .ToList();

                        var examDates = uniqueSheets
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
                            to = maxExamDate,
                            CountOfCatches = uniqueSheets.Count,
                            TotalQuantity = uniqueSheets.Sum(x => x.qs.Quantity)
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


        [HttpGet("Process-Wise")]
        public async Task<IActionResult> GetUniqueTeamAndMachineIds(string date, int processId)
        {
            try
            {
                // ✅ Validate & Parse Date
                if (!DateTime.TryParseExact(date, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
                {
                    return BadRequest("Invalid date format. Please use dd-MM-yyyy.");
                }

                // ✅ Fetch matching transactions first
                var eventLogs = await _context.EventLogs
                    .Where(el => el.Category == "Transaction"
                        && el.LoggedAT.Date == parsedDate.Date
                        && el.Event == "Status Updated"
                        && el.OldValue == "1"
                        && el.NewValue == "2")
                    .Join(_context.Transaction,
                        el => el.TransactionId,
                        t => t.TransactionId,
                        (el, t) => new { el, t })
                    .Where(joined => joined.t.ProcessId == processId)
                    .ToListAsync();

                // ✅ Extract unique TeamIds in-memory
                var uniqueTeamIds = eventLogs
                    .AsEnumerable()
                    .SelectMany(joined => joined.t.TeamId ?? new List<int>()) // Handle null lists
                    .Distinct()
                    .OrderBy(id => id)
                    .ToList();

                // ✅ Fetch TeamMembers' Names from User Table
                var teamMembers = await _context.Users
                    .Where(u => uniqueTeamIds.Contains(u.UserId))
                    .Select(u => new { /*u.UserId,*/ Name = u.FirstName + " " + u.LastName })
                    .ToListAsync();

                // ✅ Extract unique MachineIds in-memory
                var uniqueMachineIds = eventLogs
                    .Select(joined => joined.t.MachineId)
                    .Distinct()
                    .OrderBy(id => id)
                    .ToList();

                // ✅ Fetch Machine Names
                var machineData = await _context.Machine
                    .Where(m => uniqueMachineIds.Contains(m.MachineId))
                    .Select(m => new { m.MachineId, m.MachineName })
                    .ToListAsync();

                // ✅ Fetch unique EventTriggeredBy IDs
                var uniqueEventTriggeredByIds = await _context.EventLogs
                    .Where(el => el.Category == "Transaction"
                        && el.LoggedAT.Date == parsedDate.Date
                        && el.Event == "Status Updated"
                        && el.OldValue == "1"
                        && el.NewValue == "2")
                    .Join(_context.Transaction,
                        el => el.TransactionId,
                        t => t.TransactionId,
                        (el, t) => new { el, t })
                    .Join(_context.QuantitySheets,
                        joined => joined.t.QuantitysheetId,
                        qs => qs.QuantitySheetId,
                        (joined, qs) => new { joined.el, joined.t, qs })
                    .Where(finalJoin => finalJoin.t.ProcessId == processId)
                    .Select(finalJoin => finalJoin.el.EventTriggeredBy)
                    .Distinct()
                    .ToListAsync();

                // ✅ Fetch Supervisor Names from User Table
                var supervisors = await _context.Users
                    .Where(u => uniqueEventTriggeredByIds.Contains(u.UserId))
                    .Select(u => new { /*u.UserId,*/ Name = u.FirstName + " " + u.LastName })
                    .ToListAsync();

                // ✅ Count QuantitySheetIds
                var quantitySheetCount = await _context.EventLogs
                    .Where(el => el.Category == "Transaction"
                        && el.LoggedAT.Date == parsedDate.Date
                        && el.Event == "Status Updated"
                        && el.OldValue == "1"
                        && el.NewValue == "2")
                    .Join(_context.Transaction,
                        el => el.TransactionId,
                        t => t.TransactionId,
                        (el, t) => new { el, t })
                    .Join(_context.QuantitySheets,
                        joined => joined.t.QuantitysheetId,
                        qs => qs.QuantitySheetId,
                        (joined, qs) => new { joined.el, joined.t, qs })
                    .Where(finalJoin => finalJoin.t.ProcessId == processId)
                    .Select(finalJoin => finalJoin.qs.QuantitySheetId)
                    .Distinct()
                    .CountAsync();

                // ✅ Fetch FirstLoggedAt and LastLoggedAt
                var logDates = await _context.EventLogs
                    .Where(el => el.Category == "Transaction"
                        && el.LoggedAT.Date == parsedDate.Date
                        && el.Event == "Status Updated"
                        && el.OldValue == "1"
                        && el.NewValue == "2")
                    .Join(_context.Transaction,
                        el => el.TransactionId,
                        t => t.TransactionId,
                        (el, t) => new { el, t })
                    .Join(_context.QuantitySheets,
                        joined => joined.t.QuantitysheetId,
                        qs => qs.QuantitySheetId,
                        (joined, qs) => new { joined.el, joined.t, qs })
                    .Where(finalJoin => finalJoin.t.ProcessId == processId)
                    .Select(finalJoin => finalJoin.el.LoggedAT)
                    .ToListAsync();

                var firstLoggedAt = logDates.Min();
                var lastLoggedAt = logDates.Max();

                // ✅ Calculate Time Difference
                TimeSpan timeDifference = lastLoggedAt - firstLoggedAt;
                string formattedTimeDifference = $"{timeDifference.Days}d:{timeDifference.Hours}h:{timeDifference.Minutes}m:{timeDifference.Seconds}s";

                // ✅ Calculate Total Quantity
                var totalQuantity = await _context.EventLogs
                    .Where(el => el.Category == "Transaction"
                        && el.LoggedAT.Date == parsedDate.Date
                        && el.Event == "Status Updated"
                        && el.OldValue == "1"
                        && el.NewValue == "2")
                    .Join(_context.Transaction,
                        el => el.TransactionId,
                        t => t.TransactionId,
                        (el, t) => new { el, t })
                    .Join(_context.QuantitySheets,
                        joined => joined.t.QuantitysheetId,
                        qs => qs.QuantitySheetId,
                        (joined, qs) => new { joined.el, joined.t, qs })
                    .Where(finalJoin => finalJoin.t.ProcessId == processId)
                    .SumAsync(finalJoin => finalJoin.qs.Quantity);

                // ✅ Fetch Distinct Lot Numbers
                var distinctLotNos = await _context.EventLogs
                    .Where(el => el.Category == "Transaction"
                        && el.LoggedAT.Date == parsedDate.Date
                        && el.Event == "Status Updated"
                        && el.OldValue == "1"
                        && el.NewValue == "2")
                    .Join(_context.Transaction,
                        el => el.TransactionId,
                        t => t.TransactionId,
                        (el, t) => new { el, t })
                    .Where(joined => joined.t.ProcessId == processId)
                    .Select(joined => joined.t.LotNo)
                    .Distinct()
                    .ToListAsync();

                // ✅ Return Data
                return Ok(new
                {
                    TeamMembers = teamMembers,
                    Machines = machineData,
                    Supervisors = supervisors,
                    TotalCatches = quantitySheetCount,
                    FirstLoggedAt = firstLoggedAt,
                    LastLoggedAt = lastLoggedAt,
                    TimeDifference = formattedTimeDifference,
                    TotalQuantity = totalQuantity,
                    DistinctLotNos = distinctLotNos // Add this line to include distinct lot numbers in the response
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred", error = ex.Message });
            }
        }



        [HttpGet("quickCompletion")]
        public async Task<IActionResult> GetQuickCompletion(
     [FromQuery] string? date,
     [FromQuery] string? startDate,
     [FromQuery] string? endDate,
     [FromQuery] int page = 1,
     [FromQuery] int pageSize = 10)
        {
            DateTime startDateTime, endDateTime;

            if (!string.IsNullOrEmpty(date))
            {
                if (!DateTime.TryParseExact(date, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out startDateTime))
                {
                    return BadRequest("Invalid 'date' format. Use dd-MM-yyyy.");
                }
                endDateTime = startDateTime.AddDays(1);
            }
            else if (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate))
            {
                if (!DateTime.TryParseExact(startDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out startDateTime))
                    return BadRequest("Invalid 'startDate' format. Use dd-MM-yyyy.");

                if (!DateTime.TryParseExact(endDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out endDateTime))
                    return BadRequest("Invalid 'endDate' format. Use dd-MM-yyyy.");

                endDateTime = endDateTime.AddDays(1); // Make endDate inclusive
            }
            else
            {
                return BadRequest("Please provide either 'date' or both 'startDate' and 'endDate'.");
            }

            var logs = await _context.EventLogs
                .Where(e => e.Event == "Status updated"
                            && e.LoggedAT >= startDateTime
                            && e.LoggedAT < endDateTime)
                .ToListAsync();

            var transactionIds = logs.Select(e => e.TransactionId).Distinct().ToList();

            var transactions = await _context.Transaction
                .Where(t => transactionIds.Contains(t.TransactionId))
                .ToListAsync();

            var quantitySheetIds = transactions.Select(t => t.QuantitysheetId).Distinct().ToList();

            var quantitySheets = await _context.QuantitySheets
                .Where(qs => quantitySheetIds.Contains(qs.QuantitySheetId))
                .ToListAsync();

            var projectIds = transactions.Select(t => t.ProjectId).Distinct().ToList();
            var projects = await _context.Projects
                .Where(p => projectIds.Contains(p.ProjectId))
                .ToListAsync();

            var enrichedLogs = (from log in logs
                                join txn in transactions on log.TransactionId equals txn.TransactionId into txnJoin
                                from txn in txnJoin.DefaultIfEmpty()
                                join qs in quantitySheets on txn?.QuantitysheetId equals qs.QuantitySheetId into qsJoin
                                from qs in qsJoin.DefaultIfEmpty()
                                join proj in projects on txn?.ProjectId equals proj.ProjectId into projJoin
                                from proj in projJoin.DefaultIfEmpty()
                                select new
                                {
                                    Log = log,
                                    TransactionId = txn?.TransactionId,
                                    QuantitySheetId = txn?.QuantitysheetId,
                                    ProjectId = txn?.ProjectId,
                                    GroupId = proj?.GroupId,
                                    CatchNo = qs?.CatchNo,
                                    Quantity = qs?.Quantity
                                }).ToList();

            var matchedLogs = (from a in enrichedLogs
                               from b in enrichedLogs
                               where a.Log.TransactionId == b.Log.TransactionId
                                     && a.Log.EventID != b.Log.EventID
                                     && Math.Abs((a.Log.LoggedAT - b.Log.LoggedAT).TotalMinutes) < 5
                               orderby a.Log.TransactionId, a.Log.LoggedAT
                               select new
                               {
                                   EventID_A = a.Log.EventID,
                                   EventID_B = b.Log.EventID,
                                   Event_A = a.Log.Event,
                                   Event_B = b.Log.Event,
                                   a.TransactionId,
                                   a.ProjectId,
                                   a.GroupId,
                                   a.QuantitySheetId,
                                   a.CatchNo,
                                   a.Quantity,
                                   LoggedAT_A = a.Log.LoggedAT,
                                   LoggedAT_B = b.Log.LoggedAT,
                                   TriggeredBy_A = a.Log.EventTriggeredBy,
                                   TriggeredBy_B = b.Log.EventTriggeredBy,
                                   TimeDifferenceMinutes = (int)Math.Abs((a.Log.LoggedAT - b.Log.LoggedAT).TotalMinutes)
                               }).ToList();

            var totalItems = matchedLogs.Count;
            var paginatedResult = matchedLogs
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Ok(new
            {
                StartDate = startDateTime.ToString("dd-MM-yyyy"),
                EndDate = endDateTime.AddDays(-1).ToString("dd-MM-yyyy"),
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
                Items = paginatedResult
            });
        }



        [HttpGet("DailyProcessCompletionSummary")]
        public async Task<IActionResult> GetDailyProcessCompletionSummary(
           [FromQuery] string? date,
           [FromQuery] string? startDate,
           [FromQuery] string? endDate)
        {
            var indianZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            string format = "dd-MM-yyyy";
            var culture = CultureInfo.InvariantCulture;

            var eventLogsQuery = _context.EventLogs
                .Where(e => e.TransactionId != null &&
                            e.Event == "Status updated" &&
                            e.NewValue == "2");

            if (!string.IsNullOrWhiteSpace(date))
            {
                if (DateTime.TryParseExact(date, format, culture, DateTimeStyles.None, out var parsedDate))
                {
                    var targetDate = TimeZoneInfo.ConvertTime(parsedDate.Date, indianZone);
                    var nextDay = targetDate.AddDays(1);
                    eventLogsQuery = eventLogsQuery.Where(e => e.LoggedAT >= targetDate && e.LoggedAT < nextDay);
                }
                else
                {
                    return BadRequest("Invalid date format for 'date'. Use dd-MM-yyyy.");
                }
            }
            else if (!string.IsNullOrWhiteSpace(startDate) && !string.IsNullOrWhiteSpace(endDate))
            {
                if (DateTime.TryParseExact(startDate, format, culture, DateTimeStyles.None, out var parsedStartDate) &&
                    DateTime.TryParseExact(endDate, format, culture, DateTimeStyles.None, out var parsedEndDate))
                {
                    var start = TimeZoneInfo.ConvertTime(parsedStartDate.Date, indianZone);
                    var end = TimeZoneInfo.ConvertTime(parsedEndDate.Date.AddDays(1), indianZone);
                    eventLogsQuery = eventLogsQuery.Where(e => e.LoggedAT >= start && e.LoggedAT < end);
                }
                else
                {
                    return BadRequest("Invalid date format for 'startDate' or 'endDate'. Use dd-MM-yyyy.");
                }
            }

            var joinedData = await (
                from e in eventLogsQuery
                join t in _context.Transaction on e.TransactionId equals t.TransactionId
                join q in _context.QuantitySheets on t.QuantitysheetId equals q.QuantitySheetId
                join p in _context.Projects on t.ProjectId equals p.ProjectId
                select new
                {
                    ProjectId = t.ProjectId,
                    GroupId = p.GroupId,
                    LotNo = t.LotNo,
                    ProcessId = t.ProcessId,
                    QuantitySheetId = q.QuantitySheetId,
                    Quantity = q.Quantity
                }
            ).ToListAsync();

            var summary = joinedData
                .GroupBy(x => new { x.ProjectId, x.GroupId, x.LotNo, x.ProcessId })
                .Select(g =>
                {
                    var distinctSheets = g
                        .GroupBy(x => x.QuantitySheetId)
                        .Select(x => x.First());

                    return new
                    {
                        ProjectId = g.Key.ProjectId,
                        GroupId = g.Key.GroupId,
                        LotNo = g.Key.LotNo,
                        ProcessId = g.Key.ProcessId,
                        TotalQuantity = distinctSheets.Sum(x => x.Quantity),
                        CatchCount = distinctSheets.Count()
                    };
                })
                .ToList();

            return Ok(summary);
        }





        [HttpGet("process-completion")]
        public async Task<IActionResult> GetProcessCompletion(
        [FromQuery] string? date,
        [FromQuery] string? startDate,
        [FromQuery] string? endDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 10;

            // Step 1: Date filtering in EventLog
            var indianZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            string format = "dd-MM-yyyy";
            var culture = CultureInfo.InvariantCulture;

            var eventLogsQuery = _context.EventLogs
                .Where(e => e.TransactionId != null &&
                            e.Event == "Status updated" &&
                            e.NewValue == "2");

            if (!string.IsNullOrWhiteSpace(date))
            {
                if (DateTime.TryParseExact(date, format, culture, DateTimeStyles.None, out var parsedDate))
                {
                    var targetDate = TimeZoneInfo.ConvertTime(parsedDate.Date, indianZone);
                    var nextDay = targetDate.AddDays(1);
                    eventLogsQuery = eventLogsQuery.Where(e => e.LoggedAT >= targetDate && e.LoggedAT < nextDay);
                }
                else
                {
                    return BadRequest("Invalid date format for 'date'. Use dd-MM-yyyy.");
                }
            }
            else if (!string.IsNullOrWhiteSpace(startDate) && !string.IsNullOrWhiteSpace(endDate))
            {
                if (DateTime.TryParseExact(startDate, format, culture, DateTimeStyles.None, out var parsedStartDate) &&
                    DateTime.TryParseExact(endDate, format, culture, DateTimeStyles.None, out var parsedEndDate))
                {
                    var start = TimeZoneInfo.ConvertTime(parsedStartDate.Date, indianZone);
                    var end = TimeZoneInfo.ConvertTime(parsedEndDate.Date.AddDays(1), indianZone);
                    eventLogsQuery = eventLogsQuery.Where(e => e.LoggedAT >= start && e.LoggedAT < end);
                }
                else
                {
                    return BadRequest("Invalid date format for 'startDate' or 'endDate'. Use dd-MM-yyyy.");
                }
            }

            // Step 2: Get filtered TransactionIds from EventLogs
            var filteredTransactionIds = await eventLogsQuery
                .Select(e => e.TransactionId.Value)
                .Distinct()
                .ToListAsync();

            // Step 3: Get all active projects
            var activeProjects = await _context.Projects
                .Where(p => p.Status)
                .Select(p => new { p.ProjectId, p.GroupId })
                .ToListAsync();

            var activeProjectIds = new HashSet<int>(activeProjects.Select(p => p.ProjectId));

            // Step 4: Transactions with QuantitySheets (filtered TransactionIds only)
            var transQuery = from t in _context.Transaction
                             where filteredTransactionIds.Contains(t.TransactionId) &&
                                   activeProjectIds.Contains(t.ProjectId)
                             join q in _context.QuantitySheets on t.QuantitysheetId equals q.QuantitySheetId
                             select new
                             {
                                 t.ProjectId,
                                 t.LotNo,
                                 t.ProcessId,
                                 q.QuantitySheetId,
                                 q.Quantity,
                                 q.CatchNo
                             };

            var transData = await transQuery.ToListAsync();

            // Step 5: Full QuantitySheets (for Total Quantity and CatchCount, unfiltered)
            var allQtyQuery = from q in _context.QuantitySheets
                              join t in _context.Transaction on q.QuantitySheetId equals t.QuantitysheetId
                              where activeProjectIds.Contains(t.ProjectId)
                              select new
                              {
                                  t.ProjectId,
                                  t.LotNo,
                                  q.QuantitySheetId,
                                  q.Quantity,
                                  q.CatchNo
                              };

            var allQtyData = await allQtyQuery.ToListAsync();

            // Step 6: Grouping
            var result = transData
                .GroupBy(x => new { x.ProjectId, x.LotNo, x.ProcessId })
                .Select(g =>
                {
                    var uniqueCompletedSheets = g.GroupBy(x => x.QuantitySheetId).Select(x => x.First()).ToList();

                    var totalGroup = allQtyData
                        .Where(a => a.ProjectId == g.Key.ProjectId && a.LotNo == g.Key.LotNo)
                        .GroupBy(a => a.QuantitySheetId)
                        .Select(x => x.First())
                        .ToList();

                    var completedCatchCount = uniqueCompletedSheets.Select(x => x.CatchNo).Distinct().Count();
                    var totalCatchCount = totalGroup.Select(x => x.CatchNo).Distinct().Count();

                    return new
                    {
                        ProjectId = g.Key.ProjectId,
                        LotNo = g.Key.LotNo,
                        ProcessId = g.Key.ProcessId,
                        CompletedTotalQuantity = uniqueCompletedSheets.Sum(x => x.Quantity),
                        CompeletedCatchCount = completedCatchCount,
                        TotalQuantity = totalGroup.Sum(x => x.Quantity),
                        CatchCount = totalCatchCount,
                        RemainingCatchCount = totalCatchCount - completedCatchCount,
                    };
                })
                .ToList();

            // Step 7: Pagination
            var totalRecords = result.Count;
            var pagedData = result
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var response = new
            {
                Page = page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                Data = pagedData
            };

            return Ok(response);
        }

        [HttpGet("pending-catches")]
        public async Task<IActionResult> GetPendingCatches(
         [FromQuery] int groupId,
         [FromQuery] int? projectId,
         [FromQuery] string? lotNo)
        {
            if (groupId <= 0)
            {
                return BadRequest("groupId is required and must be > 0.");
            }

            // Step 1: Get active projects for group
            var projectQuery = _context.Projects
                .Where(p => p.Status && p.GroupId == groupId);

            if (projectId.HasValue)
            {
                projectQuery = projectQuery.Where(p => p.ProjectId == projectId.Value);
            }

            var activeProjects = await projectQuery
                .Select(p => new { p.ProjectId })
                .ToListAsync();

            var activeProjectIds = new HashSet<int>(activeProjects.Select(p => p.ProjectId));

            if (!activeProjectIds.Any())
            {
                return Ok(new { Message = "No active projects found for this groupId/projectId." });
            }

            // Step 2: Get completed TransactionIds from EventLogs
            var completedTransactionIds = await _context.EventLogs
                .Where(e => e.TransactionId != null &&
                            e.Event == "Status updated" &&
                            e.NewValue == "2")
                .Select(e => e.TransactionId.Value)
                .Distinct()
                .ToListAsync();

            // Step 3: Transactions with QuantitySheets (ALL matching group/project/lot)
            var transQuery = from t in _context.Transaction
                             where activeProjectIds.Contains(t.ProjectId)
                             join q in _context.QuantitySheets on t.QuantitysheetId equals q.QuantitySheetId
                             select new
                             {
                                 t.TransactionId,
                                 t.ProjectId,
                                 t.LotNo,
                                 t.ProcessId,
                                 q.QuantitySheetId,
                                 q.Quantity,
                                 q.CatchNo
                             };

            // Apply lotNo filter safely if provided
            if (!string.IsNullOrWhiteSpace(lotNo))
            {
                if (int.TryParse(lotNo, out int lotNoInt))
                {
                    transQuery = transQuery.Where(t => t.LotNo == lotNoInt);
                }
                else
                {
                    return BadRequest("Invalid lotNo. Must be numeric.");
                }
            }

            var transData = await transQuery.ToListAsync();

            // Step 4: Grouping → calculate pending catches
            var result = transData
                .GroupBy(x => new { x.ProjectId, x.LotNo, x.ProcessId })
                .Select(g =>
                {
                    var totalGroup = g
                        .GroupBy(x => x.QuantitySheetId)
                        .Select(x => x.First())
                        .ToList();

                    var completedSheets = totalGroup
                        .Where(x => completedTransactionIds.Contains(x.TransactionId))
                        .ToList();

                    var pendingSheets = totalGroup
                        .Where(x => !completedTransactionIds.Contains(x.TransactionId))
                        .ToList();

                    var completedCatchCount = completedSheets.Select(x => x.CatchNo).Distinct().Count();
                    var totalCatchCount = totalGroup.Select(x => x.CatchNo).Distinct().Count();
                    var remainingCatchCount = totalCatchCount - completedCatchCount;

                    return new
                    {
                        ProjectId = g.Key.ProjectId,
                        LotNo = g.Key.LotNo,
                        ProcessId = g.Key.ProcessId,
                        TotalQuantity = totalGroup.Sum(x => x.Quantity),
                        CatchCount = totalCatchCount,
                        CompletedCatchCount = completedCatchCount,
                        PendingCatchCount = remainingCatchCount
                    };
                })
                .Where(r => r.PendingCatchCount > 0) // show only items with pending catches
                .ToList();

            // Calculate sum of all pending catches
            var totalPendingCatchSum = result.Sum(r => r.PendingCatchCount);

            var response = new
            {
                GroupId = groupId,
                ProjectId = projectId,
                LotNo = lotNo,
                TotalRecords = result.Count,
                TotalPendingCatchCount = totalPendingCatchSum, // added this line
                Data = result
            };

            return Ok(response);
        }




    }
}