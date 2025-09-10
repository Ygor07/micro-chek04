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
                // 1. Try to get user from Redis cache
                user = await _redisService.GetUserAsync(userId);
                if (user != null)
                {
                    Console.WriteLine($"User {userId} found in Redis cache.");
                    return user;
                }

                Console.WriteLine($"User {userId} not found in Redis cache. Fetching from MongoDB...");

                // 2. If not in cache, fetch from MongoDB
                user = await _mongoDbService.GetUserByIdAsync(userId);

                if (user != null)
                {
                    Console.WriteLine($"User {userId} found in MongoDB. Storing in Redis...");
                    // Update LastAccess before storing in cache
                    user.LastAccess = DateTime.UtcNow;
                    await _redisService.SetUserAsync(userId, user, _cacheExpiry);
                    await _mongoDbService.UpdateUserAsync(userId, user); // Update LastAccess in MongoDB
                }
                else
                {
                    Console.WriteLine($"User {userId} not found in MongoDB.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while getting user session for {userId}: {ex.Message}");
                // Log the exception for further investigation
                // Depending on the severity, you might want to re-throw or return a default value
            }
            return user;
        }
    }
}

