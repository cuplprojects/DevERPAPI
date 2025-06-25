using ERPAPI.Data;
using ERPAPI.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DailyTaskController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DailyTaskController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/DailyTask?date=2025-05-26
        [HttpGet]
        public async Task<IActionResult> GetTasksByDate([FromQuery] DateTime date)
        {
            var tasks = await _context.DailyTasks
                .Where(t => t.TaskDate.Date == date.Date)
                .ToListAsync();

            return Ok(tasks);
        }

        // POST: api/DailyTask
        [HttpPost]
        public async Task<IActionResult> AddTask([FromBody] DailyTask task)
        {
            _context.DailyTasks.Add(task);
            await _context.SaveChangesAsync();
            return Ok(task);
        }

        // PUT: api/DailyTask/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTask(int id, [FromBody] DailyTask updatedTask)
        {
            var task = await _context.DailyTasks.FindAsync(id);
            if (task == null)
                return NotFound();

            task.TaskName = updatedTask.TaskName;
            task.TaskDate = updatedTask.TaskDate;
            task.Status = updatedTask.Status;

            await _context.SaveChangesAsync();
            return Ok(task);
        }

        // DELETE: api/DailyTask/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTask(int id)
        {
            var task = await _context.DailyTasks.FindAsync(id);
            if (task == null)
                return NotFound();

            _context.DailyTasks.Remove(task);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
