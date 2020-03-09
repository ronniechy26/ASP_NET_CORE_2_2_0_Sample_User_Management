using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using UserManagementDemo.DAL;
using UserManagementDemo.Models;

namespace UserManagementDemo.Controllers
{

    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        public readonly ApplicationDbContext db;
        public UserManager<ApplicationUser> userManager;
        public SignInManager<ApplicationUser> signInManager;
        public ResponseMessage responseMessage = new ResponseMessage();
        public ILogger<AuthController> logger { get; }

        public AuthController(ApplicationDbContext db, 
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<AuthController> logger)
        {
            this.signInManager = signInManager;
            this.logger = logger;
            this.userManager = userManager;
            this.db = db;
        }

      

        [AllowAnonymous]
        [Route("login")]
        [HttpPost]
        public async Task<ActionResult> Login(modelLogin model)
        {
           
            ApplicationUser user = await userManager.FindByNameAsync(model.Username);
            var result = await signInManager.PasswordSignInAsync(user,model.Password,isPersistent:true,false);
            //string awit = db.ApplicationUsers.Select(u => u.SecurityStamp).FirstOrDefault();
            //string awit2 = db.ApplicationUsers.FirstOrDefault(u => u.SecurityStamp == "").SecurityStamp;

            if (user != null && result.Succeeded == true)
            {

                if (user.userStatus != "active")
                {
                    return BadRequest(responseMessage = new ResponseMessage() { Code = "400",
                        Message = new List<string>() { "Account is not active" } });
                }
                else
                {
                    var claims = new[]
                    {
                         new Claim(JwtRegisteredClaimNames.Sub,model.Username),
                         new Claim(JwtRegisteredClaimNames.Jti,Guid.NewGuid().ToString())
                    };

                    var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("QWEASDZXCzETMBEGA1UECAwKU29tZS1TdGF0ZTESMBA"));

                    var token = new JwtSecurityToken(
                        issuer: "issuer",
                        audience: "reader",
                        expires: DateTime.UtcNow.AddHours(6),
                        claims: claims,
                        signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
                        );

                    return Ok(new
                    {
                       user = new
                        {
                            Id = user.Id, UserName = user.UserName,firstName = user.firstName,
                            department = user.department,Email = user.Email, lastName = user.lastName,
                            middleName = user.middleName,rank = user.rank, userType = user.userType,
                            DateAcctCreated = user.DateAcctCreated.ToString("MM/dd/yyyy HH:mm"),
                            TriggredColor = "Yellow"
                        },
                        token = new JwtSecurityTokenHandler().WriteToken(token),
                        expiration = token.ValidTo
                    });
                }
            }
            else
            {
                logger.LogError("incorrect email password");
                return Unauthorized(responseMessage = new ResponseMessage()
                {
                    Code = "401",
                    Message = new List<string>() { "Incorrect Username or password" }
                });
              
            }
        }

        [Route("logout")]
        [HttpPost]
        public async Task<IActionResult> Logout()
        {

            await signInManager.SignOutAsync();
            return Ok("log out ");
        }

        public class modelLogin
        {
            public string Username { get; set; }
            public string  Password { get; set; }
        }
    }
}