using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace WebRole.Models
{
    public class LastUpdateModel : Model
    {
        [JsonConverter(typeof(IsoDateTimeConverter))]
        [JsonProperty("lastUpdateTime")]
        public DateTime LastUpdateTime;

        private static string GetId(string userId)
        {
            return string.Concat("lastupdate_", userId);
        }

        public LastUpdateModel()
        { }        

        public LastUpdateModel(string userId)
        {
            LastUpdateTime = DateTime.UtcNow;
            UserId = userId;
            this.Id = GetId(userId);
        }

        /// <summary>
        /// Returns the last updated time, null if there has never been an update
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public static DateTime? GetLastUpdate(string userId)
        {
            LastUpdateModel lastUpdate = CosmosDBClient.Query<LastUpdateModel>()
                .Where(lu => lu.UserId == userId).Where(lu=>lu.Id == GetId(userId)).AsEnumerable().FirstOrDefault();
            if (lastUpdate == default(LastUpdateModel))
            {
                return null;
            }
            return lastUpdate.LastUpdateTime;
        }

        public static void SetLastUpdate(string userId)
        {
            LastUpdateModel lastUpdate = new LastUpdateModel(userId);
            CosmosDBClient.InsertOrReplace(lastUpdate);
        }
    }
}