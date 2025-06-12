using ERPAPI.Data;
using ERPAPI.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SettingsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SettingsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetSettings()
        {
            var settings = await _context.MySettings.ToListAsync();
            if (settings == null || !settings.Any())
            {
                return NotFound("No settings found.");
            }
            return Ok(settings);
        }

        [HttpPost]
        public async Task<IActionResult> CreateSetting([FromBody] MySettings setting)
        {
            if (setting == null)
            {
                return BadRequest("Invalid setting data.");
            }

            // Check if a setting with the same UserId already exists
            var existingSetting = await _context.MySettings
                                                .FirstOrDefaultAsync(p => p.UserId == setting.UserId);

            if (existingSetting != null)
            {
                // If exists, update the existing setting
                existingSetting.Settings = setting.Settings;  // Update other properties as needed

                // Optionally, you can log the update or handle it differently
                _context.MySettings.Update(existingSetting);
            }
            else
            {
                // If not, add the new setting
                _context.MySettings.Add(setting);
            }

            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetSettings), new { id = setting.SettingId }, setting);
        }

    }
}
