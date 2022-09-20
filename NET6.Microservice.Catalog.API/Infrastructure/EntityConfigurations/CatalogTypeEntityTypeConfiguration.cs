using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NET6.Microservices.Catalog.API.Models.Repositories;

namespace NET6.Microservice.Catalog.API.Infrastructure.EntityConfigurations;

class CatalogTypeEntityTypeConfiguration : IEntityTypeConfiguration<CatalogType>
{
    public void Configure(EntityTypeBuilder<CatalogType> builder)
    {
        builder.ToTable("CatalogTypes");

        builder.HasKey(ci => ci.Id);

        builder.Property(cb => cb.Type)
            .IsRequired()
            .HasMaxLength(100);
    }
}