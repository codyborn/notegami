using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Text;
using System.Threading;
using System.Net.Http;
using System.Net.Mail;
using SendGrid;
using Microsoft.WindowsAzure.ServiceRuntime;
using System.Configuration;
using System.Net;
using System.Threading.Tasks;
using SendGrid.Helpers.Mail;

namespace WebRole
{
    public static class Utils
    {
        public static string GetConfigValue(string key)
        {
            string value = string.Empty;
            try
            {
                value = RoleEnvironment.GetConfigurationSettingValue(key);
            }
            catch
            {
                value = ConfigurationManager.AppSettings[key];
            }
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("Cannot find config value for key " + key);
            }
            return value;
        }
        public static string RemoveSpecialCharacters(string str) {
           if(string.IsNullOrEmpty(str))
           {
               return str;
           }
           StringBuilder sb = new StringBuilder();
           foreach (char c in str) {
              if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '.' || c == '_' || c == '@' || c == ' ' || c == '/' || c == ':' || c == '-' || c == '+') {
                 sb.Append(c);
              }
           }
           return sb.ToString().Trim();
        }
        
        private static int seed = Environment.TickCount;

        private static readonly ThreadLocal<Random> random =
            new ThreadLocal<Random>(() => new Random(Interlocked.Increment(ref seed)));

        public static int Rand()
        {
            return random.Value.Next();
        }
        public static async Task SendMail(string email, string subject, string content)
        {          
            string apiKey = GetConfigValue("SendGridPrivateKey");
            
            dynamic sg = new SendGridAPIClient(apiKey);

            Email from = new Email("noreply@notegami.com");            
            Email to = new Email(email);
            Content innerContent = new Content("text/html", content);
            Mail mail = new Mail(from, subject, to, innerContent);

            dynamic response = await sg.client.mail.send.post(requestBody: mail.Get());
        }
    }

    public static class Constant
    {
        public static string DeveloperId = "c1c8b9bc10a6451eba25cf8a2cb07e43";
        public static string UserPartition = "users";
        public static int MaxNoteLength = 10000;
        // If you're going to query more than this number of notes,
        // Query all notes for a user and iterate through them on the server-side
        // Profiled using an account with 180 notes
        public static int MaxIndividualNoteQueryCount = 1;
        // Note that there is an invisible character between '#' and "feedback"
        public static string[] TutorialNotes =
            { "#Tutorial Send direct feedback to the developer by including #feedback in your note.",
             "#Tutorial Hashtags that you use frequently will appear in the quick-search bar at the top. You can add a tag to a new note by press-and-hold action on your phone or right-clicking on your desktop.",
             "#Tutorial You can search for your notes by content, hashtags, date, and location.",
             "#Tutorial Welcome to Notegami!\nThink of this app as your personal search engine.  Notegami helps you stay organized by automatically indexing all of your notes."
            };
        public static string ExampleNote = "#Grocerylist Coffee\nEggs\nBread";

        public enum RequestAPI
        {
            CreateUser,
            AuthUser,
            CreateNote,
            UpdateNote,
            DeleteNote,
            SetPWReset,
            ResetPW
        }
    }    
}