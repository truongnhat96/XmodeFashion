# Phí vận chuyển

Giao hàng tiêu chuẩn dùng một mức phí cố định cho mỗi đơn trên toàn quốc, không phụ thuộc tỉnh/thành, số lượng hay tổng tiền hàng. Chưa kết nối API đơn vị vận chuyển.

- Mặc định: **30.000đ/đơn** khi chưa có cấu hình.
- Admin mở **Thiết lập → Phí vận chuyển** để thay đổi mức phí (VND nguyên, từ 0 đến 1.000.000).
- Nhập **0** để miễn phí giao hàng; giỏ hàng và đơn hiển thị “Miễn phí”.
- Nếu shop đã lưu một mức phí, hệ thống giữ mức phí đó.
- Tổng tiền thu COD/VNPay/MoMo = tổng tiền hàng + phí giao hàng.

Giỏ hàng, checkout, API thiết lập và dữ liệu tư vấn chatbot cùng dùng mức phí hiệu lực. Khi tạo đơn, server đọc lại cấu hình trong transaction, so sánh phí và giá với báo giá khách vừa xem. Nếu có thay đổi thì yêu cầu tải lại trang để xác nhận tổng tiền mới. Đơn lưu phí tại thời điểm đặt, nên Admin đổi mức phí không làm thay đổi đơn đã tạo.

Tận dụng trường StoreSettings.ShippingFee hiện có: không thêm schema, migration, dữ liệu mẫu hoặc luồng khởi tạo tự động. Cấu hình được lưu khi Admin nhấn Lưu thiết lập. Khởi động lại ứng dụng sau khi cập nhật mã.

Bản thay đổi này không chạy build hay kiểm thử, theo yêu cầu trước đó của người dùng.
