﻿using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;

namespace WebRole.Models
{
    /// <summary>
    /// Stored in the table to keep track of some notes
    /// </summary>
    public class Index : TableEntity
    {
        public List<string> NoteKeys { get; set; }
    }
    /// <summary>
    /// Used by Note to keep track of its indices
    /// </summary>
    public class IndexIndex
    {
        public string IndexKey;
        public IndexType Type;
        public override bool Equals(object obj)
        {
            IndexIndex ii = obj as IndexIndex;
            if (obj == null || ii == null)
            {
                return false;
            }
            return IndexKey.Equals(ii.IndexKey);
        }
        public override int GetHashCode()
        {
            return IndexKey.GetHashCode();
        }
    }
    public enum IndexType
    {
        Loc,
        Date,
        HashTag,
        Word
    }
    public class RecentTokenList : TableEntity
    {
        public const int TokenCountLimit = 1000;
        public int TokenCount { get; set; }
        public Dictionary<string, LinkedList<RecentToken>> UsageHistory { get; set; }
        public Queue<string> ExpiryQueue { get; set; }

        public void Add(string token, RecentToken.ActionType actionType)
        {
            if (!UsageHistory.ContainsKey(token))
            {
                UsageHistory[token] = new LinkedList<RecentToken>();
            }
            RecentToken recentToken = new RecentToken();
            recentToken.Type = actionType;
            recentToken.TimeOfAction = DateTime.UtcNow;
            UsageHistory[token].AddLast(recentToken);
            ExpiryQueue.Enqueue(token);
            TokenCount++;
            // Check if some should be expired
            // Since we're using a linkedlist, removal at head is efficient
            while (TokenCount > TokenCountLimit)
            {
                string tokenToRemove = ExpiryQueue.Dequeue();
                UsageHistory[tokenToRemove].RemoveFirst();
                TokenCount--;
            }
        }
    }
    public class RecentToken
    {
        public ActionType Type { get; set; }
        public DateTime TimeOfAction { get; set; }
        public enum ActionType
        {
            Insert,
            Lookup
        }
    }
}