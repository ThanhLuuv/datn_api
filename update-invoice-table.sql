-- SQL Script để cập nhật bảng `invoice` hiện có
USE `datn`;

-- Kiểm tra cấu trúc bảng hiện tại
DESCRIBE `invoice`;

-- Thêm các cột còn thiếu (bỏ IF NOT EXISTS vì MySQL không hỗ trợ)
-- Nếu cột đã tồn tại sẽ báo lỗi, bỏ qua lỗi đó
ALTER TABLE `invoice` 
ADD COLUMN `payment_method` VARCHAR(50) NULL AFTER `payment_status`;

ALTER TABLE `invoice` 
ADD COLUMN `payment_reference` VARCHAR(100) NULL AFTER `payment_method`;

ALTER TABLE `invoice` 
ADD COLUMN `paid_at` DATETIME NULL AFTER `payment_reference`;

ALTER TABLE `invoice` 
ADD COLUMN `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP AFTER `created_at`;

-- Cập nhật các cột hiện có (nếu cần)
ALTER TABLE `invoice` 
MODIFY COLUMN `invoice_number` VARCHAR(50) NOT NULL,
MODIFY COLUMN `total_amount` DECIMAL(12,2) NOT NULL,
MODIFY COLUMN `payment_status` VARCHAR(20) NOT NULL DEFAULT 'PENDING';

-- Thêm các index còn thiếu (bỏ IF NOT EXISTS)
CREATE INDEX `idx_invoice_order` ON `invoice` (`order_id`);
CREATE INDEX `idx_invoice_payment_status` ON `invoice` (`payment_status`);
CREATE INDEX `idx_invoice_created_at` ON `invoice` (`created_at`);

-- Thêm unique constraint cho invoice_number (nếu chưa có)
ALTER TABLE `invoice` ADD CONSTRAINT `uk_invoice_number` UNIQUE (`invoice_number`);

-- Kiểm tra foreign key constraint (nếu chưa có)
-- ALTER TABLE `invoice` ADD CONSTRAINT `fk_invoice_order` FOREIGN KEY (`order_id`) REFERENCES `order` (`order_id`) ON DELETE CASCADE;

-- Kiểm tra cấu trúc bảng sau khi cập nhật
DESCRIBE `invoice`;

-- Hiển thị các index hiện có
SHOW INDEX FROM `invoice`;
