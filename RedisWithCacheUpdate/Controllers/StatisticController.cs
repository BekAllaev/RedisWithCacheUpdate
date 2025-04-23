using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using RedisWithCacheUpdate.Extensions;
using RedisWithCacheUpdate.Services;
using RedisWithCacheUpdate.StatisticsModel;

namespace RedisWithCacheUpdate.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StatisticController : ControllerBase
    {
        private readonly IDistributedCache distributedCache;

        public StatisticController(IDistributedCache distributedCache)
        { 
            this.distributedCache = distributedCache;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductsByCategory>>> GetProductsByCategories()
        {
            var list = await distributedCache.GetAsync<List<ProductsByCategory>>(Constants.PRODUCTS_BY_CATEGORIES_REDIS_KEY);

            return list;
        }
    }
}
