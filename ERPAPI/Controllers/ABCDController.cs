using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ERPAPI.Model;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ERPAPI.Data;

namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ABCDController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ABCDController(AppDbContext context)
        {
            _context = context;
        }

        // ✅ GET: api/ABCD → Get all records
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ABCD>>> GetAllABCD()
        {
            return await _context.ABCD.ToListAsync();
        }

        // ✅ GET: api/ABCD/{id} → Get a single record by ID
        [HttpGet("{id}")]
        public async Task<ActionResult<ABCD>> GetABCD(int id)
        {
            var abcd = await _context.ABCD.FindAsync(id);

            if (abcd == null)
            {
                return NotFound(new { message = "Record not found" });
            }

            return abcd;
        }

        // ✅ POST: api/ABCD → Create a new record
        [HttpPost]
        public async Task<ActionResult<ABCD>> CreateABCD([FromBody] ABCD abcd)
        {
            _context.ABCD.Add(abcd);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetABCD), new { id = abcd.ABCDId }, abcd);
        }

        // ✅ PUT: api/ABCD/{id} → Update an existing record
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateABCD(int id, ABCD updatedABCD)
        {
            if (id != updatedABCD.ABCDId)
            {
                return BadRequest(new { message = "ID mismatch" });
            }

            _context.Entry(updatedABCD).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.ABCD.Any(e => e.ABCDId == id))
                {
                    return NotFound(new { message = "Record not found" });
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // ✅ DELETE: api/ABCD/{id} → Delete a record
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteABCD(int id)
        {
            var abcd = await _context.ABCD.FindAsync(id);
            if (abcd == null)
            {
                return NotFound(new { message = "Record not found" });
            }

            _context.ABCD.Remove(abcd);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
