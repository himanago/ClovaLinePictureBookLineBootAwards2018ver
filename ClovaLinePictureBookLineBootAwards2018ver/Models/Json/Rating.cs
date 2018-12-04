using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ClovaLinePictureBookLineBootAwards2018ver.Models.Json
{
    public class Rating
    {
        [JsonProperty("key")]
        public string Key { get; set; }
        [JsonProperty("rate")]
        public int Rate { get; set; }
    }
}
