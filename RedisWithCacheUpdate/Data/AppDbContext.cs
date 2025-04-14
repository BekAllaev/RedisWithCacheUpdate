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
