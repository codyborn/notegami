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
            public string Email { get; set; }
            public string Password { get; set; }
            public int? UTCOffset { get; set; }
        }

        [HttpPost]
        public CreateUserResponse CreateUser([FromBody]CreateUserRequest userRequest)
        {
            CreateUserResponse response = new CreateUserResponse();
            if (string.IsNullOrEmpty(userRequest.Email))
            {
                response.Error = "Invalid Input";
                return response;
            }
            using (RequestTracker request = new RequestTracker(Constant.RequestAPI.CreateUser.ToString(), userRequest.Email))
            {
                try
                {
                    if (userRequest == null)
                    {
                        request.response = RequestTracker.RequestResponse.UserError;
                        response.Error = "Invalid Input";
                        return response;
                    }
                    if (string.IsNullOrEmpty(userRequest.Password) || userRequest.Password.Length < 8)
                    {
                        request.response = RequestTracker.RequestResponse.UserError;
                        response.Error = "Password must be at least 8 characters";
                        return response;
                    }
                    if (string.IsNullOrEmpty(userRequest.Email) || !userRequest.Email.Contains("@") || !userRequest.Email.Contains("."))
                    {
                        request.response = RequestTracker.RequestResponse.UserError;
                        response.Error = "Invalid email address";
                        return response;
                    }
                    UserModel retrievedUser;
                    if(UserModel.GetUser(userRequest.Email, out retrievedUser))                    
                    {
                        if (!retrievedUser.AuthCheck(userRequest.Password))
                        {
                            // User exists and pw is wrong
                            request.response = RequestTracker.RequestResponse.UserError;
                            response.Error = "User already exists";
                            return response;
                        }
                        else
                        {
                            // Just let user login
                            // Generate temporary auth token
                            string loginToken = retrievedUser.GetAuthToken();
                            // Store with updated auth table
                            UserModel.UpdateUser(retrievedUser);
                            request.response = RequestTracker.RequestResponse.LoginOnSignup;

                            response.Token = loginToken;
                            response.Error = "Success";
                            return response;
                        }
                    }

                    UserModel user = new UserModel(userRequest.Email, userRequest.Password);
                    LastUpdateModel.SetLastUpdate(user.UserId);

                    // Generate temporary auth token
                    string token = user.GetAuthToken();
                    user.Save();

                    // Create the tutorial notes
                    foreach (string note in Constant.TutorialNotes)
                    {
                        NoteModel.AddNote(note, string.Empty, 0F, 0F, user.Email, user.UserId);
                    }

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
        public bool LogOut([FromBody]AuthAttempt attempt)
        {
            if (attempt == null ||
                string.IsNullOrEmpty(attempt.Email) ||
                string.IsNullOrEmpty(attempt.AuthToken))
            {
                return false;
            }
            User user = new User();
            user.Email = attempt.Email;            
            user.Init();
            UserModel retrievedUser;
            UserModel.GetUser(user.Email, out retrievedUser);
            if (retrievedUser == null)
            {
                return false;
            }
            retrievedUser.RemoveAuthToken(attempt.AuthToken);
            return UserModel.UpdateUser(retrievedUser);
        }

        [HttpPost]
        public string AuthUser([FromBody]CreateUserRequest authRequest)
        {
            if (string.IsNullOrEmpty(authRequest.Email))
            {
                return string.Empty;
            }
            using (RequestTracker request = new RequestTracker(Constant.RequestAPI.AuthUser.ToString(), authRequest.Email))
            {
                try
                {
                    if (authRequest == null || string.IsNullOrEmpty(authRequest.Email) || string.IsNullOrEmpty(authRequest.Password))
                    {
                        request.response = RequestTracker.RequestResponse.UserError;
                        return string.Empty;
                    }
                    UserModel retrievedUser;
                    UserModel.GetUser(authRequest.Email, out retrievedUser);
                    
                    if (retrievedUser == null || !retrievedUser.AuthCheck(authRequest.Password))
                    {
                        request.response = RequestTracker.RequestResponse.UserError;
                        return string.Empty;
                    }
                    // Generate temporary auth token
                    string token = retrievedUser.GetAuthToken();
                    // Store with updated auth table
                    UserModel.UpdateUser(retrievedUser);
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
        public string SetPWResetToken([FromBody]UserModel tokenRequest)
        {
            if (string.IsNullOrEmpty(tokenRequest.Email))
            {
                return string.Empty;
            }
            using (RequestTracker request = new RequestTracker(Constant.RequestAPI.SetPWReset.ToString(), tokenRequest.Email))
            {
                try
                {                    
                    UserModel retrievedUser;
                    UserModel.GetUser(tokenRequest.Email, out retrievedUser);
                    if (retrievedUser == null)
                    {
                        request.response = RequestTracker.RequestResponse.UserError;
                        return string.Empty;
                    }
                    // Generate token with expiry
                    Tuple<string, DateTime> resetToken = new Tuple<string, DateTime>(Utils.Rand().ToString(), DateTime.UtcNow.AddDays(1));

                    retrievedUser.PWResetTokenWithExpiry = resetToken;
                    retrievedUser.Save();

                    // Send email
                    Utils.SendMail(retrievedUser.Email,
                                  "Notegami.com Password Reset",
                                  string.Format(@"<p>Please use this link to create your new password.</p>
                                                  <a href='https://notegami.com/views/passwordreset.html?email={0}&token={1}'>Create Password</a>",
                                  retrievedUser.Email, resetToken.Item1)).Wait();

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
                    retrievedUser.ClearAuthTokens();
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
            UserModel retrievedUser;
            if (!UserModel.GetUser(email, out retrievedUser))
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
