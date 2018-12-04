using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace ClovaLinePictureBookLineBootAwards2018ver.Models.MongoDB
{
    [BsonIgnoreExtraElements]
    public class CustomNarrator
    {
        [BsonElement("key")]
        public string Key { get; set; }
        [BsonElement("name")]
        public string Name { get; set; }
        [BsonElement("users")]
        public List<string> Users { get; set; }
    }
}
