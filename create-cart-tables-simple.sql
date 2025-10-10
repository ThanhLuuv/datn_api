-- Script tạo bảng cart và cart_item (không có foreign key trước)
-- Sau đó sẽ thêm foreign key sau khi kiểm tra kiểu dữ liệu

-- Xóa bảng nếu đã tồn tại (để test lại)
DROP TABLE IF EXISTS `cart_item`;
DROP TABLE IF EXISTS `cart`;

-- Tạo bảng cart
CREATE TABLE `cart` (
  `cart_id` BIGINT NOT NULL AUTO_INCREMENT,
  `customer_id` BIGINT NOT NULL,
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`cart_id`),
  UNIQUE KEY `uk_cart_customer` (`customer_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Tạo bảng cart_item (không có foreign key)
CREATE TABLE `cart_item` (
  `cart_item_id` BIGINT NOT NULL AUTO_INCREMENT,
  `cart_id` BIGINT NOT NULL,
  `isbn` VARCHAR(20) NOT NULL,  -- Dùng VARCHAR(20) để chắc chắn
  `quantity` INT NOT NULL,
  `added_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`cart_item_id`),
  KEY `idx_cart_item_cart` (`cart_id`),
  KEY `idx_cart_item_isbn` (`isbn`),
  CONSTRAINT `chk_cart_item_quantity` CHECK (`quantity` > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Kiểm tra kiểu dữ liệu của isbn trong bảng book
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    CHARACTER_SET_NAME,
    COLLATION_NAME,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_SCHEMA = DATABASE() 
  AND TABLE_NAME = 'book' 
  AND COLUMN_NAME = 'isbn';

-- Thêm foreign key sau khi kiểm tra kiểu dữ liệu
-- Uncomment các dòng dưới sau khi chạy query trên:

-- ALTER TABLE `cart` ADD CONSTRAINT `fk_cart_customer` 
-- FOREIGN KEY (`customer_id`) REFERENCES `customer` (`customer_id`) ON DELETE CASCADE;

-- ALTER TABLE `cart_item` ADD CONSTRAINT `fk_cart_item_cart` 
-- FOREIGN KEY (`cart_id`) REFERENCES `cart` (`cart_id`) ON DELETE CASCADE;

-- ALTER TABLE `cart_item` ADD CONSTRAINT `fk_cart_item_book` 
-- FOREIGN KEY (`isbn`) REFERENCES `book` (`isbn`) ON DELETE CASCADE;

-- Thêm comment
ALTER TABLE `cart` COMMENT = 'Giỏ hàng của khách hàng';
ALTER TABLE `cart_item` COMMENT = 'Sản phẩm trong giỏ hàng';

-- Thêm indexes
CREATE INDEX `idx_cart_updated_at` ON `cart` (`updated_at`);
CREATE INDEX `idx_cart_item_updated_at` ON `cart_item` (`updated_at`);
CREATE INDEX `idx_cart_item_added_at` ON `cart_item` (`added_at`);

-- Kiểm tra kết quả
SELECT 'Cart tables created successfully!' as message;
SHOW TABLES LIKE 'cart%';
