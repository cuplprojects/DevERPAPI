using ERPAPI.Data;
using ERPAPI.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LocationController : ControllerBase
    {
        private readonly AppDbContext _context;

        public LocationController(AppDbContext context)
        {
            _context = context;
        }

        // POST: api/location
        [HttpPost]
        public async Task<IActionResult> Createlocation([FromBody] Location location)
        {
            if (location == null)
                return BadRequest("Invalid location data.");

            _context.Locations.Add(location);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetlocationById), new { id = location.LocationId }, location);
        }

        // PUT: api/location/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Updatelocation(int id, [FromBody] Location updatedlocation)
        {
            if (updatedlocation == null || id != updatedlocation.LocationId)
                return BadRequest("Invalid location data or location ID mismatch.");

            var existinglocation = await _context.Locations.FindAsync(id);
            if (existinglocation == null)
                return NotFound($"No location found with ID = {id}.");

            // Update fields
            existinglocation.LocationName = updatedlocation.LocationName;
            try
            {
                await _context.SaveChangesAsync();
                return Ok(new
                {
                    Message = "location updated successfully.",
                    Data = existinglocation
                });
            }
            catch (DbUpdateException ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }


        // DELETE: api/location/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Deletelocation(int id)
        {
            var location = await _context.Locations.FindAsync(id);
            if (location == null)
                return NotFound("location not found.");

            _context.Locations.Remove(location);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/location/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetlocationById(int id)
        {
            var location = await _context.Locations.FindAsync(id);
            if (location == null)
                return NotFound("location not found.");

            return Ok(location);
        }

        // GET: api/location
        [HttpGet]
        public async Task<IActionResult> GetAlllocations()
        {
            var locations = await _context.Locations.ToListAsync();
            return Ok(locations);
        }
    }
}

