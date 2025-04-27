using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text;
using StackExchange.Redis;
using NRedisStack.RedisStackCommands;
using NRedisStack;

namespace RedisWithCacheUpdate.Extensions
{
    public static class DistributedCacheExtensions
    {
        private static JsonSerializerOptions serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            WriteIndented = true,
            AllowTrailingCommas = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        public static Task SetAsync<T>(this ConnectionMultiplexer connectionMultiplexer, string key, T value)
        {
            return SetAsync(connectionMultiplexer, key, value, new DistributedCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(30))
                .SetAbsoluteExpiration(TimeSpan.FromHours(1)));
        }

        public static async Task SetAsync<T>(this ConnectionMultiplexer connectionMultiplexer, string key, T value, DistributedCacheEntryOptions options)
        {
            var db = connectionMultiplexer.GetDatabase();
            var jsonDb = db.JSON();

            string json = JsonSerializer.Serialize(value);

            await jsonDb.SetAsync(Constants.LIST_PRODUCTS_BY_CATEGORY_REDIS_KEY, "$", json);

            await db.KeyExpireAsync(Constants.LIST_PRODUCTS_BY_CATEGORY_REDIS_KEY, TimeSpan.FromMinutes(30));
        }

        public static bool TryGetValue<T>(this ConnectionMultiplexer connectionMultiplexer, string key, out T? value)
        {
            var jsonDb = GetJsonDbFromConnectionMultiplexer(connectionMultiplexer);
            value = default;

            var val = jsonDb.Get(key);
            var valString = val.ToString();

            if (string.IsNullOrEmpty(valString)) return false;
            value = JsonSerializer.Deserialize<T>(val.ToString(), serializerOptions);

            return true;
        }

        public static async Task<T> GetAsync<T>(this ConnectionMultiplexer connectionMultiplexer, string key, DistributedCacheEntryOptions? options = null, string? path = null)
        {
            var jsonDb = GetJsonDbFromConnectionMultiplexer(connectionMultiplexer);

            string json = (await jsonDb.GetAsync(Constants.LIST_PRODUCTS_BY_CATEGORY_REDIS_KEY), path ?? string.Empty).ToString();
            string jsonWithoutParthesis = json.Trim('(', ')', ' ', ','); // without () from both ends

            T? list = JsonSerializer.Deserialize<T>(jsonWithoutParthesis);

            if (list is null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            return list;
        }

        public static async Task<T?> GetOrSetAsync<T>(this ConnectionMultiplexer connectionMultiplexer, string key, Func<Task<T>> task, DistributedCacheEntryOptions? options = null)
        {
            if (options == null)
            {
                options = new DistributedCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(30))
                .SetAbsoluteExpiration(TimeSpan.FromHours(1));
            }
            if (connectionMultiplexer.TryGetValue(key, out T? value) && value is not null)
            {
                return value;
            }
            value = await task();
            if (value is not null)
            {
                await connectionMultiplexer.SetAsync<T>(key, value, options);
            }
            return value;
        }

        private static JsonCommands GetJsonDbFromConnectionMultiplexer(ConnectionMultiplexer connectionMultiplexer)
        {
            var db = connectionMultiplexer.GetDatabase();
            var jsonDb = db.JSON();
            
            return jsonDb;
        }
    }
}
