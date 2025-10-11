# 📋 **DOCUMENTATION - Cập Nhật Hệ Thống Order & Invoice**

## 🔄 **Tổng Quan Thay Đổi**

Hệ thống đã được cập nhật để thay đổi logic quản lý đơn hàng và thêm hệ thống hóa đơn (Invoice) để kiểm tra thanh toán.

---

## 📊 **1. Thay Đổi Order Status Logic**

### **Trước đây:**
```csharp
public enum OrderStatus
{
    Paid = 0,           // 0 - Đã thanh toán
    Assigned = 1,        // 1 - Đã phân công
    Delivered = 2,       // 2 - Đã giao
    PendingPayment = 3,  // 3 - Chờ thanh toán
    Cancelled = 4        // 4 - Đã hủy
}
```

### **Sau khi cập nhật:**
```csharp
public enum OrderStatus
{
    PendingConfirmation = 0,  // 0 - Chờ xác nhận
    Confirmed = 1,             // 1 - Đã xác nhận/Phân công
    Delivered = 2,             // 2 - Đã giao
    Cancelled = 3              // 3 - Đã hủy
}
```

### **Ý nghĩa mới:**
- **0 (PendingConfirmation)**: Đơn hàng đã tạo, chờ xác nhận
- **1 (Confirmed)**: Đã xác nhận và có thể phân công giao hàng
- **2 (Delivered)**: Đã giao hàng thành công
- **3 (Cancelled)**: Đơn hàng đã bị hủy

---

## 💰 **2. Hệ Thống Invoice Mới**

### **Mục đích:**
- Thay thế việc kiểm tra thanh toán qua Order Status
- Quản lý hóa đơn độc lập với đơn hàng
- Theo dõi trạng thái thanh toán chi tiết

### **Model Invoice:**
```csharp
public class Invoice
{
    public long InvoiceId { get; set; }
    public long OrderId { get; set; }
    public string InvoiceNumber { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentStatus { get; set; } // PENDING, PAID, FAILED, REFUNDED
    public string? PaymentMethod { get; set; }
    public string? PaymentReference { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

---

## 🔧 **3. Các API Đã Cập Nhật**

### **3.1 API Tạo Đơn Hàng**
- **Endpoint**: `POST /api/order`
- **Thay đổi**: Tạo đơn hàng với `Status = 0` (PendingConfirmation)
- **Logic**: Không còn tạo payment link ngay lập tức

### **3.2 API Webhook Thanh Toán**
- **Endpoint**: `POST /api/payment/webhook`
- **Thay đổi**: Khi thanh toán thành công → Tạo Invoice với `PaymentStatus = "PAID"`
- **Logic**: Không còn update Order Status thành Paid

### **3.3 API Xác Nhận Đơn Hàng**
- **Endpoint**: `POST /api/order/{orderId}/approve`
- **Thay đổi**: Kiểm tra Invoice PAID trước khi xác nhận
- **Logic**: 
  ```csharp
  // Kiểm tra thanh toán qua Invoice
  var hasPaid = await _invoiceService.HasPaidInvoiceAsync(orderId);
  if (!hasPaid) {
      return "Đơn hàng chưa thanh toán, không thể duyệt";
  }
  ```

### **3.4 API Phân Công Giao Hàng**
- **Endpoint**: `POST /api/order/{orderId}/assign-delivery`
- **Thay đổi**: Chỉ cho phép với `Status = 1` (Confirmed)
- **Logic**: Phân công nhân viên giao hàng

### **3.5 API Xác Nhận Giao Hàng**
- **Endpoint**: `POST /api/order/{orderId}/confirm-delivered`
- **Thay đổi**: Chuyển từ `Status = 1` → `Status = 2`
- **Logic**: Hoàn thành giao hàng

---

## 📊 **4. API Invoice Mới**

### **4.1 Lấy Danh Sách Hóa Đơn**
```http
GET /api/invoice?orderId=123&paymentStatus=PAID&pageNumber=1&pageSize=10
Authorization: Bearer <JWT_TOKEN> (ADMIN/EMPLOYEE)
```

### **4.2 Lấy Hóa Đơn Theo ID**
```http
GET /api/invoice/{invoiceId}
Authorization: Bearer <JWT_TOKEN> (ADMIN/EMPLOYEE)
```

### **4.3 Lấy Hóa Đơn Theo Order ID**
```http
GET /api/invoice/order/{orderId}
Authorization: Bearer <JWT_TOKEN> (ADMIN/EMPLOYEE/CUSTOMER)
```

### **4.4 Kiểm Tra Thanh Toán**
```http
GET /api/invoice/check-payment/{orderId}
Authorization: Bearer <JWT_TOKEN> (ADMIN/EMPLOYEE/CUSTOMER)
```

**Response:**
```json
{
  "success": true,
  "message": "Đơn hàng đã thanh toán",
  "data": true
}
```

---

## 🔄 **5. Flow Mới**

### **5.1 Flow Tạo Đơn Hàng:**
1. **Tạo đơn hàng** → `Status = 0` (PendingConfirmation)
2. **Tạo payment link** → Redirect user đến PayOS
3. **User thanh toán** → PayOS gửi webhook
4. **Webhook nhận PAID** → Tạo Invoice với `PaymentStatus = "PAID"`
5. **Trừ tồn kho** → Cập nhật stock của sách

### **5.2 Flow Xác Nhận Đơn Hàng:**
1. **Admin/Employee** gọi API xác nhận
2. **Kiểm tra Invoice** → Có `PaymentStatus = "PAID"` không?
3. **Nếu đã thanh toán** → `Status = 1` (Confirmed)
4. **Nếu chưa thanh toán** → Trả lỗi "Chưa thanh toán"

### **5.3 Flow Giao Hàng:**
1. **Phân công giao hàng** → `Status = 1` + gán nhân viên
2. **Nhân viên giao hàng** → Xác nhận giao thành công
3. **Cập nhật status** → `Status = 2` (Delivered)

---

## 🗄️ **6. Cập Nhật Database**

### **6.1 Script Cập Nhật Bảng Invoice:**
```sql
-- Chạy script update-invoice-table.sql
source update-invoice-table.sql;
```

### **6.2 Các Cột Cần Thêm (nếu chưa có):**
- `payment_method` VARCHAR(50) NULL
- `payment_reference` VARCHAR(100) NULL  
- `paid_at` DATETIME NULL
- `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
- `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP

### **6.3 Index Cần Thêm:**
- `idx_invoice_order` ON (`order_id`)
- `idx_invoice_payment_status` ON (`payment_status`)
- `idx_invoice_created_at` ON (`created_at`)
- `uk_invoice_number` UNIQUE ON (`invoice_number`)

---

## ⚠️ **7. Breaking Changes**

### **7.1 API Response Changes:**
- Order Status values đã thay đổi (0,1,2,3 thay vì 0,1,2,3,4)
- Thêm Invoice endpoints mới

### **7.2 Logic Changes:**
- Không còn tự động update Order Status khi thanh toán
- Phải kiểm tra Invoice để biết đã thanh toán chưa
- Xác nhận đơn hàng yêu cầu Invoice PAID

### **7.3 Frontend Impact:**
- Cần cập nhật mapping Order Status values
- Cần tích hợp API Invoice để kiểm tra thanh toán
- UI có thể cần hiển thị thông tin Invoice

---

## 🚀 **8. Migration Guide**

### **8.1 Backend:**
- ✅ OrderStatus enum đã cập nhật
- ✅ OrderService logic đã sửa
- ✅ PaymentService webhook đã sửa
- ✅ InvoiceService và Controller đã tạo
- ✅ Program.cs đã đăng ký services

### **8.2 Database:**
```sql
-- Chạy script cập nhật
source update-invoice-table.sql;
```

### **8.3 Frontend (Cần cập nhật):**
```javascript
// Cập nhật Order Status mapping
const ORDER_STATUS = {
  0: 'Chờ xác nhận',
  1: 'Đã xác nhận', 
  2: 'Đã giao',
  3: 'Đã hủy'
};

// Thêm API kiểm tra thanh toán
const checkPayment = async (orderId) => {
  const response = await fetch(`/api/invoice/check-payment/${orderId}`);
  return response.json();
};
```

---

## ✅ **9. Testing Checklist**

- [ ] Tạo đơn hàng → Status = 0
- [ ] Thanh toán thành công → Tạo Invoice PAID
- [ ] Xác nhận đơn hàng → Kiểm tra Invoice → Status = 1
- [ ] Phân công giao hàng → Status = 1 + gán nhân viên
- [ ] Xác nhận giao hàng → Status = 2
- [ ] API Invoice hoạt động đúng
- [ ] Kiểm tra thanh toán qua Invoice

---

## 📞 **10. Support**

Nếu có vấn đề trong quá trình migration, hãy kiểm tra:
1. Database schema đã cập nhật chưa
2. Services đã được đăng ký trong Program.cs chưa
3. Order Status mapping trong frontend đã đúng chưa
4. API endpoints mới có hoạt động không

**Hệ thống đã được cập nhật hoàn chỉnh theo yêu cầu!** 🎉
