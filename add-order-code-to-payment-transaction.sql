-- Add order_code column to payment_transaction table
-- This column stores the orderCode sent to PayOS for webhook matching

ALTER TABLE `payment_transaction`
ADD COLUMN `order_code` BIGINT NOT NULL DEFAULT 0 AFTER `order_id`;

-- Add index for faster webhook lookups
CREATE INDEX `idx_payment_transaction_order_code` ON `payment_transaction` (`order_code`);

-- Update existing records with a default order_code (using transaction_id as base)
-- This is a temporary fix for existing data
UPDATE `payment_transaction` 
SET `order_code` = `transaction_id` 
WHERE `order_code` = 0;
