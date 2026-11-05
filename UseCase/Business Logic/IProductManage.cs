using Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UseCase.Business_Logic
{
    public interface IProductManage
    {
        Task<Product> AddProductAsync(Product product);
        Task<Order> AddOrderAsync(Order order);
        Task<IEnumerable<Order>> GetOrdersAsync(string code);
        Task<IEnumerable<Order>> GetOrdersAsync(long payOSCode);
        Task UpdateStatusInOrder(int newStatus, long payOSCode);
        Task<Product> UpdateProductAsync(Product product);
        Task<Product> DeleteProductAsync(int id, string? uploadsPath = default);
        Task<IEnumerable<Product>> GetProductsByCategoryAsync(int categoryId);
        Task<IEnumerable<Product>> GetProductsAsync();
        Task<IEnumerable<Product>> GetProductsAsync(string keyword);
        Task<IEnumerable<Product>> FilterByPriceAsync(double firstPrice, double lastPrice, int id);
        Task<IEnumerable<Product>> Filter(SortingType type, int id);
        Task<Product> GetProductDetail(int id);
        Task<Category> GetCategoryPostAsync();
        Task<IEnumerable<Category>> GetAllCategoriesAsync();
        Task<int> GetAvgRating(int productId);
        Task AddOrUpdateCouponAsync(int Id, string coupon, int discountPercent);
        Task<Discount?> GetCouponAsync(int id);
        Task HandleProductQuantitySoldAsync(int productId, int quantity);
    }
}
