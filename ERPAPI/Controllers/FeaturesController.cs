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
    public class FeaturesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILoggerService _loggerService;

        public FeaturesController(AppDbContext context, ILoggerService loggerService)
        {
            _context = context;
            _loggerService = loggerService;
        }

        // GET: api/Features
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Feature>>> GetFeatures()
        {
            try
            {
                var features = await _context.Features.ToListAsync();
                return features;
            }
            catch (Exception ex)
            {
                
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Features/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Feature>> GetFeature(int id)
        {
            try
            {
                var feature = await _context.Features.FindAsync(id);

                if (feature == null)
                {
                 
                    return NotFound();
                }

                return feature;
            }
            catch (Exception ex)
            {
             
                return StatusCode(500, "Internal server error");
            }
        }

        // PUT: api/Features/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutFeature(int id, Feature feature)
        {
            if (id != feature.FeatureId)
            {
                return BadRequest();
            }

            // Fetch existing entity to capture old values
            var existingFeature = await _context.Features.AsNoTracking().FirstOrDefaultAsync(f => f.FeatureId == id);
            if (existingFeature == null)
            {
                _loggerService.LogEvent($"Feature with ID {id} not found during update", "Features", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                return NotFound();
            }

            // Capture old and new values for logging
            string oldValue = Newtonsoft.Json.JsonConvert.SerializeObject(existingFeature);
            string newValue = Newtonsoft.Json.JsonConvert.SerializeObject(feature);

            _context.Entry(feature).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                _loggerService.LogEvent($"Updated feature with ID {id}", "Features", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0, oldValue, newValue);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (!FeatureExists(id))
                {
                    _loggerService.LogEvent($"Feature with ID {id} not found during update", "Features", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                    return NotFound();
                }
                else
                {
                    _loggerService.LogError("Concurrency error during feature update", ex.Message, nameof(FeaturesController));
                    throw;
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error updating feature", ex.Message, nameof(FeaturesController));
                return StatusCode(500, "Internal server error");
            }

            return NoContent();
        }

        // POST: api/Features
        [HttpPost]
        public async Task<ActionResult<Feature>> PostFeature(Feature feature)
        {
            try
            {
                // Check if a feature with the same name already exists (case-insensitive)
                bool featureExists = _context.Features.Any(f => f.Features.ToLower() == feature.Features.ToLower());

                if (featureExists)
                {
                    return BadRequest($"A feature with the name '{feature.Features}' already exists.");
                }

                // Add the feature to the database
                _context.Features.Add(feature);
                await _context.SaveChangesAsync();

                // Log the event
                _loggerService.LogEvent("Created a new feature", "Features", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);

                // Return the created feature
                return CreatedAtAction("GetFeature", new { id = feature.FeatureId }, feature);
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error creating feature", ex.Message, nameof(FeaturesController));
                return StatusCode(500, "Internal server error");
            }
        }


        // DELETE: api/Features/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFeature(int id)
        {
            try
            {
                var feature = await _context.Features.FindAsync(id);
                if (feature == null)
                {
                    _loggerService.LogEvent($"Feature with ID {id} not found during delete", "Features", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                    return NotFound();
                }

                _context.Features.Remove(feature);
                await _context.SaveChangesAsync();
                _loggerService.LogEvent($"Deleted feature with ID {id}", "Features", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);

                return NoContent();
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error deleting feature", ex.Message, nameof(FeaturesController));
                return StatusCode(500, "Internal server error");
            }
        }

        private bool FeatureExists(int id)
        {
            return _context.Features.Any(e => e.FeatureId == id);
        }
    }
}
