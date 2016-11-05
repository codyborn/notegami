using System;
using System.Threading.Tasks;
using System.Web.Http;
using WebRole.Models;

namespace WebRole.Controllers
{
    public class UserController : ApiController
    {
        public class CreateUserResponse
        {
            public string Error { get; set; }
            public string Token { get; set; }
        }
        public class CreateUserRequest
        {
            public User User { get; set; }
            public int? UTCOffset { get; set; }
        }

        [HttpPost]
        public CreateUserResponse CreateUser([FromBody]CreateUserRequest userRequest)
        {
            CreateUserResponse response = new CreateUserResponse();
            if (string.IsNullOrEmpty(userRequest.User.Email))
            {
                response.Error = "Invalid Input";
                return response;
            }
            using (RequestTracker request = new RequestTracker(Constant.RequestAPI.CreateUser.ToString(), userRequest.User.Email))
            {
                try
                {
                    if (userRequest == null || userRequest.User == null || !userRequest.UTCOffset.HasValue)
                    {
                        request.response = RequestTracker.RequestResponse.UserError;
                        response.Error = "Invalid Input";
                        return response;
                    }
                    User user = userRequest.User;
                    if (string.IsNullOrEmpty(user.Password) || user.Password.Length < 8)
                    {
                        request.response = RequestTracker.RequestResponse.UserError;
                        response.Error = "Password must be at least 8 characters";
                        return response;
                    }
                    if (string.IsNullOrEmpty(user.Email) || !user.Email.Contains("@") || !user.Email.Contains("."))
                    {
                        request.response = RequestTracker.RequestResponse.UserError;
                        response.Error = "Invalid email address";
                        return response;
                    }
                    user.Init();
                    user.EncryptPassword();
                    User retrievedUser;
                    if (TableStore.Get<User>(TableStore.TableName.users, user.PartitionKey, user.Email, out retrievedUser))
                    {
                        request.response = RequestTracker.RequestResponse.UserError;
                        response.Error = "User already exists";
                        return response;
                    }

                    user.SignupDate = DateTime.UtcNow;
                    // Generate temporary auth token
                    string token = user.GetAuthToken();
                    TableStore.Set(TableStore.TableName.users, user);
                    // Create the tutorial notes
                    foreach (string note in Constant.TutorialNotes)
                    {
                        IndexerBase.CreateNote(user.UserId, (int)userRequest.UTCOffset, note, string.Empty, user.Email);
                    }
                    IndexerBase.CreateNote(user.UserId, (int)userRequest.UTCOffset, Constant.ExampleNote, string.Empty, user.Email);

                    response.Token = token;
                    response.Error = "Success";
                    return response;
                }
                catch (Exception e)
                {
                    request.response = RequestTracker.RequestResponse.ServerError;
                    ExceptionTracker.LogException(e);
                    response.Error = "Server Error";
                    return response;
                }
            }
        }

        [HttpPost]
        public string AuthUser([FromBody]User user)
        {
            if (string.IsNullOrEmpty(user.Email))
            {
                return string.Empty;
            }
            using (RequestTracker request = new RequestTracker(Constant.RequestAPI.AuthUser.ToString(), user.Email))
            {
                try
                {
                    if (user == null || string.IsNullOrEmpty(user.Email) || string.IsNullOrEmpty(user.Password))
                    {
                        request.response = RequestTracker.RequestResponse.UserError;
                        return string.Empty;
                    }
                    user.Init();
                    User retrievedUser;
                    TableStore.Get<User>(TableStore.TableName.users, user.PartitionKey, user.Email, out retrievedUser);
                    if (retrievedUser == null || !retrievedUser.AuthCheck(user.Password))
                    {
                        request.response = RequestTracker.RequestResponse.UserError;
                        return string.Empty;
                    }
                    // New property on 8/20/16, give existing users some cred
                    if (retrievedUser.SignupDate == new DateTime())
                    {
                        retrievedUser.SignupDate = DateTime.UtcNow;
                    }
                    // Generate temporary auth token
                    string token = retrievedUser.GetAuthToken();
                    // Store with updated auth table
                    TableStore.Update(TableStore.TableName.users, retrievedUser);
                    return token;
                }
                catch (Exception e)
                {
                    request.response = RequestTracker.RequestResponse.ServerError;
                    ExceptionTracker.LogException(e);
                    return string.Empty;
                }
            }
        }

        public class AuthAttempt
        {
            public string Email { get; set; }
            public string AuthToken { get; set; }
        }
        [HttpPost]
        public bool AuthTokenValid([FromBody]AuthAttempt attempt)
        {
            if (attempt == null || 
                string.IsNullOrEmpty(attempt.Email) || 
                string.IsNullOrEmpty(attempt.AuthToken))
            {
                return false;
            }
            return !string.IsNullOrEmpty(GetUserId(attempt.Email, attempt.AuthToken));
        }

        [HttpPost]
        public string SetPWResetToken([FromBody]User user)
        {
            if (string.IsNullOrEmpty(user.Email))
            {
                return string.Empty;
            }
            using (RequestTracker request = new RequestTracker(Constant.RequestAPI.SetPWReset.ToString(), user.Email))
            {
                try
                {
                    user.Init();
                    User retrievedUser;
                    TableStore.Get<User>(TableStore.TableName.users, user.PartitionKey, user.Email, out retrievedUser);
                    if (retrievedUser == null)
                    {
                        request.response = RequestTracker.RequestResponse.UserError;
                        return string.Empty;
                    }
                    // Generate token with expiry
                    Tuple<string, DateTime> resetToken = new Tuple<string, DateTime>(Utils.Rand().ToString(), DateTime.UtcNow.AddDays(1));

                    retrievedUser.PWResetTokenWithExpiry = resetToken;
                    TableStore.Update(TableStore.TableName.users, retrievedUser);
                    // Send email
                    Utils.SendMail(user.Email,
                                  "Notegami.com Password Reset",
                                  string.Format("Please navigate to </br>http://notegami.com/views/passwordreset.html?email={0}&token={1} </br>to create your new password.",
                                  user.Email, resetToken.Item1)).Wait();

                    return "Success";
                }
                catch (Exception e)
                {
                    request.response = RequestTracker.RequestResponse.ServerError;
                    ExceptionTracker.LogException(e);
                    return string.Empty;
                }
            }
        }

        public class ResetPasswordRequest
        {
            public User User { get; set; }
            public string Token { get; set; }
        }
        [HttpPost]
        public CreateUserResponse ResetPassword([FromBody]ResetPasswordRequest pwResetRequest)
        {
            CreateUserResponse response = new CreateUserResponse();
            if (string.IsNullOrEmpty(pwResetRequest.User.Email) ||
                string.IsNullOrEmpty(pwResetRequest.User.Password) ||
                string.IsNullOrEmpty(pwResetRequest.Token))
            {
                response.Error = "Missing necessary data";
            }
            User user = pwResetRequest.User;
            using (RequestTracker request = new RequestTracker(Constant.RequestAPI.ResetPW.ToString(), user.Email))
            {
                try
                {
                    user.Init();
                    if (string.IsNullOrEmpty(user.Password) || user.Password.Length < 8)
                    {
                        request.response = RequestTracker.RequestResponse.UserError;
                        response.Error = "Password must be at least 8 characters";
                        return response;
                    }
                    User retrievedUser;
                    TableStore.Get<User>(TableStore.TableName.users, user.PartitionKey, user.Email, out retrievedUser);
                    if (retrievedUser == null)
                    {
                        request.response = RequestTracker.RequestResponse.UserError;
                        response.Error = "Invalid user";
                        return response;
                    }
                    // Confirm token
                    if (retrievedUser.PWResetTokenWithExpiry == null || 
                        retrievedUser.PWResetTokenWithExpiry.Item1 != pwResetRequest.Token ||
                        DateTime.Compare(DateTime.UtcNow, retrievedUser.PWResetTokenWithExpiry.Item2) > 0)
                    {
                        request.response = RequestTracker.RequestResponse.UserError;
                        response.Error = "Invalid or expired token";
                        return response;
                    }
                    retrievedUser.Password = user.Password;
                    retrievedUser.EncryptPassword();
                    retrievedUser.PWResetTokenWithExpiry = null;
                    string token = retrievedUser.GetAuthToken();
                    TableStore.Update(TableStore.TableName.users, retrievedUser);
                    response.Token = token;
                    response.Error = "Success";
                    return response;
                }
                catch (Exception e)
                {
                    request.response = RequestTracker.RequestResponse.ServerError;
                    ExceptionTracker.LogException(e);
                    response.Error = "Oops, something went wrong. Initiating developer punishment.";
                    return response;
                }
            }
        }

        /// <summary>
        /// Looks up the user and validates the authToken
        /// </summary>
        /// <returns>UserId if authToken is valid</returns>
        public static string GetUserId(string email, string authToken)
        {
            email = Utils.RemoveSpecialCharacters(email.ToLowerInvariant());
            User retrievedUser;
            if(!TableStore.Get<User>(TableStore.TableName.users, Constant.UserPartition, email, out retrievedUser))
            {
                return string.Empty;
            }
            if(retrievedUser.ValidateAuthToken(authToken))
            {
                return retrievedUser.UserId;
            }
            return string.Empty;
        }        
    }
}
