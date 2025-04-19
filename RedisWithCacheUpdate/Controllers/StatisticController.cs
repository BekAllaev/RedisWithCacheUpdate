using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RedisWithCacheUpdate.StatisticsModel;

namespace RedisWithCacheUpdate.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StatisticController : ControllerBase
    {
        public StatisticController() { }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductsByCategory>>> GetProductsByCategories()
        {


            return Ok();
        }
    }
}
