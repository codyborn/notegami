using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using WebRole.Models;

namespace WebRole
{
    public class CosmosDBClient
    {
        private const string EndpointUrl = "https://notegami.documents.azure.com:443/";
        private const string DataBaseName = "primary";
        private const string Collection = "NoteCollection";
        private const string DBPrimaryKey = "id";
        private const string DBPartitionKey = "/userId";
        private static string primaryKey = Utils.GetConfigValue("DocDBStorageConnectionString");
        private static Lazy<DocumentClient> client = new Lazy<DocumentClient>(() => CreateClient());
        private static Uri docUri = UriFactory.CreateDocumentCollectionUri(DataBaseName, Collection);

        public static DocumentClient Instance()
        {
            return client.Value;
        }

        private static DocumentClient CreateClient()
        {
            DocumentClient client = new DocumentClient(new Uri(EndpointUrl), primaryKey);
            bool r = CreateDBIfNotExist(client);
            return client;
        }

        private static bool CreateDBIfNotExist(DocumentClient client)
        {
            DocumentCollection myCollection = new DocumentCollection();
            myCollection.Id = Collection;
            myCollection.PartitionKey.Paths.Add(DBPartitionKey);
            myCollection.IndexingPolicy = new IndexingPolicy(new RangeIndex(DataType.String) { Precision = -1 });
            myCollection.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath
                { Path = "/encodedNote/*" }
            );

            var cr = client.CreateDatabaseIfNotExistsAsync(new Database
            { Id = DataBaseName }
            ).Result;

            var cd = client.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri(DataBaseName),
                myCollection,
                new RequestOptions { OfferThroughput = 400 }).Result;
            return true;
        }

        public static bool Insert(Model model)
        {
            ResourceResponse<Document> response = client.Value.CreateDocumentAsync(
                    UriFactory.CreateDocumentCollectionUri(DataBaseName, Collection),
                    model).Result;
            return response.StatusCode == System.Net.HttpStatusCode.Created;
        }

        public static IOrderedQueryable<T> Query<T>(bool limitOne = false, bool crossPartition = false)
        {
            if (limitOne)
            {
                return CosmosDBClient.Instance().CreateDocumentQuery<T>(docUri, new FeedOptions() { MaxItemCount = 1, EnableCrossPartitionQuery = crossPartition });
            }
            return CosmosDBClient.Instance().CreateDocumentQuery<T>(docUri, new FeedOptions() { EnableCrossPartitionQuery = crossPartition });
        }

        public static bool Update(Model model)
        {
            var response = CosmosDBClient.Instance().ReplaceDocumentAsync(
                UriFactory.CreateDocumentUri(DataBaseName, Collection, model.Id), model
                ).Result;
            return response.StatusCode == System.Net.HttpStatusCode.OK;
        }

        public static bool InsertOrReplace(Model model)
        {
            var response = CosmosDBClient.Instance().UpsertDocumentAsync(
                UriFactory.CreateDocumentCollectionUri(DataBaseName, Collection), model
                ).Result;
            return response.StatusCode == System.Net.HttpStatusCode.OK;
        }

        public static bool Delete(string id, string userId)
        {
            try
            {
                ResourceResponse<Document> response = CosmosDBClient.Instance().DeleteDocumentAsync(
                    UriFactory.CreateDocumentUri(DataBaseName, Collection, id),
                    new RequestOptions { PartitionKey = new PartitionKey(userId) }
                    ).Result;
                return response.StatusCode == System.Net.HttpStatusCode.NoContent;
            }
            catch (Exception e)
            {
                Console.Write(e.Message);
                return false;
            }
        }
    }
}