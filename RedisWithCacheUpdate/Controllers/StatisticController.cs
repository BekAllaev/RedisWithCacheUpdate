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
        private readonly IProductsByCateogryCacheService productsByCategoriesCacheService;

        public StatisticController(IProductsByCateogryCacheService productsByCateogryCacheService)
        { 
            this.productsByCategoriesCacheService = productsByCateogryCacheService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductsByCategory>>> GetProductsByCategories()
        {
            var list = await productsByCategoriesCacheService.GetListFromCacheAsync();

            return list;
        }
    }
}
