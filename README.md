# Redis with cache update
This lab is based on this article - https://codewithmukesh.com/blog/distributed-caching-in-aspnet-core-with-redis/

Also you might want take a look to the interface with which we will work in this lab - `IDistributedCache`. Microsoft docs - https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed?view=aspnetcore-9.0

### Description
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

2. Let's create `AppDbContext` where we will describe our database
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
3. Now let's run this command in order to generate migration
```
dotnet ef migrations add InitialCreate
```
4. Now we will register instance of our `DbContext`. We will do it in `Program` class, also here we will call method that will run migration and another method that will seed fake data into database.

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
