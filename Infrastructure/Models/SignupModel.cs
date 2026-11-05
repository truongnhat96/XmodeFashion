using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Models
{
    public class SignupModel
    {
        [Required(ErrorMessage = "Tên đăng nhập không được bỏ trống")]
        [MaxLength(50, ErrorMessage = "Tên đăng nhập không được vượt quá 50 ký tự")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Địa chỉ email không được bỏ trống")]
        [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên hiển thị không được bỏ trống")]
        [MaxLength(100, ErrorMessage = "Tên hiển thị không được vượt quá 100 ký tự")]
        public string DisplayName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu không được bỏ trống")]
        [MinLength(5, ErrorMessage = "Mật khẩu phải có ít nhất 5 ký tự")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Chưa xác nhận mật khẩu")]
        [Compare("Password", ErrorMessage = "Dữ liệu mật khẩu không khớp")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
