using ERPAPI.Model;
using ERPAPI.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public NotificationsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/<NotificationsController>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Notification>>> GetNotifications()
        {
            var notifications = await _context.Notifications.ToListAsync();

            if (notifications == null)
            {
                return NotFound();
            }

            return Ok(notifications);
        }

        [HttpGet("today")]
        public async Task<ActionResult<IEnumerable<Notification>>> GetLastHourNotifications()
        {

            var notifications = await _context.Notifications
                                              .Where(n=>n.IsDisplayed == false)
                                              .ToListAsync();
            foreach (var notification in notifications)
            {
                notification.IsDisplayed = true;
            }

            await _context.SaveChangesAsync();

            if (notifications == null || !notifications.Any())
            {
                return Ok();
            }

            return Ok(notifications);
        }
        // GET api/<NotificationsController>/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Notification>> GetNotificationsById(int id)
        {
            var notification = await _context.Notifications.FindAsync(id);

            return Ok(notification);
        }

        // POST api/<NotificationsController>
        [HttpPost]
        public async Task<ActionResult<Notification>> PostNotification(Notification notification)
        {
            if (notification == null)
            {
                return BadRequest("Notification is null.");
            }
            notification.PostedAt = DateTime.Now;
            notification.IsDisplayed = false;
            _context.Notifications.Add(notification);
            _context.SaveChanges();
            return Ok(notification);
        }

        // PUT api/<NotificationsController>/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutNotification(int id, Notification notification)
        {
            if (id != notification.NotificationId)
            {
                return BadRequest();
            }
            _context.Entry(notification).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE api/<NotificationsController>/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNotification(int id)
        {
            var notification = await _context.Notifications.FindAsync(id);
            if (notification == null)
            {
                return NotFound();
            }
            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
