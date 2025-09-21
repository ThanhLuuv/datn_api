# BookStore API

Hệ thống quản lý cửa hàng sách với các chức năng quản lý danh mục, sản phẩm, đơn đặt hàng và nhập hàng.

## 🚀 Tính năng chính

### 1. **Quản lý Danh mục (Category Management)**
- Tạo, sửa, xóa danh mục sách
- Tìm kiếm và phân trang danh mục
- Kiểm tra danh mục có sách trước khi xóa

### 2. **Quản lý Sản phẩm (Book Management)**
- Quản lý thông tin sách (ISBN, tên, giá, năm xuất bản, v.v.)
- Quản lý tác giả (Author Management)
- Liên kết nhiều tác giả với một cuốn sách
- Tìm kiếm và lọc sách theo nhiều tiêu chí
- Upload ảnh bìa sách

### 3. **Quản lý Phiếu đặt hàng (Purchase Order Management)**
- Tạo đơn đặt hàng mua từ nhà xuất bản
- Quản lý chi tiết đơn đặt hàng
- Theo dõi trạng thái đơn hàng
- Tính toán tổng tiền tự động

### 4. **Quản lý Nhập hàng (Goods Receipt Management)**
- Tạo phiếu nhập hàng từ đơn đặt mua
- Quản lý chi tiết nhập hàng
- Liên kết với đơn đặt mua
- Theo dõi số lượng và giá nhập

### 5. **Hệ thống Authentication & Authorization**
- Đăng ký và đăng nhập người dùng
- JWT Token authentication
- Phân quyền theo vai trò (Customer, Employee, Admin)

## 🏗️ Kiến trúc hệ thống

### **Backend Architecture**
- **Framework**: ASP.NET Core 8.0 Web API
- **Database**: MySQL với Entity Framework Core
- **Authentication**: JWT Bearer Token
- **Architecture Pattern**: Clean Architecture với Repository Pattern

### **Database Schema**
Hệ thống sử dụng database MySQL với các bảng chính:

#### **Core Entities**
- `account` - Tài khoản đăng nhập
- `role` - Vai trò người dùng
- `permission` - Quyền chi tiết
- `role_permission` - Bảng nối role-permission

#### **User Management**
- `customer` - Thông tin khách hàng
- `employee` - Thông tin nhân viên
- `department` - Phòng ban

#### **Product Management**
- `category` - Danh mục sách
- `book` - Thông tin sách
- `author` - Thông tin tác giả
- `author_book` - Bảng nối tác giả-sách
- `publisher` - Nhà xuất bản

#### **Order Management**
- `order` - Đơn hàng bán
- `order_line` - Chi tiết đơn hàng bán
- `invoice` - Hóa đơn

#### **Purchase Management**
- `purchase_order` - Đơn đặt hàng mua
- `purchase_order_line` - Chi tiết đơn đặt mua
- `goods_receipt` - Phiếu nhập hàng
- `goods_receipt_line` - Chi tiết phiếu nhập

## 📁 Cấu trúc Project

```
BookStore.Api/
├── Controllers/           # API Controllers
│   ├── AuthController.cs
│   ├── CategoryController.cs
│   ├── BookController.cs
│   ├── PurchaseOrderController.cs
│   └── GoodsReceiptController.cs
├── Services/             # Business Logic Services
│   ├── IAuthService.cs
│   ├── AuthService.cs
│   ├── ICategoryService.cs
│   ├── CategoryService.cs
│   ├── IBookService.cs
│   ├── BookService.cs
│   ├── IPurchaseOrderService.cs
│   ├── PurchaseOrderService.cs
│   ├── IGoodsReceiptService.cs
│   └── GoodsReceiptService.cs
├── Models/               # Entity Models
│   ├── Account.cs
│   ├── Role.cs
│   ├── Category.cs
│   ├── Book.cs
│   ├── Author.cs
│   ├── Publisher.cs
│   ├── PurchaseOrder.cs
│   ├── GoodsReceipt.cs
│   └── ...
├── DTOs/                 # Data Transfer Objects
│   ├── AuthDTOs.cs
│   ├── CategoryDTOs.cs
│   ├── BookDTOs.cs
│   ├── PurchaseOrderDTOs.cs
│   └── GoodsReceiptDTOs.cs
├── Data/                 # Database Context
│   └── BookStoreDbContext.cs
├── test-api.http         # HTTP Test File
├── test-api.ps1          # PowerShell Test Script
└── README.md
```

## 🛠️ Cài đặt và Chạy

### **Yêu cầu hệ thống**
- .NET 8.0 SDK
- MySQL Server 8.0+
- Visual Studio 2022 hoặc VS Code

### **Cài đặt**

1. **Clone repository**
```bash
git clone <repository-url>
cd BookStore.Api
```

2. **Cài đặt dependencies**
```bash
dotnet restore
```

3. **Cấu hình database**
- Cập nhật connection string trong `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=bookstore;Uid=root;Pwd=your_password;"
  }
}
```

4. **Chạy migrations**
```bash
dotnet ef database update
```

5. **Chạy ứng dụng**
```bash
dotnet run
```

Ứng dụng sẽ chạy tại: `https://localhost:7000`

## 🧪 Testing

### **1. HTTP Test File**
Sử dụng file `test-api.http` để test các API endpoints:

```http
### Test Authentication
POST https://localhost:7000/api/auth/register
Content-Type: application/json

{
  "email": "admin@bookstore.com",
  "password": "Admin123!",
  "confirmPassword": "Admin123!",
  "roleId": 3
}
```

### **2. PowerShell Test Script**
Chạy script PowerShell để test tự động:

```powershell
.\test-api.ps1
```

### **3. Manual Testing**
Sử dụng Swagger UI tại: `https://localhost:7000/swagger`

## 📚 API Documentation

### **Authentication APIs**
- `POST /api/auth/register` - Đăng ký tài khoản
- `POST /api/auth/login` - Đăng nhập

### **Category APIs**
- `GET /api/category` - Lấy danh sách danh mục
- `GET /api/category/{id}` - Lấy chi tiết danh mục
- `POST /api/category` - Tạo danh mục mới
- `PUT /api/category/{id}` - Cập nhật danh mục
- `DELETE /api/category/{id}` - Xóa danh mục

### **Book APIs**
- `GET /api/book` - Lấy danh sách sách (có tìm kiếm, lọc)
- `GET /api/book/{isbn}` - Lấy chi tiết sách
- `POST /api/book` - Tạo sách mới
- `PUT /api/book/{isbn}` - Cập nhật sách
- `DELETE /api/book/{isbn}` - Xóa sách
- `GET /api/book/authors` - Lấy danh sách tác giả
- `POST /api/book/authors` - Tạo tác giả mới

### **Purchase Order APIs**
- `GET /api/purchaseorder` - Lấy danh sách đơn đặt mua
- `GET /api/purchaseorder/{id}` - Lấy chi tiết đơn đặt mua
- `POST /api/purchaseorder` - Tạo đơn đặt mua mới
- `PUT /api/purchaseorder/{id}` - Cập nhật đơn đặt mua
- `DELETE /api/purchaseorder/{id}` - Xóa đơn đặt mua

### **Goods Receipt APIs**
- `GET /api/goodsreceipt` - Lấy danh sách phiếu nhập
- `GET /api/goodsreceipt/{id}` - Lấy chi tiết phiếu nhập
- `POST /api/goodsreceipt` - Tạo phiếu nhập mới
- `PUT /api/goodsreceipt/{id}` - Cập nhật phiếu nhập
- `DELETE /api/goodsreceipt/{id}` - Xóa phiếu nhập
- `GET /api/goodsreceipt/available-purchase-orders` - Lấy đơn đặt mua có thể tạo phiếu nhập

## 🔐 Authentication

Hệ thống sử dụng JWT Bearer Token authentication:

1. **Đăng ký/Đăng nhập** để lấy token
2. **Thêm token vào header** của mỗi request:
```
Authorization: Bearer <your-jwt-token>
```

## 📊 Business Logic

### **Category Management**
- Tên danh mục phải duy nhất
- Không thể xóa danh mục đang có sách
- Validation: tên bắt buộc, độ dài tối đa 150 ký tự

### **Book Management**
- ISBN phải duy nhất
- Validation: giá >= 0, số trang > 0
- Hỗ trợ nhiều tác giả cho một cuốn sách
- Upload ảnh bìa sách

### **Purchase Order Management**
- Chỉ nhân viên mới được tạo đơn đặt mua
- Tính tổng tiền tự động
- Không thể chỉnh sửa đơn đã có phiếu nhập

### **Goods Receipt Management**
- Chỉ có thể tạo phiếu nhập cho đơn đặt mua đã tồn tại
- Một đơn đặt mua chỉ có một phiếu nhập
- Số lượng dòng trong phiếu nhập phải khớp với đơn đặt mua

## 🚀 Deployment

### **Docker (Tùy chọn)**
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
COPY . /app
WORKDIR /app
EXPOSE 80
ENTRYPOINT ["dotnet", "BookStore.Api.dll"]
```

### **Production Settings**
- Cập nhật `appsettings.Production.json`
- Cấu hình HTTPS
- Thiết lập logging
- Cấu hình CORS cho production

## 🤝 Contributing

1. Fork repository
2. Tạo feature branch
3. Commit changes
4. Push to branch
5. Tạo Pull Request

## 📝 License

Dự án này được phát hành dưới MIT License.

## 📞 Support

Nếu có vấn đề hoặc câu hỏi, vui lòng tạo issue trong repository.

---

**Lưu ý**: Đây là phiên bản API backend. Để có hệ thống hoàn chỉnh, cần phát triển thêm frontend và các chức năng khác như quản lý đơn hàng bán, thanh toán, v.v.