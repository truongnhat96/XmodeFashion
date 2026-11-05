using Entities;

namespace UseCase.Business_Logic
{
    public interface IUserManage
    {
        Task<User> LoginAsync(User user, string password);
        Task<User> ChangePassAsync(User user, string newPassword);
        Task<ValidationResult> Validate(string username, string password, string confirmPassword, string email, bool passwordValid);
        Task<User> SignUpAsync(User user, string password);
        Task<User> GetUserAsync(string UoE);
        Task<User> GetUserAsync(int id);
        Task<string> GetRoleNameAsync(int id);

    }
}
