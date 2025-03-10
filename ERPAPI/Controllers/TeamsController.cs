using Microsoft.AspNetCore.Mvc;
using ERPAPI.Data;
using ERPAPI.Model;

using Microsoft.EntityFrameworkCore;
using ERPAPI.Service;
using System;
using System.Collections.Generic;

using System.Linq;

using System.Threading.Tasks;

namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TeamsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILoggerService _loggerService;

        public TeamsController(AppDbContext context, ILoggerService loggerService)
        {
            _context = context;
            _loggerService = loggerService;
        }

        // GET: api/Teams
        [HttpGet]

        public async Task<ActionResult<IEnumerable<object>>> GetTeams()

        {
            try
            {
                var teams = await _context.Teams.ToListAsync();


                // Collect all unique user IDs from the teams
                var userIds = teams.SelectMany(t => t.UserIds).Distinct().ToList();

                // Fetch user details for the collected user IDs
                var users = await _context.Users
                    .Where(u => userIds.Contains(u.UserId))
                    .Select(u => new

                    {
                        u.UserId,
                        FullName = $"{u.FirstName} {(string.IsNullOrEmpty(u.MiddleName) ? "" : u.MiddleName + " ")}{(string.IsNullOrEmpty(u.LastName) ? "" : u.LastName)}".Trim()
                    })
                    .ToListAsync();

                // Create a dictionary for quick lookup of user full names
                var userMap = users.ToDictionary(u => u.UserId, u => u.FullName);

                // Update teams to include full names of users
                var updatedTeams = teams.Select(team => new
                {
                    team.TeamId,
                    team.TeamName,
                    team.CreatedDate,
                    team.Status,
                    team.CreatedBy,
                    team.ProcessId,
                    Users = team.UserIds.Select(id => new
                    {
                        Id = id,
                        Name = userMap.ContainsKey(id) ? userMap[id] : "Unknown"

                    }).ToList()
                });

                return Ok(updatedTeams);
            }
            catch (Exception)
            {
                
                return StatusCode(500, "Failed to retrieve teams");
            }
        }


        // GET: api/Teams/5
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetTeam(int id)
        {
            try
            {
                var team = await _context.Teams.FirstOrDefaultAsync(t => t.TeamId == id);


                if (team == null)
                {
                    return NotFound(new { Message = "Team not found" });
                }

                // Map user IDs to user details
                var userIds = team.UserIds;
                var users = await _context.Users
                    .Where(u => userIds.Contains(u.UserId))
                    .Select(u => new
                    {
                        u.UserId,
                        FullName = $"{u.FirstName} {(string.IsNullOrEmpty(u.MiddleName) ? "" : u.MiddleName + " ")}{(string.IsNullOrEmpty(u.LastName) ? "" : u.LastName)}".Trim()
                    })
                    .ToListAsync();

                var updatedTeam = new
                {
                    team.TeamId,
                    team.TeamName,
                    team.CreatedDate,
                    team.ProcessId,
                    team.Status,
                    Users = users
                };

                return Ok(updatedTeam);

            }
            catch (Exception ex)
            {
               
                return StatusCode(500, "Failed to retrieve team");
            }
        }

        // GET: api/Teams/Process/{processId}
        [HttpGet("Process/{processId}")]
        public async Task<ActionResult<IEnumerable<object>>> GetTeamsByProcessId(int processId)
        {
            try
            {
                var teams = await _context.Teams
                    .Where(t => t.ProcessId == processId)
                    .ToListAsync();

                if (!teams.Any())
                {
                    return NotFound(new { Message = "No teams found for this process ID" });
                }

                // Collect all unique user IDs from the teams
                var userIds = teams.SelectMany(t => t.UserIds).Distinct().ToList();

                // Fetch user details for the collected user IDs
                var users = await _context.Users
                    .Where(u => userIds.Contains(u.UserId))
                    .Select(u => new
                    {
                        u.UserId,
                        FullName = $"{u.FirstName} {(string.IsNullOrEmpty(u.MiddleName) ? "" : u.MiddleName + " ")}{(string.IsNullOrEmpty(u.LastName) ? "" : u.LastName)}".Trim()
                    })
                    .ToListAsync();

                // Create a dictionary for quick lookup of user full names
                var userMap = users.ToDictionary(u => u.UserId, u => u.FullName);

                // Update teams to include full names of users
                var updatedTeams = teams.Select(team => new
                {
                    team.TeamId,
                    team.TeamName,
                    team.CreatedDate,
                    team.Status,
                    team.ProcessId,
                    Users = team.UserIds.Select(id => new
                    {
                        UserId = id,
                        FullName = userMap.ContainsKey(id) ? userMap[id] : "Unknown"
                    }).ToList()
                });

                return Ok(updatedTeams);
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Failed to retrieve teams by process ID", ex.Message, "TeamsController");
                return StatusCode(500, "Failed to retrieve teams by process ID");
            }
        }



        [HttpPost]
        public async Task<ActionResult<Team>> PostTeam(Team team)
        {
            try
            {
                // Normalize user IDs by ordering them
                var normalizedUserIds = team.UserIds.OrderBy(id => id).ToList();

                // Check if a team with the same name already exists for the given process
                bool nameExists = await _context.Teams
                    .AnyAsync(t => t.TeamName == team.TeamName && t.ProcessId == team.ProcessId);

                if (nameExists)
                {
                    var conflictMessage = $"Conflict: Team with the same name '{team.TeamName}' and process ID '{team.ProcessId}' already exists.";
                    Console.WriteLine(conflictMessage);
                    _loggerService.LogError("Team creation conflict", conflictMessage, "TeamsController");
                    return Conflict(new { Message = "A team with the same name and process ID already exists." });
                }

                // Check if a team with the same members and process ID already exists, regardless of the team name
                var existingTeams = await _context.Teams
                    .Where(t => t.ProcessId == team.ProcessId)
                    .ToListAsync();

                bool sameMembersExist = existingTeams.Any(t =>
                    t.UserIds.OrderBy(id => id).SequenceEqual(normalizedUserIds));

                if (sameMembersExist)
                {
                    var conflictMessage = $"Conflict: A team with the same members and process ID '{team.ProcessId}' already exists.";
                    Console.WriteLine(conflictMessage);
                    _loggerService.LogError("Team creation conflict", conflictMessage, "TeamsController");
                    return Conflict(new { Message = "A team with the same members and process ID already exists." });
                }

                // Add the new team if no conflicts are found
                _context.Teams.Add(team);
                await _context.SaveChangesAsync();

                // Log the successful team creation
                var logMessage = $"Team created successfully with ID: {team.TeamId}, Name: {team.TeamName}, Process ID: {team.ProcessId}, Members: {string.Join(", ", team.UserIds)}";
                Console.WriteLine(logMessage);
                _loggerService.LogEvent(
                    "Team created",
                    "Teams",
                    team.TeamId,
                    oldValue: null,
                    newValue: $"Name: '{team.TeamName}', Process ID: '{team.ProcessId}', Members: '{string.Join(", ", team.UserIds)}', Status: '{team.Status}'"
                );

                return CreatedAtAction(nameof(GetTeam), new { id = team.TeamId }, team);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to create team: " + ex.ToString());
                _loggerService.LogError("Failed to create team", ex.Message, "TeamsController");
                return StatusCode(500, "Failed to create team");
            }
        }




        // PUT: api/Teams/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTeam(int id, Team team)
        {
            if (id != team.TeamId)
            {
                return BadRequest(new { Message = "Invalid Team ID" });
            }

            try
            {
                // Find the existing team by ID
                var existingTeam = await _context.Teams.FindAsync(id);
                if (existingTeam == null)
                {
                    return NotFound(new { Message = "Team not found" });
                }

                // Normalize user IDs by ordering them
                var normalizedUserIds = team.UserIds.OrderBy(uid => uid).ToList();

                // Check if another team with the same members and process ID exists, but with a different team name
                var duplicateTeam = await _context.Teams
                    .Where(t => t.TeamId != id && t.ProcessId == team.ProcessId)
                    .ToListAsync();

                bool sameMembersExist = duplicateTeam.Any(t =>
                    t.UserIds.OrderBy(uid => uid).SequenceEqual(normalizedUserIds));

                if (sameMembersExist)
                {
                    return Conflict(new { Message = "A team with the same members and process ID already exists." });
                }

                // Initialize old and new value variables
                string oldValue = string.Empty;
                string newValue = string.Empty;

                if (existingTeam.TeamName != team.TeamName)
                {
                    oldValue += $" '{existingTeam.TeamName}'; ";
                    newValue += $" '{team.TeamName}'; ";
                }
                if (existingTeam.ProcessId != team.ProcessId)
                {
                    oldValue += $" '{existingTeam.ProcessId}'; ";
                    newValue += $" '{team.ProcessId}'; ";
                }
                if (existingTeam.Status != team.Status)
                {
                    oldValue += $" '{existingTeam.Status}'; ";
                    newValue += $" '{team.Status}'; ";
                }
                if (!existingTeam.UserIds.OrderBy(uid => uid).SequenceEqual(normalizedUserIds))
                {
                    oldValue += $" '{string.Join(", ", existingTeam.UserIds)}'; ";
                    newValue += $" '{string.Join(", ", team.UserIds)}'; ";
                }

                // Update the existing team with new values
                existingTeam.TeamName = team.TeamName;
                existingTeam.ProcessId = team.ProcessId;
                existingTeam.Status = team.Status;
                existingTeam.UserIds = team.UserIds;

                await _context.SaveChangesAsync();

                // Log the changes
                if (!string.IsNullOrEmpty(oldValue) || !string.IsNullOrEmpty(newValue))
                {
                    _loggerService.LogEvent(
                        "Team updated",
                        "Teams",
                        id,
                        oldValue: oldValue.TrimEnd(' ', ';'),
                        newValue: newValue.TrimEnd(' ', ';')
                    );
                }

                return Ok(new { Message = "Team updated successfully" });
            }
            catch (Exception ex)
            {
                _loggerService.LogError(
                    "Failed to update team",
                    ex.Message,
                    "TeamsController"
                );
                return StatusCode(500, "Failed to update team");
            }
        }



        // DELETE: api/Teams/5
        [HttpDelete("{id}")]
     
        public async Task<IActionResult> DeleteTeam(int id)
        {
            try
            {
                var team = await _context.Teams.FindAsync(id);
                if (team == null)
                {
                    return NotFound(new { Message = "Team not found" });
                }

                // Capture details of the team to log before deleting
                var oldTeam = new
                {
                    team.TeamId,
                    team.TeamName,
                    team.Status,
                    UserIds = string.Join(", ", team.UserIds)
                };

                _context.Teams.Remove(team);
                await _context.SaveChangesAsync();

                // Log the deletion with the old value
                _loggerService.LogEvent(
                    "Team deleted",
                    "Teams",
                    id,
                    oldValue: System.Text.Json.JsonSerializer.Serialize(oldTeam)
                );

                return Ok(new { Message = "Team deleted successfully" });
            }
            catch (Exception ex)
            {
                _loggerService.LogError(
                    "Failed to delete team",
                    ex.Message,
                    "TeamsController"
                );
                return StatusCode(500, "Failed to delete team");
            }
        }
    }
}
