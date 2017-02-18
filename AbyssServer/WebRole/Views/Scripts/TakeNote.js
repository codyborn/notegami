usingMobileDevice = false;
if (/Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent)) {
    usingMobileDevice = true;
}

currTheme = "day";
// if it's night, load night theme
function DisplayTimeBasedTheme() {
    var currHour = new Date().getHours();
    if (currHour < 5 || currHour > 20) {
        if (currTheme == "day") {
            if (typeof $('link[title=NightTheme]')[0] == "undefined") {
                $('head').append('<link rel="stylesheet" href="Style/NightTheme.css" type="text/css" title="NightTheme" />');
            }
            else {
                $('link[title=NightTheme]')[0].disabled = false;
            }
        }
        currTheme = "night";
    }
    else {
        if (currTheme == "night") {
            $('link[title=NightTheme]')[0].disabled = true;
        }
        currTheme = "day";
    }
}
DisplayTimeBasedTheme();
// Update theme every 60 sec
var themeClock = window.setInterval(function () { DisplayTimeBasedTheme() }, 60 * 1000);

var showDownConverter = new showdown.Converter({
    tasklists: 'true',
    simplifiedAutoLink: 'true',
    strikethrough: 'true',
    disableForced4SpacesIndentedSublists: 'true'
});

function TakeNoteCheckAuthCookie() {

    CheckAuthCookie(
        function () { QueryRecentTokens(); }, // on success
        function () { Redirect("Signup.html"); showError("Please log in to continue"); }); // onfailure
}

function ToggleNightmode() {
    window.clearInterval(themeClock);
    if (currTheme == "day") {
        if (typeof $('link[title=NightTheme]')[0] == "undefined") {
            $('head').append('<link rel="stylesheet" href="Style/NightTheme.css" type="text/css" title="NightTheme" />');
        }
        else {
            $('link[title=NightTheme]')[0].disabled = false;
        }
        currTheme = "night";
    }
    else {
        $('link[title=NightTheme]')[0].disabled = true;
        currTheme = "day";
    }
}

// Limit all textarea to MAXNOTELENGTH
function LimitTextAreaMaxLength() {
    $(function () {
        $("textarea").bind('input propertychange', function () {
            if ($(this).val().length > MAXNOTELENGTH) {
                $(this).val($(this).val().substring(0, MAXNOTELENGTH));
            }
        })
    });
}
LimitTextAreaMaxLength();

function HideNoteInput() {
    if (usingMobileDevice) {
        // make room for mobile keyboard
        MasterViewModel.noteInputViewModel.shouldBeVisible(false);
        document.getElementById('Results').classList.remove('contractedResults');
        document.getElementById('Results').classList.add('expandResults');
    }
}
function DisplayNoteInput() {
    if (usingMobileDevice) {
        MasterViewModel.noteInputViewModel.shouldBeVisible(true);
        document.getElementById('Results').classList.add('contractedResults');
        document.getElementById('Results').classList.remove('expandResults');
    }
}

// KO model for the note input container
function NoteInputViewModel() {
    var self = this;
    self.shouldBeVisible = ko.observable(true);
}

function NoteFacade(note, currentTag) {
    var self = this;
    self.note = ko.observable(note);
    self.tag = currentTag.toLowerCase();
    self.completed = self.note().completed;
    self.completed.subscribe(function () {
        UpdateNote(self);
    });

    self.shouldBeVisible = ko.computed(function () {
        var tags = self.note().getNoteTags();
        if (self.note().originalText() == "") {
            return false;
        }

        // Filter notes on tokens if there exists local query tokens
        if (MasterViewModel.noteListViewModel.currentQueryTokens().length > 0) {
            var shouldBeVisible = false;
            for (var i = 0; i < MasterViewModel.noteListViewModel.currentQueryTokens().length; i++) {
                var token = MasterViewModel.noteListViewModel.currentQueryTokens()[i].toLowerCase();
                if (self.note().originalText().toLowerCase().indexOf(token) >= 0) {
                    shouldBeVisible = true;
                    break;
                }
            }
            if (!shouldBeVisible) {
                return false;
            }
        }

        for (var i = 0; i < tags.length; i++) {
            if (tags[i].toLowerCase() == self.tag) {
                return true;
            }
        }
        return false;
    });
    self.formattedText = ko.computed(function () {
        var noteContents = ReplaceThisTagWithNothing(self.note().contents(), currentTag);
        noteContents = EscapeHtmlTags(noteContents);
        noteContents = ReplaceHashtagsWithQuickLinks(noteContents).trim();
        //noteContents = ReplaceURLsWithMarkdownVersion(noteContents);
        noteContents = showDownConverter.makeHtml(noteContents);
        return ReplaceURLWithBlankTarget(noteContents);
    });
    self.smallerFont = ko.computed(function () {
        return self.formattedText().length > LARGEFONTMAXLENGTH;
    });
    self.editingNote = ko.observable(false);
    self.pressAndHoldAction = function (noteFacade, evt) {
        self.setNoteToEditing(noteFacade, evt);
    };
    self.setNoteToEditing = function (noteFacade, evt) {
        if (self.editingNote()) {
            return;
        }
        AddWindowUnload();
        // Get the on-page element, to help with scroll to element
        MasterViewModel.noteListViewModel.editingNoteTarget = noteFacade.parentElement.parentElement;
        // Save the height of the prev div to resize the textarea to match
        var textHeight = $(noteFacade).height();
        self.editingNote(true);
        // if one was previously editing, cancel the edit
        if (MasterViewModel.noteListViewModel.editingNote != null) {
            MasterViewModel.noteListViewModel.editingNote.cancelEdit();
        }
        MasterViewModel.noteListViewModel.editingNote = self;
        if (usingMobileDevice) {
            // the mobile keyboard will cover some of the results
            // this ensures that it's visible
            // wait 100ms to give time for re-adjustment due to keyboard
            window.setTimeout(function () {
                ScrollToElement(MasterViewModel.noteListViewModel.editingNoteTarget);
            }, 100);
        }
        // focus on the textarea
        // will have to update this path if you change the html structure
        noteFacade.parentElement.children[1].children[0].focus();
        if (textHeight > $(noteFacade.parentElement.children[1].children[0]).height()) {
            noteFacade.parentElement.children[1].children[0].style.height = textHeight + "px";
        }
    };
    self.cancelEdit = function () {
        self.note().contents(self.note().originalText());
        self.editingNote(false);
        MasterViewModel.noteListViewModel.editingNote = null;
        RemoveWindowUnload();
    };
    self.saveEdit = function () {
        UpdateNote(self);
    };
    self.onSuccessfulUpdate = function () {
        self.note().originalText(self.note().contents());
        self.editingNote(false);
        MasterViewModel.noteListViewModel.editingNote = null;
        RemoveWindowUnload();
    }
    self.deleteNote = function () {
        DeleteNote(self);
    };
    self.onSuccessfulDelete = function () {
        self.note().originalText("");
        self.note().contents("");
        RemoveWindowUnload();
    }

    self.note().contents.focused = ko.observable();
}

function Note(id, text, timestamp, completed) {
    var self = this;
    self.noteId = id;
    self.originalText = ko.observable(text);
    self.contents = ko.observable(text);
    self.timestamp = timestamp;
    self.completed = ko.observable(completed);
    // allows only one facade to save the note at a time
    self.updating = false;

    self.formattedTimestamp = ko.computed(function () {
        var date = new Date(self.timestamp);
        var minutes = date.getMinutes();
        if (minutes < 10) {
            minutes = "0" + minutes;
        }
        var hour = date.getHours() >= 13 ? date.getHours() - 12 : date.getHours() == 0 ? 12 : date.getHours();
        var timeString = hour + ":" + minutes;
        if (date.getHours() < 12) {
            timeString += "&nbsp;am";
        }
        else {
            timeString += "&nbsp;pm";
        }

        // if it's not today, add the date
        if (new Date(Date.now()).getDate() != date.getDate()) {
            timeString = (date.getMonth() + 1) + "/" + date.getDate() + " " + timeString;
        }
        else {
            timeString = "Today " + timeString;
        }
        return timeString;
    });
    self.getNoteTags = function () {
        var tags = self.originalText().match(new RegExp(/#(\w+)/ig));
        if (tags != null) {
            return tags;
        }
        return [UNCATEGORIZEDTAG];
    };
}
function NoteNode(tag) {
    var self = this;
    self.tag = tag;
    self.notes = ko.observableArray();
    // sort notes first on completed, second on date
    self.sortedNotes = ko.computed(function () {
        if (typeof self.notes() == "undefined") {
            return self.notes();
        }
        return self.notes().sort(function (n1, n2) {
            if (n1.note().completed() && !n2.note().completed()) {
                return 1;
            }
            else if (!n1.note().completed() && n2.note().completed()) {
                return -1;
            }
            else if (new Date(n1.note().timestamp) < new Date(n2.note().timestamp)) {
                return 1;
            }
            return -1;
        });
    });
    self.shouldDisplayNode = ko.computed(function () {
        // if there are any notes visible
        var anyVisible = false;
        for (var i = 0; i < self.notes().length; i++) {
            if (self.notes()[i].shouldBeVisible()) {
                anyVisible = true;
                break;
            }
        }
        if (!anyVisible) {
            return false;
        }
        // if specific tags were requested, then display the node if its tag is contained in the query
        if (MasterViewModel.noteListViewModel.queryContents != null && MasterViewModel.noteListViewModel.queryContents.indexOf('#') != -1) {
            return MasterViewModel.noteListViewModel.queryContents.toLowerCase().indexOf(self.tag.toLowerCase()) != -1;
        }
        return true;
    });
    this.saveNotePosition = function (elem) {
        if (elem.nodeType == 1) {
            elem.saveOffsetTop = elem.offsetTop;
        }
    };

    this.moveNote = function (elem) {
        if (elem.nodeType == 1) {
            if (elem.offsetTop !== elem.saveOffsetTop) {
                var tempElement = elem.cloneNode(true);
                $(elem).css({ visibility: 'hidden' });
                $(tempElement).css({
                    position: "absolute",
                    width: window.getComputedStyle(elem).width
                });
                elem.parentNode.appendChild(tempElement);
                $(tempElement)
                    .css({ top: elem.saveOffsetTop })
                    .animate({ top: elem.offsetTop }, function () {
                        $(elem).css({ visibility: 'visible' });
                        elem.parentNode.removeChild(tempElement);
                    });
            }
        }
    };
}

// Data view model
// Given a note, adds it to its appropriate noteNode
function NoteListViewModel() {
    var self = this;
    self.noteNodes = ko.observableArray([]);
    // Associative array for quick lookup
    // Cannot use associative arrays in KO observable
    self.noteNodeTags = [];
    // Tokenizes current query for dynamic updating of displayed notes
    self.currentQueryTokens = ko.observableArray([]);
    self.updateCurrentQueryContent = function (queryContents) {
        if (queryContents.length > 3) {
            var newQueryTokens = ko.observableArray([]);
            var queryTokens = queryContents.split(' ');
            for (var j = 0; j < queryTokens.length; j++) {
                // Remove short tokens
                if (queryTokens[j].length > 3) {
                    newQueryTokens.push(queryTokens[j]);
                }
            }
            self.currentQueryTokens(newQueryTokens());
        }
        else {
            self.currentQueryTokens([]);
        }
    }
    self.clearCurrentQueryContent = function () {
        self.currentQueryTokens([]);
    }
    self.queryContents;
    self.queryOccurred = ko.observable(false);

    // Operations
    self.clearNotes = function () {
        self.noteNodes([]);
        self.noteNodeTags = [];
    }
    self.addNote = function (id, contents, timestamp, completed) {
        // figure out which noteNodes this note belongs to
        var newNote = new Note(id, Urldecode(contents), timestamp, completed);
        var noteTags = newNote.getNoteTags();
        // add the new note obj to each of the lists
        // if a note is updated that is contained in multiple noteNodes, 
        // then each copy should be updated

        // If the NoteNode doesn't already exist, create it and add the notes from all other nodes
        for (var i = 0; i < noteTags.length; i++) {
            // Create a new NoteNode
            var lowerCaseTag = noteTags[i].toLowerCase();
            if (self.noteNodeTags[lowerCaseTag] == null) {
                var newNoteNode = new NoteNode(noteTags[i]);
                self.noteNodes.push(newNoteNode);
                self.noteNodeTags[lowerCaseTag] = true;
                // foreach noteNode that is not the new one,
                // add each of its existing notes to this new noteNode
                for (var j = 0; j < self.noteNodes.length; j++) {
                    var oldNoteNode = self.noteNodes[j];
                    if (oldNoteNode.tag != noteTags[i]) {
                        for (var k = 0; k < oldNoteNode.notes.length; k++) {
                            // clone note with new tag
                            var noteFacade = new NoteFacade(oldNoteNode.notes[k].note(), noteTags[i]);
                            newNoteNode.notes.push(noteFacade);
                        }
                    }
                }
            }
        }
        // each NoteNode contains every note, but decides whether to display it        
        for (var j = 0; j < self.noteNodes().length; j++) {
            var noteNode = self.noteNodes()[j];
            // create a note facade to display the tagless text
            var noteFacade = new NoteFacade(newNote, noteNode.tag);
            self.noteNodes()[j].notes.push(noteFacade);
        }
    }

    self.queryTag = function (noteNode) {
        if (noteNode.tag != UNCATEGORIZEDTAG) {
            HashTagClick(noteNode.tag);
        }
    }
    self.editingNote = null;
    self.editingNoteTarget = null;
}


// KO can only bind one object to the page
MasterViewModel = {
    noteInputViewModel: new NoteInputViewModel(),
    noteListViewModel: new NoteListViewModel()
}

ko.applyBindings(MasterViewModel);
ko.bindingHandlers.pressAndHold = {
    // This will be called when the binding is first applied to an element
    // Set up any initial state, event handlers, etc. here        
    init: function (element, valueAccessor, allBindings, viewModel, bindingContext) {
        // press and hold triggers oncontextmenu()        
        if (usingMobileDevice) {
            AddPressAndHoldListener(element,
                function (event) {
                    var value = valueAccessor();
                    value(element, event);
                });
        }
        else {
            AddClickListener(element,
                            function (event) {
                                var value = valueAccessor();
                                value(element, event);
                            });
        }
    }
};
ko.bindingHandlers.slideIn = {
    init: function (element, valueAccessor) {
        var value = ko.utils.unwrapObservable(valueAccessor());
        $(element).toggle(value);
    },
    update: function (element, valueAccessor) {
        var value = ko.utils.unwrapObservable(valueAccessor());
        value ? $(element).slideDown() : $(element).slideUp();
    }
};

function AddClickListener(element, action) {
    element.addEventListener("click", function (event) {
        // Prevent default behavior
        //event.preventDefault();
        action(event);
    });
}

function AddPressAndHoldListener(element, action) {
    var eventToListenTo = "mousedown";
    if (is_safari) {
        eventToListenTo = "touchstart";
    }
    element.addEventListener(eventToListenTo, function (event) {
        // Prevent default behavior
        event.preventDefault();
        // Test that the touch is correctly detected            
        // Timer for long touch detection
        element.timerLongTouch = setTimeout(function () {
            action(event);
        }, 500);
    });
    eventToListenTo = "mousemove";
    if (is_safari) {
        eventToListenTo = "touchmove";
    }
    element.addEventListener(eventToListenTo, function (event) {
        // If timerLongTouch is still running, then this is not a long touch 
        // (there is a move) so stop the timer
        clearTimeout(element.timerLongTouch);
    });
    eventToListenTo = "mouseup";
    if (is_safari) {
        eventToListenTo = "touchend";
    }
    element.addEventListener(eventToListenTo, function () {
        // Prevent default behavior
        event.preventDefault();

        // If timerLongTouch is still running, then this is not a long touch
        // so stop the timer
        clearTimeout(element.timerLongTouch);
    });
}

// full screen on mobile devices
window.addEventListener("load", function () { window.scrollTo(0, 0); });
var activeAction = QueryNotes;
// Will be populated by geocoder
var userCity = "";
$(document).ready(function () {
    $(function () {
        $("#MenuList").menu();
    });
    document.body.addEventListener("click", function () { MakeFullScreenOnAction(); });
    var cachedResponse = localStorage.getItem("recentTokens");
    if (cachedResponse != null) {
        var parsedResponse = JSON.parse(cachedResponse);
        PopulateRecentTokenDisplays(parsedResponse);
    }
    if (is_safari) {
        document.getElementById('Results').style.height = ($(document).height() - 320) + "px";
    }
    // Handle pre-query in QS
    var queryStringValue = GetQSParameterByName("q");
    if (queryStringValue != "" && queryStringValue != null) {
        document.getElementById('QueryContents').value = queryStringValue;
        QueryNotes();
    }
    else {
        QueryRecentNotes();
    }
});

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
            success: function (response) {
                if (response == "Success") {
                    document.getElementById("NoteContents").value = "";
                    DisplayNoteCharactersLeft();
                    ShowCompletedIcon("NoteButton");
                    document.getElementById("NoteStatusMessage").innerHTML = "Success";
                    // Update the query results
                    if (document.getElementById("QueryContents").value == "") {
                        // Refresh recent if the query is empty
                        QueryRecentNotes();
                    }
                    else {
                        QueryNotes();
                    }
                    QueryRecentTokens();
                    document.getElementById('NoteContents').focus();
                }
                else {
                    HideButtonImage("NoteButton");
                    if (response == "Expired") {
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
            if (response == "Success") {
                noteNodeObj.onSuccessfulUpdate();
            }
            if (response == "Expired") {
                // Auth token has expired
                AuthUserAndSetCookie(email, CacheStoreGet("password"),
                    function () { UpdateNote(noteNodeObj); }, // on success
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
            }
            HideButtonImageByClassName("DeleteNoteButton");
        }
    });
}

function QueryRecentNotes() {
    var today = new Date();
    var yesterday = new Date();
    yesterday.setDate(today.getDate() - 1);
    var lastWeek = new Date();
    lastWeek.setDate(today.getDate() - 7);
    QueryNotes(DateToString(today) + "-" + DateToString(lastWeek), CacheAndDisplayRecentNotes, "recentNotes");
}

// Only update display if the notes are different
// Store latest response in cache
function CacheAndDisplayRecentNotes(response, queryWords) {
    var cachedResponse = localStorage.getItem(queryWords);
    if (cachedResponse != null) {
        var parsedResponse = JSON.parse(cachedResponse);
        // equivalence check        
        if (parsedResponse.length != response.Notes.length) {
            localStorage.setItem(queryWords, JSON.stringify(response.Notes));
            DisplayResults(response.Notes);
            return;
        }
        for (var i = 0; i < parsedResponse.length; i++) {
            if (parsedResponse[i].RowKey != response.Notes[i].RowKey ||
                parsedResponse[i].Timestamp != response.Notes[i].Timestamp) {
                localStorage.setItem(queryWords, JSON.stringify(response.Notes));
                DisplayResults(response.Notes);
                return;
            }
        }
    }
    else {
        localStorage.setItem(queryWords, JSON.stringify(response.Notes));
        DisplayResults(response.Notes);
    }
}

function SearchCachedNotes() {
    var queryContents = document.getElementById('QueryContents').value;    
    MasterViewModel.noteListViewModel.updateCurrentQueryContent(queryContents);
    //if (queryContents.length > 3) {
    //    var cachedResponse = localStorage.getItem("recentNotes");
    //    DisplayResults(cachedResponse);
    //}
}

function QueryNotes(queryContents, callBackOnSuccess, reportedQueryContents) {    
    var fromUserInput = false;
    if (typeof queryContents == "undefined") {
        queryContents = document.getElementById("QueryContents").value;
        fromUserInput = true;
    }

    if (queryContents != "") {
        // Allows for quickly grabbing the most recent notes from the local cache        
        if (typeof reportedQueryContents == "undefined") {
            reportedQueryContents = queryContents;
        }
        var cachedResponse = localStorage.getItem(reportedQueryContents);
        if (cachedResponse != null) {
            var parsedResponse = JSON.parse(cachedResponse);
            DisplayResults(parsedResponse);
        }

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
                    if (typeof callBackOnSuccess == "undefined") {
                        MasterViewModel.noteListViewModel.queryOccurred(true);
                        DisplayResults(response.Notes);
                    }
                    else {
                        callBackOnSuccess(response, reportedQueryContents);
                    }
                }
                else if (response.Status == "Expired") {
                    // Auth token has expired
                    AuthUserAndSetCookie(email, CacheStoreGet("password"),
                            function () { QueryNotes(queryContents, callBackOnSuccess, reportedQueryContents); }, // on success
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

function PopulateAutoComplete(response) {

    if (response != null) {
        var tagsAndLocations = response["tags"].concat(response["locations"]);
        AddWordsToInputAutoComplete(tagsAndLocations, "QueryContents");
        AddWordsToInputAutoComplete(response["tags"], "NoteContents");
    }
}

function split(val) {
    return val.split(/\s*/);
}
function extractLast(term) {
    return split(term).pop();
}
function AddWordsToInputAutoComplete(wordBag, targetIndex) {
    // clear previous one in case of reloading data
    $("#" + targetIndex).textcomplete("destroy");
    $("#" + targetIndex).textcomplete([
    {
        words: wordBag,
        match: /(#?\w+)$/i,
        search: function (term, callback) {
            callback($.map(this.words, function (word) {
                return word.indexOf(term) === 0 || word.indexOf(term) === 1 ? word : null;
            }));
        },
        index: 1,
        replace: function (word) {
            return word + ' ';
        }
    }
    ]);
}

function PopulateRecentTokenDisplays(response) {
    DisplayQuickSearchBar(response);
    PopulateAutoComplete(response);
    DisplayQuickInputBar(response);
}
function DisplayQuickSearchBar(response) {
    var quickSearchContainer = document.getElementById("QuickSearchContainer");
    quickSearchContainer.innerHTML = "";
    // Add date links
    var today = new Date();
    var yesterday = new Date();
    yesterday.setDate(today.getDate() - 1);
    var lastWeek = new Date();
    lastWeek.setDate(today.getDate() - 7);
    var dateList = document.createElement('div');
    dateList.appendChild(CreateQuickSearchButton("Today", false, DateToString(today), false));
    dateList.appendChild(CreateQuickSearchButton("Yesterday", false, DateToString(yesterday), false));
    dateList.appendChild(CreateQuickSearchButton("Last 7 Days", false, DateToString(today) + "-" + DateToString(lastWeek), false));
    quickSearchContainer.appendChild(dateList);

    if (response != null) {
        var recentTokenDisplayCount = usingMobileDevice ? MAXRECENTTOKENSTODISPLAYFORMOBILE : MAXRECENTTOKENSTODISPLAYFORBROWSER;
        // Add hashtag links
        var hashtagList = document.createElement('div');
        hashtagList.classList.add("QuickLinksList");
        hashtagList.classList.add("noselect");
        for (var i = 0; i < Math.min(response["tags"].length, recentTokenDisplayCount) ; i++) {
            hashtagList.appendChild(CreateQuickSearchButton(response["tags"][i], true, null, true));
        }
        quickSearchContainer.appendChild(hashtagList);
    }
}

function DisplayQuickInputBar(response) {
    var quickInputContainer = document.getElementById("QuickInputContainer");
    quickInputContainer.innerHTML = "";

    if (response != null) {
        var recentTokenDisplayCount = usingMobileDevice ? MAXRECENTTOKENSTODISPLAYFORMOBILE : MAXRECENTTOKENSTODISPLAYFORBROWSER;
        // Add hashtag links
        var hashtagList = document.createElement('div');
        hashtagList.classList.add("QuickLinksList");
        hashtagList.classList.add("noselect");
        for (var i = 0; i < Math.min(response["tags"].length, recentTokenDisplayCount) ; i++) {
            hashtagList.appendChild(CreateQuickInputButton(response["tags"][i]));
        }
        quickInputContainer.appendChild(hashtagList);
    }
}

function DisplayQuickInputContainer() {
    document.getElementById("QuickInputContainer").style.display = "block";
}

function HideQuickInputContainer() {
    document.getElementById("QuickInputContainer").style.display = "none";
}

function DateTimeToString(date) {
    return DateToString(date) + " " + date.getHours() + ":" + date.getMinutes();
}

function DateToString(date) {
    var dd = date.getDate();
    var mm = date.getMonth() + 1;
    var yyyy = date.getFullYear();
    return mm + "/" + dd + "/" + yyyy;
}

function CreateQuickSearchButton(text, addToNote, searchFor, searchCachedNotes) {
    if (searchFor == null || searchFor == "") {
        searchFor = text;
    }
    var button = document.createElement('input');
    button.type = "submit";
    button.className = "quickSearchButton";
    button.value = text;
    button.addEventListener("click", function () {
        document.getElementById('QueryContents').value = searchFor;
        if (searchCachedNotes) {
            // Update local tokens to current search input
            SearchCachedNotes();
        }
        QueryNotes();
    });
    var addToNoteAction = function () {
        if (addToNote != null && addToNote) {
            AddTagToNoteContent(searchFor);
        }
        return false; /* prevent context menu from popping up */
    };
    // on right click
    button.oncontextmenu = addToNoteAction;
    if (is_safari) {
        AddPressAndHoldListener(button, addToNoteAction);
    }
    return button;
}


function CreateQuickInputButton(tag) {
    var button = document.createElement('input');
    button.type = "submit";
    button.className = "quickSearchButton";
    button.value = tag;
    button.addEventListener("click", function () {
        AddTagToNoteContent(tag);
    });
    var queryTagAction = function () {
        document.getElementById('QueryContents').value = tag;
        QueryNotes();
        return false; /* prevent context menu from popping up */
    };
    // on right click
    button.oncontextmenu = queryTagAction;
    if (is_safari) {
        AddPressAndHoldListener(button, queryTagAction);
    }
    return button;
}

function AddTagToNoteContent(tag) {
    // if the note text doesn't already contain the tag, add it
    var noteText = document.getElementById('NoteContents').value;
    if (noteText.indexOf(tag) == -1) {
        if (noteText != "" && noteText.charAt(noteText.length - 1) != " ") {
            document.getElementById('NoteContents').value += " ";
        }
        document.getElementById('NoteContents').value += tag + " ";
        document.getElementById('NoteContents').focus();
    }
}

// Groups each note by their tags and displays them in each category
// if the user is querying for some specific tags, only display those
function DisplayResults(response, queryContents) {
    // if queryExact is not empty, we only show tags contained in the query contents
    var queryExact = new Array();
    MasterViewModel.noteListViewModel.clearNotes();
    if (response.length > 0) {
        // let's us know which nodes to show
        MasterViewModel.noteListViewModel.queryContents = queryContents;        
        for (var i = response.length - 1; i >= 0; i--) {            
            MasterViewModel.noteListViewModel.addNote(response[i].RowKey, response[i].EncodedNote, response[i].Timestamp, response[i].Completed);
        }
        //ScrollToBottom();
        LimitTextAreaMaxLength();
    }
}

function ReplaceURLWithBlankTarget(noteText) {
    var hashTagLinkedText = noteText.replace(new RegExp(/<a /ig), "<a target='_blank'");
    return hashTagLinkedText;
}
function ReplaceHashtagsWithQuickLinks(noteText) {
    var hashTagLinkedText = noteText.replace(new RegExp(/#(\w+)/ig), "<a href='#' onclick='HashTagClick(\"#$1\");'>#$1</a> ");
    return hashTagLinkedText;
}
function ReplaceThisTagWithNothing(noteText, tag) {
    return noteText.replace(new RegExp(tag + "\s?", 'i'), "")
}
function ReplaceURLsWithMarkdownVersion(noteText) {
    var anchoredText = noteText.replace(new RegExp(/\s(http|ftp|https):\/\/(\S)+/ig), " [$&]($&) ");
    return anchoredText;
}
function EscapeHtmlTags(noteText) {
    return noteText.replace(new RegExp("<", 'g'), "&lt;").replace(new RegExp(">", 'g'), "&gt;");
}
function HashTagClick(text) {
    document.getElementById('QueryContents').value = text;
    QueryNotes();
}

var autoScrolling = false;
function ScrollToElement(element) {
    var results = document.getElementById("Results");
    // scroll up should be current scroll + the elements position on the page - some buffer
    autoScrolling = true;
    results.scrollTop = results.scrollTop + (element.getBoundingClientRect().top - RESULTSSCROLLBUFFER);
    autoScrolling = false;
}
var lastScrollPos = null;
var currentSearchContainerPos = 0;
function UpdateSearchBarLocation() {
    if (!usingMobileDevice) {
        return;
    }
    // ignore scrolling performed by the JS
    if (autoScrolling) {
        return;
    }
    var results = document.getElementById("Results");
    if (lastScrollPos == null) {
        lastScrollPos = results.scrollTop;
        return;
    }
    var scrollDiff = results.scrollTop - lastScrollPos;
    lastScrollPos = results.scrollTop;
    var searchContainer = document.getElementById("SearchBarContainer");
    var searchContainerHeight = searchContainer.getBoundingClientRect().height - 5;
    // if scrolling down and the search bar is at the peak
    // or scrolling up and the search bar is at the origin, then return
    if ((scrollDiff > 0 && currentSearchContainerPos == -searchContainerHeight) ||
        (scrollDiff < 0 && currentSearchContainerPos == 0)) {
        return;
    }
    currentSearchContainerPos = currentSearchContainerPos - scrollDiff;
    if (currentSearchContainerPos > 0) {
        currentSearchContainerPos = 0;
    }
    if (currentSearchContainerPos < -searchContainerHeight) {
        currentSearchContainerPos = -searchContainerHeight;
    }
    searchContainer.style.top = currentSearchContainerPos + "px";
    results.style.marginTop = (currentSearchContainerPos + searchContainerHeight + 20) + "px";
}

function DisplayNoteCharactersLeft() {
    var noteLength = document.getElementById("NoteContents").value.length;
    var lineCount = document.getElementById("NoteContents").value.lineCount();
    // Adjust size of input field based on lineCount
    if (lineCount >= 4 && lineCount <= 10) {
        document.getElementById("NoteContents").style.height = (83.2 / 3 * lineCount) + "px";
    }
    else if (lineCount < 4) {
        document.getElementById("NoteContents").style.height = "83.2px";
    }
    // Decrease the font size if text is over threshold
    if (noteLength > LARGEFONTMAXLENGTH) {
        document.getElementById("NoteContents").className = "smallerFont";
    }
    else {
        document.getElementById("NoteContents").className = "";
    }
    document.getElementById("NoteStatusMessage").innerHTML = noteLength + "/" + MAXNOTELENGTH;
}

function DisplayCity(city) {
    userCity = city;
    document.getElementById('LocationStatusMessage').innerHTML = "";
    var locIcon = document.createElement('img');
    locIcon.className = 'locationIcon';
    locIcon.src = "Images/location_icon.svg";
    var locationMessage = document.createElement('span');
    locationMessage.innerHTML = city;
    document.getElementById('LocationStatusMessage').appendChild(locIcon);
    document.getElementById('LocationStatusMessage').appendChild(locationMessage);
}

// Prevent "Flash of unstyled content"
function PreventFOUC() {
    document.getElementById('Results').style.display = "inline";
    document.getElementById('NoteContents').value = "";
}

/* Menu Controls */
function ToggleMenu() {
    var menuList = document.getElementById('MenuListContainer');
    if ($(MenuListContainer).is(":hidden")) {

        if (usingMobileDevice) {
            document.getElementById('BodyOverlay').style.display = "inline";
        }
        $(MenuListContainer).animate({ width: 'toggle' }, 200);
    }
    else {
        CloseMenu();
    }
}
function CloseMenu() {
    $(MenuListContainer).animate({ width: 'hide' }, 200);
    document.getElementById('BodyOverlay').style.display = "none";
}

function LeaveFeedback() {
    document.getElementById('NoteContents').value = "#feedback ";
    document.getElementById('NoteContents').focus();
}