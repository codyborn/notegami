// Constants
var MAXNOTELENGTH = 10000;
var UNCATEGORIZEDTAG = "Uncategorized";
// 550 accounts for the top banner and some padding
var RESULTSSCROLLBUFFER = 200;
var MAXRECENTTOKENSTODISPLAYFORBROWSER = 7;
var MAXRECENTTOKENSTODISPLAYFORMOBILE = 15;
var LARGEFONTMAXLENGTH = 40;

/* Browser detection */
var is_chrome = navigator.userAgent.indexOf('Chrome') > -1;
var is_explorer = navigator.userAgent.indexOf('MSIE') > -1;
var is_firefox = navigator.userAgent.indexOf('Firefox') > -1;
var is_safari = navigator.userAgent.indexOf("Safari") > -1;
var is_opera = navigator.userAgent.toLowerCase().indexOf("op") > -1;
if ((is_chrome) && (is_safari)) { is_safari = false; }
if ((is_chrome) && (is_opera)) { is_chrome = false; }

function StoreAuthToken(email, password, token) {
    CacheStoreSet("email", email);
    CacheStoreSet("password", password);
    CacheStoreSet("token", token);
}
function ClearAuthToken() {
    CacheStoreSet("email", null);
    CacheStoreSet("password", null);
    CacheStoreSet("token", null);
}

function CacheStoreGet(key)
{
    if (typeof localStorage != "undefined" && localStorage.getItem(key) != null && localStorage.getItem(key) != "null") {
        return localStorage.getItem(key);
    }
    if (document.cookie.indexOf(key) != -1) {
        var cookieList = document.cookie.split(';');
        for (var i = 0; i < cookieList.length; i++) {
            var cookieKVP = cookieList[i].split('=');
            if (cookieKVP.length == 2 && cookieKVP[0].trim() == key) {                
                if (cookieKVP[1] != "null") {
                    return cookieKVP[1];
                }
            }
        }
    }
    return null;
}

function CacheStoreSet(key, value) {
    if (typeof localStorage != "undefined") {
        localStorage.setItem(key, value);
    }
    SetCookie(key, value, 30);
}
function SetCookie(cname, cvalue, exdays) {
    var d = new Date();
    d.setTime(d.getTime() + (exdays * 24 * 60 * 60 * 1000));
    var expires = "expires=" + d.toUTCString();
    document.cookie = cname + "=" + cvalue + "; " + expires;
}

function AuthUserAndSetCookie(email, password, callbackOnSuccess, callbackOnFailure) {
    var user =
    {
        Email: email,
        Password: password
    }

    $.ajax({
        type: "POST",
        url: "../user/AuthUser",
        data: user,
        success: function (response) {
            if (response != "") {
                StoreAuthToken(email, password, response);
                CacheStoreSet('prevSignup', true);
                console.log('found user: ' + response);
                if (callbackOnSuccess != null) {
                    callbackOnSuccess();
                }
            }
            else {
                if (callbackOnFailure != null) {
                    callbackOnFailure();
                }
            }
        },
        dataType: 'json'
    });
}
// Must navigate to the Signup.html page prior to calling
function showError(message) {
    document.getElementById("errorDisplay").innerHTML = message;
    document.getElementById('SigninButton').disabled = false;
    document.getElementById('SignupButton').disabled = false;
}

function Urldecode(str) {
    return decodeURIComponent((str + '').replace(/\+/g, '%20').replace(new RegExp("%0a", 'g'), "\n"));
}

function PerformActionIfReturn(event) {
    var x = event.which || event.keyCode;
    if (x == 13) // return
    {
        activeAction();
    }
}

function MakeFullScreenOnAction() {
    var body = document.documentElement;
    if (body.requestFullscreen) {
        body.requestFullscreen();
    } else if (body.webkitrequestFullscreen) {
        body.webkitrequestFullscreen();
    } else if (body.mozrequestFullscreen) {
        body.mozrequestFullscreen();
    } else if (body.msrequestFullscreen) {
        body.msrequestFullscreen();
    }
}

function ShowLoading(buttonId) {
    document.getElementById(buttonId).style.backgroundImage = "url('Images/Loading_icon.gif')";
    document.getElementById(buttonId).className = "StatusButton";
    document.getElementById(buttonId).disabled = true;
}
function ShowLoadingByClassName(className) {
    var buttons = document.getElementsByClassName(className);
    for (var i = 0; i < buttons.length; i++) {
        buttons[i].style.backgroundImage = "url('Images/Loading_icon.gif')";
        buttons[i].classList.add("StatusButton");
        buttons[i].disabled = true;
    }
}
function HideButtonImage(buttonId) {
    document.getElementById(buttonId).className = "";
    document.getElementById(buttonId).style.backgroundImage = "";
    document.getElementById(buttonId).disabled = false;
}
function HideButtonImageByClassName(className) {
    var buttons = document.getElementsByClassName(className);
    for (var i = 0; i < buttons.length; i++) {
        buttons[i].style.backgroundImage = "";
        buttons[i].classList.remove("StatusButton");
        buttons[i].disabled = false;
    }
}
function ShowCompletedIcon(buttonId) {
    document.getElementById(buttonId).style.backgroundImage = "url('Images/check_icon.gif')";
    document.getElementById(buttonId).className = "StatusButton";
    window.setTimeout(function () { HideButtonImage(buttonId); }, 2200);
}

function GetQSParameterByName(name, url) {
    if (!url) url = window.location.href;
    name = name.replace(/[\[\]]/g, "\\$&");
    var regex = new RegExp("[?&]" + name + "(=([^&]*)|&|$)"),
        results = regex.exec(url);
    if (!results) return null;
    if (!results[2]) return '';
    return decodeURIComponent(results[2].replace(/\+/g, " "));
}

function Redirect(uri) {
    if (navigator.userAgent.match(/Android/i)) {        
        document.location = location.origin + "/Views/" + uri;
    }
    else {
        window.location.replace(uri);
    }
}
var showUnload = false;
window.addEventListener("beforeunload", function (e) {
    if (!showUnload && document.getElementById('NoteContents').value == "") {
        return undefined;
    }
    var confirmationMessage = "Leave unsaved note?";
    (e || window.event).returnValue = confirmationMessage; //Gecko + IE
    return confirmationMessage; //Gecko + Webkit, Safari, Chrome etc.
});

function AddWindowUnload() {
    showUnload = true;
}
function RemoveWindowUnload() {
    showUnload = false;
}
function placeCaretAtEnd(el) {
    el.focus();
    if (typeof window.getSelection != "undefined"
            && typeof document.createRange != "undefined") {
        var range = document.createRange();
        range.selectNodeContents(el);
        range.collapse(false);
        var sel = window.getSelection();
        sel.removeAllRanges();
        sel.addRange(range);
    } else if (typeof document.body.createTextRange != "undefined") {
        var textRange = document.body.createTextRange();
        textRange.moveToElementText(el);
        textRange.collapse(false);
        textRange.select();
    }
}
String.prototype.lines = function () { return this.split(/\r*\n/); }
String.prototype.lineCount = function () { return this.lines().length - (navigator.userAgent.indexOf("MSIE") != -1); }