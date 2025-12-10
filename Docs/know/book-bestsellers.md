## API: GET /api/book/bestsellers

- **Chức năng**: Lấy danh sách sách bán chạy nhất.
- **Controller**: `BookController.GetBestSellingBooks` gọi `_bookService.GetBestSellingBooksAsync(limit)`.

### Vị trí code
- Controller: `Controllers/BookController.cs` – `GetBestSellingBooks([FromQuery] int limit = 10)`, anonymous.
- Service: `Services/BookService.cs` – `GetBestSellingBooksAsync(int limit = 10)`.
- Giá hiện tại dùng hàm chung `GetCurrentPriceAsync`.

### Luồng xử lý chính
1. Nhận query `limit` (mặc định 10) để giới hạn số sách.
2. Service truy vấn `Books` kèm `Category`, `Publisher`, `AuthorBooks/Author`, `BookPromotions/Promotion`, `OrderLines`.
3. Lọc chỉ sách còn bán (`Stock > 0`, `Status == true`) và có đơn hàng (`OrderLines.Any()`).
4. Sắp xếp giảm dần theo tổng số lượng đã bán `OrderLines.Sum(Qty)`, lấy `limit`.
5. Tính `CurrentPrice` dựa trên bảng `PriceChanges` hoặc `AveragePrice`.
6. Nếu có khuyến mãi đang hiệu lực, tính `DiscountedPrice` theo mức giảm cao nhất.
7. Trả về `ApiResponse<List<BookDto>>` chứa trường `TotalSold`.

### Giải thích code (service)
- Truy vấn EF có `Include` cho category/publisher/authors/promotions, và `OrderLines` để tính `TotalSold`.
- `Where(b => b.Stock > 0 && b.Status == true && b.OrderLines.Any())` đảm bảo đang bán và đã có đơn.
- `OrderByDescending(b => b.OrderLines.Sum(ol => ol.Qty)).Take(limit)` chọn top bán chạy.
- Mỗi sách:
  - `currentPrice = GetCurrentPriceAsync(isbn)` lấy giá hiện tại theo lịch sử.
  - Nếu có promo hiệu lực, lấy `maxDiscount`, tính `discountedPrice`.
  - Map vào `BookDto` kèm `TotalSold`, giá, danh mục, NXB, tác giả, khuyến mãi.
- Bọc kết quả trong `ApiResponse`; catch exception trả `Success=false`.

### Dữ liệu trả về (BookDto)
- Giá hiện tại, giá giảm (nếu có), tồn kho, trạng thái, tổng đã bán.
- Thông tin danh mục, nhà XB, tác giả, danh sách khuyến mãi đang hoạt động.

### Mã liên quan
- `Controllers/BookController.cs` – action `GetBestSellingBooks`.
- `Services/BookService.cs` – method `GetBestSellingBooksAsync`.

