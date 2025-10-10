-- Script sửa lỗi collation cho foreign key isbn

-- Kiểm tra collation của cột isbn trong cả hai bảng
SELECT 
    TABLE_NAME,
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    CHARACTER_SET_NAME,
    COLLATION_NAME
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_SCHEMA = DATABASE() 
  AND TABLE_NAME IN ('book', 'cart_item')
  AND COLUMN_NAME = 'isbn';

-- Sửa collation của cột isbn trong cart_item để khớp với book
-- Thay đổi collation thành giống hệt bảng book
ALTER TABLE `cart_item` 
MODIFY COLUMN `isbn` VARCHAR(20) NOT NULL COLLATE utf8mb4_unicode_ci;

-- Thử thêm foreign key lại
ALTER TABLE `cart_item`
ADD CONSTRAINT `fk_cart_item_book` 
FOREIGN KEY (`isbn`) REFERENCES `book` (`isbn`) ON DELETE CASCADE;

-- Nếu vẫn lỗi, thử với collation khác:
-- ALTER TABLE `cart_item` 
-- MODIFY COLUMN `isbn` VARCHAR(20) NOT NULL COLLATE utf8mb4_general_ci;

-- Kiểm tra lại collation sau khi sửa
SELECT 
    TABLE_NAME,
    COLUMN_NAME,
    COLLATION_NAME
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_SCHEMA = DATABASE() 
  AND TABLE_NAME IN ('book', 'cart_item')
  AND COLUMN_NAME = 'isbn';

-- Kiểm tra kết quả
SELECT 'Foreign key added successfully!' as message;
