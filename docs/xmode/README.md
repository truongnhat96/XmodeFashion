# Giao diện Xmode

Chatbot mua sắm Groq và dữ liệu nghiệm thu: [hướng dẫn cấu hình, kiểm thử và SQL](shopping-chat.md).

Trang chủ và danh mục hiển thị dữ liệu SQL Server, không đọc fixture hoặc `localStorage` mock. Khi chưa có sản phẩm, giao diện hiển thị “Chưa có sản phẩm nào”. Giá VND dùng decimal; size, màu và tồn kho gắn với biến thể.

| Đường dẫn | Chức năng |
| --- | --- |
| `/`, `/xmode/catalog`, `/xmode/product/{id}` | Trang chủ, tìm kiếm/lọc/phân trang, chi tiết sản phẩm |
| `/xmode/cart`, `/xmode/checkout`, `/xmode/orders` | Giỏ hàng, đặt COD và đơn hàng của tài khoản hoặc phiên khách |
| `/xmode/admin/products`, `/xmode/admin/categories` | Quản trị sản phẩm, biến thể, ảnh và danh mục |
| `/xmode/admin/orders` | Xem, xác nhận, giao, hoàn tất, hủy và lưu trữ đơn |
| `/xmode/admin/content`, `/xmode/admin/size-charts`, `/xmode/admin/settings` | Quản trị trang chủ, ảnh, bảng size và phí giao hàng |
| `/Posts`, `/BlogList` | Tạp chí và quản lý bài viết |

Admin phải cấu hình phí vận chuyển trước khi khách đặt đơn. Thêm hàng vào giỏ không trừ tồn; tạo đơn trừ tồn trong transaction. Hủy đơn hoàn tồn một lần; Admin chỉ hủy đơn đang giao sau khi xác nhận đã nhận lại hàng. Thanh toán trực tuyến chưa được triển khai; chatbot tư vấn dùng catalog công khai và không tự đặt đơn.

Kiểm thử tích hợp dùng ứng dụng thật và database SQL Server riêng tại `tools/fashion-e2e`, điều khiển Edge/Playwright bằng `tools/xmode-ui/fashion-e2e.cjs`.
