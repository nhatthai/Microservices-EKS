using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NET6.Microservices.Catalog.API.Models.Repositories;

namespace NET6.Microservice.Catalog.API.Infrastructure.EntityConfigurations;

class CatalogBrandEntityTypeConfiguration: IEntityTypeConfiguration<CatalogBrand>
{
    public void Configure(EntityTypeBuilder<CatalogBrand> builder)
    {
        builder.ToTable("CatalogBrands");

        builder.HasKey(ci => ci.Id);

        builder.Property(cb => cb.Brand)
            .IsRequired()
            .HasMaxLength(100);
    }
}