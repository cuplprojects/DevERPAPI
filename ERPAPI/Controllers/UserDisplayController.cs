using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ERPAPI.Data;
using ERPAPI.Model;
using Microsoft.AspNetCore.Authorization;

namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserDisplayController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UserDisplayController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/UserDisplay
        // [Authorize]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserDisplay>>> GetAllUserDisplays()
        {
            try
            {
                var userDisplays = await _context.UserDisplays.ToListAsync();
                return Ok(userDisplays);
            }
            catch (Exception)
            {
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/UserDisplay/user/5
        // [Authorize]
        [HttpGet("user/{userId}")]
        public async Task<ActionResult<IEnumerable<UserDisplay>>> GetUserDisplays(int userId)
        {
            try
            {
                var userDisplays = await _context.UserDisplays
                    .Where(ud => ud.UserId == userId)
                    .ToListAsync();

                if (!userDisplays.Any())
                {
                    return NotFound($"No displays found for user with ID {userId}");
                }

                return Ok(userDisplays);
            }
            catch (Exception)
            {
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/UserDisplay
        // [Authorize]
        [HttpPost]
        public async Task<ActionResult<UserDisplay>> AssignDisplayToUser(UserDisplay userDisplay)
        {
            try
            {
                // Check if user exists
                var userExists = await _context.Users.AnyAsync(u => u.UserId == userDisplay.UserId);
                if (!userExists)
                {
                    return BadRequest("Invalid User ID");
                }

                // Check if display exists
                var displayExists = await _context.Displays.AnyAsync(d => d.DisplayId == userDisplay.DisplayId);
                if (!displayExists)
                {
                    return BadRequest("Invalid Display ID");
                }

                // Check if assignment already exists
                var existingAssignment = await _context.UserDisplays
                    .FirstOrDefaultAsync(ud => ud.UserId == userDisplay.UserId && ud.DisplayId == userDisplay.DisplayId);

                if (existingAssignment != null)
                {
                    return Conflict("This user-display assignment already exists");
                }

                _context.UserDisplays.Add(userDisplay);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetUserDisplays), new { userId = userDisplay.UserId }, userDisplay);
            }
            catch (Exception)
            {
                return StatusCode(500, "Internal server error");
            }
        }

        // PUT: api/UserDisplay/5/3
        // [Authorize]
        [HttpPut("{userId}/{oldDisplayId}")]
        public async Task<IActionResult> UpdateUserDisplay(int userId, int oldDisplayId, UserDisplay newUserDisplay)
        {
            try
            {
                if (userId != newUserDisplay.UserId)
                {
                    return BadRequest("User ID mismatch");
                }

                var existingAssignment = await _context.UserDisplays
                    .FirstOrDefaultAsync(ud => ud.UserId == userId && ud.DisplayId == oldDisplayId);

                if (existingAssignment == null)
                {
                    return NotFound("User-display assignment not found");
                }

                // Check if user exists
                var userExists = await _context.Users.AnyAsync(u => u.UserId == userId);
                if (!userExists)
                {
                    return BadRequest("Invalid User ID");
                }

                // Check if new display exists
                var displayExists = await _context.Displays.AnyAsync(d => d.DisplayId == newUserDisplay.DisplayId);
                if (!displayExists)
                {
                    return BadRequest("Invalid Display ID");
                }

                // Check if the new assignment would create a duplicate
                if (oldDisplayId != newUserDisplay.DisplayId)
                {
                    var duplicateExists = await _context.UserDisplays
                        .AnyAsync(ud => ud.UserId == userId && ud.DisplayId == newUserDisplay.DisplayId);
                    
                    if (duplicateExists)
                    {
                        return Conflict("This user-display assignment already exists");
                    }
                }

                existingAssignment.DisplayId = newUserDisplay.DisplayId;
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception)
            {
                return StatusCode(500, "Internal server error");
            }
        }

        // DELETE: api/UserDisplay/5/3
        // [Authorize]
        [HttpDelete("{userId}/{displayId}")]
        public async Task<IActionResult> DeleteUserDisplay(int userId, int displayId)
        {
            try
            {
                var userDisplay = await _context.UserDisplays
                    .FirstOrDefaultAsync(ud => ud.UserId == userId && ud.DisplayId == displayId);

                if (userDisplay == null)
                {
                    return NotFound("User-display assignment not found");
                }

                _context.UserDisplays.Remove(userDisplay);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception)
            {
                return StatusCode(500, "Internal server error");
            }
        }

        // DELETE: api/UserDisplay/user/5
        // [Authorize]
        [HttpDelete("user/{userId}")]
        public async Task<IActionResult> DeleteAllUserDisplays(int userId)
        {
            try
            {
                var userDisplays = await _context.UserDisplays
                    .Where(ud => ud.UserId == userId)
                    .ToListAsync();

                if (!userDisplays.Any())
                {
                    return NotFound($"No displays found for user with ID {userId}");
                }

                _context.UserDisplays.RemoveRange(userDisplays);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception)
            {
                return StatusCode(500, "Internal server error");
            }
        }
    }
} 