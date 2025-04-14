namespace RedisWithCacheUpdate.Model
{
    public class Product
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public int CategoryId { get; set; }

        public double UnitPrice { get; set; }

        public Category Category { get; set; }
    }
}
