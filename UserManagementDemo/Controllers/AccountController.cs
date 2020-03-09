using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UserManagementDemo.DAL;
using UserManagementDemo.Models;
using UserManagementDemo.Services;

namespace UserManagementDemo.Controllers
{

    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : Controller
    {
        public readonly ApplicationDbContext dbContext;
        public UserManager<ApplicationUser> userManager;
        private readonly IEmailSender emailSender;

        public ResponseMessage ResponseMessage = new ResponseMessage();

        public AccountController(ApplicationDbContext dbContext,
               UserManager<ApplicationUser> userManager,
               IEmailSender emailSender)
        {
            this.emailSender = emailSender;
            this.userManager = userManager;
            this.dbContext = dbContext;
        }

        [Route("users")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ApplicationUser>>> GetAllUsers([FromQuery]Filters filters)
        {
            var users =  await dbContext.ApplicationUsers.Select(u => new
            {
                u.Id, u.UserName, u.firstName, u.lastName,
                u.middleName, u.rank, u.department, u.userStatus,
                u.userType,u.Email, u.DateAcctCreated
            }).ToListAsync();

            if (!string.IsNullOrEmpty(filters.Search))
            {
                users =  users.Where(u => u.firstName.Contains(filters.Search, StringComparison.OrdinalIgnoreCase)
                || u.lastName.Contains(filters.Search, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            switch (filters.SortBy)
            {
                case "name":
                    users = users.OrderBy(u => u.lastName).ToList();
                    break;
                case "rank":
                    users = users.OrderByDescending(u => u.rank).ToList();
                    break;
                case "dateCreated":
                    users = users.OrderByDescending(u => u.DateAcctCreated).ToList();
                    break;
                default:
                    break;
            }
            if (filters.Datefrom != null || filters.Dateto != null)
            {
                if (filters.Datefrom > filters.Dateto)
                    return BadRequest(ResponseMessage = new ResponseMessage
                    {
                        Code = "400",
                        Message = new List<string>() { "Datefrom must be less than DateTo" }
                    });
                else if ((filters.Datefrom == null && filters.Dateto != null) || (filters.Datefrom != null && filters.Dateto == null))
                    return BadRequest(ResponseMessage = new ResponseMessage
                    {
                        Code = "400",
                        Message = new List<string>() { "Datefield is missing" }
                    });
                else
                    users = users.Where(u => u.DateAcctCreated >= filters.Datefrom
                    && u.DateAcctCreated <= filters.Dateto).ToList();
            }

            if (users.Count > 0)
            {
                int page = (filters.Page ?? 1);
                int pageSize = (filters.PageSize ?? 3);

                int maxPage = (users.Count % pageSize == 0) ? users.Count / pageSize : users.Count / pageSize + 1;

                if (page <= maxPage)
                    users = (users.Skip((page - 1) * pageSize).Take(pageSize)).ToList();
                else
                    return Json(new string[] { });
               
                return Json(new
                {
                    users,
                    Datefrom = filters.Datefrom,
                    DateTo = filters.Dateto,
                    Page = page,
                    PageSize = pageSize,
                    sortby = filters.SortBy,
                    Search = filters.Search
                });
            }
            else
            {
                return BadRequest(ResponseMessage = new ResponseMessage
                {
                    Code = "400",
                    Message = new List<string>() { "No data match" }
                });
            }
        }

        [Route("user/{id}")]
        [HttpGet]
        public async Task<ActionResult<ApplicationUser>> GetUser(string id)
        {
            var amp = dbContext.ApplicationUsers.Where(u => u.Id == id.ToString()).Select(u => u.SecurityStamp);
            var user = await dbContext.ApplicationUsers.Where(u => u.Id == id.ToString())
                        .Select(u => new
                        {
                            u.Id, u.UserName,u.firstName,u.lastName, u.middleName,
                            u.rank,u.department, u.userStatus, u.userType,u.Email,
                            u.DateAcctCreated
                        }).ToListAsync();

            if (user.Count == 0)
            {
                return NotFound(ResponseMessage = new ResponseMessage { Code = "404",
                    Message = new List<string>() { "User not Found" } });
            }

            return Ok( new { user,amp } );
        }

        [Route("user")]
        [HttpPost]
        public async Task<ActionResult<ApplicationUser>> AddUser([FromBody]Newtonsoft.Json.Linq.JObject obj)
        {
            Newtonsoft.Json.Linq.JObject payload = obj.ToObject<Newtonsoft.Json.Linq.JObject>();
            List<string> error = ValidateResponseMessagesList(payload);

            if (error.Count == 0)
            {
                string username = payload.SelectToken("userName").ToObject<string>();
                var user = await userManager.FindByNameAsync(username);

                if (user == null)
                {
                    user = new ApplicationUser();

                    user.UserName = username;
                    user.middleName = payload.SelectToken("middleName").ToObject<string>();
                    user.firstName = payload.SelectToken("firstName").ToObject<string>();
                    user.lastName = payload.SelectToken("lastName").ToObject<string>();
                    user.rank = Convert.ToInt32(payload.SelectToken("rank").ToObject<Int32>());
                    user.department = payload.SelectToken("department").ToObject<string>();
                    user.userStatus = payload.SelectToken("userStatus").ToObject<string>();
                    user.userType = payload.SelectToken("userType").ToObject<string>();
                    user.Email = payload.SelectToken("email").ToObject<string>();
                    user.DateAcctCreated = Convert.ToDateTime(DateTime.Now.ToString("MM/dd/yyyy HH:mm"));

                    string Hash = payload.SelectToken("Hash").ToObject<string>();
                    user.PasswordAndSalt = Helpers.PasswordHash.CreatePasswordSalt(Hash);

                    await userManager.CreateAsync(user);

                    string ctoken = await userManager.GenerateEmailConfirmationTokenAsync(user); 
                   
                    string ctokenlink = Url.Action("ConfirmEmail", "Account", new
                    {
                        userid = user.Id,
                        token = ctoken
                    },protocol: HttpContext.Request.Scheme);

                    var email = user.Email;
                    var subject = "Confirm Email";
                    await emailSender.SendEmailAsync(email, subject, "Please confirm your account by clicking this button link : <br/> "
                        + " <button><a href=" + ctokenlink+"> Click </a></button> ");
                }
                else
                {
                    return Conflict(ResponseMessage = new ResponseMessage { Code = "409",
                        Message = new List<string>() { "Username already Exist" } });
                }

                return Json(new
                {
                    Id = user.Id,UserName = user.UserName,
                    firstName = user.firstName, department = user.department,
                    Email = user.Email,lastName = user.lastName,
                    middleName = user.middleName,rank = user.rank,
                    userType = user.userType,DateAcctCreated = DateTime.Now
                });
            }
            else
            {
                return BadRequest(new ResponseMessage() { Code = "400", Message = error }) ;
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            if (userId == null || token == null)
                return BadRequest(new { Code = "400", Message = "Null" });

            var user = await userManager.FindByIdAsync(userId);

            if (user == null)
                return BadRequest(new { Code = "404", Message = "Not found" });

            var result = await userManager.ConfirmEmailAsync(user, token);
          
            if (result.Succeeded)
            {
                await userManager.UpdateSecurityStampAsync(user);
                string passwordTemp = Helpers.PasswordHash.GenerateRandomPassword();
                string newPasswordHash = userManager.PasswordHasher.HashPassword(user, passwordTemp);
                user.PasswordHash = newPasswordHash;

                dbContext.ApplicationUsers.Update(user);
                var success = await dbContext.SaveChangesAsync();

                if (success > 0)
                {
                    var subject = "Account Created";
                    var message = "Thank you for confirming your email. Here is your password : " + passwordTemp;
                    await emailSender.SendEmailAsync(user.Email, subject, message);

                    return Ok(new { Code  = "200", Message = "Email Confirm Successful" });
                }
                else
                {
                    return BadRequest("errorrr");
                }   
            }
            else
            {
                return BadRequest(new { Code = "400", Message = "Email not confirm , Invalid Token" });
            }

        }

        [Route("user/{id}")]
        [HttpPut]
        public async Task<ActionResult> EditUser(string id, [FromBody]Newtonsoft.Json.Linq.JObject obj)
        {
            Newtonsoft.Json.Linq.JObject payload = obj.ToObject<Newtonsoft.Json.Linq.JObject>();

            var account = await userManager.FindByIdAsync(id.ToString());

            if (account != null)
            {
                List<string> errors = ValidateResponseMessagesList(payload);

                if (errors.Count == 0)
                {
                    account.UserName = payload.SelectToken("userName").ToObject<string>();
                    account.firstName = payload.SelectToken("firstName").ToObject<string>();
                    account.department = payload.SelectToken("department").ToObject<string>();
                    account.Email = payload.SelectToken("email").ToObject<string>();
                    account.lastName = payload.SelectToken("lastName").ToObject<string>();
                    account.middleName = payload.SelectToken("middleName").ToObject<string>();
                    account.rank = Convert.ToInt32(payload.SelectToken("rank").ToObject<Int32>());
                    account.userType = payload.SelectToken("userType").ToObject<string>();
                    account.userStatus = payload.SelectToken("userStatus").ToObject<string>();

                    dbContext.Update(account);
                    await dbContext.SaveChangesAsync();
                }
                else
                {
                    return BadRequest(new ResponseMessage()
                    {
                        Code = "404",
                        Message = errors
                    });
                }
            }
            else
            {
                return BadRequest(ResponseMessage = new ResponseMessage { Code = "400",
                    Message = new List<string>() { "Not found" }});
            }

            return Json(new
            {
                Id = account.Id,UserName = account.UserName,
                firstName = account.firstName, lastName = account.lastName,department = account.department,
                Email = account.Email, middleName = account.middleName,
                rank = account.rank,userType = account.userType,
                DateAcctCreated = account.DateAcctCreated.ToString("MM/dd/yyyy HH:mm")
            });
        }

        [Route("user/{id}")]
        [HttpDelete]
        public async Task<ActionResult> DeleteUser(string id)
        {
            var user = await dbContext.ApplicationUsers.FirstOrDefaultAsync(u => u.Id == id.ToString());

            if (user == null)
                return NotFound(ResponseMessage = new ResponseMessage { Code = "404",
                    Message = new List<string>() { "Not found" } });
            else
                dbContext.ApplicationUsers.Remove(user);
                await dbContext.SaveChangesAsync();
            
            return Json(new
            {
                Id = user.Id,UserName = user.UserName,
                firstName = user.firstName,department = user.department,
                Email = user.Email,lastName = user.lastName,
                middleName = user.middleName,rank = user.rank,
                userType = user.userType,
                DateAcctCreated = user.DateAcctCreated.ToString("MM/dd/yyyy HH:mm")
            });
        }

        [Route("user/changepassword/{id}")]
        [HttpPut]
        public async Task<ActionResult> ChangePassword(string id, [FromBody]Newtonsoft.Json.Linq.JObject obj)
        {
            Newtonsoft.Json.Linq.JObject payload = obj.ToObject<Newtonsoft.Json.Linq.JObject>();
            try
            {
                var user = await userManager.FindByIdAsync(id);
                string password = payload.SelectToken("password").ToObject<string>();

                if (user == null )
                {
                    return NotFound(ResponseMessage = new ResponseMessage { Code = "404",
                        Message = new List<string>() { "Not found" } });
                }

                if (password == string.Empty || password == null)
                {
                    return NotFound(ResponseMessage = new ResponseMessage
                    {
                        Code = "400",
                        Message = new List<string>() { "Password is missing" }
                    });
                }

                string newPasswordHash = userManager.PasswordHasher.HashPassword(user, password);
                user.PasswordHash = newPasswordHash;
                dbContext.ApplicationUsers.Update(user);
            }
            catch (Exception)
            {
                return BadRequest(ResponseMessage = new ResponseMessage { Code = "400",
                    Message = new List<string>() { "Password field is missing" } });
            }

            var result = await dbContext.SaveChangesAsync();
            if (result > 0)
                return Ok(ResponseMessage = new ResponseMessage() { Code = "200",
                    Message = new List<string>() { "Password Successfully changed" }});
            else
                return BadRequest(ResponseMessage = new ResponseMessage { Code = "400",
                    Message = new List<string>() { "Password failed to change" }} );
        }

        public static List<string> ValidateResponseMessagesList(Newtonsoft.Json.Linq.JObject obj)
        {
            List<string> error = new List<string>();
            Newtonsoft.Json.Linq.JObject payload = obj.ToObject<Newtonsoft.Json.Linq.JObject>();

            if (payload.SelectToken("userName") == null || payload.SelectToken("userName").ToString() == string.Empty)
                error.Add("userName is required");
            if (payload.SelectToken("firstName") == null || payload.SelectToken("firstName").ToString() == string.Empty)
                error.Add("firstName is required");
            if (payload.SelectToken("lastName") == null || payload.SelectToken("lastName").ToString() == string.Empty)
                error.Add("lastName is required");
            if (payload.SelectToken("rank") == null || Convert.ToInt32(payload.SelectToken("rank")) <= 0)
                error.Add("rank is required");
            if (payload.SelectToken("department")== null || payload.SelectToken("department").ToString() == string.Empty)
                error.Add("department is required");
            if (payload.SelectToken("userStatus") == null || payload.SelectToken("userStatus").ToString() == string.Empty)
                error.Add("userStatus is required");
            if (payload.SelectToken("userType") == null || payload.SelectToken("userType").ToString() == string.Empty)
                error.Add("userType is required");
            if (payload.SelectToken("email") == null || payload.SelectToken("email").ToString() == string.Empty)
                error.Add("email is required");

            return error;
        }

        [Route("user/checkHash/{id}")]
        [HttpPost]
        public async Task<IActionResult> CheckHash(string id, [FromBody]Newtonsoft.Json.Linq.JObject obj)
        {
            var user = await dbContext.ApplicationUsers.FirstOrDefaultAsync(u => u.Id == id);
            Newtonsoft.Json.Linq.JObject payload = obj.ToObject<Newtonsoft.Json.Linq.JObject>();

            if (user  != null)
            {
                string HashAndSalt = user.PasswordAndSalt;
                string input = payload.SelectToken("Hashpassword").ToObject<string>();

                if (Helpers.PasswordHash.IsPasswordValid(input, HashAndSalt))
                {
                    return Ok(input + " " + HashAndSalt + "    match");
                }
            }

            return BadRequest();
        }

        [Route("user/changestatus/{id}")]
        [HttpPut]
        public async Task<IActionResult> ChangeUserStatus(string id,[FromBody] Newtonsoft.Json.Linq.JObject obj)
        {
            ApplicationUser user = await dbContext.ApplicationUsers.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                return NotFound(new { Code = "404", Message = "Not Found" });
            }
            else
            {
                Newtonsoft.Json.Linq.JObject payload = obj.ToObject<Newtonsoft.Json.Linq.JObject>();

                if (payload.SelectToken("userStatus") != null && 
                    payload.SelectToken("userStatus").ToString() != string.Empty)
                {
                    string Status = payload.SelectToken("userStatus").ToObject<string>();
                    user.userStatus = Status;
                    dbContext.ApplicationUsers.Update(user);
                }
            }

            var result = await dbContext.SaveChangesAsync();
            if (result > 0)
                return Ok(new { Code = "200", Message = "User Status Changed" });
            else
                return BadRequest(new { Code = "400", Message = "UserStatus field is required" });
        }
        [AllowAnonymous]
        [Route("user/send")]
        [HttpPost]
        public async Task<IActionResult> SendEmail()
        {

            var email = "ron@acctechnology.ph";

            var subject = "Account Created";

            var message = "This is a test message.";

            await emailSender.SendEmailAsync(email, subject, message);

            return Ok("email sent");
        }

        [AllowAnonymous]
        [Route("users")]
        [HttpPost]
        public async Task<IEnumerable<String>> test()
        {
            return null;
        }

    }

    

    #region Helpers
    public class Filters
    {
        public int? Page { get; set; }
        public int? PageSize { get; set; }
        public string Search { get; set; } = "";
        public string SortBy { get; set; } = "name";
        public DateTime? Datefrom { get; set; }
        public DateTime? Dateto { get; set; }

    }

    public class ResponseMessage
    {
        public string Code { get; set; }
        public List<string> Message { get; set; }
    }


    #endregion

}