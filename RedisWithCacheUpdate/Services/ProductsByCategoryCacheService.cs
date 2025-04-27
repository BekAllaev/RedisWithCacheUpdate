using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using NRedisStack.RedisStackCommands;
using RedisWithCacheUpdate.Data;
using RedisWithCacheUpdate.Extensions;
using RedisWithCacheUpdate.Model;
using RedisWithCacheUpdate.StatisticsModel;
using StackExchange.Redis;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;

namespace RedisWithCacheUpdate.Services
{
    public class ProductsByCategoryCacheService(AppDbContext context, ConnectionMultiplexer connectionMultiplexer, ILogger<ProductsByCategoryCacheService> logger) : IProductsByCateogryCacheService
    {
        private const string NULL_CACHE_ERROR_MESSAGE = "Key not exists nor were able to set the one";
        private readonly DistributedCacheEntryOptions CacheOptions = new DistributedCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromMinutes(30))
            .SetAbsoluteExpiration(TimeSpan.FromHours(1));

        public async Task<ProductsByCategory> GetByKeyAsync(string key)
        {
            string path = $"$[?(@.CategoryName==\"{key}\")]";

            var productByCategory = await connectionMultiplexer.GetAsync<ProductsByCategory>(Constants.LIST_PRODUCTS_BY_CATEGORY_REDIS_KEY, path: path);

            return productByCategory;
        }

        public async Task<IEnumerable<ProductsByCategory>> GetListFromCacheAsync()
        {
            List<ProductsByCategory>? productsByCategories = await connectionMultiplexer.GetAsync<List<ProductsByCategory>?>(Constants.LIST_PRODUCTS_BY_CATEGORY_REDIS_KEY);

            if (productsByCategories is null)
            {
                throw new ArgumentNullException(NULL_CACHE_ERROR_MESSAGE);
            }

            return productsByCategories;
        }

        public async Task SetCacheAsync()
        {
            await DropCacheIfExist();

            var statistics = await GetStatistics();

            await SetStatistics(statistics);
        }

        public async Task UpdateCacheAsync()
        {
            await connectionMultiplexer.GetDatabase().JSON().DelAsync(Constants.LIST_PRODUCTS_BY_CATEGORY_REDIS_KEY);

            var statistics = await GetStatistics();

            await SetStatistics(statistics);
        }

        private async Task SetStatistics(List<ProductsByCategory> statistics)
        {
            await connectionMultiplexer.SetAsync(Constants.LIST_PRODUCTS_BY_CATEGORY_REDIS_KEY, statistics, CacheOptions);
        }

        private Task<List<ProductsByCategory>> GetStatistics()
        {
            var stastics = context
                .Categories
                .Select(x => new ProductsByCategory
                {
                    CategoryName = x.Name,
                    ProductCount = x.Products.Count()
                })
                .ToListAsync();

            return stastics;
        }

        private Task DropCacheIfExist()
        {
            if (!connectionMultiplexer.TryGetValue(Constants.LIST_PRODUCTS_BY_CATEGORY_REDIS_KEY, out object _))
            {
                return connectionMultiplexer.GetDatabase().JSON().DelAsync(Constants.LIST_PRODUCTS_BY_CATEGORY_REDIS_KEY);
            }
            return Task.CompletedTask;
        }
    }
}
