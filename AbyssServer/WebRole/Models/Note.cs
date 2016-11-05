using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Web;

namespace WebRole.Models
{
    public class Note : TableEntity
    {
        public string EncodedNote { get; set; }
        public string Location { get; set; }      
        public HashSet<IndexIndex> Indices { get; set; }
        public bool Completed { get; set; }
        
        public void SetNoteContents(string contents)
        {
            EncodedNote = HttpUtility.UrlEncode(contents);
        }
        public string GetNoteContents()
        {
            return HttpUtility.UrlDecode(EncodedNote);
        }
        // Ever incrementing unique index
        public static string GenerateRowKey()
        {
            return string.Format("note_{0}", DateTime.UtcNow.Ticks.ToString());
        }
    }
}