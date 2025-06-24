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
    public class AlarmsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILoggerService _loggerService;

        public AlarmsController(AppDbContext context, ILoggerService loggerService)
        {
            _context = context;
            _loggerService = loggerService;
        }

        // GET: api/Alarms
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Alarm>>> GetAlarm()
        {
            try
            {
                var alarms = await _context.Alarm.ToListAsync();
                return alarms;
            }
            catch (Exception)
            {
              
                return StatusCode(500, "Internal server error");
            }
        }


        [HttpGet("CatchWithAlarms")]
        public async Task<ActionResult<IEnumerable<object>>> GetCatchWithAlarms()
        {
            try
            {
                var result = await (
                    from t in _context.Transaction
                    where t.AlarmId != null || t.Remarks != null || t.VoiceRecording != null
                    join p in _context.Projects on t.ProjectId equals p.ProjectId into projGroup
                    from p in projGroup.DefaultIfEmpty()

                    join pr in _context.Processes on t.ProcessId equals pr.Id into procGroup
                    from pr in procGroup.DefaultIfEmpty()

                    join a in _context.Alarm on t.AlarmId equals a.AlarmId.ToString() into alarmGroup
                    from a in alarmGroup.DefaultIfEmpty()

                    join qs in _context.QuantitySheets on t.QuantitysheetId equals qs.QuantitySheetId into qsGroup
                    from qs in qsGroup.DefaultIfEmpty()

                    select new
                    {
                        TransactionId = t.TransactionId,
                        AlarmId = t.AlarmId,
                        Remarks = t.Remarks,
                        VoiceRecording = t.VoiceRecording,
                        ProcessId = t.ProcessId,
                        ProjectId = t.ProjectId,
                        ProjectName = p != null ? p.Name : null,
                        ProcessName = pr != null ? pr.Name : null,
                        AlarmMessage = a != null ? a.Message : null,  // Replace 'Message' with actual column
                        LotNo = t.LotNo,
                        CatchNo = qs != null ? qs.CatchNo : null       // Replace 'CatchNo' with actual column
                    }
                ).ToListAsync();

                if (result == null || !result.Any())
                {
                    return NotFound("No transactions found with alarms or remarks.");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Alarms/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Alarm>> GetAlarm(int id)
        {
            try
            {
                var alarm = await _context.Alarm.FindAsync(id);

                if (alarm == null)
                {
                  
                    return NotFound();
                }

                return alarm;
            }
            catch (Exception)
            {
                return StatusCode(500, "Internal server error");
            }
        }

        // PUT: api/Alarms/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutAlarm(int id, Alarm alarm)
        {
            if (id != alarm.AlarmId)
            {
                return BadRequest();
            }

            // Fetch the existing entity before updating to capture old values
            var existingAlarm = await _context.Alarm.AsNoTracking().FirstOrDefaultAsync(a => a.AlarmId == id);
            if (existingAlarm == null)
            {
                _loggerService.LogEvent($"Alarm with ID {id} not found during update", "Alarms", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                return NotFound();
            }

            // Capture the old and new values for logging
            string oldValue = Newtonsoft.Json.JsonConvert.SerializeObject(existingAlarm);
            string newValue = Newtonsoft.Json.JsonConvert.SerializeObject(alarm);

            _context.Entry(alarm).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                _loggerService.LogEvent($"Updated alarm with ID {id}", "Alarms", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0, oldValue, newValue);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (!AlarmExists(id))
                {
                    _loggerService.LogEvent($"Alarm with ID {id} not found during update", "Alarms", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                    return NotFound();
                }
                else
                {
                    _loggerService.LogError("Concurrency error during alarm update", ex.Message, nameof(AlarmsController));
                    throw;
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error updating alarm", ex.Message, nameof(AlarmsController));
                return StatusCode(500, "Internal server error");
            }

            return NoContent();
        }

        // POST: api/Alarms
        [HttpPost]
        public async Task<ActionResult<Alarm>> PostAlarm(Alarm alarm)
        {
            try
            {
                _context.Alarm.Add(alarm);
                await _context.SaveChangesAsync();
                _loggerService.LogEvent("Created a new alarm", "Alarms", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);

                return CreatedAtAction("GetAlarm", new { id = alarm.AlarmId }, alarm);
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error creating alarm", ex.Message, nameof(AlarmsController));
                return StatusCode(500, "Internal server error");
            }
        }

        // DELETE: api/Alarms/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAlarm(int id)
        {
            try
            {
                var alarm = await _context.Alarm.FindAsync(id);
                if (alarm == null)
                {
                    _loggerService.LogEvent($"Alarm with ID {id} not found during delete", "Alarms", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                    return NotFound();
                }

                _context.Alarm.Remove(alarm);
                await _context.SaveChangesAsync();
                _loggerService.LogEvent($"Deleted alarm with ID {id}", "Alarms", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);

                return NoContent();
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error deleting alarm", ex.Message, nameof(AlarmsController));
                return StatusCode(500, "Internal server error");
            }
        }

        private bool AlarmExists(int id)
        {
            return _context.Alarm.Any(e => e.AlarmId == id);
        }
    }
}
