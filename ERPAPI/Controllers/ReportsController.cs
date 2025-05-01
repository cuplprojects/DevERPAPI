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





        //[HttpGet("GetCatchNoByProject/{projectId}")]
        //public async Task<IActionResult> GetCatchNoByProject(int projectId)
        //{
        //    try
        //    {
        //        // Fetch all CatchNo where ProjectId matches and Status is 1
        //        var quantitySheets = await _context.QuantitySheets
        //            .Where(q => q.ProjectId == projectId && q.Status == 1)
        //            .Select(q => q.CatchNo)
        //            .ToListAsync();

        //        if (quantitySheets == null || quantitySheets.Count == 0)
        //        {
        //            return NotFound(new { Message = "No records found with Status = 1 for the given ProjectId." });
        //        }

        //        // Fetch event logs where category is 'Production' and projectId is present in OldValue or NewValue
        //        var eventLogs = await _context.EventLogs
        //            .Where(e => e.Category == "Production" && (e.OldValue.Contains(projectId.ToString()) || e.NewValue.Contains(projectId.ToString())))
        //            .Select(e => new { e.NewValue, e.LoggedAT })
        //            .ToListAsync();

        //        if (eventLogs == null || eventLogs.Count == 0)
        //        {
        //            return NotFound(new { Message = "No event logs found for the given ProjectId." });
        //        }

        //        return Ok(new { CatchNumbers = quantitySheets, Events = eventLogs });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { Message = "An error occurred.", Error = ex.Message });
        //    }
        //}

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

            /* var totalRecords = await queryable
                 .CountAsync(q => q.CatchNo.StartsWith(query) ||
                                 q.Subject.StartsWith(query) ||
                                 q.Course.StartsWith(query) ||
                                 (q.PaperTitle != null && q.PaperTitle.StartsWith(query)));

             var results = await queryable
                 .Where(q => q.CatchNo.StartsWith(query) ||
                             q.Subject.StartsWith(query) ||
                             q.Course.StartsWith(query) ||
                             (q.PaperTitle != null && q.PaperTitle.StartsWith(query)))
                 .Select(q => new
                 {
                     q.CatchNo,
                     MatchedColumn = q.CatchNo.StartsWith(query) ? "CatchNo" :
                                     q.Subject.StartsWith(query) ? "Subject" :
                                     q.Course.StartsWith(query) ? "Course" : "Paper",
                     MatchedValue = q.CatchNo.StartsWith(query) ? q.CatchNo :
                                    q.Subject.StartsWith(query) ? q.Subject :
                                    q.Course.StartsWith(query) ? q.Course : q.Paper,
                     q.ProjectId,
                     q.LotNo
                 })
                 .Skip((page - 1) * pageSize) // Skip records based on the page number
                 .Take(pageSize) // Limit the number of results per page
                 .ToListAsync();*/

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

        [HttpGet("DailyReports")]
        public async Task<IActionResult> GetDailyReports(string date, int? userId = null)
        {
            try
            {
                if (!DateTime.TryParseExact(date, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
                {
                    return BadRequest("Invalid date format. Please use dd-MM-yyyy.");
                }

                var baseQuery = _context.EventLogs
                    .Where(el => el.Category == "Transaction"
                        && el.LoggedAT.Date == parsedDate.Date
                        && el.Event == "Status Updated"
                        && el.OldValue == "1"
                        && el.NewValue == "2");

                if (userId.HasValue)
                {
                    baseQuery = baseQuery.Where(el => el.EventTriggeredBy == userId.Value);
                }

                var joinedData = baseQuery
                    .Join(_context.Transaction,
                        el => el.TransactionId,
                        t => t.TransactionId,
                        (el, t) => new { el, t })
                    .Join(_context.QuantitySheets,
                        joined => joined.t.QuantitysheetId,
                        qs => qs.QuantitySheetId,
                        (joined, qs) => new { joined.el, joined.t, qs })
                    .Join(_context.Projects,
                        joined => joined.t.ProjectId,
                        p => p.ProjectId,
                        (joined, p) => new { joined.el, joined.t, joined.qs, p })
                    .Join(_context.Groups,
                        joined => joined.p.GroupId,
                        g => g.Id,
                        (joined, g) => new
                        {
                            joined.el,
                            joined.t,
                            joined.qs,
                            joined.p,
                            GroupName = g.Name
                        });

                var results = await joinedData.ToListAsync();

                var transactionSummaries = results.Select(r => new
                {
                    TransactionId = r.t.TransactionId,
                    ProjectName = r.p.Name,
                    QuantitySheetId = r.t.QuantitysheetId,
                    ZoneId = r.t.ZoneId,
                    CatchNo = r.qs.CatchNo,
                    GroupName = r.GroupName,
                    UserId = r.el.EventTriggeredBy,
                    ProcessId = r.t.ProcessId,
                }).ToList();

                var teamIds = results.SelectMany(r => r.t.TeamId ?? new List<int>()).Distinct().ToList();
                var teamMembers = await _context.Users
                    .Where(u => teamIds.Contains(u.UserId))
                    .Select(u => new { Name = u.FirstName + " " + u.LastName })
                    .ToListAsync();

                var machineIds = results.Select(r => r.t.MachineId).Distinct().ToList();
                var machines = await _context.Machine
                    .Where(m => machineIds.Contains(m.MachineId))
                    .Select(m => new { m.MachineId, m.MachineName })
                    .ToListAsync();

                var supervisorIds = results.Select(r => r.el.EventTriggeredBy).Distinct().ToList();
                var supervisors = await _context.Users
                    .Where(u => supervisorIds.Contains(u.UserId))
                    .Select(u => new { Name = u.FirstName + " " + u.LastName })
                    .ToListAsync();

                var quantitySheetCount = results.Select(r => r.qs.QuantitySheetId).Distinct().Count();
                var totalQuantity = results.Sum(r => r.qs.Quantity);

                var logTimes = results.Select(r => r.el.LoggedAT).ToList();
                var firstLog = logTimes.Min();
                var lastLog = logTimes.Max();
                var timeDiff = lastLog - firstLog;
                string formattedDiff = $"{timeDiff.Days}d:{timeDiff.Hours}h:{timeDiff.Minutes}m:{timeDiff.Seconds}s";

                var lotNos = results.Select(r => r.t.LotNo).Distinct().ToList();

                return Ok(new
                {
                    UserTransactionDetails = transactionSummaries,
                    TeamMembers = teamMembers,
                    Machines = machines,
                    Supervisors = supervisors,
                    TotalCatches = quantitySheetCount,
                    FirstLoggedAt = firstLog,
                    LastLoggedAt = lastLog,
                    TimeDifference = formattedDiff,
                    TotalQuantity = totalQuantity,
                    DistinctLotNos = lotNos
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


    }
}