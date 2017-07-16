/* LOCATION JS */
var geocoder;
var userLat;
var userLng;

//Get the latitude and the longitude;
function successFunction(position) {
    userLat = position.coords.latitude;
    userLng = position.coords.longitude;
    GetCity(userLat, userLng);
    QueryRecentTokens();
}

function GeoCoderInit() {
    geocoder = new google.maps.Geocoder();
    if (navigator.geolocation) {
        navigator.geolocation.getCurrentPosition(successFunction);
    }
}

function GetCity(lat, lng) {
    var latlng = new google.maps.LatLng(lat, lng);
    geocoder.geocode({ 'latLng': latlng }, function (results, status) {
        if (status == google.maps.GeocoderStatus.OK) {
            console.log(results)
            if (results[1]) {
                for (var i = 0; i < results[0].address_components.length; i++) {
                    for (var b = 0; b < results[0].address_components[i].types.length; b++) {
                        if (results[0].address_components[i].types[b] == "locality") {
                            city = results[0].address_components[i];
                            break;
                        }
                    }
                }
                //city data
                var userCity = city.short_name.replace(/ /g, '');
                console.log("Using " + userCity + " as city");
                DisplayCity(userCity);
            }
        }
    });
}