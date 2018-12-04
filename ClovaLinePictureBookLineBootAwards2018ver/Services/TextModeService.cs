using ClovaLinePictureBookLineBootAwards2018ver.Models.MongoDB;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ClovaLinePictureBookLineBootAwards2018ver.Services
{
    public class TextModeService
    {
        public static async Task AddCacheAsync(string userId, TextMode textMode)
        {
            await GetCollection().InsertOneAsync(new Cache()
            {
                UserId = userId,
                TextMode = new TextModeCache()
                {
                    TextMode = textMode
                }
            });
        }

        public static async Task<Cache> FindCacheAsync(string userId)
        {
            var cacheCursor = await GetCollection().FindAsync(c => c.UserId == userId && c.TextMode != null);
            return await cacheCursor.FirstOrDefaultAsync();
        }

        public static async Task SaveAsync(Cache cache)
        {
            var update = Builders<Cache>.Update.Set(c => c.TextMode, cache.TextMode);
            await GetCollection().UpdateOneAsync(c => c.Id == cache.Id, update);
        }

        private static IMongoCollection<Cache> GetCollection()
        {
            var mongoClient = new MongoClient(Consts.CosmosDbConnectionString);
            var database = mongoClient.GetDatabase(Consts.CosmosDbDatabaseName);
            var cacheCollection = database.GetCollection<Cache>(Consts.CosmosDbTransactionCollectionName);
            return cacheCollection;
        }
    }
}
