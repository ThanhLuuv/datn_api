## Tài liệu Stored Procedures: Báo cáo tồn kho và Giá nhập bình quân

Tệp này giải thích chi tiết 2 thủ tục đã được khai báo trong `inventory-procedures.sql` để phục vụ báo cáo tồn kho theo ngày và cập nhật giá nhập bình quân cho sách.

### 1) SP_InventoryReport_AsOfDate(IN p_ReportDate DATE)

- Mục đích: Trả về tồn kho của từng sách tại thời điểm ngày báo cáo p_ReportDate dựa trên số tồn hiện tại (`book.stock`) và “quay lùi” theo các phát sinh sau ngày báo cáo.
- Đầu vào: `p_ReportDate` (DATE) — ngày cắt sổ báo cáo.
- Đầu ra: Một tập kết quả gồm các cột: `Category`, `ISBN`, `Title`, `QuantityOnHand`, `AveragePrice`.

#### Bảng/tổ chức dữ liệu liên quan
- `book(stock, average_price, category_id, status, ...)`
- `category(name, ...)`
- `goods_receipt(gr_id, po_id, received_at, ...)`
- `goods_receipt_line(gr_line_id, gr_id, qty_received, unit_cost)`
- `purchase_order_line(po_line_id, po_id, isbn, qty_ordered, unit_price)`
- `invoice(invoice_id, order_id, payment_status, paid_at, ...)`
- `order(order_id, ...)`
- `order_line(order_line_id, order_id, isbn, qty, unit_price)`

#### Logic tính toán (tóm tắt)
1. Ánh xạ dòng hàng của phiếu nhập `goods_receipt_line` sang `isbn` thông qua thứ tự dòng trong cùng một Đơn mua (PO) và Phiếu nhập (GR):
   - Vì `goods_receipt_line` không lưu `isbn`, ta ghép theo thứ tự (ROW_NUMBER) giữa các dòng PO (`purchase_order_line`) và dòng GR (`goods_receipt_line`) trong cùng một `po_id`/`gr_id`.
2. Tính tổng số lượng nhập sau ngày báo cáo (>= p_ReportDate) theo `isbn`.
3. Tính tổng số lượng bán sau ngày báo cáo (>= p_ReportDate) theo `isbn` chỉ tính các hóa đơn đã thanh toán (`invoice.payment_status = 'PAID'`).
4. “Quay lùi” tồn từ hiện tại về ngày báo cáo theo công thức:
   - `QuantityOnHand` = `book.stock`
     − `ReceiptsAfterReportDate` + `SalesAfterReportDate`.
   - Lý do: tồn hiện tại đã bao gồm nhập và xuất đến thời điểm hiện tại; để có tồn tại ngày p_ReportDate, cần trừ đi nhập phát sinh sau ngày đó và cộng lại phần bán phát sinh sau ngày đó.
5. Lọc chỉ sách đang hoạt động (`book.status = TRUE`) và không trả các dòng âm.

#### Giả định/ghi chú quan trọng
- Hệ thống hiện tại tăng tồn kho khi tạo Phiếu nhập, và giảm tồn kho khi hóa đơn được thanh toán (PaymentService). Các nghiệp vụ Khách trả hàng hay Hủy đơn chưa làm thay đổi tồn trong code hiện có, vì vậy chưa đưa vào phép “quay lùi”.
- Việc ghép `goods_receipt_line` với `purchase_order_line` theo thứ tự dòng là nhất quán với cách Service đang bù tồn (duyệt theo thứ tự dòng trong PO/GR).
- Nếu sau này bổ sung cập nhật tồn khi Duyệt trả hàng (Return.Approved) hay khi Hủy đơn, cần mở rộng thủ tục để trừ/cộng tương ứng.

#### Cách chạy
1. Nạp thủ tục:
   - `SOURCE inventory-procedures.sql;`
2. Gọi thủ tục:
```sql
CALL SP_InventoryReport_AsOfDate('2025-10-01');
```

---

### 2) SP_UpdateAveragePrice_Last4Receipts(IN p_Isbn VARCHAR(20))

- Mục đích: Tính giá nhập bình quân gia quyền dựa trên 4 Phiếu nhập gần nhất có chứa sách `p_Isbn`, sau đó cập nhật vào `book.average_price`.
- Đầu vào: `p_Isbn` — mã ISBN của sách.
- Đầu ra: Một hàng trả về cột `AveragePrice` để tiện log/kiểm tra.

#### Logic tính toán
1. Ánh xạ dòng GR sang `isbn` tương tự như thủ tục trên (ROW_NUMBER theo GR và PO) nhưng lọc theo `pol.isbn = p_Isbn`.
2. Lấy ra 4 `gr_id` gần nhất (ORDER BY `received_at` DESC) có phát sinh với `p_Isbn`.
3. Tính tổng tiền và tổng số lượng của các dòng nhập thuộc 4 GR đó và của riêng `p_Isbn`:
   - `sum_amount = SUM(qty_received * unit_cost)`
   - `sum_qty = SUM(qty_received)`
4. Tính `avg_price = ROUND(sum_amount / NULLIF(sum_qty, 0), 0)`; nếu không có dữ liệu, trả 0.
5. Cập nhật `book.average_price` và `book.updated_at` cho `isbn` tương ứng, đồng thời trả về `AveragePrice`.

#### Giả định/ghi chú
- Dữ liệu giá nhập được lưu ở `goods_receipt_line.unit_cost`.
- Nếu một Phiếu nhập chứa nhiều sách, chỉ các dòng được ánh xạ theo `isbn` mới được tính.
- Nếu muốn dùng nhiều hơn 4 lần nhập gần nhất, có thể thay đổi `LIMIT 4`.

#### Cách chạy
```sql
CALL SP_UpdateAveragePrice_Last4Receipts('9786041234567');
```

---

### Cách triển khai/rollback
- Triển khai: chạy `SOURCE inventory-procedures.sql;`
- Xóa thủ tục:
```sql
DROP PROCEDURE IF EXISTS SP_InventoryReport_AsOfDate;
DROP PROCEDURE IF EXISTS SP_UpdateAveragePrice_Last4Receipts;
```

### Mở rộng trong tương lai
- Bổ sung ảnh hưởng của Trả hàng (Return) vào tồn kho: khi `Return.Status = Approved` có thể cộng lại `qty_returned` vào `book.stock`, đồng thời thủ tục báo cáo cần trừ phần “trả sau ngày báo cáo”.
- Tính tồn theo sổ chi tiết (xuất/nhập) thay vì “quay lùi” từ `book.stock` để độc lập với tồn hiện tại.
- Ghi nhận `isbn` trực tiếp trên `goods_receipt_line` để bỏ qua bước ánh xạ theo thứ tự dòng.




