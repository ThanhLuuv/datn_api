-- ============================================
-- Script reset và tạo lại toàn bộ permissions và role_permission
-- GIỮ NGUYÊN bảng role hiện tại (không thay đổi)
-- Role IDs trong database:
-- role_id = 1: CUSTOMER
-- role_id = 2: SALES_EMPLOYEE
-- role_id = 3: ADMIN
-- role_id = 4: DELIVERY_EMPLOYEE
-- ============================================

-- Bước 1: Xóa bảng role_permission (DROP và tạo lại từ đầu)
DROP TABLE IF EXISTS `role_permission`;

-- Bước 2: Tạo lại bảng role_permission
CREATE TABLE `role_permission` (
  `role_id` BIGINT NOT NULL,
  `permission_id` BIGINT NOT NULL,
  PRIMARY KEY (`role_id`, `permission_id`),
  CONSTRAINT `fk_role_permission_role` FOREIGN KEY (`role_id`) REFERENCES `role` (`role_id`) ON DELETE CASCADE,
  CONSTRAINT `fk_role_permission_permission` FOREIGN KEY (`permission_id`) REFERENCES `permission` (`permission_id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Bước 3: Xóa và tạo lại bảng permission (đảm bảo có đủ 42 permissions)
DROP TABLE IF EXISTS `permission`;

CREATE TABLE `permission` (
  `permission_id` BIGINT NOT NULL AUTO_INCREMENT,
  `code` VARCHAR(100) NOT NULL,
  `name` VARCHAR(150) NOT NULL,
  `description` VARCHAR(300) DEFAULT NULL,
  PRIMARY KEY (`permission_id`),
  UNIQUE KEY `code` (`code`)
) ENGINE=InnoDB AUTO_INCREMENT=43 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- Bước 4: Insert tất cả 42 permissions
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
(42, 'ASSIGN_PERMISSION', 'Gán quyền', 'Gán quyền cho vai trò');

-- Bước 5: Insert role_permission cho từng role

-- 5.1. ADMIN (RoleId = 3) - Có TẤT CẢ 42 permissions
INSERT INTO `role_permission` (`role_id`, `permission_id`) VALUES
(3, 1), (3, 2), (3, 3), (3, 4), (3, 5), (3, 6), (3, 7), (3, 8), (3, 9), (3, 10),
(3, 11), (3, 12), (3, 13), (3, 14), (3, 15), (3, 16), (3, 17), (3, 18), (3, 19), (3, 20),
(3, 21), (3, 22), (3, 23), (3, 24), (3, 25), (3, 26), (3, 27), (3, 28), (3, 29), (3, 30),
(3, 31), (3, 32), (3, 33), (3, 34), (3, 35), (3, 36), (3, 37), (3, 38), (3, 39), (3, 40),
(3, 41), (3, 42);

-- 5.2. SALES_EMPLOYEE (RoleId = 2) - Có tất cả quyền như ADMIN trừ:
--     - READ_EMPLOYEE (34), WRITE_EMPLOYEE (35)
--     - READ_DEPARTMENT (36), WRITE_DEPARTMENT (37)
--     - READ_ROLE (40), WRITE_ROLE (41), ASSIGN_PERMISSION (42)
INSERT INTO `role_permission` (`role_id`, `permission_id`) VALUES
-- Tất cả permissions từ 1 đến 42, trừ 34, 35, 36, 37, 40, 41, 42
(2, 1), (2, 2), (2, 3), (2, 4), (2, 5), (2, 6), (2, 7), (2, 8), (2, 9), (2, 10),
(2, 11), (2, 12), (2, 13), (2, 14), (2, 15), (2, 16), (2, 17), (2, 18), (2, 19), (2, 20),
(2, 21), (2, 22), (2, 23), (2, 24), (2, 25), (2, 26), (2, 27), (2, 28), (2, 29), (2, 30),
(2, 31), (2, 32), (2, 33), (2, 38), (2, 39);
-- Không có: 34 (READ_EMPLOYEE), 35 (WRITE_EMPLOYEE), 36 (READ_DEPARTMENT), 37 (WRITE_DEPARTMENT), 40 (READ_ROLE), 41 (WRITE_ROLE), 42 (ASSIGN_PERMISSION)

-- 5.3. DELIVERY_EMPLOYEE (RoleId = 4) - Nhân viên giao hàng (giữ nguyên quyền hiện tại)
INSERT INTO `role_permission` (`role_id`, `permission_id`) VALUES
-- Đọc: Category, Book, Order, Customer, Area
(4, 1),  -- READ_CATEGORY
(4, 3),  -- READ_BOOK
(4, 13), -- READ_ORDER
(4, 17), -- READ_CUSTOMER
(4, 38), -- READ_AREA
-- Ghi: Order (chỉ cập nhật trạng thái giao hàng), ASSIGN_DELIVERY
(4, 14), -- WRITE_ORDER (chỉ để cập nhật trạng thái)
(4, 16); -- ASSIGN_DELIVERY

-- 5.4. CUSTOMER (RoleId = 1) - Khách hàng
INSERT INTO `role_permission` (`role_id`, `permission_id`) VALUES
-- Đọc: Category, Book, Publisher, Author, Promotion (public)
(1, 1),  -- READ_CATEGORY
(1, 3),  -- READ_BOOK
(1, 5),  -- READ_PUBLISHER
(1, 7),  -- READ_AUTHOR
(1, 25), -- READ_PROMOTION
-- Ghi: Cart (của chính mình), Order (tạo đơn của chính mình), Payment (thanh toán đơn của chính mình)
(1, 19), -- READ_CART
(1, 20), -- WRITE_CART
(1, 13), -- READ_ORDER (chỉ đơn của mình)
(1, 14), -- WRITE_ORDER (tạo đơn)
(1, 21), -- READ_PAYMENT
(1, 22); -- WRITE_PAYMENT

-- Bước 6: Kiểm tra kết quả
SELECT 
    r.role_id,
    r.name AS role_name,
    COUNT(rp.permission_id) AS permission_count,
    GROUP_CONCAT(p.code ORDER BY p.permission_id SEPARATOR ', ') AS permissions
FROM `role` r
LEFT JOIN `role_permission` rp ON r.role_id = rp.role_id
LEFT JOIN `permission` p ON rp.permission_id = p.permission_id
GROUP BY r.role_id, r.name
ORDER BY r.role_id;

-- ============================================
-- Kết quả mong đợi:
-- role_id = 1 (CUSTOMER): 11 permissions
-- role_id = 2 (SALES_EMPLOYEE): 35 permissions (tất cả trừ READ_EMPLOYEE, WRITE_EMPLOYEE, READ_DEPARTMENT, WRITE_DEPARTMENT, READ_ROLE, WRITE_ROLE, ASSIGN_PERMISSION)
-- role_id = 3 (ADMIN): 42 permissions
-- role_id = 4 (DELIVERY_EMPLOYEE): 7 permissions
-- ============================================


