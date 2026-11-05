using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Models
{
    public class ChangePassModel
    {
        public string Username { get; set; } = string.Empty;
        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        [MinLength(5, ErrorMessage = "Mật khẩu phải có ít nhất 5 ký tự")]
        public string NewPassword { get; set; } = string.Empty;
        [Required(ErrorMessage = "Xác nhận mật khẩu không được để trống")]
        [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp với mật khẩu mới")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
