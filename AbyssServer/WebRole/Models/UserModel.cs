using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Security.Cryptography;
using Microsoft.Azure.Documents;

namespace WebRole.Models
{
    public class UserModel : Model
    {
        // Need to be able to lookup by email
        [JsonProperty("email")]
        public string Email;

        [JsonProperty("password")]
        public string Password;

        [JsonProperty("encryptedPassword")]
        public byte[] EncryptedPassword;

        [JsonConverter(typeof(IsoDateTimeConverter))]
        [JsonProperty("signupDate")]
        public DateTime SignupDate;

        [JsonProperty("tokenList")]
        public Dictionary<string, DateTime> TokenList;

        [JsonProperty("pwResetTokenWithExpiry")]
        public Tuple<string, DateTime> PWResetTokenWithExpiry;

        public UserModel()
        { }

        public UserModel(string email, string password)
        {
            Email = Utils.RemoveSpecialCharacters(email.ToLowerInvariant());
            Password = password;
            Id = string.Concat("user_", Guid.NewGuid().ToString("N"));
            UserId = Id;
            EncryptPassword();
        }        

        public static bool GetUser(string email, out UserModel user)
        {
            email = Utils.RemoveSpecialCharacters(email.ToLowerInvariant());
            user = CosmosDBClient.Query<UserModel>(limitOne: true, crossPartition: true)
            .Where(u => u.Email == email).AsEnumerable().FirstOrDefault();
            return user != null;
        }
        public static bool UpdateUser(UserModel user)
        {
            return CosmosDBClient.Update(user);
        }
        public bool Save()
        {
            return CosmosDBClient.Insert(this);
        }

        /// <summary>
        /// Set encrypted password and remove password for storing
        /// </summary>
        public void EncryptPassword()
        {
            if (string.IsNullOrEmpty(Password))
            {
                throw new ArgumentNullException("password");
            }
            EncryptedPassword = Encrypt(Password);
            Password = string.Empty;
        }
        public bool AuthCheck(string attemptPassword)
        {
            if (EncryptedPassword == null)
            {
                throw new ArgumentNullException("Cannot call AuthCheck on a User without an encryptedPassword set");
            }
            byte[] pwBytes = Encrypt(attemptPassword);
            return pwBytes.SequenceEqual(EncryptedPassword);
        }
        private byte[] Encrypt(string pw)
        {
            byte[] pwBytes = Encoding.ASCII.GetBytes(pw);
            return SHA256.Create().ComputeHash(pwBytes);
        }

        /// <summary>
        /// Generates and returns new auth token
        /// Adds auth token to user's local list
        /// Removes expired tokens from the local list
        /// Caller reponsible for saving entity in table
        /// </summary>
        /// <returns>New Auth Token</returns>
        public string GetAuthToken()
        {
            // Generate new token
            string authToken = Utils.Rand().ToString();
            DateTime expiry = DateTime.UtcNow.AddDays(90);
            if (TokenList == null)
            {
                TokenList = new Dictionary<string, DateTime>();
            }
            TokenList[authToken] = expiry;

            DateTime minDate = DateTime.MaxValue;
            string oldestToken = string.Empty;
            if (TokenList.Count > 10)
            {
                foreach (string key in TokenList.Keys)
                {
                    if (TokenList[key] < minDate)
                    {
                        minDate = TokenList[key];
                        oldestToken = key;
                    }
                }
                TokenList.Remove(oldestToken);
            }

            return authToken;
        }

        /// <summary>
        /// Returns true if the auth token is still valid
        /// 2/2017 ignoring expiry on token since user can logout
        /// </summary>
        public bool ValidateAuthToken(string token)
        {
            if (TokenList == null)
            {
                return false;
            }
            return TokenList.ContainsKey(token);// && TokenList[token].CompareTo(DateTime.UtcNow) >= 0;
        }

        // For logout scenario
        public bool RemoveAuthToken(string token)
        {
            if (TokenList == null)
            {
                return false;
            }
            return TokenList.Remove(token);
        }

        public bool ClearAuthTokens()
        {

            if (TokenList == null)
            {
                return false;
            }
            TokenList.Clear();
            return true;
        }
    }
}