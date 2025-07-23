using ERPAPI.Data;
using ERPAPI.Model;
using ERPAPI.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Text.Json;


namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QPMastersController : ControllerBase
    {

        private readonly AppDbContext _context;
        private readonly ILoggerService _loggerService;

        public QPMastersController(AppDbContext context, ILoggerService loggerService)
        {
            _context = context;
            _loggerService = loggerService;
        }

        // GET: api/qpmasters
        [HttpGet]
        public async Task<ActionResult<IEnumerable<QpMaster>>> GetQpMaster()
        {
            try
            {
                var qpmasters = await _context.QpMasters.ToListAsync();
                return qpmasters;
            }
            catch (Exception)
            {

                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/qpmasters/5
        [HttpGet("{id}")]
        public async Task<ActionResult<QpMaster>> Getqpmaster(int id)
        {
            try
            {
                var qpmaster = await _context.QpMasters.FindAsync(id);

                if (qpmaster == null)
                {

                    return NotFound();
                }

                return qpmaster;
            }
            catch (Exception)
            {

                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("Columns")]

        public IActionResult GetColumnNames()
        {
            var columnNames = typeof(QpMaster).GetProperties()
                .Where(prop => prop.Name != "QPMasterId" &&
                    prop.Name != "GroupId" &&
                    prop.Name != "CustomizedField1" &&
                    prop.Name != "CustomizedField2" &&
                    prop.Name != "CustomizedField3")
                .Select(prop => prop.Name)
                .ToList();

            return Ok(columnNames);
        }

        // PUT: api/qpmasters/5
        [HttpPut("{id}")]
        public async Task<IActionResult> Putqpmaster(int id, QpMaster qpmaster)
        {
            if (id != qpmaster.QPMasterId)
            {
                return BadRequest();
            }

            // Fetch existing entity to capture old values
            var existingqpmaster = await _context.QpMasters.AsNoTracking().FirstOrDefaultAsync(c => c.QPMasterId == id);
            if (existingqpmaster == null)
            {
                _loggerService.LogEvent($"qpmaster with ID {id} not found during update", "qpmasters", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                return NotFound();
            }

            // Capture old and new values for logging
            string oldValue = Newtonsoft.Json.JsonConvert.SerializeObject(existingqpmaster);
            string newValue = Newtonsoft.Json.JsonConvert.SerializeObject(qpmaster);

            _context.Entry(qpmaster).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                _loggerService.LogEvent($"Updated qpmaster with ID {id}", "qpmasters", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0, oldValue, newValue);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (!qpmasterExists(id))
                {
                    _loggerService.LogEvent($"qpmaster with ID {id} not found during update", "qpmasters", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                    return NotFound();
                }
                else
                {
                    _loggerService.LogError("Concurrency error during qpmaster update", ex.Message, nameof(qpmaster));
                    throw;
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error updating qpmaster", ex.Message, nameof(QPMastersController));
                return StatusCode(500, "Internal server error");
            }

            return NoContent();
        }

        // POST: api/qpmasters
        [HttpPost]
        public async Task<ActionResult> Postqpmaster(List<QpMaster> qpmasterList)
        {
            if (qpmasterList == null || !qpmasterList.Any())
            {
                return BadRequest("No QuantitySheet data provided.");
            }

            // Normalize and remove internal duplicates
            var distinctQpList = qpmasterList
                .GroupBy(x => new { x.GroupId, NepCode = x.NEPCode.Trim().ToLower() })
                .Select(g => g.First())
                .ToList();

            var addedQpList = new List<QpMaster>();

            foreach (var qpmaster in distinctQpList)
            {
                var groupId = qpmaster.GroupId;
                var nepcode = qpmaster.NEPCode?.Trim();
                var normalizedNepcode = nepcode?.ToLower();

                var group = await _context.Groups
                    .Where(p => p.Id == groupId)
                    .FirstOrDefaultAsync();

                if (group == null)
                {
                    return BadRequest(new { message = $"Group with ID {groupId} not found." });
                }

                bool exists = await _context.QpMasters
                    .AnyAsync(q => q.GroupId == groupId
                        && q.NEPCode.Trim().ToLower() == normalizedNepcode);

                // If only one entry in request and it already exists -> send error
                if (qpmasterList.Count == 1 && exists)
                {
                    return BadRequest(new { message = $"NEPCode '{nepcode}' already exists for Group ID {groupId}." });
                }

                // If multiple entries, just skip the one that exists
                if (!exists)
                {
                    await _context.QpMasters.AddAsync(qpmaster);
                    addedQpList.Add(qpmaster);
                }
            }

            if (!addedQpList.Any())
            {
                return Ok(new { message = "No new entries were added (all were duplicates)." });
            }

            await _context.SaveChangesAsync();

            // Log added entries
            foreach (var qpmaster in addedQpList)
            {
                _loggerService.LogEvent(
                    "New QuantitySheet added",
                    "QuantitySheet",
                    1,
                    null,
                    $"GroupId: {qpmaster.GroupId}"
                );
            }

            return Ok(new { message = $"{addedQpList.Count} QuantitySheet(s) added successfully." });
        }




        // DELETE: api/qpmasters/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Deleteqpmaster(int id)
        {
            try
            {
                var qpmaster = await _context.QpMasters.FindAsync(id);
                if (qpmaster == null)
                {
                    _loggerService.LogEvent($"qpmaster with ID {id} not found during delete", "qpmasters", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                    return NotFound();
                }

                _context.QpMasters.Remove(qpmaster);
                await _context.SaveChangesAsync();
                _loggerService.LogEvent($"Deleted qpmaster with ID {id}", "qpmasters", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);

                return NoContent();
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error deleting qpmaster", ex.Message, nameof(QPMastersController));
                return StatusCode(500, "Internal server error");
            }
        }

        private bool qpmasterExists(int id)
        {
            return _context.QpMasters.Any(e => e.QPMasterId == id);
        }

        [HttpGet("Filter")]
        public async Task<IActionResult> GetQpMasters(
            [FromQuery] int? groupId = null,
            [FromQuery] int? typeId = null,
            [FromQuery] int? courseId = null,
            [FromQuery] List<int> examTypeId = null) // Accepting array input
        {
            var query = from qp in _context.QpMasters
                        join grp in _context.Groups on qp.GroupId equals grp.Id into grpJoin
                        from grp in grpJoin.DefaultIfEmpty()

                        join crs in _context.Courses on qp.CourseId equals crs.CourseId into crsJoin
                        from crs in crsJoin.DefaultIfEmpty()

                        join et in _context.ExamTypes on qp.ExamTypeId equals et.ExamTypeId into etJoin
                        from et in etJoin.DefaultIfEmpty()

                        join sub in _context.Subjects on qp.SubjectId equals sub.SubjectId into subJoin
                        from sub in subJoin.DefaultIfEmpty()
                        join type in _context.Types on qp.TypeId equals type.TypeId into typeJoin
                        from type in typeJoin.DefaultIfEmpty()
                        select new
                        {
                            qp.QPMasterId,
                            GroupName = grp != null ? grp.Name : null,

                            qp.NEPCode,
                            qp.UniqueCode,
                            SubjectName = sub != null ? sub.SubjectName : null,
                            qp.PaperNumber,
                            qp.PaperTitle,
                            qp.MaxMarks,
                            qp.Duration,
                            qp.StructureOfPaper,
                            qp.CustomizedField2,
                            qp.CustomizedField3,
                            CourseName = crs != null ? crs.CourseName : null,
                            ExamTypeName = et != null ? et.TypeName : null,
                            TypeName = typeId.HasValue ? type.Types : null,
                        };

            if (groupId.HasValue)
                query = query.Where(q => q.GroupName != null &&
                    _context.Groups.Any(g => g.Id == groupId && g.Name == q.GroupName));

            if (typeId.HasValue)

                query = query.Where(q => _context.Types.Any(t => t.TypeId == typeId && t.Types == q.TypeName));


            if (courseId.HasValue)
                query = query.Where(q => _context.Courses.Any(c => c.CourseId == courseId && c.CourseName == q.CourseName));

            if (examTypeId != null && examTypeId.Any())
                query = query.Where(q => _context.ExamTypes.Any(e => examTypeId.Contains(e.ExamTypeId) && e.TypeName == q.ExamTypeName));

            var result = await query
                .AsNoTracking()
                .ToListAsync();

            return Ok(result);
        }



        [HttpGet("SearchInQpMaster")]
        public async Task<IActionResult> SearchInQpMaster(
           [FromQuery] string search,
           [FromQuery] int? groupId,
           [FromQuery] string? examTypeId,
           [FromQuery] int page = 1,
           [FromQuery] int pageSize = 5)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return BadRequest("Search query cannot be null or empty.");
            }

            var parsedExamTypeIds = !string.IsNullOrWhiteSpace(examTypeId)
                ? examTypeId.Split(',')
                    .Select(id => int.TryParse(id, out var parsed) ? parsed : (int?)null)
                    .Where(id => id.HasValue)
                    .Select(id => id.Value)
                    .ToList()
                : new List<int>();

            var existingQPIds = await _context.QuantitySheets
                .Select(qs => qs.QPId)
                .ToListAsync();

            var baseQuery = from qp in _context.QpMasters
                            join crs in _context.Courses on qp.CourseId equals crs.CourseId into crsJoin
                            from crs in crsJoin.DefaultIfEmpty()
                            join et in _context.ExamTypes on qp.ExamTypeId equals et.ExamTypeId into etJoin
                            from et in etJoin.DefaultIfEmpty()
                            join sn in _context.Subjects on qp.SubjectId equals sn.SubjectId into snJoin
                            from sn in snJoin.DefaultIfEmpty()
                            where (qp.NEPCode.Contains(search) ||
                                   qp.UniqueCode.Contains(search) ||
                                   qp.PaperNumber.Contains(search) ||
                                   crs.CourseName.Contains(search) ||
                                   qp.PaperTitle.Contains(search)) &&
                                  (!parsedExamTypeIds.Any() || (qp.ExamTypeId.HasValue && parsedExamTypeIds.Contains(qp.ExamTypeId.Value))) &&
                                  (!groupId.HasValue || qp.GroupId == groupId) &&
                                  !existingQPIds.Contains(qp.QPMasterId)
                            select new
                            {
                                qp.QPMasterId,
                                qp.NEPCode,
                                qp.UniqueCode,
                                qp.PaperTitle,
                                qp.CourseId,
                                CourseName = crs.CourseName,
                                qp.PaperNumber,
                                qp.Duration,
                                qp.LanguageId,
                                qp.ExamTypeId,
                                qp.SubjectId,
                                ExamTypeName = et.TypeName,
                                SubjectName = sn.SubjectName,
                                qp.MaxMarks,
                            };

            // Count before pagination
            var totalCount = await baseQuery.CountAsync();

            // Apply pagination
            var paginatedQuery = await baseQuery
                .AsNoTracking()
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var languageDict = await _context.Languages
                .ToDictionaryAsync(l => l.LanguageId, l => l.Languages);

            var finalResult = paginatedQuery.Select(qp => new
            {
                qp.QPMasterId,
                qp.NEPCode,
                qp.UniqueCode,
                qp.PaperTitle,
                qp.CourseId,
                qp.CourseName,
                qp.SubjectId,
                qp.PaperNumber,
                qp.Duration,
                LanguageIds = qp.LanguageId,
                LanguageNames = qp.LanguageId != null
                    ? qp.LanguageId.Select(id => languageDict.ContainsKey(id) ? languageDict[id] : null).Where(name => name != null).ToList()
                    : new List<string>(),
                qp.ExamTypeId,
                qp.ExamTypeName,
                qp.SubjectName,
                qp.MaxMarks,
            }).ToList();

            return Ok(new
            {
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                Data = finalResult
            });
        }




        [HttpGet("GetExamTypeNamesByProjectId/{projectId}")]
        public async Task<IActionResult> GetExamTypeNamesByProjectId(int projectId)
        {
            try
            {
                // Get the project by projectId
                var project = await _context.Projects
                    .Where(p => p.ProjectId == projectId)
                    .Select(p => new { p.ExamTypeId })
                    .FirstOrDefaultAsync();

                if (project == null)
                {
                    _loggerService.LogError("Project not found.", null, $"ProjectId: {projectId}");
                    return NotFound("Project not found.");
                }

                // Get the exam type names for the examTypeIds
                var examTypeNames = await _context.ExamTypes
                    .Where(et => project.ExamTypeId.Contains(et.ExamTypeId))
                    .Select(et => et.TypeName)
                    .ToListAsync();

                return Ok(examTypeNames);
            }
            catch (Exception ex)
            {
                _loggerService.LogError(ex.Message, ex.StackTrace, "Error occurred while fetching exam type names.");
                return StatusCode(500, "Internal server error.");
            }
        }

        [HttpGet("grouped-quantitysheet-by-project")]
        public async Task<IActionResult> GetGroupedQuantitySheetByProject([FromQuery] int projectId)
        {
            try
            {
                if (projectId <= 0)
                    return BadRequest("Invalid projectId.");

                // Get the requested project
                var project = await _context.Projects
                    .FirstOrDefaultAsync(p => p.ProjectId == projectId);

                if (project == null)
                    return NotFound("Project not found.");

                // Fetch QuantitySheets for the given project
                var quantitySheets = await _context.QuantitySheets
                    .Where(q => q.ProjectId == projectId)
                    .ToListAsync();

                // Prepare grouped result
                var result = quantitySheets
                    .GroupBy(q => new { q.ProjectId, q.CourseId })
                    .Select(g => new
                    {
                        GroupId = project.GroupId,
                        ProjectId = g.Key.ProjectId,
                        CourseId = g.Key.CourseId,
                        //TotalQuantity = g.Sum(x => x.Quantity),
                        //Count = g.Count(),
                        TypeId = project.TypeId,
                        PaperCount = _context.QpMasters
                            .Count(qp => qp.GroupId == project.GroupId && qp.CourseId == g.Key.CourseId),
                        //NoOfSeries = project.TypeId == 1 ? project.NoOfSeries : null,
                        Count = (project.TypeId == 1 && project.NoOfSeries.HasValue && project.NoOfSeries.Value > 0)
                            ? (int?)Math.Ceiling((double)g.Count() / project.NoOfSeries.Value)
                            : null
                    })
                    .ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }




    }
}

