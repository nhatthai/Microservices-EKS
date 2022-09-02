using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NET6.Microservices.Catalog.API.Models.Repositories;

namespace NET6.Microservices.Catalog.API.Infrastructure;

public class CatalogDBContext : DbContext
{
    public CatalogDBContext(DbContextOptions<CatalogDBContext> options) : base(options)
    {

    }

    public DbSet<CatalogItem> CatalogItems { get; set; }
    public DbSet<CatalogBrand> CatalogBrands { get; set; }
    public DbSet<CatalogType> CatalogTypes { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
    }
}


public class CatalogContextDesignFactory : IDesignTimeDbContextFactory<CatalogDBContext>
{
    public CatalogDBContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CatalogDBContext>()
            .UseSqlServer("Server=.;Initial Catalog=CatalogDb;Integrated Security=true");

        return new CatalogDBContext(optionsBuilder.Options);
    }
}