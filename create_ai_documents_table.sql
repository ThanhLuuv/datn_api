-- Script tạo bảng ai_documents cho AI Search
-- Chạy script này trực tiếp trong MySQL để tạo bảng

USE thienduong_LTDB;

CREATE TABLE IF NOT EXISTS `ai_documents` (
    `id` BIGINT NOT NULL AUTO_INCREMENT,
    `ref_type` VARCHAR(100) NOT NULL COMMENT 'Loại dữ liệu: book, order, customer, inventory, sales_insight',
    `ref_id` VARCHAR(120) NOT NULL COMMENT 'ID của record gốc (ISBN, OrderId, CustomerId, ...)',
    `content` TEXT NOT NULL COMMENT 'Nội dung text đã chuẩn hóa để embed',
    `embedding_json` JSON NOT NULL COMMENT 'Vector embedding từ Gemini (dạng JSON array)',
    `updated_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`id`),
    UNIQUE KEY `IX_ai_documents_ref_type_ref_id` (`ref_type`, `ref_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Kiểm tra bảng đã tạo thành công
SELECT 
    TABLE_NAME,
    TABLE_ROWS,
    CREATE_TIME
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = 'thienduong_LTDB' 
  AND TABLE_NAME = 'ai_documents';





