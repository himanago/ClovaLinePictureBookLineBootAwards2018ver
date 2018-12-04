using ClovaLinePictureBookLineBootAwards2018ver.Models.Json;
using ClovaLinePictureBookLineBootAwards2018ver.Models.MongoDB;
using Line.Messaging;
using MongoDB.Driver;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClovaLinePictureBookLineBootAwards2018ver.Services
{
    public class RateService
    {
        /// <summary>
        /// 評価を登録・更新し結果メッセージをLINEに送信します。
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="rate"></param>
        /// <param name="bookKey"></param>
        /// <param name="replyToken"></param>
        /// <param name="lineMessagingClient"></param>
        /// <returns></returns>
        public static async Task UpdateRateAndSendResultAsync(string userId, int rate, string bookKey, string replyToken, LineMessagingClient lineMessagingClient)
        {
            // DBアクセス
            var mongoClient = new MongoClient(Consts.CosmosDbConnectionString);
            var database = mongoClient.GetDatabase(Consts.CosmosDbDatabaseName);
            var bookCollection = database.GetCollection<Book>(Consts.CosmosDbBookCollectionName);
            var bookCursor = await bookCollection.FindAsync(b => b.Key == bookKey);
            var book = await bookCursor.FirstOrDefaultAsync();

            if (book.Ratings?.Any(b => b.UserId == userId) ?? false)
            {
                // 未評価の場合（新規評価）
                book.Ratings.Remove(book.Ratings.First(b => b.UserId == userId));
                book.Ratings.Add(new UserRating { UserId = userId, Rating = rate });

                var update = Builders<Book>.Update.Set(d => d.Ratings, book.Ratings);
                await bookCollection.UpdateOneAsync(b => b.Key == bookKey, update);
            }
            else
            {
                // 評価更新
                var update = Builders<Book>.Update.Push(d => d.Ratings, new UserRating { UserId = userId, Rating = rate });
                await bookCollection.UpdateOneAsync(b => b.Key == bookKey, update);
            }

            // LINEへメッセージを送る
            var message = $"評価:{rate}で登録しました！またお話がききたくなったらいつでも呼んでくださいね！";
            if (string.IsNullOrEmpty(replyToken))
            {
                // Clovaからの評価の場合はPush
                await lineMessagingClient.PushMessageAsync(userId, new[] { message });
            }
            else
            {
                // LINEからの評価の場合はReply
                await lineMessagingClient.ReplyMessageAsync(replyToken, new[] { message });
            }
        }
    }
}
