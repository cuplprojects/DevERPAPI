using Microsoft.AspNetCore.Mvc;
using ERPAPI.Data;
using ERPAPI.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CatchTeamsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CatchTeamsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/CatchTeams
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CatchTeam>>> GetCatchTeams()
        {
            try
            {
                var catchTeams = await _context.CatchTeams.ToListAsync();
                return Ok(catchTeams);
            }
            catch (Exception)
            {
                // Log error here
                return StatusCode(500, "Failed to retrieve catch teams");
            }
        }

        // GET: api/CatchTeams/{id}
        [HttpGet("{QuantitySheetId}")]
        public async Task<ActionResult<CatchTeam>> GetCatchTeam(int QuantitySheetId)
        {
            try
            {
                var catchTeam = await _context.CatchTeams.FindAsync(QuantitySheetId);

                if (catchTeam == null)
                {
                    return NotFound();
                }

                return Ok(catchTeam);
            }
            catch (Exception)
            {
                // Log error here
                return StatusCode(500, "Failed to retrieve catch team");
            }
        }

        // POST: api/CatchTeams
        [HttpPost]
        public async Task<ActionResult<IEnumerable<CatchTeam>>> PostCatchTeam([FromBody] List<CatchTeam> catchTeams)
        {
            if (catchTeams == null || catchTeams.Count == 0)
            {
                return BadRequest("Invalid catch team data");
            }

            try
            {
                foreach (var catchTeam in catchTeams)
                {
                    // Convert the Members array to a comma-separated string if needed
                    catchTeam.Members = string.Join(",", catchTeam.Members.Trim('[', ']', '\"').Split(',').Select(m => m.Trim()));

                    // Check if a catch team with the same QuantitySheetId already exists
                    var existingCatchTeam = await _context.CatchTeams
                        .FirstOrDefaultAsync(ct => ct.QuantitySheetId == catchTeam.QuantitySheetId);

                    if (existingCatchTeam != null)
                    {
                        // Update the existing catch team
                        existingCatchTeam.Members = catchTeam.Members;
                        _context.Entry(existingCatchTeam).State = EntityState.Modified;
                    }
                    else
                    {
                        // Add the new catch team
                        _context.CatchTeams.Add(catchTeam);
                    }
                }

                await _context.SaveChangesAsync();
                return CreatedAtAction(nameof(GetCatchTeams), catchTeams);
            }
            catch (Exception)
            {
                // Log error here
                return StatusCode(500, "Failed to create or update catch teams");
            }
        }

        // PUT: api/CatchTeams/{id}
        [HttpPut("{QuantitySheetId}")]
        public async Task<IActionResult> PutCatchTeam(int QuantitySheetId, [FromBody] CatchTeam catchTeam)
        {
            if (QuantitySheetId != catchTeam.QuantitySheetId)
            {
                return BadRequest("CatchTeam ID mismatch");
            }

            try
            {
                // Convert the Members array to a comma-separated string
                catchTeam.Members = string.Join(",", catchTeam.Members.Trim('[', ']', '\"').Split(',').Select(m => m.Trim()));

                _context.Entry(catchTeam).State = EntityState.Modified;

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CatchTeamExists(QuantitySheetId))
                {
                    return NotFound();
                }
                else
                {
                    // Log error here
                    return StatusCode(500, "Failed to update catch team");
                }
            }
            catch (Exception ex)
            {
                // Log error here
                return StatusCode(500, "Failed to update catch team");
            }
        }

        // DELETE: api/CatchTeams/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCatchTeam(int id)
        {
            try
            {
                var catchTeam = await _context.CatchTeams.FindAsync(id);
                if (catchTeam == null)
                {
                    return NotFound();
                }

                _context.CatchTeams.Remove(catchTeam);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception)
            {
                // Log error here
                return StatusCode(500, "Failed to delete catch team");
            }
        }

        [HttpPut]
        public async Task<IActionResult> PutCatchTeams([FromBody] List<CatchTeam> catchTeams)
        {
            if (catchTeams == null || catchTeams.Count == 0)
            {
                return BadRequest("Invalid catch team data");
            }

            try
            {
                foreach (var catchTeam in catchTeams)
                {
                    // Check if the catch team exists
                    if (!CatchTeamExists(catchTeam.QuantitySheetId))
                    {
                        return NotFound($"CatchTeam with ID {catchTeam.QuantitySheetId} not found");
                    }

                    // Convert the Members array to a comma-separated string
                    catchTeam.Members = string.Join(",", catchTeam.Members.Trim('[', ']', '\"').Split(',').Select(m => m.Trim()));

                    // Update the entity
                    _context.Entry(catchTeam).State = EntityState.Modified;
                }

                await _context.SaveChangesAsync();
                return NoContent(); // Return a 204 No Content status on success
            }
            catch (Exception)
            {
                // Log error here
                return StatusCode(500, "Failed to update catch teams");
            }
        }


        private bool CatchTeamExists(int id)
        {
            return _context.CatchTeams.Any(e => e.QuantitySheetId == id);
        }
    }
}
