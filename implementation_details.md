# Sistema de Gerenciamento de Sessões de Usuário em C#

Este documento detalha a implementação de um sistema de gerenciamento de sessões de usuário em C#, utilizando MongoDB para persistência de dados e Redis para cache de sessões ativas.

## 1. Estrutura de Classes para o Usuário

A classe `User` representa o perfil do usuário e é mapeada para o MongoDB. As propriedades `Id`, `Name`, `Email` e `LastAccess` são incluídas conforme os requisitos.

```csharp
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
```

## 2. Conexão com MongoDB e Redis

### MongoDB Service (`MongoDbService.cs`)

O `MongoDbService` encapsula a lógica de conexão e operações CRUD básicas com o MongoDB. Ele utiliza `IMongoCollection<User>` para interagir com a coleção de usuários.

```csharp
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
```

### Redis Service (`RedisService.cs`)

O `RedisService` gerencia a conexão e as operações de cache com o Redis. Ele usa `StackExchange.Redis` e `Newtonsoft.Json` para serializar/desserializar objetos `User` para armazenamento no Redis.

```csharp
// RedisService.cs
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
```

## 3. Lógica de Verificação de Cache e Fallback para o Banco de Dados

A classe `UserSessionManager` contém a lógica principal para verificar o cache no Redis e, se o usuário não for encontrado, buscar no MongoDB. Após a busca no MongoDB, o usuário é armazenado no Redis para futuras requisições.

```csharp
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
```

## 4. Tratamento de Exceções e Boas Práticas

### Tratamento de Exceções

O código inclui blocos `try-catch` para lidar com exceções que podem ocorrer durante a inicialização dos serviços ou durante as operações de busca de usuário. Isso garante que a aplicação não falhe abruptamente e que os erros sejam registrados.

### Boas Práticas

*   **Assincronicidade (`async/await`):** Todas as operações de I/O com MongoDB e Redis são implementadas de forma assíncrona (`async/await`), o que é crucial para a performance e escalabilidade em aplicações web, pois libera threads para outras requisições enquanto aguarda respostas de I/O.
*   **Tempo de Expiração no Redis:** O cache no Redis é configurado com um tempo de expiração (`_cacheExpiry = TimeSpan.FromMinutes(15)`), garantindo que os dados no cache não fiquem obsoletos indefinidamente e que o Redis não consuma memória excessiva com dados antigos.
*   **Atualização de `LastAccess`:** A propriedade `LastAccess` do usuário é atualizada tanto no objeto retornado quanto no MongoDB e no Redis, refletindo o último acesso do usuário.
*   **Configuração Centralizada:** As strings de conexão e o tempo de expiração do cache são definidos em `Program.cs`, facilitando a configuração e manutenção.
*   **Separação de Responsabilidades:** O código é dividido em classes (`User`, `MongoDbService`, `RedisService`, `UserSessionManager`) com responsabilidades claras, promovendo a modularidade e a manutenibilidade.

## 5. Exemplo de Uso (`Program.cs`)

O arquivo `Program.cs` demonstra como inicializar os serviços e utilizar o `UserSessionManager` para simular o login e acesso de usuários, incluindo a criação de um novo usuário.

```csharp
// Program.cs
using System;
using System.Threading.Tasks;

namespace UserSessionManager
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // --- Configuração ---
            // Substitua com sua string de conexão do MongoDB, nome do banco de dados e nome da coleção
            const string mongoDbConnectionString = "mongodb://localhost:27017";
            const string mongoDbDatabaseName = "UserDB";
            const string mongoDbCollectionName = "Users";

            // Substitua com sua string de conexão do Redis
            const string redisConnectionString = "localhost:6379";

            // Tempo de expiração do cache (15 minutos conforme requisito)
            TimeSpan cacheExpiry = TimeSpan.FromMinutes(15);

            // --- Inicialização dos Serviços ---
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
                Console.WriteLine($"Erro ao inicializar serviços: {ex.Message}");
                Console.WriteLine("Por favor, certifique-se de que o MongoDB e o Redis estão em execução e acessíveis.");
                return; // Sair se os serviços não puderem ser inicializados
            }

            // --- Simular Login/Acesso do Usuário ---
            string testUserId = "60c72b2f9b1e8b001c8e4d7c"; // Exemplo de ID de Usuário

            // 1. Tentar obter o usuário pela primeira vez (deve buscar no MongoDB e depois cachear)
            Console.WriteLine("\n--- Primeira tentativa de obter usuário ---");
            User user1 = await sessionManager.GetUserSessionAsync(testUserId);
            PrintUser(user1);

            // Simular um atraso
            await Task.Delay(2000);

            // 2. Tentar obter o usuário novamente (deve buscar no cache Redis)
            Console.WriteLine("\n--- Segunda tentativa de obter usuário (deve ser do cache) ---");
            User user2 = await sessionManager.GetUserSessionAsync(testUserId);
            PrintUser(user2);

            // --- Exemplo: Criar um novo usuário e depois recuperá-lo ---
            Console.WriteLine("\n--- Criando um novo usuário e recuperando-o ---");
            User newUser = new User
            {
                Name = "Usuário Teste",
                Email = "usuario.teste@example.com",
                LastAccess = DateTime.UtcNow
            };

            try
            {
                await mongoDbService.CreateUserAsync(newUser);
                Console.WriteLine($"Novo usuário criado com ID: {newUser.Id}");

                User retrievedNewUser = await sessionManager.GetUserSessionAsync(newUser.Id);
                PrintUser(retrievedNewUser);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao criar ou recuperar novo usuário: {ex.Message}");
            }

            Console.WriteLine("\nPressione qualquer tecla para sair.");
            Console.ReadKey();
        }

        static void PrintUser(User user)
        {
            if (user != null)
            {
                Console.WriteLine($"ID do Usuário: {user.Id}");
                Console.WriteLine($"Nome: {user.Name}");
                Console.WriteLine($"Email: {user.Email}");
                Console.WriteLine($"Último Acesso: {user.LastAccess}");
            }
            else
            {
                Console.WriteLine("Usuário não encontrado.");
            }
        }
    }
}
```

## Como Executar o Projeto

1.  **Pré-requisitos:**
    *   .NET SDK 8.0 ou superior
    *   MongoDB instalado e em execução (porta padrão 27017)
    *   Redis instalado e em execução (porta padrão 6379)

2.  **Clonar o Projeto (se aplicável) ou Copiar os Arquivos:**
    Certifique-se de ter todos os arquivos (`User.cs`, `MongoDbService.cs`, `RedisService.cs`, `UserSessionManager.cs`, `Program.cs`, `user_session_manager.csproj`) na mesma pasta.

3.  **Restaurar Dependências:**
    Abra um terminal na pasta raiz do projeto (`user_session_manager`) e execute:
    ```bash
    dotnet restore
    ```

4.  **Executar a Aplicação:**
    No mesmo terminal, execute:
    ```bash
    dotnet run
    ```

    A aplicação tentará se conectar ao MongoDB e Redis e simulará as operações de busca e criação de usuários, exibindo a saída no console.

