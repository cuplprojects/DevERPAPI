using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using ERPAPI.Model;
using ERPAPI.Data;

namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SubjectController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SubjectController(AppDbContext context)
        {
            _context = context;
        }

        // POST: api/Subject
        [HttpPost]
        public async Task<IActionResult> CreateSubject([FromBody] Subject subject)
        {
            if (subject == null)
                return BadRequest("Invalid subject data.");

            _context.Subjects.Add(subject);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetSubjectById), new { id = subject.SubjectId }, subject);
        }

        // PUT: api/Subject/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSubject(int id, [FromBody] Subject updatedSubject)
        {
            if (id != updatedSubject.SubjectId)
                return BadRequest("Subject ID mismatch.");

            var subject = await _context.Subjects.FindAsync(id);
            if (subject == null)
                return NotFound("Subject not found.");

            subject.SubjectName = updatedSubject.SubjectName;

            await _context.SaveChangesAsync();
            return Ok(subject);
        }

        // DELETE: api/Subject/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSubject(int id)
        {
            var subject = await _context.Subjects.FindAsync(id);
            if (subject == null)
                return NotFound("Subject not found.");

            _context.Subjects.Remove(subject);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // Optional: GET by ID
        [HttpGet("{id}")]
        public async Task<IActionResult> GetSubjectById(int id)
        {
            var subject = await _context.Subjects.FindAsync(id);
            if (subject == null)
                return NotFound("Subject not found.");

            return Ok(subject);
        }
    }
}
