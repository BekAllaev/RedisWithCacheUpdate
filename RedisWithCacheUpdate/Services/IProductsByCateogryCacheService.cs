using RedisWithCacheUpdate.StatisticsModel;

namespace RedisWithCacheUpdate.Services
{
    /// <summary>
    /// CRUD operations for products by categories statistics
    /// All operations occur in cache
    /// </summary>
    public interface IProductsByCateogryCacheService
    {
        /// <summary>
        /// Method is runned when app starts
        /// </summary>
        /// <returns></returns>
        Task SetCacheAsync();

        /// <summary>
        /// Recalculate stastics and update cache
        /// </summary>
        /// <returns></returns>
        Task UpdateCacheAsync();

        Task<IEnumerable<ProductsByCategory>> GetListFromCacheAsync();
    }
}
