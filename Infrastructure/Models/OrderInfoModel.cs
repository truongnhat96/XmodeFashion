namespace Infrastructure.Models
{
    public class OrderInfoModel
    {
        public string FullName { get; set; } = string.Empty;
        public string OrderId { get; set; } = string.Empty;
        public string OrderInfo {  get; set; } = string.Empty;
        public double Amount { get; set; }
    }

    public class OrderDetailModel
    {
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string OrderCode { get; set; } = string.Empty;
        public string? Note { get; set; }
    }
}
