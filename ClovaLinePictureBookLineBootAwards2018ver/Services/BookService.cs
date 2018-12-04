using ClovaLinePictureBookLineBootAwards2018ver.Models.MongoDB;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClovaLinePictureBookLineBootAwards2018ver.Services
{
    public static class BookService
    {
        public static async Task<List<Book>> FindAllAvailableBooksAsync(string userId)
        {
            var mongoClient = new MongoClient(Consts.CosmosDbConnectionString);
            var database = mongoClient.GetDatabase(Consts.CosmosDbDatabaseName);
            var bookCollection = database.GetCollection<Book>(Consts.CosmosDbBookCollectionName);
            var bookCursor = await bookCollection.FindAsync(
                b => b.Price == 0 || b.Buyers.Any(s => s == userId)); // 無料または購入済み
            return await bookCursor.ToListAsync();
        }

        public static async Task<List<Book>> FindAllUnavailableBooksAsync(string userId)
        {
            var mongoClient = new MongoClient(Consts.CosmosDbConnectionString);
            var database = mongoClient.GetDatabase(Consts.CosmosDbDatabaseName);
            var bookCollection = database.GetCollection<Book>(Consts.CosmosDbBookCollectionName);
            var bookCursor = await bookCollection.FindAsync(b => b.Price > 0 && !b.Buyers.Any(s => s == userId)); // 有料かつ未購入
            return await bookCursor.ToListAsync();
        }

        public static async Task<Book> FindBookByTitleOrKeyAsync(string title, string bookKey)
        {
            var mongoClient = new MongoClient(Consts.CosmosDbConnectionString);
            var database = mongoClient.GetDatabase(Consts.CosmosDbDatabaseName);
            var bookCollection = database.GetCollection<Book>(Consts.CosmosDbBookCollectionName);
            var bookCursor = await bookCollection.FindAsync(b => b.Title == title || b.Key == bookKey);
            return await bookCursor.FirstOrDefaultAsync();
        }

        public static async Task SaveBuyerAsync(Cache cache)
        {
            var mongoClient = new MongoClient(Consts.CosmosDbConnectionString);
            var database = mongoClient.GetDatabase(Consts.CosmosDbDatabaseName);
            var bookCollection = database.GetCollection<Book>(Consts.CosmosDbBookCollectionName);
            var bookCursor = await bookCollection.FindAsync(b => b.Title == cache.LinePay.Title);
            var book = await bookCursor.FirstOrDefaultAsync();

            book.Buyers.Add(cache.UserId);

            var update = Builders<Book>.Update.Set(d => d.Buyers, book.Buyers);
            await bookCollection.UpdateOneAsync(b => b.Title == cache.LinePay.Title, update);
        }
    }
}
