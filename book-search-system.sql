-- SQL script to create views and stored procedures for the new book system
-- This script should be run after the price-system-migration.sql

-- Create view for books with current prices
CREATE OR REPLACE VIEW v_books_with_current_price AS
SELECT 
    b.isbn,
    b.title,
    b.page_count,
    b.average_price,
    COALESCE(pc.new_price, b.average_price) AS current_price,
    b.publish_year,
    b.category_id,
    c.name AS category_name,
    b.publisher_id,
    p.name AS publisher_name,
    b.image_url,
    b.created_at,
    b.updated_at,
    b.stock,
    b.status
FROM book b
LEFT JOIN category c ON b.category_id = c.category_id
LEFT JOIN publisher p ON b.publisher_id = p.publisher_id
LEFT JOIN (
    SELECT 
        pc1.isbn,
        pc1.new_price,
        ROW_NUMBER() OVER (
            PARTITION BY pc1.isbn 
            ORDER BY pc1.effective_date DESC, pc1.changed_at DESC
        ) as rn
    FROM price_change pc1
    WHERE pc1.is_active = 1 
    AND pc1.effective_date <= NOW()
) pc ON b.isbn = pc.isbn AND pc.rn = 1;

-- Create stored procedure to get books with promotions
DELIMITER //
CREATE PROCEDURE sp_get_books_with_promotions(IN p_limit INT)
BEGIN
    SELECT 
        b.isbn,
        b.title,
        b.page_count,
        b.average_price,
        COALESCE(pc.new_price, b.average_price) AS current_price,
        COALESCE(pc.new_price, b.average_price) * (1 - pr.discount_pct / 100) AS discounted_price,
        b.publish_year,
        b.category_id,
        c.name AS category_name,
        b.publisher_id,
        p.name AS publisher_name,
        b.image_url,
        b.created_at,
        b.updated_at,
        b.stock,
        b.status,
        pr.promotion_id,
        pr.name AS promotion_name,
        pr.discount_pct,
        pr.start_date,
        pr.end_date
    FROM book b
    LEFT JOIN category c ON b.category_id = c.category_id
    LEFT JOIN publisher p ON b.publisher_id = p.publisher_id
    LEFT JOIN (
        SELECT 
            pc1.isbn,
            pc1.new_price,
            ROW_NUMBER() OVER (
                PARTITION BY pc1.isbn 
                ORDER BY pc1.effective_date DESC, pc1.changed_at DESC
            ) as rn
        FROM price_change pc1
        WHERE pc1.is_active = 1 
        AND pc1.effective_date <= NOW()
    ) pc ON b.isbn = pc.isbn AND pc.rn = 1
    INNER JOIN book_promotion bp ON b.isbn = bp.isbn
    INNER JOIN promotion pr ON bp.promotion_id = pr.promotion_id
    WHERE pr.start_date <= NOW() 
    AND pr.end_date >= NOW()
    ORDER BY pr.discount_pct DESC
    LIMIT p_limit;
END //
DELIMITER ;

-- Create stored procedure to get best selling books
DELIMITER //
CREATE PROCEDURE sp_get_best_selling_books(IN p_limit INT)
BEGIN
    SELECT 
        b.isbn,
        b.title,
        b.page_count,
        b.average_price,
        COALESCE(pc.new_price, b.average_price) AS current_price,
        b.publish_year,
        b.category_id,
        c.name AS category_name,
        b.publisher_id,
        p.name AS publisher_name,
        b.image_url,
        b.created_at,
        b.updated_at,
        b.stock,
        b.status,
        SUM(ol.qty) AS total_sold
    FROM book b
    LEFT JOIN category c ON b.category_id = c.category_id
    LEFT JOIN publisher p ON b.publisher_id = p.publisher_id
    LEFT JOIN (
        SELECT 
            pc1.isbn,
            pc1.new_price,
            ROW_NUMBER() OVER (
                PARTITION BY pc1.isbn 
                ORDER BY pc1.effective_date DESC, pc1.changed_at DESC
            ) as rn
        FROM price_change pc1
        WHERE pc1.is_active = 1 
        AND pc1.effective_date <= NOW()
    ) pc ON b.isbn = pc.isbn AND pc.rn = 1
    INNER JOIN order_line ol ON b.isbn = ol.isbn
    INNER JOIN `order` o ON ol.order_id = o.order_id
    WHERE o.status = 2 -- Delivered
      AND o.placed_at >= DATE_SUB(NOW(), INTERVAL 30 DAY)
    GROUP BY b.isbn, b.title, b.page_count, b.average_price, pc.new_price, 
             b.publish_year, b.category_id, c.name, b.publisher_id, p.name, 
             b.image_url, b.created_at, b.updated_at, b.stock, b.status
    ORDER BY total_sold DESC
    LIMIT p_limit;
END //
DELIMITER ;

-- Create stored procedure to get latest books
DELIMITER //
CREATE PROCEDURE sp_get_latest_books(IN p_limit INT)
BEGIN
    SELECT 
        b.isbn,
        b.title,
        b.page_count,
        b.average_price,
        COALESCE(pc.new_price, b.average_price) AS current_price,
        b.publish_year,
        b.category_id,
        c.name AS category_name,
        b.publisher_id,
        p.name AS publisher_name,
        b.image_url,
        b.created_at,
        b.updated_at,
        b.stock,
        b.status
    FROM book b
    LEFT JOIN category c ON b.category_id = c.category_id
    LEFT JOIN publisher p ON b.publisher_id = p.publisher_id
    LEFT JOIN (
        SELECT 
            pc1.isbn,
            pc1.new_price,
            ROW_NUMBER() OVER (
                PARTITION BY pc1.isbn 
                ORDER BY pc1.effective_date DESC, pc1.changed_at DESC
            ) as rn
        FROM price_change pc1
        WHERE pc1.is_active = 1 
        AND pc1.effective_date <= NOW()
    ) pc ON b.isbn = pc.isbn AND pc.rn = 1
    ORDER BY b.created_at DESC
    LIMIT p_limit;
END //
DELIMITER ;

-- Create stored procedure to search books with advanced filtering
DELIMITER //
CREATE PROCEDURE sp_search_books(
    IN p_search_term VARCHAR(500),
    IN p_category_id BIGINT,
    IN p_publisher_id BIGINT,
    IN p_min_price DECIMAL(12,2),
    IN p_max_price DECIMAL(12,2),
    IN p_min_year INT,
    IN p_max_year INT,
    IN p_sort_by VARCHAR(50),
    IN p_sort_direction VARCHAR(10),
    IN p_page_number INT,
    IN p_page_size INT
)
BEGIN
    DECLARE v_offset INT DEFAULT 0;
    SET v_offset = (p_page_number - 1) * p_page_size;
    
    SET @sql = CONCAT('
        SELECT 
            b.isbn,
            b.title,
            b.page_count,
            b.average_price,
            COALESCE(pc.new_price, b.average_price) AS current_price,
            b.publish_year,
            b.category_id,
            c.name AS category_name,
            b.publisher_id,
            p.name AS publisher_name,
            b.image_url,
            b.created_at,
            b.updated_at,
            b.stock,
            b.status
        FROM book b
        LEFT JOIN category c ON b.category_id = c.category_id
        LEFT JOIN publisher p ON b.publisher_id = p.publisher_id
        LEFT JOIN (
            SELECT 
                pc1.isbn,
                pc1.new_price,
                ROW_NUMBER() OVER (
                    PARTITION BY pc1.isbn 
                    ORDER BY pc1.effective_date DESC, pc1.changed_at DESC
                ) as rn
            FROM price_change pc1
            WHERE pc1.is_active = 1 
            AND pc1.effective_date <= NOW()
        ) pc ON b.isbn = pc.isbn AND pc.rn = 1
        WHERE 1=1');
    
    -- Add search conditions
    IF p_search_term IS NOT NULL AND p_search_term != '' THEN
        SET @sql = CONCAT(@sql, ' AND (b.title LIKE CONCAT(''%'', ?, ''%'') 
                                    OR b.isbn LIKE CONCAT(''%'', ?, ''%'')
                                    OR EXISTS (
                                        SELECT 1 FROM author_book ab 
                                        INNER JOIN author a ON ab.author_id = a.author_id 
                                        WHERE ab.isbn = b.isbn 
                                        AND (a.first_name LIKE CONCAT(''%'', ?, ''%'') 
                                             OR a.last_name LIKE CONCAT(''%'', ?, ''%'')
                                             OR CONCAT(a.first_name, '' '', a.last_name) LIKE CONCAT(''%'', ?, ''%''))
                                    )
                                    OR c.name LIKE CONCAT(''%'', ?, ''%'')
                                    OR p.name LIKE CONCAT(''%'', ?, ''%''))');
    END IF;
    
    IF p_category_id IS NOT NULL THEN
        SET @sql = CONCAT(@sql, ' AND b.category_id = ?');
    END IF;
    
    IF p_publisher_id IS NOT NULL THEN
        SET @sql = CONCAT(@sql, ' AND b.publisher_id = ?');
    END IF;
    
    IF p_min_year IS NOT NULL THEN
        SET @sql = CONCAT(@sql, ' AND b.publish_year >= ?');
    END IF;
    
    IF p_max_year IS NOT NULL THEN
        SET @sql = CONCAT(@sql, ' AND b.publish_year <= ?');
    END IF;
    
    -- Add price filtering (will be applied after getting current prices)
    IF p_min_price IS NOT NULL THEN
        SET @sql = CONCAT(@sql, ' AND COALESCE(pc.new_price, b.average_price) >= ?');
    END IF;
    
    IF p_max_price IS NOT NULL THEN
        SET @sql = CONCAT(@sql, ' AND COALESCE(pc.new_price, b.average_price) <= ?');
    END IF;
    
    -- Add sorting
    CASE p_sort_by
        WHEN 'title' THEN
            IF p_sort_direction = 'desc' THEN
                SET @sql = CONCAT(@sql, ' ORDER BY b.title DESC');
            ELSE
                SET @sql = CONCAT(@sql, ' ORDER BY b.title ASC');
            END IF;
        WHEN 'price' THEN
            IF p_sort_direction = 'desc' THEN
                SET @sql = CONCAT(@sql, ' ORDER BY COALESCE(pc.new_price, b.average_price) DESC');
            ELSE
                SET @sql = CONCAT(@sql, ' ORDER BY COALESCE(pc.new_price, b.average_price) ASC');
            END IF;
        WHEN 'year' THEN
            IF p_sort_direction = 'desc' THEN
                SET @sql = CONCAT(@sql, ' ORDER BY b.publish_year DESC');
            ELSE
                SET @sql = CONCAT(@sql, ' ORDER BY b.publish_year ASC');
            END IF;
        WHEN 'created' THEN
            IF p_sort_direction = 'desc' THEN
                SET @sql = CONCAT(@sql, ' ORDER BY b.created_at DESC');
            ELSE
                SET @sql = CONCAT(@sql, ' ORDER BY b.created_at ASC');
            END IF;
        ELSE
            SET @sql = CONCAT(@sql, ' ORDER BY b.title ASC');
    END CASE;
    
    SET @sql = CONCAT(@sql, ' LIMIT ', v_offset, ', ', p_page_size);
    
    PREPARE stmt FROM @sql;
    
    -- Execute with parameters
    IF p_search_term IS NOT NULL AND p_search_term != '' THEN
        EXECUTE stmt USING p_search_term, p_search_term, p_search_term, p_search_term, p_search_term, p_search_term, p_search_term;
    ELSE
        EXECUTE stmt;
    END IF;
    
    DEALLOCATE PREPARE stmt;
END //
DELIMITER ;

-- Create index for better performance
CREATE INDEX idx_price_change_isbn_effective ON price_change(isbn, effective_date, is_active);
CREATE INDEX idx_book_promotion_promotion ON book_promotion(promotion_id);
CREATE INDEX idx_promotion_dates ON promotion(start_date, end_date);
CREATE INDEX idx_order_line_isbn ON order_line(isbn);
CREATE INDEX idx_order_status ON `order`(status);

-- Insert some sample promotions for testing
INSERT INTO promotion (name, description, discount_pct, start_date, end_date, created_at, updated_at) VALUES
('Black Friday Sale', 'Biggest sale of the year', 50.00, '2024-11-24', '2024-11-30', NOW(), NOW()),
('Summer Reading', 'Summer book promotion', 25.00, '2024-06-01', '2024-08-31', NOW(), NOW()),
('New Year Special', 'New year book promotion', 30.00, '2024-01-01', '2024-01-31', NOW(), NOW());

-- Add some books to promotions (assuming books exist)
-- You can run these after you have books in your database
-- INSERT INTO book_promotion (isbn, promotion_id) VALUES
-- ('978-604-1-00003-4', 1),
-- ('978-604-1-00003-5', 1),
-- ('978-605-1-00001', 2);
