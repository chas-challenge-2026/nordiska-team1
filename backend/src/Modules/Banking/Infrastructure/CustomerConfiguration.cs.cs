using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nordiska.Modules.Banking.Domain;

namespace Nordiska.Modules.Banking.Infrastructure.DbConfigs;

/*
    Each domain has their own configuration file. 
    Its possible to just map it automatically with ".ToTable". 
    However I decided to explicitly declare each field to make it more robust and scalable. 
    
*/
public sealed class CustomerConfiguration
    : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.PersonalNum)
            .IsRequired()
            .HasMaxLength(12); //todo: kom på en smartarte lösming att sätta dessa värden och kommunicera med dto:s ? ? 

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(254);

        builder.Property(x => x.PasswordHash)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => x.PersonalNum)
            .IsUnique();

        builder.HasIndex(x => x.Email)
            .IsUnique();
    }
}