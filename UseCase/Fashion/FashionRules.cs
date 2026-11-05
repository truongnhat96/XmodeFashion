using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace UseCase.Fashion;

public static class FashionRules
{
    public static void ValidateProduct(ProductInput x)
    {
        if (string.IsNullOrWhiteSpace(x.Code) || x.Code.Length > 50 || string.IsNullOrWhiteSpace(x.Name) || x.Name.Length > 120 || string.IsNullOrWhiteSpace(x.Description))
            throw new CatalogValidationException("Mã, tên và mô tả sản phẩm là bắt buộc.");
        if (x.Brand?.Length > 100 || x.Material?.Length > 250 || x.Fit?.Length > 100) throw new CatalogValidationException("Thông tin sản phẩm quá dài.");
        if (x.Audience is not ("Nam" or "Nữ" or "Unisex") || x.Price <= 0 || (x.OldPrice is not null && x.OldPrice <= x.Price))
            throw new CatalogValidationException("Đối tượng hoặc giá sản phẩm không hợp lệ.");
        if (x.Variants is null || x.Variants.Count == 0 || x.Images is null) throw new CatalogValidationException("Cần ít nhất một biến thể.");
        if (x.Variants.Any(v => string.IsNullOrWhiteSpace(v.Sku) || v.Sku.Length > 60 || string.IsNullOrWhiteSpace(v.Color) || v.Color.Length > 30 ||
            string.IsNullOrWhiteSpace(v.Size) || v.Size.Length > 20 || v.Stock < 0 || !Regex.IsMatch(v.Hex ?? "", "^#[0-9a-fA-F]{6}$")))
            throw new CatalogValidationException("Thông tin biến thể không hợp lệ.");
        if (x.Variants.Select(v => (v.Color.Trim().ToUpperInvariant(), v.Size.Trim().ToUpperInvariant())).Distinct().Count() != x.Variants.Count ||
            x.Variants.Select(v => v.Sku.Trim().ToUpperInvariant()).Distinct().Count() != x.Variants.Count ||
            x.Variants.Where(v => v.Id.HasValue).Select(v => v.Id).Distinct().Count() != x.Variants.Count(v => v.Id.HasValue))
            throw new CatalogValidationException("Biến thể hoặc SKU bị trùng.");
        if (x.Images.Select(i => i.MediaAssetId).Distinct().Count() != x.Images.Count || x.Images.Count(i => i.IsPrimary) != (x.Images.Count == 0 ? 0 : 1))
            throw new CatalogValidationException("Ảnh bị trùng hoặc chưa chọn đúng một ảnh đại diện.");
    }

    public static void ValidateOwner(CartOwner owner)
    {
        if ((owner.UserId is null) == string.IsNullOrWhiteSpace(owner.GuestKeyHash) || owner.GuestKeyHash?.Length > 64)
            throw new CatalogValidationException("Danh tính giỏ hàng không hợp lệ.");
    }

    public static void ValidateOrder(PlaceOrderInput input)
    {
        if (input.RequestId == Guid.Empty || string.IsNullOrWhiteSpace(input.RecipientName) || input.RecipientName.Length > 100 ||
            string.IsNullOrWhiteSpace(input.Email) || input.Email.Length > 100 || !new EmailAddressAttribute().IsValid(input.Email) ||
            string.IsNullOrWhiteSpace(input.Phone) || input.Phone.Length > 25 || string.IsNullOrWhiteSpace(input.Address) || input.Address.Length > 300 ||
            string.IsNullOrWhiteSpace(input.City) || input.City.Length > 100 || input.Note?.Length > 1000 || input.Lines is null ||
            input.Lines.Count == 0 || input.Lines.Any(x => x.VariantId <= 0 || x.Quantity <= 0 || x.UnitPrice <= 0) ||
            input.Lines.Select(x => x.VariantId).Distinct().Count() != input.Lines.Count)
            throw new CatalogValidationException("Thông tin đặt hàng không hợp lệ.");
    }

    public static bool CanTransition(string current, string next, bool goodsReturned) => (current, next) switch
    {
        ("Pending", "Confirmed" or "Cancelled") => true,
        ("Confirmed", "Shipping" or "Cancelled") => true,
        ("Shipping", "Completed") => true,
        ("Shipping", "Cancelled") => goodsReturned,
        _ => false
    };
}
