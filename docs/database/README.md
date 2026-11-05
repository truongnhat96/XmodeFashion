# Database thời trang Xmode

Ứng dụng dùng `FashionContext`, EF Core 9 và SQL Server. Connection string là `ConnectionStrings:FashionConnection`; cấu hình mặc định trỏ đến `.\SQLEXPRESS`, database `XmodeFashion`. Biến môi trường `ConnectionStrings__FashionConnection` ghi đè giá trị trong appsettings. Không trỏ connection string thời trang vào database cũ.

Ứng dụng web không tự migration và không seed dữ liệu. Công cụ khởi tạo kiểm tra tên database và lịch sử migration, từ chối schema không tương thích và không ghi đè tài khoản đã có. Từ thư mục gốc:

```powershell
dotnet restore
dotnet build OnlineShop.sln
dotnet restore tools/fashion-init/FashionInit.csproj
dotnet run --project tools/fashion-init/FashionInit.csproj -- --schema-only
```

Lệnh cuối tạo `XmodeFashion` nếu chưa có và áp dụng schema thời trang. Danh mục, sản phẩm và nội dung bắt đầu trống. Nếu database đã có schema khác, công cụ dừng. Tạo Admin bằng lệnh sau; công cụ yêu cầu nhập mật khẩu ẩn hai lần, tối thiểu 10 ký tự:

```powershell
$env:XMODE_ADMIN_USER = 'Admin123'
$env:XMODE_ADMIN_EMAIL = 'test123@truongshop.id.vn'
dotnet run --project tools/fashion-init/FashionInit.csproj --no-restore
```

Không cần xác nhận email để đăng nhập. Email được lưu như thông tin tài khoản. Không ghi mật khẩu vào mã nguồn hoặc file cấu hình chia sẻ. Sau khi có Admin, chạy `dotnet run --project Infrastructure/Infrastructure.csproj` và đăng nhập tại `/Login`; tạo danh mục, sản phẩm và thiết lập phí vận chuyển trong Admin.

## Cloudinary

Tại thư mục gốc, file `.env` được bỏ qua bởi Git. Ứng dụng đọc riêng ba biến sau từ `.env`; biến môi trường tương ứng có ưu tiên cao hơn:

```dotenv
CloudinarySettings__CloudName=<cloud-name>
CloudinarySettings__ApiKey=<api-key>
CloudinarySettings__ApiSecret=<api-secret>
```

Trong môi trường triển khai, nên cung cấp ba biến qua cơ chế quản lý bí mật của môi trường. Không đưa giá trị thật vào repository. Nếu thiếu cấu hình, cửa hàng vẫn xem được dữ liệu hiện có nhưng upload ảnh sẽ báo lỗi.

## Kiểm thử

```powershell
dotnet test UseCase.Tests/UseCase.Tests.csproj
dotnet run --project tools/fashion-db-checks/FashionDbChecks.csproj
dotnet build tools/fashion-init/FashionInit.csproj
dotnet run --project tools/fashion-e2e/FashionE2E.csproj
```

Hai bộ kiểm thử tích hợp tạo database tên ngẫu nhiên trên `.\SQLEXPRESS` và chỉ xóa database do chính bộ kiểm thử tạo. Bộ trình duyệt cần Node.js, Playwright và Microsoft Edge. Để kiểm tra Cloudinary thật, đặt `$env:XMODE_TEST_CLOUDINARY='1'` trước lệnh E2E; bộ kiểm thử upload rồi xóa ảnh thử của nó.
