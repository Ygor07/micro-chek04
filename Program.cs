using System;
using System.Threading.Tasks;

namespace UserSessionManager
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // --- Configuration ---
            // Replace with your actual MongoDB connection string, database name, and collection name
            const string mongoDbConnectionString = "mongodb://localhost:27017";
            const string mongoDbDatabaseName = "UserDB";
            const string mongoDbCollectionName = "Users";

            // Replace with your actual Redis connection string
            const string redisConnectionString = "localhost:6379";

            // Cache expiry time (15 minutes as per requirement)
            TimeSpan cacheExpiry = TimeSpan.FromMinutes(15);

            // --- Service Initialization ---
            MongoDbService mongoDbService = null;
            RedisService redisService = null;
            UserSessionManager sessionManager = null;

            try
            {
                mongoDbService = new MongoDbService(mongoDbConnectionString, mongoDbDatabaseName, mongoDbCollectionName);
                redisService = new RedisService(redisConnectionString);
                sessionManager = new UserSessionManager(mongoDbService, redisService, cacheExpiry);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing services: {ex.Message}");
                Console.WriteLine("Please ensure MongoDB and Redis are running and accessible.");
                return; // Exit if services cannot be initialized
            }

            // --- Simulate User Login/Access ---
            string testUserId = "60c72b2f9b1e8b001c8e4d7c"; // Example User ID

            // 1. Try to get user for the first time (should hit MongoDB and then cache)
            Console.WriteLine("\n--- First attempt to get user ---");
            User user1 = await sessionManager.GetUserSessionAsync(testUserId);
            PrintUser(user1);

            // Simulate a delay
            await Task.Delay(2000);

            // 2. Try to get user again (should hit Redis cache)
            Console.WriteLine("\n--- Second attempt to get user (should be from cache) ---");
            User user2 = await sessionManager.GetUserSessionAsync(testUserId);
            PrintUser(user2);

            // --- Example: Create a new user and then retrieve it ---
            Console.WriteLine("\n--- Creating a new user and retrieving it ---");
            User newUser = new User
            {
                Name = "Test User",
                Email = "test.user@example.com",
                LastAccess = DateTime.UtcNow
            };

            try
            {
                await mongoDbService.CreateUserAsync(newUser);
                Console.WriteLine($"New user created with ID: {newUser.Id}");

                User retrievedNewUser = await sessionManager.GetUserSessionAsync(newUser.Id);
                PrintUser(retrievedNewUser);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating or retrieving new user: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to exit.");
            Console.ReadKey();
        }

        static void PrintUser(User user)
        {
            if (user != null)
            {
                Console.WriteLine($"User ID: {user.Id}");
                Console.WriteLine($"Name: {user.Name}");
                Console.WriteLine($"Email: {user.Email}");
                Console.WriteLine($"Last Access: {user.LastAccess}");
            }
            else
            {
                Console.WriteLine("User not found.");
            }
        }
    }
}

