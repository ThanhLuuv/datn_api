-- Bootstrap Roles, Permissions, RolePermissions, PurchaseOrderStatus, and map accounts to roles
-- MySQL script - safe to run multiple times (uses upsert/ignore)

USE datn;

START TRANSACTION;

-- =====================
-- Roles
-- =====================
INSERT INTO role (role_id, name, description)
VALUES
	(1, 'ADMIN', 'Quản trị viên'),
	(2, 'SALES_EMPLOYEE', 'Nhân viên bán hàng'),
	(3, 'DELIVERY_EMPLOYEE', 'Nhân viên giao hàng'),
	(4, 'CUSTOMER', 'Khách hàng')
AS new(role_id, name, description)
ON DUPLICATE KEY UPDATE
	name = new.name,
	description = new.description;

-- =====================
-- Permissions
-- =====================
INSERT INTO permission (permission_id, code, name, description)
VALUES
	(1,  'READ_CATEGORY',         'Đọc danh mục',           'Xem danh sách danh mục'),
	(2,  'WRITE_CATEGORY',        'Ghi danh mục',           'Tạo, sửa, xóa danh mục'),
	(3,  'READ_BOOK',             'Đọc sách',               'Xem danh sách sách'),
	(4,  'WRITE_BOOK',            'Ghi sách',               'Tạo, sửa, xóa sách'),
	(5,  'READ_PURCHASE_ORDER',   'Đọc đơn đặt mua',        'Xem danh sách đơn đặt mua'),
	(6,  'WRITE_PURCHASE_ORDER',  'Ghi đơn đặt mua',        'Tạo, sửa, xóa đơn đặt mua'),
	(7,  'READ_GOODS_RECEIPT',    'Đọc phiếu nhập',         'Xem danh sách phiếu nhập'),
	(8,  'WRITE_GOODS_RECEIPT',   'Ghi phiếu nhập',         'Tạo, sửa, xóa phiếu nhập'),
    (9,  'SALES_MANAGEMENT',      'Quản lý bán hàng',       'Quản lý đơn hàng, khách hàng'),
    (10, 'DELIVERY_MANAGEMENT',   'Quản lý giao hàng',      'Quản lý vận chuyển, giao hàng'),
    (11, 'READ_EMPLOYEE',         'Xem nhân viên',          'Xem danh sách và chi tiết nhân viên'),
    (12, 'WRITE_EMPLOYEE',        'Quản lý nhân viên',      'Tạo, sửa, xóa nhân viên'),
    (13, 'READ_DEPARTMENT',       'Xem phòng ban',          'Xem danh sách và chi tiết phòng ban'),
    (14, 'WRITE_DEPARTMENT',      'Quản lý phòng ban',      'Tạo, sửa, xóa phòng ban')
AS new(permission_id, code, name, description)
ON DUPLICATE KEY UPDATE
	code = new.code,
	name = new.name,
	description = new.description;

-- =====================
-- Role-Permission mapping
-- =====================
-- ADMIN: all permissions
INSERT IGNORE INTO role_permission (role_id, permission_id)
SELECT 1, p.permission_id FROM permission p;

-- SALES_EMPLOYEE: 1,3,5,7,9
INSERT IGNORE INTO role_permission (role_id, permission_id) VALUES
	(2,1),(2,3),(2,5),(2,7),(2,9);

-- DELIVERY_EMPLOYEE: 1,3,5,7,10
INSERT IGNORE INTO role_permission (role_id, permission_id) VALUES
	(3,1),(3,3),(3,5),(3,7),(3,10);

-- CUSTOMER: 1,3
INSERT IGNORE INTO role_permission (role_id, permission_id) VALUES
	(4,1),(4,3);

-- =====================
-- Purchase Order Statuses (IDs are used across the app)
-- 1=Pending, 2=Sent, 3=Confirmed, 4=Delivered, 5=Cancelled
-- =====================
INSERT INTO purchase_order_status (status_id, status_name, description)
VALUES
	(1, 'Pending',   'Đơn đặt mua đang chờ xử lý'),
	(2, 'Sent',      'Đã gửi đơn đặt mua cho nhà cung cấp'),
	(3, 'Confirmed', 'Nhà xuất bản đã xác nhận'),
	(4, 'Delivered', 'Đã giao/nhập hàng'),
	(5, 'Cancelled', 'Đã hủy đơn đặt mua')
AS new(status_id, status_name, description)
ON DUPLICATE KEY UPDATE
	status_name = new.status_name,
	description = new.description;

-- =====================
-- OPTIONAL: Map known accounts to roles (if they exist)
-- =====================
UPDATE account SET role_id = 1 WHERE email = 'admin@bookstore.com';
UPDATE account SET role_id = 2 WHERE email IN ('employee@bookstore.com','sales@bookstore.com');
UPDATE account SET role_id = 3 WHERE email IN ('delivery@bookstore.com','shipper@bookstore.com');
UPDATE account SET role_id = 4 WHERE email = 'customer@bookstore.com';

COMMIT;

-- How to run (PowerShell):
--   mysql -h shuttle.proxy.rlwy.net -P 42130 -u root -p datn < permissions-setup.sql
