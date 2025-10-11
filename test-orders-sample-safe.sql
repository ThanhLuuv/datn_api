-- Sample orders for testing delivery assignment API
-- These orders are in Pending status and ready for delivery assignment
-- SAFE VERSION: Checks existence before creating/inserting

-- 1. First, ensure we have required tables and sample data
-- (Run employee-area-simple-migration-v2.sql first if not done)

-- 2. Create tables if not exist
CREATE TABLE IF NOT EXISTS role (
    role_id BIGINT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description VARCHAR(300)
);

CREATE TABLE IF NOT EXISTS department (
    department_id BIGINT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description VARCHAR(300)
);

CREATE TABLE IF NOT EXISTS account (
    account_id BIGINT AUTO_INCREMENT PRIMARY KEY,
    email VARCHAR(191) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    role_id BIGINT NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6)
);

CREATE TABLE IF NOT EXISTS employee (
    employee_id BIGINT AUTO_INCREMENT PRIMARY KEY,
    account_id BIGINT NOT NULL,
    department_id BIGINT NOT NULL,
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    gender INT NOT NULL,
    dob DATE NULL,
    address VARCHAR(300) NULL,
    phone VARCHAR(30) NULL,
    email VARCHAR(191) NULL,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6)
);

CREATE TABLE IF NOT EXISTS customer (
    customer_id BIGINT AUTO_INCREMENT PRIMARY KEY,
    account_id BIGINT NOT NULL,
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    gender INT NOT NULL,
    phone VARCHAR(30) NULL,
    email VARCHAR(191) NULL,
    address VARCHAR(300) NULL
);

CREATE TABLE IF NOT EXISTS publisher (
    publisher_id BIGINT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(150) NOT NULL,
    address VARCHAR(300),
    phone VARCHAR(30),
    email VARCHAR(191)
);

CREATE TABLE IF NOT EXISTS category (
    category_id BIGINT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description VARCHAR(300)
);

CREATE TABLE IF NOT EXISTS book (
    isbn VARCHAR(20) PRIMARY KEY,
    title VARCHAR(300) NOT NULL,
    publisher_id BIGINT NOT NULL,
    category_id BIGINT NOT NULL,
    page_count INT NOT NULL,
    unit_price DECIMAL(12,2) NOT NULL,
    publish_year INT NOT NULL,
    stock INT NOT NULL DEFAULT 0,
    status BOOLEAN NOT NULL DEFAULT TRUE,
    image_url VARCHAR(500),
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6)
);

CREATE TABLE IF NOT EXISTS `order` (
    order_id BIGINT AUTO_INCREMENT PRIMARY KEY,
    customer_id BIGINT NOT NULL,
    placed_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    receiver_name VARCHAR(150) NOT NULL,
    receiver_phone VARCHAR(30) NOT NULL,
    shipping_address VARCHAR(300) NOT NULL,
    status INT NOT NULL DEFAULT 0,
    approved_by BIGINT NULL,
    delivered_by BIGINT NULL,
    delivery_date DATE NULL
);

CREATE TABLE IF NOT EXISTS order_line (
    order_line_id BIGINT AUTO_INCREMENT PRIMARY KEY,
    order_id BIGINT NOT NULL,
    isbn VARCHAR(20) NOT NULL,
    qty INT NOT NULL,
    unit_price DECIMAL(12,2) NOT NULL
);

-- 3. Insert sample roles (only if not exists)
INSERT INTO role (role_id, name, description) 
SELECT 1, 'Customer', 'Khách hàng' WHERE NOT EXISTS (SELECT 1 FROM role WHERE role_id = 1);

INSERT INTO role (role_id, name, description) 
SELECT 2, 'Admin', 'Quản trị viên' WHERE NOT EXISTS (SELECT 1 FROM role WHERE role_id = 2);

INSERT INTO role (role_id, name, description) 
SELECT 3, 'Employee', 'Nhân viên' WHERE NOT EXISTS (SELECT 1 FROM role WHERE role_id = 3);

INSERT INTO role (role_id, name, description) 
SELECT 4, 'Delivery Employee', 'Nhân viên giao hàng' WHERE NOT EXISTS (SELECT 1 FROM role WHERE role_id = 4);

-- 4. Insert sample departments (only if not exists)
INSERT INTO department (department_id, name, description) 
SELECT 1, 'Giao Hàng', 'Phòng giao hàng' WHERE NOT EXISTS (SELECT 1 FROM department WHERE department_id = 1);

-- 5. Insert sample accounts for delivery employees (only if not exists)
INSERT INTO account (account_id, email, password_hash, role_id, is_active) 
SELECT 1, 'shipper1@example.com', 'test-hash', 4, TRUE 
WHERE NOT EXISTS (SELECT 1 FROM account WHERE account_id = 1);

INSERT INTO account (account_id, email, password_hash, role_id, is_active) 
SELECT 2, 'shipper2@example.com', 'test-hash', 4, TRUE 
WHERE NOT EXISTS (SELECT 1 FROM account WHERE account_id = 2);

INSERT INTO account (account_id, email, password_hash, role_id, is_active) 
SELECT 3, 'shipper3@example.com', 'test-hash', 4, TRUE 
WHERE NOT EXISTS (SELECT 1 FROM account WHERE account_id = 3);

-- 6. Insert sample delivery employees (only if not exists)
INSERT INTO employee (employee_id, account_id, department_id, first_name, last_name, gender, phone, email) 
SELECT 1, 1, 1, 'Nguyen', 'Van A', 1, '0901111111', 'shipper1@example.com'
WHERE NOT EXISTS (SELECT 1 FROM employee WHERE employee_id = 1);

INSERT INTO employee (employee_id, account_id, department_id, first_name, last_name, gender, phone, email) 
SELECT 2, 2, 1, 'Tran', 'Van B', 1, '0902222222', 'shipper2@example.com'
WHERE NOT EXISTS (SELECT 1 FROM employee WHERE employee_id = 2);

INSERT INTO employee (employee_id, account_id, department_id, first_name, last_name, gender, phone, email) 
SELECT 3, 3, 1, 'Le', 'Thi C', 2, '0903333333', 'shipper3@example.com'
WHERE NOT EXISTS (SELECT 1 FROM employee WHERE employee_id = 3);

-- 7. Insert sample accounts for customers (only if not exists)
INSERT INTO account (account_id, email, password_hash, role_id, is_active) 
SELECT 4, 'customer1@example.com', 'test-hash', 1, TRUE 
WHERE NOT EXISTS (SELECT 1 FROM account WHERE account_id = 4);

INSERT INTO account (account_id, email, password_hash, role_id, is_active) 
SELECT 5, 'customer2@example.com', 'test-hash', 1, TRUE 
WHERE NOT EXISTS (SELECT 1 FROM account WHERE account_id = 5);

INSERT INTO account (account_id, email, password_hash, role_id, is_active) 
SELECT 6, 'customer3@example.com', 'test-hash', 1, TRUE 
WHERE NOT EXISTS (SELECT 1 FROM account WHERE account_id = 6);

INSERT INTO account (account_id, email, password_hash, role_id, is_active) 
SELECT 7, 'customer4@example.com', 'test-hash', 1, TRUE 
WHERE NOT EXISTS (SELECT 1 FROM account WHERE account_id = 7);

INSERT INTO account (account_id, email, password_hash, role_id, is_active) 
SELECT 8, 'customer5@example.com', 'test-hash', 1, TRUE 
WHERE NOT EXISTS (SELECT 1 FROM account WHERE account_id = 8);

-- 8. Insert sample customers (only if not exists)
INSERT INTO customer (customer_id, account_id, first_name, last_name, gender, phone, email, address) 
SELECT 1, 4, 'Nguyen', 'Van Customer', 1, '0904444444', 'customer1@example.com', '123 Phường Vũng Tàu, TP. Vũng Tàu'
WHERE NOT EXISTS (SELECT 1 FROM customer WHERE customer_id = 1);

INSERT INTO customer (customer_id, account_id, first_name, last_name, gender, phone, email, address) 
SELECT 2, 5, 'Tran', 'Thi Customer', 2, '0905555555', 'customer2@example.com', '456 Phường Tam Thắng, TP. Vũng Tàu'
WHERE NOT EXISTS (SELECT 1 FROM customer WHERE customer_id = 2);

INSERT INTO customer (customer_id, account_id, first_name, last_name, gender, phone, email, address) 
SELECT 3, 6, 'Le', 'Van Customer', 1, '0906666666', 'customer3@example.com', '789 Phường Rạch Dừa, TP. Vũng Tàu'
WHERE NOT EXISTS (SELECT 1 FROM customer WHERE customer_id = 3);

INSERT INTO customer (customer_id, account_id, first_name, last_name, gender, phone, email, address) 
SELECT 4, 7, 'Pham', 'Thi Customer', 2, '0907777777', 'customer4@example.com', '321 Phường Sài Gòn, Quận 1, TP.HCM'
WHERE NOT EXISTS (SELECT 1 FROM customer WHERE customer_id = 4);

INSERT INTO customer (customer_id, account_id, first_name, last_name, gender, phone, email, address) 
SELECT 5, 8, 'Hoang', 'Van Customer', 1, '0908888888', 'customer5@example.com', '654 Phường Tân Định, Quận 1, TP.HCM'
WHERE NOT EXISTS (SELECT 1 FROM customer WHERE customer_id = 5);

-- 9. Insert sample publishers (only if not exists)
INSERT INTO publisher (publisher_id, name, address, phone, email) 
SELECT 1, 'NXB Test', '123 Đường Test, TP.HCM', '0900000000', 'nxb@test.com'
WHERE NOT EXISTS (SELECT 1 FROM publisher WHERE publisher_id = 1);

-- 10. Insert sample categories (only if not exists)
INSERT INTO category (category_id, name, description) 
SELECT 1, 'Sách Test', 'Danh mục sách test'
WHERE NOT EXISTS (SELECT 1 FROM category WHERE category_id = 1);

-- 11. Insert sample books (only if not exists)
INSERT INTO book (isbn, title, publisher_id, category_id, page_count, unit_price, publish_year, stock, status, image_url) 
SELECT '9781234567890', 'Sách Test 1', 1, 1, 200, 50000, 2023, 100, TRUE, NULL
WHERE NOT EXISTS (SELECT 1 FROM book WHERE isbn = '9781234567890');

INSERT INTO book (isbn, title, publisher_id, category_id, page_count, unit_price, publish_year, stock, status, image_url) 
SELECT '9781234567891', 'Sách Test 2', 1, 1, 250, 75000, 2023, 50, TRUE, NULL
WHERE NOT EXISTS (SELECT 1 FROM book WHERE isbn = '9781234567891');

INSERT INTO book (isbn, title, publisher_id, category_id, page_count, unit_price, publish_year, stock, status, image_url) 
SELECT '9781234567892', 'Sách Test 3', 1, 1, 300, 100000, 2023, 75, TRUE, NULL
WHERE NOT EXISTS (SELECT 1 FROM book WHERE isbn = '9781234567892');

-- 12. Insert sample orders (PENDING status - ready for delivery assignment)
INSERT INTO `order` (order_id, customer_id, placed_at, receiver_name, receiver_phone, shipping_address, status, approved_by, delivered_by, delivery_date) 
SELECT 1001, 1, NOW(), 'Nguyen Van Customer', '0904444444', '123 Phường Vũng Tàu, TP. Vũng Tàu', 0, NULL, NULL, NULL
WHERE NOT EXISTS (SELECT 1 FROM `order` WHERE order_id = 1001);

INSERT INTO `order` (order_id, customer_id, placed_at, receiver_name, receiver_phone, shipping_address, status, approved_by, delivered_by, delivery_date) 
SELECT 1002, 2, NOW(), 'Tran Thi Customer', '0905555555', '456 Phường Tam Thắng, TP. Vũng Tàu', 0, NULL, NULL, NULL
WHERE NOT EXISTS (SELECT 1 FROM `order` WHERE order_id = 1002);

INSERT INTO `order` (order_id, customer_id, placed_at, receiver_name, receiver_phone, shipping_address, status, approved_by, delivered_by, delivery_date) 
SELECT 1003, 3, NOW(), 'Le Van Customer', '0906666666', '789 Phường Rạch Dừa, TP. Vũng Tàu', 0, NULL, NULL, NULL
WHERE NOT EXISTS (SELECT 1 FROM `order` WHERE order_id = 1003);

INSERT INTO `order` (order_id, customer_id, placed_at, receiver_name, receiver_phone, shipping_address, status, approved_by, delivered_by, delivery_date) 
SELECT 1004, 4, NOW(), 'Pham Thi Customer', '0907777777', '321 Phường Sài Gòn, Quận 1, TP.HCM', 0, NULL, NULL, NULL
WHERE NOT EXISTS (SELECT 1 FROM `order` WHERE order_id = 1004);

INSERT INTO `order` (order_id, customer_id, placed_at, receiver_name, receiver_phone, shipping_address, status, approved_by, delivered_by, delivery_date) 
SELECT 1005, 5, NOW(), 'Hoang Van Customer', '0908888888', '654 Phường Tân Định, Quận 1, TP.HCM', 0, NULL, NULL, NULL
WHERE NOT EXISTS (SELECT 1 FROM `order` WHERE order_id = 1005);

-- 13. Insert order lines for each order (only if not exists)
INSERT INTO order_line (order_line_id, order_id, isbn, qty, unit_price) 
SELECT 2001, 1001, '9781234567890', 1, 50000
WHERE NOT EXISTS (SELECT 1 FROM order_line WHERE order_line_id = 2001);

INSERT INTO order_line (order_line_id, order_id, isbn, qty, unit_price) 
SELECT 2002, 1002, '9781234567891', 1, 75000
WHERE NOT EXISTS (SELECT 1 FROM order_line WHERE order_line_id = 2002);

INSERT INTO order_line (order_line_id, order_id, isbn, qty, unit_price) 
SELECT 2003, 1003, '9781234567892', 1, 100000
WHERE NOT EXISTS (SELECT 1 FROM order_line WHERE order_line_id = 2003);

INSERT INTO order_line (order_line_id, order_id, isbn, qty, unit_price) 
SELECT 2004, 1004, '9781234567890', 1, 50000
WHERE NOT EXISTS (SELECT 1 FROM order_line WHERE order_line_id = 2004);

INSERT INTO order_line (order_line_id, order_id, isbn, qty, unit_price) 
SELECT 2005, 1004, '9781234567891', 1, 75000
WHERE NOT EXISTS (SELECT 1 FROM order_line WHERE order_line_id = 2005);

INSERT INTO order_line (order_line_id, order_id, isbn, qty, unit_price) 
SELECT 2006, 1005, '9781234567890', 1, 50000
WHERE NOT EXISTS (SELECT 1 FROM order_line WHERE order_line_id = 2006);

INSERT INTO order_line (order_line_id, order_id, isbn, qty, unit_price) 
SELECT 2007, 1005, '9781234567891', 1, 75000
WHERE NOT EXISTS (SELECT 1 FROM order_line WHERE order_line_id = 2007);

INSERT INTO order_line (order_line_id, order_id, isbn, qty, unit_price) 
SELECT 2008, 1005, '9781234567892', 1, 100000
WHERE NOT EXISTS (SELECT 1 FROM order_line WHERE order_line_id = 2008);

-- 14. Insert some assigned orders (for testing different scenarios)
INSERT INTO `order` (order_id, customer_id, placed_at, receiver_name, receiver_phone, shipping_address, status, approved_by, delivered_by, delivery_date) 
SELECT 1006, 1, NOW(), 'Nguyen Van Customer', '0904444444', '999 Phường Phú Mỹ, TP. Vũng Tàu', 1, 1, 1, DATE_ADD(NOW(), INTERVAL 1 DAY)
WHERE NOT EXISTS (SELECT 1 FROM `order` WHERE order_id = 1006);

INSERT INTO `order` (order_id, customer_id, placed_at, receiver_name, receiver_phone, shipping_address, status, approved_by, delivered_by, delivery_date) 
SELECT 1007, 2, NOW(), 'Tran Thi Customer', '0905555555', '888 Phường Tam Long, TP. Vũng Tàu', 1, 1, 2, DATE_ADD(NOW(), INTERVAL 2 DAY)
WHERE NOT EXISTS (SELECT 1 FROM `order` WHERE order_id = 1007);

INSERT INTO order_line (order_line_id, order_id, isbn, qty, unit_price) 
SELECT 2009, 1006, '9781234567890', 2, 50000
WHERE NOT EXISTS (SELECT 1 FROM order_line WHERE order_line_id = 2009);

INSERT INTO order_line (order_line_id, order_id, isbn, qty, unit_price) 
SELECT 2010, 1006, '9781234567891', 2, 75000
WHERE NOT EXISTS (SELECT 1 FROM order_line WHERE order_line_id = 2010);

INSERT INTO order_line (order_line_id, order_id, isbn, qty, unit_price) 
SELECT 2011, 1007, '9781234567892', 1, 100000
WHERE NOT EXISTS (SELECT 1 FROM order_line WHERE order_line_id = 2011);

INSERT INTO order_line (order_line_id, order_id, isbn, qty, unit_price) 
SELECT 2012, 1007, '9781234567890', 1, 50000
WHERE NOT EXISTS (SELECT 1 FROM order_line WHERE order_line_id = 2012);

INSERT INTO order_line (order_line_id, order_id, isbn, qty, unit_price) 
SELECT 2013, 1007, '9781234567891', 1, 75000
WHERE NOT EXISTS (SELECT 1 FROM order_line WHERE order_line_id = 2013);

-- 15. Insert some delivered orders (for testing employee workload)
INSERT INTO `order` (order_id, customer_id, placed_at, receiver_name, receiver_phone, shipping_address, status, approved_by, delivered_by, delivery_date) 
SELECT 1008, 3, NOW(), 'Le Van Customer', '0906666666', '777 Phường Tân Thành, TP. Vũng Tàu', 2, 1, 1, DATE_SUB(NOW(), INTERVAL 1 DAY)
WHERE NOT EXISTS (SELECT 1 FROM `order` WHERE order_id = 1008);

INSERT INTO `order` (order_id, customer_id, placed_at, receiver_name, receiver_phone, shipping_address, status, approved_by, delivered_by, delivery_date) 
SELECT 1009, 4, NOW(), 'Pham Thi Customer', '0907777777', '666 Phường Bàn Cờ, Quận 3, TP.HCM', 2, 1, 2, DATE_SUB(NOW(), INTERVAL 2 DAY)
WHERE NOT EXISTS (SELECT 1 FROM `order` WHERE order_id = 1009);

INSERT INTO order_line (order_line_id, order_id, isbn, qty, unit_price) 
SELECT 2014, 1008, '9781234567890', 3, 50000
WHERE NOT EXISTS (SELECT 1 FROM order_line WHERE order_line_id = 2014);

INSERT INTO order_line (order_line_id, order_id, isbn, qty, unit_price) 
SELECT 2015, 1008, '9781234567891', 2, 75000
WHERE NOT EXISTS (SELECT 1 FROM order_line WHERE order_line_id = 2015);

INSERT INTO order_line (order_line_id, order_id, isbn, qty, unit_price) 
SELECT 2016, 1009, '9781234567892', 2, 100000
WHERE NOT EXISTS (SELECT 1 FROM order_line WHERE order_line_id = 2016);

INSERT INTO order_line (order_line_id, order_id, isbn, qty, unit_price) 
SELECT 2017, 1009, '9781234567890', 1, 50000
WHERE NOT EXISTS (SELECT 1 FROM order_line WHERE order_line_id = 2017);

-- 16. Verify the data
SELECT 'Sample data created successfully!' as message;

-- Show pending orders (ready for delivery assignment)
SELECT 
    'PENDING ORDERS (Chờ xác nhận - Ready for delivery assignment):' as info,
    o.order_id,
    CONCAT(c.first_name, ' ', c.last_name) as customer_name,
    o.receiver_name,
    o.receiver_phone,
    o.shipping_address,
    o.placed_at,
    CASE o.status 
        WHEN 0 THEN 'CHỜ XÁC NHẬN'
        WHEN 1 THEN 'ĐÃ PHÂN CÔNG' 
        WHEN 2 THEN 'ĐÃ GIAO'
        WHEN 3 THEN 'ĐÃ HỦY'
        ELSE 'UNKNOWN'
    END as status_text
FROM `order` o
JOIN customer c ON o.customer_id = c.customer_id
WHERE o.status = 0
ORDER BY o.order_id;

-- Show assigned orders (already assigned to employees)
SELECT 
    'ASSIGNED ORDERS (Đã phân công - Already assigned):' as info,
    o.order_id,
    CONCAT(c.first_name, ' ', c.last_name) as customer_name,
    o.receiver_name,
    o.receiver_phone,
    o.shipping_address,
    CONCAT(e.first_name, ' ', e.last_name) as assigned_employee,
    o.delivery_date,
    CASE o.status 
        WHEN 0 THEN 'CHỜ XÁC NHẬN'
        WHEN 1 THEN 'ĐÃ PHÂN CÔNG' 
        WHEN 2 THEN 'ĐÃ GIAO'
        WHEN 3 THEN 'ĐÃ HỦY'
        ELSE 'UNKNOWN'
    END as status_text
FROM `order` o
JOIN customer c ON o.customer_id = c.customer_id
JOIN employee e ON o.delivered_by = e.employee_id
WHERE o.status = 1
ORDER BY o.order_id;

-- Show delivered orders (already delivered)
SELECT 
    'DELIVERED ORDERS (Đã giao - Already delivered):' as info,
    o.order_id,
    CONCAT(c.first_name, ' ', c.last_name) as customer_name,
    o.receiver_name,
    o.receiver_phone,
    o.shipping_address,
    CONCAT(e.first_name, ' ', e.last_name) as delivered_employee,
    o.delivery_date,
    CASE o.status 
        WHEN 0 THEN 'CHỜ XÁC NHẬN'
        WHEN 1 THEN 'ĐÃ PHÂN CÔNG' 
        WHEN 2 THEN 'ĐÃ GIAO'
        WHEN 3 THEN 'ĐÃ HỦY'
        ELSE 'UNKNOWN'
    END as status_text
FROM `order` o
JOIN customer c ON o.customer_id = c.customer_id
JOIN employee e ON o.delivered_by = e.employee_id
WHERE o.status = 2
ORDER BY o.order_id;

-- Show employee workload
SELECT 
    'EMPLOYEE WORKLOAD:' as info,
    e.employee_id,
    CONCAT(e.first_name, ' ', e.last_name) as employee_name,
    COUNT(o.order_id) as assigned_orders,
    GROUP_CONCAT(a.name ORDER BY a.name SEPARATOR ', ') as assigned_areas
FROM employee e
LEFT JOIN `order` o ON e.employee_id = o.delivered_by AND o.status = 1
LEFT JOIN employee_area ea ON e.employee_id = ea.employee_id AND ea.is_active = TRUE
LEFT JOIN area a ON ea.area_id = a.area_id
GROUP BY e.employee_id, e.first_name, e.last_name
ORDER BY e.employee_id;



