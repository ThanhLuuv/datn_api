-- Fix return table by adding missing columns
-- This script adds the missing columns to the return table

-- 1. Check current return table structure
SELECT 'CURRENT RETURN TABLE STRUCTURE:' as info;
DESCRIBE `datn.return`;

-- 2. Add missing columns (MySQL doesn't support IF NOT EXISTS in ALTER TABLE)
-- Add status column
ALTER TABLE `datn.return` ADD COLUMN status INT NOT NULL DEFAULT 0;

-- Add processed_by column
ALTER TABLE `datn.return` ADD COLUMN processed_by BIGINT NULL;

-- Add processed_at column
ALTER TABLE `datn.return` ADD COLUMN processed_at DATETIME(6) NULL;

-- Add notes column
ALTER TABLE `datn.return` ADD COLUMN notes VARCHAR(500) NULL;

-- 3. Check updated table structure
SELECT 'UPDATED RETURN TABLE STRUCTURE:' as info;
DESCRIBE `datn.return`;

-- 4. Create return_line table if not exists
CREATE TABLE IF NOT EXISTS `datn.return_line` (
    return_line_id BIGINT AUTO_INCREMENT PRIMARY KEY,
    return_id BIGINT NOT NULL,
    order_line_id BIGINT NOT NULL,
    qty_returned INT NOT NULL,
    amount DECIMAL(12,2) NOT NULL
);

-- 5. Create invoice table if not exists (for foreign key reference)
CREATE TABLE IF NOT EXISTS `datn.invoice` (
    invoice_id BIGINT AUTO_INCREMENT PRIMARY KEY,
    order_id BIGINT NOT NULL,
    total_amount DECIMAL(14,2) NOT NULL,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
);

-- 6. Insert sample data for testing (only if not exists)
INSERT INTO `datn.invoice` (invoice_id, order_id, total_amount, created_at) 
SELECT 1, 1001, 50000, NOW()
WHERE NOT EXISTS (SELECT 1 FROM `datn.invoice` WHERE invoice_id = 1);

INSERT INTO `datn.invoice` (invoice_id, order_id, total_amount, created_at) 
SELECT 2, 1002, 75000, NOW()
WHERE NOT EXISTS (SELECT 1 FROM `datn.invoice` WHERE invoice_id = 2);

INSERT INTO `datn.invoice` (invoice_id, order_id, total_amount, created_at) 
SELECT 3, 1003, 100000, NOW()
WHERE NOT EXISTS (SELECT 1 FROM `datn.invoice` WHERE invoice_id = 3);

-- Insert sample returns (only if not exists)
INSERT INTO `datn.return` (return_id, invoice_id, created_at, reason, status, processed_by, processed_at, notes) 
SELECT 1, 1, NOW(), 'Sách bị hỏng trong quá trình vận chuyển', 0, NULL, NULL, NULL
WHERE NOT EXISTS (SELECT 1 FROM `datn.return` WHERE return_id = 1);

INSERT INTO `datn.return` (return_id, invoice_id, created_at, reason, status, processed_by, processed_at, notes) 
SELECT 2, 2, NOW(), 'Khách hàng không hài lòng với chất lượng sách', 1, 1, NOW(), 'Đã duyệt và hoàn tiền'
WHERE NOT EXISTS (SELECT 1 FROM `datn.return` WHERE return_id = 2);

INSERT INTO `datn.return` (return_id, invoice_id, created_at, reason, status, processed_by, processed_at, notes) 
SELECT 3, 3, NOW(), 'Đặt nhầm sách', 2, 1, NOW(), 'Từ chối vì không đủ điều kiện'
WHERE NOT EXISTS (SELECT 1 FROM `datn.return` WHERE return_id = 3);

-- Insert sample return lines (only if not exists)
INSERT INTO `datn.return_line` (return_line_id, return_id, order_line_id, qty_returned, amount) 
SELECT 1, 1, 2001, 1, 50000
WHERE NOT EXISTS (SELECT 1 FROM `datn.return_line` WHERE return_line_id = 1);

INSERT INTO `datn.return_line` (return_line_id, return_id, order_line_id, qty_returned, amount) 
SELECT 2, 2, 2002, 1, 75000
WHERE NOT EXISTS (SELECT 1 FROM `datn.return_line` WHERE return_line_id = 2);

INSERT INTO `datn.return_line` (return_line_id, return_id, order_line_id, qty_returned, amount) 
SELECT 3, 3, 2003, 1, 100000
WHERE NOT EXISTS (SELECT 1 FROM `datn.return_line` WHERE return_line_id = 3);

-- 7. Verify the data
SELECT 'Fix completed successfully!' as message;

-- Show sample returns
SELECT 
    'SAMPLE RETURNS:' as info,
    r.return_id,
    r.invoice_id,
    r.reason,
    CASE r.status 
        WHEN 0 THEN 'PENDING'
        WHEN 1 THEN 'APPROVED'
        WHEN 2 THEN 'REJECTED'
        WHEN 3 THEN 'PROCESSED'
        ELSE 'UNKNOWN'
    END as status_text,
    r.processed_by,
    r.processed_at,
    r.notes,
    r.created_at
FROM `datn.return` r
ORDER BY r.return_id;
