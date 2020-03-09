using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UserManagementDemo.Models;

namespace UserManagementDemo.DAL
{
    public class SeedData
    {
        public static void Initialize(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            context.Database.EnsureCreated();

           if (!context.Users.Any())
            {
                ApplicationUser user = new ApplicationUser()
                {
                    Email = "ron@acctechnology.ph",
                    SecurityStamp = Guid.NewGuid().ToString(),
                    UserName = "Ron",
                    userStatus = "active",
                    userType = "user"
                };

                userManager.CreateAsync(user, "@Password123");
            
            }
        }
    }
}
