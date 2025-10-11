-- Sửa lỗi tax_amount trong bảng invoice
USE `datn`;

-- Kiểm tra cấu trúc bảng invoice hiện tại
DESCRIBE `invoice`;

-- Thêm cột tax_amount với giá trị mặc định là 0
ALTER TABLE `invoice` 
ADD COLUMN `tax_amount` DECIMAL(12,2) NOT NULL DEFAULT 0 AFTER `total_amount`;

-- Kiểm tra lại cấu trúc
DESCRIBE `invoice`;
