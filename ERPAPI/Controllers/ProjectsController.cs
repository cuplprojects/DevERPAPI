using ERPAPI.Data;
using ERPAPI.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace ERPAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProjectController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProjectController(AppDbContext context)
        {
            _context = context;
        }


        [HttpGet("GetProjectProcesses")]
        public async Task<ActionResult<IEnumerable<ProjectProcess>>> GetProjectProcesses()
        {
            var projectProcesses = await _context.ProjectProcesses
                .Select(pp => new
                {
                    pp.Id,
                    pp.ProjectId,
                    pp.ProcessId,
                    pp.Weightage,
                    pp.Sequence,
                    UserIds = pp.UserId // Use the updated UserIds
                })
                .ToListAsync();

            return Ok(projectProcesses);
        }



        [HttpGet]
        public async Task<ActionResult<IEnumerable<Project>>> GetProject()
        {
            return await _context.Projects.OrderByDescending(p => p.ProjectId).ToListAsync();
        }



        [HttpGet("{id}")]
        public async Task<ActionResult<Project>> GetProjectById(int id)
        {
            var projectWithType = await (from m in _context.Projects
                                         join p in _context.Types on m.TypeId equals p.TypeId
                                         where m.ProjectId == id // Filter by the provided id
                                         select new
                                         {
                                             m.ProjectId,
                                             m.TypeId,
                                             m.Status,
                                             m.GroupId,
                                             m.Name,
                                             m.Description,
                                             m.NoOfSeries,
                                             m.SeriesName,
                                             m.ExamTypeId,
                                             ProjectType = p.Types
                                         }).FirstOrDefaultAsync(); // Use FirstOrDefault to get a single project

            if (projectWithType == null)
            {
                return NotFound(); // Return 404 if not found
            }

            return Ok(projectWithType); // Return the found project
        }



        [HttpPost]
        public async Task<ActionResult<Project>> PostProject([FromBody] Project project)
        {
            // Check for duplicate project name
            var existingProject = await _context.Projects
                .FirstOrDefaultAsync(p => p.Name == project.Name);
            if (existingProject != null)
            {
                return BadRequest("A project with the same name already exists.");
            }

            // Check if project type is Booklets and series is not provided
            var projectType = await _context.Types
                .Where(t => t.TypeId == project.TypeId)
                .Select(t => t.Types)
                .FirstOrDefaultAsync();

            if (projectType == "Booklets" && (!project.NoOfSeries.HasValue || string.IsNullOrEmpty(project.SeriesName)))
            {
                return BadRequest("NoOfSeries and SeriesName are required for Booklet type projects");
            }

            // Set the Date to the current date
            project.Date = DateTime.Now;

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetProject", new { id = project.ProjectId }, project);
        }






        [HttpPut("{id}")]
        public async Task<IActionResult> PutProject(int id, Project project)
        {
            if (id != project.ProjectId)
            {
                return BadRequest();
            }

            // Check if project type is Booklets and series is not provided
            var projectType = await _context.Types
                .Where(t => t.TypeId == project.TypeId)
                .Select(t => t.Types)
                .FirstOrDefaultAsync();

            if (projectType == "Booklets" && (!project.NoOfSeries.HasValue || string.IsNullOrEmpty(project.SeriesName)))
            {
                return BadRequest("NoOfSeries and SeriesName are required for Booklet type projects");
            }

            _context.Entry(project).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProjectExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }


        private bool ProjectExists(int id)
        {
            return _context.Projects.Any(e => e.ProjectId == id);
        }

        // GET: api/Project/GetActiveProjects
        [HttpGet("GetActiveProjects")]
        public async Task<ActionResult<IEnumerable<Project>>> GetActiveProjects()
        {
            var activeProjects = await _context.Projects
                .Where(p => p.Status == true) // Filtering projects with Status = true
                .ToListAsync();

            if (activeProjects == null || !activeProjects.Any())
            {
                return NotFound("No active projects found.");
            }

            return Ok(activeProjects);
        }



        [Authorize]
        [HttpGet("GetDistinctProjectsForUser/{userId}")]
        public async Task<ActionResult<IEnumerable<Project>>> GetDistinctProjectsForUser(int userId)
        {
            // Fetch the user to check their RoleId
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            var ongoingProjects = await _context.QuantitySheets
         .Where(q => q.Status == 1) // Filter for ongoing projects (Status == 1)
         .GroupBy(p => p.ProjectId)  // Group by ProjectId
         .Select(group => new { ProjectId = group.Key })
         .ToListAsync();

            // Check the RoleId and act accordingly
            if (user.RoleId < 5)
            {
                // Get all active projects if the RoleId is 1, 2, 3, or 4
                var activeProjects = await _context.Projects
                    .Where(p => p.Status == true) // Assuming "Status" indicates active projects
                    .OrderByDescending(p => p.ProjectId)
                    .ToListAsync();

                return Ok(activeProjects);
            }
            else
            {
                var projectProcesses = await _context.ProjectProcesses
           .AsNoTracking()
           .ToListAsync();

                var userAssignedProcesses = projectProcesses
          .Where(pp => pp.UserId.Contains(userId)) // Client-side filtering
          .Select(pp => pp.ProjectId) // Select the project IDs
          .Distinct() // Ensure distinct project IDs
          .ToList();
                // Fetch all project processes that contain the userId in the UserId list
                var ongoingProjectIds = ongoingProjects.Select(op => op.ProjectId).ToList();

                // Filter the userAssignedProcesses to include only ongoing projects
                var userAssignedOngoingProjects = userAssignedProcesses
                    .Where(up => ongoingProjectIds.Contains(up))
                    .ToList();

                // If no ongoing projects are assigned to the user, return 404
                if (!userAssignedOngoingProjects.Any())
                {
                    return NotFound("No ongoing projects found for this user.");
                }

                // Fetch the project details for the distinct ongoing project IDs assigned to the user
                var projects = await _context.Projects
                    .Where(p => userAssignedOngoingProjects.Contains(p.ProjectId))
                    .OrderByDescending(p => p.ProjectId) // Order by LastReleasedLotDate in descending order
                    .ToListAsync();


                return Ok(projects);
            }
        }


        //[HttpGet("GetDistinctProjectsForUser/{userId}")]
        //public async Task<ActionResult<IEnumerable<Project>>> GetDistinctProjectsForUser(int userId)
        //{
        //    // Fetch all project processes that contain the userId in the UserId list
        //    var projectProcesses = await _context.ProjectProcesses
        //        .AsNoTracking() // Optional: For read-only operations
        //        .ToListAsync();

        //    // Filter for processes where the UserId list contains the userId
        //    var userAssignedProcesses = projectProcesses
        //        .Where(pp => pp.UserId.Contains(userId)) // Client-side filtering
        //        .Select(pp => pp.ProjectId) // Select the project IDs
        //        .Distinct() // Ensure distinct project IDs
        //        .ToList();

        //    // If no project IDs are found, return 404
        //    if (!userAssignedProcesses.Any())
        //    {
        //        return NotFound("No projects found for this user.");
        //    }

        //    // Now fetch the project details for the distinct project IDs
        //    var projects = await _context.Projects
        //        .Where(p => userAssignedProcesses.Contains(p.ProjectId)) // Filter projects by distinct IDs
        //        .ToListAsync();

        //    return Ok(projects);
        //}





    }

}
