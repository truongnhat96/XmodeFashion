using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace UseCase.MailService
{
    public static class HtmlHelper
    {
        public static string GenerateHTMLContent(List<string> items, bool isPaid)
        {
            var sb = new StringBuilder();

            // Phần đầu của HTML
            sb.Append(@"<!DOCTYPE html>
<html lang=""vi"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
  <title>" + (isPaid ? "Thông Báo Thanh Toán Thành Công" : "Xác Nhận Đơn Hàng Thành Công") + @"</title>
  <style>
    body {
      margin: 0;
      font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
      background: #f4f7f9;
      color: #333;
      padding: 20px;
    }
    .email-container {
      background: #fff;
      max-width: 600px;
      margin: auto;
      padding: 30px;
      border-radius: 8px;
      box-shadow: 0 0 20px rgba(0, 0, 0, 0.1);
    }
    .header {
      text-align: center;
      margin-bottom: 30px;
    }
    .header h1 {
      color: #4CAF50;
    }
    .content p {
      font-size: 1.1em;
      line-height: 1.6;
      margin: 20px 0;
    }
    .info-box {
      border-top: 1px solid #ddd;
      padding-top: 20px;
      margin-top: 30px;
    }
    .info-box h2 {
      font-size: 1.3em;
      margin-bottom: 15px;
      color: #333;
    }
    .contact-item {
      margin-bottom: 10px;
      display: flex;
      align-items: center;
    }
    .contact-item span {
      margin-left: 10px;
      font-size: 1em;
    }
    .contact-icon {
      width: 24px;
      height: 24px;
    }
    .btn {
      display: inline-block;
      margin-top: 20px;
      padding: 12px 25px;
      background: #4CAF50;
      color: #fff;
      text-decoration: none;
      border-radius: 5px;
      transition: background 0.3s ease;
    }
    .btn:hover {
      background: #43a047;
    }
  </style>
  <link rel=""stylesheet"" href=""https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css"">
</head>
<body>
  <div class=""email-container"">
    <div class=""header"">
      <h1>TruongShop Xin Chào Quý Khách!" + @"</h1>
    </div>
    <div class=""content"">
      <p>" + (isPaid
                ? "Chúng tôi xin thông báo rằng giao dịch thanh toán của bạn đã được xử lý thành công. Cảm ơn bạn đã tin tưởng và sử dụng dịch vụ của chúng tôi."
                : "Chúng tôi xin thông báo rằng giao dịch đơn hàng của bạn đã được xử lý thành công. Vui lòng gửi ảnh thông tin đã chuyển khoản thành công để nhận sản phẩm.") + @"</p>
      <p>Chúng tôi sẽ gửi thông tin sản phẩm đến cho bạn trong vòng 1 tiếng tính từ lúc đặt hàng thông qua số điện thoại đã đăng ký (Zalo), trong khung giờ 7h - 23h.</p>
      <p>Nếu quá thời gian trên mà bạn vẫn chưa nhận được thông tin sản phẩm, vui lòng liên hệ với chúng tôi qua số điện thoại hoặc Facebook để được hỗ trợ kịp thời.</p>
    </div>
    
    <!-- Phần hiển thị danh sách -->
    <div class=""list-content"">
      <h2>Sản phẩm đã mua:</h2>
      <ul>");

            // Lặp qua danh sách và thêm các mục vào HTML
            foreach (var item in items)
            {
                sb.Append("<li>" + item + "</li>");
            }

            sb.Append(@"</ul>
    </div>
    
    <div class=""info-box"">
      <h2>Thông Tin Liên Hệ Người Bán</h2>
      <div class=""contact-item"">
        <i class=""fa fa-phone"" aria-hidden=""true""></i>
        <span>Số điện thoại: 0358 223 929</span>
      </div>
      <div class=""contact-item"">
        <i class=""fa fa-facebook-square"" aria-hidden=""true""></i>
        <span>Facebook: <a href=""https://www.facebook.com/truong.luong.386820"" target=""_blank"">facebook.com</a></span>
      </div>
      <div class=""contact-item"">
        <i class=""fa fa-comments"" aria-hidden=""true""></i>
        <span>Zalo: 0358 223 929</span>
      <div class=""contact-item"">
        <i class=""fa fa-comments"" aria-hidden=""true""></i>
        <span>Email: <a href=""mailto:hotro@truongshop.id.vn"">hotro@truongshop.id.vn</a></span>
      </div>
      </div>
    </div>
  </div>
</body>
</html>");

            return sb.ToString();
        }

        public static string GenerateHTMLContent(List<string> products, string name, string phone, string email, string? note)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"vi\">");
            sb.AppendLine("<head>");
            sb.AppendLine("  <meta charset=\"UTF-8\" />");
            sb.AppendLine("  <title>Thông báo đơn hàng mới</title>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body style=\"margin:0;padding:0;background-color:#f2f4f6;\">");
            sb.AppendLine("  <table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" role=\"presentation\">");
            sb.AppendLine("    <tr><td align=\"center\">");
            sb.AppendLine("      <table width=\"600\" cellpadding=\"0\" cellspacing=\"0\" role=\"presentation\" style=\"margin:40px 0;background:#fff;border-radius:8px;overflow:hidden;\">");

            // Header
            sb.AppendLine("        <tr><td style=\"background:#004aac;padding:20px;text-align:center;\">");
            sb.AppendLine("          <h1 style=\"color:#fff;font-family:Arial,sans-serif;font-size:24px;margin:0;\">TruongShop</h1>");
            sb.AppendLine("        </td></tr>");

            // Title
            sb.AppendLine("        <tr><td style=\"padding:30px 40px 0;\">");
            sb.AppendLine("          <h2 style=\"font-family:Arial,sans-serif;font-size:20px;color:#333;margin:0;\">📣 Đơn hàng mới vừa được đặt</h2>");
            sb.AppendLine("        </td></tr>");

            // Body
            sb.AppendLine("        <tr><td style=\"padding:20px 40px;\">");
            sb.AppendLine("          <p style=\"font-family:Arial,sans-serif;font-size:16px;color:#555;line-height:1.5;\">Chào bạn,</p>");
            sb.AppendLine("          <p style=\"font-family:Arial,sans-serif;font-size:16px;color:#555;line-height:1.5;\">Khách hàng vừa đặt hàng với thông tin:</p>");

            // Customer info
            sb.AppendLine("          <table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" role=\"presentation\" style=\"border-collapse:collapse;margin:20px 0;\">");
            sb.AppendLine($"            <tr><td style=\"font-family:Arial,sans-serif;font-size:15px;color:#333;width:120px;\"><strong>Họ & tên:</strong></td><td style=\"font-family:Arial,sans-serif;font-size:15px;color:#555;\">{name}</td></tr>");
            sb.AppendLine($"            <tr><td style=\"font-family:Arial,sans-serif;font-size:15px;color:#333;\"><strong>Điện thoại:</strong></td><td style=\"font-family:Arial,sans-serif;font-size:15px;color:#555;\">{phone}</td></tr>");
            sb.AppendLine($"            <tr><td style=\"font-family:Arial,sans-serif;font-size:15px;color:#333;\"><strong>Email:</strong></td><td style=\"font-family:Arial,sans-serif;font-size:15px;color:#555;\">{email}</td></tr>");
            sb.AppendLine($"            <tr><td style=\"font-family:Arial,sans-serif;font-size:15px;color:#333;vertical-align:top;\"><strong>Ghi chú:</strong></td><td style=\"font-family:Arial,sans-serif;font-size:15px;color:#555;\">{(string.IsNullOrWhiteSpace(note) ? "– Không có –" : note)}</td></tr>");
            sb.AppendLine("          </table>");

            // Order details
            sb.AppendLine("          <table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" role=\"presentation\" style=\"border-collapse:collapse;\">");
            sb.AppendLine("            <tr><td style=\"font-family:Arial,sans-serif;font-size:16px;color:#333;padding-bottom:8px;\"><strong>🛒 Chi tiết đơn hàng</strong></td></tr>");
            foreach (var prod in products)
            {
                sb.AppendLine("            <tr>");
                sb.AppendLine($"              <td style=\"font-family:Arial,sans-serif;font-size:15px;color:#555;padding:6px 0;\">{WebUtility.HtmlEncode(prod)}</td>");
                sb.AppendLine("            </tr>");
            }
            sb.AppendLine("          </table>");

            // Footer
            sb.AppendLine("        <tr><td style=\"background:#f2f4f6;padding:20px 40px;text-align:center;\">");
            sb.AppendLine("          <p style=\"font-family:Arial,sans-serif;font-size:12px;color:#888;line-height:1.4;margin:0;\">");
            sb.AppendLine("            © 2025 TRUONGSHOP. Địa chỉ: 11DV06 Khu đô thị Mỗ Lao, Hà Đông, Hà Nội.<br/>");
            sb.AppendLine("            Hotline: 0358223929 | Email: luongnhattruong2004@gmail.com");
            sb.AppendLine("          </p>");
            sb.AppendLine("        </td></tr>");

            sb.AppendLine("      </table>");
            sb.AppendLine("    </td></tr>");
            sb.AppendLine("  </table>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        public static string GeneratePasswordResetEmail(string url)
        {
            return $@"<!DOCTYPE html>
<html lang=""vi"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Yêu cầu đặt lại mật khẩu</title>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background-color: #f2f4f6;
            margin: 0;
            padding: 0;
        }}
        .wrapper {{
            width: 100%;
            background-color: #f2f4f6;
            padding: 40px 0;
        }}
        .container {{
            background-color: #ffffff;
            max-width: 500px;
            margin: 0 auto;
            padding: 40px 30px;
            border-radius: 8px;
            box-shadow: 0 4px 15px rgba(0, 0, 0, 0.05);
            text-align: center;
        }}
        .header h1 {{
            color: #333333;
            font-size: 24px;
            margin-bottom: 10px;
        }}
        .content p {{
            color: #555555;
            font-size: 16px;
            line-height: 1.6;
            margin-bottom: 25px;
        }}
        .btn-reset {{
            display: inline-block;
            background-color: #007bff;
            color: #ffffff !important;
            text-decoration: none;
            padding: 14px 30px;
            font-size: 16px;
            font-weight: bold;
            border-radius: 5px;
            margin-bottom: 25px;
            transition: background-color 0.3s ease;
        }}
        .btn-reset:hover {{
            background-color: #0056b3;
        }}
        .fallback-link {{
            font-size: 13px;
            color: #777777;
            word-wrap: break-word;
            margin-bottom: 20px;
        }}
        .fallback-link a {{
            color: #007bff;
            text-decoration: none;
        }}
        .footer {{
            border-top: 1px solid #eeeeee;
            padding-top: 20px;
            font-size: 12px;
            color: #999999;
            line-height: 1.5;
        }}
    </style>
</head>
<body>
    <div class=""wrapper"">
        <div class=""container"">
            <div class=""header"">
                <h1>Đặt Lại Mật Khẩu</h1>
            </div>
            <div class=""content"">
                <p>Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn. Vui lòng nhấn vào nút bên dưới để thiết lập mật khẩu mới. Nếu bạn không yêu cầu điều này, bạn có thể bỏ qua email này một cách an toàn.</p>
                <a href=""{url}"" class=""btn-reset"">Đặt Lại Mật Khẩu</a>
                <div class=""fallback-link"">
                    Vui lòng không chia sẻ liên kết này với bất kỳ ai để tránh việc bị mất quyền truy cập vào tài khoản của bạn.
                </div>
            </div>
            <div class=""footer"">
                &copy; 2026 TruongShop. All rights reserved.<br>
                Nếu bạn cần hỗ trợ, vui lòng liên hệ với nhóm hỗ trợ của chúng tôi qua email: <a href=""mailto:hotro@truongshop.id.vn"">hotro@truongshop.id.vn</a>.
            </div>
        </div>
    </div>
</body>
</html>";
        }
    }
}
