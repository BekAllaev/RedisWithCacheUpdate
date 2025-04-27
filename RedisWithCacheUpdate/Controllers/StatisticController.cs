using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using RedisWithCacheUpdate.Extensions;
using RedisWithCacheUpdate.Services;
using RedisWithCacheUpdate.StatisticsModel;
using StackExchange.Redis;

namespace RedisWithCacheUpdate.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StatisticController : ControllerBase
    {
        private readonly IProductsByCateogryCacheService productsByCateogryCacheService;

        public StatisticController(IProductsByCateogryCacheService productsByCateogryCacheService)
        {
            this.productsByCateogryCacheService = productsByCateogryCacheService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductsByCategory>>> GetProductsByCategories()
        {
            var list = (await productsByCateogryCacheService.GetListFromCacheAsync()).ToList();

            return list;
        }

        [HttpGet("{key}")]
        public async Task<ActionResult<ProductsByCategory>> GetProductsByCategory(string key)
        {
            var productByCategory = await productsByCateogryCacheService.GetByKeyAsync(key);

            return productByCategory;
        }
    }
}
