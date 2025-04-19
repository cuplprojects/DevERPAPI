using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;
using ERPAPI.Data;
using ERPAPI.Model;

namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ExamTypeController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ExamTypeController(AppDbContext context)
        {
            _context = context;
        }

        // POST: api/ExamType
        [HttpPost]
        public async Task<IActionResult> CreateExamType([FromBody] ExamType examType)
        {
            if (examType == null)
                return BadRequest("Invalid data.");

            _context.ExamTypes.Add(examType);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetExamTypeById), new { id = examType.ExamTypeId }, examType);
        }

        // PUT: api/ExamType/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateExamType(int id, [FromBody] ExamType updatedExamType)
        {
            if (id != updatedExamType.ExamTypeId)
                return BadRequest("ID mismatch.");

            var existingExamType = await _context.ExamTypes.FindAsync(id);
            if (existingExamType == null)
                return NotFound();

            existingExamType.TypeName = updatedExamType.TypeName;
            

            await _context.SaveChangesAsync();
            return Ok(existingExamType);
        }

        // DELETE: api/ExamType/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteExamType(int id)
        {
            var examType = await _context.ExamTypes.FindAsync(id);
            if (examType == null)
                return NotFound();

            _context.ExamTypes.Remove(examType);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/ExamType/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetExamTypeById(int id)
        {
            var examType = await _context.ExamTypes.FindAsync(id);
            if (examType == null)
                return NotFound();

            return Ok(examType);
        }


        [HttpGet]
        public async Task<IActionResult> GetAllExamTypes()
        {
            var examTypes = await _context.ExamTypes.ToListAsync();
            return Ok(examTypes);
        }


        [HttpGet("ExamType")]
        public async Task<ActionResult<IEnumerable<ExamType>>> GetExamType(string examtype)
        {
            var types = await _context.ExamTypes
                  .Where(c => c.TypeName == examtype)
                  .Select(c => c.ExamTypeId)
                  .FirstOrDefaultAsync();

            if (types == null)
                return NotFound("Type not found.");

            return Ok(types);

        }
    }
}
