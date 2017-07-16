using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Microsoft.Azure.Documents.Spatial;

namespace WebRole.Models
{
    /// <summary>
    /// Tracks Tags from note Add and Searches
    /// Must allow for query by DateTime and location
    /// </summary>
    public class TransactionModel : Model
    {
        /// <summary>
        /// If a note is deleted or updated, must remove the previous note
        /// Remove is feature s.t. a user has a mechanism to clean up recently used tags
        /// </summary>
        [JsonProperty("noteId")]
        public string NoteId;

        [JsonConverter(typeof(IsoDateTimeConverter))]
        [JsonProperty("transactionTime")]
        public DateTime TransactionTime;

        [JsonProperty("location")]
        public Point Location;

        [JsonProperty("city")]
        public string City;

        [JsonProperty("transactionType")]
        public TransactionType Type;

        public enum TransactionType
        {
            Add,
            Search
        }

        [JsonProperty("tag")]
        public string Tag;

        public TransactionModel()
        { }

        // NoteId may be blank in case of search
        public TransactionModel(string userId, TransactionType type, string tag, string city, float latitude, float longitude, string noteId = "") 
            : this(userId, type, tag, city, new Point(longitude, latitude), noteId)
        { }

        public TransactionModel(string userId, TransactionType type, string tag, string city, Point location, string noteId = "")
        {
            if (string.IsNullOrEmpty(noteId) && type == TransactionType.Add)
            {
                throw new ArgumentNullException("Add transaction missing note");
            }
            UserId = userId;
            NoteId = noteId;
            Id = GetId(tag, DateTime.UtcNow.Ticks.ToString());
            Type = type;
            TransactionTime = DateTime.UtcNow;
            Tag = tag;
            City = city;
            Location = location;
        }

        private static string GetId(string tag, string noteId)
        {
            return string.Concat("t_", noteId, DateTime.UtcNow.Ticks.ToString());
        }

        public static bool AddTransaction(string userId, TransactionType type, string tag, string city, float latitude, float longitude, string noteId = "")
        {
            return AddTransaction(userId, type, tag, city, new Point(longitude, latitude), noteId);
        }
        public static bool AddTransaction(string userId, TransactionType type, string tag, string city, Point location, string noteId = "")
        {
            TransactionModel transaction = new TransactionModel(userId, type, tag, city, location, noteId);
            return CosmosDBClient.Insert(transaction);
        }

        public static bool RemoveTransaction(string userId, string noteId, string tag)
        {
            return CosmosDBClient.Delete(GetId(tag, noteId), userId);
        }

        public static IEnumerable<string> GetTagsByLocation(string userId, Point userLocation, TransactionType type)
        {
            var transactions = CosmosDBClient.Query<TransactionModel>().Where(t => t.UserId == userId && t.Type == type).Select(t => new { transaction = t, distance = ((int)t.Location.Distance(userLocation))/10});
            IEnumerable<string> tags = transactions.AsEnumerable().GroupBy(t => t.transaction.Tag)
                                                                  .Select(g => new { g.Key, spaceSort = g.Min(t => t.distance), countSort = g.Count() })
                                                                  .OrderBy(g => g.spaceSort)
                                                                  .ThenBy(g => g.countSort)
                                                                  .Select(t => t.Key);
            return tags;
        }

        public static IEnumerable<string> GetRecentLocations(string userId)
        {
            // TODO: Include frequency into sorting
            IQueryable<TransactionModel> transactions = CosmosDBClient.Query<TransactionModel>().Where(t => t.UserId == userId).Where(t => t.City != string.Empty).Where(t => t.City != null);
            IEnumerable<string> tags = transactions.OrderBy(t => t.TransactionTime).Select(t => t.City).AsEnumerable().Distinct();
            return tags;
        }

        public static IEnumerable<TransactionModel> GetTagsByNoteId(string userId, string noteId)
        {
            IQueryable<TransactionModel> transactions = CosmosDBClient.Query<TransactionModel>().Where(t => t.UserId == userId && t.NoteId == noteId);
            return transactions.AsEnumerable();
        }
    }
}