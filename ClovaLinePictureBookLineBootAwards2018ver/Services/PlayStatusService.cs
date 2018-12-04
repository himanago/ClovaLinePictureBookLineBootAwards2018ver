using ClovaLinePictureBookLineBootAwards2018ver.Models.MongoDB;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ClovaLinePictureBookLineBootAwards2018ver.Services
{
    public class PlayStatusService
    {
        public static async Task AddCacheAsync(string userId, string beforeId, bool isFinished = false)
        {
            await GetCollection().InsertOneAsync(new Cache()
            {
                UserId = userId,
                StopRequest = new StopRequestCache()
                {
                    IsStopped = false,
                    IsPaused = false,
                    IsFinished = isFinished,
                    BeforId = beforeId
                }
            });
        }

        public static async Task<Cache> FindCacheAsync(string userId)
        {
            var cacheCursor = await GetCollection().FindAsync(c => c.UserId == userId && c.StopRequest != null);
            return await cacheCursor.FirstOrDefaultAsync();
        }

        public static async Task SaveAsync(Cache cache)
        {
            var update = Builders<Cache>.Update.Set(c => c.StopRequest, cache.StopRequest);
            await GetCollection().UpdateOneAsync(c => c.Id == cache.Id, update);
        }

        public static async Task DeleteAsync(Cache cache)
        {
            await GetCollection().DeleteOneAsync(c => c.Id == cache.Id);
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
