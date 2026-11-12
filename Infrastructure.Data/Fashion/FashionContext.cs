using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data.Fashion;

public class FashionContext(DbContextOptions<FashionContext> options) : DbContext(options)
{
    public DbSet<FashionRole> Roles => Set<FashionRole>();
    public DbSet<FashionUser> Users => Set<FashionUser>();
    public DbSet<FashionPost> Posts => Set<FashionPost>();
    public DbSet<FashionCategory> Categories => Set<FashionCategory>();
    public DbSet<FashionProduct> Products => Set<FashionProduct>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<FashionProductImage> ProductImages => Set<FashionProductImage>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<FashionCart> Carts => Set<FashionCart>();
    public DbSet<FashionCartItem> CartItems => Set<FashionCartItem>();
    public DbSet<FashionPayment> Payments => Set<FashionPayment>();
    public DbSet<FashionOrder> Orders => Set<FashionOrder>();
    public DbSet<FashionOrderItem> OrderItems => Set<FashionOrderItem>();
    public DbSet<FashionOrderStatusHistory> OrderStatusHistories => Set<FashionOrderStatusHistory>();
    public DbSet<HomeSection> HomeSections => Set<HomeSection>();
    public DbSet<StoreSettings> StoreSettings => Set<StoreSettings>();
    public DbSet<SizeChart> SizeCharts => Set<SizeChart>();
    public DbSet<SizeChartRow> SizeChartRows => Set<SizeChartRow>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<FashionRole>().HasIndex(x => x.Name).IsUnique();
        b.Entity<FashionUser>().HasIndex(x => x.Username).IsUnique();
        b.Entity<FashionUser>().HasIndex(x => x.Email).IsUnique();
        b.Entity<MediaAsset>().HasIndex(x => x.PublicId).IsUnique();
        b.Entity<FashionCategory>().HasIndex(x => x.Name).IsUnique().HasFilter("[IsDeleted] = 0");
        b.Entity<FashionProduct>().HasIndex(x => x.Code).IsUnique();
        b.Entity<FashionProduct>().HasIndex(x => x.Name);
        b.Entity<FashionProduct>().Property(x => x.Price).HasPrecision(18, 2);
        b.Entity<FashionProduct>().Property(x => x.OldPrice).HasPrecision(18, 2);
        b.Entity<FashionProduct>().ToTable(t => t.HasCheckConstraint("CK_Product_Prices", "[Price] > 0 AND ([OldPrice] IS NULL OR [OldPrice] > [Price])"));
        b.Entity<FashionProduct>().HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<FashionProduct>().HasOne(x => x.SizeChart).WithMany().HasForeignKey(x => x.SizeChartId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<FashionProduct>().HasMany(x => x.Variants).WithOne(x => x.Product).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<FashionProduct>().HasMany(x => x.Images).WithOne().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ProductVariant>().HasIndex(x => x.Sku).IsUnique();
        b.Entity<ProductVariant>().HasIndex(x => new { x.ProductId, x.Color, x.Size }).IsUnique();
        b.Entity<ProductVariant>().ToTable(t => t.HasCheckConstraint("CK_Variant_Stock", "[Stock] >= 0"));
        b.Entity<FashionProductImage>().HasOne(x => x.MediaAsset).WithMany().HasForeignKey(x => x.MediaAssetId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<FashionProductImage>().HasIndex(x => x.ProductId).IsUnique().HasFilter("[IsPrimary] = 1");
        b.Entity<FashionCart>().HasIndex(x => x.UserId).IsUnique().HasFilter("[UserId] IS NOT NULL");
        b.Entity<FashionCart>().HasMany(x => x.Items).WithOne().HasForeignKey(x => x.CartId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<FashionCart>().HasIndex(x => x.GuestKeyHash).IsUnique().HasFilter("[GuestKeyHash] IS NOT NULL");
        b.Entity<FashionCart>().ToTable(t => t.HasCheckConstraint("CK_Cart_Owner", "([UserId] IS NOT NULL AND [GuestKeyHash] IS NULL) OR ([UserId] IS NULL AND [GuestKeyHash] IS NOT NULL)"));
        b.Entity<FashionCartItem>().HasIndex(x => new { x.CartId, x.VariantId }).IsUnique();
        b.Entity<FashionCartItem>().HasOne(x => x.Variant).WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<FashionCartItem>().ToTable(t => t.HasCheckConstraint("CK_CartItem_Quantity", "[Quantity] > 0"));
        b.Entity<FashionPayment>().HasIndex(x => x.OrderId).IsUnique();
        b.Entity<FashionPayment>().HasIndex(x => new { x.Provider, x.TransactionId }).IsUnique().HasFilter("[TransactionId] IS NOT NULL");
        b.Entity<FashionPayment>().Property(x => x.Amount).HasPrecision(18, 2);
        b.Entity<FashionPayment>().HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<FashionOrder>().Property(x => x.PaymentMethod).HasDefaultValue("COD");
        b.Entity<FashionOrder>().Property(x => x.PaymentStatus).HasDefaultValue("Unpaid");
        b.Entity<FashionOrder>().HasIndex(x => new { x.PaymentStatus, x.PaymentExpiresAt });
        b.Entity<FashionOrder>().HasIndex(x => x.Code).IsUnique();
        b.Entity<FashionOrder>().HasMany(x => x.Items).WithOne().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<FashionOrder>().HasMany(x => x.History).WithOne().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<FashionOrder>().HasIndex(x => x.RequestId).IsUnique();
        b.Entity<FashionOrder>().Property(x => x.ShippingFee).HasPrecision(18, 2);
        b.Entity<FashionOrder>().Property(x => x.Total).HasPrecision(18, 2);
        b.Entity<FashionOrder>().ToTable(t => t.HasCheckConstraint("CK_Order_Amounts", "[ShippingFee] >= 0 AND [Total] >= 0"));
        b.Entity<FashionOrderItem>().Property(x => x.UnitPrice).HasPrecision(18, 2);
        b.Entity<FashionOrderItem>().HasOne(x => x.Variant).WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<FashionOrderItem>().ToTable(t => t.HasCheckConstraint("CK_OrderItem_Amounts", "[Quantity] > 0 AND [UnitPrice] > 0"));
        b.Entity<SizeChartRow>().HasIndex(x => new { x.SizeChartId, x.Size }).IsUnique();
        b.Entity<HomeSection>().HasIndex(x => x.Slot).IsUnique();
        b.Entity<StoreSettings>().Property(x => x.Id).ValueGeneratedNever();
        b.Entity<StoreSettings>().Property(x => x.ShippingFee).HasPrecision(18, 2);
        b.Entity<StoreSettings>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_StoreSettings_Fee", "[ShippingFee] IS NULL OR [ShippingFee] >= 0");
            t.HasCheckConstraint("CK_StoreSettings_Singleton", "[Id] = 1");
        });
    }
}
