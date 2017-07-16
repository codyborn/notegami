using System;
using System.Collections.Generic;
using System.Web;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Microsoft.Azure.Documents.Spatial;

namespace WebRole.Models
{
    public class NoteModel : Model
    {
        [JsonProperty("encodedNote")]
        public string EncodedNote;

        [JsonProperty("location")]
        public Point Location;

        [JsonProperty("city")]
        public HashSet<string> City;

        [JsonProperty("hashtags")]
        public List<string> Hashtags;

        [JsonProperty("words")]
        public List<string> Words;

        [JsonProperty("completed")]
        public bool Completed;

        [JsonConverter(typeof(IsoDateTimeConverter))]
        [JsonProperty("createdTime")]
        public DateTime CreatedTime;

        [JsonConverter(typeof(IsoDateTimeConverter))]
        [JsonProperty("lastUpdatedTime")]
        public DateTime LastUpdatedTime;

        public NoteModel()
        { }
        
        public NoteModel(string content, string city, float latitude, float longitude, string email, string userId)
        {
            Id = string.Concat("note_", DateTime.UtcNow.Ticks.ToString());
            UserId = userId;
            IEnumerable<string> removedTags;
            IEnumerable<string> newTags;
            SetNoteContents(content, out removedTags, out newTags);
            foreach(string tag in newTags)
            {
                TransactionModel.AddTransaction(userId, TransactionModel.TransactionType.Add, tag, city, latitude, longitude, Id);
            }
            CreatedTime = DateTime.UtcNow;
            LastUpdatedTime = CreatedTime;
            City = new HashSet<string>();
            if (!string.IsNullOrEmpty(city))
            {
                City.Add(city.ToLowerInvariant());
            }
            Completed = false;
            Location = new Point(longitude, latitude);
            // Performs special actions on note if applicable
            new ActionRunner().RunActions(email, DateTime.UtcNow, content, city);
        }

        public static NoteModel AddNote(string content, string city, float latitude, float longitude, string email, string userId)
        {
            NoteModel note = new NoteModel(content, city, latitude, longitude, email, userId);
            if (CosmosDBClient.Insert(note))
            {
                return note;
            }
            return null;
        }

        public void SetNoteContents(string content, out IEnumerable<string> removedTags, out IEnumerable<string> newTags)
        {
            EncodedNote = HttpUtility.UrlEncode(content);
            // keep track of which tags we've seen to remove the deleted ones from the Transactions
            HashSet<string> leftoverTags = new HashSet<string>();
            if (this.Hashtags != null)
            {
                leftoverTags = new HashSet<string>(this.Hashtags);
            }
            // keep track of new tags to add to Transactions
            newTags = new List<string>();
            this.Hashtags = new List<string>();
            this.Words = new List<string>();
            IEnumerable<string> uniqueTokens = IndexerBase.GetDistinctTokens(content);
            foreach (string token in uniqueTokens)
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }
                if (new HashTagIndexer().IsMember(token))
                {
                    this.Hashtags.Add(token);
                    if (!leftoverTags.Contains(token))
                    {
                        ((List<string>)newTags).Add(token);
                    }
                    else
                    {
                        leftoverTags.Remove(token);
                    }
                }
                else // default is a word
                {
                    this.Words.Add(token);
                }
            }
            removedTags = leftoverTags.AsEnumerable();
        }

        public string GetNoteContents()
        {
            return HttpUtility.UrlDecode(EncodedNote);
        }

        /// <summary>
        /// Updates the note and cleans up removed tags
        /// </summary>
        public static NoteModel UpdateNote(string userId, string noteId, string noteContents, string city, float latitude, float longitude, bool completed)
        {
            NoteModel note = CosmosDBClient.Query<NoteModel>()
                .Where(n => n.UserId == userId).Where(n => n.Id == noteId).AsEnumerable().FirstOrDefault();
            if (note == default(NoteModel))
            {
                return null;
            }
            IEnumerable<string> removedTags;
            IEnumerable<string> newTags;
            note.SetNoteContents(noteContents, out removedTags, out newTags);
            // cleanup any removed tags (vast majority of the time user will not remove tags)
            foreach(string tag in removedTags)
            {
                TransactionModel.RemoveTransaction(userId, noteId, tag);
            }
            foreach (string tag in newTags)
            {
                TransactionModel.AddTransaction(userId, TransactionModel.TransactionType.Add, tag, city, latitude, longitude, noteId);
            }
            note.LastUpdatedTime = DateTime.UtcNow;
            if (note.City == null)
            {
                note.City = new HashSet<string>();
            }
            if (!string.IsNullOrEmpty(city))
            {
                note.City.Add(city);
            }
            note.Completed = completed;
            if (!CosmosDBClient.Update(note))
            {
                return null;
            }
            return note;
        }

        /// <summary>
        /// Deletes the note and cleans up the transactions
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="noteId"></param>
        public static bool DeleteNote(string userId, string noteId)
        {
            IEnumerable<TransactionModel> transactions = TransactionModel.GetTagsByNoteId(userId, noteId);
            //Task<bool>[] deleteRequests = new Task<bool>[transactions.Count() + 1];
            //int writeIndex = 0;
            bool success = true;
            foreach (TransactionModel tranny in transactions)
            {
                 success &= CosmosDBClient.Delete(tranny.Id, userId);
            }
            success &= CosmosDBClient.Delete(noteId, userId);
            return success;
            // Wait for all to complete and combine results
            //bool[] results = Task.WhenAll(deleteRequests.ToArray()).Result;
            //return results.Aggregate((workingBool, currBool) => workingBool && currBool);
        }

        /// <summary>
        /// Queries CosmosDB for each of the token types
        /// sorts results based on location (if provided) and date
        /// </summary>
        public static IEnumerable<NoteModel> QueryNotes(string userId, string queryContents, string city, Point userLocation = null)
        {
            List<string> uniqueTokens = IndexerBase.GetDistinctTokens(queryContents).ToList();
            if (uniqueTokens.Count == 0)
            {
                return new List<NoteModel>();
            }
            IQueryable<NoteModel> notes = CosmosDBClient.Query<NoteModel>().Where(n => n.UserId == userId);
            foreach (string token in uniqueTokens)
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }
                // Check each Indexer for membership
                if (new HashTagIndexer().IsMember(token))
                {
                    new HashTagIndexer().FilterNoteKeys(ref notes, token);
                    TransactionModel.AddTransaction(userId, TransactionModel.TransactionType.Search, token, city, userLocation);
                    continue; // index membership is disjoint
                }
                // Check each Indexer for membership
                if (new LocationIndexer().IsMember(token))
                {
                    new LocationIndexer().FilterNoteKeys(ref notes, token);
                    continue; // index membership is disjoint
                }
                else if (new DateIndexer().IsMember(token))
                {
                    new DateIndexer().FilterNoteKeys(ref notes, token);
                    continue; // index membership is disjoint
                }
                else if (new DateRangeIndexer().IsMember(token))
                {
                    // Date range is an exception where it's not stored as an actual index
                    // but instead a range of indices
                    new DateRangeIndexer().FilterNoteKeys(ref notes, token);
                    continue; // index membership is disjoint
                }
                else
                {
                    // Allow user to forget to add '#' when querying hashtags
                    //new HashTagIndexer().FilterNoteKeys(ref notes, "#" + token);

                    // Word is always the default token
                    new WordIndexer().FilterNoteKeys(ref notes, token);
                }
            }
            return notes;
            //// Build the query based on the tokens
            //if (userLocation != null)
            //{

            //    return notes.OrderByDescending(n => n.Location.Distance(userLocation))
            //                .ThenBy(n => n.LastUpdatedTime);
            //}
            //return notes.OrderByDescending(n => n.LastUpdatedTime);
        }
    }
}