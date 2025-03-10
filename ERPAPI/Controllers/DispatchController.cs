using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
    public class DispatchController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILoggerService _loggerService;

        public DispatchController(AppDbContext context, ILoggerService loggerService)
        {
            _context = context;
            _loggerService = loggerService;
        }

        // GET: api/Dispatch
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetDispatches()
        {
            try
            {
                var dispatches = await _context.Dispatch.ToListAsync();
                var dispatchesWithDetails = dispatches.Select(dispatch => new
                {
                    dispatch.Id,
                    dispatch.ProcessId,
                    dispatch.ProjectId,
                    dispatch.LotNo,
                    dispatch.BoxCount,
                    dispatch.MessengerName,
                    dispatch.MessengerMobile,
                    dispatch.DispatchMode,
                    dispatch.VehicleNumber,
                    dispatch.DriverName,
                    dispatch.DriverMobile,
                    dispatch.CreatedAt,
                    dispatch.UpdatedAt,
                    dispatch.Status
                }).ToList();

              
                return Ok(dispatchesWithDetails);
            }
            catch (Exception ex)
            {
       
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Dispatch/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Dispatch>> GetDispatch(int id)
        {
            try
            {
                var dispatch = await _context.Dispatch.FindAsync(id);

                if (dispatch == null)
                {
                   
                    return NotFound();
                }

              
                return Ok(dispatch);
            }
            catch (Exception)
            {
              
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Dispatch/project/{projectId}/lot/{lotNo}
        [HttpGet("project/{projectId}/lot/{lotNo}")]
        public async Task<ActionResult<IEnumerable<object>>> GetDispatchByProjectAndLot(int projectId, string lotNo)
        {
            try
            {
                // Fetch the dispatch records based on projectId and lotNo
                var dispatches = await _context.Dispatch
                    .Where(d => d.ProjectId == projectId && d.LotNo == lotNo)
                    .ToListAsync();

                // If no dispatch records found, return NotFound
                if (dispatches == null || !dispatches.Any())
                {
                    
                    return NotFound();
                }

                // Select relevant details to return
                var dispatchesWithDetails = dispatches.Select(dispatch => new
                {
                    dispatch.Id,
                    dispatch.ProcessId,
                    dispatch.ProjectId,
                    dispatch.LotNo,
                    dispatch.BoxCount,
                    dispatch.MessengerName,
                    dispatch.MessengerMobile,
                    dispatch.DispatchMode,
                    dispatch.VehicleNumber,
                    dispatch.DriverName,
                    dispatch.DriverMobile,
                    dispatch.CreatedAt,
                    dispatch.UpdatedAt,
                    dispatch.Status
                }).ToList();

              
                return Ok(dispatchesWithDetails);
            }
            catch (Exception)
            {
            
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Dispatch/project/{projectId}/lot/{lotNo}
        [HttpGet("project/{projectId}")]
        public async Task<ActionResult<IEnumerable<object>>> GetDispatchByProject(int projectId)
        {
            try
            {
                // Fetch the dispatch records based on projectId and lotNo
                var dispatches = await _context.Dispatch
                    .Where(d => d.ProjectId == projectId)
                    .ToListAsync();

                // If no dispatch records found, return NotFound
                if (dispatches == null || !dispatches.Any())
                {

                    return NotFound();
                }

                // Select relevant details to return
                var dispatchesWithDetails = dispatches.Select(dispatch => new
                {
                    dispatch.Id,
                    dispatch.ProcessId,
                    dispatch.ProjectId,
                    dispatch.LotNo,
                    dispatch.BoxCount,
                    dispatch.MessengerName,
                    dispatch.MessengerMobile,
                    dispatch.DispatchMode,
                    dispatch.VehicleNumber,
                    dispatch.DriverName,
                    dispatch.DriverMobile,
                    dispatch.CreatedAt,
                    dispatch.UpdatedAt,
                    dispatch.Status
                }).ToList();


                return Ok(dispatchesWithDetails);
            }
            catch (Exception)
            {

                return StatusCode(500, "Internal server error");
            }
        }

        // PUT: api/Dispatch/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutDispatch(int id, Dispatch dispatch)
        {
            if (id != dispatch.Id)
            {
                return BadRequest();
            }

            _context.Entry(dispatch).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                _loggerService.LogEvent($"Updated dispatch with ID {id}", "Dispatch", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DispatchExists(id))
                {
                    _loggerService.LogEvent($"Dispatch with ID {id} not found during update", "Dispatch", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                    return NotFound();
                }
                else
                {
                    _loggerService.LogError("Concurrency error during dispatch update", "Dispatch", nameof(DispatchController));
                    throw;
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error updating dispatch", ex.Message, nameof(DispatchController));
                return StatusCode(500, "Internal server error");
            }

            return NoContent();
        }

        // POST: api/Dispatch
        [HttpPost]
        public async Task<ActionResult<Dispatch>> PostDispatch(Dispatch dispatch)
        {
            try
            {
                _context.Dispatch.Add(dispatch);
                await _context.SaveChangesAsync();

                _loggerService.LogEvent("Created a new dispatch", "Dispatch", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                return CreatedAtAction("GetDispatch", new { id = dispatch.Id }, dispatch);
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error creating dispatch", ex.Message, nameof(DispatchController));
                return StatusCode(500, "Internal server error");
            }
        }

        // DELETE: api/Dispatch/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDispatch(int id)
        {
            try
            {
                var dispatch = await _context.Dispatch.FindAsync(id);
                if (dispatch == null)
                {
                    _loggerService.LogEvent($"Dispatch with ID {id} not found during delete", "Dispatch", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                    return NotFound();
                }

                _context.Dispatch.Remove(dispatch);
                await _context.SaveChangesAsync();

                _loggerService.LogEvent($"Deleted dispatch with ID {id}", "Dispatch", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                return NoContent();
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error deleting dispatch", ex.Message, nameof(DispatchController));
                return StatusCode(500, "Internal server error");
            }
        }

        private bool DispatchExists(int id)
        {
            return _context.Dispatch.Any(e => e.Id == id);
        }
    }
}
