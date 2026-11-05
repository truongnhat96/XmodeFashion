using Entities;

namespace UseCase.Repository
{
    public interface IUserRepository
    {
        Task<User> AddAccountAsync(User user);
        Task<User?> GetUserAsync(string usernameORemail);
        Task<User?> GetUserAsync(int id);
        Task<User> UpdateAccountAsync(User user);
        bool EmailExists(string email);
    }
}
