## API: GET /api/book

- **Chức năng**: Tìm kiếm + phân trang danh sách sách (trang chủ hoặc trang danh mục).
- **Controller**: `BookController.GetBooks` gọi `_bookService.GetBooksAsync(searchRequest)`.

### Vị trí code
- Controller: `Controllers/BookController.cs` – `GetBooks([FromQuery] BookSearchRequest searchRequest)`, anonymous.
- Service: `Services/BookService.cs` – `GetBooksAsync(BookSearchRequest searchRequest)`.
- DTO: `DTOs/BookDTOs.cs` – `BookSearchRequest`, `BookListResponse`, `BookDto`.
- Hàm giá: `GetCurrentPriceAsync` (dùng cho lọc giá thực tế).

### Tham số query chính (BookSearchRequest)
- `searchTerm`: từ khóa (tên sách/ISBN).
- `categoryId`, `publisherId`: lọc theo danh mục / NXB.
- `minPrice`, `maxPrice`: lọc theo giá hiện tại.
- `minYear`, `maxYear`: lọc năm xuất bản.
- `pageNumber`, `pageSize`: phân trang (mặc định 1, 10).
- `sortBy`: `title|price|year|created`, `sortDirection`: `asc|desc`.

### Luồng xử lý chính
1. Service dựng truy vấn `Books` kèm `Category`, `Publisher`, `AuthorBooks/Author`, `BookPromotions/Promotion`.
2. Áp filter:
   - `searchTerm`: chuẩn hóa qua `NormalizeSearchTerm`, so khớp `Title` hoặc `Isbn`.
   - `categoryId`, `publisherId`: lọc chính xác.
   - `minYear`, `maxYear`: lọc theo năm xuất bản.
3. Sắp xếp theo `sortBy/sortDirection` (mặc định theo tên).
4. Phân trang với `Skip` + `Take`; tính `totalCount`, `totalPages`.
5. Với từng sách:
   - Tính `CurrentPrice` từ `PriceChanges` (giá mới nhất theo thời gian) hoặc `AveragePrice`.
   - Nếu có khuyến mãi đang hiệu lực, lấy mức giảm cao nhất để tính `DiscountedPrice`.
   - Áp filter giá sau khi có `CurrentPrice` (minPrice/maxPrice).
6. Trả về `ApiResponse<BookListResponse>` gồm danh sách, tổng số dòng, số trang, trang hiện tại.

### Giải thích code (service)
- `Include` dữ liệu liên quan để dựng DTO đầy đủ.
- Bộ lọc theo `searchTerm` dùng `EF.Functions.Like` trên `Title` và `Isbn` (đã normalize).
- `SortBy` switch-case: `title`, `price`, `year`, `created`; mặc định theo `Title` tăng.
- Lấy `totalCount` trước khi `Skip/Take` để tính `TotalPages`.
- Sau khi fetch:
  - Vòng lặp tính `CurrentPrice`; nếu ngoài khoảng `minPrice/maxPrice` thì bỏ qua (lọc sau khi biết giá thực).
  - Tính `discountedPrice` nếu có khuyến mãi hiệu lực (theo ngày UTC).
  - Map sang `BookDto` (giá, danh mục, NXB, tác giả, trạng thái, tồn kho, khuyến mãi).
- Trả `ApiResponse` với `Success=true` hoặc `Success=false` nếu exception, kèm `Errors`.

### Dữ liệu trả về (BookDto)
- Thông tin sách, giá hiện tại, giá giảm (nếu có), tồn kho, trạng thái.
- Danh mục, NXB, tác giả, danh sách khuyến mãi đang hoạt động.

### Mã liên quan
- `Controllers/BookController.cs` – action `GetBooks`.
- `Services/BookService.cs` – method `GetBooksAsync`.
- DTOs: `DTOs/BookDTOs.cs` – `BookDto`, `BookListResponse`, `BookSearchRequest`.

