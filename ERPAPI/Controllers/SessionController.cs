using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using ERPAPI.Model;
using ERPAPI.Data;

namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SessionController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SessionController(AppDbContext context)
        {
            _context = context;
        }

        // POST: api/Session
        [HttpPost]
        public async Task<IActionResult> CreateSession([FromBody] Session session)
        {
            if (session == null)
                return BadRequest("Invalid session data.");

            _context.Sessions.Add(session);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetSessionById), new { id = session.SessionId }, session);
        }

        // PUT: api/Session/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSession(int id, [FromBody] Session updatedSession)
        {
            if (id != updatedSession.SessionId)
                return BadRequest("Session ID mismatch.");

            var existingSession = await _context.Sessions.FindAsync(id);
            if (existingSession == null)
                return NotFound("Session not found.");

            existingSession.session = updatedSession.session;

            await _context.SaveChangesAsync();
            return Ok(existingSession);
        }

        // DELETE: api/Session/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSession(int id)
        {
            var session = await _context.Sessions.FindAsync(id);
            if (session == null)
                return NotFound("Session not found.");

            _context.Sessions.Remove(session);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // Optional GET: api/Session/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetSessionById(int id)
        {
            var session = await _context.Sessions.FindAsync(id);
            if (session == null)
                return NotFound("Session not found.");

            return Ok(session);
        }
    }
}
