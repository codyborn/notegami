using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace WebRole.Models
{
    public class User : TableEntity
    {
        public string Email { get; set; }
        //public string FirstName { get; set; }
        //public string LastName { get; set; }
        public string Password { get; set; }
        public byte[] EncryptedPassword { get; set; }
        public string UserId { get; set; }
        public DateTime SignupDate { get; set; }
        public Dictionary<string, DateTime> TokenList { get; set; }
        public Tuple<string, DateTime> PWResetTokenWithExpiry { get; set; }

        public void Init()
        {
            //FirstName = Utils.RemoveSpecialCharacters(FirstName);
            //LastName = Utils.RemoveSpecialCharacters(LastName);
            Email = Utils.RemoveSpecialCharacters(Email.ToLowerInvariant());
            // If the user is being created for the first time
            if(string.IsNullOrEmpty(UserId))
            {
                UserId = Guid.NewGuid().ToString("N");
            }
            else
            {
                UserId = Utils.RemoveSpecialCharacters(UserId);
            }
            // Users entity is the only model responsible for setting its own partition
            // It's inefficient to perform a point query w/o both partition and key
            this.PartitionKey = Constant.UserPartition;
            this.RowKey = Email;
        }

        /// <summary>
        /// Set encrypted password and remove password for storing
        /// </summary>
        public void EncryptPassword()
        {
            if(string.IsNullOrEmpty(Password))
            {
                throw new ArgumentNullException("password");
            }
            EncryptedPassword = Encrypt(Password);
            Password = string.Empty;
        }
        public bool AuthCheck(string attemptPassword)
        {
            if(EncryptedPassword == null)
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
            DateTime expiry = DateTime.UtcNow.AddDays(1);
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
        /// </summary>
        public bool ValidateAuthToken(string token)
        {
            if (TokenList == null)
            {
                return false;
            }
            return TokenList.ContainsKey(token) && TokenList[token].CompareTo(DateTime.UtcNow) >= 0;
        }
    }
}