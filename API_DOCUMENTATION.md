# 📚 BOOKSTORE API DOCUMENTATION

## 🚀 Tổng quan hệ thống

**Base URL:** `http://localhost:5000`  
**Authentication:** JWT Bearer Token  
**Content-Type:** `application/json`

## 🔐 Authentication & Authorization

### Roles trong hệ thống:
- **ADMIN** (RoleId: 1) - Quản trị viên (có tất cả quyền)
- **SALES_EMPLOYEE** (RoleId: 2) - Nhân viên bán hàng (quản lý đơn hàng, khách hàng)
- **DELIVERY_EMPLOYEE** (RoleId: 3) - Nhân viên giao hàng (quản lý vận chuyển, phiếu nhập)
- **CUSTOMER** (RoleId: 4) - Khách hàng (chỉ xem sách và danh mục)

---

## 📋 1. HEALTH CHECK APIs

### 1.1 Health Check
```http
GET /health
```
**Mô tả:** Kiểm tra trạng thái tổng thể của API  
**Authentication:** Không cần  
**Response:**
```json
"Healthy"
```

### 1.2 Health Ready
```http
GET /health/ready
```
**Mô tả:** Kiểm tra API sẵn sàng nhận request  
**Authentication:** Không cần  
**Response:**
```json
"Healthy"
```

### 1.3 Health Live
```http
GET /health/live
```
**Mô tả:** Kiểm tra API đang hoạt động  
**Authentication:** Không cần  
**Response:**
```json
"Healthy"
```

---

## 🔑 2. AUTHENTICATION APIs

### 2.1 Đăng ký tài khoản
```http
POST /api/auth/register
```
**Mô tả:** Đăng ký tài khoản mới  
**Authentication:** Không cần  
**Request Body:**
```json
{
  "email": "user@example.com",
  "password": "Password123!",
  "confirmPassword": "Password123!",
  "roleId": 4
}
```
**Response:**
```json
{
  "success": true,
  "message": "Đăng ký thành công",
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "user": {
      "accountId": 1,
      "email": "user@example.com",
      "roleId": 1,
      "isActive": true
    }
  }
}
```

### 2.2 Đăng nhập
```http
POST /api/auth/login
```
**Mô tả:** Đăng nhập vào hệ thống  
**Authentication:** Không cần  
**Request Body:**
```json
{
  "email": "user@example.com",
  "password": "Password123!"
}
```
**Response:**
```json
{
  "success": true,
  "message": "Đăng nhập thành công",
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "user": {
      "accountId": 1,
      "email": "user@example.com",
      "roleId": 1,
      "isActive": true
    }
  }
}
```

---

## 📚 3. CATEGORY APIs

### 3.1 Lấy danh sách danh mục
```http
GET /api/category
```
**Mô tả:** Lấy danh sách tất cả danh mục  
**Authentication:** Cần  
**Query Parameters:**
- `pageNumber` (int, optional): Số trang (mặc định: 1)
- `pageSize` (int, optional): Số item mỗi trang (mặc định: 10)
- `searchTerm` (string, optional): Tìm kiếm theo tên

**Response:**
```json
{
  "success": true,
  "message": "Lấy danh sách danh mục thành công",
  "data": {
    "categories": [
      {
        "categoryId": 1,
        "name": "Tiểu thuyết",
        "description": "Thể loại tiểu thuyết văn học",
        "bookCount": 5
      }
    ],
    "totalCount": 1,
    "pageNumber": 1,
    "pageSize": 10,
    "totalPages": 1
  }
}
```

### 3.2 Lấy danh mục theo ID
```http
GET /api/category/{id}
```
**Mô tả:** Lấy thông tin chi tiết danh mục  
**Authentication:** Cần  
**Path Parameters:**
- `id` (int): ID của danh mục

**Response:**
```json
{
  "success": true,
  "message": "Lấy thông tin danh mục thành công",
  "data": {
    "categoryId": 1,
    "name": "Tiểu thuyết",
    "description": "Thể loại tiểu thuyết văn học",
    "bookCount": 5
  }
}
```

### 3.3 Tạo danh mục mới
```http
POST /api/category
```
**Mô tả:** Tạo danh mục mới  
**Authentication:** Cần (ADMIN)  
**Request Body:**
```json
{
  "name": "Tên danh mục",
  "description": "Mô tả danh mục"
}
```
**Response:**
```json
{
  "success": true,
  "message": "Tạo danh mục thành công",
  "data": {
    "categoryId": 6,
    "name": "Tên danh mục",
    "description": "Mô tả danh mục",
    "bookCount": 0
  }
}
```

### 3.4 Cập nhật danh mục
```http
PUT /api/category/{id}
```
**Mô tả:** Cập nhật thông tin danh mục  
**Authentication:** Cần (ADMIN)  
**Path Parameters:**
- `id` (int): ID của danh mục

**Request Body:**
```json
{
  "name": "Tên danh mục đã cập nhật",
  "description": "Mô tả danh mục đã cập nhật"
}
```
**Response:**
```json
{
  "success": true,
  "message": "Cập nhật danh mục thành công",
  "data": {
    "categoryId": 6,
    "name": "Tên danh mục đã cập nhật",
    "description": "Mô tả danh mục đã cập nhật",
    "bookCount": 0
  }
}
```

---

## 📖 4. BOOK APIs

### 4.1 Lấy danh sách sách
```http
GET /api/book
```
**Mô tả:** Lấy danh sách tất cả sách  
**Authentication:** Cần  
**Query Parameters:**
- `pageNumber` (int, optional): Số trang
- `pageSize` (int, optional): Số item mỗi trang
- `searchTerm` (string, optional): Tìm kiếm theo tên sách
- `categoryId` (int, optional): Lọc theo danh mục
- `publisherId` (int, optional): Lọc theo nhà xuất bản

**Response:**
```json
{
  "success": true,
  "message": "Lấy danh sách sách thành công",
  "data": {
    "books": [
      {
        "isbn": "978-604-1-00001-1",
        "title": "Truyện Kiều",
        "pageCount": 300,
        "unitPrice": 50.00,
        "publishYear": 2020,
        "categoryId": 1,
        "categoryName": "Tiểu thuyết",
        "publisherId": 1,
        "publisherName": "NXB Kim Đồng",
        "imageUrl": "https://example.com/truyen-kieu.jpg",
        "authors": [
          {
            "authorId": 1,
            "firstName": "Nguyễn Du",
            "lastName": "Nguyễn",
            "fullName": "Nguyễn Du Nguyễn",
            "gender": "Male",
            "dateOfBirth": "1765-01-01T00:00:00Z"
          }
        ],
        "createdAt": "2024-01-01T00:00:00Z",
        "updatedAt": "2024-01-01T00:00:00Z"
      }
    ],
    "totalCount": 1,
    "pageNumber": 1,
    "pageSize": 10,
    "totalPages": 1
  }
}
```

### 4.2 Lấy sách theo ISBN
```http
GET /api/book/{isbn}
```
**Mô tả:** Lấy thông tin chi tiết sách  
**Authentication:** Cần  
**Path Parameters:**
- `isbn` (string): ISBN của sách

**Response:** Tương tự như response của danh sách sách nhưng chỉ có 1 item

### 4.3 Lấy sách theo nhà xuất bản
```http
GET /api/book/by-publisher/{publisherId}
```
**Mô tả:** Lấy danh sách sách theo nhà xuất bản  
**Authentication:** Cần  
**Path Parameters:**
- `publisherId` (long): ID của nhà xuất bản

**Query Parameters:**
- `pageNumber` (int, optional): Số trang (mặc định: 1)
- `pageSize` (int, optional): Số item mỗi trang (mặc định: 10)
- `searchTerm` (string, optional): Tìm kiếm theo tên sách, danh mục hoặc tác giả

**Response:**
```json
{
  "success": true,
  "message": "Lấy danh sách sách theo nhà xuất bản thành công",
  "data": {
    "books": [
      {
        "isbn": "978-604-1-00001-1",
        "title": "Truyện Kiều",
        "pageCount": 300,
        "unitPrice": 50.00,
        "publishYear": 2020,
        "categoryId": 1,
        "categoryName": "Tiểu thuyết",
        "publisherId": 1,
        "publisherName": "NXB Kim Đồng",
        "imageUrl": "https://example.com/truyen-kieu.jpg",
        "authors": [
          {
            "authorId": 1,
            "firstName": "Nguyễn Du",
            "lastName": "Nguyễn",
            "fullName": "Nguyễn Du Nguyễn",
            "gender": "Male",
            "dateOfBirth": "1765-01-01T00:00:00Z"
          }
        ],
        "createdAt": "2024-01-01T00:00:00Z",
        "updatedAt": "2024-01-01T00:00:00Z"
      }
    ],
    "totalCount": 1,
    "pageNumber": 1,
    "pageSize": 10,
    "totalPages": 1
  }
}
```

### 4.4 Lấy danh sách tác giả
```http
GET /api/book/authors
```
**Mô tả:** Lấy danh sách tất cả tác giả  
**Authentication:** Cần  
**Response:**
```json
{
  "success": true,
  "message": "Lấy danh sách tác giả thành công",
  "data": [
    {
      "authorId": 1,
      "firstName": "Nguyễn Du",
      "lastName": "Nguyễn",
      "fullName": "Nguyễn Du Nguyễn",
      "gender": "Male",
      "dateOfBirth": "1765-01-01T00:00:00Z",
      "address": "Hà Tĩnh",
      "email": "nguyendu@example.com"
    }
  ]
}
```

### 4.5 Tạo tác giả mới
```http
POST /api/book/authors
```
**Mô tả:** Tạo tác giả mới  
**Authentication:** Cần (ADMIN)  
**Request Body:**
```json
{
  "firstName": "Tên",
  "lastName": "Họ",
  "gender": 0,
  "dateOfBirth": "1990-01-01T00:00:00Z",
  "address": "Địa chỉ",
  "email": "email@example.com"
}
```
**Response:**
```json
{
  "success": true,
  "message": "Tạo tác giả thành công",
  "data": {
    "authorId": 6,
    "firstName": "Tên",
    "lastName": "Họ",
    "fullName": "Tên Họ",
    "gender": "Male",
    "dateOfBirth": "1990-01-01T00:00:00Z",
    "address": "Địa chỉ",
    "email": "email@example.com"
  }
}
```

---

## 🏢 5. PUBLISHER APIs

### 5.1 Lấy danh sách nhà xuất bản
```http
GET /api/publisher
```
**Mô tả:** Lấy danh sách tất cả nhà xuất bản  
**Authentication:** Cần  
**Query Parameters:**
- `pageNumber` (int, optional): Số trang (mặc định: 1)
- `pageSize` (int, optional): Số item mỗi trang (mặc định: 10)
- `searchTerm` (string, optional): Tìm kiếm theo tên, địa chỉ hoặc email

**Response:**
```json
{
  "success": true,
  "message": "Lấy danh sách nhà xuất bản thành công",
  "data": {
    "publishers": [
      {
        "publisherId": 1,
        "name": "NXB Kim Đồng",
        "address": "Hà Nội",
        "email": "kimdong@example.com",
        "phone": "02438257291",
        "bookCount": 5
      }
    ],
    "totalCount": 1,
    "pageNumber": 1,
    "pageSize": 10,
    "totalPages": 1
  }
}
```

### 5.2 Lấy nhà xuất bản theo ID
```http
GET /api/publisher/{id}
```
**Mô tả:** Lấy thông tin chi tiết nhà xuất bản  
**Authentication:** Cần  
**Path Parameters:**
- `id` (long): ID của nhà xuất bản

**Response:**
```json
{
  "success": true,
  "message": "Lấy thông tin nhà xuất bản thành công",
  "data": {
    "publisherId": 1,
    "name": "NXB Kim Đồng",
    "address": "Hà Nội",
    "email": "kimdong@example.com",
    "phone": "02438257291",
    "bookCount": 5
  }
}
```

### 5.3 Tạo nhà xuất bản mới
```http
POST /api/publisher
```
**Mô tả:** Tạo nhà xuất bản mới  
**Authentication:** Cần (ADMIN)  
**Request Body:**
```json
{
  "name": "NXB Test",
  "address": "123 Test Street, Test City",
  "email": "test@example.com",
  "phone": "0123456789"
}
```
**Response:**
```json
{
  "success": true,
  "message": "Tạo nhà xuất bản thành công",
  "data": {
    "publisherId": 4,
    "name": "NXB Test",
    "address": "123 Test Street, Test City",
    "email": "test@example.com",
    "phone": "0123456789",
    "bookCount": 0
  }
}
```

### 5.4 Cập nhật nhà xuất bản
```http
PUT /api/publisher/{id}
```
**Mô tả:** Cập nhật thông tin nhà xuất bản  
**Authentication:** Cần (ADMIN)  
**Path Parameters:**
- `id` (long): ID của nhà xuất bản

**Request Body:**
```json
{
  "name": "NXB Test Updated",
  "address": "456 Updated Street, Updated City",
  "email": "updated@example.com",
  "phone": "0987654321"
}
```
**Response:**
```json
{
  "success": true,
  "message": "Cập nhật nhà xuất bản thành công",
  "data": {
    "publisherId": 4,
    "name": "NXB Test Updated",
    "address": "456 Updated Street, Updated City",
    "email": "updated@example.com",
    "phone": "0987654321",
    "bookCount": 0
  }
}
```

### 5.5 Xóa nhà xuất bản
```http
DELETE /api/publisher/{id}
```
**Mô tả:** Xóa nhà xuất bản  
**Authentication:** Cần (ADMIN)  
**Path Parameters:**
- `id` (long): ID của nhà xuất bản

**Response:**
```json
{
  "success": true,
  "message": "Xóa nhà xuất bản thành công",
  "data": true
}
```

---

## 🛒 6. PURCHASE ORDER APIs

### 6.1 Lấy danh sách đơn đặt mua
```http
GET /api/purchaseorder
```
**Mô tả:** Lấy danh sách tất cả đơn đặt mua  
**Authentication:** Cần (SALES_EMPLOYEE/DELIVERY_EMPLOYEE/ADMIN)  
**Query Parameters:**
- `pageNumber` (int, optional): Số trang
- `pageSize` (int, optional): Số item mỗi trang
- `searchTerm` (string, optional): Tìm kiếm theo ghi chú

**Response:**
```json
{
  "success": true,
  "message": "Lấy danh sách đơn đặt mua thành công",
  "data": {
    "purchaseOrders": [
      {
        "poId": 1,
        "publisherId": 1,
        "publisherName": "NXB Kim Đồng",
        "orderedAt": "2024-01-01T00:00:00Z",
        "createdBy": 1,
        "createdByName": "Admin User",
        "note": "Ghi chú đơn hàng",
        "totalAmount": 500.00,
        "totalQuantity": 10,
        "lines": [
          {
            "poLineId": 1,
            "isbn": "978-604-1-00001-1",
            "bookTitle": "Truyện Kiều",
            "qtyOrdered": 5,
            "unitPrice": 50.00,
            "lineTotal": 250.00
          }
        ]
      }
    ],
    "totalCount": 1,
    "pageNumber": 1,
    "pageSize": 10,
    "totalPages": 1
  }
}
```

### 5.2 Tạo đơn đặt mua mới
```http
POST /api/purchaseorder
```
**Mô tả:** Tạo đơn đặt mua mới  
**Authentication:** Cần (SALES_EMPLOYEE/ADMIN)  
**Request Body:**
```json
{
  "publisherId": 1,
  "note": "Ghi chú đơn hàng",
  "lines": [
    {
      "isbn": "978-604-1-00001-1",
      "qtyOrdered": 10,
      "unitPrice": 45.00
    },
    {
      "isbn": "978-604-1-00002-2",
      "qtyOrdered": 5,
      "unitPrice": 35.00
    }
  ]
}
```
**Response:**
```json
{
  "success": true,
  "message": "Tạo đơn đặt mua thành công",
  "data": {
    "poId": 2,
    "publisherId": 1,
    "publisherName": "NXB Kim Đồng",
    "orderedAt": "2024-01-01T00:00:00Z",
    "createdBy": 1,
    "createdByName": "Admin User",
    "note": "Ghi chú đơn hàng",
    "totalAmount": 625.00,
    "totalQuantity": 15,
    "lines": [
      {
        "poLineId": 3,
        "isbn": "978-604-1-00001-1",
        "bookTitle": "Truyện Kiều",
        "qtyOrdered": 10,
        "unitPrice": 45.00,
        "lineTotal": 450.00
      },
      {
        "poLineId": 4,
        "isbn": "978-604-1-00002-2",
        "bookTitle": "Nhật ký trong tù",
        "qtyOrdered": 5,
        "unitPrice": 35.00,
        "lineTotal": 175.00
      }
    ]
  }
}
```

---

## 📦 6. GOODS RECEIPT APIs

### 6.1 Lấy danh sách phiếu nhập
```http
GET /api/goodsreceipt
```
**Mô tả:** Lấy danh sách tất cả phiếu nhập  
**Authentication:** Cần (DELIVERY_EMPLOYEE/ADMIN)  
**Query Parameters:**
- `pageNumber` (int, optional): Số trang
- `pageSize` (int, optional): Số item mỗi trang

**Response:**
```json
{
  "success": true,
  "message": "Lấy danh sách phiếu nhập thành công",
  "data": {
    "goodsReceipts": [
      {
        "grId": 1,
        "poId": 1,
        "publisherName": "NXB Kim Đồng",
        "receivedAt": "2024-01-01T00:00:00Z",
        "createdBy": 1,
        "createdByName": "Admin User",
        "note": "Ghi chú phiếu nhập",
        "totalAmount": 400.00,
        "totalQuantity": 8,
        "lines": [
          {
            "grLineId": 1,
            "isbn": "978-604-1-00001-1",
            "bookTitle": "Truyện Kiều",
            "qtyReceived": 8,
            "unitCost": 42.00,
            "lineTotal": 336.00
          }
        ]
      }
    ],
    "totalCount": 1,
    "pageNumber": 1,
    "pageSize": 10,
    "totalPages": 1
  }
}
```

### 6.2 Lấy danh sách đơn đặt mua có thể tạo phiếu nhập
```http
GET /api/goodsreceipt/available-purchase-orders
```
**Mô tả:** Lấy danh sách đơn đặt mua chưa có phiếu nhập  
**Authentication:** Cần (DELIVERY_EMPLOYEE/ADMIN)  
**Response:**
```json
{
  "success": true,
  "message": "Lấy danh sách đơn đặt mua có thể tạo phiếu nhập thành công",
  "data": [
    {
      "poId": 1,
      "publisherId": 1,
      "publisherName": "NXB Kim Đồng",
      "orderedAt": "2024-01-01T00:00:00Z",
      "createdBy": 1,
      "createdByName": "Admin User",
      "note": "Ghi chú đơn hàng",
      "totalAmount": 500.00,
      "totalQuantity": 10,
      "lines": [
        {
          "poLineId": 1,
          "isbn": "978-604-1-00001-1",
          "bookTitle": "Truyện Kiều",
          "qtyOrdered": 5,
          "unitPrice": 50.00,
          "lineTotal": 250.00
        }
      ]
    }
  ]
}
```

### 6.3 Tạo phiếu nhập mới
```http
POST /api/goodsreceipt
```
**Mô tả:** Tạo phiếu nhập mới từ đơn đặt mua  
**Authentication:** Cần (DELIVERY_EMPLOYEE/ADMIN)  
**Request Body:**
```json
{
  "poId": 1,
  "note": "Ghi chú phiếu nhập",
  "lines": [
    {
      "qtyReceived": 8,
      "unitCost": 42.00
    },
    {
      "qtyReceived": 4,
      "unitCost": 32.00
    }
  ]
}
```
**Response:**
```json
{
  "success": true,
  "message": "Tạo phiếu nhập thành công",
  "data": {
    "grId": 2,
    "poId": 1,
    "publisherName": "NXB Kim Đồng",
    "receivedAt": "2024-01-01T00:00:00Z",
    "createdBy": 1,
    "createdByName": "Admin User",
    "note": "Ghi chú phiếu nhập",
    "totalAmount": 464.00,
    "totalQuantity": 12,
    "lines": [
      {
        "grLineId": 3,
        "isbn": "978-604-1-00001-1",
        "bookTitle": "Truyện Kiều",
        "qtyReceived": 8,
        "unitCost": 42.00,
        "lineTotal": 336.00
      },
      {
        "grLineId": 4,
        "isbn": "978-604-1-00002-2",
        "bookTitle": "Nhật ký trong tù",
        "qtyReceived": 4,
        "unitCost": 32.00,
        "lineTotal": 128.00
      }
    ]
  }
}
```

---

## 🧪 7. TEST APIs

### 7.1 Test GET (Public)
```http
GET /api/test
```
**Mô tả:** Endpoint test công khai  
**Authentication:** Không cần  
**Response:**
```json
{
  "message": "Đây là endpoint công khai, ai cũng có thể truy cập",
  "timestamp": "2024-01-01T00:00:00Z"
}
```

### 7.2 Test POST (Protected)
```http
POST /api/test
```
**Mô tả:** Endpoint test yêu cầu authentication  
**Authentication:** Cần  
**Response:**
```json
{
  "message": "Bạn đã đăng nhập thành công!",
  "email": "user@example.com",
  "role": "ADMIN",
  "timestamp": "2024-01-01T00:00:00Z"
}
```

### 7.3 Test Protected
```http
GET /api/test/protected
```
**Mô tả:** Endpoint test yêu cầu authentication  
**Authentication:** Cần  
**Response:**
```json
{
  "message": "Bạn đã đăng nhập thành công!",
  "email": "user@example.com",
  "role": "ADMIN",
  "timestamp": "2024-01-01T00:00:00Z"
}
```

### 7.4 Test Admin Only
```http
GET /api/test/admin-only
```
**Mô tả:** Endpoint chỉ dành cho ADMIN  
**Authentication:** Cần (ADMIN)  
**Response:**
```json
{
  "message": "Chỉ ADMIN mới có thể truy cập endpoint này",
  "email": "admin@example.com",
  "timestamp": "2024-01-01T00:00:00Z"
}
```

### 7.5 Test Sales Only
```http
GET /api/test/sales-only
```
**Mô tả:** Endpoint dành cho nhân viên bán hàng và ADMIN  
**Authentication:** Cần (SALES_EMPLOYEE/ADMIN)  
**Response:**
```json
{
  "message": "Endpoint dành cho nhân viên bán hàng và admin",
  "email": "sales@example.com",
  "role": "SALES_EMPLOYEE",
  "timestamp": "2024-01-01T00:00:00Z"
}
```

### 7.6 Test Delivery Only
```http
GET /api/test/delivery-only
```
**Mô tả:** Endpoint dành cho nhân viên giao hàng và ADMIN  
**Authentication:** Cần (DELIVERY_EMPLOYEE/ADMIN)  
**Response:**
```json
{
  "message": "Endpoint dành cho nhân viên giao hàng và admin",
  "email": "delivery@example.com",
  "role": "DELIVERY_EMPLOYEE",
  "timestamp": "2024-01-01T00:00:00Z"
}
```

### 7.7 Test Staff Only
```http
GET /api/test/staff-only
```
**Mô tả:** Endpoint dành cho tất cả nhân viên và ADMIN  
**Authentication:** Cần (SALES_EMPLOYEE/DELIVERY_EMPLOYEE/ADMIN)  
**Response:**
```json
{
  "message": "Endpoint dành cho tất cả nhân viên và admin",
  "email": "employee@example.com",
  "role": "SALES_EMPLOYEE",
  "timestamp": "2024-01-01T00:00:00Z"
}
```

---

## 👥 8. EMPLOYEE & DEPARTMENT MANAGEMENT APIs

### 8.1 Employee APIs (ADMIN)

List employees (search, filter by department, pagination)
```http
GET /api/employee?searchTerm=&departmentId=&pageNumber=1&pageSize=10
```

Get employee by id
```http
GET /api/employee/{employeeId}
```

Create employee with login account (transactional)
```http
POST /api/employee/create-with-account
Content-Type: application/json

{
  "accountEmail": "employee@example.com",
  "password": "Password123!",
  "roleId": 2,
  "isActive": true,
  "departmentId": 1,
  "firstName": "Nguyen",
  "lastName": "Van A",
  "gender": "Male",
  "dateOfBirth": "1995-01-01T00:00:00Z",
  "address": "HN",
  "phone": "0900000000",
  "employeeEmail": "nv.a@example.com"
}
```

Update employee
```http
PUT /api/employee/{employeeId}
```

Delete employee
```http
DELETE /api/employee/{employeeId}
```

### 8.2 Department APIs (ADMIN)

List departments
```http
GET /api/department?pageNumber=1&pageSize=10&searchTerm=
```

Get department by id
```http
GET /api/department/{departmentId}
```

Create department
```http
POST /api/department
{
  "name": "Phòng Kinh doanh",
  "description": "Quản lý bán hàng"
}
```

Update department
```http
PUT /api/department/{departmentId}
```

Delete department
```http
DELETE /api/department/{departmentId}
```

Notes
- All endpoints require JWT and ADMIN role.
- Permissions added: READ_EMPLOYEE, WRITE_EMPLOYEE, READ_DEPARTMENT, WRITE_DEPARTMENT.

---

## 🎯 7. PROMOTION APIs

### 7.1 Lấy danh sách khuyến mãi
```http
GET /api/promotion
```
**Authentication:** Cần (ADMIN, EMPLOYEE)
**Query:** `Name`, `MinDiscountPct`, `MaxDiscountPct`, `StartDate`, `EndDate`, `Status` (active|upcoming|expired|all), `IssuedBy`, `BookIsbn`, `Page`, `PageSize`, `SortBy`, `SortOrder`
**Response:**
```json
{
  "success": true,
  "message": "Lấy danh sách khuyến mãi thành công",
  "data": {
    "promotions": [
      {
        "promotionId": 1,
        "name": "Sale T10",
        "description": "Giảm 15%",
        "discountPct": 15.0,
        "startDate": "2025-10-01",
        "endDate": "2025-10-31",
        "issuedBy": 2,
        "issuedByName": "Sales User",
        "createdAt": "2025-10-01T00:00:00Z",
        "updatedAt": "2025-10-01T00:00:00Z",
        "books": []
      }
    ],
    "totalCount": 1,
    "pageNumber": 1,
    "pageSize": 10,
    "totalPages": 1
  }
}
```

### 7.2 Lấy chi tiết khuyến mãi
```http
GET /api/promotion/{promotionId}
```
**Authentication:** Cần (ADMIN, EMPLOYEE)

### 7.3 Tạo khuyến mãi
```http
POST /api/promotion
```
**Authentication:** Cần (ADMIN, SALES_EMPLOYEE)
**Body:**
```json
{
  "name": "Sale T10",
  "description": "Giảm 15%",
  "discountPct": 15.0,
  "startDate": "2025-10-05",
  "endDate": "2025-10-31",
  "bookIsbns": ["978-604-1-00001-1", "978-604-1-00002-2"]
}
```

### 7.4 Cập nhật khuyến mãi
```http
PUT /api/promotion/{promotionId}
```
**Authentication:** Cần (ADMIN, SALES_EMPLOYEE)
**Body:** cùng cấu trúc với tạo mới

### 7.5 Xóa khuyến mãi
```http
DELETE /api/promotion/{promotionId}
```
**Authentication:** Cần (ADMIN)

### 7.6 Thống kê khuyến mãi
```http
GET /api/promotion/stats
```
**Authentication:** Cần (ADMIN, EMPLOYEE)

### 7.7 Danh sách sách đang có khuyến mãi (Public)
```http
GET /api/promotion/active-books
```
**Authentication:** Không cần

### 7.8 Danh sách khuyến mãi theo ISBN (Public)
```http
GET /api/promotion/book/{isbn}
```
**Authentication:** Không cần

---

## 🧾 8. CUSTOMER ORDER APIs

### 8.1 Lấy danh sách đơn hàng
```http
GET /api/order
```
**Authentication:** Cần (ADMIN, EMPLOYEE, DELIVERY_EMPLOYEE)
**Query:** `keyword`, `customerId`, `status` (Pending|Assigned|Delivered), `fromDate`, `toDate`, `pageNumber`, `pageSize`
**Response:**
```json
{
  "success": true,
  "message": "Lấy danh sách đơn hàng thành công",
  "data": {
    "orders": [
      {
        "orderId": 1,
        "customerId": 10,
        "customerName": "Nguyen Van A",
        "placedAt": "2025-10-01T03:00:00Z",
        "receiverName": "B",
        "receiverPhone": "0900000000",
        "shippingAddress": "HN",
        "deliveryDate": null,
        "status": "Pending",
        "approvedBy": null,
        "deliveredBy": null,
        "totalAmount": 300.0,
        "totalQuantity": 3,
        "lines": []
      }
    ],
    "totalCount": 1,
    "pageNumber": 1,
    "pageSize": 10,
    "totalPages": 1
  }
}
```

### 8.2 Lấy chi tiết đơn hàng
```http
GET /api/order/{orderId}
```
**Authentication:** Cần (ADMIN, EMPLOYEE, DELIVERY_EMPLOYEE)

### 8.3 Duyệt/Không duyệt đơn
```http
POST /api/order/{orderId}/approve
```
**Authentication:** Cần (ADMIN, EMPLOYEE)
**Body:**
```json
{
  "approved": true,
  "note": "Đồng ý"
}
```

### 8.4 Phân công giao hàng
```http
POST /api/order/{orderId}/assign-delivery
```
**Authentication:** Cần (ADMIN, EMPLOYEE)
**Body:**
```json
{
  "deliveryEmployeeId": 2,
  "deliveryDate": "2025-10-03T00:00:00Z"
}
```

### 8.5 Xác nhận giao hàng thành công
```http
POST /api/order/{orderId}/confirm-delivered
```
**Authentication:** Cần (ADMIN, DELIVERY_EMPLOYEE)
**Body:**
```json
{
  "success": true,
  "note": "Đã giao khách"
}
```

---

## 📖 8. SWAGGER UI

### 8.1 Swagger Documentation
```http
GET /swagger
```
**Mô tả:** Truy cập Swagger UI để xem tài liệu API tương tác  
**Authentication:** Không cần  
**Response:** HTML page với Swagger UI

---

## 🔧 Error Handling

### Cấu trúc lỗi chung:
```json
{
  "success": false,
  "message": "Mô tả lỗi",
  "data": null,
  "errors": ["Chi tiết lỗi 1", "Chi tiết lỗi 2"]
}
```

### Các mã lỗi thường gặp:
- **400 Bad Request:** Dữ liệu đầu vào không hợp lệ
- **401 Unauthorized:** Chưa đăng nhập hoặc token không hợp lệ
- **403 Forbidden:** Không có quyền truy cập
- **404 Not Found:** Không tìm thấy resource
- **500 Internal Server Error:** Lỗi server

---

## 🔑 Authentication Headers

Để sử dụng các API yêu cầu authentication, thêm header sau:

```http
Authorization: Bearer {your_jwt_token}
Content-Type: application/json
```

---

## 📝 Ghi chú quan trọng

1. **Pagination:** Tất cả API danh sách đều hỗ trợ phân trang
2. **Search:** Hầu hết API danh sách đều hỗ trợ tìm kiếm
3. **Validation:** Tất cả input đều được validate
4. **Role-based Access:** Một số API chỉ dành cho ADMIN/EMPLOYEE
5. **Timestamps:** Tất cả timestamps đều ở định dạng UTC ISO 8601
6. **Currency:** Tất cả giá tiền đều ở định dạng decimal với 2 chữ số thập phân

---

## 🚀 Quick Start

1. **Đăng ký tài khoản ADMIN:**
```bash
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@bookstore.com",
    "password": "Admin123!",
    "confirmPassword": "Admin123!",
    "roleId": 1
  }'
```

2. **Đăng nhập để lấy token:**
```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@bookstore.com",
    "password": "Admin123!"
  }'
```

3. **Sử dụng token để gọi API:**
```bash
curl -X GET http://localhost:5000/api/category \
  -H "Authorization: Bearer {your_token}"
```

---

**📞 Support:** Nếu có vấn đề gì, hãy kiểm tra Swagger UI tại `http://localhost:5000/swagger`
