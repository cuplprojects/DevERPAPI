using Microsoft.AspNetCore.Mvc;
using ERPAPI.Model;
using Microsoft.EntityFrameworkCore;
using ERPAPI.Data;

namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QpMasterController : ControllerBase
    {
        private readonly AppDbContext _context;

        public QpMasterController(AppDbContext context)
        {
            _context = context;
        }

        // POST: api/QpMaster
        [HttpPost]
        public async Task<IActionResult> CreateQpMaster([FromBody] QpMaster qpMaster)
        {
            _context.QpMasters.Add(qpMaster);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetQpMaster), new { id = qpMaster.QPMasterId }, qpMaster);
        }

        // GET (helper to support POST CreatedAtAction)
        [HttpGet("{id}")]
        public async Task<ActionResult<QpMaster>> GetQpMaster(int id)
        {
            var qpMaster = await _context.QpMasters.FindAsync(id);
            if (qpMaster == null)
                return NotFound();

            return qpMaster;
        }

        // PUT: api/QpMaster/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateQpMaster(int id, [FromBody] QpMaster qpMaster)
        {
            if (id != qpMaster.QPMasterId)
                return BadRequest("ID mismatch");

            _context.Entry(qpMaster).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.QpMasters.Any(e => e.QPMasterId == id))
                    return NotFound();
                else
                    throw;
            }

            return NoContent();
        }

        // DELETE: api/QpMaster/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteQpMaster(int id)
        {
            var qpMaster = await _context.QpMasters.FindAsync(id);
            if (qpMaster == null)
                return NotFound();

            _context.QpMasters.Remove(qpMaster);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
