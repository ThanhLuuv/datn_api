-- Kiểm tra kiểu dữ liệu của cột isbn trong bảng book
DESCRIBE `book`;

-- Hoặc sử dụng query này để xem chi tiết hơn
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_SCHEMA = DATABASE() 
  AND TABLE_NAME = 'book' 
  AND COLUMN_NAME = 'isbn';
