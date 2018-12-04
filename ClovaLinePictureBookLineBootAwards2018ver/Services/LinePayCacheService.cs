using ClovaLinePictureBookLineBootAwards2018ver.Models.MongoDB;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ClovaLinePictureBookLineBootAwards2018ver.Services
{
    public static class LinePayCacheService
    {
        public static async Task AddCacheAsync(long transactionId, string userId, string title, int amount)
        {
            await GetCollection().InsertOneAsync(new Cache()
            {
                UserId = userId,
                LinePay = new LinePayCache()
                {
                    TransactionId = transactionId,
                    Title = title,
                    Amount = amount
                }
            });
        }

        public static async Task<Cache> FindCacheAsync(long transactionId)
        {
            var cacheCursor = await GetCollection().FindAsync(c => c.LinePay.TransactionId == transactionId);
            return await cacheCursor.FirstOrDefaultAsync();
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
