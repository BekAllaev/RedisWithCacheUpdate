# Redis with cache update
This lab is based on this article - https://codewithmukesh.com/blog/distributed-caching-in-aspnet-core-with-redis/

Also you might want take a look to the interface with which we will work in this lab - `IDistributedCache`. Microsoft docs - https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed?view=aspnetcore-9.0

## Description
In this lab I create three endpoints, two of them are simple CRUD controllers for reading and writing `Product` and `Category` entities and the third one is the endpoint that have only one `Get` verb and returns statistical data(later on I will describe this data in more details). 

I have simulated this simple flow where statistics is stored in the cache.
![image](https://github.com/user-attachments/assets/69fcccce-e1ef-4b27-932d-b52ebab48e61)

This use case diagram explains how we can interact with system in the current lab
![image](https://github.com/user-attachments/assets/7ce2e971-f78c-4592-a79b-2e47bda0cf90)

## Instruction
Reproduce everything from the base [article](https://codewithmukesh.com/blog/distributed-caching-in-aspnet-core-with-redis/) until the point where autor starts creating `ProductService`, from this point we need to change our implementation. We won't need `ProductService`

Before we start download this NuGet packages:
- `Bogus` - we will need it in order to generate fake data
- `Microsoft.EntityFrameworkCore.Sqlite` - we will need it in order to work with SQLite database (actually we will work with in-memory database)
- `Microsoft.EntityFrameworkCore.Tools` - we will need it in order to generate and run code-first migrations

---

1. First of all let's implement models. We start with `Product` and `Category` models:

```
using System.Text.Json.Serialization;

namespace RedisWithCacheUpdate.Model
{
    public class Product
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public int CategoryId { get; set; }

        public double UnitPrice { get; set; }

        [JsonIgnore]
        public Category? Category { get; set; }
    }
}
```

```
namespace RedisWithCacheUpdate.Model
{
    public class Category
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public ICollection<Product> Products { get; set; }
    }
}
```
2. Add `Constant` class, this class will contain all constants that will be used all over the app, for example in our case I store there cache key
```
namespace RedisWithCacheUpdate
{
    public static class Constants
    {
        public const string PRODUCTS_BY_CATEGORIES_REDIS_KEY = "productsByCategories";

        public const string PRODUCTS_BY_CATEGORY_REDIS_KEY = "productsByCategory";
    }
}
```

3. Let's create `AppDbContext` where we will describe our database
```
using System.Collections.Generic;
using System.Reflection.Emit;
using System;
using Microsoft.EntityFrameworkCore;
using RedisWithCacheUpdate.Model;

namespace RedisWithCacheUpdate.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Product> Products { get; set; }

        public DbSet<Category> Categories { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        // Optionally, override OnModelCreating to specify more configuration.
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Product>()
                .HasKey(x => x.Id);

            modelBuilder.Entity<Category>()
                .HasKey(x => x.Id);

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)        // A Product has one Category
                .WithMany(c => c.Products)      // A Category has many Products
                .HasForeignKey(p => p.CategoryId)  // Specify the foreign key property
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
```
4. Now let's run this command in order to generate migration
```
dotnet ef migrations add InitialCreate
```
5. Now we will register instance of our `DbContext`. We will do it in `Program` class, also here we will call method that will run migration and another method that will seed fake data into database.
>Don't forget to add neccessary usings
>```
>using Bogus;
>using Microsoft.Data.Sqlite;
>using Microsoft.EntityFrameworkCore;
>using RedisWithCacheUpdate.Data;
>using RedisWithCacheUpdate.Model;
>```

Add method that seeds fake data, add it somewhere outside of `Main` method
> Here we use `Bogus`, using on `Bogus` we had added earlier
```
static void SeedData(AppDbContext context)
{
    // Create a Faker for the Category model.
    var categoryFaker = new Faker<Category>()
        .RuleFor(c => c.Id, f => f.IndexFaker + 1) // Auto-increment Id starting at 1
        .RuleFor(c => c.Name, f => f.Commerce.Department())
        .RuleFor(c => c.Description, f => f.Lorem.Sentence());

    int productIdCounter = 1;

    // Create a Faker for the Product model.
    // Notice that the CategoryId will be assigned later for each product.
    var productFaker = new Faker<Product>()
        .RuleFor(p => p.Id, _ => productIdCounter++) // Auto-increment Id starting at 1
        .RuleFor(p => p.Name, f => f.Commerce.ProductName())
        .RuleFor(p => p.UnitPrice, f => Convert.ToDouble(f.Commerce.Price()));

    // Generate 5 categories.
    var categories = categoryFaker.Generate(5);
    var allProducts = new List<Product>();

    // For each category, generate between 1 to 10 products.
    foreach (var category in categories)
    {
        int productCount = new Faker().Random.Int(1, 10);
        // Clone the productFaker to assign CategoryId specifically for this category.
        var productsForCategory = productFaker.Clone()
            .RuleFor(p => p.CategoryId, f => category.Id)
            .Generate(productCount);

        allProducts.AddRange(productsForCategory);
    }

    // Add to the context and save changes.
    context.Categories.AddRange(categories);
    context.Products.AddRange(allProducts);
    context.SaveChanges();
}
```

This pice of code registers `AppDbContext`
> Add somewhere in the `Program` class const with connection string, I did it this way 
>```
>private const string ConnectionString = "Data Source=file:memdb1?mode=memory&cache=shared";
>```
```
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(ConnectionString));
```

Now let's call methods that will run pending migrations and then we will call `SeedData` method 
> Add this chunk of code right after service registrations
```
var app = builder.Build();

var scope = app.Services.CreateScope();

var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
// Make sure the database is created and then seed it.
context.Database.Migrate();
context.Database.EnsureCreated();

SeedData(context);
```
6. Now let's create model for statistics. This so called statistical model is used only for storing info about - how many products are in each category
```
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
```
7. Now we will create service that will work with this model, in other words this is repo for model `ProductsByCategory` but storage is cache. Here is interface:
> I think concept of storing stastical information in the ***NoSQL*** is good, ***NoSQL*** is good for data where relations is not so principal, in our case this is stastical data, its nature is just to show info, stastical data is result of some calculations so I think it is good to store it in the `Redis`
```
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
```

And its implementation
```
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
```
8. Now let's register it in service collection. Also you can see method `SetCacheAsync`, we should call it in the `Program` so we intially set stastics when program starts. Register service using this code:
>You can put it somewhere at the end of the services registration part
```
builder.Services.AddScoped<IProductsByCateogryCacheService, ProductsByCategoryCacheService>();
```
Call `SetCacheAsync` method like this
> This code will be somewhere in the pipeline part
```
var productsByCategoryCacheService = scope.ServiceProvider.GetRequiredService<IProductsByCateogryCacheService>();
await productsByCategoryCacheService.SetCacheAsync();
```

At the end you `Program.cs` will look like this:
```
using Bogus;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RedisWithCacheUpdate.Data;
using RedisWithCacheUpdate.Model;
using RedisWithCacheUpdate.Services;

namespace RedisWithCacheUpdate
{
    public class Program
    {
        private const string ConnectionString = "Data Source=file:memdb1?mode=memory&cache=shared";

        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(ConnectionString));

            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = "localhost";
                options.ConfigurationOptions = new StackExchange.Redis.ConfigurationOptions()
                {
                    AbortOnConnectFail = true,
                    EndPoints = { options.Configuration }
                };
            });

            builder.Services.AddScoped<IProductsByCateogryCacheService, ProductsByCategoryCacheService>();

            var app = builder.Build();

            var scope = app.Services.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Make sure the database is created and then seed it.
            context.Database.Migrate();
            context.Database.EnsureCreated();

            SeedData(context);

            var productsByCategoryCacheService = scope.ServiceProvider.GetRequiredService<IProductsByCateogryCacheService>();

            await productsByCategoryCacheService.SetCacheAsync();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }

        static void SeedData(AppDbContext context)
        {
            // Create a Faker for the Category model.
            var categoryFaker = new Faker<Category>()
                .RuleFor(c => c.Id, f => f.IndexFaker + 1) // Auto-increment Id starting at 1
                .RuleFor(c => c.Name, f => f.Commerce.Department())
                .RuleFor(c => c.Description, f => f.Lorem.Sentence());

            int productIdCounter = 1;

            // Create a Faker for the Product model.
            // Notice that the CategoryId will be assigned later for each product.
            var productFaker = new Faker<Product>()
                .RuleFor(p => p.Id, _ => productIdCounter++) // Auto-increment Id starting at 1
                .RuleFor(p => p.Name, f => f.Commerce.ProductName())
                .RuleFor(p => p.UnitPrice, f => Convert.ToDouble(f.Commerce.Price()));

            // Generate 5 categories.
            var categories = categoryFaker.Generate(5);
            var allProducts = new List<Product>();

            // For each category, generate between 1 to 10 products.
            foreach (var category in categories)
            {
                int productCount = new Faker().Random.Int(1, 10);
                // Clone the productFaker to assign CategoryId specifically for this category.
                var productsForCategory = productFaker.Clone()
                    .RuleFor(p => p.CategoryId, f => category.Id)
                    .Generate(productCount);

                allProducts.AddRange(productsForCategory);
            }

            // Add to the context and save changes.
            context.Categories.AddRange(categories);
            context.Products.AddRange(allProducts);
            context.SaveChanges();
        }
    }
}
```

9. Create `ProductController`, you can scaffold it with VS.
```
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RedisWithCacheUpdate.Data;
using RedisWithCacheUpdate.Model;
using RedisWithCacheUpdate.Services;

namespace RedisWithCacheUpdate.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IProductsByCateogryCacheService _productsByCateogryCacheService;

        public ProductsController(AppDbContext context, IProductsByCateogryCacheService productsByCateogryCacheService)
        {
            _context = context;
            _productsByCateogryCacheService = productsByCateogryCacheService;
        }

        // GET: api/Products
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
        {
            return await _context.Products.ToListAsync();
        }

        // GET: api/Products/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Product>> GetProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);

            if (product == null)
            {
                return NotFound();
            }

            return product;
        }

        // PUT: api/Products/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutProduct(int id, Product product)
        {
            if (id != product.Id)
            {
                return BadRequest();
            }

            _context.Entry(product).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Products
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Product>> PostProduct(Product product)
        {
            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            await _productsByCateogryCacheService.UpdateCacheAsync();

            return CreatedAtAction("GetProduct", new { id = product.Id }, product);
        }

        // DELETE: api/Products/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.Id == id);
        }
    }
}
```
Actually this is just scaffolded controller but with little changes. We inject there `IProductsByCateogryCacheService _productsByCateogryCacheService` so we can call it right after new product was added. We do this in the `PostProduct` method. Let's take a closer look:
```
[HttpPost]
public async Task<ActionResult<Product>> PostProduct(Product product)
{
    _context.Products.Add(product);
    await _context.SaveChangesAsync();

    await _productsByCateogryCacheService.UpdateCacheAsync();

    return CreatedAtAction("GetProduct", new { id = product.Id }, product);
}
```
Here after `await _context.SaveChangesAsync()` we call `await _productsByCateogryCacheService.UpdateCacheAsync()`, so this line of code updates the cache.

10. At the end just add `StatisticsController`
> Here we just insert `IDistributedCache` that will return list of cached statistical data
```
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
```

## Testing
Now let's run our web api (don't forget to run `Redis`). When you run it you can access `OpenAPI` json file that describes our web API on this address - `https://localhost:7103/openapi/v1.json`. 

I save this file and then I import it into the Postman. Once you did it you can call get method of the `StatisticalController` and then create new product via POST method of the `ProductController`. After that execute GET method of the `StasticalController` once again and you will see that your statistics got updated. So this your are sure that cache is stored and you can manipulate it 

## Some theory (from conservations)
`Redis` stores cache in the RAM but it also can write cache into permanent storage into the disk but how this happens?  
If you run this command in your `Redis-cli`:
```
CONFIG GET save
```
You will get next result:
```
1) "save"
2) "3600 1 300 100 60 10000"
```
Maybe in your case configs will be different but what does it means?  
First pair(3600 1) means that if in the last one hour (3600 seconds = 1 hour) there were only one change(it can be read/write/update) then all data written during this write will be written to the disk.  
Second pair means the same but it says that it should be 100 operations during last 5 minutes, same for third pair, you can test it by changing configs with this command:
```
CONFIG SET save "900 1 300 100 60 10000"
```
This command above changes time limit for first pair from 1 hour to 15 minutes, so run this command, then do one write to your cache and then set timer to 15 minutes and then you can restart your `Redis` server and you will see that result of your last write operation is present, actually it was read from the disk once you have started `Redis`.

Don't confuse it with cache that is written with no expiration time. Cache with no expiration time means that this cache will live as long as `Redis` runs but once you stop the server your cache will be cleared
