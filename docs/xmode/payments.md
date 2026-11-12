# Thanh toán COD, VNPay và MoMo

Checkout `/xmode/checkout` dùng cùng đơn hàng thời trang và tồn kho SQL Server. COD là lựa chọn mặc định. VNPay/MoMo tự xuất hiện và có thể chọn khi đủ thông tin merchant; không cần cờ Enabled. Cổng chưa có khóa được ẩn. Không tích hợp PayOS.

## Cấu hình sandbox

Tự điền thông tin merchant trong `.env` ở thư mục gốc (đã được Git bỏ qua). Không đưa khóa vào JavaScript, commit hoặc chat. `.env.example` có toàn bộ tên biến:

```dotenv
Payments__PublicBaseUrl=https://your-public-https-origin.example
Vnpay__TmnCode=
Vnpay__HashSecret=
Vnpay__BaseUrl=https://sandbox.vnpayment.vn/paymentv2/vpcpay.html
MomoAPI__PartnerCode=
MomoAPI__AccessKey=
MomoAPI__SecretKey=
MomoAPI__Endpoint=https://test-payment.momo.vn/v2/gateway/api/create
```

`PublicBaseUrl` là tùy chọn: để trống sẽ dùng origin HTTPS của website đang mở. Khi điền, chỉ chứa origin HTTPS, không kèm đường dẫn/query. Khi chạy localhost, cần domain/tunnel HTTPS trỏ tới ứng dụng để cổng gọi IPN. Chỉ cấu hình localhost thì trình duyệt có thể quay lại nhưng máy chủ cổng không gọi được máy bạn. Với VNPay, đăng ký URL IPN trong cấu hình merchant:

| Cổng | Return URL (trình duyệt) | IPN (máy chủ cổng gọi) |
|---|---|---|
| VNPay | `/api/xmode/payments/vnpay/return` GET | `/api/xmode/payments/vnpay/ipn` GET |
| MoMo | `/api/xmode/payments/momo/return` GET | `/api/xmode/payments/momo/ipn` POST JSON |

Ghép các đường dẫn trên với `Payments__PublicBaseUrl`. Khởi động lại ứng dụng sau khi thay `.env`. Cấu hình môi trường hệ điều hành ưu tiên hơn `.env`.

MoMo dùng API v2 `captureWallet`, `autoCapture=true`; thông tin merchant giao thức cũ `captureMoMoWallet` cần được thay bằng thông tin phù hợp API hiện tại. Các khóa mẫu và callback cũ đã được bỏ khỏi `appsettings.json`. Production chỉ bật bằng merchant production và endpoint chính thức tương ứng.

## Schema và luồng đơn

Migration `AddFashionPayments` chỉ bổ sung bảng `Payments`, các trường phương thức/trạng thái/thời hạn thanh toán vào `Orders` và index. Đơn có trước migration mặc định COD/Chưa thanh toán. Không tự suy đoán số tiền đã thu của các đơn cũ.

Ứng dụng **không tự migration/seed khi khởi động**. Với database thời trang hiện hữu, áp dụng rõ ràng script idempotent trước khi chạy bản mới:

```powershell
sqlcmd -S '.\SQLEXPRESS' -E -d XmodeFashion -i docs/database/xmode-payments-upgrade.sql -f 65001 -b
```

Máy mới dùng `tools/fashion-init` để khởi tạo toàn bộ schema trước. Không chạy script nâng cấp riêng trên schema tài khoản số cũ. Không nhập dữ liệu cũ.

1. Server kiểm tra giỏ/giá/phí/tồn; tạo đơn và attempt thanh toán, giữ kho, xóa giỏ trong transaction. RequestId chống tạo trùng đơn. Online chỉ hỗ trợ tổng VND nguyên từ 5.000 đến 50.000.000; trường hợp khác vẫn chọn được COD.
2. `POST /api/xmode/orders/{id}/payment` yêu cầu đúng chủ đơn và antiforgery. Mã giao dịch được lưu từ lúc tạo đơn; gọi lặp dùng cùng mã và URL. Server lấy số tiền từ snapshot đơn, không nhận số tiền từ trình duyệt.
3. Browser return chỉ điều hướng về chi tiết đơn. Chỉ IPN đã xác thực chữ ký, merchant, mã tham chiếu và số tiền mới cập nhật thanh toán. VNPay yêu cầu cả ResponseCode và TransactionStatus là `00`; MoMo chỉ resultCode `0` là đã thu tiền.
4. IPN không cần cookie/CSRF vì được xác thực bằng HMAC; API tạo đơn/tạo thanh toán/hoàn tiền vẫn yêu cầu antiforgery. Không lưu payload có khóa hoặc thông tin thẻ.
5. Online giữ kho 15 phút. Worker kiểm tra mỗi 30 giây, hoặc kiểm tra ngay khi khách mở lại liên kết hết hạn. Không có cron migration/seed. Hết hạn/hủy/thất bại trả kho đúng một lần. Nếu app dừng, worker xử lý đơn hết hạn sau khi khởi động lại.
6. Admin không xác nhận/chuyển giao/hoàn tất đơn online chưa `Paid`. COD được ghi Paid khi Admin hoàn tất đơn.
7. Thành công đến muộn không khôi phục đơn hủy: ghi `RefundRequired`, không trừ lại kho. Hủy đơn online đã trả tiền cũng ghi `RefundRequired`. Admin phải hoàn tiền ở cổng/ngân hàng rồi chọn **Ghi nhận đã hoàn tiền**, nhập mã tham chiếu để lưu lịch sử. Nút này chỉ ghi nhận nghiệp vụ, không gọi API hoàn tiền.

Các trạng thái thanh toán: `Unpaid`, `Pending`, `Paid`, `Failed`, `Expired`, `Cancelled`, `RefundRequired`, `Refunded`. Trạng thái giao hàng được quản lý riêng. Khách xem trạng thái và nút **Kiểm tra thanh toán** trong chi tiết đơn, có thể mở lại liên kết còn hạn. Khi MoMo timeout hoặc phản hồi lỗi, đơn vẫn được lưu; không tạo mã thanh toán khác để tránh thu tiền hai lần. Đơn có thể chờ IPN hoặc tự hết hạn; nếu đã chuyển tiền cần liên hệ cửa hàng để đối soát trước khi đặt lại.

## Kiểm tra

```powershell
dotnet run --project tools/fashion-payment-checks/FashionPaymentChecks.csproj
dotnet run --project tools/fashion-e2e/FashionE2E.csproj
dotnet test UseCase.Tests/UseCase.Tests.csproj
```

Bộ payment checks dùng SQL Server riêng tên ngẫu nhiên và HTTP fixtures, gồm chữ ký MoMo/VNPay, số tiền, quyền chủ đơn, URL giả mạo, tạo lặp, callback lặp, timeout giữ hàng, hủy đồng thời, tiền về muộn và ghi nhận hoàn tiền. Playwright dùng backend thật/database riêng và merchant fixtures; chặn điều hướng mạng VNPay. Không gọi giao dịch merchant thật. Chạy sandbox end-to-end với dịch vụ thực cần merchant credentials và public IPN URL do chủ shop cấu hình.

Sidebar Admin desktop cố định cùng header, lấy chiều cao header qua ResizeObserver để không chừa khoảng trống khi cuộn/đổi viewport; menu có cuộn riêng trên màn hình thấp. Mobile dùng menu trong luồng trang như trước.

Tham chiếu chính thức: [VNPay v2.1.0](https://sandbox.vnpayment.vn/apis/docs/thanh-toan-pay/pay.html), [MoMo wallet payment](https://developers.momo.vn/v3/vi/docs/payment/api/wallet/onetime/), [MoMo IPN](https://developers.momo.vn/v3/vi/docs/payment/api/result-handling/notification/).

Giao hàng tiêu chuẩn tính một mức phí cố định mỗi đơn trên toàn quốc, mặc định 30.000đ. Admin thay đổi phí trong Thiết lập hoặc đặt 0 để miễn phí. Không cần thay đổi database. Bản điều chỉnh tự bật thanh toán và phí tùy chọn không chạy kiểm thử theo yêu cầu.
