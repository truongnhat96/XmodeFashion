using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Data.Fashion;

// Persistent model for the fashion database.
public class FashionRole
{
    public int Id { get; set; }
    [MaxLength(20)] public required string Name { get; set; }
}

public class FashionUser
{
    public int Id { get; set; }
    [MaxLength(100)] public required string Username { get; set; }
    [MaxLength(100)] public required string PasswordHash { get; set; }
    [MaxLength(100)] public required string DisplayName { get; set; }
    [MaxLength(100)] public required string Email { get; set; }
    public int RoleId { get; set; }
    public FashionRole Role { get; set; } = null!;
}

public class FashionPost
{
    public Guid Id { get; set; }
    [MaxLength(100)] public required string Title { get; set; }
    public required string Content { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public int UserId { get; set; }
    public FashionUser User { get; set; } = null!;
}

public class MediaAsset
{
    public int Id { get; set; }
    [MaxLength(500)] public required string Url { get; set; }
    [MaxLength(250)] public required string PublicId { get; set; }
    [MaxLength(250)] public string? Alt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class FashionCategory
{
    public int Id { get; set; }
    [MaxLength(100)] public required string Name { get; set; }
    [MaxLength(1000)] public string? Description { get; set; }
    public int? ImageId { get; set; }
    public MediaAsset? Image { get; set; }
    public int SortOrder { get; set; }
    public bool IsVisible { get; set; } = true;
    public bool IsDeleted { get; set; }
}

public class SizeChart
{
    public int Id { get; set; }
    [MaxLength(100)] public required string Name { get; set; }
    [MaxLength(2000)] public string? Instructions { get; set; }
    public List<SizeChartRow> Rows { get; set; } = [];
}

public class SizeChartRow
{
    public int Id { get; set; }
    public int SizeChartId { get; set; }
    [MaxLength(20)] public required string Size { get; set; }
    [MaxLength(50)] public string? Chest { get; set; }
    [MaxLength(50)] public string? Waist { get; set; }
    [MaxLength(50)] public string? Hip { get; set; }
    public int SortOrder { get; set; }
}

public class FashionProduct
{
    public int Id { get; set; }
    [MaxLength(50)] public required string Code { get; set; }
    [MaxLength(120)] public required string Name { get; set; }
    [MaxLength(100)] public string? Brand { get; set; }
    public int CategoryId { get; set; }
    public FashionCategory Category { get; set; } = null!;
    [MaxLength(20)] public required string Audience { get; set; }
    public decimal Price { get; set; }
    public decimal? OldPrice { get; set; }
    public required string Description { get; set; }
    [MaxLength(250)] public string? Material { get; set; }
    [MaxLength(100)] public string? Fit { get; set; }
    public string? Care { get; set; }
    public bool IsNew { get; set; }
    public bool Featured { get; set; }
    public bool IsVisible { get; set; } = true;
    public bool IsDeleted { get; set; }
    public int? SizeChartId { get; set; }
    public SizeChart? SizeChart { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    [Timestamp] public byte[] Version { get; set; } = [];
    public List<ProductVariant> Variants { get; set; } = [];
    public List<FashionProductImage> Images { get; set; } = [];
}

public class ProductVariant
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public FashionProduct Product { get; set; } = null!;
    [MaxLength(60)] public required string Sku { get; set; }
    [MaxLength(30)] public required string Color { get; set; }
    [MaxLength(7)] public required string Hex { get; set; }
    [MaxLength(20)] public required string Size { get; set; }
    public int Stock { get; set; }
    public bool IsActive { get; set; } = true;
    [Timestamp] public byte[] Version { get; set; } = [];
}

public class FashionProductImage
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int MediaAssetId { get; set; }
    public MediaAsset MediaAsset { get; set; } = null!;
    public int SortOrder { get; set; }
    public bool IsPrimary { get; set; }
}

public class FashionCart
{
    public Guid Id { get; set; }
    public int? UserId { get; set; }
    public FashionUser? User { get; set; }
    [MaxLength(64)] public string? GuestKeyHash { get; set; }
    public List<FashionCartItem> Items { get; set; } = [];
}

public class FashionCartItem
{
    public Guid Id { get; set; }
    public Guid CartId { get; set; }
    public int VariantId { get; set; }
    public ProductVariant Variant { get; set; } = null!;
    public int Quantity { get; set; }
}

public class FashionOrder
{
    public Guid Id { get; set; }
    [MaxLength(40)] public required string Code { get; set; }
    public Guid RequestId { get; set; }
    public int? UserId { get; set; }
    public FashionUser? User { get; set; }
    [MaxLength(64)] public string? GuestKeyHash { get; set; }
    [MaxLength(100)] public required string RecipientName { get; set; }
    [MaxLength(100)] public required string Email { get; set; }
    [MaxLength(25)] public required string Phone { get; set; }
    [MaxLength(300)] public required string Address { get; set; }
    [MaxLength(100)] public required string City { get; set; }
    [MaxLength(1000)] public string? Note { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal Total { get; set; }
    [MaxLength(30)] public required string Status { get; set; }
    [MaxLength(10)] public string PaymentMethod { get; set; } = "COD";
    [MaxLength(20)] public string PaymentStatus { get; set; } = "Unpaid";
    public DateTime? PaymentExpiresAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Timestamp] public byte[] Version { get; set; } = [];
    public List<FashionOrderItem> Items { get; set; } = [];
    public List<FashionOrderStatusHistory> History { get; set; } = [];
}

public class FashionOrderItem
{
    public int Id { get; set; }
    public Guid OrderId { get; set; }
    public int VariantId { get; set; }
    public ProductVariant Variant { get; set; } = null!;
    [MaxLength(120)] public required string ProductName { get; set; }
    [MaxLength(60)] public required string Sku { get; set; }
    [MaxLength(30)] public required string Color { get; set; }
    [MaxLength(20)] public required string Size { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

public class FashionOrderStatusHistory
{
    public int Id { get; set; }
    public Guid OrderId { get; set; }
    [MaxLength(30)] public string? PreviousStatus { get; set; }
    [MaxLength(30)] public required string Status { get; set; }
    public int? ActorUserId { get; set; }
    [MaxLength(1000)] public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class HomeSection
{
    public int Id { get; set; }
    [MaxLength(50)] public required string Slot { get; set; }
    [MaxLength(200)] public required string Title { get; set; }
    [MaxLength(200)] public string? Subtitle { get; set; }
    [MaxLength(2000)] public string? Body { get; set; }
    public int? ImageId { get; set; }
    public MediaAsset? Image { get; set; }
    [MaxLength(100)] public string? LinkText { get; set; }
    [MaxLength(500)] public string? LinkUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsVisible { get; set; } = true;
}

public class StoreSettings
{
    public int Id { get; set; }
    [MaxLength(100)] public string? Name { get; set; }
    [MaxLength(250)] public string? Tagline { get; set; }
    [MaxLength(100)] public string? Email { get; set; }
    [MaxLength(25)] public string? Phone { get; set; }
    public decimal? ShippingFee { get; set; }
}

public class FashionPayment
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public FashionOrder Order { get; set; } = null!;
    [MaxLength(10)] public required string Provider { get; set; }
    public decimal Amount { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "Pending";
    [MaxLength(2000)] public string? Url { get; set; }
    [MaxLength(100)] public string? TransactionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    [Timestamp] public byte[] Version { get; set; } = [];
}
