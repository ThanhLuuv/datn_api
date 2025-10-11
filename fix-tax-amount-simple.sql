USE `datn`;
ALTER TABLE `invoice` ADD COLUMN `tax_amount` DECIMAL(12,2) NOT NULL DEFAULT 0 AFTER `total_amount`;
