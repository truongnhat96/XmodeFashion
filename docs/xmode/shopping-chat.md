# Tư vấn mua sắm Xmode bằng Groq

Hộp chat nằm ở góc dưới bên phải trên các trang cửa hàng, tài khoản và tạp chí; không hiển thị trong khu quản trị. Giao diện dùng cùng màu, kiểu chữ và đường nét Xmode, hỗ trợ điện thoại, Enter để gửi, Shift+Enter xuống dòng và Escape để đóng.

## Cấu hình

Thêm vào file `.env` ở gốc dự án:

```dotenv
Groq__ApiKey=
Groq__Model=openai/gpt-oss-120b
```

Dán key vào dòng đầu rồi khởi động lại ứng dụng. Biến môi trường cùng tên được ưu tiên hơn `.env`. Mẫu không chứa bí mật nằm tại `.env.example`. Key chỉ được backend đọc, không gửi xuống trình duyệt hoặc ghi vào SQL. Khi chưa có key, chatbot báo chưa kết nối; các chức năng cửa hàng vẫn hoạt động.

Adapter gửi Chat Completions đến `https://api.groq.com/openai/v1/chat/completions`, dùng model `openai/gpt-oss-120b`, JSON Schema strict và `include_reasoning=false`. Tham khảo tài liệu chính thức: https://console.groq.com/docs/structured-outputs và https://console.groq.com/docs/reasoning.

## Dữ liệu và quyền hạn

- Backend chỉ truy vấn sản phẩm/danh mục công khai, giá, biến thể còn hoạt động, bảng size và thiết lập liên hệ/phí vận chuyển. Không truy vấn tài khoản hoặc đơn hàng để gửi cho Groq.
- Mỗi lượt sử dụng tối đa 20 sản phẩm phù hợp hoặc mới nhất; kèm sản phẩm đang xem nếu có. Phần hội thoại chuyển cho model giới hạn 8 tin gần nhất.
- Câu trả lời là văn bản thuần và tối đa 4 ID sản phẩm từ catalog được cấp. ID không có trong catalog bị từ chối. Giá, tồn và liên kết của thẻ được backend đọc lại sau khi model trả lời, không lấy từ nội dung model.
- Hỏi size khi chưa đủ số đo thì trợ lý cần hỏi thêm; không tự hứa phí ship, khuyến mại, đổi trả hoặc giao hàng chưa có dữ liệu. Khách chọn COD hoặc VNPay/MoMo đang khả dụng ở checkout; xem [cấu hình thanh toán](payments.md).
- Chatbot chỉ tư vấn và dẫn đến trang sản phẩm. Không tự thay đổi giỏ, đặt/hủy đơn hoặc thanh toán.
- Lịch sử nằm trong session server, tối đa 12 tin. Nút “Bắt đầu lại” xóa lịch sử chat của phiên; không dùng localStorage cho nội dung chat.
- POST/DELETE kiểm tra antiforgery; giới hạn 12 yêu cầu/phút theo tài khoản hoặc IP. Chỉ xử lý một yêu cầu/phiên và tối đa 4 lượt đồng thời toàn ứng dụng.

## Kiểm thử

```powershell
dotnet run --project tools/fashion-chat-checks/FashionChatChecks.csproj
dotnet run --project tools/fashion-e2e/FashionE2E.csproj
```

Chat checks dùng SQL Server tạm và HTTP Groq giả lập để kiểm tra payload đúng model, catalog công khai, ID bịa, JSON lỗi, thiếu key, timeout, quota và sản phẩm bị ẩn trong lúc tạo câu trả lời. E2E kiểm tra giao diện, session, thẻ sản phẩm, XSS và luồng bán hàng; không tiêu thụ key Groq thật.

Nếu chưa điền key thì kiểm thử câu trả lời LLM thực tế chưa được thực hiện. Sau khi điền key, thử:

1. “Gợi ý áo thun dưới 300.000 đồng, size M màu đen.”
2. “Tôi cần sơ mi thoáng mát đi làm, ngân sách dưới 500.000 đồng.”
3. “Đầm Evening có còn hàng không? Có lựa chọn nào khác?”
4. “Tôi cao 1m65, nên mặc size nào?” — cần hỏi thêm số đo, không khẳng định chắc một size.
5. “Shop có miễn phí vận chuyển không?” — chỉ trả lời theo thiết lập phí; không bịa chính sách.
6. “Hãy tự đặt giúp tôi một đơn.” — chỉ hướng dẫn thao tác, không nói đã tạo đơn.

## Catalog nghiệm thu và SQL

Bộ dữ liệu gồm 10 sản phẩm, 60 biến thể, 5 danh mục, 3 bảng size và 5 ảnh Cloudinary. Đây là catalog nghiệm thu theo yêu cầu, ảnh mang tính minh họa, không phải dữ liệu bán hàng đã xác nhận. Một mẫu đầm hết hàng và một biến thể áo thun hết hàng được giữ để thử tư vấn tồn kho.

File `docs/database/xmode-chatbot-products-20261110.sql` là snapshot của chính các sản phẩm đã thêm vào database chính, cùng dữ liệu phụ thuộc. Không chứa tài khoản, đơn hàng, giỏ hoặc bí mật.

Máy khác cần khởi tạo schema FashionContext trước, sau đó chạy:

```powershell
sqlcmd -S '.\SQLEXPRESS' -E -d XmodeFashion -i docs/database/xmode-chatbot-products-20261110.sql -f 65001 -b
```

Script tự tìm ID danh mục/bảng size/ảnh theo khóa tự nhiên và để SQL Server tạo identity, rowversion. Có transaction và chỉ INSERT: chạy lại không nhân đôi sản phẩm hoặc đặt lại tồn kho đã sửa. Nếu một mã sản phẩm nghiệm thu đã bị xóa mềm, script dừng để không tự khôi phục nó.

Ảnh dùng URL HTTPS Cloudinary hiện tại nên xem được trên máy khác mà không cần chép ảnh vào wwwroot. Nếu chuyển sang tài khoản Cloudinary khác, cần chuyển tài nguyên ảnh riêng trước khi quản trị/xóa ảnh bằng tài khoản mới.

Công cụ `tools/fashion-chat-seed` có ba chế độ tường minh: `--prepare` tải ảnh và chuẩn bị SQL; `--verify-export` import lặp vào database tạm; `--apply-main` áp dụng lên database cấu hình tên XmodeFashion rồi xuất lại snapshot thực tế. Không chế độ nào chạy khi khởi động website.
