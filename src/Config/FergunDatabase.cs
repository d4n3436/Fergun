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
    /// <summary>
    /// Represents the bot database.
    /// </summary>
    public class FergunDatabase
    {
        private readonly MongoClient _client;
        private readonly IMongoDatabase _database;

        /// <summary>
        /// Initializes a new instance of the <see cref="FergunDatabase"/> class with the provided database name.
        /// </summary>
        /// <param name="database">The name of the database.</param>
        public FergunDatabase(string database)
        {
            _client = new MongoClient();
            _database = _client.GetDatabase(database);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FergunDatabase"/> class with the provided database name and connection string.
        /// </summary>
        /// <param name="database">The name of the database.</param>
        /// <param name="url">The connection string.</param>
        public FergunDatabase(string database, string url)
        {
            _client = new MongoClient(new MongoUrlBuilder(url).ToMongoUrl());
            _database = _client.GetDatabase(database);
        }

        /// <summary>
        /// Gets whether the bot is connected to the database.
        /// </summary>
        public bool IsConnected
        {
            get
            {
                try
                {
                    _client.ListDatabaseNames();
                    return _client.Cluster.Description.State == ClusterState.Connected;
                }
                catch (MongoException) { return false; }
            }
        }

        /// <summary>
        /// Inserts a document.
        /// </summary>
        /// <param name="collection">The collection.</param>
        /// <param name="document">The document.</param>
        public void InsertDocument<T>(string collection, T document)
        {
            var c = _database.GetCollection<T>(collection);
            c.InsertOne(document);
        }

        /// <summary>
        /// Inserts a document asynchronously.
        /// </summary>
        /// <param name="collection">The collection.</param>
        /// <param name="document">The document.</param>
        public async Task InsertDocumentAsync<T>(string collection, T document)
        {
            var c = _database.GetCollection<T>(collection);
            await c.InsertOneAsync(document);
        }

        /// <summary>
        /// Inserts multiple documents.
        /// </summary>
        /// <param name="collection">The collection.</param>
        /// <param name="documents">The documents.</param>
        public void InsertDocuments<T>(string collection, IEnumerable<T> documents)
        {
            var c = _database.GetCollection<T>(collection);
            c.InsertMany(documents);
        }

        /// <summary>
        /// Inserts multiple documents asynchronously.
        /// </summary>
        /// <param name="collection">The collection.</param>
        /// <param name="documents">The documents.</param>
        public async Task InsertDocumentsAsync<T>(string collection, IEnumerable<T> documents)
        {
            var c = _database.GetCollection<T>(collection);
            await c.InsertManyAsync(documents);
        }

        /// <summary>
        /// Inserts or updates a document.
        /// </summary>
        /// <typeparam name="T">A type that inherits from <see cref="IIdentity"/>.</typeparam>
        /// <param name="collection">The collection.</param>
        /// <param name="document">The document.</param>
        public void InsertOrUpdateDocument<T>(string collection, T document) where T : IIdentity
        {
            var c = _database.GetCollection<T>(collection);
            if (document.ObjectId == ObjectId.Empty)
            {
                c.InsertOne(document); // driver creates ObjectId under the hood
            }
            else
            {
                c.ReplaceOne(x => x.ObjectId == document.ObjectId, document);
            }
        }

        /// <summary>
        /// Inserts or updates a document asynchronously.
        /// </summary>
        /// <typeparam name="T">A type that inherits from <see cref="IIdentity"/>.</typeparam>
        /// <param name="collection">The collection.</param>
        /// <param name="document">The document.</param>
        public async Task InsertOrUpdateDocumentAsync<T>(string collection, T document) where T : IIdentity
        {
            var c = _database.GetCollection<T>(collection);
            if (document.ObjectId == ObjectId.Empty)
            {
                await c.InsertOneAsync(document); // driver creates ObjectId under the hood
            }
            else
            {
                await c.ReplaceOneAsync(x => x.ObjectId == document.ObjectId, document);
            }
        }

        /// <summary>
        /// Gets a single document.
        /// </summary>
        /// <param name="collection">The collection.</param>
        public T GetSingleDocument<T>(string collection)
        {
            var c = _database.GetCollection<T>(collection);
            return c.Find(new BsonDocument()).Single();
        }

        /// <summary>
        /// Gets a single document asynchronously.
        /// </summary>
        /// <param name="collection">The collection.</param>
        public async Task<T> GetSingleDocumentAsync<T>(string collection)
        {
            var c = _database.GetCollection<T>(collection);
            return await c.Find(new BsonDocument()).SingleAsync();
        }

        /// <summary>
        /// Gets all the documents in the collection.
        /// </summary>
        /// <param name="collection">The collection.</param>
        public IEnumerable<T> GetAllDocuments<T>(string collection)
        {
            var c = _database.GetCollection<T>(collection);
            return c.Find(new BsonDocument()).ToEnumerable();
        }

        /// <summary>
        /// Gets all the documents in the collection asynchronously.
        /// </summary>
        /// <param name="collection">The collection.</param>
        public async Task<IAsyncEnumerable<T>> GetAllDocumentsAsync<T>(string collection)
        {
            var c = _database.GetCollection<T>(collection);
            return (await c.FindAsync(new BsonDocument())).ToEnumerable().ToAsyncEnumerable();
        }

        /// <summary>
        /// Deletes a document.
        /// </summary>
        /// <typeparam name="T">A type that inherits from <see cref="IIdentity"/>.</typeparam>
        /// <param name="collection">The collection.</param>
        /// <param name="document">The document.</param>
        public bool DeleteDocument<T>(string collection, T document) where T : IIdentity
        {
            var c = _database.GetCollection<T>(collection);
            var result = c.DeleteOne(x => x.ObjectId == document.ObjectId);
            return result.IsAcknowledged;
        }

        /// <summary>
        /// Deletes a document asynchronously.
        /// </summary>
        /// <typeparam name="T">A type that inherits from <see cref="IIdentity"/>.</typeparam>
        /// <param name="collection">The collection.</param>
        /// <param name="document">The document.</param>
        public async Task<bool> DeleteDocumentAsync<T>(string collection, T document) where T : IIdentity
        {
            var c = _database.GetCollection<T>(collection);
            var result = await c.DeleteOneAsync(x => x.ObjectId == document.ObjectId);
            return result.IsAcknowledged;
        }

        /// <summary>
        /// Finds a document.
        /// </summary>
        /// <param name="collection">The collection.</param>
        /// <param name="filter">The filter.</param>
        public T FindDocument<T>(string collection, Expression<Func<T, bool>> filter) where T : class
        {
            var c = _database.GetCollection<T>(collection);
            return c.Find(filter).Limit(1).FirstOrDefault();
        }

        /// <summary>
        /// Finds a document asynchronously.
        /// </summary>
        /// <param name="collection">The collection.</param>
        /// <param name="filter">The filter.</param>
        public async Task<T> FindDocumentAsync<T>(string collection, Expression<Func<T, bool>> filter) where T : class
        {
            var c = _database.GetCollection<T>(collection);
            return await (await c.FindAsync(filter)).FirstOrDefaultAsync();
        }

        /// <summary>
        /// Finds multiple documents.
        /// </summary>
        /// <param name="collection">The collection.</param>
        /// <param name="filter">The filter.</param>
        public IEnumerable<T> FindManyDocuments<T>(string collection, Expression<Func<T, bool>> filter) where T : class
        {
            var c = _database.GetCollection<T>(collection);
            return c.Find(filter).ToEnumerable();
        }

        /// <summary>
        /// Finds multiple documents asynchronously.
        /// </summary>
        /// <param name="collection">The collection.</param>
        /// <param name="filter">The filter.</param>
        public async Task<IAsyncEnumerable<T>> FindManyDocumentsAsync<T>(string collection, Expression<Func<T, bool>> filter) where T : class
        {
            var c = _database.GetCollection<T>(collection);
            return (await c.FindAsync(filter)).ToEnumerable().ToAsyncEnumerable();
        }

        /// <summary>
        /// Renames a collection.
        /// </summary>
        /// <param name="oldName">The old name.</param>
        /// <param name="newName">The new name.</param>
        public void RenameCollection(string oldName, string newName)
        {
            _database.RenameCollection(oldName, newName);
        }

        /// <summary>
        /// Renames a collection asynchronously.
        /// </summary>
        /// <param name="oldName">The old name.</param>
        /// <param name="newName">The new name.</param>
        public async Task RenameCollectionAsync(string oldName, string newName)
        {
            await _database.RenameCollectionAsync(oldName, newName);
        }

        /// <summary>
        /// Runs a command.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>The result of the commands.</returns>
        public string RunCommand(string command)
        {
            try
            {
                var result = _database.RunCommand<BsonDocument>(BsonDocument.Parse(command));
                return result.ToJson();
            }
            catch (FormatException)
            {
                return null;
            }
            catch (MongoCommandException)
            {
                return null;
            }
        }
    }
}