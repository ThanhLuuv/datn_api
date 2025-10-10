-- Script sửa lỗi: Tạo bảng cart_item với kiểu dữ liệu isbn đúng
-- (Thay thế VARCHAR(20) bằng kiểu dữ liệu thực tế của bảng book)

-- Tạo bảng cart (giữ nguyên)
CREATE TABLE `cart` (
  `cart_id` BIGINT NOT NULL AUTO_INCREMENT,
  `customer_id` BIGINT NOT NULL,
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`cart_id`),
  UNIQUE KEY `uk_cart_customer` (`customer_id`),
  CONSTRAINT `fk_cart_customer` FOREIGN KEY (`customer_id`) REFERENCES `customer` (`customer_id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Tạo bảng cart_item với kiểu dữ liệu isbn phù hợp
-- Nếu isbn trong bảng book là VARCHAR(13), thay đổi như sau:
CREATE TABLE `cart_item` (
  `cart_item_id` BIGINT NOT NULL AUTO_INCREMENT,
  `cart_id` BIGINT NOT NULL,
  `isbn` VARCHAR(13) NOT NULL,  -- Thay đổi từ VARCHAR(20) thành VARCHAR(13)
  `quantity` INT NOT NULL,
  `added_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`cart_item_id`),
  KEY `idx_cart_item_cart` (`cart_id`),
  KEY `idx_cart_item_isbn` (`isbn`),
  CONSTRAINT `fk_cart_item_cart` FOREIGN KEY (`cart_id`) REFERENCES `cart` (`cart_id`) ON DELETE CASCADE,
  CONSTRAINT `fk_cart_item_book` FOREIGN KEY (`isbn`) REFERENCES `book` (`isbn`) ON DELETE CASCADE,
  CONSTRAINT `chk_cart_item_quantity` CHECK (`quantity` > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Hoặc nếu isbn trong bảng book là CHAR(13), sử dụng:
/*
CREATE TABLE `cart_item` (
  `cart_item_id` BIGINT NOT NULL AUTO_INCREMENT,
  `cart_id` BIGINT NOT NULL,
  `isbn` CHAR(13) NOT NULL,  -- Thay đổi thành CHAR(13)
  `quantity` INT NOT NULL,
  `added_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`cart_item_id`),
  KEY `idx_cart_item_cart` (`cart_id`),
  KEY `idx_cart_item_isbn` (`isbn`),
  CONSTRAINT `fk_cart_item_cart` FOREIGN KEY (`cart_id`) REFERENCES `cart` (`cart_id`) ON DELETE CASCADE,
  CONSTRAINT `fk_cart_item_book` FOREIGN KEY (`isbn`) REFERENCES `book` (`isbn`) ON DELETE CASCADE,
  CONSTRAINT `chk_cart_item_quantity` CHECK (`quantity` > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
*/

-- Thêm comment và indexes
ALTER TABLE `cart` COMMENT = 'Giỏ hàng của khách hàng';
ALTER TABLE `cart_item` COMMENT = 'Sản phẩm trong giỏ hàng';

-- Thêm comment cho các cột
ALTER TABLE `cart` MODIFY COLUMN `cart_id` BIGINT NOT NULL AUTO_INCREMENT COMMENT 'ID giỏ hàng';
ALTER TABLE `cart` MODIFY COLUMN `customer_id` BIGINT NOT NULL COMMENT 'ID khách hàng';
ALTER TABLE `cart` MODIFY COLUMN `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'Thời gian tạo';
ALTER TABLE `cart` MODIFY COLUMN `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT 'Thời gian cập nhật';

ALTER TABLE `cart_item` MODIFY COLUMN `cart_item_id` BIGINT NOT NULL AUTO_INCREMENT COMMENT 'ID sản phẩm trong giỏ';
ALTER TABLE `cart_item` MODIFY COLUMN `cart_id` BIGINT NOT NULL COMMENT 'ID giỏ hàng';
ALTER TABLE `cart_item` MODIFY COLUMN `isbn` VARCHAR(13) NOT NULL COMMENT 'ISBN sách';  -- Thay đổi thành VARCHAR(13)
ALTER TABLE `cart_item` MODIFY COLUMN `quantity` INT NOT NULL COMMENT 'Số lượng';
ALTER TABLE `cart_item` MODIFY COLUMN `added_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'Thời gian thêm vào giỏ';
ALTER TABLE `cart_item` MODIFY COLUMN `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT 'Thời gian cập nhật';

-- Tạo index để tối ưu performance
CREATE INDEX `idx_cart_updated_at` ON `cart` (`updated_at`);
CREATE INDEX `idx_cart_item_updated_at` ON `cart_item` (`updated_at`);
CREATE INDEX `idx_cart_item_added_at` ON `cart_item` (`added_at`);

-- Kiểm tra kết quả
SELECT 'Cart tables created successfully!' as message;
SHOW TABLES LIKE 'cart%';
