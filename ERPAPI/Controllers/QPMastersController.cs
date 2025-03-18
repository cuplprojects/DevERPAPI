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
                               prop.Name != "CustomizedField3" )
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
     [FromQuery] int? examTypeId = null)
        {
            var query = _context.QpMasters.AsQueryable();

            if (groupId.HasValue || typeId.HasValue || courseId.HasValue || examTypeId.HasValue)
            {
                if (groupId.HasValue)
                    query = query.Where(q => q.GroupId == groupId);
                if (typeId.HasValue)
                    query = query.Where(q => q.TypeId == typeId);
                if (courseId.HasValue)
                    query = query.Where(q => q.CourseId == courseId);
                if (examTypeId.HasValue)
                    query = query.Where(q => q.ExamTypeId == examTypeId);
            }

            var result = await query
                .AsNoTracking()
                .Select(q => new
                {
                    q.QPMasterId,
                    q.GroupId,
                    q.TypeId,
                    q.NEPCode,
                    q.PrivateCode,
                    q.SubjectId,
                    q.PaperNumber,
                    q.PaperTitle,
                    q.MaxMarks,
                    q.Duration,
                    q.CustomizedField1,
                    q.CustomizedField2,
                    q.CustomizedField3,
                    q.CourseId,
                    q.ExamTypeId
                })
                .ToListAsync();  // Add ToListAsync() to execute the query and return the result

            return Ok(result);
        }


    }
}

