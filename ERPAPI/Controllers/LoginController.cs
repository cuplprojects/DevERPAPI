using Microsoft.AspNetCore.Mvc;
using ERPAPI.Encryption;
using ERPAPI.Model.NonDbModels;
using ERPAPI.Service;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;
using NuGet.Common;
using ERPAPI.Data;
using ERPAPI.Model;
using ERPGenericFunctions.Model;
using Microsoft.EntityFrameworkCore;

namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LoginController : ControllerBase
    {
        private IConfiguration _configuration;
        private readonly AppDbContext _context;
        private readonly ILoggerService _loggerService;

        public LoginController(AppDbContext context, ILoggerService loggerService, IConfiguration configuration)
        {
            _configuration = configuration;
            _context = context;
            _loggerService = loggerService;
        }

        private string GenerateToken(UserAuth user)
        {
            var securitykey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var credentials = new SigningCredentials(securitykey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.UserId.ToString()),
                new Claim("AutogenPass", user.AutogenPass.ToString()),
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Issuer"],
                claims: claims,
                expires: DateTime.Now.AddHours(8),
                signingCredentials: credentials
                );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }


        [AllowAnonymous]
        [HttpPost("login")]

        public IActionResult Login([FromBody] Model.NonDbModels.LoginRequest loginRequest)
        {
            var userAuth = (from user in _context.Users
                            join ua in _context.UserAuths on user.UserId equals ua.UserId
                            join ur in _context.Set<User>() on user.UserId equals ur.UserId

                            join r in _context.Set<Role>() on ur.RoleId equals r.RoleId
                            where user.UserName == loginRequest.UserName
                            select new
                            {
                                ua,
                                user.Status,
                                user.UserName,
                                Role = new
                                {
                                    r.RoleId,
                                    r.RoleName,
                                    r.PriorityOrder,
                                    r.PermissionList,
                                    r.Status,
                                }
                            }).FirstOrDefault();

            if (userAuth == null)
            {
                return NotFound("User not found");
            }

            if (!userAuth.Status)
            {
                return Unauthorized("User is inactive");
            }

            string hashedPassword = Sha256.ComputeSHA256Hash(loginRequest.Password);

            if (hashedPassword != userAuth.ua.Password)
            {
                return Unauthorized("Invalid password");
            }

            // Check if it's the user's first login (AutogenPass == true)
            if (userAuth.ua.AutogenPass)
            {
                // Let the user login but prompt them to change their password
                var token = GenerateToken(userAuth.ua);
                _loggerService.LogEvent($"User Logged-in (First Time)", "Login", userAuth.ua.UserId);
                return Ok(new
                {
                    token = token,
                    userAuth.ua.UserId,
                    userAuth.ua.AutogenPass,

                    Message = "This is your first login, please change your password."
                });
            }
            else
            {
                // Normal login process
                var token = GenerateToken(userAuth.ua);
                _loggerService.LogEvent($"User Logged-in", "Login", userAuth.ua.UserId);
                return Ok(new { token = token, userAuth.ua.UserId, userAuth.ua.AutogenPass, role = userAuth.Role });
            }

        }

        // Change Password API
        [Authorize]
        [HttpPut("Changepassword/{id}")]
        public IActionResult ChangePassword(int id, ChangePasswordRequest cred)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }


            string oldHashPass = Sha256.ComputeSHA256Hash(cred.OldPassword);
            var userauth = _context.UserAuths.FirstOrDefault(i => i.UserId == id);

            if (userauth == null)
            {
                return NotFound("User Authentication Data Not Found");
            }
            if (userauth.Password != oldHashPass)
            {
                return BadRequest("Existing Password Invalid");
            }

            string newPassword = cred.NewPassword;

            string hashedPassword = Sha256.ComputeSHA256Hash(newPassword);

            userauth.Password = hashedPassword;

            userauth.AutogenPass = false;

            _context.SaveChanges();
            var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                // Now you have the user ID
                _loggerService.LogEvent("Password-Changed", "Login", userId);
            }

            return Ok(new { newPassword, hashedPassword });


        }

        [HttpPost("setSecurityAnswers")]
        public IActionResult SetSecurityAnswers(SetSecurityAnswersRequest request)
        {
            var user = _context.UserAuths.FirstOrDefault(x => x.UserId == request.UserId);
            if (user == null)
            {
                return NotFound("User not found.");
            }


            user.SecurityQuestion1Id = request.SecurityQuestion1Id;
            user.SecurityQuestion2Id = request.SecurityQuestion2Id;
            user.SecurityAnswer1 = request.SecurityAnswer1;
            user.SecurityAnswer2 = request.SecurityAnswer2;
            _context.SaveChanges();

            _loggerService.LogEvent("Security answers set", "User", user.UserId);
            return Ok("Security answers set successfully.");
        }






        [HttpGet("forgotPassword/securityQuestions/{username}")]
        public IActionResult GetSecurityQuestions(string username)
        {
            var user = _context.Users.FirstOrDefault(x => x.UserName == username);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            var userAuth = _context.UserAuths.FirstOrDefault(u => u.UserId == user.UserId);
            if (userAuth == null)
            {
                return NotFound("Security questions not set for the user.");
            }

            // Fetch the security question details from the SecurityQuestions table
            var securityQuestion1 = _context.SecurityQuestions.FirstOrDefault(q => q.QuestionId == userAuth.SecurityQuestion1Id);
            var securityQuestion2 = _context.SecurityQuestions.FirstOrDefault(q => q.QuestionId == userAuth.SecurityQuestion2Id);

            // Check if both questions exist
            if (securityQuestion1 == null || securityQuestion2 == null)
            {
                return NotFound("One or both security questions not found.");
            }

            // Create an array containing two objects, each with question id and text
            var securityQuestions = new[]
            {
        new { QuestionId = securityQuestion1.QuestionId, Question = securityQuestion1.SecurityQuestions },
        new { QuestionId = securityQuestion2.QuestionId, Question = securityQuestion2.SecurityQuestions }
    };

            return Ok(securityQuestions);
        }







        [HttpPost("forgotPassword/verifySecurityAnswers")]
        public IActionResult VerifySecurityAnswers([FromBody] VerifySecurityAnswers request)
        {
            var user = _context.Users.FirstOrDefault(x => x.UserName == request.UserName);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            var userAuth = _context.UserAuths.FirstOrDefault(u => u.UserId == user.UserId);
            if (userAuth == null)
            {
                return Unauthorized("Security answers not set for the user.");
            }

            if (userAuth.SecurityAnswer1 != request.SecurityAnswer1 || userAuth.SecurityAnswer2 != request.SecurityAnswer2)
            {
                return Unauthorized("Security answers do not match.");
            }

            return Ok(new { Message = "Security answers verified successfully." });
        }


        [HttpPost("forgotPassword/setNewPassword")]
        public IActionResult SetNewPassword([FromBody] SetNewPassword request)
        {
            var user = _context.Users.FirstOrDefault(x => x.UserName == request.UserName);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            var userAuth = _context.UserAuths.FirstOrDefault(u => u.UserId == user.UserId);
            if (userAuth == null)
            {
                return Unauthorized("Security answers not set for the user.");
            }

            // Verify if the user has passed the security answers step
            if (!request.SecurityAnswersVerified)
            {
                return Unauthorized("Security answers were not verified.");
            }

            // Set new password
            userAuth.Password = Sha256.ComputeSHA256Hash(request.NewPassword);
            userAuth.AutogenPass = false;
            _context.SaveChanges();

            _loggerService.LogEvent("Password reset", "User", userAuth.UserId);
            return Ok("Password reset successfully.");
        }


        // PUT: api/SecurityQuestions/SetPassword
        [HttpPut("SetPassword")]
        public async Task<IActionResult> SetPassword(SetPass setPassword)
        {
            try
            {
                // Find the user authentication record
                var userAuth = await _context.UserAuths.FirstOrDefaultAsync(ua => ua.UserId == setPassword.UserId);
                if (userAuth == null)
                {
                    return NotFound(new { Message = "User not found" });
                }

                // Hash the new password
                var hashedPassword = Sha256.ComputeSHA256Hash(setPassword.NewPassword);

                // Update the password in the UserAuth table
                userAuth.Password = hashedPassword;
                userAuth.AutogenPass = false; // Assuming the new password is not auto-generated

                // Save changes to the database
                _context.Entry(userAuth).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                return Ok(new { Message = "Password updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while setting the password", Error = ex.Message });
            }
        }



        private bool SecurityQuestionExists(int id)
        {
            return _context.SecurityQuestions.Any(e => e.QuestionId == id);
        }

    }
}
