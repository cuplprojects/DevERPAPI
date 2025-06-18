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

        [HttpGet("UnderProduction")]
        public async Task<IActionResult> GetUnderProduction()
        {
            // Step 1: Fetch all required data from the database
            var getProject = await _context.Projects
                .Select(p => new { p.ProjectId, p.Name, p.GroupId, p.TypeId })
                .ToListAsync();

            var getdistinctlotsofproject = await _context.QuantitySheets
                .Where(q => q.Status == 1)
                .Select(q => new { q.LotNo, q.ProjectId, q.ExamDate, q.QuantitySheetId,q.Quantity })
                .Distinct()
                .ToListAsync();
           
            

            var getdispatchedlots = await _context.Dispatch
                .Select(d => new { d.LotNo, d.ProjectId })
                .ToListAsync();
            var dispatchedLotKeys = new HashSet<string>(
      getdispatchedlots.Select(d => $"{d.ProjectId}|{d.LotNo}")
  );

            var quantitySheetGroups = getdistinctlotsofproject
                .GroupBy(q => new { q.LotNo, q.ProjectId })
                .ToDictionary(
                  g => $"{g.Key.ProjectId}|{g.Key.LotNo}",
                    g => new {
                        TotalCatchNo = g.Select(q => q.QuantitySheetId).Count(),
                        TotalQuantity = g.Sum(q => q.Quantity),
                        FromDate = g.Min(q => DateTime.TryParse(q.ExamDate, out var d) ? d : DateTime.MinValue),
                        ToDate = g.Max(q => DateTime.TryParse(q.ExamDate, out var d) ? d : DateTime.MinValue)
                    }
                );

          

            // Step 3: Perform joins and calculate result in-memory
            var underProduction = (from project in getProject
                                   from kvp in quantitySheetGroups
                                   let keyParts = kvp.Key.Split(new[] { '|' }, StringSplitOptions.None)
                                   let projectId = int.Parse(keyParts[0])
                                   let lotNo = keyParts[1]
                                   where project.ProjectId == projectId && !dispatchedLotKeys.Contains(kvp.Key)
                                   select new
                                   {
                                       project.ProjectId,
                                       project.Name,
                                       project.GroupId,
                                       FromDate = kvp.Value.FromDate,
                                       ToDate = kvp.Value.ToDate,
                                       project.TypeId,
                                       LotNo =lotNo,
                                       TotalCatchNo = kvp.Value.TotalCatchNo,
                                       TotalQuantity = kvp.Value.TotalQuantity
                                   }).ToList();

            return Ok(underProduction);
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




        //[HttpGet("DailyReports")]
        //public async Task<IActionResult> GetDailyReports(string date = null, string? startDate = null, string? endDate = null, int? userId = null, int? groupId = null, int page = 1, int pageSize = 10)
        //{
        //    try
        //    {
        //        DateTime? parsedDate = null;
        //        DateTime? parsedStartDate = null;
        //        DateTime? parsedEndDate = null;

        //        if (!string.IsNullOrEmpty(date))
        //        {
        //            if (!DateTime.TryParseExact(date, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
        //                return BadRequest("Invalid date format. Please use dd-MM-yyyy.");
        //            parsedDate = parsed;
        //        }

        //        if (!string.IsNullOrEmpty(startDate))
        //        {
        //            if (!DateTime.TryParseExact(startDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedStart))
        //                return BadRequest("Invalid startDate format. Please use dd-MM-yyyy.");
        //            parsedStartDate = parsedStart;
        //        }

        //        if (!string.IsNullOrEmpty(endDate))
        //        {
        //            if (!DateTime.TryParseExact(endDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedEnd))
        //                return BadRequest("Invalid endDate format. Please use dd-MM-yyyy.");
        //            parsedEndDate = parsedEnd;
        //        }

        //        var baseQuery = _context.EventLogs
        //            .Where(el => el.Category == "Transaction"
        //                && el.Event == "Status Updated"
        //                && el.OldValue == "1"
        //                && el.NewValue == "2");

        //        if (parsedStartDate.HasValue && parsedEndDate.HasValue)
        //        {
        //            baseQuery = baseQuery.Where(el => el.LoggedAT.Date >= parsedStartDate.Value.Date && el.LoggedAT.Date <= parsedEndDate.Value.Date);
        //        }
        //        else if (parsedDate.HasValue)
        //        {
        //            baseQuery = baseQuery.Where(el => el.LoggedAT.Date == parsedDate.Value.Date);
        //        }
        //        else
        //        {
        //            return BadRequest("Please provide either a valid date or both startDate and endDate.");
        //        }

        //        if (userId.HasValue)
        //        {
        //            baseQuery = baseQuery.Where(el => el.EventTriggeredBy == userId.Value);
        //        }

        //        var joinedData = baseQuery
        //            .Join(_context.Transaction,
        //                el => el.TransactionId,
        //                t => t.TransactionId,
        //                (el, t) => new { el, t })
        //            .Join(_context.QuantitySheets,
        //                joined => joined.t.QuantitysheetId,
        //                qs => qs.QuantitySheetId,
        //                (joined, qs) => new { joined.el, joined.t, qs })
        //            .Join(_context.Projects,
        //                joined => joined.t.ProjectId,
        //                p => p.ProjectId,
        //                (joined, p) => new { joined.el, joined.t, joined.qs, p })
        //            .Join(_context.Groups,
        //                joined => joined.p.GroupId,
        //                g => g.Id,
        //                (joined, g) => new
        //                {
        //                    joined.el,
        //                    joined.t,
        //                    joined.qs,
        //                    joined.p,
        //                    Group = g
        //                })
        //            .Where(r => !groupId.HasValue || r.p.GroupId == groupId.Value)
        //            .Select(r => new
        //            {
        //                r.el,
        //                r.t,
        //                r.qs,
        //                r.p,
        //                GroupName = r.Group.Name
        //            });

        //        var results = await joinedData.ToListAsync();


        //        var teamIds = results
        //            .Where(r => r.t.TeamId != null)
        //            .SelectMany(r => r.t.TeamId)
        //            .Distinct()
        //            .ToList();

        //        var teamUserMap = await _context.Teams
        //            .Where(t => teamIds.Contains(t.TeamId))
        //            .ToDictionaryAsync(t => t.TeamId, t => t.UserIds);

        //        var allUserIds = teamUserMap.Values.SelectMany(u => u).Distinct().ToList();

        //        var userMap = await _context.Users
        //            .Where(u => allUserIds.Contains(u.UserId))
        //            .ToDictionaryAsync(u => u.UserId, u => u.UserName);

        //        var eventTriggeredByIds = results.Select(r => r.el.EventTriggeredBy).Distinct().ToList();

        //        var triggeredByMap = await _context.Users
        //            .Where(u => eventTriggeredByIds.Contains(u.UserId))
        //            .ToDictionaryAsync(u => u.UserId, u => u.UserName);

        //        var pagedResults = results
        //            .Skip((page - 1) * pageSize)
        //            .Take(pageSize)
        //            .ToList();

        //        var transactionSummaries = pagedResults
        //            .Select(r =>
        //            {
        //                var userIds = r.t.TeamId != null
        //                    ? r.t.TeamId
        //                        .Where(tid => teamUserMap.ContainsKey(tid))
        //                        .SelectMany(tid => teamUserMap[tid])
        //                        .Distinct()
        //                        .ToList()
        //                    : new List<int>();

        //                var userNames = userIds
        //                    .Where(uid => userMap.ContainsKey(uid))
        //                    .Select(uid => userMap[uid])
        //                    .ToList();

        //                var EndTime = _context.EventLogs
        //                    .Where(el => el.TransactionId == r.t.TransactionId)
        //                    .OrderByDescending(el => el.LoggedAT)
        //                    .Select(el => el.LoggedAT)
        //                    .FirstOrDefault();

        //                var startTime = _context.EventLogs
        //                    .Where(el => el.TransactionId == r.t.TransactionId)
        //                    .OrderBy(el => el.LoggedAT)
        //                    .Select(el => el.LoggedAT)
        //                    .FirstOrDefault();

        //                string triggeredByUserName = triggeredByMap.ContainsKey(r.el.EventTriggeredBy)
        //                    ? triggeredByMap[r.el.EventTriggeredBy]
        //                    : "Unknown";

        //                return new
        //                {
        //                    TransactionId = r.t.TransactionId,
        //                    ProjectName = r.p.Name,
        //                    QuantitySheetId = r.t.QuantitysheetId,
        //                    ZoneId = r.t.ZoneId,
        //                    CatchNo = r.qs.CatchNo,
        //                    GroupName = r.GroupName,
        //                    Supervisor = triggeredByUserName,
        //                    StartTime = startTime,
        //                    EndTime = EndTime,
        //                    ProcessId = r.t.ProcessId,
        //                    Quantity = r.qs.Quantity,
        //                    MachineId = r.t.MachineId,
        //                    Lot = r.t.LotNo,
        //                    StatusCode = r.t.Status,
        //                    TeamMembersNames = userNames
        //                };
        //            })
        //            .ToList();

        //        var machineIds = results.Select(r => r.t.MachineId).Distinct().ToList();
        //        var machines = await _context.Machine
        //            .Where(m => machineIds.Contains(m.MachineId))
        //            .Select(m => new { m.MachineId, m.MachineName })
        //            .ToListAsync();

        //        var supervisorIds = results.Select(r => r.el.EventTriggeredBy).Distinct().ToList();
        //        var supervisors = await _context.Users
        //            .Where(u => supervisorIds.Contains(u.UserId))
        //            .Select(u => new { Name = u.FirstName + " " + u.LastName })
        //            .ToListAsync();




        //        var logTimes = results.Select(r => r.el.LoggedAT).ToList();
        //        var firstLog = logTimes.Min();
        //        var lastLog = logTimes.Max();
        //        var timeDiff = lastLog - firstLog;
        //        string formattedDiff = $"{timeDiff.Days}d:{timeDiff.Hours}h:{timeDiff.Minutes}m:{timeDiff.Seconds}s";





        //        return Ok(new
        //        {
        //            UserTransactionDetails = transactionSummaries,

        //            CurrentPage = page,
        //            PageSize = pageSize
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { message = "An error occurred", error = ex.Message });
        //    }
        //}



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

                if (parsedStartDate.HasValue && parsedEndDate.HasValue)
                {
                    logQuery = logQuery.Where(el => el.LoggedAT.Date >= parsedStartDate.Value && el.LoggedAT.Date <= parsedEndDate.Value);
                }
                else if (parsedDate.HasValue)
                {
                    logQuery = logQuery.Where(el => el.LoggedAT.Date == parsedDate.Value);
                }

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
                    .Select(g => new
                    {
                        GroupName = groups.ContainsKey(g.Key.GroupId) ? groups[g.Key.GroupId] : "Unknown",
                        ProjectId = g.Key.ProjectId,
                        TypeId = g.Key.TypeId,
                        LotNo = g.Key.LotNo,
                        CountOfCatches = g.Count(),
                        TotalQuantity = g.Sum(x => x.qs.Quantity)
                    })
                    .ToList();

                return Ok(report);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred", error = ex.Message });
            }
        }






        [HttpGet("DailySummary")]
        public async Task<IActionResult> GetDailySummary(string date = null, string? startDate = null, string? endDate = null, int? userId = null, int? groupId = null)
        {
            try
            {
                DateTime? parsedDate = null;
                DateTime? parsedStartDate = null;
                DateTime? parsedEndDate = null;

                if (!string.IsNullOrEmpty(date))
                {
                    if (!DateTime.TryParseExact(date, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
                        return BadRequest("Invalid date format. Please use dd-MM-yyyy.");
                    parsedDate = parsed;
                }

                if (!string.IsNullOrEmpty(startDate))
                {
                    if (!DateTime.TryParseExact(startDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedStart))
                        return BadRequest("Invalid startDate format. Please use dd-MM-yyyy.");
                    parsedStartDate = parsedStart;
                }

                if (!string.IsNullOrEmpty(endDate))
                {
                    if (!DateTime.TryParseExact(endDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedEnd))
                        return BadRequest("Invalid endDate format. Please use dd-MM-yyyy.");
                    parsedEndDate = parsedEnd;
                }

                var baseQuery = _context.EventLogs
                    .Where(el => el.Category == "Transaction"
                        && el.Event == "Status Updated"
                        && el.OldValue == "1"
                        && el.NewValue == "2");

                if (parsedStartDate.HasValue && parsedEndDate.HasValue)
                {
                    baseQuery = baseQuery.Where(el => el.LoggedAT.Date >= parsedStartDate.Value.Date && el.LoggedAT.Date <= parsedEndDate.Value.Date);
                }
                else if (parsedDate.HasValue)
                {
                    baseQuery = baseQuery.Where(el => el.LoggedAT.Date == parsedDate.Value.Date);
                }
                else
                {
                    return BadRequest("Please provide either a valid date or both startDate and endDate.");
                }

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
                            Group = g
                        })
                    .Where(r => !groupId.HasValue || r.p.GroupId == groupId.Value);

                var results = await joinedData.ToListAsync();

                var quantitySheetCount = results.Select(r => r.qs.QuantitySheetId).Distinct().Count();
                var totalQuantity = results.Sum(r => r.qs.Quantity);
                var lotNos = results.Select(r => r.t.LotNo).Distinct().ToList();

                var quantityByProcess = results
                    .Where(r => r.t.Status == 2)
                    .GroupBy(r => r.t.ProcessId)
                    .Select(g => new
                    {
                        processId = g.Key,
                        totalQuantity = g.Sum(x => x.qs.Quantity)
                    })
                    .ToList();

                var supervisorIds = results.Select(r => r.el.EventTriggeredBy).Distinct().ToList();
                var supervisors = await _context.Users
                    .Where(u => supervisorIds.Contains(u.UserId))
                    .Select(u => new { name = u.FirstName + " " + u.LastName })
                    .ToListAsync();

                var groupWise = results
                    .Select(r => new { r.Group.Id, r.Group.Name })
                    .Distinct()
                    .Select(g => new
                    {
                        groupId = g.Id,
                        groupName = g.Name
                    })
                    .ToList();

                var numberOfGroups = groupWise.Count;

                var projectWise = results
                    .Select(r => new { r.p.ProjectId, r.p.Name })
                    .Distinct()
                    .Select(p => new
                    {
                        projectId = p.ProjectId,
                        projectName = p.Name
                    })
                    .ToList();

                var numberOfProjects = projectWise.Count;

                return Ok(new
                {
                    supervisors,
                    totalCatches = quantitySheetCount,
                    totalQuantity,
                    distinctLotNos = lotNos,
                    completedQuantityByProcess = quantityByProcess,
                    groupWiseCounts = groupWise,
                    numberOfGroups,
                    projectWiseCounts = projectWise,
                    numberOfProjects
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

        [HttpGet("MachineWiseReport")]
        public async Task<IActionResult> GetMachineWiseReport(
        int machineId,
        string date = null,
        string startDate = null,
        string endDate = null)
        {
            try
            {
                if (machineId <= 0)
                    return BadRequest("MachineId is required and must be greater than 0.");

                DateTime? parsedDate = null;
                DateTime? parsedStartDate = null;
                DateTime? parsedEndDate = null;

                if (!string.IsNullOrEmpty(date))
                {
                    if (!DateTime.TryParseExact(date, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
                        return BadRequest("Invalid date format. Please use dd-MM-yyyy.");
                    parsedDate = parsed;
                }
                else if (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate))
                {
                    if (!DateTime.TryParseExact(startDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedStart))
                        return BadRequest("Invalid startDate format. Please use dd-MM-yyyy.");
                    if (!DateTime.TryParseExact(endDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedEnd))
                        return BadRequest("Invalid endDate format. Please use dd-MM-yyyy.");
                    parsedStartDate = parsedStart;
                    parsedEndDate = parsedEnd;
                }
                else
                {
                    return BadRequest("Provide either a valid date or both startDate and endDate.");
                }

                var baseQuery = _context.EventLogs
                    .Where(el => el.Category == "Transaction"
                        && el.Event == "Status Updated"
                        && el.OldValue == "1"
                        && el.NewValue == "2");

                if (parsedStartDate.HasValue && parsedEndDate.HasValue)
                {
                    baseQuery = baseQuery.Where(el => el.LoggedAT.Date >= parsedStartDate.Value.Date && el.LoggedAT.Date <= parsedEndDate.Value.Date);
                }
                else if (parsedDate.HasValue)
                {
                    baseQuery = baseQuery.Where(el => el.LoggedAT.Date == parsedDate.Value.Date);
                }

                var joinedData = baseQuery
                    .Join(_context.Transaction,
                        el => el.TransactionId,
                        t => t.TransactionId,
                        (el, t) => new { el, t })
                    .Where(joined => joined.t.MachineId == machineId)
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

                var teamIds = results
                    .Where(r => r.t.TeamId != null)
                    .SelectMany(r => r.t.TeamId)
                    .Distinct()
                    .ToList();

                var teamUserMap = await _context.Teams
                    .Where(t => teamIds.Contains(t.TeamId))
                    .ToDictionaryAsync(t => t.TeamId, t => t.UserIds);

                var allUserIds = teamUserMap.Values.SelectMany(u => u).Distinct().ToList();

                var userMap = await _context.Users
                    .Where(u => allUserIds.Contains(u.UserId))
                    .ToDictionaryAsync(u => u.UserId, u => u.UserName);

                var triggeredByIds = results.Select(r => r.el.EventTriggeredBy).Distinct().ToList();

                var triggeredByMap = await _context.Users
                    .Where(u => triggeredByIds.Contains(u.UserId))
                    .ToDictionaryAsync(u => u.UserId, u => u.UserName);

                var transactionSummaries = results.Select(r =>
                {
                    var userIds = r.t.TeamId != null
                        ? r.t.TeamId
                            .Where(tid => teamUserMap.ContainsKey(tid))
                            .SelectMany(tid => teamUserMap[tid])
                            .Distinct()
                            .ToList()
                        : new List<int>();

                    var userNames = userIds
                        .Where(uid => userMap.ContainsKey(uid))
                        .Select(uid => userMap[uid])
                        .ToList();

                    var endTime = _context.EventLogs
                        .Where(el => el.TransactionId == r.t.TransactionId)
                        .OrderByDescending(el => el.LoggedAT)
                        .Select(el => el.LoggedAT)
                        .FirstOrDefault();

                    var startTime = _context.EventLogs
                        .Where(el => el.TransactionId == r.t.TransactionId)
                        .OrderBy(el => el.LoggedAT)
                        .Select(el => el.LoggedAT)
                        .FirstOrDefault();

                    string triggeredByUserName = triggeredByMap.ContainsKey(r.el.EventTriggeredBy)
                        ? triggeredByMap[r.el.EventTriggeredBy]
                        : "Unknown";

                    return new
                    {
                        TransactionId = r.t.TransactionId,
                        ProjectName = r.p.Name,
                        QuantitySheetId = r.t.QuantitysheetId,
                        ZoneId = r.t.ZoneId,
                        CatchNo = r.qs.CatchNo,
                        GroupName = r.GroupName,
                        Supervisor = triggeredByUserName,
                        StartTime = startTime,
                        EndTime = endTime,
                        ProcessId = r.t.ProcessId,
                        Quantity = r.qs.Quantity,
                        MachineId = r.t.MachineId,
                        Lot = r.t.LotNo,
                        StatusCode = r.t.Status,
                        TeamMembersNames = userNames
                    };
                }).ToList();

                return Ok(new
                {
                    MachineId = machineId,
                    ReportDate = parsedDate?.ToString("dd-MM-yyyy"),
                    StartDate = parsedStartDate?.ToString("dd-MM-yyyy"),
                    EndDate = parsedEndDate?.ToString("dd-MM-yyyy"),
                    TotalRecords = transactionSummaries.Count,
                    MachineWiseDetails = transactionSummaries
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred", error = ex.Message });
            }
        }

        [HttpGet("DailyProductionReleaseReport")]
        public async Task<IActionResult> GetDailyProductionReleaseReport(string? date = null, string? startDate = null, string? endDate = null)
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

                // 🚫 DO NOT FILTER HERE
                var quantitySheetsRaw = await _context.QuantitySheets
                    .Where(q => q.Status == 1)
                    .ToListAsync();

                // ✅ Filter in memory using TryParse
                var quantitySheets = quantitySheetsRaw
                    .Where(q =>
                    {
                        if (DateTime.TryParse(q.ExamDate, out var examDt))
                        {
                            if (parsedDate.HasValue)
                                return examDt.Date == parsedDate.Value;
                            if (parsedStartDate.HasValue && parsedEndDate.HasValue)
                                return examDt.Date >= parsedStartDate.Value && examDt.Date <= parsedEndDate.Value;
                            return true;
                        }
                        return false;
                    })
                    .ToList();

                var quantitySheetIds = quantitySheets.Select(q => q.QuantitySheetId).ToList();

                var transactions = await _context.Transaction
                    .Where(t => quantitySheetIds.Contains(t.QuantitysheetId))
                    .ToListAsync();

                var projectIds = transactions.Select(t => t.ProjectId).Distinct().ToList();
                var projects = await _context.Projects
                    .Where(p => projectIds.Contains(p.ProjectId))
                    .ToListAsync();

                var groupIds = projects.Select(p => p.GroupId).Distinct().ToList();
                var groups = await _context.Groups
                    .Where(g => groupIds.Contains(g.Id))
                    .ToDictionaryAsync(g => g.Id, g => g.Name);

                var joinedData = transactions
                    .Join(projects, t => t.ProjectId, p => p.ProjectId, (t, p) => new { t, p })
                    .Join(quantitySheets, tp => tp.t.QuantitysheetId, qs => qs.QuantitySheetId, (tp, qs) => new { tp.t, tp.p, qs })
                    .ToList();

                var report = joinedData
                    .GroupBy(x => new { x.t.ProjectId, x.p.TypeId, x.p.GroupId, x.t.LotNo })
                    .Select(g =>
                    {
                        var examDates = g
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
                            CountOfCatches = g.Count(),
                            TotalQuantity = g.Sum(x => x.qs.Quantity)
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

       


    }
}