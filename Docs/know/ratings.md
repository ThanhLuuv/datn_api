## APIs: /api/ratings

- **Controller**: `Controllers/RatingsController.cs`.
- **Các endpoint**:
  - `GET /api/ratings/{isbn}` (anonymous): danh sách đánh giá theo ISBN, phân trang.
  - `GET /api/ratings/{isbn}/stats` (anonymous): thống kê tổng số, trung bình sao, histogram.
  - `POST /api/ratings` (Authorize): tạo/cập nhật đánh giá của người đã mua.

### GET /api/ratings/{isbn}
1. Nhận `page` (>=1, default 1), `pageSize` (1..50, default 10).
2. Query `Ratings` theo `isbn`, sắp xếp `CreatedAt` desc.
3. Tính `total`, `avgStars` (round 2).
4. Trả `{ success=true, data, total, page, pageSize, avgStars }`.

### GET /api/ratings/{isbn}/stats
1. Lấy tất cả rating theo ISBN.
2. Tính `total`, `avgStars`, `histogram` (group by stars).
3. Trả `{ success=true, total, avgStars, histogram }`.

### POST /api/ratings
1. Yêu cầu đăng nhập (có email trong claim).
2. Body: `{ isbn, stars (1..5), comment? }`. Nếu stars ngoài 1..5 → 400.
3. Map email -> `customerId` (join Account -> Customer); nếu không có → 403.
4. Kiểm tra đã mua và giao thành công: tồn tại `OrderLine` có ISBN và `Order.Status == Delivered` của customer. Không thỏa → 403.
5. Nếu chưa có rating: tạo mới với `CreatedAt=UtcNow`.
6. Nếu đã có: cập nhật `Stars`, `Comment`, `UpdatedAt=UtcNow`.
7. Lưu DB, trả `{ success=true }`.

### Lưu ý
- Mỗi customer tối đa 1 đánh giá/ISBN; POST sẽ upsert.
- Chỉ khách đã mua và giao thành công mới được đánh giá (chặn review ảo).


