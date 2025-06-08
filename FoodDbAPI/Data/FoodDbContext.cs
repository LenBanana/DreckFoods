using System.Text.Json;
using FoodDbAPI.Models;
using FoodDbAPI.Models.Fddb;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FoodDbAPI.Data;

public class FoodDbContext(DbContextOptions<FoodDbContext> options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<UserWeightEntry> WeightEntries { get; set; }
    public DbSet<FoodEntry> FoodEntries { get; set; }
    public DbSet<FddbFood> FddbFoods { get; set; }
    public DbSet<FddbFoodNutrition> FddbFoodNutritions { get; set; }
    public DbSet<Meal> Meals { get; set; }
    public DbSet<MealItem> MealItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        var stringListConverter = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
        });

        // UserWeightEntry configuration
        modelBuilder.Entity<UserWeightEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.User)
                .WithMany(u => u.WeightEntries)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.UserId, e.RecordedAt });
        });

        // FoodEntry configuration
        modelBuilder.Entity<FoodEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.User)
                .WithMany(u => u.FoodEntries)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Property(e => e.FoodName).HasMaxLength(200);
            entity.Property(e => e.Brand).HasMaxLength(100);
            entity.Property(e => e.ConsumedAt)
                .HasConversion(
                    v => v.ToUniversalTime(),
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            entity.HasIndex(e => new { e.UserId, e.ConsumedAt });
        });

        // FddbFood configuration
        modelBuilder.Entity<FddbFood>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(500);
            entity.Property(e => e.Url).HasMaxLength(1000);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.ImageUrl).HasMaxLength(1000);
            entity.Property(e => e.Brand).HasMaxLength(200);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.Brand);
            
            // Use the string list converter for Tags
            entity.Property(e => e.Tags)
                .HasColumnName("TagsJson")
                .HasConversion(stringListConverter)
                .HasMaxLength(1000);

            // Configure the one-to-one relationship with nutrition
            entity.HasOne(e => e.Nutrition)
                .WithOne(n => n.FddbFood)
                .HasForeignKey<FddbFoodNutrition>(n => n.FddbFoodId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // FddbFoodNutrition configuration
        modelBuilder.Entity<FddbFoodNutrition>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.FddbFoodId).IsUnique();
        });
        
        // Meal configuration
        modelBuilder.Entity<Meal>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.Property(e => e.CreatedAt)
                .HasConversion(
                    v => v.ToUniversalTime(),
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
                    
            entity.Property(e => e.UpdatedAt)
                .HasConversion(
                    v => v.ToUniversalTime(),
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
                    
            entity.HasIndex(e => e.UserId);
        });
        
        // MealItem configuration
        modelBuilder.Entity<MealItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.HasOne(e => e.Meal)
                .WithMany(m => m.MealItems)
                .HasForeignKey(e => e.MealId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.FddbFood)
                .WithMany()
                .HasForeignKey(e => e.FddbFoodId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent deletion of food items that are part of a meal
                
            entity.HasIndex(e => e.MealId);
        });
    }
}
