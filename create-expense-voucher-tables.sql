-- Tạo bảng phiếu chi
-- Drop tables if exist (in dependency order)
DROP TABLE IF EXISTS expense_voucher_line;
DROP TABLE IF EXISTS expense_voucher;

-- Tạo bảng expense_voucher
CREATE TABLE expense_voucher (
    expense_voucher_id BIGINT AUTO_INCREMENT PRIMARY KEY,
    voucher_number VARCHAR(50) NOT NULL UNIQUE,
    voucher_date DATETIME NOT NULL,
    description TEXT,
    total_amount DECIMAL(12,2) NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'PENDING',
    expense_type VARCHAR(20) NOT NULL DEFAULT 'RETURN_REFUND',
    created_by BIGINT,
    approved_by BIGINT,
    approved_at DATETIME NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    
    INDEX idx_voucher_number (voucher_number),
    INDEX idx_voucher_date (voucher_date),
    INDEX idx_status (status),
    INDEX idx_expense_type (expense_type),
    INDEX idx_created_by (created_by),
    INDEX idx_approved_by (approved_by),
    
    FOREIGN KEY (created_by) REFERENCES employee(employee_id) ON DELETE SET NULL,
    FOREIGN KEY (approved_by) REFERENCES employee(employee_id) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- Tạo bảng expense_voucher_line
CREATE TABLE expense_voucher_line (
    expense_voucher_line_id BIGINT AUTO_INCREMENT PRIMARY KEY,
    expense_voucher_id BIGINT NOT NULL,
    description TEXT,
    amount DECIMAL(12,2) NOT NULL,
    reference VARCHAR(100),
    reference_type VARCHAR(20),
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    INDEX idx_expense_voucher_id (expense_voucher_id),
    INDEX idx_reference (reference),
    INDEX idx_reference_type (reference_type),
    
    FOREIGN KEY (expense_voucher_id) REFERENCES expense_voucher(expense_voucher_id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- Thêm comment cho các bảng
ALTER TABLE expense_voucher COMMENT = 'Bảng phiếu chi';
ALTER TABLE expense_voucher_line COMMENT = 'Bảng chi tiết phiếu chi';

-- Thêm comment cho các cột quan trọng
ALTER TABLE expense_voucher MODIFY COLUMN voucher_number VARCHAR(50) NOT NULL UNIQUE COMMENT 'Số phiếu chi (format: PC20241201001)';
ALTER TABLE expense_voucher MODIFY COLUMN voucher_date DATETIME NOT NULL COMMENT 'Ngày phiếu chi';
ALTER TABLE expense_voucher MODIFY COLUMN total_amount DECIMAL(12,2) NOT NULL COMMENT 'Tổng số tiền';
ALTER TABLE expense_voucher MODIFY COLUMN status VARCHAR(20) NOT NULL DEFAULT 'PENDING' COMMENT 'Trạng thái: PENDING, APPROVED, REJECTED';
ALTER TABLE expense_voucher MODIFY COLUMN expense_type VARCHAR(20) NOT NULL DEFAULT 'RETURN_REFUND' COMMENT 'Loại chi: RETURN_REFUND, OPERATIONAL, SUPPLIER_PAYMENT, SALARY, MARKETING, OTHER';

ALTER TABLE expense_voucher_line MODIFY COLUMN amount DECIMAL(12,2) NOT NULL COMMENT 'Số tiền';
ALTER TABLE expense_voucher_line MODIFY COLUMN reference VARCHAR(100) COMMENT 'Tham chiếu (return_id, order_id, etc.)';
ALTER TABLE expense_voucher_line MODIFY COLUMN reference_type VARCHAR(20) COMMENT 'Loại tham chiếu: RETURN, ORDER, SUPPLIER, etc.';
