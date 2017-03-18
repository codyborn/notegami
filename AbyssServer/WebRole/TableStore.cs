using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System.Configuration;
using Microsoft.WindowsAzure.ServiceRuntime;
using System.Threading.Tasks;
using System.Diagnostics;

namespace WebRole
{
    public class TableStore
    {
        private static string connectionString = Utils.GetConfigValue("StorageConnectionString");
        private static Lazy<Dictionary<string, CloudTable>> tables = new Lazy<Dictionary<string, CloudTable>>(() => GetTables());
        public enum TableName
        {
            notes,
            users,
            indices,
            recentTokens,
            pingTable,
            lastUpdate
        };

        private static Dictionary<string, CloudTable> GetTables()
        {
            Dictionary<string, CloudTable> tables = new Dictionary<string, CloudTable>();
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);

            // Create the table client.
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();

            foreach (TableName TableName in Enum.GetValues(typeof(TableName)))
            {
                string tableName = TableName.ToString();
                // Create the table if it doesn't exist.
                CloudTable table = tableClient.GetTableReference(tableName);
                table.CreateIfNotExists();
                tables[tableName] = table;
            }
            return tables;
        }

        public static void Set(TableName table, TableEntity value)
        {
            TableStoreEntity container = new TableStoreEntity(value);
            TableOperation insertOperation = TableOperation.Insert(container);
            CloudTable cloudTable = tables.Value[table.ToString()];
            cloudTable.Execute(insertOperation);
        }

        public static void Delete(TableName table, TableEntity value)
        {
            TableStoreEntity container = new TableStoreEntity(value);
            container.ETag = "*";
            TableOperation deleteOperation = TableOperation.Delete(container);
            CloudTable cloudTable = tables.Value[table.ToString()];
            cloudTable.Execute(deleteOperation);
        }

        public static void Update(TableName table, TableEntity value)
        {
            TableStoreEntity container = new TableStoreEntity(value);
            container.ETag = "*";
            container.Timestamp = DateTime.UtcNow;
            TableOperation updateOperation = TableOperation.InsertOrReplace(container);
            CloudTable cloudTable = tables.Value[table.ToString()];
            cloudTable.Execute(updateOperation);
        }

        // queryEntity usually just contains the rowKey and partitionKey - and that's okay
        public static bool Get<T>(TableName table, string partitionKey, string rowKey, out T retrievedEntity)
            where T : TableEntity
        {
            TableOperation retrieveOperation = TableOperation.Retrieve<TableStoreEntity>(partitionKey, rowKey);
            CloudTable cloudTable = tables.Value[table.ToString()];
            TableResult retrievedResult = cloudTable.Execute(retrieveOperation);

            // Print the phone number of the result.
            if (retrievedResult.Result != null)
            {
                retrievedEntity = ((TableStoreEntity)retrievedResult.Result).GetDeserializedObj<T>();
                return true;
            }
            else
            {
                retrievedEntity = null;
                return false;
            }
        }

        public static IEnumerable<T> GetAllEntitiesInAPartition<T>(TableName table, string partitionKey)
            where T : TableEntity
        {
            TableQuery<TableStoreEntity> query = new TableQuery<TableStoreEntity>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionKey));
            CloudTable cloudTable = tables.Value[table.ToString()];
            IEnumerable<TableStoreEntity> retrievedResult = cloudTable.ExecuteQuery(query);
            List<T> response = new List<T>();
            foreach (TableStoreEntity entity in retrievedResult)
            {
                response.Add(entity.GetDeserializedObj<T>());
            }
            return response;
        }
        public static IEnumerable<T> GetFilteredEntitiesInAPartition<T>(TableName table, string partitionKey, string filterConditions)
            where T : TableEntity
        {
            TableQuery<TableStoreEntity> query = new TableQuery<TableStoreEntity>().Where(
                TableQuery.CombineFilters(
                    TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionKey),
                    TableOperators.And,
                    filterConditions));
            CloudTable cloudTable = tables.Value[table.ToString()];
            IEnumerable<TableStoreEntity> retrievedResult = cloudTable.ExecuteQuery(query);
            List<T> response = new List<T>();
            foreach (TableStoreEntity entity in retrievedResult)
            {
                response.Add(entity.GetDeserializedObj<T>());
            }
            return response;
        }

        public static IEnumerable<T> GetEntitiesWhereKeyInRange<T>(TableName table, string partitionKey, string greaterThanOrEqual, string lessThanOrEqual)
            where T : TableEntity
        {
            // Must break apart the query since CombineFilters only takes 3 args
            string queryInner = TableQuery.CombineFilters(
                    TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionKey),
                    TableOperators.And,
                    TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.GreaterThanOrEqual, greaterThanOrEqual));
            string queryOuter = TableQuery.CombineFilters(
                    queryInner,
                    TableOperators.And,
                    TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.LessThanOrEqual, lessThanOrEqual));
            TableQuery<TableStoreEntity> rangeQuery = new TableQuery<TableStoreEntity>().Where(queryOuter);

            CloudTable cloudTable = tables.Value[table.ToString()];
            IEnumerable<TableStoreEntity> retrievedResult = cloudTable.ExecuteQuery(rangeQuery);
            List<T> response = new List<T>();
            foreach (TableStoreEntity entity in retrievedResult)
            {
                response.Add(entity.GetDeserializedObj<T>());
            }
            return response;
        }

        public static void BatchInsertOrUpdate(TableName table, List<TableEntity> entities)
        {
            CloudTable cloudTable = tables.Value[table.ToString()];
            int rowOffset = 0;            

            while (rowOffset < entities.Count)
            {
                // limit of 100 per batch
                List<TableStoreEntity> rows = entities.Skip(rowOffset).Take(100).Select(a => new TableStoreEntity(a)).ToList();

                rowOffset += rows.Count;                

                var batch = new TableBatchOperation();

                foreach (var row in rows)
                {
                    batch.InsertOrReplace(row);
                }

                // submit
                cloudTable.ExecuteBatch(batch);
            }
        }
    }

    public class TableStoreEntity : TableEntity
    {
        public string serializedObj { get; set; }
        public TableStoreEntity()
        { }
        public TableStoreEntity(TableEntity obj)
        {
            this.PartitionKey = obj.PartitionKey;
            this.RowKey = obj.RowKey;
            serializedObj = JsonConvert.SerializeObject(obj);
        }
        public T GetDeserializedObj<T>()
        {
            return JsonConvert.DeserializeObject<T>(serializedObj);
        }
    }
}