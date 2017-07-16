
var cachedNotes = new CachedNotes();
var updateChecker = window.setInterval(function () { cachedNotes.UpdateIfExpired(); }, 10 * 1000);

// Object that wraps the local cached notes
function CachedNotes() {
    this.notes = null;
    this.worker = new Worker("Scripts/LocalCachedNotes_Worker.js");
    this.worker.onmessage = function (e) {
        if (e.data != "undefined") {
            localStorage.setItem("recentNotes", e.data);
            cachedNotes.SetLastUpdate();
        }
    }
    this.SetLastUpdate = function () {
        localStorage.setItem("lastUpdateTime", (new Date().getTime() * 10000) + 621355968000000000);
    }
    this.UpdateIfExpired = function () {
        GetLastUpdateTime(function (utcLastUpdate) {
            var lastUpdate = localStorage.getItem("lastUpdateTime");        
            if (lastUpdate == null || 
                utcLastUpdate == null || 
                utcLastUpdate > parseInt(lastUpdate)) {
                cachedNotes.QueryRecentNotes();
            }
        })
    };
    this.GetNotes = function () {
        if (this.notes == null || this.notes.length == 0) {
            var cachedResponse = localStorage.getItem("recentNotes");
            if (cachedResponse != null && cachedResponse != "undefined") {
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
        this.SetLastUpdate();
        this.notes = newNotes;
    };
    this.DeleteNote = function (rowKey) {
        var notes = this.GetNotes();
        if (notes == null) {
            return;
        }
        this.worker.postMessage(["delete", rowKey, notes]);
    }
    this.UpdateNote = function (updatedNote) {
        var notes = this.GetNotes();
        if (notes == null) {
            return;
        }
        this.worker.postMessage(["update", updatedNote, notes]);
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
    this.QueryRecentNotes = function () {
        var today = new Date();
        var yesterday = new Date();
        yesterday.setDate(today.getDate() - 1);
        var lastWeek = new Date();
        lastWeek.setDate(today.getDate() - 10);
        QueryNotes(DateToString(today) + "-" + DateToString(lastWeek), function (response) {
            var oldNotes = cachedNotes.GetNotes();
            cachedNotes.SetNotes(response.Notes);
            if (MasterViewModel.noteListViewModel.displayingRecentNotes()) {
                // equivalence check        
                if (oldNotes == null || response.Notes.length != oldNotes.length) {
                    cachedNotes.DisplayRecentNotes();
                    return;
                }
                for (var i = 0; i < response.Notes.length; i++) {
                    if (response.Notes[i].id != oldNotes[i].id ||
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
        MasterViewModel.noteListViewModel.displayingRecentNotes(true);
    }
}

function SearchCachedNotes() {
    var queryContents = document.getElementById('QueryContents').value;
    MasterViewModel.noteListViewModel.updateCurrentQueryContent(queryContents);
    if (!MasterViewModel.noteListViewModel.displayingRecentNotes()) {
        cachedNotes.DisplayRecentNotes();
    }
    document.getElementById("Results").scrollTop = 0;
    // Auto query the server if scroll is close to the end
    var scrollAmountLeft = document.getElementById("Results").scrollHeight - document.getElementById("Results").scrollTop - $("#Results").height();
    if (MasterViewModel.noteListViewModel.displayingRecentNotes() &&
        scrollAmountLeft < 100)
    {
        QueryNotes();
    }
}
// Prevents searching after each character press which can be UI intensive
// Waits for user to pause
var typingBuffer = 300;
var keyUpTimeOut;
function SearchCachedNotesWithBuffer(event) {
    if (typeof keyUpTimeOut != "undefined") {
        window.clearTimeout(keyUpTimeOut);
    }
    keyUpTimeOut = window.setTimeout(function () { SearchCachedNotes() }, typingBuffer);
    if (event.keyCode == 13) {
        QueryNotes();
    }
}