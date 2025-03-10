using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ERPAPI.Data;
using ERPAPI.Model;
using ERPAPI.Service; // Assuming your LoggerService namespace is here

namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MessagesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILoggerService _loggerService;

        public MessagesController(AppDbContext context, ILoggerService loggerService)
        {
            _context = context;
            _loggerService = loggerService;
        }

        private int GetUserIdFromIdentity()
        {
            return User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0;
        }

        // GET: api/Messages
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Message>>> GetMessage()
        {
            try
            {
                var messages = await _context.Message.ToListAsync();
               
                return messages;
            }
            catch (Exception ex)
            {
               
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Messages/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Message>> GetMessage(int id)
        {
            try
            {
                var message = await _context.Message.FindAsync(id);

                if (message == null)
                {
                   
                    return NotFound();
                }

               
                return message;
            }
            catch (Exception)
            {
              
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Messages/5?lang=L1
        [HttpGet("messagebyId/{id}")]
        public async Task<ActionResult> GetMessage(int id, [FromQuery] string lang)
        {
            var message = await _context.Message.FindAsync(id);

            if (message == null)
            {
               
                return NotFound(new { Message = "Message not found" });
            }

            string title;
            string description;

            // Determine which language to use for title and description based on the query parameter
            if (lang == "L1")
            {
                title = message.L1Title;
                description = message.L1Desc;
            }
            else if (lang == "L2")
            {
                title = message.L2Title;
                description = message.L2Desc;
            }
            else
            {
               
                return BadRequest(new { Message = "Invalid language parameter. Use 'L1' or 'L2'." });
            }

          

            var response = new
            {
                MessageId = message.MessageId,
                Title = title,
                Description = description
            };

            return Ok(response);
        }

        // PUT: api/Messages/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutMessage(int id, Message message)
        {
            if (id != message.MessageId)
            {
                _loggerService.LogError("Message ID mismatch", "ID mismatch during update", "MessagesController");
                return BadRequest();
            }

            var existingMessage = await _context.Message.FindAsync(id);
            if (existingMessage == null)
            {
                _loggerService.LogEvent($"Message with ID {id} not found for update", "Messages", GetUserIdFromIdentity());
                return NotFound();
            }

            string oldValue = $"L1Title: {existingMessage.L1Title}, L1Desc: {existingMessage.L1Desc}, L2Title: {existingMessage.L2Title}, L2Desc: {existingMessage.L2Desc}";
            string newValue = $"L1Title: {message.L1Title}, L1Desc: {message.L1Desc}, L2Title: {message.L2Title}, L2Desc: {message.L2Desc}";

            _context.Entry(existingMessage).CurrentValues.SetValues(message);

            try
            {
                await _context.SaveChangesAsync();
                _loggerService.LogEvent($"Updated message with ID {id}", "Messages", GetUserIdFromIdentity(), oldValue, newValue);
                return NoContent();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MessageExists(id))
                {
                    return NotFound();
                }
                else
                {
                    _loggerService.LogError("Concurrency exception during update", "Failed to update message", "MessagesController");
                    throw;
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogError(ex.Message, "Failed to update message", "MessagesController");
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/Messages
        [HttpPost]
        public async Task<ActionResult<Message>> PostMessage(Message message)
        {
            try
            {
                _context.Message.Add(message);
                await _context.SaveChangesAsync();
                _loggerService.LogEvent($"Created new message with ID {message.MessageId}", "Messages", GetUserIdFromIdentity());
                return CreatedAtAction("GetMessage", new { id = message.MessageId }, message);
            }
            catch (Exception ex)
            {
                _loggerService.LogError(ex.Message, "Failed to create message", "MessagesController");
                return StatusCode(500, "Internal server error");
            }
        }

        // DELETE: api/Messages/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMessage(int id)
        {
            var message = await _context.Message.FindAsync(id);
            if (message == null)
            {
                _loggerService.LogEvent($"Message with ID {id} not found for deletion", "Messages", GetUserIdFromIdentity());
                return NotFound();
            }

            string oldValue = $"L1Title: {message.L1Title}, L1Desc: {message.L1Desc}, L2Title: {message.L2Title}, L2Desc: {message.L2Desc}";

            try
            {
                _context.Message.Remove(message);
                await _context.SaveChangesAsync();
                _loggerService.LogEvent($"Deleted message with ID {id}", "Messages", GetUserIdFromIdentity(), oldValue, null);
                return NoContent();
            }
            catch (Exception ex)
            {
                _loggerService.LogError(ex.Message, "Failed to delete message", "MessagesController");
                return StatusCode(500, "Internal server error");
            }
        }

        private bool MessageExists(int id)
        {
            return _context.Message.Any(e => e.MessageId == id);
        }
    }
}
