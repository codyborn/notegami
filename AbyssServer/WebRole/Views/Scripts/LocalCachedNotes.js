var cachedNotes = new CachedNotes();
var lastUpdatedCachedNotes = new Date();
// one day in milliseconds
var one_day = 1000 * 60 * 60 * 24;
// Check to see if the results are expired
this.updateChecker = window.setInterval(function () {
    if ((new Date().getTime() - lastUpdatedCachedNotes.getTime()) > one_day) {
        cachedNotes.QueryRecentNotes();
    }
}, 60 * 1000);

// Object that wraps the local cached notes
function CachedNotes() {
    this.notes = null;
    this.GetNotes = function () {
        if (this.notes == null) {
            var cachedResponse = localStorage.getItem("recentNotes");
            if (cachedResponse != null) {
                this.notes = JSON.parse(cachedResponse);
            }
            else {
                this.QueryRecentNotes();
            }
        }
        return this.notes;
    };
    this.SetNotes = function (newNotes) {
        localStorage.setItem("recentNotes", JSON.stringify(newNotes));
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
        displayingRecentNotes = true;
        DisplayResults(this.GetNotes());
    }
}

function SearchCachedNotes() {
    var queryContents = document.getElementById('QueryContents').value;
    if (!displayingRecentNotes) {
        cachedNotes.DisplayRecentNotes();
    }
    MasterViewModel.noteListViewModel.updateCurrentQueryContent(queryContents);
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