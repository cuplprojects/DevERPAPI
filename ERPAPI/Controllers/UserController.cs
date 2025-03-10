using Microsoft.AspNetCore.Mvc;
using ERPAPI.Encryption;
using ERPAPI.Services;
using ERPAPI.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using ERPGenericFunctions.Model;
using System;
using System.IO;
using System.Threading.Tasks;

using ERPAPI.Service;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILoggerService _loggerService;
        private readonly IWebHostEnvironment _hostingEnvironment;

        public UserController(AppDbContext context, ILoggerService loggerService, IWebHostEnvironment hostingEnvironment)
        {
            _context = context;
            _loggerService = loggerService;
            _hostingEnvironment = hostingEnvironment;
        }

        // POST: api/User/create
        [HttpPost("create")]
        public IActionResult CreateUser(User user)
        {
            try
            {
                // Check if the username already exists
                var existingUser = _context.Users.FirstOrDefault(u => u.UserName == user.UserName);
                if (existingUser != null)
                {
                    return Conflict(new { Message = "Username already exists" });
                }

                // Add the new user to the database
                _context.Users.Add(user);
                _context.SaveChanges();

                // Check the user's RoleId
                if (user.RoleId != 6)
                {
                    // Generate and hash a password for the user
                    string generatedPassword = Passwordgen.GeneratePassword();
                    var hashedPassword = Sha256.ComputeSHA256Hash(generatedPassword);

                    // Create a UserAuth entry with the auto-generated password
                    var userAuth = new UserAuth
                    {
                        UserId = user.UserId,
                        Password = hashedPassword,
                        AutogenPass = true
                    };

                    _context.UserAuths.Add(userAuth);
                    _context.SaveChanges();

                    // Log the event
                    _loggerService.LogEvent("User created with password", "User", user.UserId);

                    return Ok(new
                    {
                        UserId = user.UserId,
                        Message = "User created with password",
                        UserName = user.UserName,
                        Password = generatedPassword
                    });
                }

                // Log the event for users with RoleId = 6
                _loggerService.LogEvent("User created without password", "User", user.UserId);

                return Ok(new
                {
                    UserId = user.UserId,
                    Message = "User created without password",
                    UserName = user.UserName
                });
            }
            catch (Exception ex)
            {
                _loggerService.LogError("User creation failed", ex.Message, "UserController");
                return StatusCode(500, "User creation failed");
            }
        }


        // GET: api/User
        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetAllUsers()
        {
            try
            {
                // Retrieve all users from the database
                var users = await Task.FromResult(_context.Users.ToList());
                return Ok(users);
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Failed to retrieve users", ex.Message, "UserController");
                return StatusCode(500, "Failed to retrieve users");
            }
        }

        // GET: api/User
        /* [HttpGet("operator")]
         public async Task<ActionResult<IEnumerable<User>>> GetOperators()
         {
             try
             {
                 // Retrieve all users from the database
                 var users = await Task.FromResult(_context.Users.Where(r=>r.RoleId==6).ToList());
                 return Ok(users);
             }
             catch (Exception ex)
             {
                 _loggerService.LogError("Failed to retrieve operators", ex.Message, "UserController");
                 return StatusCode(500, "Failed to retrieve operators");
             }
         }*/
        [HttpGet("operator")]
        public async Task<ActionResult<IEnumerable<User>>> GetOperators()
        {
            try
            {
                // Retrieve users with roles 6 or 4 and add FullName as a concatenation of FirstName and MiddleName
                var users = await Task.FromResult(
                    _context.Users
                        .Where(r => r.RoleId == 6 || r.RoleId == 4)
                        .Select(u => new
                        {
                            u.UserId,
                            u.FirstName,
                            u.MiddleName,
                            u.LastName,
                            u.RoleId,
                            u.Address,
                            u.Gender,
                            u.UserName,
                            u.Status,
                            u.MobileNo,

                            FullName = u.FirstName + " " + u.MiddleName + " " + u.LastName// Concatenate FirstName and MiddleName
                        })
                        .ToList()
                );

                return Ok(users);
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Failed to retrieve operators", ex.Message, "UserController");
                return StatusCode(500, "Failed to retrieve operators");
            }
        }


        [HttpGet("LoggedUser")]
        [Authorize] // Ensures the request is authenticated
        public async Task<ActionResult<User>> GetUserByJwt()
        {
            try
            {
                var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return BadRequest("Invalid token");
                }

                // Retrieve the user from the database
                var user = await _context.Users
                    .Where(u => u.UserId == userId)
                    .Join(
                        _context.Roles,
                        user => user.RoleId,
                        role => role.RoleId,
                        (user, role) => new
                        {
                            user.UserId,
                            user.UserName,
                            user.FirstName,
                            user.MiddleName,
                            user.LastName,
                            user.MobileNo,
                            user.Status,
                            user.Gender,
                            user.Address,
                            user.ProfilePicturePath,
                            Role = new
                            {
                                role.RoleId,
                                role.RoleName,
                                role.PriorityOrder,
                                role.Status,
                                Permissions = role.PermissionList // Use PermissionList for deserialized permissions
                            }
                        }
                    )
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return NotFound("User not found.");
                }

                return Ok(user); // Return the retrieved user information
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Failed to retrieve user by JWT", ex.Message, "UserController");
                return StatusCode(500, "Failed to retrieve user");
            }
        }


        // GET: api/User/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<User>> GetUserById(int id)
        {
            try
            {
                // Retrieve the user from the database by ID
                var user = await Task.FromResult(_context.Users.FirstOrDefault(u => u.UserId == id));

                if (user == null)
                {
                    return NotFound(new { Message = "User not found" });
                }

                return Ok(user);
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Failed to retrieve user", ex.Message, "UserController");
                return StatusCode(500, "Failed to retrieve user");
            }
        }

        // POST: api/User/generatePassword/{id}
        [HttpPost("generatePassword/{id}")]
        public IActionResult GeneratePasswordForUser(int id)
        {
            try
            {
                // Find the user by ID
                var user = _context.Users.FirstOrDefault(u => u.UserId == id);
                if (user == null)
                {
                    return NotFound(new { Message = "User not found" });
                }

                // Check if a UserAuth entry already exists for this user
                var userAuth = _context.UserAuths.FirstOrDefault(ua => ua.UserId == user.UserId);
                if (userAuth == null)
                {
                    // If no UserAuth exists, create a new one
                    userAuth = new UserAuth
                    {
                        UserId = user.UserId
                    };
                    _context.UserAuths.Add(userAuth);
                }

                // Generate and hash a new password
                string generatedPassword = Passwordgen.GeneratePassword();
                var hashedPassword = Sha256.ComputeSHA256Hash(generatedPassword);

                // Update UserAuth with the new password and set AutogenPass to true
                userAuth.Password = hashedPassword;
                userAuth.AutogenPass = true;

                // Save changes to the database
                _context.SaveChanges();

                // Log the event
                _loggerService.LogEvent("Password generated for existing user", "User", user.UserId);

                // Return the new password to the client
                return Ok(new { Message = "Password generated", UserName = user.UserName, Password = generatedPassword });
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Failed to generate password for user", ex.Message, "UserController");
                return StatusCode(500, new { Message = "Failed to generate password" });
            }
        }

        // POST: api/User/uploadProfilePicture/{id}
        [HttpPost("upload/{userId}")]
        public IActionResult UploadImage(int userId)
        {
            try
            {
                // Ensure the user with the provided userId exists
                if (UserExists(userId))
                {
                    var file = Request.Form.Files[0];

                    if (file.Length > 0)
                    {
                        // Extract the file extension from the original filename
                        var fileExtension = Path.GetExtension(file.FileName);

                        // Generate the custom filename based on the user ID and original file extension
                        var customFileName = $"{userId}_profilepic{fileExtension}";

                        var filePath = Path.Combine("wwwroot/Image", customFileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            file.CopyTo(stream);
                        }

                        // Update the user's profile picture path in the database
                        var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
                        if (user != null)
                        {
                            user.ProfilePicturePath = $"image/{customFileName}";
                            _context.SaveChanges();
                        }

                        return Ok(new { message = "Image uploaded successfully", filePath });
                    }
                    else
                    {
                        return BadRequest("No file uploaded");
                    }
                }
                else
                {
                    return BadRequest("Invalid user");
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // PUT: api/User/updateProfilePicture/{userId}
        [HttpPut("updateProfilePicture/{userId}")]
        public IActionResult UpdateProfilePicture(int userId)
        {
            try
            {
                // Ensure the user with the provided userId exists
                var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
                if (user == null)
                {
                    return NotFound(new { Message = "User not found" });
                }

                // Delete the existing profile picture if it exists
                if (!string.IsNullOrEmpty(user.ProfilePicturePath))
                {
                    var oldFilePath = Path.Combine("wwwroot", user.ProfilePicturePath);
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }

                // Proceed to upload the new profile picture
                var file = Request.Form.Files[0];
                if (file.Length > 0)
                {
                    var fileExtension = Path.GetExtension(file.FileName);
                    var customFileName = $"{userId}_profilepic{fileExtension}";
                    var newFilePath = Path.Combine("wwwroot/Image", customFileName);

                    using (var stream = new FileStream(newFilePath, FileMode.Create))
                    {
                        file.CopyTo(stream);
                    }

                    // Update the profile picture path in the database
                    user.ProfilePicturePath = $"image/{customFileName}";
                    _context.SaveChanges();

                    return Ok(new { message = "Profile picture updated successfully", filePath = user.ProfilePicturePath });
                }
                else
                {
                    return BadRequest("No file uploaded");
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }


        // DELETE: api/User/deleteProfilePicture/{userId}
        [HttpDelete("deleteProfilePicture/{userId}")]
        public IActionResult DeleteProfilePicture(int userId)
        {
            try
            {
                var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
                if (user == null)
                {
                    return NotFound(new { Message = "User not found" });
                }

                if (string.IsNullOrEmpty(user.ProfilePicturePath))
                {
                    return BadRequest(new { Message = "No profile picture to delete" });
                }

                var filePath = Path.Combine("wwwroot", user.ProfilePicturePath);
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                user.ProfilePicturePath = null;
                _context.SaveChanges();

                return Ok(new { Message = "Profile picture deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Internal server error", Details = ex.InnerException?.Message ?? ex.Message });
            }

        }



        private bool UserExists(int id)
        {
            return _context.Users.Any(e => e.UserId == id);
        }

        // PUT: api/User/edit/{id}
        [HttpPut("edit/{id}")]
        public IActionResult EditUser(int id, [FromBody] User updatedUser)
        {
            try
            {
                // Find the user by ID
                var existingUser = _context.Users.FirstOrDefault(u => u.UserId == id);
                if (existingUser == null)
                {
                    return NotFound(new { Message = "User not found" });
                }

                // Update user properties
                existingUser.RoleId = updatedUser.RoleId;
                existingUser.Address = updatedUser.Address;
                existingUser.MobileNo = updatedUser.MobileNo;

                // Save changes to the database
                _context.SaveChanges();

                // Log the event
                _loggerService.LogEvent("User details updated", "User", id);

                return Ok(new { Message = "User updated successfully", User = existingUser });
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Failed to update user", ex.Message, "UserController");
                return StatusCode(500, new { Message = "Failed to update user" });
            }
        }



        // DELETE: api/User/delete/{id}
        [HttpDelete("delete/{id}")]
        public IActionResult DeleteUser(int id)
        {
            try
            {
                // Find the user by ID
                var user = _context.Users.FirstOrDefault(u => u.UserId == id);
                if (user == null)
                {
                    return NotFound(new { Message = "User not found" });
                }

                // Remove the user from the database
                _context.Users.Remove(user);

                // Remove associated UserAuth if it exists
                var userAuth = _context.UserAuths.FirstOrDefault(ua => ua.UserId == id);
                if (userAuth != null)
                {
                    _context.UserAuths.Remove(userAuth);
                }

                // Save changes to the database
                _context.SaveChanges();

                // Log the event
                _loggerService.LogEvent("User deleted", "User", id);

                return Ok(new { Message = "User deleted successfully" });
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Failed to delete user", ex.Message, "UserController");
                return StatusCode(500, new { Message = "Failed to delete user" });
            }
        }

        [HttpPost("UnlockScreenByPin")]
        public async Task<IActionResult> UnlockScreenByPin([FromBody] PinUnlockRequest request)
        {
            try
            {
                // Validate the request
                if (request == null || request.Pin <= 0)
                {
                    return BadRequest(new { Message = "PIN is required and must be a positive number." });
                }

                // Retrieve the user from the token
                var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return BadRequest(new { Message = "Invalid User Token." });
                }

                // Find the user in the database
                var userAuth = await _context.UserAuths.FirstOrDefaultAsync(u => u.UserId == userId);
                if (userAuth == null)
                {
                    return NotFound(new { Message = "User not found." });
                }

                // Validate the PIN
                if (userAuth.ScreenLockPin != request.Pin)
                {
                    return BadRequest(new { Message = "Incorrect PIN." });
                }

                // Unlock the screen (custom logic if needed)
               

                return Ok(new { Message = "Screen unlocked successfully." });
            }
            catch (Exception ex)
            {
                
                return StatusCode(500, new { Message = "Internal server error", Details = ex.Message });
            }
        }

        public class PinUnlockRequest
        {
            public int Pin { get; set; }
        }


        [HttpPut("ChangeScreenLockPin")]
        public async Task<IActionResult> ChangeScreenLockPin([FromBody] ChangePinRequest request)
        {
            try
            {
                // Ensure the request body is not null and PINs are valid
                if (request == null || request.OldPin <= 0 || request.NewPin <= 0)
                {
                    return BadRequest(new { Message = "Both Old PIN and New PIN are required and must be positive numbers." });
                }

                var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return BadRequest("Invalid token");
                }
               
                // Find the user in the database
                var userAuth = await _context.UserAuths.FirstOrDefaultAsync(u => u.UserId == userId);
                if (userAuth == null)
                {
                    return NotFound(new { Message = "User not found." });
                }

                // Validate the Old PIN
                if (userAuth.ScreenLockPin != request.OldPin)
                {
                    return BadRequest(new { Message = "Incorrect Old PIN." });
                }

                // Validate the New PIN
                var newPinLength = request.NewPin.ToString().Length;
                if (newPinLength < 3 || newPinLength > 5)
                {
                    return BadRequest(new { Message = "New PIN must be between 3 and 5 digits." });
                }

                // Ensure the new PIN is different from the old PIN
                if (request.OldPin == request.NewPin)
                {
                    return BadRequest(new { Message = "New PIN cannot be the same as the Old PIN." });
                }

                // Update the user record with the new PIN
                userAuth.ScreenLockPin = request.NewPin;
                _context.UserAuths.Update(userAuth);
                await _context.SaveChangesAsync();

                // Log the event (for security auditing purposes)
                _loggerService.LogEvent("Screen lock PIN changed", "User", userId);

                return Ok(new { Message = "Screen lock PIN updated successfully." });
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Failed to change screen lock PIN", ex.Message, "UserController");
                return StatusCode(500, new { Message = "Internal server error", Details = ex.Message });
            }
        }

        public class ChangePinRequest
        {
            public int OldPin { get; set; }
            public int NewPin { get; set; }
        }






    }
}
