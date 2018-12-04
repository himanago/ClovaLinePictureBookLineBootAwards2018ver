using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace ClovaLinePictureBookLineBootAwards2018ver.Models.MongoDB
{
    /// <summary>
    /// LINE Payのキャッシュや停止要求ログ等、各種トランザクションデータ用コレクション
    /// （コレクション数節約のため、1コレクション内に同居させる）
    /// </summary>
    [BsonIgnoreExtraElements]
    public class Cache
    {
        [BsonId]
        public ObjectId Id { get; set; }
        [BsonElement("userId")]
        public string UserId { get; set; }
        [BsonElement("linePay")]
        public LinePayCache LinePay { get; set; }
        [BsonElement("stopRequest")]
        public StopRequestCache StopRequest { get; set; }
        [BsonElement("textMode")]
        public TextModeCache TextMode { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class LinePayCache
    {
        [BsonElement("transactionId")]
        public long TransactionId { get; set; }
        [BsonElement("title")]
        public string Title { get; set; }
        [BsonElement("amount")]
        public int Amount { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class StopRequestCache
    {
        [BsonElement("isStopped")]
        public bool IsStopped { get; set; }
        [BsonElement("isPaused")]
        public bool IsPaused { get; set; }
        [BsonElement("isFinished")]
        public bool IsFinished { get; set; }
        [BsonElement("beforeId")]
        public string BeforId { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class TextModeCache
    {
        [BsonElement("textMode")]
        public TextMode TextMode { get; set; }
    }
}
