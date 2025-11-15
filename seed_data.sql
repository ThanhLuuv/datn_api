-- ============================================
-- Seed Data cho BookStore Database
-- Chạy file này để seed permissions và role_permissions
-- LƯU Ý: Roles đã tồn tại trong database, không cần tạo lại
-- ============================================

-- 1. Seed Roles (Vai trò) - BỎ QUA vì roles đã tồn tại
-- Nếu cần tạo roles mới, uncomment phần dưới:
-- INSERT IGNORE INTO `role` (`role_id`, `name`, `description`) VALUES
-- (1, 'CUSTOMER', 'Khách hàng'),
-- (2, 'SALES_EMPLOYEE', 'Nhân viên bán hàng'),
-- (3, 'ADMIN', 'Quản trị viên'),
-- (4, 'DELIVERY_EMPLOYEE', 'Nhân viên giao hàng');

-- 2. Seed Permissions (Quyền)
INSERT INTO `permission` (`permission_id`, `code`, `name`, `description`) VALUES
-- Category permissions
(1, 'READ_CATEGORY', 'Đọc danh mục', 'Xem danh sách danh mục'),
(2, 'WRITE_CATEGORY', 'Ghi danh mục', 'Tạo, sửa, xóa danh mục'),

-- Book permissions
(3, 'READ_BOOK', 'Đọc sách', 'Xem danh sách sách'),
(4, 'WRITE_BOOK', 'Ghi sách', 'Tạo, sửa, xóa sách'),

-- Publisher permissions
(5, 'READ_PUBLISHER', 'Đọc nhà xuất bản', 'Xem danh sách nhà xuất bản'),
(6, 'WRITE_PUBLISHER', 'Ghi nhà xuất bản', 'Tạo, sửa, xóa nhà xuất bản'),

-- Author permissions
(7, 'READ_AUTHOR', 'Đọc tác giả', 'Xem danh sách tác giả'),
(8, 'WRITE_AUTHOR', 'Ghi tác giả', 'Tạo, sửa, xóa tác giả'),

-- Purchase Order permissions
(9, 'READ_PURCHASE_ORDER', 'Đọc đơn đặt mua', 'Xem danh sách đơn đặt mua'),
(10, 'WRITE_PURCHASE_ORDER', 'Ghi đơn đặt mua', 'Tạo, sửa, xóa đơn đặt mua'),

-- Goods Receipt permissions
(11, 'READ_GOODS_RECEIPT', 'Đọc phiếu nhập', 'Xem danh sách phiếu nhập'),
(12, 'WRITE_GOODS_RECEIPT', 'Ghi phiếu nhập', 'Tạo, sửa, xóa phiếu nhập'),

-- Order permissions
(13, 'READ_ORDER', 'Đọc đơn hàng', 'Xem danh sách đơn hàng'),
(14, 'WRITE_ORDER', 'Ghi đơn hàng', 'Tạo, sửa đơn hàng'),
(15, 'APPROVE_ORDER', 'Duyệt đơn hàng', 'Duyệt/hủy đơn hàng'),
(16, 'ASSIGN_DELIVERY', 'Gán giao hàng', 'Gán nhân viên giao hàng cho đơn hàng'),

-- Customer permissions
(17, 'READ_CUSTOMER', 'Đọc khách hàng', 'Xem danh sách khách hàng'),
(18, 'WRITE_CUSTOMER', 'Ghi khách hàng', 'Tạo, sửa thông tin khách hàng'),

-- Cart permissions
(19, 'READ_CART', 'Đọc giỏ hàng', 'Xem giỏ hàng'),
(20, 'WRITE_CART', 'Ghi giỏ hàng', 'Thêm, sửa, xóa sản phẩm trong giỏ hàng'),

-- Payment permissions
(21, 'READ_PAYMENT', 'Đọc thanh toán', 'Xem lịch sử thanh toán'),
(22, 'WRITE_PAYMENT', 'Ghi thanh toán', 'Tạo liên kết thanh toán'),

-- Invoice permissions
(23, 'READ_INVOICE', 'Đọc hóa đơn', 'Xem danh sách hóa đơn'),
(24, 'WRITE_INVOICE', 'Ghi hóa đơn', 'Tạo, sửa hóa đơn'),

-- Promotion permissions
(25, 'READ_PROMOTION', 'Đọc khuyến mãi', 'Xem danh sách khuyến mãi'),
(26, 'WRITE_PROMOTION', 'Ghi khuyến mãi', 'Tạo, sửa, xóa khuyến mãi'),

-- Return permissions
(27, 'READ_RETURN', 'Đọc trả hàng', 'Xem danh sách đơn trả hàng'),
(28, 'WRITE_RETURN', 'Ghi trả hàng', 'Tạo, xử lý đơn trả hàng'),

-- Price Change permissions
(29, 'READ_PRICE_CHANGE', 'Đọc thay đổi giá', 'Xem lịch sử thay đổi giá'),
(30, 'WRITE_PRICE_CHANGE', 'Ghi thay đổi giá', 'Tạo thay đổi giá sách'),

-- Expense permissions
(31, 'READ_EXPENSE', 'Đọc chi phí', 'Xem danh sách chi phí'),
(32, 'WRITE_EXPENSE', 'Ghi chi phí', 'Tạo, sửa, xóa chi phí'),

-- Report permissions
(33, 'READ_REPORT', 'Đọc báo cáo', 'Xem các báo cáo'),

-- Employee permissions
(34, 'READ_EMPLOYEE', 'Đọc nhân viên', 'Xem danh sách nhân viên'),
(35, 'WRITE_EMPLOYEE', 'Ghi nhân viên', 'Tạo, sửa, xóa nhân viên'),

-- Department permissions
(36, 'READ_DEPARTMENT', 'Đọc phòng ban', 'Xem danh sách phòng ban'),
(37, 'WRITE_DEPARTMENT', 'Ghi phòng ban', 'Tạo, sửa, xóa phòng ban'),

-- Area permissions
(38, 'READ_AREA', 'Đọc khu vực', 'Xem danh sách khu vực'),
(39, 'WRITE_AREA', 'Ghi khu vực', 'Tạo, sửa, xóa khu vực'),

-- Role & Permission permissions
(40, 'READ_ROLE', 'Đọc vai trò', 'Xem danh sách vai trò'),
(41, 'WRITE_ROLE', 'Ghi vai trò', 'Tạo, sửa vai trò'),
(42, 'ASSIGN_PERMISSION', 'Gán quyền', 'Gán quyền cho vai trò')
ON DUPLICATE KEY UPDATE `code` = VALUES(`code`), `name` = VALUES(`name`), `description` = VALUES(`description`);

-- 3. Seed Role Permissions (Phân quyền cho từng role)
-- LƯU Ý: Role IDs sau khi chạy fix_role_ids.sql:
-- role_id = 1: ADMIN
-- role_id = 2: SALES_EMPLOYEE
-- role_id = 3: DELIVERY_EMPLOYEE
-- role_id = 4: CUSTOMER

-- 3.1. ADMIN (RoleId = 1) - Có TẤT CẢ 42 permissions
INSERT INTO `role_permission` (`role_id`, `permission_id`) VALUES
-- ADMIN có tất cả permissions từ 1 đến 42
(1, 1), (1, 2), (1, 3), (1, 4), (1, 5), (1, 6), (1, 7), (1, 8), (1, 9), (1, 10),
(1, 11), (1, 12), (1, 13), (1, 14), (1, 15), (1, 16), (1, 17), (1, 18), (1, 19), (1, 20),
(1, 21), (1, 22), (1, 23), (1, 24), (1, 25), (1, 26), (1, 27), (1, 28), (1, 29), (1, 30),
(1, 31), (1, 32), (1, 33), (1, 34), (1, 35), (1, 36), (1, 37), (1, 38), (1, 39), (1, 40),
(1, 41), (1, 42)
ON DUPLICATE KEY UPDATE `role_id` = VALUES(`role_id`), `permission_id` = VALUES(`permission_id`);

-- 3.2. SALES_EMPLOYEE (RoleId = 2) - Nhân viên bán hàng
INSERT INTO `role_permission` (`role_id`, `permission_id`) VALUES
-- Đọc: Category, Book, Publisher, Author, Purchase Order, Goods Receipt, Order, Customer, Cart, Payment, Invoice, Promotion, Return, Price Change, Report, Area
(2, 1),  -- READ_CATEGORY
(2, 3),  -- READ_BOOK
(2, 5),  -- READ_PUBLISHER
(2, 7),  -- READ_AUTHOR
(2, 9),  -- READ_PURCHASE_ORDER
(2, 11), -- READ_GOODS_RECEIPT
(2, 13), -- READ_ORDER
(2, 17), -- READ_CUSTOMER
(2, 19), -- READ_CART
(2, 21), -- READ_PAYMENT
(2, 23), -- READ_INVOICE
(2, 25), -- READ_PROMOTION
(2, 27), -- READ_RETURN
(2, 29), -- READ_PRICE_CHANGE
(2, 33), -- READ_REPORT
(2, 38), -- READ_AREA
-- Ghi: Order, Cart, Payment, Invoice, Promotion, Return, Price Change
(2, 14), -- WRITE_ORDER
(2, 15), -- APPROVE_ORDER
(2, 20), -- WRITE_CART
(2, 22), -- WRITE_PAYMENT
(2, 24), -- WRITE_INVOICE
(2, 26), -- WRITE_PROMOTION
(2, 28), -- WRITE_RETURN
(2, 30)  -- WRITE_PRICE_CHANGE
ON DUPLICATE KEY UPDATE `role_id` = VALUES(`role_id`), `permission_id` = VALUES(`permission_id`);

-- 3.3. DELIVERY_EMPLOYEE (RoleId = 3) - Nhân viên giao hàng
INSERT INTO `role_permission` (`role_id`, `permission_id`) VALUES
-- Đọc: Category, Book, Order, Customer, Area
(3, 1),  -- READ_CATEGORY
(3, 3),  -- READ_BOOK
(3, 13), -- READ_ORDER
(3, 17), -- READ_CUSTOMER
(3, 38), -- READ_AREA
-- Ghi: Order (chỉ cập nhật trạng thái giao hàng), ASSIGN_DELIVERY
(3, 14), -- WRITE_ORDER (chỉ để cập nhật trạng thái)
(3, 16)  -- ASSIGN_DELIVERY
ON DUPLICATE KEY UPDATE `role_id` = VALUES(`role_id`), `permission_id` = VALUES(`permission_id`);

-- 3.4. CUSTOMER (RoleId = 4) - Khách hàng
INSERT INTO `role_permission` (`role_id`, `permission_id`) VALUES
-- Đọc: Category, Book, Publisher, Author, Promotion (public)
(4, 1),  -- READ_CATEGORY
(4, 3),  -- READ_BOOK
(4, 5),  -- READ_PUBLISHER
(4, 7),  -- READ_AUTHOR
(4, 25), -- READ_PROMOTION
-- Ghi: Cart (của chính mình), Order (tạo đơn của chính mình), Payment (thanh toán đơn của chính mình)
(4, 19), -- READ_CART
(4, 20), -- WRITE_CART
(4, 13), -- READ_ORDER (chỉ đơn của mình)
(4, 14), -- WRITE_ORDER (tạo đơn)
(4, 21), -- READ_PAYMENT
(4, 22)  -- WRITE_PAYMENT
ON DUPLICATE KEY UPDATE `role_id` = VALUES(`role_id`), `permission_id` = VALUES(`permission_id`);

-- ============================================
-- Kết thúc seed data
-- LƯU Ý: Chạy fix_role_ids.sql TRƯỚC khi chạy file này!
-- Sau khi chạy file này, đảm bảo:
-- 1. ADMIN (role_id = 1) có đầy đủ 42 permissions
-- 2. Các role khác có permissions tương ứng:
--    - SALES_EMPLOYEE (role_id = 2)
--    - DELIVERY_EMPLOYEE (role_id = 3)
--    - CUSTOMER (role_id = 4)
-- 3. Đăng nhập lại để lấy token mới có permissions đầy đủ
-- ============================================

