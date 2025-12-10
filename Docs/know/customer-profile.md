## API: GET/PUT /api/customer/me

- **Chức năng**: Xem và cập nhật thông tin cá nhân khách hàng đang đăng nhập.
- **Controller**: `Controllers/CustomerController.cs` – `GetMyProfile()`, `UpdateMyProfile(UpdateCustomerProfileDto request)`.
- **Quyền**:
  - GET: `PERM_READ_CUSTOMER`
  - PUT: `PERM_WRITE_CUSTOMER`

### GET /api/customer/me
1. Lấy `accountId` từ claim `NameIdentifier`.
2. Tìm `Customer` theo `AccountId`; không có → 401.
3. Map sang `CustomerProfileDto` (id, tên, giới tính, DOB, địa chỉ, phone, email, timestamps).
4. Trả `ApiResponse<CustomerProfileDto>` success.

### PUT /api/customer/me
1. Validate body `UpdateCustomerProfileDto`; sai → 400 với lỗi ModelState.
2. Lấy customer từ token (như GET); không có → 401.
3. Cập nhật các trường: `FirstName`, `LastName`, `Gender` (parse enum), `DateOfBirth`, `Address`, `Phone`, `Email` (nếu gửi), `UpdatedAt=UtcNow`.
4. Lưu DB, map lại DTO và trả `ApiResponse` success.

### Lưu ý
- Không cho sửa ngoài các trường nêu trên; không đụng tới quyền/role.
- Cần token hợp lệ và hồ sơ customer đã được tạo trước đó.


