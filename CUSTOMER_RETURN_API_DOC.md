# 📦 **API Trả Hàng Cho Customer - Documentation**

## 🎯 **Tổng Quan**

Đã mở API trả hàng cho Customer với các endpoint riêng biệt, đảm bảo Customer chỉ có thể quản lý yêu cầu trả hàng của chính mình.

---

## 🔐 **Phân Quyền**

### **Customer API** (`/api/customer/return`)
- **Quyền**: `CUSTOMER` role
- **Giới hạn**: Chỉ xem/quản lý returns của chính mình
- **Bảo mật**: Tự động lấy `customerId` từ JWT token

### **Admin/Employee API** (`/api/return`)
- **Quyền**: `ADMIN, EMPLOYEE` roles  
- **Quyền hạn**: Xem/quản lý tất cả returns
- **Chức năng**: Xử lý và cập nhật trạng thái returns

---

## 📋 **API Endpoints Cho Customer**

### **1. Tạo Yêu Cầu Trả Hàng**
```http
POST /api/customer/return
Authorization: Bearer <CUSTOMER_JWT_TOKEN>
Content-Type: application/json

{
  "orderId": 15,
  "reason": "Sản phẩm bị lỗi",
  "returnLines": [
    {
      "isbn": "978-604-1-00001-1",
      "quantity": 1,
      "reason": "Sách bị rách trang"
    }
  ]
}
```

**Response:**
```json
{
  "success": true,
  "message": "Tạo yêu cầu trả hàng thành công",
  "data": {
    "returnId": 1,
    "orderId": 15,
    "customerId": 6,
    "status": "PENDING",
    "reason": "Sản phẩm bị lỗi",
    "createdAt": "2025-01-10T15:30:00",
    "returnLines": [
      {
        "returnLineId": 1,
        "isbn": "978-604-1-00001-1",
        "quantity": 1,
        "reason": "Sách bị rách trang"
      }
    ]
  }
}
```

### **2. Xem Danh Sách Yêu Cầu Trả Hàng**
```http
GET /api/customer/return?pageNumber=1&pageSize=10&status=PENDING
Authorization: Bearer <CUSTOMER_JWT_TOKEN>
```

**Query Parameters:**
- `pageNumber` (default: 1): Số trang
- `pageSize` (default: 10): Kích thước trang
- `status` (optional): Lọc theo trạng thái (PENDING, APPROVED, REJECTED, CANCELLED, COMPLETED)
- `fromDate` (optional): Từ ngày
- `toDate` (optional): Đến ngày

**Response:**
```json
{
  "success": true,
  "message": "Lấy danh sách yêu cầu trả hàng thành công",
  "data": {
    "returns": [
      {
        "returnId": 1,
        "orderId": 15,
        "customerId": 6,
        "status": "PENDING",
        "reason": "Sản phẩm bị lỗi",
        "createdAt": "2025-01-10T15:30:00",
        "processedAt": null,
        "processedBy": null,
        "returnLines": [...]
      }
    ],
    "totalCount": 1,
    "pageNumber": 1,
    "pageSize": 10,
    "totalPages": 1
  }
}
```

### **3. Xem Chi Tiết Yêu Cầu Trả Hàng**
```http
GET /api/customer/return/{returnId}
Authorization: Bearer <CUSTOMER_JWT_TOKEN>
```

**Response:**
```json
{
  "success": true,
  "message": "Lấy chi tiết yêu cầu trả hàng thành công",
  "data": {
    "returnId": 1,
    "orderId": 15,
    "customerId": 6,
    "status": "PENDING",
    "reason": "Sản phẩm bị lỗi",
    "createdAt": "2025-01-10T15:30:00",
    "processedAt": null,
    "processedBy": null,
    "returnLines": [
      {
        "returnLineId": 1,
        "isbn": "978-604-1-00001-1",
        "bookTitle": "Truyện Kiều",
        "quantity": 1,
        "unitPrice": 50000,
        "reason": "Sách bị rách trang"
      }
    ]
  }
}
```

### **4. Hủy Yêu Cầu Trả Hàng**
```http
PUT /api/customer/return/{returnId}/cancel
Authorization: Bearer <CUSTOMER_JWT_TOKEN>
```

**Điều kiện:** Chỉ có thể hủy khi `status = "PENDING"`

**Response:**
```json
{
  "success": true,
  "message": "Hủy yêu cầu trả hàng thành công",
  "data": {
    "returnId": 1,
    "status": "CANCELLED",
    "processedAt": "2025-01-10T16:00:00",
    "processedBy": "CUSTOMER_CANCEL"
  }
}
```

---

## 🔄 **Trạng Thái Yêu Cầu Trả Hàng**

| Status | Mô tả | Ai có thể thay đổi |
|--------|-------|-------------------|
| `PENDING` | Chờ xử lý | Customer (hủy), Admin/Employee (duyệt/từ chối) |
| `APPROVED` | Đã duyệt | Admin/Employee |
| `REJECTED` | Từ chối | Admin/Employee |
| `CANCELLED` | Đã hủy | Customer, Admin/Employee |
| `COMPLETED` | Hoàn thành | Admin/Employee |

---

## 🛡️ **Bảo Mật**

### **Kiểm Tra Quyền Sở Hữu:**
- Customer chỉ có thể xem/quản lý returns của chính mình
- Tự động lấy `customerId` từ JWT token
- Kiểm tra `return.CustomerId` trước khi cho phép truy cập

### **Validation:**
- Kiểm tra đơn hàng có thuộc về customer không
- Kiểm tra sản phẩm có trong đơn hàng không
- Kiểm tra số lượng trả không vượt quá số lượng đã mua

---

## 📊 **So Sánh API**

| Chức năng | Customer API | Admin/Employee API |
|-----------|--------------|-------------------|
| **Tạo return** | ✅ Chỉ cho chính mình | ✅ Cho bất kỳ customer |
| **Xem danh sách** | ✅ Chỉ của mình | ✅ Tất cả returns |
| **Xem chi tiết** | ✅ Chỉ của mình | ✅ Bất kỳ return nào |
| **Cập nhật status** | ❌ Chỉ hủy (PENDING) | ✅ Tất cả status |
| **Hủy return** | ✅ Chỉ của mình | ✅ Bất kỳ return nào |

---

## 🚀 **Cách Sử Dụng**

### **1. Customer tạo yêu cầu trả hàng:**
```javascript
const createReturn = async (orderId, reason, returnLines) => {
  const response = await fetch('/api/customer/return', {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${customerToken}`,
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({
      orderId,
      reason,
      returnLines
    })
  });
  return response.json();
};
```

### **2. Customer xem danh sách returns:**
```javascript
const getMyReturns = async (page = 1, status = null) => {
  const params = new URLSearchParams({ pageNumber: page });
  if (status) params.append('status', status);
  
  const response = await fetch(`/api/customer/return?${params}`, {
    headers: {
      'Authorization': `Bearer ${customerToken}`
    }
  });
  return response.json();
};
```

### **3. Customer hủy yêu cầu:**
```javascript
const cancelReturn = async (returnId) => {
  const response = await fetch(`/api/customer/return/${returnId}/cancel`, {
    method: 'PUT',
    headers: {
      'Authorization': `Bearer ${customerToken}`
    }
  });
  return response.json();
};
```

---

## ⚠️ **Lưu Ý**

1. **Customer chỉ có thể trả hàng từ đơn hàng của chính mình**
2. **Chỉ có thể hủy yêu cầu khi status = "PENDING"**
3. **Không thể tự cập nhật status thành APPROVED/REJECTED**
4. **Cần có JWT token hợp lệ với role CUSTOMER**

---

## ✅ **Hoàn Thành**

**API trả hàng cho Customer đã được mở với đầy đủ chức năng và bảo mật!** 🎉

- ✅ Tạo yêu cầu trả hàng
- ✅ Xem danh sách returns của mình  
- ✅ Xem chi tiết return
- ✅ Hủy yêu cầu (khi PENDING)
- ✅ Bảo mật: chỉ truy cập returns của chính mình
- ✅ Validation đầy đủ
