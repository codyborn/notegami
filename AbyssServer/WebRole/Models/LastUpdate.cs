using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebRole.Models
{
    public class LastUpdate : TableEntity
    {
        public DateTime LastUpdateTime { get; set; }
        
        /// <summary>
        /// Returns the last updated time, null if there has never been an update
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public static DateTime? GetLastUpdate(string userId)
        {
            LastUpdate lastUpdate = null;
            if (!TableStore.Get<LastUpdate>(TableStore.TableName.lastUpdate, userId, userId, out lastUpdate))
            {
                return null;
            }
            return lastUpdate.LastUpdateTime;
        }

        public static void SetLastUpdate(string userId)
        {
            LastUpdate lastUpdate = new LastUpdate();
            lastUpdate.LastUpdateTime = DateTime.UtcNow;
            lastUpdate.RowKey = userId;
            lastUpdate.PartitionKey = userId;
            TableStore.Update(TableStore.TableName.lastUpdate, lastUpdate);
        }
    }
}