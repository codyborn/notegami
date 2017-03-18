var cachedNotes = new CachedNotes();
var updateChecker = window.setInterval(function () { cachedNotes.UpdateIfExpired(); }, 10 * 1000);

// Object that wraps the local cached notes
function CachedNotes() {
    this.notes = null;
    this.SetLastUpdate = function () {
        localStorage.setItem("lastUpdateTime", (new Date().getTime() * 10000) + 621355968000000000);
    }
    this.UpdateIfExpired = function () {
        GetLastUpdateTime(function (utcLastUpdate) {
            var lastUpdate = localStorage.getItem("lastUpdateTime");        
            if (lastUpdate == null || 
                utcLastUpdate == null || 
                utcLastUpdate > parseInt(lastUpdate)) {
                cachedNotes.QueryAllNotes();
            }
        })
    };
    this.GetNotes = function () {
        if (this.notes == null) {
            var cachedResponse = localStorage.getItem("recentNotes");
            if (cachedResponse != null) {
                this.notes = JSON.parse(cachedResponse);
            }
            else {
                this.QueryAllNotes();
            }
        }
        return this.notes;
    };
    this.SetNotes = function (newNotes) {
        localStorage.setItem("recentNotes", JSON.stringify(newNotes));
        this.SetLastUpdate();
        this.notes = newNotes;
    };
    this.DeleteNote = function (rowKey) {
        var notes = this.GetNotes();
        if (notes == null) {
            return;
        }
        for (var i = 0; i < notes.length; i++) {
            if (notes[i].RowKey == rowKey) {
                notes.splice(i, 1);
                localStorage.setItem("recentNotes", JSON.stringify(notes));
                this.SetLastUpdate();
                return;
            }
        }
    }
    this.UpdateNote = function (updatedNote) {
        var notes = this.GetNotes();
        if (notes == null) {
            return;
        }
        for (var i = 0; i < notes.length; i++) {
            if (notes[i].RowKey == updatedNote.RowKey) {
                notes[i] = updatedNote;
                localStorage.setItem("recentNotes", JSON.stringify(notes));
                this.SetLastUpdate();
                return;
            }
        }
    }
    this.AddNote = function (newNote) {
        var notes = this.GetNotes();
        if (notes == null) {
            return;
        }
        notes.push(newNote);
        localStorage.setItem("recentNotes", JSON.stringify(notes));
        this.SetLastUpdate();
    }
    this.QueryAllNotes = function () {
        QueryAllNotes(function (response) {
            var oldNotes = cachedNotes.GetNotes();
            cachedNotes.SetNotes(response.Notes);
            // equivalence check        
            if (response.Notes.length != oldNotes.length) {
                cachedNotes.DisplayRecentNotes();
                return;
            }
            for (var i = 0; i < response.Notes.length; i++) {
                if (response.Notes[i].RowKey != oldNotes[i].RowKey ||
                    response.Notes[i].Timestamp != oldNotes[i].Timestamp) {
                    cachedNotes.DisplayRecentNotes();
                    return;
                }
            }            
        });
    }
    this.QueryRecentNotes = function () {
        var today = new Date();
        var yesterday = new Date();
        yesterday.setDate(today.getDate() - 1);
        var lastWeek = new Date();
        lastWeek.setDate(today.getDate() - 7);
        QueryNotes(DateToString(today) + "-" + DateToString(lastWeek), function (response) {
            var oldNotes = cachedNotes.GetNotes();
            cachedNotes.SetNotes(response.Notes);
            if (displayingRecentNotes) {
                // equivalence check        
                if (response.Notes.length != oldNotes.length) {
                    cachedNotes.DisplayRecentNotes();
                    return;
                }
                for (var i = 0; i < response.Notes.length; i++) {
                    if (response.Notes[i].RowKey != oldNotes[i].RowKey ||
                        response.Notes[i].Timestamp != oldNotes[i].Timestamp) {
                        cachedNotes.DisplayRecentNotes();
                        return;
                    }
                }
            }
        },
        true);
    }
    this.DisplayRecentNotes = function () {
        DisplayResults(this.GetNotes());
    }
}

function SearchCachedNotes() {
    var queryContents = document.getElementById('QueryContents').value;
    MasterViewModel.noteListViewModel.updateCurrentQueryContent(queryContents);
    document.getElementById("Results").scrollTop = 0;
}
// Prevents searching after each character press which can be UI intensive
// Waits for user to pause
var typingBuffer = 300;
var keyUpTimeOut;
function SearchCachedNotesWithBuffer() {
    if (typeof keyUpTimeOut != "undefined") {
        window.clearTimeout(keyUpTimeOut);
    }
    keyUpTimeOut = window.setTimeout(function () { SearchCachedNotes() }, typingBuffer);
}