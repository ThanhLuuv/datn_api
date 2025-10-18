-- Thêm các cột khấu trừ vào bảng return
ALTER TABLE `return` 
ADD COLUMN `apply_deduction` BOOLEAN NOT NULL DEFAULT FALSE COMMENT 'Có áp dụng khấu trừ không',
ADD COLUMN `deduction_percent` DECIMAL(5,2) NOT NULL DEFAULT 0.00 COMMENT 'Phần trăm khấu trừ',
ADD COLUMN `deduction_amount` DECIMAL(12,2) NOT NULL DEFAULT 0.00 COMMENT 'Số tiền khấu trừ',
ADD COLUMN `final_amount` DECIMAL(12,2) NOT NULL DEFAULT 0.00 COMMENT 'Số tiền cuối cùng sau khấu trừ';

-- Thêm index cho các cột mới
ALTER TABLE `return` 
ADD INDEX `idx_apply_deduction` (`apply_deduction`),
ADD INDEX `idx_final_amount` (`final_amount`);
