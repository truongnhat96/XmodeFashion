using Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UseCase.Repository
{
    public interface IProductRepository
    {
        Task<IEnumerable<Product>> GetProductsAsync();
        Task<Product> GetProductAsync(int id);
        Task<IEnumerable<Product>> FilterBy(SortingType type, int id);
        Task<IEnumerable<Product>> GetProductsAsync(int categoryId);
        Task<IEnumerable<Product>> FindProductsAsync(string keyword);
        Task<IEnumerable<Product>> FindProductsAsync(double firstPrice, double lastPrice, int id);
        Task<Product> AddProductAsync(Product product);
        Task<Product> UpdateProductAsync(Product product);
        /// <summary>
        /// Use when user buy product
        /// </summary>
        /// <param name="id"></param>
        /// <param name="quantity"></param>
        /// <returns></returns>
        Task UpdateQuantityAsync(int id, int quantity);
        Task<Product> DeleteProductAsync(int id, string? uploadsPath = default);
        Task<Order> AddOrderAsync(Order order);
        Task<IEnumerable<Order>> GetOrdersAsync(string code);
        Task<IEnumerable<Order>> GetOrdersAsync(long payOSCode);
        Task UpdateStatusInOrder(int newStatus, long payOSCode);
    }
}
