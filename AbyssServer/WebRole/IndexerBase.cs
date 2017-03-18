using System;
using System.Collections.Generic;
using System.Web;
using WebRole.Models;
using System.Linq;
using System.Threading.Tasks;
using System.Collections;
using Microsoft.WindowsAzure.Storage.Table;

namespace WebRole
{
    /// <summary>
    /// Cheap passthrough objects
    /// Each indexer is responsible for recognizing, storing, 
    ///     and querying its set of tokens
    /// Index has a list of note keys for quick lookup
    /// Note has a list of indices for cleanup when delete/updating note
    /// </summary>
    public abstract class IndexerBase
    {
        protected virtual bool ShouldTrackRecent()
        {
            return false;
        }
        protected string GetRowKeyPrefix()
        {
            return GetIndexType().ToString().ToLowerInvariant();
        }
        protected abstract IndexType GetIndexType();
        public abstract bool IsMember(string token);

        public virtual string GetRowKey(string token)
        {
            string encodedToken = HttpUtility.UrlEncode(token.ToLowerInvariant());
            return string.Format("{0}_{1}", GetRowKeyPrefix(), encodedToken);
        }
        public virtual string GetTokenFromRowKey(string rowKey)
        {
            string encodedToken = rowKey.Replace(string.Format("{0}_", GetRowKeyPrefix()), string.Empty);
            return HttpUtility.UrlDecode(encodedToken);
        }
        public virtual string GetRecentTokenRowKey()
        {
            return string.Format("{0}_r", GetRowKeyPrefix());
        }

        /// <summary>
        /// Adds the note ref to the index's list
        /// Adds the index ref to the note's list
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="token"></param>
        /// <param name="note"></param>              
        /// <param name="shouldTrackRecent">In the case that the note is used for messaging, don't add tokens to recent</param>
        public virtual TableEntity AddNote(string userId, string token, Note note, bool shouldTrackRecent, List<RecentTokenList> recentTokenList = null)
        {
            string rowKey = GetRowKey(token);
            Index index;
            
            // Add the index to the note for cleanup in case of update/delete
            note.Indices.Add(new IndexIndex { IndexKey = rowKey, Type = GetIndexType() });
            
            if (!TableStore.Get<Index>(TableStore.TableName.indices, userId, rowKey, out index))
            {
                index = new Index();
                index.PartitionKey = userId;
                index.RowKey = rowKey;
                index.NoteKeys = new List<string>();
                index.NoteKeys.Add(note.RowKey);
            }
            else
            {
                index.NoteKeys.Add(note.RowKey);
            }

            if (ShouldTrackRecent() && shouldTrackRecent)
            {
                AddTokenToRecent(recentTokenList, userId, token);
            }
            return index;
        }

        public virtual void RemoveNote(string userId, string token, string noteKey)
        {
            string rowKey = GetRowKey(token);
            Index index;
            if (!TableStore.Get<Index>(TableStore.TableName.indices, userId, rowKey, out index))
            {
                return;
            }
            else
            {
                index.NoteKeys.Remove(noteKey);
                TableStore.Update(TableStore.TableName.indices, index);
            }
        }

        public static TableEntity NoteRemoverHelper(string userId, string indexKey, string noteKey, IndexType type)
        {
            Index index;
            if (!TableStore.Get<Index>(TableStore.TableName.indices, userId, indexKey, out index))
            {
                return null;
            }
            else
            {
                index.NoteKeys.Remove(noteKey);
                return index;
            }
        }

        public virtual IEnumerable<string> QueryNoteKeys(string userId, string token)
        {
            string rowKey = GetRowKey(token);
            Index index;
            if (!TableStore.Get<Index>(TableStore.TableName.indices, userId, rowKey, out index))
            {
                return new List<string>();
            }
            return index.NoteKeys;
        }


        /// <summary>
        /// Adds the token to the user's recently used list
        /// Removes expired list items
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="token"></param>
        private void AddTokenToRecent(List<RecentTokenList> recentTokenLists, string userId, string token)
        {
            string rowKey = GetRecentTokenRowKey();
            RecentTokenList recentTokenListOfInterest = null;
            // Create the recentTokenList for this user-token if it doesn't exist
            foreach (RecentTokenList recentTokenList in recentTokenLists)
            {
                if (rowKey == recentTokenList.RowKey)
                {
                    recentTokenListOfInterest = recentTokenList;
                    break;
                }
            }
            if (recentTokenListOfInterest == null)
            {
                recentTokenListOfInterest = new RecentTokenList();
                recentTokenListOfInterest.PartitionKey = userId;
                recentTokenListOfInterest.RowKey = rowKey;
                recentTokenListOfInterest.UsageHistory = new Dictionary<string, LinkedList<RecentToken>>();
                recentTokenListOfInterest.ExpiryQueue = new Queue<string>();

                recentTokenLists.Add(recentTokenListOfInterest);
            }
            recentTokenListOfInterest.Add(token);
        }        

        /// <summary>
        /// Return the list of most used tokens for this Index type
        /// </summary>
        /// <returns>Empty list if no history</returns>
        public IEnumerable<string> GetRecentTokens(string userId)
        {
            if (!ShouldTrackRecent())
            {
                throw new NotImplementedException("Not tracking token history for this index type");
            }
            string rowKey = GetRecentTokenRowKey();
            RecentTokenList recentTokenList;
            if (!TableStore.Get<RecentTokenList>(TableStore.TableName.recentTokens, userId, rowKey, out recentTokenList))
            {
                return new List<string>();
            }
            return from pair in recentTokenList.UsageHistory
            orderby pair.Value.Count() descending
            select pair.Key;
        }

        public IEnumerable<string> DeleteRecentToken(string userId, string token)
        {
            if (!ShouldTrackRecent())
            {
                throw new NotImplementedException("Not tracking token history for this index type");
            }
            string rowKey = GetRecentTokenRowKey();
            RecentTokenList recentTokenList;
            if (!TableStore.Get<RecentTokenList>(TableStore.TableName.recentTokens, userId, rowKey, out recentTokenList))
            {
                return null;
            }
            recentTokenList.Remove(token);
            TableStore.Update(TableStore.TableName.recentTokens, recentTokenList);

            return from pair in recentTokenList.UsageHistory
                   orderby pair.Value.Count() descending
                   select pair.Key;
        }

        public static IEnumerable<Note> QueryNotes(string userId, string queryContents)
        {
            // Get Note row keys that match token filters
            HashSet<string> noteKeys = TokenizeAndQuery(queryContents, userId);

            // In the case that we're querying a small number of notes (~1)
            // it's more efficient to query for just those notes directly
            // else it's more efficient to query all notes for a given user and 
            // search through them for the notes of interest
            int noteCount = 0;
            bool queryIndividually = true;
            // Use this approach to opposed to .Count to prevent total IEnum iteration
            foreach (string noteKey in noteKeys)
            {
                if (++noteCount >= Constant.MaxIndividualNoteQueryCount)
                {
                    queryIndividually = false;
                    break;
                }
            }
            if (queryIndividually)
            {
                List<Note> notes = new List<Note>();
                foreach (string noteKey in noteKeys)
                {
                    Note queriedNote;
                    if (!TableStore.Get<Note>(TableStore.TableName.notes, userId, noteKey, out queriedNote))
                    {
                        throw new Exception("Cannot locate a note in the index:" + noteKey);
                    }
                    notes.Add(queriedNote);
                }
                return RemoveIndicesFromResponse(notes);
            }
            else
            {
                IEnumerable<Note> allNotes = TableStore.GetAllEntitiesInAPartition<Note>(TableStore.TableName.notes, userId);
                IEnumerable<Note> notesOfInterest = allNotes.Where<Note>((n) => noteKeys.Contains(n.RowKey));
                return RemoveIndicesFromResponse(notesOfInterest);
            }
        }

        public static IEnumerable<Note> GetAllNotes(string userId)
        {
            IEnumerable<Note> allNotes = TableStore.GetAllEntitiesInAPartition<Note>(TableStore.TableName.notes, userId).Where(n => n.EncodedNote != null);
            return RemoveIndicesFromResponse(allNotes);
        }

        /// <summary>
        /// Cleanup the indices from the response
        /// </summary>
        private static IEnumerable<Note> RemoveIndicesFromResponse(IEnumerable<Note> notes)
        {
            List<Note> cleanedNotes = new List<Note>();
            foreach (Note n in notes)
            {
                n.Indices = null;                
                cleanedNotes.Add(n);
            }
            return cleanedNotes;
        }

        /// <summary>
        /// Separated out for testing w/o worrying about auth
        /// Creates note in DB and indexes tokens
        /// </summary>
        public static Note CreateNote(string userId, int utcOffset, string noteContents, string location, string email)
        {
            Note note = new Note();
            note.RowKey = Note.GenerateRowKey();
            note.PartitionKey = userId;
            note.SetNoteContents(noteContents);
            note.Timestamp = DateTime.UtcNow;
            note.Location = location;
            note.Indices = new HashSet<IndexIndex>();

            // pass in the users local date for indexing
            DateTime localTime = DateTime.UtcNow.AddMinutes(-utcOffset);

            // IndexNote will add all the index keys to the note
            IndexNote(noteContents, localTime, location, userId, note);

            // Performs special actions on note if applicable
            new ActionRunner().RunActions(email, DateTime.UtcNow, noteContents, location);

            TableStore.Set(TableStore.TableName.notes, note);
            return note;
        }

        /// <summary>
        /// Create a simple note only indexed on #feedback
        /// This allows developer (that's you) to still use the app like normal
        /// </summary>
        public static void CreateFeedbackNote(DateTime timestamp, string feedbackMessage)
        {
            string userId = Constant.DeveloperId;

            Note note = new Note();
            note.RowKey = Note.GenerateRowKey();
            note.PartitionKey = userId;
            note.SetNoteContents(feedbackMessage);
            note.Timestamp = timestamp;
            note.Indices = new HashSet<IndexIndex>();

            List<TableEntity> insertUpdateList = new List<TableEntity>();
            // Only add an index for #feedback
            insertUpdateList.Add(new HashTagIndexer().AddNote(userId, "#feedback", note, shouldTrackRecent:false));

            TableStore.BatchInsertOrUpdate(TableStore.TableName.indices, insertUpdateList);

            TableStore.Set(TableStore.TableName.notes, note);
        }

        /// <summary>
        /// Locates previous note and updates indices and note
        /// </summary>
        /// <returns>false if note cannot be found</returns>
        public static Note UpdateNote(string userId, int utcOffset, string noteKey, string noteContents, string location, bool completed)
        {
            // If note doesn't exist, fail
            Note retrievedNote = null;
            if (!TableStore.Get<Note>(TableStore.TableName.notes, userId, noteKey, out retrievedNote))
            {
                return null;
            }
            // Check note's indexindex list for backwards compatibility
            if (retrievedNote.Indices == null)
            {
                retrievedNote.Indices = new HashSet<IndexIndex>();
            }
            DateTime localTime = DateTime.UtcNow.AddMinutes(-utcOffset);
            retrievedNote.Timestamp = DateTime.UtcNow;
            // Keep location and date indices; only remove the contents
            RemoveContentIndices(retrievedNote, userId);

            retrievedNote.SetNoteContents(noteContents);
            // Reindex note
            IndexNote(noteContents, localTime, location, userId, retrievedNote);
            retrievedNote.Completed = completed;
            TableStore.Update(TableStore.TableName.notes, retrievedNote);
            return retrievedNote;
        }

        /// <summary>
        /// Deletes note and cleans up all indices
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="noteKey"></param>
        /// <param name="recentTokensUpdated">true if delete caused an update to recent token list</param>
        /// <returns>true if successfully deleted</returns>
        public static bool DeleteNote(string userId, string noteKey, out bool recentTokensUpdated)
        {
            // If note doesn't exist, fail
            recentTokensUpdated = false;
            Note retrievedNote = null;
            if (!TableStore.Get<Note>(TableStore.TableName.notes, userId, noteKey, out retrievedNote))
            {
                return false;
            }
            RemoveAllIndices(retrievedNote, userId, out recentTokensUpdated);
            
            TableStore.Delete(TableStore.TableName.notes, retrievedNote);
            return true;
        }

        /// <summary>
        /// Indexes each token with the rowkey of the note for quick lookup
        /// Adds the index keys to the note for reverse search (delete note and its indices)
        /// </summary>
        private static void IndexNote(string noteContents, DateTime timestamp, string location, string userId, Note note)
        {
            // List contains all of the entities to update at once
            List<TableEntity> insertUpdateList = new List<TableEntity>();
            List<RecentTokenList> recentTokenLists = TableStore.GetAllEntitiesInAPartition<RecentTokenList>(TableStore.TableName.recentTokens, userId).ToList();
            
            if (!string.IsNullOrEmpty(location))
            {
                insertUpdateList.Add(new LocationIndexer().AddNote(userId, location, note, true, recentTokenLists));
            }
            insertUpdateList.Add(new DateIndexer().AddNote(userId, timestamp.ToString("yyyy/MM/dd"), note, true, recentTokenLists));
            IEnumerable uniqueTokens = GetDistinctTokens(noteContents);
            foreach (string token in uniqueTokens)
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }
                if (new HashTagIndexer().IsMember(token))
                {
                    insertUpdateList.Add(new HashTagIndexer().AddNote(userId, token, note, true, recentTokenLists));
                }
                else // default is a word
                {
                    insertUpdateList.Add(new WordIndexer().AddNote(userId, token, note, true, recentTokenLists));
                }
            }
            TableStore.BatchInsertOrUpdate(TableStore.TableName.indices, insertUpdateList);
            TableStore.BatchInsertOrUpdate(TableStore.TableName.recentTokens, recentTokenLists.Cast<TableEntity>().ToList());
        }

        public static IEnumerable<string> GetDistinctTokens(string noteContents)
        {
            string[] tokens = noteContents.ToLowerInvariant().Split(new char[] { ' ', '\r', '\n', ',', ';', '.', '!',  '\'', '"', '+', '(', ')', '?', '' }); // contains invisible char
            // de-dupe
            Dictionary<string, bool> uniqueTokens = new Dictionary<string, bool>();
            foreach (string token in tokens)
            {
                uniqueTokens[token] = true;
            }
            return uniqueTokens.Keys;
        }

        /// <summary>
        /// Remove HashTag and Word indices
        /// </summary>
        public static void RemoveContentIndices(Note note, string userId)
        {
            if (note.Indices == null)
            {
                return;
            }
            List<TableEntity> updateList = new List<TableEntity>();
            IndexIndex[] contentIndices = note.Indices.Where(i => i.Type == IndexType.HashTag || i.Type == IndexType.Word).ToArray();
            foreach (IndexIndex index in contentIndices)
            {
                TableEntity update = NoteRemoverHelper(userId, index.IndexKey, note.RowKey, index.Type);
                note.Indices.Remove(index);
                if (update != null)
                {
                    updateList.Add(update);
                }
            }
            TableStore.BatchInsertOrUpdate(TableStore.TableName.indices, updateList);
        }
        public static void RemoveAllIndices(Note note, string userId, out bool recentTokensUpdated)
        {
            recentTokensUpdated = false;
            if (note.Indices == null)
            {
                return;
            }
            List<TableEntity> updateList = new List<TableEntity>();
            foreach (IndexIndex index in note.Indices)
            {
                TableEntity update = NoteRemoverHelper(userId, index.IndexKey, note.RowKey, index.Type);
                if (update != null)
                {
                    updateList.Add(update);
                    // If the hashtag has no more notes associated, remove from recents
                    if (index.Type == IndexType.HashTag)
                    {
                        string token = new HashTagIndexer().GetTokenFromRowKey(index.IndexKey);
                        IEnumerable<string> notes = new HashTagIndexer().QueryNoteKeys(userId, token);
                        // last one
                        if (notes.Count() == 1)
                        {
                            new HashTagIndexer().DeleteRecentToken(userId, token);
                            recentTokensUpdated = true;
                        }
                    }
                }
            }
            TableStore.BatchInsertOrUpdate(TableStore.TableName.indices, updateList);
            // It'll be deleted but clear the list for consistency's sake
            note.Indices = new HashSet<IndexIndex>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="uniqueTokens"></param>
        /// <returns></returns>
        private static HashSet<string> GetTokenIndicesFromRawTokens(IEnumerable<string> uniqueTokens, ref List<IEnumerable<string>> queryResults, string userId)
        {
            HashSet<string> queryIndexKeys = new HashSet<string>();
            foreach (string token in uniqueTokens)
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }
                // Check each Indexer for membership
                if (new HashTagIndexer().IsMember(token))
                {
                    queryIndexKeys.Add(new HashTagIndexer().GetRowKey(token));
                    continue; // index membership is disjoint
                }
                else if (new DateIndexer().IsMember(token))
                {
                    queryIndexKeys.Add(new DateIndexer().GetRowKey(token));
                    continue; // index membership is disjoint
                }
                else if (new DateRangeIndexer().IsMember(token))
                {
                    // Date range is an exception where it's not stored as an actual index
                    // but instead a range of indices
                    queryResults.Add(new DateRangeIndexer().QueryNoteKeys(userId, token));
                    continue; // index membership is disjoint
                }
                else
                {
                    // Allow user to forget to add '#' when querying hashtags
                    queryIndexKeys.Add(new HashTagIndexer().GetRowKey("#"+token));

                    // Word is always the default token
                    queryIndexKeys.Add(new WordIndexer().GetRowKey(token));
                }
            }
            return queryIndexKeys;
        }

        /// <summary>
        /// Identifies each token type and queries all notes that match for the user
        /// Performs an intersection on the sets of results of each query
        /// </summary>
        private static HashSet<string> TokenizeAndQuery(string queryContents, string userId)
        {            
            List<IEnumerable<string>> queryResults = new List<IEnumerable<string>>();
            List<string> uniqueTokens = GetDistinctTokens(queryContents).ToList();
            
            if (uniqueTokens.Count == 0)
            {
                return new HashSet<string>();
            }
            HashSet<string> queryIndexKeys = GetTokenIndicesFromRawTokens(uniqueTokens, ref queryResults, userId);            

            // Faster and cheaper to get all indices for a user and iterate through the results
            // than to query each index individually if more than a single token
            if (queryIndexKeys.Count() == 1)
            {
                Index index;
                if (!TableStore.Get<Index>(TableStore.TableName.indices, userId, queryIndexKeys.First(), out index))
                {
                    return new HashSet<string>();
                }
                queryResults.Add(index.NoteKeys);
            }
            else
            {
                IEnumerable<Index> indices = TableStore.GetAllEntitiesInAPartition<Index>(TableStore.TableName.indices, userId);
                // Hash it once to prevent multiple iteration (likely to be a short list)
                int queryIndexKeysCount = queryIndexKeys.Count;
                foreach (Index index in indices)
                {
                    // if this is an index of interest
                    if (queryIndexKeys.Contains(index.RowKey))
                    {
                        queryResults.Add(index.NoteKeys);
                        queryIndexKeys.Remove(index.RowKey);
                        // if we've exhausted the query list, no point in continuing
                        if (queryIndexKeys.Count == 0)
                        {
                            break;
                        }
                    }
                }
            }

            // Find the intersection of each query
            if (queryResults.Count == 0)
            {
                return new HashSet<string>();
            }
            // Must contain at least one result
            HashSet<string> intersection = queryResults
            .Skip(1)
            .Aggregate(
                new HashSet<string>(queryResults.First()),
                (h, e) => { h.UnionWith(e); return h; }
            );
            return intersection;
        }

        public static Dictionary<string, IEnumerable<string>> GetAllRecentTokens(string userId)
        {
            Dictionary<string, IEnumerable<string>> tokens = new Dictionary<string, IEnumerable<string>>();
            tokens["locations"] = new LocationIndexer().GetRecentTokens(userId);
            tokens["tags"] = new HashTagIndexer().GetRecentTokens(userId);
            return tokens;
        }
    }

    /// <summary>
    /// Indexer for single words 
    /// Ex. "hello"
    /// </summary>
    public class WordIndexer : IndexerBase
    {
        protected override IndexType GetIndexType()
        {
            return IndexType.Word;
        }

        // Everything defaults to a word so be sure to check this last
        public override bool IsMember(string token)
        {
            return true;
        }
    }


    /// <summary>
    /// Indexer for hashtags
    /// Ex. "#yolo"
    /// </summary>
    public class HashTagIndexer : IndexerBase
    {
        protected override bool ShouldTrackRecent()
        {
            return true;
        }
        protected override IndexType GetIndexType()
        {
            return IndexType.HashTag;
        }

        public override bool IsMember(string token)
        {
            return token.StartsWith("#") && !string.IsNullOrEmpty(token.Trim('#'));
        }
    }

    /// <summary>
    /// Indexer for city note was written
    /// Ex. "Seattle"
    /// </summary>
    public class LocationIndexer : IndexerBase
    {
        protected override bool ShouldTrackRecent()
        {
            return true;
        }
        // Insert the location as a word, to prevent need for special characters to query location
        protected override IndexType GetIndexType()
        {
            return IndexType.Word;
        }

        public override bool IsMember(string token)
        {
            throw new NotImplementedException("LocationIndexer does not have special members");
        }        
    }

    /// <summary>
    /// Indexer for dates
    /// Ex. "2016/01/13"
    /// </summary>
    public class DateIndexer : IndexerBase
    {
        protected override IndexType GetIndexType()
        {
            return IndexType.Date;
        }

        public override bool IsMember(string token)
        {
            DateTime date;
            if (!DateTime.TryParse(token, out date))
            {
                return false;
            }
            return date.Hour == 0;
        }

        /// <summary>
        /// We must override to standardize date format
        /// </summary>
        public override string GetRowKey(string token)
        {
            // We should only be getting row key if IsMember (and thus proper datetime)
            // therefore we bubble up exception if cannot Parse
            string encodedToken = DateTime.Parse(token).ToUniversalTime().ToString("yyyyMMdd");
            return string.Format("{0}_{1}", this.GetRowKeyPrefix(), encodedToken);
        }
    }

    /// <summary>
    /// This feature isn't needed immediately since it's read-only (no extra data stored)
    /// Indexer for date ranges
    /// Ex. "2016/01/13-2016/05/06"
    /// </summary>
    public class DateRangeIndexer : IndexerBase
    {
        /// <summary>
        /// Intentionally same as DateIndexer
        /// </summary>
        protected override IndexType GetIndexType()
        {
            return IndexType.Date;
        }

        public override bool IsMember(string token)
        {
            if (!token.Contains("-"))
            {
                return false;
            }
            string[] potentialDates = token.Split(new char[] { '-' });
            // A date range only has two dates separated by a dash
            if (potentialDates.Length != 2)
            {
                return false;
            }
            foreach (string potentialDate in potentialDates)
            {
                DateTime dummy;
                if (!DateTime.TryParse(potentialDate, out dummy))
                {
                    return false;
                }
            }
            return true;
        }

        public override string GetRowKey(string token)
        {
            throw new NotImplementedException("DateRange should not be inserted or queried directly");
        }

        /// <summary>
        /// Does nothing; DateRange Index is only for querying
        /// </summary>
        public override TableEntity AddNote(string userId, string token, Note note, bool shouldTrackRecent, List<RecentTokenList> recentTokenLists)
        {
            throw new NotImplementedException("DateRange should not be inserted or queried directly");
        }

        public override IEnumerable<string> QueryNoteKeys(string userId, string token)
        {
            // A date range only has two dates separated by a dash
            string[] potentialDates = token.Split(new char[] { '-' });
            // We should only be getting row key if IsMember (and thus proper datetime)
            // therefore we bubble up exception if cannot Parse
            DateTime time1 = DateTime.Parse(potentialDates[0]);
            DateTime time2 = DateTime.Parse(potentialDates[1]);
            string rowKey1 = new DateIndexer().GetRowKey(potentialDates[0]);
            string rowKey2 = new DateIndexer().GetRowKey(potentialDates[1]);
            IEnumerable<Index> results;
            // Decide order
            if (time1.CompareTo(time2) < 0)
            {
                results = TableStore.GetEntitiesWhereKeyInRange<Index>(TableStore.TableName.indices, userId, rowKey1, rowKey2);
            }
            else
            {
                // reverse order
                results = TableStore.GetEntitiesWhereKeyInRange<Index>(TableStore.TableName.indices, userId, rowKey2, rowKey1);
            }
            
            return results.Aggregate(
                new List<string>(),
                (h, e) => { h.AddRange(e.NoteKeys); return h; }
                );
        }
    }
}