using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Microsoft.Azure.Documents.Spatial;

namespace WebRole.Models
{
    public abstract class Model
    {
        [JsonProperty("id")]
        public string Id;

        [JsonProperty("userId")]
        public string UserId;
    }
}