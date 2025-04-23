using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using RedisWithCacheUpdate.Data;
using RedisWithCacheUpdate.Extensions;
using RedisWithCacheUpdate.StatisticsModel;
using System.Collections;
using System.Collections.Generic;

namespace RedisWithCacheUpdate.Services
{
    public class ProductsByCategoryCacheService(AppDbContext context, IDistributedCache cache, ILogger<ProductsByCategoryCacheService> logger) : IProductsByCateogryCacheService
    {
        private const string NULL_CACHE_ERROR_MESSAGE = "Key not exists nor were able to set the one";
        private readonly DistributedCacheEntryOptions CacheOptions = new DistributedCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(20))
            .SetSlidingExpiration(TimeSpan.FromMinutes(2));

        public async Task<IEnumerable<ProductsByCategory>> GetListFromCacheAsync()
        {
            List<ProductsByCategory>? productsByCategories = await cache.GetAsync<List<ProductsByCategory>?>(Constants.PRODUCTS_BY_CATEGORIES_REDIS_KEY);

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
            await cache.RemoveAsync(Constants.PRODUCTS_BY_CATEGORIES_REDIS_KEY);

            var statistics = await GetStatistics();

            await SetStatistics(statistics);
        }

        private async Task SetStatistics(List<ProductsByCategory> statistics)
        {
            await cache.SetAsync(Constants.PRODUCTS_BY_CATEGORIES_REDIS_KEY, statistics, CacheOptions);
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
            if (!cache.TryGetValue(Constants.PRODUCTS_BY_CATEGORIES_REDIS_KEY, out object _))
            {
                return cache.RemoveAsync(Constants.PRODUCTS_BY_CATEGORIES_REDIS_KEY);
            }
            return Task.CompletedTask;
        }
    }
}
