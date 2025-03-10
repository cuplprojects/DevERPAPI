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
    public class ZonesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILoggerService _loggerService;

        public ZonesController(AppDbContext context, ILoggerService loggerService)
        {
            _context = context;
            _loggerService = loggerService;
        }

        // GET: api/Zones
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetZones()
        {
            try
            {
                var zones = await _context.Zone.ToListAsync();
                var machines = await _context.Machine.ToListAsync();
                var cameras = await _context.Camera.ToListAsync();

                var machineDictionary = machines.ToDictionary(m => m.MachineId, m => m.MachineName);
                var cameraDictionary = cameras.ToDictionary(c => c.CameraId, c => c.Name);

                var zonesWithNames = zones.Select(zone => new
                {
                    zone.ZoneId,
                    zone.ZoneNo,
                    zone.ZoneDescription,
                    zone.CameraIds,
                    zone.MachineId,
                    MachineNames = zone.MachineId
                        .Select(machineId => machineDictionary.TryGetValue(machineId, out var machineName) ? machineName : "Unknown Machine")
                        .ToList(),
                    CameraNames = zone.CameraIds
                        .Select(cameraId => cameraDictionary.TryGetValue(cameraId, out var cameraName) ? cameraName : "Unknown Camera")
                        .ToList()
                }).ToList();

                return Ok(zonesWithNames);
            }
            catch (Exception ex)
            {
           
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Zones/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Zone>> GetZone(int id)
        {
            try
            {
                var zone = await _context.Zone.FindAsync(id);

                if (zone == null)
                {
                   
                    return NotFound();
                }

                return zone;
            }
            catch (Exception ex)
            {
               
                return StatusCode(500, "Internal server error");
            }
        }

        // PUT: api/Zones/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutZone(int id, Zone zone)
        {
            if (id != zone.ZoneId)
            {
                return BadRequest();
            }

            var existingZone = await _context.Zone.AsNoTracking().FirstOrDefaultAsync(z => z.ZoneId == id);
            if (existingZone == null)
            {
                _loggerService.LogEvent($"Zone with ID {id} not found during update", "Zones", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                return NotFound();
            }

            // Capture the old and new values for logging
            string oldValue = Newtonsoft.Json.JsonConvert.SerializeObject(existingZone);
            string newValue = Newtonsoft.Json.JsonConvert.SerializeObject(zone);

            _context.Entry(zone).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                _loggerService.LogEvent($"Updated zone with ID {id}", "Zones", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0, oldValue, newValue);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (!ZoneExists(id))
                {
                    _loggerService.LogEvent($"Zone with ID {id} not found during update", "Zones", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                    return NotFound();
                }
                else
                {
                    _loggerService.LogError("Concurrency error during zone update", ex.Message, nameof(ZonesController));
                    throw;
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error updating zone", ex.Message, nameof(ZonesController));
                return StatusCode(500, "Internal server error");
            }

            return NoContent();
        }

        // POST: api/Zones
        [HttpPost]
        public async Task<ActionResult<Zone>> PostZone(Zone zone)
        {
            try
            {
                _context.Zone.Add(zone);
                await _context.SaveChangesAsync();
                _loggerService.LogEvent("Created a new zone", "Zones", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);

                return CreatedAtAction("GetZone", new { id = zone.ZoneId }, zone);
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error creating zone", ex.Message, nameof(ZonesController));
                return StatusCode(500, "Internal server error");
            }
        }

        // DELETE: api/Zones/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteZone(int id)
        {
            try
            {
                var zone = await _context.Zone.FindAsync(id);
                if (zone == null)
                {
                    _loggerService.LogEvent($"Zone with ID {id} not found during delete", "Zones", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                    return NotFound();
                }

                _context.Zone.Remove(zone);
                await _context.SaveChangesAsync();
                _loggerService.LogEvent($"Deleted zone with ID {id}", "Zones", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);

                return NoContent();
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error deleting zone", ex.Message, nameof(ZonesController));
                return StatusCode(500, "Internal server error");
            }
        }

        private bool ZoneExists(int id)
        {
            return _context.Zone.Any(e => e.ZoneId == id);
        }
    }
}
