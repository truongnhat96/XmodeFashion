using Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;
using UseCase.Business_Logic;
using UseCase.UnitOfWork;

namespace UseCase
{
    public class UserManage : IUserManage
    {
        private readonly IUserUnitOfWork _unitOfWork;
        private readonly PasswordHasher<User> _passwordHasher;

        public UserManage(IUserUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            _passwordHasher = new PasswordHasher<User>();
        }

        public Task<User> ChangePassAsync(User user, string newPassword)
        {
            var hashedPassword = _passwordHasher.HashPassword(user, newPassword);
            user.PasswordHash = hashedPassword;
            return _unitOfWork.UserRepository.UpdateAccountAsync(user);
        }

        public async Task<string> GetRoleNameAsync(int id)
        {
            var role = await _unitOfWork.RoleRepository.GetRoleAsync(id);
            return role.Name;
        }

        public async Task<User> GetUserAsync(string UoE)
        {
            return await _unitOfWork.UserRepository.GetUserAsync(UoE) ?? throw new ArgumentNullException("Email hoặc người dùng không tồn tại trong hệ thống");
        }

        public async Task<User> GetUserAsync(int id)
        {
            return await _unitOfWork.UserRepository.GetUserAsync(id) ?? throw new("User not exsist");
        }

        public async Task<User> LoginAsync(User user, string password)
        {
            var currentUser = await _unitOfWork.UserRepository.GetUserAsync(user.Username);
            currentUser ??= await _unitOfWork.UserRepository.GetUserAsync(user.Email);
            if (currentUser == null) {
                throw new ArgumentNullException("Invalid username");
            }
            var result = _passwordHasher.VerifyHashedPassword(currentUser, currentUser.PasswordHash, password);
            if (result == PasswordVerificationResult.Success)
            {
                return currentUser;
            }
            throw new ArgumentNullException("Invalid username or password");
        }

        public async Task<User> SignUpAsync(User user, string password)
        {
            var hashedPassword = _passwordHasher.HashPassword(user, password);
            user.PasswordHash = hashedPassword;
            return await _unitOfWork.UserRepository.AddAccountAsync(user);
        }

        public async Task<ValidationResult> Validate(string username, string password, string confirmPassword, string email, bool passwordValid)
        {
            if(password != confirmPassword)
            {
                return ValidationResult.PasswordNotMatch;
            }
            if (!Regex.IsMatch(username, @"^(?=.*[A-Za-z])(?=.*\d)[A-Za-z0-9]+$"))
            {
                return ValidationResult.UsernameNotValid;
            }
            var user = await _unitOfWork.UserRepository.GetUserAsync(username);
            if (user != null)
            {
                return ValidationResult.UserExists;
            }
            if(_unitOfWork.UserRepository.EmailExists(email))
            {
                return ValidationResult.EmailExists;
            }
            if (!passwordValid)
            {
                return ValidationResult.PasswordTooShort;
            }
            return ValidationResult.Success;
        }
    }
}
