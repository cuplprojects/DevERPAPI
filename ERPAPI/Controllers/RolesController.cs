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
    public class RolesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILoggerService _loggerService;

        public RolesController(AppDbContext context, ILoggerService loggerService)
        {
            _context = context;
            _loggerService = loggerService;
        }

        // GET: api/Roles
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Role>>> GetRole()
        {
            try
            {
                var roles = await _context.Roles.ToListAsync();
                return Ok(roles);
            }
            catch (Exception)
            {
           
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Roles/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Role>> GetRole(int id)
        {
            try
            {
                var role = await _context.Roles.FindAsync(id);
                if (role == null)
                {
                  
                    return NotFound();
                }
                return role;
            }
            catch (Exception)
            {
               
                return StatusCode(500, "Internal server error");
            }
        }

        // PUT: api/Roles/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutRole(int id, Role role)
        {
            if (id != role.RoleId)
            {
                return BadRequest();
            }

            // Fetch existing entity to capture old values
            var existingRole = await _context.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.RoleId == id);
            if (existingRole == null)
            {
                _loggerService.LogEvent($"Role with ID {id} not found during update", "Roles", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                return NotFound();
            }

            // Capture old and new values for logging
            string oldValue = Newtonsoft.Json.JsonConvert.SerializeObject(existingRole);
            string newValue = Newtonsoft.Json.JsonConvert.SerializeObject(role);

            _context.Entry(role).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                _loggerService.LogEvent($"Updated role with ID {id}", "Roles", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0, oldValue, newValue);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (!RoleExists(id))
                {
                    _loggerService.LogEvent($"Role with ID {id} not found during update", "Roles", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                    return NotFound();
                }
                else
                {
                    _loggerService.LogError("Concurrency error during role update", ex.Message, nameof(RolesController));
                    throw;
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error updating role", ex.Message, nameof(RolesController));
                return StatusCode(500, "Internal server error");
            }

            return NoContent();
        }

        // POST: api/Roles
        [HttpPost]
        public async Task<ActionResult<Role>> PostRole(Role role)
        {
            try
            {
                // Validate the incoming role object
                if (string.IsNullOrEmpty(role.RoleName))
                {
                    return BadRequest(new { role = new[] { "The role field is required." } });
                }

                if (role.PriorityOrder <= 0)
                {
                    return BadRequest(new { priorityOrder = new[] { "Priority order must be a positive integer." } });
                }

                if (role.PermissionList == null || !role.PermissionList.Any())
                {
                    return BadRequest(new { permissionList = new[] { "At least one permission must be provided." } });
                }

                _context.Roles.Add(role);
                await _context.SaveChangesAsync();

                _loggerService.LogEvent($"Created a new role with ID {role.RoleId}", "Roles", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                return CreatedAtAction("GetRole", new { id = role.RoleId }, role);
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error creating role", ex.Message, nameof(RolesController));
                return StatusCode(500, "Internal server error");
            }
        }

        // DELETE: api/Roles/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRole(int id)
        {
            try
            {
                var role = await _context.Roles.FindAsync(id);
                if (role == null)
                {
                    _loggerService.LogEvent($"Role with ID {id} not found during delete", "Roles", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                    return NotFound();
                }

                _context.Roles.Remove(role);
                await _context.SaveChangesAsync();

                _loggerService.LogEvent($"Deleted role with ID {id}", "Roles", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                return NoContent();
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error deleting role", ex.Message, nameof(RolesController));
                return StatusCode(500, "Internal server error");
            }
        }

        private bool RoleExists(int id)
        {
            return _context.Roles.Any(e => e.RoleId == id);
        }
    }
}
