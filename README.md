# micro-chek04


// User.cs
using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UserSessionManager
{
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public DateTime LastAccess { get; set; }
    }
}


CONEXÃO

// MongoDbService.cs
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

RedisService

using StackExchange.Redis;
using System.Threading.Tasks;
using System;
using Newtonsoft.Json;

namespace UserSessionManager
{
    public class RedisService
    {
        private readonly ConnectionMultiplexer _redis;
        private readonly IDatabase _db;

        public RedisService(string connectionString)
        {
            _redis = ConnectionMultiplexer.Connect(connectionString);
            _db = _redis.GetDatabase();
        }

        public async Task<User> GetUserAsync(string key)
        {
            var data = await _db.StringGetAsync(key);
            return data.IsNullOrEmpty ? null : JsonConvert.DeserializeObject<User>(data);
        }

        public async Task SetUserAsync(string key, User user, TimeSpan expiry)
        {
            await _db.StringSetAsync(key, JsonConvert.SerializeObject(user), expiry);
        }
    }
}


Lógica de Cache

// UserSessionManager.cs
using System;
using System.Threading.Tasks;

namespace UserSessionManager
{
    public class UserSessionManager
    {
        private readonly MongoDbService _mongoDbService;
        private readonly RedisService _redisService;
        private readonly TimeSpan _cacheExpiry;

        public UserSessionManager(MongoDbService mongoDbService, RedisService redisService, TimeSpan cacheExpiry)
        {
            _mongoDbService = mongoDbService;
            _redisService = redisService;
            _cacheExpiry = cacheExpiry;
        }

        public async Task<User> GetUserSessionAsync(string userId)
        {
            User user = null;
            try
            {
                // 1. Tentar obter o usuário do cache Redis
                user = await _redisService.GetUserAsync(userId);
                if (user != null)
                {
                    Console.WriteLine($"Usuário {userId} encontrado no cache Redis.");
                    return user;
                }

                Console.WriteLine($"Usuário {userId} não encontrado no cache Redis. Buscando no MongoDB...");

                // 2. Se não estiver no cache, buscar no MongoDB
                user = await _mongoDbService.GetUserByIdAsync(userId);

                if (user != null)
                {
                    Console.WriteLine($"Usuário {userId} encontrado no MongoDB. Armazenando no Redis...");
                    // Atualizar LastAccess antes de armazenar no cache
                    user.LastAccess = DateTime.UtcNow;
                    await _redisService.SetUserAsync(userId, user, _cacheExpiry);
                    await _mongoDbService.UpdateUserAsync(userId, user); // Atualizar LastAccess no MongoDB
                }
                else
                {
                    Console.WriteLine($"Usuário {userId} não encontrado no MongoDB.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ocorreu um erro ao obter a sessão do usuário para {userId}: {ex.Message}");
                // Logar a exceção para investigação futura
                // Dependendo da gravidade, você pode querer relançar ou retornar um valor padrão
            }
            return user;
        }
    }
}

Questões de Boas Práticas

Assincronicidade
Todas as operações assíncrona  (async/await), dão performance e escalabilidade em aplicações web.

Tempo de Expiração no Redis
 (_cacheExpiry = TimeSpan.FromMinutes(15)), garantindo que os dados no cache não fiquem obsoletos indefinidamente.

 Separação de Responsabilidades
É dividido em classes (User, MongoDbService, RedisService, UserSessionManager), promovendo a modularidade e a manutenibilidade.


