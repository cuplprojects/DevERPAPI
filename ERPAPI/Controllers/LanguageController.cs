﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using ERPAPI.Model;
using ERPAPI.Data;

namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LanguageController : ControllerBase
    {
        private readonly AppDbContext _context;

        public LanguageController(AppDbContext context)
        {
            _context = context;
        }

        // POST: api/Language
        [HttpPost]
        public async Task<IActionResult> CreateLanguage([FromBody] Language language)
        {
            if (language == null)
                return BadRequest("Invalid language data.");

            _context.Languages.Add(language);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetLanguageById), new { id = language.LanguageId }, language);
        }

        // PUT: api/Language/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateLanguage(int id, [FromBody] Language updatedLanguage)
        {
            if (id != updatedLanguage.LanguageId)
                return BadRequest("Language ID mismatch.");

            var existingLanguage = await _context.Languages.FindAsync(id);
            if (existingLanguage == null)
                return NotFound("Language not found.");

            existingLanguage.Languages = updatedLanguage.Languages;

            await _context.SaveChangesAsync();
            return Ok(existingLanguage);
        }

        // DELETE: api/Language/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteLanguage(int id)
        {
            var language = await _context.Languages.FindAsync(id);
            if (language == null)
                return NotFound("Language not found.");

            _context.Languages.Remove(language);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/Language/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetLanguageById(int id)
        {
            var language = await _context.Languages.FindAsync(id);
            if (language == null)
                return NotFound("Language not found.");

            return Ok(language);
        }


        // GET: api/Language
        [HttpGet]
        public async Task<IActionResult> GetAllLanguages()
        {
            var languages = await _context.Languages.ToListAsync();
            return Ok(languages);
        }

        [HttpGet("Language")]
        public async Task<ActionResult<IEnumerable<Language>>> GetLanguageId(string language)
        {
            var languages = await _context.Languages
                  .Where(c => c.Languages == language)
                  .Select(c => c.LanguageId)
                  .FirstOrDefaultAsync();

            if (languages == null)
                return NotFound("Language not found.");

            return Ok(languages);
        }

    }
}
