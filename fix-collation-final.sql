-- Script sửa collation để khớp với bảng book
-- Từ hình ảnh: book.isbn có collation utf8mb4_0900_ai_ci
-- cart_item.isbn có collation utf8mb4_unicode_ci

-- Sửa collation của cart_item.isbn để khớp với book.isbn
ALTER TABLE `cart_item` 
MODIFY COLUMN `isbn` VARCHAR(20) NOT NULL COLLATE utf8mb4_0900_ai_ci;

-- Thêm foreign key sau khi sửa collation
ALTER TABLE `cart_item`
ADD CONSTRAINT `fk_cart_item_book` 
FOREIGN KEY (`isbn`) REFERENCES `book` (`isbn`) ON DELETE CASCADE;

-- Kiểm tra collation sau khi sửa
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
