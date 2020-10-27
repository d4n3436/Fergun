using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;

namespace Fergun
{
    public class FergunDB
    {
        private readonly MongoClient client;
        private readonly IMongoDatabase db;

        public FergunDB(string database)
        {
            client = new MongoClient();
            db = client.GetDatabase(database);
        }

        public FergunDB(string database, string url)
        {
            client = new MongoClient(new MongoUrlBuilder(url).ToMongoUrl());
            db = client.GetDatabase(database);
        }

        public FergunDB(string database, string user, string password, string host = null)
        {
            if (string.IsNullOrEmpty(host))
            {
                host = "localhost";
            }
            var connectionString = $"mongodb://{user}:{password}@{host}/admin";
            client = new MongoClient(connectionString);
            db = client.GetDatabase(database);
        }

        public bool IsConnected
        {
            get
            {
                try
                {
                    client.ListDatabaseNames();
                    return client.Cluster.Description.State == ClusterState.Connected;
                }
                catch (MongoException) { return false; }
            }
        }

        public void InsertRecord<T>(string table, T record)
        {
            var collection = db.GetCollection<T>(table);
            collection.InsertOne(record);
        }

        public async Task InsertRecordAsync<T>(string table, T record)
        {
            var collection = db.GetCollection<T>(table);
            await collection.InsertOneAsync(record);
        }

        public void InsertRecords<T>(string table, List<T> records)
        {
            var collection = db.GetCollection<T>(table);
            collection.InsertMany(records);
        }

        public async Task InsertRecordsAsync<T>(string table, List<T> records)
        {
            var collection = db.GetCollection<T>(table);
            await collection.InsertManyAsync(records);
        }

        public T LoadRecord<T>(string table)
        {
            var collection = db.GetCollection<T>(table);
            return collection.Find(new BsonDocument()).Single();
        }

        public async Task<T> LoadRecordAsync<T>(string table)
        {
            var collection = db.GetCollection<T>(table);
            return await collection.Find(new BsonDocument()).SingleAsync();
        }

        public List<T> LoadRecords<T>(string table)
        {
            var collection = db.GetCollection<T>(table);
            return collection.Find(new BsonDocument()).ToList();
        }

        public async Task<List<T>> LoadRecordsAsync<T>(string table)
        {
            var collection = db.GetCollection<T>(table);
            return await collection.FindAsync(new BsonDocument()).Result.ToListAsync();
        }

        public void UpdateRecord<T>(string table, T entity) where T : IIdentity
        {
            var collection = db.GetCollection<T>(table);
            if (entity.ObjectId == ObjectId.Empty)
            {
                collection.InsertOne(entity); // driver creates ObjectId under the hood
            }
            else
            {
                collection.ReplaceOne(x => x.ObjectId == entity.ObjectId, entity);
            }
        }

        public async Task UpdateRecordAsync<T>(string table, T entity) where T : IIdentity
        {
            var collection = db.GetCollection<T>(table);
            if (entity.ObjectId == ObjectId.Empty)
            {
                await collection.InsertOneAsync(entity); // driver creates ObjectId under the hood
            }
            else
            {
                await collection.ReplaceOneAsync(x => x.ObjectId == entity.ObjectId, entity);
            }
        }

        public bool DeleteRecord<T>(string table, T entity) where T : IIdentity
        {
            var collection = db.GetCollection<T>(table);
            var result = collection.DeleteOne(x => x.ObjectId == entity.ObjectId);
            return result.IsAcknowledged;
        }

        public async Task<bool> DeleteRecordAsync<T>(string table, T entity) where T : IIdentity
        {
            var collection = db.GetCollection<T>(table);
            var result = await collection.DeleteOneAsync(x => x.ObjectId == entity.ObjectId);
            return result.IsAcknowledged;
        }

        //public void DeleteRecords<T>(string table, List<T> entities) where T : IIdentity
        //{
        //    var collection = db.GetCollection<T>(table);
        //    var ids = Builders<T>.Filter.In(x => x.ObjectId, collection);
        //    collection.DeleteMany(x => );
        //}

        public T Find<T>(string table, Expression<Func<T, bool>> filter) where T : class
        {
            var collection = db.GetCollection<T>(table);
            var result = collection.Find(filter).Limit(1).ToList();
            if (result.Count == 0)
                return default;
            return result[0];
        }

        public async Task<T> FindAsync<T>(string table, Expression<Func<T, bool>> filter) where T : class
        {
            var collection = db.GetCollection<T>(table);
            var result = await (await collection.FindAsync(filter)).ToListAsync();
            if (result.Count == 0)
                return default;
            return result[0];
        }

        public List<T> FindMany<T>(string table, Expression<Func<T, bool>> filter) where T : class
        {
            var collection = db.GetCollection<T>(table);
            return collection.Find(filter).ToList();
        }

        public async Task<List<T>> FindManyAsync<T>(string table, Expression<Func<T, bool>> filter) where T : class //FilterDefinition<T>
        {
            var collection = db.GetCollection<T>(table);
            return await (await collection.FindAsync(filter)).ToListAsync();
        }

        public void RenameCollection(string oldName, string newName)
        {
            db.RenameCollection(oldName, newName);
        }

        public async Task RenameCollectionAsync(string oldName, string newName)
        {
            await db.RenameCollectionAsync(oldName, newName);
        }

        public string RunCommand(string command)
        {
            try
            {
                var result = db.RunCommand<BsonDocument>(BsonDocument.Parse(command));
                return result.ToJson();
            }
            catch (FormatException)
            {
                return "Error";
            }
            catch (MongoCommandException)
            {
                return "Error";
            }
        }
    }
}