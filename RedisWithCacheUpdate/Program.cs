
using Bogus;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RedisWithCacheUpdate.Data;
using RedisWithCacheUpdate.Model;

namespace RedisWithCacheUpdate
{
    public class Program
    {
        private const string ConnectionString = "Data Source=file:memdb1?mode=memory&cache=shared";

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(ConnectionString));

            var app = builder.Build();

            var scope = app.Services.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Make sure the database is created and then seed it.
            context.Database.Migrate();
            context.Database.EnsureCreated();

            SeedData(context);

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
