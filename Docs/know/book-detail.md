## API: GET /api/book/{isbn}

- **Chức năng**: Lấy thông tin chi tiết một sách theo ISBN.
- **Controller**: `Controllers/BookController.cs` – `GetBook(string isbn)` (AllowAnonymous).
- **Service**: `Services/BookService.cs` – `GetBookByIsbnAsync(string isbn)`.

### Luồng xử lý
1. Controller nhận `{isbn}`, gọi service.
2. Service truy vấn `Books` kèm `Category`, `Publisher`, `AuthorBooks/Author`.
3. Nếu không tìm thấy → `Success=false`, `Message="Book not found"`, HTTP 404.
4. Tính `CurrentPrice` (giá mới nhất từ `PriceChanges` nếu có, else `AveragePrice`).
5. Map `BookDto`: thông tin sách, danh mục, NXB, tác giả, tồn kho, trạng thái, timestamps, giá hiện tại.
6. Trả `ApiResponse<BookDto>` thành công; lỗi → `Success=false` với `Errors`.

### Ghi chú
- Endpoint không trả khuyến mãi trong chi tiết (khác với các danh sách có `ActivePromotions`). Nếu cần khuyến mãi, phải mở rộng service hoặc dùng API promotions.
- Không yêu cầu đăng nhập.





