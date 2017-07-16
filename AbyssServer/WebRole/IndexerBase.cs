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
        public abstract bool IsMember(string token);

        public virtual void FilterNoteKeys(ref IQueryable<NoteModel> notes, string token)
        { }
        
        /// <summary>
        /// Create a simple note only indexed on #feedback
        /// This allows developer (that's you) to still use the app like normal
        /// </summary>
        public static void CreateFeedbackNote(DateTime timestamp, string feedbackMessage)
        {
            // Only add an index for #feedback
            NoteModel.AddNote(feedbackMessage, string.Empty, 0F, 0F, string.Empty, Constant.DeveloperId);
        }

        public static IEnumerable<string> GetDistinctTokens(string noteContents)
        {
            string[] tokens = noteContents.ToLowerInvariant().Split(new char[] { ' ', '\r', '\n', ',', ';', '.', '!', '\'', '"', '+', '(', ')', '?', '' }); // contains invisible char
            // de-dupe
            HashSet<string> uniqueTokens = new HashSet<string>();
            foreach (string token in tokens)
            {
                if (!stopWords.Contains(token))
                {
                    uniqueTokens.Add(token);
                }
            }
            return uniqueTokens.AsEnumerable();
        }

        private static HashSet<string> stopWords = new HashSet<string>(){ "the", "is", "and", "a", "an", "on", "in", "that" };        
    }

    /// <summary>
    /// Indexer for single words 
    /// Ex. "hello"
    /// </summary>
    public class WordIndexer : IndexerBase
    {
        // Everything defaults to a word so be sure to check this last
        public override bool IsMember(string token)
        {
            return true;
        }
        public override void FilterNoteKeys(ref IQueryable<NoteModel> notes, string token)
        {
            // https://azure.microsoft.com/en-us/blog/searching-for-text-with-documentdb/
            notes = notes.SelectMany(n =>
                n.Words.Where(word => word == token).Select(word => n));
        }
    }


    /// <summary>
    /// Indexer for hashtags
    /// Ex. "#yolo"
    /// </summary>
    public class HashTagIndexer : IndexerBase
    {
        public override bool IsMember(string token)
        {
            return token.StartsWith("#") && !string.IsNullOrEmpty(token.Trim('#'));
        }
        public override void FilterNoteKeys(ref IQueryable<NoteModel> notes, string token)
        {
            // https://azure.microsoft.com/en-us/blog/searching-for-text-with-documentdb/
            notes = notes.SelectMany(n =>
                n.Hashtags.Where(word => word == token).Select(word => n));
        }
    }

    /// <summary>
    /// Indexer for city note was written
    /// Ex. "Seattle"
    /// </summary>
    public class LocationIndexer : IndexerBase
    {
        public override bool IsMember(string token)
        {
            return token.StartsWith("@") && !string.IsNullOrEmpty(token.Trim('@'));
        }

        public override void FilterNoteKeys(ref IQueryable<NoteModel> notes, string token)
        {
            notes = notes.Where(n => n.City.Contains(token.Trim('@')));
        }
    }

    /// <summary>
    /// Indexer for dates
    /// Ex. "2016/01/13"
    /// </summary>
    public class DateIndexer : IndexerBase
    {
        public override bool IsMember(string token)
        {
            DateTime date;
            if (!DateTime.TryParse(token, out date))
            {
                return false;
            }
            return date.Hour == 0;
        }
        
        public override void FilterNoteKeys(ref IQueryable<NoteModel> notes, string token)
        {
            DateTime queryDate = DateTime.Parse(token).Date;
            notes = notes.Where(n => n.CreatedTime >= queryDate &&
                             n.CreatedTime < queryDate.AddDays(1));
        }
    }

    /// <summary>
    /// This feature isn't needed immediately since it's read-only (no extra data stored)
    /// Indexer for date ranges
    /// Ex. "2016/01/13-2016/05/06"
    /// </summary>
    public class DateRangeIndexer : IndexerBase
    {
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

        public override void FilterNoteKeys(ref IQueryable<NoteModel> notes, string token)
        {
            // A date range only has two dates separated by a dash
            string[] potentialDates = token.Split(new char[] { '-' });
            // We should only be getting row key if IsMember (and thus proper datetime)
            // therefore we bubble up exception if cannot Parse
            DateTime time1 = DateTime.Parse(potentialDates[0]);
            DateTime time2 = DateTime.Parse(potentialDates[1]);
            // Decide order
            if (time1.CompareTo(time2) >= 0)
            {
                var t = time1;
                time1 = time2;
                time2 = t;
            }
            // Add one day to the second day to inclue the entire day
            notes = notes.Where<NoteModel>(n => n.LastUpdatedTime >= time1).Where(n => n.LastUpdatedTime <= time2.AddDays(2));
        }
    }
}