using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebRole;
using WebRole.Models;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using WebRole.Controllers;
using WebRole.Models;

namespace AbyssTest
{
    [TestClass]
    public class WebApiTests
    {
        [TestMethod]
        public void UserSignup()
        {
            // User created
            var createUserRequest = new UserController.CreateUserRequest();
            createUserRequest.Email = Guid.NewGuid().ToString("N") + "@test.com";
            createUserRequest.Password = Guid.NewGuid().ToString("N");
            UserController.CreateUserResponse response = new UserController().CreateUser(createUserRequest);
            Assert.IsTrue(response.Error == "Success");
            Assert.IsFalse(string.IsNullOrEmpty(response.Token));

            // User authed
            bool tokenIsValid = new UserController().AuthTokenValid(new UserController.AuthAttempt
            {
                Email = createUserRequest.Email,
                AuthToken = response.Token
            });
            Assert.IsTrue(tokenIsValid);
            tokenIsValid = new UserController().AuthTokenValid(new UserController.AuthAttempt
            {
                Email = createUserRequest.Email,
                AuthToken = "wrong"
            });
            Assert.IsFalse(tokenIsValid);

            // Login 
            string loginSuccess = new UserController().AuthUser(new UserController.CreateUserRequest
            {
                Email = createUserRequest.Email,
                Password = createUserRequest.Password
            });
            Assert.IsFalse(string.IsNullOrEmpty(loginSuccess));

            string loginFailure = new UserController().AuthUser(new UserController.CreateUserRequest
            {
                Email = createUserRequest.Email,
                Password = "notthepassword"
            });
            Assert.IsTrue(string.IsNullOrEmpty(loginFailure));
        }

        [TestMethod]
        public void NoteManagement()
        {
            // User created
            var createUserRequest = new UserController.CreateUserRequest();
            createUserRequest.Email = Guid.NewGuid().ToString("N") + "@test.com";
            createUserRequest.Password = Guid.NewGuid().ToString("N");
            UserController.CreateUserResponse response = new UserController().CreateUser(createUserRequest);
            Assert.IsTrue(response.Error == "Success");
            Assert.IsFalse(string.IsNullOrEmpty(response.Token));

            // Get Last Update Time
            var lastUpdateQuery = new NoteController.SimpleRequestInput();
            lastUpdateQuery.Email = createUserRequest.Email;
            lastUpdateQuery.AuthToken = response.Token;
            long? lastUpdate = new NoteController().GetLastUpdate(lastUpdateQuery);
            Assert.IsNotNull(lastUpdate);

            // Get Recent tokens
            var recentTokenQuery = new NoteController.SimpleRequestInput();
            recentTokenQuery.Email = createUserRequest.Email;
            recentTokenQuery.AuthToken = response.Token;
            var tokens = new NoteController().QueryRecentTokens(recentTokenQuery);
            Assert.IsTrue(tokens.ContainsKey("searchtags"));
            Assert.IsTrue(tokens.ContainsKey("createtags"));
            Assert.IsTrue(tokens.ContainsKey("locations"));
            Assert.IsTrue(tokens["searchtags"].Count() == 3);
            Assert.IsTrue(tokens["createtags"].Count() > 0);
            Assert.IsTrue(tokens["locations"].Count() == 0);
            var createtags1 = tokens["createtags"].Count();

            // Invalid token - Query
            var queryNotes = new NoteController.QueryNotesAction();
            queryNotes.Email = createUserRequest.Email;
            queryNotes.AuthToken = "badToken";
            queryNotes.QueryContents = "#tutorial";
            NoteController.QueryNotesResponse queryResponse = new NoteController().QueryNotes(queryNotes);
            Assert.AreEqual("Expired", queryResponse.Status);
            
            // Correct token - Query
            queryNotes.AuthToken = response.Token;
            queryNotes.QueryContents = "#tutorial";
            queryResponse = new NoteController().QueryNotes(queryNotes);
            Assert.AreEqual("Success", queryResponse.Status);
            Assert.IsTrue(queryResponse.Notes.Count() > 0);


            queryNotes.AuthToken = response.Token;
            queryNotes.QueryContents = $"{DateTime.UtcNow.AddDays(-1).ToString("yyyy/MM/dd")}-{DateTime.UtcNow.ToString("yyyy/MM/dd")}";
            queryResponse = new NoteController().QueryNotes(queryNotes);
            Assert.AreEqual("Success", queryResponse.Status);
            Assert.IsTrue(queryResponse.Notes.Count() > 0);

            // Get Recent tokens
            tokens = new NoteController().QueryRecentTokens(recentTokenQuery);
            Assert.IsTrue(tokens.ContainsKey("searchtags"));
            Assert.IsTrue(tokens.ContainsKey("createtags"));
            Assert.IsTrue(tokens.ContainsKey("locations"));
            Assert.IsTrue(tokens["searchtags"].Count() == 3);
            Assert.IsTrue(tokens["createtags"].Count() > 0);
            Assert.IsTrue(tokens["locations"].Count() == 0);         

            // Invalid token - Create
            var createNote = new NoteController.CreateNoteAction();
            createNote.Email = createUserRequest.Email;
            createNote.AuthToken = "badToken";
            createNote.NoteContents = "#test test note";
            createNote.City = "Bellevue";
            createNote.Latitude = 47.610150F;
            createNote.Longitude = -122.201516F;
            var createNoteResponse = new NoteController().CreateNote(createNote);
            Assert.AreEqual("Expired", createNoteResponse.Status);

            // Correct token - Create
            createNote.AuthToken = response.Token;
            createNoteResponse = new NoteController().CreateNote(createNote);
            Assert.AreEqual("Success", createNoteResponse.Status);

            // Note creation updates timestamp
            var lastUpdate2 = new NoteController().GetLastUpdate(lastUpdateQuery);
            Assert.IsNotNull(lastUpdate2 > lastUpdate);

            // Get Recent tokens
            tokens = new NoteController().QueryRecentTokens(recentTokenQuery);
            Assert.IsTrue(tokens.ContainsKey("searchtags"));
            Assert.IsTrue(tokens.ContainsKey("createtags"));
            Assert.IsTrue(tokens.ContainsKey("locations"));
            Assert.AreEqual(tokens["searchtags"].Count(), 4);
            Assert.IsTrue(tokens["createtags"].Count() > createtags1);
            Assert.IsTrue(tokens["locations"].Count() == 1);
            Assert.AreEqual(tokens["locations"].First(), "Bellevue");

            // Query new note
            queryNotes.QueryContents = "#test";
            queryResponse = new NoteController().QueryNotes(queryNotes);
            Assert.AreEqual("Success", queryResponse.Status);
            Assert.IsTrue(queryResponse.Notes.Count() == 1);
            Assert.AreEqual(queryResponse.Notes.First().GetNoteContents(), createNote.NoteContents);
            Assert.IsFalse(queryResponse.Notes.First().Completed);
            NoteModel queriedNote = queryResponse.Notes.First();
            
            // Note query does not update timestamp
            var lastUpdate3 = new NoteController().GetLastUpdate(lastUpdateQuery);
            Assert.IsNotNull(lastUpdate3 == lastUpdate2);
            
            // Get Recent tokens
            tokens = new NoteController().QueryRecentTokens(recentTokenQuery);
            Assert.IsTrue(tokens.ContainsKey("searchtags"));
            Assert.IsTrue(tokens.ContainsKey("createtags"));
            Assert.IsTrue(tokens.ContainsKey("locations"));
            Assert.IsTrue(tokens["searchtags"].Count() == 4);
            Assert.IsTrue(tokens["createtags"].Count() > createtags1);
            Assert.IsTrue(tokens["locations"].Count() == 1);

            // Incorrect id - Update note
            var updateNote = new NoteController.UpdateNoteAction();
            updateNote.Email = createUserRequest.Email;
            updateNote.AuthToken = response.Token;
            updateNote.RowKey = "invalidID";
            updateNote.Completed = true;
            updateNote.NoteContents = "#test updated note";
            var updateResponse = new NoteController().UpdateNote(updateNote);
            Assert.AreEqual("Invalid", updateResponse.Status);

            // Correct id - Update note
            updateNote = new NoteController.UpdateNoteAction();
            updateNote.Email = createUserRequest.Email;
            updateNote.AuthToken = response.Token;
            updateNote.RowKey = queriedNote.Id;
            updateNote.Completed = true;
            updateNote.NoteContents = "#test updated note";
            updateResponse = new NoteController().UpdateNote(updateNote);
            Assert.AreEqual("Success", updateResponse.Status);
            Assert.AreEqual(queriedNote.Id, updateResponse.Note.Id);
            Assert.AreEqual(updateNote.NoteContents, updateResponse.Note.GetNoteContents());
            Assert.IsTrue(updateResponse.Note.Completed);
            Assert.IsTrue(updateResponse.Note.LastUpdatedTime > queriedNote.LastUpdatedTime);

            // Update note updates timestamp
            var lastUpdate4 = new NoteController().GetLastUpdate(lastUpdateQuery);
            Assert.IsNotNull(lastUpdate4 > lastUpdate3);

            // Incorrect id - Delete note
            var deleteNote = new NoteController.DeleteNoteAction();
            deleteNote.Email = createUserRequest.Email;
            deleteNote.AuthToken = response.Token;
            deleteNote.RowKey = "Invalid";
            var deleteResponse = new NoteController().DeleteNote(deleteNote);
            Assert.AreEqual("Invalid", deleteResponse);

            // Correct id - Delete note
            deleteNote = new NoteController.DeleteNoteAction();
            deleteNote.Email = createUserRequest.Email;
            deleteNote.AuthToken = response.Token;
            deleteNote.RowKey = updateResponse.Note.Id;
            deleteResponse = new NoteController().DeleteNote(deleteNote);
            Assert.AreEqual("Success", deleteResponse);

            // Delete note updates timestamp
            var lastUpdate5 = new NoteController().GetLastUpdate(lastUpdateQuery);
            Assert.IsNotNull(lastUpdate5 > lastUpdate4);

            // Get Recent tokens
            tokens = new NoteController().QueryRecentTokens(recentTokenQuery);
            Assert.IsTrue(tokens.ContainsKey("searchtags"));
            Assert.IsTrue(tokens.ContainsKey("createtags"));
            Assert.IsTrue(tokens.ContainsKey("locations"));
            Assert.IsTrue(tokens["searchtags"].Count() == 4);
            Assert.IsTrue(tokens["createtags"].Count() == createtags1);
            Assert.IsTrue(tokens["locations"].Count() == 0);
        }
    }
}
