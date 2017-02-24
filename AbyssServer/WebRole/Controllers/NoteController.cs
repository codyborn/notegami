using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using WebRole.Models;

namespace WebRole.Controllers
{
    public class NoteController : ApiController
    {
        public class CreateNoteAction
        {
            public string Email { get; set; }
            public int? UTCOffset { get; set; }
            public string AuthToken { get; set; }
            public string NoteContents { get; set; }
            public string Location { get; set; }
        }
        public class UpdateNoteAction
        {
            public string Email { get; set; }
            public int? UTCOffset { get; set; }
            public string AuthToken { get; set; }
            public string NoteContents { get; set; }
            public bool Completed { get; set; }
            public string Location { get; set; }
            public string RowKey { get; set; }
        }
        public class DeleteNoteAction
        {
            public string Email { get; set; }
            public string TimeStamp { get; set; }
            public string AuthToken { get; set; }
            public string RowKey { get; set; }
        }
        public class QueryNotesAction
        {
            public string Email { get; set; }
            public string AuthToken { get; set; }
            public string QueryContents { get; set; }
        }
        public class RecentTokensAction
        {
            public string Email { get; set; }
            public string AuthToken { get; set; }
        }
        public class DeleteRecentTokenAction
        {
            public string Email { get; set; }
            public string AuthToken { get; set; }
            public string Token { get; set; }
        }
        public class CreateUpdateNoteResponse
        {
            public string Status { get; set; }
            public Note Note { get; set; }
        }

        [HttpPost]
        public CreateUpdateNoteResponse CreateNote([FromBody]CreateNoteAction createNote)
        {
            CreateUpdateNoteResponse response = new CreateUpdateNoteResponse();
            response.Status = string.Empty;
            if (string.IsNullOrEmpty(createNote.Email))
            {
                return response;
            }
            using (RequestTracker request = new RequestTracker(Constant.RequestAPI.CreateNote.ToString(), createNote.Email))
            {
                try
                {
                    if (string.IsNullOrEmpty(createNote.Email) ||
                        string.IsNullOrEmpty(createNote.AuthToken) ||
                        string.IsNullOrEmpty(createNote.NoteContents) ||
                        !createNote.UTCOffset.HasValue)
                    {
                        request.response = RequestTracker.RequestResponse.UserError;
                        
                        return response;
                    }
                    if (createNote.NoteContents.Length > Constant.MaxNoteLength)
                    {
                        request.response = RequestTracker.RequestResponse.UserError;

                        response.Status = "TooLong";
                        return response;
                    }
                    // Use the email and authToken to get the userId
                    string userId = UserController.GetUserId(createNote.Email, createNote.AuthToken);
                    if (string.IsNullOrEmpty(userId))
                    {
                        request.response = RequestTracker.RequestResponse.UserError;
                        // Expired AuthToken
                        response.Status = "Expired";
                        return response;
                    }
                    response.Note = IndexerBase.CreateNote(userId, (int)createNote.UTCOffset, createNote.NoteContents, createNote.Location, createNote.Email);
                    response.Status = "Success";
                    return response;
                }
                catch (Exception e)
                {
                    request.response = RequestTracker.RequestResponse.ServerError;
                    ExceptionTracker.LogException(e);
                    return response;
                }
            }
        }

        [HttpPost]
        public CreateUpdateNoteResponse UpdateNote([FromBody]UpdateNoteAction updateNote)
        {
            CreateUpdateNoteResponse response = new CreateUpdateNoteResponse();
            response.Status = string.Empty;

            if (string.IsNullOrEmpty(updateNote.Email))
            {
                return response;
            }
            using (RequestTracker request = new RequestTracker(Constant.RequestAPI.UpdateNote.ToString(), updateNote.Email))
            {
                try
                {
                    if (string.IsNullOrEmpty(updateNote.Email) ||
                        string.IsNullOrEmpty(updateNote.AuthToken) ||
                        string.IsNullOrEmpty(updateNote.NoteContents) ||
                        string.IsNullOrEmpty(updateNote.RowKey) ||
                        !updateNote.UTCOffset.HasValue)
                    {
                        request.response = RequestTracker.RequestResponse.UserError;
                        return response;
                    }
                    if (updateNote.NoteContents.Length > Constant.MaxNoteLength)
                    {
                        request.response = RequestTracker.RequestResponse.UserError;
                        response.Status = "TooLong";
                        return response;
                    }
                    // Use the email and authToken to get the userId
                    string userId = UserController.GetUserId(updateNote.Email, updateNote.AuthToken);
                    if (string.IsNullOrEmpty(userId))
                    {
                        request.response = RequestTracker.RequestResponse.UserError;
                        // Expired AuthToken
                        response.Status = "Expired";
                        return response;
                    }
                    response.Note = IndexerBase.UpdateNote(userId, (int)updateNote.UTCOffset, updateNote.RowKey, updateNote.NoteContents, updateNote.Location, updateNote.Completed);
                    response.Status = "Success";
                    return response;
                }
                catch (Exception e)
                {
                    request.response = RequestTracker.RequestResponse.ServerError;
                    ExceptionTracker.LogException(e);
                    return response;
                }
            }
        }


        [HttpPost]
        public string DeleteNote([FromBody]DeleteNoteAction deleteNote)
        {
            if (string.IsNullOrEmpty(deleteNote.Email))
            {
                return string.Empty;
            }
            using (RequestTracker request = new RequestTracker(Constant.RequestAPI.DeleteNote.ToString(), deleteNote.Email))
            {
                try
                {
                    if (string.IsNullOrEmpty(deleteNote.Email) ||
                        string.IsNullOrEmpty(deleteNote.AuthToken) ||
                        string.IsNullOrEmpty(deleteNote.RowKey))
                    {
                        request.response = RequestTracker.RequestResponse.UserError;
                        return string.Empty;
                    }
                    // Use the email and authToken to get the userId
                    string userId = UserController.GetUserId(deleteNote.Email, deleteNote.AuthToken);
                    if (string.IsNullOrEmpty(userId))
                    {
                        request.response = RequestTracker.RequestResponse.UserError;
                        // Expired AuthToken
                        return "Expired";
                    }
                    bool recentTokensUpdated;
                    IndexerBase.DeleteNote(userId, deleteNote.RowKey, out recentTokensUpdated);
                    if (recentTokensUpdated)
                    {
                        return "RefreshRecent";
                    }
                    return "Success";
                }
                catch (Exception e)
                {
                    request.response = RequestTracker.RequestResponse.ServerError;
                    ExceptionTracker.LogException(e);
                    return string.Empty;
                }
            }
        }

        private DateTime TicksToDatTime(long ticks)
        {
            DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            dtDateTime = dtDateTime.AddMilliseconds(ticks).ToLocalTime();
            return dtDateTime;
        }

        public class QueryNotesResponse
        {
            public string Status { get; set; }
            public IEnumerable<Note> Notes { get; set; }
        }

        [HttpPost]
        public QueryNotesResponse QueryNotes([FromBody]QueryNotesAction queryNotes)
        {
            QueryNotesResponse response = new QueryNotesResponse();            
            response.Notes = new List<Note>();

            if (string.IsNullOrEmpty(queryNotes.Email) ||
                string.IsNullOrEmpty(queryNotes.AuthToken) ||
                string.IsNullOrEmpty(queryNotes.QueryContents))
            {
                response.Status = "InvalidInput";
                return response;
            }
            // Use the email and authToken to get the userId
            string userId = UserController.GetUserId(queryNotes.Email, queryNotes.AuthToken);
            if (string.IsNullOrEmpty(userId))
            {
                // Expired AuthToken
                response.Status = "Expired";
                return response;
            }
            response.Status = "Success";
            response.Notes = IndexerBase.QueryNotes(userId, queryNotes.QueryContents);
            return response;
        }

        [HttpPost]
        public Dictionary<string, IEnumerable<string>> QueryRecentTokens([FromBody]RecentTokensAction recentNotesRequest)
        {
            if (string.IsNullOrEmpty(recentNotesRequest.Email) ||
                string.IsNullOrEmpty(recentNotesRequest.AuthToken))
            {
                return null;
            }
            // Use the email and authToken to get the userId
            string userId = UserController.GetUserId(recentNotesRequest.Email, recentNotesRequest.AuthToken);
            if (string.IsNullOrEmpty(userId))
            {
                // Expired AuthToken
                return null;
            }
            return IndexerBase.GetAllRecentTokens(userId);
        }

        [HttpPost]
        public Dictionary<string, IEnumerable<string>> DeleteRecentToken([FromBody]DeleteRecentTokenAction deleteRecentTokenRequest)
        {
            if (string.IsNullOrEmpty(deleteRecentTokenRequest.Email) ||
                string.IsNullOrEmpty(deleteRecentTokenRequest.AuthToken) ||
                string.IsNullOrEmpty(deleteRecentTokenRequest.Token))
            {
                return null;
            }
            // Use the email and authToken to get the userId
            string userId = UserController.GetUserId(deleteRecentTokenRequest.Email, deleteRecentTokenRequest.AuthToken);
            if (string.IsNullOrEmpty(userId))
            {
                // Expired AuthToken
                return null;
            }
            new HashTagIndexer().DeleteRecentToken(userId, deleteRecentTokenRequest.Token);
            return IndexerBase.GetAllRecentTokens(userId);
        }
    }
}