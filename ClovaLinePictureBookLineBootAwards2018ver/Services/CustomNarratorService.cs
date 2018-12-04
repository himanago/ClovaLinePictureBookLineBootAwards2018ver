using ClovaLinePictureBookLineBootAwards2018ver.Models.MongoDB;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ClovaLinePictureBookLineBootAwards2018ver.Services
{
    public class CustomNarratorService
    {
        public static async Task<string> GetNarratorNameAsync(string userId)
        {
            var mongoClient = new MongoClient(Consts.CosmosDbConnectionString);
            var database = mongoClient.GetDatabase(Consts.CosmosDbDatabaseName);
            var narratorCollection = database.GetCollection<CustomNarrator>(Consts.CosmosDbCustomNarratorCollectionName);
            var narratorCursor = await narratorCollection.FindAsync(n => n.Users.Contains(userId));
            var narrator = await narratorCursor.FirstOrDefaultAsync();

            if (narrator == null)
            {
                return "nozomi_emo";    // デフォルト
            }
            return narrator.Key;
        }

        public static async Task<List<CustomNarrator>> GetNarratorsAsync()
        {
            var mongoClient = new MongoClient(Consts.CosmosDbConnectionString);
            var database = mongoClient.GetDatabase(Consts.CosmosDbDatabaseName);
            var narratorCollection = database.GetCollection<CustomNarrator>(Consts.CosmosDbCustomNarratorCollectionName);
            var narratorCursor = await narratorCollection.FindAsync(n => true); // 全件
            return await narratorCursor.ToListAsync();
        }

        public static async Task UpdateCustomNarratorAsync(string userId, string narratorKey)
        {
            // ナレーターを更新
            var mongoClient = new MongoClient(Consts.CosmosDbConnectionString);
            var database = mongoClient.GetDatabase(Consts.CosmosDbDatabaseName);
            var narratorCollection = database.GetCollection<CustomNarrator>(Consts.CosmosDbCustomNarratorCollectionName);

            // 現在の設定を削除
            var currentCursor = await narratorCollection.FindAsync(n => n.Users.Contains(userId));
            var current = await currentCursor.FirstOrDefaultAsync();
            if (current != null)
            {
                var currentUsers = current.Users;
                currentUsers.Remove(userId);
                var remove = Builders<CustomNarrator>.Update.Set(n => n.Users, currentUsers);
                await narratorCollection.UpdateOneAsync(n => n.Key == current.Key, remove);
            }

            // 更新先へ追加
            var narratorCursor = await narratorCollection.FindAsync(n => n.Key == narratorKey);
            var narrator = await narratorCursor.FirstOrDefaultAsync();
            var list = narrator.Users;
            list.Add(userId);
            var update = Builders<CustomNarrator>.Update.Set(n => n.Users, list);
            await narratorCollection.UpdateOneAsync(n => n.Key == narratorKey, update);
        }

    }
}
