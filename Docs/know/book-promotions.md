## API: GET /api/book/promotions

- **Chức năng**: Lấy danh sách sách đang có khuyến mãi còn hiệu lực.
- **Controller**: `BookController.GetBooksWithPromotion` gọi `_bookService.GetBooksWithPromotionAsync(limit)`.

### Vị trí code
- Controller: `Controllers/BookController.cs` – `GetBooksWithPromotion([FromQuery] int limit = 10)`, anonymous.
- Service: `Services/BookService.cs` – `GetBooksWithPromotionAsync(int limit = 10)`.
- Hàm giá: dùng `GetCurrentPriceAsync`.

### Luồng xử lý chính
1. Nhận query `limit` (mặc định 10).
2. Service truy vấn `Books` kèm `Category`, `Publisher`, `AuthorBooks/Author`, `BookPromotions/Promotion`.
3. Lọc sách còn bán (`Stock > 0`, `Status == true`) và có khuyến mãi đang hiệu lực (`StartDate <= hôm nay <= EndDate`).
4. Sắp xếp theo mức giảm cao nhất (`BookPromotions.Max(DiscountPct)`), lấy `limit`.
5. Tính `CurrentPrice` từ bảng `PriceChanges` (hoặc `AveragePrice` nếu chưa đổi giá).
6. Tính `DiscountedPrice` theo phần trăm giảm cao nhất.
7. Trả về `ApiResponse<List<BookDto>>` đã kèm `ActivePromotions`.

### Giải thích code (service)
- EF `Include` các liên quan; `Where` lọc còn hàng + đang bán + có promo hiệu lực theo ngày UTC.
- `OrderByDescending(b => b.BookPromotions.Max(bp => bp.Promotion.DiscountPct)).Take(limit)` ưu tiên sách giảm sâu nhất.
- Mỗi sách:
  - `currentPrice = GetCurrentPriceAsync(isbn)`.
  - `maxDiscount = bookData.Promotions.Max(p => p.DiscountPct)`, `discountedPrice = currentPrice * (1 - maxDiscount/100)`.
  - Map `BookDto` với giá, danh mục, NXB, tác giả, trạng thái, tồn kho, `ActivePromotions`.
- Trả `ApiResponse` thành công hoặc lỗi (catch exception).

### Dữ liệu trả về (BookDto)
- Giá hiện tại, giá đã giảm, tồn kho, trạng thái.
- Thông tin danh mục, nhà XB, tác giả, danh sách khuyến mãi đang hoạt động.

### Mã liên quan
- `Controllers/BookController.cs` – action `GetBooksWithPromotion`.
- `Services/BookService.cs` – method `GetBooksWithPromotionAsync`.

