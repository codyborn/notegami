using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using WebRole.Models;
using Microsoft.Azure.Documents.Spatial;

namespace WebRole.Controllers
{
    public class NoteController : ApiController
    {
        public class CreateNoteAction
        {
            public string Email { get; set; }
            public string AuthToken { get; set; }
            public string NoteContents { get; set; }
            public string City { get; set; }
            public float Latitude { get; set; }
            public float Longitude { get; set; }
        }
        public class UpdateNoteAction
        {
            public string Email { get; set; }
            public string AuthToken { get; set; }
            public string NoteContents { get; set; }
            public bool Completed { get; set; }
            public string City { get; set; }
            public string RowKey { get; set; }
            public float Latitude { get; set; }
            public float Longitude { get; set; }
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
            public string City { get; set; }
            public float Latitude { get; set; }
            public float Longitude { get; set; }
        }
        public class DeleteRecentTokenAction
        {
            public string Email { get; set; }
            public string AuthToken { get; set; }
            public string Token { get; set; }
        }
        public class SimpleRequestInput
        {
            public string Email { get; set; }
            public string AuthToken { get; set; }
            public float Latitude { get; set; }
            public float Longitude { get; set; }
        }
        public class CreateUpdateNoteResponse
        {
            public string Status { get; set; }
            public NoteModel Note { get; set; }
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
                        string.IsNullOrEmpty(createNote.NoteContents))
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
                    response.Note = NoteModel.AddNote(createNote.NoteContents, createNote.City, createNote.Latitude, createNote.Longitude, createNote.Email, userId);
                    LastUpdateModel.SetLastUpdate(userId);
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
                        string.IsNullOrEmpty(updateNote.RowKey))
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
                    response.Note = NoteModel.UpdateNote(userId, updateNote.RowKey, updateNote.NoteContents, updateNote.City, updateNote.Latitude, updateNote.Longitude, updateNote.Completed);
                    if (response.Note == null)
                    {
                        request.response = RequestTracker.RequestResponse.UserError;
                        // Expired AuthToken
                        response.Status = "Invalid";
                        return response;
                    }
                    LastUpdateModel.SetLastUpdate(userId);
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
                    if (!NoteModel.DeleteNote(userId, deleteNote.RowKey))
                    {
                        return "Invalid";
                    }
                    LastUpdateModel.SetLastUpdate(userId);
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
            public IEnumerable<NoteModel> Notes { get; set; }
        }

        [HttpPost]
        public QueryNotesResponse QueryNotes([FromBody]QueryNotesAction queryNotes)
        {
            QueryNotesResponse response = new QueryNotesResponse();
            response.Notes = new List<NoteModel>();

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
            response.Notes = NoteModel.QueryNotes(userId, queryNotes.QueryContents, queryNotes.City, new Point(queryNotes.Longitude, queryNotes.Latitude)).AsEnumerable();
            return response;
        }

        //[HttpPost]
        //public QueryNotesResponse GetAllNotes([FromBody]SimpleRequestInput allNotesRequest)
        //{
        //    QueryNotesResponse response = new QueryNotesResponse();
        //    response.Notes = new List<NoteModel>();

        //    if (string.IsNullOrEmpty(allNotesRequest.Email) ||
        //        string.IsNullOrEmpty(allNotesRequest.AuthToken))
        //    {
        //        response.Status = "InvalidInput";
        //        return response;
        //    }
        //    // Use the email and authToken to get the userId
        //    string userId = UserController.GetUserId(allNotesRequest.Email, allNotesRequest.AuthToken);
        //    if (string.IsNullOrEmpty(userId))
        //    {
        //        // Expired AuthToken
        //        response.Status = "Expired";
        //        return response;
        //    }
        //    response.Notes = IndexerBase.GetAllNotes(userId);
        //    response.Status = "Success";
        //    return response;
        //}


        [HttpPost]
        public Dictionary<string, IEnumerable<string>> QueryRecentTokens([FromBody]SimpleRequestInput recentNotesRequest)
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
            Dictionary<string, IEnumerable<string>> tokens = new Dictionary<string, IEnumerable<string>>();
            tokens["searchtags"] = TransactionModel.GetTagsByLocation(userId, new Point(recentNotesRequest.Longitude, recentNotesRequest.Latitude), TransactionModel.TransactionType.Search).Take(100);
            tokens["createtags"] = TransactionModel.GetTagsByLocation(userId, new Point(recentNotesRequest.Longitude, recentNotesRequest.Latitude), TransactionModel.TransactionType.Add).Take(100);
            if (tokens["searchtags"].Count() < 20)
            {
                tokens["searchtags"] = tokens["searchtags"].Union(tokens["createtags"]);
            }
            tokens["locations"] = TransactionModel.GetRecentLocations(userId);
            return tokens;
        }

        [HttpPost]
        public long? GetLastUpdate([FromBody]SimpleRequestInput getLastUpdateAction)
        {
            if (string.IsNullOrEmpty(getLastUpdateAction.Email) ||
                string.IsNullOrEmpty(getLastUpdateAction.AuthToken))
            {
                return null;
            }
            // Use the email and authToken to get the userId
            string userId = UserController.GetUserId(getLastUpdateAction.Email, getLastUpdateAction.AuthToken);
            if (string.IsNullOrEmpty(userId))
            {
                // Expired AuthToken
                return null;
            }
            DateTime? lastUpdate = LastUpdateModel.GetLastUpdate(userId);
            if (lastUpdate == null)
            {
                return null;
            }
            return lastUpdate.Value.Ticks;
        }
    }
}