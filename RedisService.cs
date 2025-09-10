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

