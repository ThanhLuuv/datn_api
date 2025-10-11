-- SQL Script để cập nhật bảng `invoice` hiện có (An toàn)
USE `datn`;

-- Kiểm tra cấu trúc bảng hiện tại
DESCRIBE `invoice`;

-- Thêm các cột còn thiếu (từng cột một để dễ debug)
-- Nếu cột đã tồn tại sẽ báo lỗi, bỏ qua lỗi đó

-- Thêm payment_method
ALTER TABLE `invoice` 
ADD COLUMN `payment_method` VARCHAR(50) NULL AFTER `tax_amount`;

-- Thêm payment_reference  
ALTER TABLE `invoice` 
ADD COLUMN `payment_reference` VARCHAR(100) NULL AFTER `payment_method`;

-- Thêm paid_at
ALTER TABLE `invoice` 
ADD COLUMN `paid_at` DATETIME NULL AFTER `payment_reference`;

-- Thêm updated_at
ALTER TABLE `invoice` 
ADD COLUMN `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP AFTER `created_at`;

-- Thêm invoice_number nếu chưa có
ALTER TABLE `invoice` 
ADD COLUMN `invoice_number` VARCHAR(50) NOT NULL DEFAULT 'TEMP' AFTER `order_id`;

-- Thêm payment_status nếu chưa có
ALTER TABLE `invoice` 
ADD COLUMN `payment_status` VARCHAR(20) NOT NULL DEFAULT 'PENDING' AFTER `total_amount`;

-- Cập nhật các cột hiện có
ALTER TABLE `invoice` 
MODIFY COLUMN `invoice_number` VARCHAR(50) NOT NULL,
MODIFY COLUMN `total_amount` DECIMAL(12,2) NOT NULL,
MODIFY COLUMN `payment_status` VARCHAR(20) NOT NULL DEFAULT 'PENDING';

-- Thêm các index còn thiếu
CREATE INDEX `idx_invoice_order` ON `invoice` (`order_id`);
CREATE INDEX `idx_invoice_payment_status` ON `invoice` (`payment_status`);
CREATE INDEX `idx_invoice_created_at` ON `invoice` (`created_at`);

-- Thêm unique constraint cho invoice_number
ALTER TABLE `invoice` ADD CONSTRAINT `uk_invoice_number` UNIQUE (`invoice_number`);

-- Kiểm tra cấu trúc bảng sau khi cập nhật
DESCRIBE `invoice`;

-- Hiển thị các index hiện có
SHOW INDEX FROM `invoice`;
