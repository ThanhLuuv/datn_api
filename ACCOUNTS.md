### Test Accounts and Usage

- **Admin account**
  - Email: `admin.test@example.com`
  - Password: `Admin@123`
  - Role: `ADMIN`
  - Obtain JWT:
    ```bash
    curl -s -X POST http://localhost:5256/api/auth/login \
      -H "Content-Type: application/json" \
      -d '{"email":"admin.test@example.com","password":"Admin@123"}'
    ```
  - Use JWT (example):
    ```bash
    curl -s -H "Authorization: Bearer YOUR_JWT" \
      "http://localhost:5256/api/admin/price-changes?isbn=9786040000001&page=1&pageSize=5"
    ```

- **Endpoints commonly used with Admin**
  - PriceChange Admin APIs
    - List: `GET /api/admin/price-changes?isbn=&page=&pageSize=`
    - Create: `POST /api/admin/price-changes`
      ```bash
      curl -s -X POST -H "Authorization: Bearer YOUR_JWT" -H "Content-Type: application/json" \
        -d '{"isbn":"9786040000001","oldPrice":110000,"newPrice":105000,"employeeId":1}' \
        http://localhost:5256/api/admin/price-changes
      ```
    - Update: `PUT /api/admin/price-changes/{isbn}/{changedAt}`
    - Delete: `DELETE /api/admin/price-changes/{isbn}/{changedAt}`

- **Customer account (create as needed)**
  - Register:
    ```bash
    curl -s -X POST http://localhost:5256/api/auth/register -H "Content-Type: application/json" \
      -d '{"email":"user.test@example.com","password":"User@123","confirmPassword":"User@123"}'
    ```
  - Login to get JWT:
    ```bash
    curl -s -X POST http://localhost:5256/api/auth/login -H "Content-Type: application/json" \
      -d '{"email":"user.test@example.com","password":"User@123"}'
    ```
  - Current token (example):
    eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ1c2VyLnRlc3RAZXhhbXBsZS5jb20iLCJlbWFpbCI6InVzZXIudGVzdEBleGFtcGxlLmNvbSIsImh0dHA6Ly9zY2hlbWFzLm1pY3Jvc29mdC5jb20vd3MvMjAwOC8wNi9pZGVudGl0eS9jbGFpbXMvcm9sZSI6IkNVU1RPTUVSIiwianRpIjoiNmY3NGQ0MzQtMTNkMi00NDVkLTk3MWEtOTgwN2U4ZDYzMTE1IiwiaWF0IjoxNzU4NjA5OTQxLCJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6IjMiLCJleHAiOjE3NTkyMTQ3NDEsImlzcyI6IkJvb2tTdG9yZUFwaSIsImF1ZCI6IkJvb2tTdG9yZUFwaVVzZXJzIn0.ti7wqLfB0B9DKVvcQGTgzOERI6FAy6hc6G-V1ko7L7I

- **Public endpoints (no auth)**
  - Bestsellers: `GET /api/storefront/bestsellers?days=30&top=10`
  - New books: `GET /api/storefront/new-books?days=30&top=10`
  - Search: `GET /api/storefront/search?title=abc&page=1&pageSize=12`
  - Price history: `GET /api/storefront/price-history/{isbn}`

- **Where used (FE)**
  - Login/Logout pages use Auth APIs.
  - Home/Search use Storefront APIs (bán chạy, sách mới, tìm kiếm, price history hiển thị giá).
  - Admin pages (khi triển khai) dùng nhóm API `/api/admin/price-changes`.

Note: JWT expires periodically; re-login to refresh token.


