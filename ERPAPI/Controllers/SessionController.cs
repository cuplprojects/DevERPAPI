﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using ERPAPI.Model;
using ERPAPI.Data;
using System.Linq;

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
            if (updatedSession == null || id != updatedSession.SessionId)
                return BadRequest("Invalid session data or session ID mismatch.");

            var existingSession = await _context.Sessions.FindAsync(id);
            if (existingSession == null)
                return NotFound($"No session found with ID = {id}.");

            // Update fields
            existingSession.session = updatedSession.session;
            existingSession.Status = updatedSession.Status;
            try
            {
                await _context.SaveChangesAsync();
                return Ok(new
                {
                    Message = "Session updated successfully.",
                    Data = existingSession
                });
            }
            catch (DbUpdateException ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
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

        // GET: api/Session/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetSessionById(int id)
        {
            var session = await _context.Sessions.FindAsync(id);
            if (session == null)
                return NotFound("Session not found.");

            return Ok(session);
        }

        // GET: api/Session
        [HttpGet]
        public async Task<IActionResult> GetAllSessions()
        {
            var sessions = await _context.Sessions.ToListAsync();
            return Ok(sessions);
        }
    }
}
