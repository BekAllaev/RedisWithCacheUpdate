namespace RedisWithCacheUpdate.StatisticsModel
{
    /// <summary>
    /// Amount of product in each category
    /// </summary>
    public class ProductsByCategory
    {
        public string CategoryName { get; set; }

        public int ProductCount { get; set; }
    }
}
