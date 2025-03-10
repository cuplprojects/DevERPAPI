using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ERPAPI.Data;
using ERPAPI.Model;
using ERPAPI.Service;

namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TextLabelsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILoggerService _loggerService;

        public TextLabelsController(AppDbContext context, ILoggerService loggerService)
        {
            _context = context;
            _loggerService = loggerService;
        }

        private int GetUserIdFromIdentity()
        {
            // Extract the user ID from User.Identity.Name, assuming it holds a numeric string representation of the user ID.
            return User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0;
        }

        // GET: api/TextLabels
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TextLabel>>> GetTextLabel()
        {
            try
            {
                var labels = await _context.TextLabel.ToListAsync();
                int userId = GetUserIdFromIdentity();
              
                return labels;
            }
            catch (Exception )
            {
               
                return StatusCode(500, "Failed to retrieve text labels");
            }
        }

        // GET: api/TextLabels/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TextLabel>> GetTextLabel(int id)
        {
            try
            {
                var textLabel = await _context.TextLabel.FindAsync(id);

                if (textLabel == null)
                {
                    
                    return NotFound();
                }

               
                return textLabel;
            }
            catch (Exception ex)
            {
              
                return StatusCode(500, "Failed to retrieve text label");
            }
        }

        // GET: api/TextLabels/translations/{language}
        [HttpGet("translations/{language}")]
        public async Task<ActionResult<Dictionary<string, object>>> GetTranslations(string language)
        {
            if (language != "en" && language != "hi")
            {
               
                return BadRequest("Invalid language. Supported languages are 'en' and 'hi'.");
            }

            try
            {
                var textLabels = await _context.TextLabel.ToListAsync();
                var translations = textLabels.ToDictionary(
                    label => label.LabelKey,
                    label => new
                    {
                        id = label.TextLabelId,
                        text = language == "en" ? label.EnglishLabel : label.HindiLabel
                    });

                return Ok(translations);
            }
            catch (Exception ex)
            {
               
                return StatusCode(500, "Failed to retrieve translations");
            }
        }


        // PUT: api/TextLabels/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTextLabel(int id, TextLabel textLabel)
        {
            if (id != textLabel.TextLabelId)
            {
                _loggerService.LogError("TextLabel ID mismatch", "TextLabel ID mismatch during update", "TextLabelsController");
                return BadRequest();
            }

            var existingLabel = await _context.TextLabel.FindAsync(id);
            if (existingLabel == null)
            {
                _loggerService.LogEvent($"Text label with ID {id} not found for update", "TextLabels", GetUserIdFromIdentity());
                return NotFound();
            }

            string oldValue = $"EnglishLabel: {existingLabel.EnglishLabel}, HindiLabel: {existingLabel.HindiLabel}";
            string newValue = $"EnglishLabel: {textLabel.EnglishLabel}, HindiLabel: {textLabel.HindiLabel}";

            _context.Entry(existingLabel).CurrentValues.SetValues(textLabel);

            try
            {
                await _context.SaveChangesAsync();
                _loggerService.LogEvent($"Updated text label with ID {id}", "TextLabels", GetUserIdFromIdentity(), oldValue, newValue);
                return NoContent();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TextLabelExists(id))
                {
                    return NotFound();
                }
                else
                {
                    _loggerService.LogError("Concurrency exception during update", "Failed to update text label", "TextLabelsController");
                    return StatusCode(500, "Failed to update text label");
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogError(ex.Message, "Failed to update text label", "TextLabelsController");
                return StatusCode(500, "Failed to update text label");
            }
        }

        // POST: api/TextLabels
        [HttpPost]
        public async Task<ActionResult<TextLabel>> PostTextLabel(TextLabel textLabel)
        {
            try
            {
                _context.TextLabel.Add(textLabel);
                await _context.SaveChangesAsync();
                _loggerService.LogEvent($"Created new text label with ID {textLabel.TextLabelId}", "TextLabels", GetUserIdFromIdentity());
                return CreatedAtAction("GetTextLabel", new { id = textLabel.TextLabelId }, textLabel);
            }
            catch (Exception ex)
            {
                _loggerService.LogError(ex.Message, "Failed to create text label", "TextLabelsController");
                return StatusCode(500, "Failed to create text label");
            }
        }

        // DELETE: api/TextLabels/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTextLabel(int id)
        {
            var textLabel = await _context.TextLabel.FindAsync(id);
            if (textLabel == null)
            {
                _loggerService.LogEvent($"Text label with ID {id} not found for deletion", "TextLabels", GetUserIdFromIdentity());
                return NotFound();
            }

            string oldValue = $"EnglishLabel: {textLabel.EnglishLabel}, HindiLabel: {textLabel.HindiLabel}";

            try
            {
                _context.TextLabel.Remove(textLabel);
                await _context.SaveChangesAsync();
                _loggerService.LogEvent($"Deleted text label with ID {id}", "TextLabels", GetUserIdFromIdentity(), oldValue, null);
                return NoContent();
            }
            catch (Exception ex)
            {
                _loggerService.LogError(ex.Message, "Failed to delete text label", "TextLabelsController");
                return StatusCode(500, "Failed to delete text label");
            }
        }

        private bool TextLabelExists(int id)
        {
            return _context.TextLabel.Any(e => e.TextLabelId == id);
        }
    }
}
