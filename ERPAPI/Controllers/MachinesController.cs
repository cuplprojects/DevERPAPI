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
    public class MachinesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILoggerService _loggerService;

        public MachinesController(AppDbContext context, ILoggerService loggerService)
        {
            _context = context;
            _loggerService = loggerService;
        }

        // GET: api/Machines
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetMachines()
        {
            try
            {
                var machinesWithProcesses = await (from m in _context.Machine
                                                   join p in _context.Processes on m.ProcessId equals p.Id
                                                   select new
                                                   {
                                                       m.MachineId,
                                                       m.MachineName,
                                                       m.Status,
                                                       m.ProcessId,
                                                       ProcessName = p.Name
                                                   }).ToListAsync();
                return Ok(machinesWithProcesses);
            }
            catch (Exception)
            {
              
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Machines/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Machine>> GetMachine(int id)
        {
            try
            {
                var machine = await _context.Machine.FindAsync(id);
                if (machine == null)
                {
                  
                    return NotFound();
                }
                return machine;
            }
            catch (Exception)
            {
               
                return StatusCode(500, "Internal server error");
            }
        }

        // PUT: api/Machines/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutMachine(int id, Machine machine)
        {
            if (id != machine.MachineId)
            {
                return BadRequest();
            }

            // Fetch existing entity to capture old values
            var existingMachine = await _context.Machine.AsNoTracking().FirstOrDefaultAsync(m => m.MachineId == id);
            if (existingMachine == null)
            {
                _loggerService.LogEvent($"Machine with ID {id} not found during update", "Machines",
                    User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                return NotFound();
            }

            // Check for duplicate machine name
            var duplicateMachine = await _context.Machine
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MachineName == machine.MachineName && m.MachineId != id);
            if (duplicateMachine != null)
            {
                return BadRequest("A machine with the same name already exists.");
            }

            // Capture old and new values for logging
            string oldValue = Newtonsoft.Json.JsonConvert.SerializeObject(existingMachine);
            string newValue = Newtonsoft.Json.JsonConvert.SerializeObject(machine);

            _context.Entry(machine).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                _loggerService.LogEvent($"Updated machine with ID {id}", "Machines",
                    User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0, oldValue, newValue);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (!MachineExists(id))
                {
                    _loggerService.LogEvent($"Machine with ID {id} not found during update", "Machines",
                        User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                    return NotFound();
                }
                else
                {
                    _loggerService.LogError("Concurrency error during machine update", ex.Message, nameof(MachinesController));
                    throw;
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error updating machine", ex.Message, nameof(MachinesController));
                return StatusCode(500, "Internal server error");
            }

            return NoContent();
        }


        // POST: api/Machines
        [HttpPost]
        public async Task<ActionResult<Machine>> PostMachine(Machine machine)
        {
            try
            {
                // Check for duplicate machine name
                var existingMachine = await _context.Machine
                    .FirstOrDefaultAsync(m => m.MachineName == machine.MachineName);
                if (existingMachine != null)
                {
                    return BadRequest("A machine with the same name already exists.");
                }

                _context.Machine.Add(machine);
                await _context.SaveChangesAsync();

                _loggerService.LogEvent("Created a new machine", "Machines",
                    User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);

                return CreatedAtAction("GetMachine", new { id = machine.MachineId }, machine);
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error creating machine", ex.Message, nameof(MachinesController));
                return StatusCode(500, "Internal server error");
            }
        }


        // DELETE: api/Machines/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMachine(int id)
        {
            try
            {
                var machine = await _context.Machine.FindAsync(id);
                if (machine == null)
                {
                    _loggerService.LogEvent($"Machine with ID {id} not found during delete", "Machines", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                    return NotFound();
                }

                _context.Machine.Remove(machine);
                await _context.SaveChangesAsync();
                _loggerService.LogEvent($"Deleted machine with ID {id}", "Machines", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);

                return NoContent();
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error deleting machine", ex.Message, nameof(MachinesController));
                return StatusCode(500, "Internal server error");
            }
        }

        private bool MachineExists(int id)
        {
            return _context.Machine.Any(e => e.MachineId == id);
        }
    }
}
