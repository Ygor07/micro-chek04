using MongoDB.Driver;
using System.Threading.Tasks;

namespace UserSessionManager
{
    public class MongoDbService
    {
        private readonly IMongoCollection<User> _users;

        public MongoDbService(string connectionString, string databaseName, string collectionName)
        {
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(databaseName);
            _users = database.GetCollection<User>(collectionName);
        }

        public async Task<User> GetUserByIdAsync(string id)
        {
            return await _users.Find(user => user.Id == id).FirstOrDefaultAsync();
        }

        public async Task CreateUserAsync(User user)
        {
            await _users.InsertOneAsync(user);
        }

        public async Task UpdateUserAsync(string id, User userIn)
        {
            await _users.ReplaceOneAsync(user => user.Id == id, userIn);
        }
    }
}

