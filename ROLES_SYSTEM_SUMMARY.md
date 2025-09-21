# 🎭 HỆ THỐNG 4 ROLES - BOOKSTORE API

## 📋 Tổng quan

Hệ thống BookStore API đã được cập nhật để hỗ trợ **4 roles** với phân quyền rõ ràng:

## 👥 Danh sách Roles

### 1. ADMIN (RoleId: 1) - Quản trị viên
- **Quyền hạn:** Tất cả quyền trong hệ thống
- **Chức năng:**
  - Quản lý danh mục (CRUD)
  - Quản lý sách (CRUD)
  - Quản lý tác giả (CRUD)
  - Xem tất cả đơn đặt mua
  - Xem tất cả phiếu nhập
  - Quản lý người dùng
  - Truy cập tất cả API

### 2. SALES_EMPLOYEE (RoleId: 2) - Nhân viên bán hàng
- **Quyền hạn:** Quản lý bán hàng và khách hàng
- **Chức năng:**
  - Xem danh mục và sách
  - Xem đơn đặt mua
  - Tạo và cập nhật đơn đặt mua
  - Xem phiếu nhập (chỉ xem)
  - Quản lý khách hàng

### 3. DELIVERY_EMPLOYEE (RoleId: 3) - Nhân viên giao hàng
- **Quyền hạn:** Quản lý giao hàng và kho
- **Chức năng:**
  - Xem danh mục và sách
  - Xem đơn đặt mua
  - Xem phiếu nhập
  - Tạo và cập nhật phiếu nhập
  - Quản lý vận chuyển

### 4. CUSTOMER (RoleId: 4) - Khách hàng
- **Quyền hạn:** Chỉ xem thông tin công khai
- **Chức năng:**
  - Xem danh mục sách
  - Xem danh sách sách
  - Tìm kiếm sách
  - Xem thông tin chi tiết sách

## 🔐 Phân quyền chi tiết

### API Categories
- **GET /api/category** - Tất cả roles
- **POST /api/category** - Chỉ ADMIN
- **PUT /api/category/{id}** - Chỉ ADMIN

### API Books
- **GET /api/book** - Tất cả roles
- **GET /api/book/{isbn}** - Tất cả roles
- **POST /api/book** - Chỉ ADMIN
- **PUT /api/book/{isbn}** - Chỉ ADMIN
- **GET /api/book/authors** - Tất cả roles
- **POST /api/book/authors** - Chỉ ADMIN

### API Purchase Orders
- **GET /api/purchaseorder** - SALES_EMPLOYEE, DELIVERY_EMPLOYEE, ADMIN
- **POST /api/purchaseorder** - SALES_EMPLOYEE, ADMIN
- **PUT /api/purchaseorder/{id}** - SALES_EMPLOYEE, ADMIN

### API Goods Receipts
- **GET /api/goodsreceipt** - DELIVERY_EMPLOYEE, ADMIN
- **GET /api/goodsreceipt/available-purchase-orders** - DELIVERY_EMPLOYEE, ADMIN
- **POST /api/goodsreceipt** - DELIVERY_EMPLOYEE, ADMIN
- **PUT /api/goodsreceipt/{id}** - DELIVERY_EMPLOYEE, ADMIN

### API Test
- **GET /api/test** - Tất cả roles (public)
- **POST /api/test** - Tất cả roles (authenticated)
- **GET /api/test/protected** - Tất cả roles (authenticated)
- **GET /api/test/admin-only** - Chỉ ADMIN
- **GET /api/test/sales-only** - SALES_EMPLOYEE, ADMIN
- **GET /api/test/delivery-only** - DELIVERY_EMPLOYEE, ADMIN
- **GET /api/test/staff-only** - SALES_EMPLOYEE, DELIVERY_EMPLOYEE, ADMIN

## 🚀 Cách sử dụng

### 1. Đăng ký tài khoản
```json
{
  "email": "user@example.com",
  "password": "Password123!",
  "confirmPassword": "Password123!",
  "roleId": 1  // 1=ADMIN, 2=SALES_EMPLOYEE, 3=DELIVERY_EMPLOYEE, 4=CUSTOMER
}
```

### 2. Đăng nhập để lấy token
```json
{
  "email": "user@example.com",
  "password": "Password123!"
}
```

### 3. Sử dụng token trong header
```http
Authorization: Bearer {your_jwt_token}
```

## 📊 Ma trận quyền hạn

| Chức năng | ADMIN | SALES_EMPLOYEE | DELIVERY_EMPLOYEE | CUSTOMER |
|-----------|-------|----------------|-------------------|----------|
| Xem danh mục | ✅ | ✅ | ✅ | ✅ |
| Quản lý danh mục | ✅ | ❌ | ❌ | ❌ |
| Xem sách | ✅ | ✅ | ✅ | ✅ |
| Quản lý sách | ✅ | ❌ | ❌ | ❌ |
| Xem đơn đặt mua | ✅ | ✅ | ✅ | ❌ |
| Tạo đơn đặt mua | ✅ | ✅ | ❌ | ❌ |
| Xem phiếu nhập | ✅ | ✅ | ✅ | ❌ |
| Tạo phiếu nhập | ✅ | ❌ | ✅ | ❌ |

## 🔧 Cấu hình Database

### Bảng Roles
```sql
INSERT INTO role (role_id, name, description) VALUES
(1, 'ADMIN', 'Quản trị viên'),
(2, 'SALES_EMPLOYEE', 'Nhân viên bán hàng'),
(3, 'DELIVERY_EMPLOYEE', 'Nhân viên giao hàng'),
(4, 'CUSTOMER', 'Khách hàng');
```

### Bảng Permissions
```sql
INSERT INTO permission (permission_id, code, name, description) VALUES
(1, 'READ_CATEGORY', 'Đọc danh mục', 'Xem danh sách danh mục'),
(2, 'WRITE_CATEGORY', 'Ghi danh mục', 'Tạo, sửa, xóa danh mục'),
(3, 'READ_BOOK', 'Đọc sách', 'Xem danh sách sách'),
(4, 'WRITE_BOOK', 'Ghi sách', 'Tạo, sửa, xóa sách'),
(5, 'READ_PURCHASE_ORDER', 'Đọc đơn đặt mua', 'Xem danh sách đơn đặt mua'),
(6, 'WRITE_PURCHASE_ORDER', 'Ghi đơn đặt mua', 'Tạo, sửa, xóa đơn đặt mua'),
(7, 'READ_GOODS_RECEIPT', 'Đọc phiếu nhập', 'Xem danh sách phiếu nhập'),
(8, 'WRITE_GOODS_RECEIPT', 'Ghi phiếu nhập', 'Tạo, sửa, xóa phiếu nhập'),
(9, 'SALES_MANAGEMENT', 'Quản lý bán hàng', 'Quản lý đơn hàng, khách hàng'),
(10, 'DELIVERY_MANAGEMENT', 'Quản lý giao hàng', 'Quản lý vận chuyển, giao hàng');
```

## 🧪 Testing

Sử dụng script `test-4-roles.ps1` để test toàn bộ hệ thống:

```powershell
.\test-4-roles.ps1
```

Script này sẽ:
1. Đăng ký tài khoản cho cả 4 roles
2. Đăng nhập và lấy token cho mỗi role
3. Test phân quyền cho từng role
4. Hiển thị kết quả chi tiết

## 📝 Ghi chú quan trọng

1. **JWT Token:** Mỗi token chứa thông tin role của user
2. **Authorization:** Sử dụng `[Authorize(Roles = "ROLE1,ROLE2")]` để phân quyền
3. **Controller Level:** Có thể set authorization ở controller level
4. **Method Level:** Có thể override authorization ở method level
5. **Error Handling:** 403 Forbidden khi không có quyền truy cập

## 🔄 Workflow thực tế

### Quy trình bán hàng:
1. **CUSTOMER** xem sách và danh mục
2. **SALES_EMPLOYEE** tạo đơn đặt mua từ nhà xuất bản
3. **DELIVERY_EMPLOYEE** tạo phiếu nhập khi hàng về
4. **ADMIN** quản lý toàn bộ hệ thống

### Quy trình quản lý:
1. **ADMIN** tạo danh mục và sách
2. **SALES_EMPLOYEE** quản lý đơn hàng
3. **DELIVERY_EMPLOYEE** quản lý kho và giao hàng
4. **CUSTOMER** chỉ xem và mua sách

---

**📞 Support:** Xem `API_DOCUMENTATION.md` để biết chi tiết về từng API endpoint.
