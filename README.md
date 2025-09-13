# BookStore API

API backend cho ứng dụng BookStore với authentication sử dụng JWT và MySQL database.

## Cấu hình

### Database
- MySQL Server: localhost:3309
- Database: datn
- Username: root
- Password: 123456

### Cấu trúc Database

```sql
CREATE TABLE Role (
    role_id INT AUTO_INCREMENT PRIMARY KEY,
    role_name VARCHAR(50) NOT NULL UNIQUE
);

CREATE TABLE Account (
    account_id INT AUTO_INCREMENT PRIMARY KEY,
    email VARCHAR(255) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    role_id INT NOT NULL,
    CONSTRAINT fk_account_role FOREIGN KEY (role_id) REFERENCES Role(role_id)
);
```

### Roles mặc định
1. CUSTOMER (role_id = 1)
2. EMPLOYEE (role_id = 2) 
3. ADMIN (role_id = 3)

## Chạy ứng dụng

```bash
# Restore packages
dotnet restore

# Build project
dotnet build

# Chạy ứng dụng
dotnet run
```

Ứng dụng sẽ chạy tại: https://localhost:7138

## API Endpoints

### Authentication

#### Đăng ký
```
POST /api/auth/register
Content-Type: application/json

{
    "email": "user@example.com",
    "password": "123456",
    "confirmPassword": "123456",
    "roleId": 1
}
```

#### Đăng nhập
```
POST /api/auth/login
Content-Type: application/json

{
    "email": "user@example.com",
    "password": "123456"
}
```

### Test Endpoints

#### Public (không cần authentication)
```
GET /api/test/public
```

#### Protected (cần authentication)
```
GET /api/test/protected
Authorization: Bearer {jwt_token}
```

#### Admin Only (chỉ ADMIN)
```
GET /api/test/admin-only
Authorization: Bearer {admin_jwt_token}
```

#### Staff Only (EMPLOYEE và ADMIN)
```
GET /api/test/staff-only
Authorization: Bearer {staff_jwt_token}
```

## Swagger UI

Truy cập Swagger UI tại: https://localhost:7138/swagger

## Test với file .http

Sử dụng file `test-api.http` để test các endpoints với VS Code REST Client extension.

## Công nghệ sử dụng

- .NET 8
- Entity Framework Core
- MySQL (Pomelo.EntityFrameworkCore.MySql)
- JWT Authentication
- BCrypt.Net (password hashing)
- Swagger/OpenAPI
