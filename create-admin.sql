-- Tạo tài khoản admin trực tiếp trong database
USE datn;

-- Tạo tài khoản admin
INSERT INTO account (email, password_hash, role_id, is_active, created_at, updated_at)
VALUES ('admin@bookstore.com', '$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy', 3, 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE 
    password_hash = '$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy',
    role_id = 3,
    is_active = 1,
    updated_at = NOW();

-- Tạo tài khoản employee
INSERT INTO account (email, password_hash, role_id, is_active, created_at, updated_at)
VALUES ('employee@bookstore.com', '$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy', 2, 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE 
    password_hash = '$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy',
    role_id = 2,
    is_active = 1,
    updated_at = NOW();

-- Tạo tài khoản customer
INSERT INTO account (email, password_hash, role_id, is_active, created_at, updated_at)
VALUES ('customer@bookstore.com', '$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy', 1, 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE 
    password_hash = '$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy',
    role_id = 1,
    is_active = 1,
    updated_at = NOW();

-- Kiểm tra dữ liệu đã được tạo
SELECT 'Accounts created:' as status;
SELECT account_id, email, role_id, is_active FROM account;

SELECT 'Roles:' as status;
SELECT role_id, name, description FROM role;

SELECT 'Categories:' as status;
SELECT category_id, name, description FROM category;

SELECT 'Publishers:' as status;
SELECT publisher_id, name, address FROM publisher;

SELECT 'Authors:' as status;
SELECT author_id, first_name, last_name, gender FROM author;

SELECT 'Books:' as status;
SELECT isbn, title, unit_price, category_id, publisher_id FROM book;
