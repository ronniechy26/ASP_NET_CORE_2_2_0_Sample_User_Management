using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MailKit.Net.Smtp;


namespace UserManagementDemo.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly EmailSettings emailSettings;
        private readonly IHostingEnvironment env;

        public EmailSender(
            IOptions<EmailSettings> emailSettings,
            IHostingEnvironment env) 
        {
            this.emailSettings = emailSettings.Value;
            this.env = env;
        }
        public async Task SendEmailAsync(string email, string subject, string message)
        {
            try
            {
                MimeMessage mimeMessage = new MimeMessage();
                mimeMessage.From.Add(new MailboxAddress(emailSettings.SenderName, emailSettings.Sender));
                mimeMessage.To.Add(new MailboxAddress(email));
                mimeMessage.Subject = subject;

                BodyBuilder bodyBuilder = new BodyBuilder();
                bodyBuilder.HtmlBody = "<h1>Hello World!</h1> <br/><br/>" +  message;
               
               // bodyBuilder.TextBody = message;
                //bodyBuilder.Attachments.Add(_env.WebRootPath + "\\uploads\\try.jpg");
                mimeMessage.Body = bodyBuilder.ToMessageBody();

                using (SmtpClient client = new SmtpClient())
                {
                    client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                    //if (env.IsDevelopment())
                        await client.ConnectAsync(emailSettings.MailServer, emailSettings.MailPort, true);
                    //else
                    //    await client.ConnectAsync(_emailSettings.MailServer);

                    await client.AuthenticateAsync(emailSettings.Sender, emailSettings.Password);
                    await client.SendAsync(mimeMessage);
                    await client.DisconnectAsync(true);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(ex.Message);
            }
        }
    }
    
}
