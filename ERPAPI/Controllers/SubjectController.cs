﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using ERPAPI.Model;
using ERPAPI.Data;
using ERPAPI.Service;

namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SubjectController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILoggerService _loggerService;

        public SubjectController(AppDbContext context, ILoggerService loggerService)
        {
            _context = context;
            _loggerService = loggerService;
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

        // GET: api/Subject/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetSubjectById(int id)
        {
            var subject = await _context.Subjects.FindAsync(id);
            if (subject == null)
                return NotFound("Subject not found.");

            return Ok(subject);
        }


        // GET: api/Subject
        [HttpGet]
        public async Task<IActionResult> GetAllSubjects()
        {
            try
            {
                var subjects = await _context.Subjects.ToListAsync();
                return Ok(subjects);
            }
            catch (Exception ex)
            {
                // Log the exception
                _loggerService.LogError(ex.Message, "An error occurred while fetching subjects.", "SubjectController");

                // Return a 500 Internal Server Error response with a user-friendly message
                return StatusCode(500, "Internal server error. Please try again later.");
            }
        }

        [HttpGet("Subject")]
        public async Task<IActionResult> GetSubjectId(string subject)
        {
            var subjectname = await _context.Subjects
                .Where(c => c.SubjectName == subject)
                .Select(c => c.SubjectId)
                .FirstOrDefaultAsync();

            if (subjectname == null)
                return NotFound("Subject not found.");

            return Ok(subjectname);

        }
    }
}
