## API đổi mật khẩu

Hiện tại trong mã nguồn **chưa có** endpoint đổi mật khẩu (không thấy trong `Controllers/AuthController.cs` hoặc các controller khác). Auth chỉ có:
- `POST /api/auth/register`
- `POST /api/auth/login`
- `POST /api/auth/refresh-token`
- OAuth Google (`/api/auth/google`, `/api/auth/google/callback`)

### Nếu cần bổ sung
- Thêm DTO: `ChangePasswordDto { CurrentPassword, NewPassword, ConfirmPassword }`.
- Controller (Auth): `[Authorize] POST /api/auth/change-password`:
  1) Lấy account từ token (email/nameidentifier).
  2) Kiểm tra mật khẩu hiện tại (BCrypt verify `PasswordHash`).
  3) Hash mật khẩu mới, lưu `PasswordHash`.
  4) Trả `ApiResponse` success/fail.
- Service: thêm hàm `ChangePasswordAsync`.

Hiện chưa có logic này, nên frontend không thể gọi đổi mật khẩu qua API sẵn có.


