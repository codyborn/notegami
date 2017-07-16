
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
        if (!MasterViewModel.noteListViewModel.displayingRecentNotes())
        {
            return true;
        }

        // Filter notes on tokens if there exists local query tokens
        if (MasterViewModel.noteListViewModel.currentQueryTokens().length > 0) {
            var shouldBeVisible = true;
            for (var i = 0; i < MasterViewModel.noteListViewModel.currentQueryTokens().length; i++) {
                var token = MasterViewModel.noteListViewModel.currentQueryTokens()[i].toLowerCase();
                if (!(self.note().originalText().toLowerCase().indexOf(token) >= 0 ||
                    AreDatesEqualOrContained(self.note().timestamp, token) ||
                    self.note().cities.includes(token.replace('@','')))) {
                        shouldBeVisible = false;
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
        return self.note().contents().length > LARGEFONTMAXLENGTH;
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
    self.keySaveEdit = function (data, event) {
        if (event.ctrlKey && event.keyCode == 83) {
            UpdateNote(self);
            return false;
        }
        return true;
    }
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

function Note(id, text, timestamp, completed, cities) {
    var self = this;
    self.noteId = id;
    self.originalText = ko.observable(text);
    self.contents = ko.observable(text);
    self.timestamp = timestamp;
    self.cities = cities;
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
        if (new Date(MasterViewModel.noteListViewModel.currDate()).getDate() != date.getDate()) {
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
    self.latestTimestamp = ko.observable(new Date(0));
    self.addNote = function (noteFacade) {
        var noteDate = new Date(noteFacade.note().timestamp);
        if (noteDate > self.latestTimestamp()) {
            self.latestTimestamp(noteDate);
        }
        self.notes.push(noteFacade);
    }
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
        self.notes.valueHasMutated();
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
        //if (elem.nodeType == 1) {
        //    if (elem.offsetTop !== elem.saveOffsetTop) {
        //        var tempElement = elem.cloneNode(true);
        //        $(elem).css({ visibility: 'hidden' });
        //        $(tempElement).css({
        //            position: "absolute",
        //            width: window.getComputedStyle(elem).width
        //        });
        //        elem.parentNode.appendChild(tempElement);
        //        $(tempElement)
        //            .css({ top: elem.saveOffsetTop })
        //            .animate({ top: elem.offsetTop }, function () {
        //                $(elem).css({ visibility: 'visible' });
        //                elem.parentNode.removeChild(tempElement);
        //            });
        //    }
        //}
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
    self.displayingRecentNotes = ko.observable(true);
    self.noResultsFound = ko.observable(false);

    // sort notes first on completed, second on date
    self.sortedNoteNodes = ko.computed(function () {
        if (typeof self.noteNodes() == "undefined") {
            return self.noteNodes();
        }
        return self.noteNodes().sort(function (n1, n2) {
            if (new Date(n1.latestTimestamp()) < new Date(n2.latestTimestamp())) {
                return 1;
            }
            return -1;
        });
        self.notes.valueHasMutated();
    });

    self.updateCurrentQueryContent = function (queryContents) {
        if (typeof queryContents == "undefined") {
            queryContents = " ";
        }
        var newQueryTokens = ko.observableArray([]);
        var queryTokens = queryContents.split(' ');        
        for (var j = 0; j < queryTokens.length; j++) {
            if (queryTokens[j].length > 0) {
                newQueryTokens.push(queryTokens[j]);
            }
        }
        if (newQueryTokens().length == 0) {
            // default to recent if not searching anything
            var recentNoteString = DateToString(new Date(new Date().setDate(new Date().getDate() - 7))) + "-" + DateToString(new Date());
            newQueryTokens.push(recentNoteString);
        }
        self.currentQueryTokens(newQueryTokens());
    }
    self.clearCurrentQueryContent = function () {
        self.currentQueryTokens([]);
    }
    self.queryContents;
    self.queryOccurred = ko.observable(false);
    // Key is noteId, value is timestamp
    self.currentDisplayedNotes = new Array();

    // Operations
    self.paginatedMerging = function (notes, queryContents, previouslyDisplayedNotes, previouslyDisplayedTags, startIndex) {
        if (typeof startIndex == "undefined") {
            startIndex = 0;
        }
        if (typeof previouslyDisplayedNotes == "undefined") {
            // clone the existing displayed notes and clear the contents to be filled below
            previouslyDisplayedNotes = $.extend({}, self.currentDisplayedNotes);
            self.currentDisplayedNotes = new Array();
            // clone the existing note nodes to keep track of what should be removed
            previouslyDisplayedTags = $.extend({}, self.noteNodeTags);
            self.queryContents = queryContents;
        }
        var notesToProcess = new Array();
        var notesLength = notes.length;
        var lastIndex = 0;
        for (var i = 0; i+startIndex < notesLength && i < 100; i++) {
            notesToProcess.push(notes[i + startIndex]);
            lastIndex = i + startIndex + 1;
        }
        self.mergeNotes(notesToProcess, previouslyDisplayedNotes, previouslyDisplayedTags);

        // Keep merging if there exist more pages of notes, otherwise cleanup the dead notes
        if (lastIndex != notes.length) {
            window.setTimeout(function () {
                self.paginatedMerging(notes, queryContents, previouslyDisplayedNotes, previouslyDisplayedTags, lastIndex)
            }, 100);
        }
        else {
            // Cleanup notes that were not seen
            for (var key in previouslyDisplayedNotes) {
                if (previouslyDisplayedNotes[key] != null) {
                    self.removeNote(key);
                }
            }
            // Cleanup notes nodes that were not seen
            for (var key in previouslyDisplayedTags) {
                if (previouslyDisplayedTags[key] != null) {
                    delete self.noteNodeTags[key];
                }
            }
        }
        // Set the results flag
        self.noResultsFound(Object.keys(self.currentDisplayedNotes).length == 0);
    }
    self.mergeNotes = function (notes, previouslyDisplayedNotes, previouslyDisplayedTags) {        
        for (var i = notes.length - 1; i >= 0; i--) {
            // create a dummy note object to get the note tags
            var newNote = new Note("dummy", Urldecode(notes[i].encodedNote), null, false, []);
            var noteTags = newNote.getNoteTags();
            // track which tags we haven't seen by removing the ones we do see from the tracking list
            for (var j = 0; j < noteTags.length; j++) {
                var lowerCaseTag = noteTags[j].toLowerCase();
                previouslyDisplayedTags[lowerCaseTag] = null;
            }

            // If the note to be displayed doesn't exist or is out of date, attempt to remove and add it
            if (typeof previouslyDisplayedNotes[notes[i].id] == "undefined" ||
                previouslyDisplayedNotes[notes[i].id] != notes[i].lastUpdatedTime) {                
                self.removeNote(notes[i].id);
                MasterViewModel.noteListViewModel.addNote(notes[i].id, notes[i].encodedNote, notes[i].lastUpdatedTime, notes[i].completed, notes[i].city);
            }
            // remove it from the cloned list so that we know it's been checked
            previouslyDisplayedNotes[notes[i].id] = null;

            // update our currentDisplayedNotes list for next time
            self.currentDisplayedNotes[notes[i].id] = notes[i].lastUpdatedTime;
        }
    }
    self.addNote = function (id, contents, timestamp, completed, city) {
        // figure out which noteNodes this note belongs to
        var newNote = new Note(id, Urldecode(contents), timestamp, completed, city);
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
                self.noteNodeTags[lowerCaseTag] = newNoteNode;
            }
            var noteFacade = new NoteFacade(newNote, noteTags[i]);
            self.noteNodeTags[lowerCaseTag].addNote(noteFacade);
        }
    }
    // Attempt to remove note from nodes
    // If key doesn't exist, ignore and return
    // If a noteNode doesn't contain any keys, then it will not be visible
    self.removeNote = function (id) {
        // Check each note facade in each note node
        for (var i = 0; i < self.noteNodes().length; i++) {
            for (var j = 0; j < self.noteNodes()[i].notes().length; j++) {
                if (self.noteNodes()[i].notes()[j].note().noteId == id) {
                    self.noteNodes()[i].notes.splice(j, 1);
                    break;
                }
            }
        }
    }
    self.queryTag = function (noteNode) {
        if (noteNode.tag != UNCATEGORIZEDTAG) {
            HashTagClick(noteNode.tag);
        }
    }
    self.editingNote = null;
    self.editingNoteTarget = null;
    self.currDate = ko.observable(new Date());
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
        AddClickListener(element,
                function (event) {
                    var value = valueAccessor();
                    value(element, event);
                });
        //// press and hold triggers oncontextmenu()        
        //if (usingMobileDevice) {
        //    AddPressAndHoldListener(element,
        //        function (event) {
        //            var value = valueAccessor();
        //            value(element, event);
        //        });
        //}
        //else {
        //    AddClickListener(element,
        //                    function (event) {
        //                        var value = valueAccessor();
        //                        value(element, event);
        //                    });
        //}
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