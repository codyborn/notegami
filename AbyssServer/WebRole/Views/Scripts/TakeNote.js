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
        displayingRecentNotes = true;
        cachedNotes.DisplayRecentNotes();
        cachedNotes.QueryRecentNotes();
    }
});

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
    dateList.appendChild(CreateQuickSearchButton("Today", false, DateToString(today), true));
    dateList.appendChild(CreateQuickSearchButton("Yesterday", false, DateToString(yesterday), true));
    dateList.appendChild(CreateQuickSearchButton("Last 7 Days", false, DateToString(lastWeek) + "-" + DateToString(today), true));
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
    if (response != null && response.length > 0) {
        // let's us know which nodes to show
        MasterViewModel.noteListViewModel.mergeNotes(response, queryContents);
        //ScrollToBottom();
        LimitTextAreaMaxLength();
    }
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