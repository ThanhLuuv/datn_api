-- Price System Migration
-- This script restructures the price system to use dynamic pricing from price_change table

-- 1. Create new price_change table with proper structure
CREATE TABLE IF NOT EXISTS `price_change` (
    `price_change_id` BIGINT NOT NULL AUTO_INCREMENT,
    `isbn` VARCHAR(20) NOT NULL,
    `old_price` DECIMAL(12,2) NOT NULL,
    `new_price` DECIMAL(12,2) NOT NULL,
    `effective_date` DATETIME NOT NULL,
    `changed_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `employee_id` BIGINT NOT NULL,
    `reason` VARCHAR(500) NULL,
    `is_active` BOOLEAN NOT NULL DEFAULT TRUE,
    PRIMARY KEY (`price_change_id`),
    INDEX `idx_price_change_isbn` (`isbn`),
    INDEX `idx_price_change_effective_date` (`effective_date`),
    INDEX `idx_price_change_active` (`is_active`),
    CONSTRAINT `fk_price_change_book` FOREIGN KEY (`isbn`) REFERENCES `book` (`isbn`) ON DELETE CASCADE,
    CONSTRAINT `fk_price_change_employee` FOREIGN KEY (`employee_id`) REFERENCES `employee` (`employee_id`) ON DELETE CASCADE
);

-- 2. Add average_price column to book table
ALTER TABLE `book` 
ADD COLUMN `average_price` DECIMAL(12,2) NOT NULL DEFAULT 0.00 AFTER `page_count`;

-- 3. Migrate existing unit_price data to average_price
UPDATE `book` SET `average_price` = `unit_price`;

-- 4. Create initial price_change records for all existing books
INSERT INTO `price_change` (`isbn`, `old_price`, `new_price`, `effective_date`, `changed_at`, `employee_id`, `reason`, `is_active`)
SELECT 
    `isbn`,
    0.00 as `old_price`,
    `unit_price` as `new_price`,
    `created_at` as `effective_date`,
    `created_at` as `changed_at`,
    1 as `employee_id`, -- Assuming admin employee_id = 1
    'Initial price setup' as `reason`,
    TRUE as `is_active`
FROM `book`
WHERE `unit_price` > 0;

-- 5. Update BookStoreDbContext to include PriceChange entity
-- This will be handled in the C# code

-- 6. Create stored procedure to get current price for a book
DELIMITER //
CREATE PROCEDURE `sp_get_current_book_price`(
    IN p_isbn VARCHAR(20),
    IN p_date DATETIME
)
BEGIN
    DECLARE current_price DECIMAL(12,2) DEFAULT 0.00;
    
    -- Get the most recent active price change for the book
    SELECT pc.new_price INTO current_price
    FROM price_change pc
    WHERE pc.isbn = p_isbn 
      AND pc.is_active = TRUE
      AND pc.effective_date <= IFNULL(p_date, NOW())
    ORDER BY pc.effective_date DESC, pc.changed_at DESC
    LIMIT 1;
    
    -- If no price change found, return average price from book table
    IF current_price = 0.00 THEN
        SELECT b.average_price INTO current_price
        FROM book b
        WHERE b.isbn = p_isbn;
    END IF;
    
    SELECT current_price as current_price;
END //
DELIMITER ;

-- 7. Create stored procedure to update book average price
DELIMITER //
CREATE PROCEDURE `sp_update_book_average_price`(
    IN p_isbn VARCHAR(20)
)
BEGIN
    DECLARE avg_price DECIMAL(12,2) DEFAULT 0.00;
    
    -- Calculate average price from all price changes
    SELECT AVG(pc.new_price) INTO avg_price
    FROM price_change pc
    WHERE pc.isbn = p_isbn 
      AND pc.is_active = TRUE;
    
    -- Update book average price
    UPDATE book 
    SET average_price = IFNULL(avg_price, 0.00),
        updated_at = NOW()
    WHERE isbn = p_isbn;
    
    SELECT ROW_COUNT() as affected_rows;
END //
DELIMITER ;

-- 8. Create trigger to update average price when price changes
DELIMITER //
CREATE TRIGGER `tr_price_change_update_average` 
AFTER INSERT ON `price_change`
FOR EACH ROW
BEGIN
    CALL sp_update_book_average_price(NEW.isbn);
END //
DELIMITER ;

-- 9. Create view for books with current prices
CREATE VIEW `v_books_with_current_price` AS
SELECT 
    b.isbn,
    b.title,
    b.page_count,
    b.average_price,
    b.publish_year,
    b.category_id,
    b.publisher_id,
    b.created_at,
    b.updated_at,
    b.image_url,
    b.stock,
    b.status,
    COALESCE(
        (SELECT pc.new_price 
         FROM price_change pc 
         WHERE pc.isbn = b.isbn 
           AND pc.is_active = TRUE 
           AND pc.effective_date <= NOW()
         ORDER BY pc.effective_date DESC, pc.changed_at DESC 
         LIMIT 1),
        b.average_price
    ) as current_price
FROM book b;

-- 10. Update existing order_line and purchase_order_line to use current prices
-- Note: This is for reference only - actual price should be captured at order time
-- The unit_price in these tables should remain as historical data

-- 11. Add indexes for better performance
CREATE INDEX `idx_price_change_isbn_effective` ON `price_change` (`isbn`, `effective_date`, `is_active`);
CREATE INDEX `idx_price_change_employee` ON `price_change` (`employee_id`);

-- 12. Sample data for testing
-- Insert some sample price changes for testing
INSERT INTO `price_change` (`isbn`, `old_price`, `new_price`, `effective_date`, `changed_at`, `employee_id`, `reason`, `is_active`)
VALUES 
    ('978-604-1-00003-4', 100000.00, 95000.00, '2025-01-01 00:00:00', NOW(), 1, 'New Year promotion', TRUE),
    ('978-604-1-00003-5', 120000.00, 110000.00, '2025-01-15 00:00:00', NOW(), 1, 'Mid-month discount', TRUE),
    ('978-605-1-00001', 80000.00, 75000.00, '2025-02-01 00:00:00', NOW(), 1, 'February sale', TRUE);

-- 13. Update average prices for books with price changes
CALL sp_update_book_average_price('978-604-1-00003-4');
CALL sp_update_book_average_price('978-604-1-00003-5');
CALL sp_update_book_average_price('978-605-1-00001');

-- Migration completed successfully
SELECT 'Price system migration completed successfully' as status;



