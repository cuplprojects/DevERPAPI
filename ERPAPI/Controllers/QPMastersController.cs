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

            foreach (var qpmaster in qpmasterList)
            {
                var groupId = qpmaster.GroupId;
                var group = await _context.Groups
                    .Where(p => p.Id == groupId)
                    .FirstOrDefaultAsync();

                if (group == null)
                {
                    return BadRequest($"Group with ID {groupId} not found.");
                }

                // Add the QpMaster object to the context
                await _context.QpMasters.AddAsync(qpmaster);
            }

            // Save all changes in one batch
            await _context.SaveChangesAsync();

            // Log the event for each item in the list
            foreach (var qpmaster in qpmasterList)
            {
                var groupId = qpmaster.GroupId;
                _loggerService.LogEvent(
                    "New QuantitySheet added",
                    "QuantitySheet",
                    1, // Replace with actual user ID or triggered by value
                    null,
                    $"GroupId: {groupId}"
                );
            }

            return Ok();
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

                        select new
                        {
                            qp.QPMasterId,
                            GroupName = grp != null ? grp.Name : null,
                            Type = et != null ? et.Type : null,
                            qp.NEPCode,
                            qp.PrivateCode,
                            SubjectName = sub != null ? sub.SubjectName : null,
                            qp.PaperNumber,
                            qp.PaperTitle,
                            qp.MaxMarks,
                            qp.Duration,
                            qp.CustomizedField1,
                            qp.CustomizedField2,
                            qp.CustomizedField3,
                            CourseName = crs != null ? crs.CourseName : null,
                            ExamTypeName = et != null ? et.TypeName : null
                        };

            if (groupId.HasValue)
                query = query.Where(q => q.GroupName != null &&
                    _context.Groups.Any(g => g.Id == groupId && g.Name == q.GroupName));

            if (typeId.HasValue)
                query = query.Where(q => _context.ExamTypes.Any(t => t.ExamTypeId == typeId && t.Type == q.Type));

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
            [FromQuery] int? groupId, // Add groupId as a nullable int
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 5)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return BadRequest("");
            }

            var query = (
                from qp in _context.QpMasters
                join crs in _context.Courses on qp.CourseId equals crs.CourseId into crsJoin
                from crs in crsJoin.DefaultIfEmpty()
                where (qp.NEPCode.Contains(search) ||
                    qp.PrivateCode.Contains(search) ||
                    qp.PaperNumber.Contains(search) ||
                    qp.PaperTitle.Contains(search)) &&
                    (!groupId.HasValue || qp.GroupId == groupId) // Add groupId filter
                select new
                {
                    qp.QPMasterId,
                    qp.NEPCode,
                    qp.PaperTitle,
                    qp.CourseId,
                    CourseName = crs.CourseName // Select CourseName from the joined table
                }
            );

            var result = await query
                .AsNoTracking()
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(result);
        }



        [HttpPost("InsertIntoQuantitySheet")]
        public async Task<IActionResult> InsertIntoQuantitySheet([FromBody] int qpMasterId, int projectId)
        {
            // Retrieve data from QpMaster table
            var qpMaster = await _context.QpMasters
                .Where(qp => qp.QPMasterId == qpMasterId)
                .Select(qp => new
                {
                    qp.NEPCode,
                    qp.PrivateCode,
                    qp.PaperNumber,
                    qp.PaperTitle,
                    qp.CourseId,
                    qp.MaxMarks,
                    qp.Duration,
                    qp.ExamTypeId,
                    qp.SubjectId,
                    qp.LanguageId // Assuming LanguageId is a list of integers
                })
                .FirstOrDefaultAsync();

            if (qpMaster == null)
            {
                return NotFound("QPMaster not found.");
            }

            // Create a new QuantitySheet object and map the relevant fields
            var quantitySheet = new QuantitySheet
            {
                NEPCode = qpMaster.NEPCode,
                PrivateCode = qpMaster.PrivateCode,
                PaperNumber = qpMaster.PaperNumber,
                PaperTitle = qpMaster.PaperTitle,
                CourseId = qpMaster.CourseId.Value, // Ensure CourseId is not null
                MaxMarks = qpMaster.MaxMarks.Value, // Ensure MaxMarks is not null
                Duration = qpMaster.Duration,
                ExamTypeId = qpMaster.ExamTypeId.Value, // Ensure ExamTypeId is not null
                SubjectId = qpMaster.SubjectId.Value, // Ensure SubjectId is not null
               /* Language = string.Join(", ", qpMaster.LanguageId ?? new List<int>()),*/ // Convert list to string
                LanguageId = qpMaster.LanguageId ?? new List<int>(),

                QPId = qpMasterId, // Assuming QPId in QuantitySheet corresponds to QPMasterId
                ProjectId = projectId // Assign ProjectId to QuantitySheet
            };

            // Insert the new QuantitySheet record into the database
            _context.QuantitySheets.Add(quantitySheet);
            await _context.SaveChangesAsync();

            return Ok("Data inserted into QuantitySheet successfully.");
        }


        /* [HttpPost("InsertIntoQuantitySheet")]
         public async Task<IActionResult> InsertIntoQuantitySheet([FromBody] InsertQuantitySheetRequest request)
         {
             // Retrieve data from QpMaster table
             var qpMaster = await _context.QpMasters
                 .Where(qp => qp.QPMasterId == request.QPMasterId)
                 .Select(qp => new
                 {
                     qp.NEPCode,
                     qp.PrivateCode,
                     qp.PaperNumber,
                     qp.PaperTitle,
                     qp.CourseId,
                     qp.MaxMarks,
                     qp.Duration,
                     qp.ExamTypeId,
                     qp.SubjectId,
                     qp.LanguageId // Assuming LanguageId is a list of integers
                 })
                 .FirstOrDefaultAsync();

             if (qpMaster == null)
             {
                 return NotFound("QPMaster not found.");
             }

             // Create a new QuantitySheet object and map the relevant fields
             var quantitySheet = new QuantitySheet
             {
                 NEPCode = qpMaster.NEPCode,
                 PrivateCode = qpMaster.PrivateCode,
                 PaperNumber = qpMaster.PaperNumber,
                 PaperTitle = qpMaster.PaperTitle,
                 CourseId = qpMaster.CourseId.Value, // Ensure CourseId is not null
                 MaxMarks = qpMaster.MaxMarks.Value, // Ensure MaxMarks is not null
                 Duration = qpMaster.Duration,
                 ExamTypeId = qpMaster.ExamTypeId.Value, // Ensure ExamTypeId is not null
                 SubjectId = qpMaster.SubjectId.Value, // Ensure SubjectId is not null
                 Language = string.Join(", ", qpMaster.LanguageId ?? new List<int>()), // Convert list to string
                 QPId = request.QPMasterId, // Assuming QPId in QuantitySheet corresponds to QPMasterId

                 // Map the additional field from the request
                 CatchNo = request.CatchNo
             };

             // Insert the new QuantitySheet record into the database
             _context.QuantitySheets.Add(quantitySheet);
             await _context.SaveChangesAsync();

             return Ok("Data inserted into QuantitySheet successfully.");
         }

         // Define a request model to capture the additional field
         public class InsertQuantitySheetRequest
         {
             public int QPMasterId { get; set; }
             public string CatchNo { get; set; }
         }*/



        [HttpPost("InsertIntoQuantitySheetByCourseId")]
        public async Task<IActionResult> InsertIntoQuantitySheetByCourseId([FromBody] int courseId)
        {
            // Retrieve data from QpMaster table based on courseId
            var qpMasters = await _context.QpMasters
                .Where(qp => qp.CourseId == courseId)
                .Select(qp => new
                {
                    qp.QPMasterId, // Include QPMasterId in the selection
                    qp.NEPCode,
                    qp.PrivateCode,
                    qp.PaperNumber,
                    qp.PaperTitle,
                    qp.CourseId,
                    qp.MaxMarks,
                    qp.Duration,
                    qp.ExamTypeId,
                    qp.SubjectId,
                    qp.LanguageId // Assuming LanguageId is a list of integers
                })
                .ToListAsync();

            if (qpMasters == null || !qpMasters.Any())
            {
                return NotFound("No QPMasters found for the given course ID.");
            }

            // Iterate over each QpMaster and insert into QuantitySheet
            foreach (var qpMaster in qpMasters)
            {
                // Create a new QuantitySheet object and map the relevant fields
                var quantitySheet = new QuantitySheet
                {
                    NEPCode = qpMaster.NEPCode,
                    PrivateCode = qpMaster.PrivateCode,
                    PaperNumber = qpMaster.PaperNumber,
                    PaperTitle = qpMaster.PaperTitle,
                    CourseId = qpMaster.CourseId.Value, // Ensure CourseId is not null
                    MaxMarks = qpMaster.MaxMarks.Value, // Ensure MaxMarks is not null
                    Duration = qpMaster.Duration,
                    ExamTypeId = qpMaster.ExamTypeId.Value, // Ensure ExamTypeId is not null
                    SubjectId = qpMaster.SubjectId.Value, // Ensure SubjectId is not null
                    LanguageId = qpMaster.LanguageId ?? new List<int>(), // Convert list to string
                    ProcessId = new List<int>(),
                    QPId = qpMaster.QPMasterId // Use QPMasterId from the selection
                };

                // Insert the new QuantitySheet record into the database
                _context.QuantitySheets.Add(quantitySheet);
                await _context.SaveChangesAsync();
            }

            return Ok("Data inserted into QuantitySheet successfully.");
        }

/*        [HttpPost("InsertIntoQuantitySheetByCourseId")]
        public async Task<IActionResult> InsertIntoQuantitySheetByCourseId([FromBody] InsertQuantitySheetByCourseIdRequest request)
        {
            // Retrieve data from QpMaster table based on courseId
            var qpMasters = await _context.QpMasters
                .Where(qp => qp.CourseId == request.CourseId)
                .Select(qp => new
                {
                    qp.QPMasterId, // Include QPMasterId in the selection
                    qp.NEPCode,
                    qp.PrivateCode,
                    qp.PaperNumber,
                    qp.PaperTitle,
                    qp.CourseId,
                    qp.MaxMarks,
                    qp.Duration,
                    qp.ExamTypeId,
                    qp.SubjectId,
                    qp.LanguageId // Assuming LanguageId is a list of integers
                })
                .ToListAsync();

                    if (qpMasters == null || !qpMasters.Any())
                    {
                        return NotFound("No QPMasters found for the given course ID.");
                    }

                    // Iterate over each QpMaster and insert into QuantitySheet
                    foreach (var qpMaster in qpMasters)
                    {
                        // Create a new QuantitySheet object and map the relevant fields
                        var quantitySheet = new QuantitySheet
                        {
                            NEPCode = qpMaster.NEPCode,
                            PrivateCode = qpMaster.PrivateCode,
                            PaperNumber = qpMaster.PaperNumber,
                            PaperTitle = qpMaster.PaperTitle,
                            CourseId = qpMaster.CourseId.Value, // Ensure CourseId is not null
                            MaxMarks = qpMaster.MaxMarks.Value, // Ensure MaxMarks is not null
                            Duration = qpMaster.Duration,
                            ExamTypeId = qpMaster.ExamTypeId.Value, // Ensure ExamTypeId is not null
                            SubjectId = qpMaster.SubjectId.Value, // Ensure SubjectId is not null
                            Language = string.Join(", ", qpMaster.LanguageId ?? new List<int>()), // Convert list to string
                            QPId = qpMaster.QPMasterId, // Use QPMasterId from the selection

                            // Map the additional field from the request
                            CatchNo = request.CatchNo
                        };

                        // Insert the new QuantitySheet record into the database
                        _context.QuantitySheets.Add(quantitySheet);
                        await _context.SaveChangesAsync();
                    }

                    return Ok("Data inserted into QuantitySheet successfully.");
                }

        // Define a request model to capture the additional field
        public class InsertQuantitySheetByCourseIdRequest
        {
            public int CourseId { get; set; }
            public string CatchNo { get; set; }
        }*/

        [HttpPost("InsertIntoQuantitySheetByExamTypeIds")]
        public async Task<IActionResult> InsertIntoQuantitySheetByExamTypeIds([FromBody] List<int> examTypeIds, int projectId)
        {
            if (examTypeIds == null || !examTypeIds.Any())
            {
                return BadRequest("Invalid input. ExamTypeIds array cannot be null or empty.");
            }

            // Retrieve data from QpMaster table based on ExamTypeIds
            var qpMasters = await _context.QpMasters
                .Where(qp => examTypeIds.Contains(qp.ExamTypeId ?? 0)) // Handle nullable int
                .Select(qp => new
                {
                    qp.QPMasterId,
                    qp.NEPCode,
                    qp.PrivateCode,
                    qp.PaperNumber,
                    qp.PaperTitle,
                    qp.CourseId,
                    qp.MaxMarks,
                    qp.Duration,
                    qp.ExamTypeId,
                    qp.SubjectId,
                    qp.LanguageId // Assuming LanguageId is a list of integers
                })
                .ToListAsync();

            if (!qpMasters.Any())
            {
                return NotFound("No QPMasters found for the given ExamType IDs.");
            }

            foreach (var qpMaster in qpMasters)
            {
                var quantitySheet = new QuantitySheet
                {
                    NEPCode = qpMaster.NEPCode,
                    PrivateCode = qpMaster.PrivateCode,
                    PaperNumber = qpMaster.PaperNumber,
                    PaperTitle = qpMaster.PaperTitle,
                    CourseId = qpMaster.CourseId ?? 0, // Ensure non-null value
                    MaxMarks = qpMaster.MaxMarks ?? 0, // Ensure non-null value
                    Duration = qpMaster.Duration, // Assuming Duration is non-nullable
                    ExamTypeId = qpMaster.ExamTypeId ?? 0, // Ensure non-null value
                    SubjectId = qpMaster.SubjectId ?? 0, // Ensure non-null value
                    LanguageId = qpMaster.LanguageId ?? new List<int>(), // Convert list to string
                    ProcessId = new List<int>(),
                    QPId = qpMaster.QPMasterId, // Assuming QPMasterId is never null
                    ProjectId = projectId // Assign ProjectId to QuantitySheet
                };

                _context.QuantitySheets.Add(quantitySheet);
            }

            await _context.SaveChangesAsync();
            return Ok("Data inserted into QuantitySheet successfully.");
        }



    }
}

