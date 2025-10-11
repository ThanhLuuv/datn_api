-- Migration script to add status column to return table
-- SAFE VERSION: Checks existence before creating/altering

-- 1. Create return table if not exists (with status column)
CREATE TABLE IF NOT EXISTS return (
    return_id BIGINT AUTO_INCREMENT PRIMARY KEY,
    invoice_id BIGINT NOT NULL,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    reason VARCHAR(500),
    status INT NOT NULL DEFAULT 0,
    processed_by BIGINT NULL,
    processed_at DATETIME(6) NULL,
    notes VARCHAR(500) NULL
);

-- 2. Add status column if not exists (for existing tables)
SET @sql = 'ALTER TABLE return ADD COLUMN IF NOT EXISTS status INT NOT NULL DEFAULT 0';
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- 3. Add processed_by column if not exists
SET @sql = 'ALTER TABLE return ADD COLUMN IF NOT EXISTS processed_by BIGINT NULL';
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- 4. Add processed_at column if not exists
SET @sql = 'ALTER TABLE return ADD COLUMN IF NOT EXISTS processed_at DATETIME(6) NULL';
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- 5. Add notes column if not exists
SET @sql = 'ALTER TABLE return ADD COLUMN IF NOT EXISTS notes VARCHAR(500) NULL';
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- 6. Create return_line table if not exists
CREATE TABLE IF NOT EXISTS return_line (
    return_line_id BIGINT AUTO_INCREMENT PRIMARY KEY,
    return_id BIGINT NOT NULL,
    isbn VARCHAR(20) NOT NULL,
    qty INT NOT NULL,
    unit_price DECIMAL(12,2) NOT NULL,
    amount DECIMAL(12,2) NOT NULL,
    reason VARCHAR(500) NULL
);

-- 7. Create invoice table if not exists (for foreign key reference)
CREATE TABLE IF NOT EXISTS invoice (
    invoice_id BIGINT AUTO_INCREMENT PRIMARY KEY,
    order_id BIGINT NOT NULL,
    total_amount DECIMAL(14,2) NOT NULL,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
);

-- 8. Insert sample return statuses (for reference)
-- Status values:
-- 0 = PENDING (Chờ xử lý)
-- 1 = APPROVED (Đã duyệt)
-- 2 = REJECTED (Từ chối)
-- 3 = PROCESSED (Đã xử lý)

-- 9. Insert sample data for testing (only if not exists)
INSERT INTO invoice (invoice_id, order_id, total_amount, created_at) 
SELECT 1, 1001, 50000, NOW()
WHERE NOT EXISTS (SELECT 1 FROM invoice WHERE invoice_id = 1);

INSERT INTO invoice (invoice_id, order_id, total_amount, created_at) 
SELECT 2, 1002, 75000, NOW()
WHERE NOT EXISTS (SELECT 1 FROM invoice WHERE invoice_id = 2);

INSERT INTO invoice (invoice_id, order_id, total_amount, created_at) 
SELECT 3, 1003, 100000, NOW()
WHERE NOT EXISTS (SELECT 1 FROM invoice WHERE invoice_id = 3);

-- 10. Insert sample returns (only if not exists)
INSERT INTO return (return_id, invoice_id, created_at, reason, status, processed_by, processed_at, notes) 
SELECT 1, 1, NOW(), 'Sách bị hỏng trong quá trình vận chuyển', 0, NULL, NULL, NULL
WHERE NOT EXISTS (SELECT 1 FROM return WHERE return_id = 1);

INSERT INTO return (return_id, invoice_id, created_at, reason, status, processed_by, processed_at, notes) 
SELECT 2, 2, NOW(), 'Khách hàng không hài lòng với chất lượng sách', 1, 1, NOW(), 'Đã duyệt và hoàn tiền'
WHERE NOT EXISTS (SELECT 1 FROM return WHERE return_id = 2);

INSERT INTO return (return_id, invoice_id, created_at, reason, status, processed_by, processed_at, notes) 
SELECT 3, 3, NOW(), 'Đặt nhầm sách', 2, 1, NOW(), 'Từ chối vì không đủ điều kiện'
WHERE NOT EXISTS (SELECT 1 FROM return WHERE return_id = 3);

-- 11. Insert sample return lines (only if not exists)
INSERT INTO return_line (return_line_id, return_id, isbn, qty, unit_price, amount, reason) 
SELECT 1, 1, '9781234567890', 1, 50000, 50000, 'Sách bị hỏng'
WHERE NOT EXISTS (SELECT 1 FROM return_line WHERE return_line_id = 1);

INSERT INTO return_line (return_line_id, return_id, isbn, qty, unit_price, amount, reason) 
SELECT 2, 2, '9781234567891', 1, 75000, 75000, 'Chất lượng không đạt'
WHERE NOT EXISTS (SELECT 1 FROM return_line WHERE return_line_id = 2);

INSERT INTO return_line (return_line_id, return_id, isbn, qty, unit_price, amount, reason) 
SELECT 3, 3, '9781234567892', 1, 100000, 100000, 'Đặt nhầm'
WHERE NOT EXISTS (SELECT 1 FROM return_line WHERE return_line_id = 3);

-- 12. Verify the data
SELECT 'Return status migration completed successfully!' as message;

-- Show return statuses
SELECT 
    'RETURN STATUSES:' as info,
    '0 = PENDING (Chờ xử lý)' as status_0,
    '1 = APPROVED (Đã duyệt)' as status_1,
    '2 = REJECTED (Từ chối)' as status_2,
    '3 = PROCESSED (Đã xử lý)' as status_3;

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
FROM return r
ORDER BY r.return_id;



