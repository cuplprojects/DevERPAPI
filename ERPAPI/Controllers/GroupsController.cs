using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ERPAPI.Data;
using ERPAPI.Model;
using ERPAPI.Services;
using ERPAPI.Service;

namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GroupsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILoggerService _loggerService;

        public GroupsController(AppDbContext context, ILoggerService loggerService)
        {
            _context = context;
            _loggerService = loggerService;
        }

        // GET: api/Groups
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Group>>> GetGroups()
        {
            try
            {
                var groups = await _context.Groups.OrderByDescending(g=>g.Id).ToListAsync();
                return groups;
            }
            catch (Exception ex)
            {
               
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Groups/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Group>> GetGroup(int id)
        {
            try
            {
                var group = await _context.Groups.FindAsync(id);

                if (group == null)
                {
                
                    return NotFound();
                }

                return group;
            }
            catch (Exception)
            {
             
                return StatusCode(500, "Internal server error");
            }
        }

        // PUT: api/Groups/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutGroup(int id, Group @group)
        {
            if (id != @group.Id)
            {
                return BadRequest();
            }

            // Fetch existing entity to capture old values
            var existingGroup = await _context.Groups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id);
            if (existingGroup == null)
            {
                _loggerService.LogEvent($"Group with ID {id} not found during update", "Groups", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                return NotFound();
            }

            // Capture old and new values for logging
            string oldValue = Newtonsoft.Json.JsonConvert.SerializeObject(existingGroup);
            string newValue = Newtonsoft.Json.JsonConvert.SerializeObject(@group);

            _context.Entry(@group).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                _loggerService.LogEvent($"Updated group with ID {id}", "Groups", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0, oldValue, newValue);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (!GroupExists(id))
                {
                    _loggerService.LogEvent($"Group with ID {id} not found during update", "Groups", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                    return NotFound();
                }
                else
                {
                    _loggerService.LogError("Concurrency error during group update", ex.Message, nameof(GroupsController));
                    throw;
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error updating group", ex.Message, nameof(GroupsController));
                return StatusCode(500, "Internal server error");
            }

            return NoContent();
        }

        // POST: api/Groups
        [HttpPost]
        public async Task<ActionResult<Group>> PostGroup(Group @group)
        {
            try
            {
                _context.Groups.Add(@group);
                await _context.SaveChangesAsync();
                _loggerService.LogEvent("Created a new group", "Groups", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);

                return CreatedAtAction("GetGroup", new { id = @group.Id }, @group);
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error creating group", ex.Message, nameof(GroupsController));
                return StatusCode(500, "Internal server error");
            }
        }

        // DELETE: api/Groups/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGroup(int id)
        {
            try
            {
                var group = await _context.Groups.FindAsync(id);
                if (group == null)
                {
                    _loggerService.LogEvent($"Group with ID {id} not found during delete", "Groups", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                    return NotFound();
                }

                _context.Groups.Remove(group);
                await _context.SaveChangesAsync();
                _loggerService.LogEvent($"Deleted group with ID {id}", "Groups", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);

                return NoContent();
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error deleting group", ex.Message, nameof(GroupsController));
                return StatusCode(500, "Internal server error");
            }
        }

        private bool GroupExists(int id)
        {
            return _context.Groups.Any(e => e.Id == id);
        }
    }
}
