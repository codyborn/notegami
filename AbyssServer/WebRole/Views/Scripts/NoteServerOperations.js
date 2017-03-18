
function QueryNotes(queryContents, callBackOnSuccess, isRecentNotesQuery) {
    var fromUserInput = false;
    if (MasterViewModel.noteListViewModel.editingNote != null) {
        MasterViewModel.noteListViewModel.editingNote.cancelEdit();
    }
    if (typeof queryContents == "undefined") {
        queryContents = document.getElementById("QueryContents").value;
        fromUserInput = true;
    }

    if (queryContents != "") {
        if (fromUserInput) {
            ShowLoading("QueryButton");
        }
        var email = CacheStoreGet("email");
        var authToken = CacheStoreGet("token");
        var data =
            {
                Email: email,
                AuthToken: authToken,
                QueryContents: queryContents
            };
        $.ajax({
            type: "POST",
            url: "../note/QueryNotes",
            data: data,
            success: function (response) {
                if (response.Status == "Success") {
                    if (!isRecentNotesQuery) {
                        displayingRecentNotes = false;
                    }
                    if (typeof callBackOnSuccess == "undefined") {
                        MasterViewModel.noteListViewModel.queryOccurred(true);
                        DisplayResults(response.Notes);
                    }
                    else {
                        callBackOnSuccess(response);
                    }
                }
                else if (response.Status == "Expired") {
                    // Auth token has expired
                    AuthUserAndSetCookie(email, CacheStoreGet("password"),
                            function () { QueryNotes(queryContents, callBackOnSuccess); }, // on success
                            function () {
                                Redirect('Signup.html');
                                showError("Please log in to continue");
                            }) // on failure
                }
                HideButtonImage("QueryButton");
            }
        });
    }
}


function QueryAllNotes(callBackOnSuccess) {
    var email = CacheStoreGet("email");
    var authToken = CacheStoreGet("token");
    var data =
        {
            Email: email,
            AuthToken: authToken
        };
    $.ajax({
        type: "POST",
        url: "../note/GetAllNotes",
        data: data,
        success: function (response) {
            if (response.Status == "Success") {
                callBackOnSuccess(response);
            }
            else if (response.Status == "Expired") {
                // Auth token has expired
                AuthUserAndSetCookie(email, CacheStoreGet("password"),
                        function () { QueryAllNotes(callBackOnSuccess); }, // on success
                        function () {
                            Redirect('Signup.html');
                            showError("Please log in to continue");
                        }) // on failure
            }
        }
    });
}


function GetLastUpdateTime(callBackOnSuccess) {
    var email = CacheStoreGet("email");
    var authToken = CacheStoreGet("token");
    var data =
        {
            Email: email,
            AuthToken: authToken
        };
    $.ajax({
        type: "POST",
        url: "../note/GetLastUpdate",
        data: data,
        success: function (response) {            
            callBackOnSuccess(response);
        }
    });
}

// Displays recent tokens as quick search links
function QueryRecentTokens() {
    var email = CacheStoreGet("email");
    var authToken = CacheStoreGet("token");
    var data =
        {
            Email: email,
            AuthToken: authToken
        };
    $.ajax({
        type: "POST",
        url: "../note/QueryRecentTokens",
        data: data,
        success: function (response) {
            if (response != null) {
                localStorage.setItem("recentTokens", JSON.stringify(response));
                PopulateRecentTokenDisplays(response);
            }
        }
    });
}

// Deletes recent token and reloads controls
function DeleteRecentTokens(token) {
    var email = CacheStoreGet("email");
    var authToken = CacheStoreGet("token");
    var data =
        {
            Email: email,
            AuthToken: authToken,
            Token: token
        };
    $.ajax({
        type: "POST",
        url: "../note/DeleteRecentToken",
        data: data,
        success: function (response) {
            if (response != null) {
                localStorage.setItem("recentTokens", JSON.stringify(response));
                PopulateRecentTokenDisplays(response);
            }
        }
    });
}

function LogOut() {
    var email = CacheStoreGet("email");
    var authToken = CacheStoreGet("token");
    var authAttempt =
    {
        Email: email,
        AuthToken: authToken
    }

    $.ajax({
        type: "POST",
        url: "../user/LogOut",
        data: authAttempt,
        dataType: 'json'
    });
    localStorage.clear();
    ClearAuthToken();
    Redirect("Signup.html");
}

function CreateNote() {
    var noteContents = document.getElementById("NoteContents").value;
    if (noteContents != "") {
        ShowLoading("NoteButton");
        var email = CacheStoreGet("email");
        var authToken = CacheStoreGet("token");
        var today = new Date();
        var data =
            {
                Email: email,
                UTCOffset: new Date().getTimezoneOffset(),
                AuthToken: authToken,
                NoteContents: noteContents,
                Location: userCity
            };
        $.ajax({
            type: "POST",
            url: "../note/CreateNote",
            data: data,
            success: function (createNoteResponse) {                
                if (createNoteResponse.Status == "Success") {
                    document.getElementById("NoteContents").value = "";
                    DisplayNoteCharactersLeft();
                    ShowCompletedIcon("NoteButton");
                    document.getElementById("NoteStatusMessage").innerHTML = "Success";
                    var newNote = createNoteResponse.Note;
                    cachedNotes.AddNote(newNote);
                    MasterViewModel.noteListViewModel.addNote(newNote.RowKey, newNote.EncodedNote, newNote.Timestamp, newNote.Completed);
                    document.getElementById('NoteContents').focus();
                }
                else {
                    HideButtonImage("NoteButton");
                    if (createNoteResponse.Status == "Expired") {
                        // Auth token has expired
                        AuthUserAndSetCookie(email, CacheStoreGet("password"),
                            function () { CreateNote(); }, // on success
                            function () {
                                Redirect('Signup.html');
                                showError("Please log in to continue");
                            }) // on failure
                    }
                }
            }
        });
    }
}

// updates note on server and updates UI appropriately
function UpdateNote(noteNodeObj) {
    // Prevent two facades from updating the same note
    // this can happen in the "complete" logic
    if (noteNodeObj.note().updating) {
        return;
    }
    noteNodeObj.note().updating = true;
    // update each save button to display loading
    ShowLoadingByClassName("SaveEditButton");
    var noteContents = noteNodeObj.note().contents();
    if (noteContents == "") {
        HideButtonImageByClassName("SaveEditButton");
        return;
    }
    var email = CacheStoreGet("email");
    var authToken = CacheStoreGet("token");
    var today = new Date();
    var data =
        {
            Email: email,
            UTCOffset: new Date().getTimezoneOffset(),
            AuthToken: authToken,
            NoteContents: noteContents,
            Completed: noteNodeObj.note().completed(),
            Location: userCity,
            RowKey: noteNodeObj.note().noteId
        };
    $.ajax({
        type: "POST",
        url: "../note/UpdateNote",
        data: data,
        success: function (response) {
            if (response.Status == "Success") {
                cachedNotes.UpdateNote(response.Note);
                noteNodeObj.onSuccessfulUpdate();
            }
            if (response.Status == "Expired") {
                // Auth token has expired
                AuthUserAndSetCookie(email, CacheStoreGet("password"),
                    function () {
                        UpdateNote(noteNodeObj);
                    }, // on success
                    function () {
                        Redirect('Signup.html');
                        showError("Please log in to continue");
                    }) // on failure
            }
            HideButtonImageByClassName("SaveEditButton");
        }
    });
    // Unlock the note
    window.setTimeout(function () {
        noteNodeObj.note().updating = false;
    }, 1000);
}

// delete note on server and updates UI appropriately
function DeleteNote(noteNodeObj) {
    // update each save button to display loading
    ShowLoadingByClassName("DeleteNoteButton");
    var email = CacheStoreGet("email");
    var authToken = CacheStoreGet("token");
    var data =
        {
            Email: email,
            AuthToken: authToken,
            RowKey: noteNodeObj.note().noteId
        };
    $.ajax({
        type: "POST",
        url: "../note/DeleteNote",
        data: data,
        success: function (response) {
            if (response == "Expired") {
                // Auth token has expired
                AuthUserAndSetCookie(email, CacheStoreGet("password"),
                    function () { DeleteNote(noteNodeObj); }, // on success
                    function () {
                        Redirect('Signup.html');
                        showError("Please log in to continue");
                    }) // on failure
            }
            else {
                if (response == "RefreshRecent") {
                    QueryRecentTokens();
                }
                noteNodeObj.onSuccessfulDelete();
                // attempt to remove it from recent
                cachedNotes.DeleteNote(noteNodeObj.note().noteId);
            }
            HideButtonImageByClassName("DeleteNoteButton");
        }
    });
}