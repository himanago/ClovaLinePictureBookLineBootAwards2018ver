using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace ClovaLinePictureBookLineBootAwards2018ver.Models.MongoDB
{
    /// <summary>
    /// 絵本データのコレクション用のモデルクラスです。
    /// </summary>
    [BsonIgnoreExtraElements]
    public class Book
    {
        [BsonId]
        public ObjectId Id { get; set; }
        [BsonElement("key")]
        public string Key { get; set; }
        [BsonElement("title")]
        public string Title { get; set; }
        [BsonElement("summary")]
        public string Summary { get; set; }
        [BsonElement("speeches")]
        public List<Speech> Speeches { get; set; }
        [BsonElement("ratings")]
        public List<UserRating> Ratings { get; set; }
        [BsonElement("price")]
        public int Price { get; set; }
        [BsonElement("buyers")]
        public List<string> Buyers { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class Speech
    {
        [BsonElement("key")]
        public string Key { get; set; }
        [BsonElement("text")]
        public string Text { get; set; }
        [BsonElement("kanaText")]
        public string KanaText { get; set; }
        [BsonElement("hasImage")]
        public bool HasImage { get; set; }
        [BsonElement("speaker")]
        public string Speaker { get; set; }
        [BsonElement("speed")]
        public double Speed { get; set; }
        [BsonElement("pitch")]
        public double Pitch { get; set; }
        [BsonElement("range")]
        public double Range { get; set; }
        [BsonElement("joy")]
        public double Joy { get; set; }
        [BsonElement("sadness")]
        public double Sadness { get; set; }
        [BsonElement("anger")]
        public double Anger { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class UserRating
    {
        [BsonElement("userId")]
        public string UserId { get; set; }
        [BsonElement("rating")]
        public int Rating { get; set; }
    }
}
