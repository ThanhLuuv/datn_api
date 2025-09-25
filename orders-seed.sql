-- Seed random customer orders across months for revenue testing
-- Safe to run multiple times: it creates a procedure and drops it afterward

USE datn;

DELIMITER $$
DROP PROCEDURE IF EXISTS seed_orders $$
CREATE PROCEDURE seed_orders()
BEGIN
  DECLARE m INT DEFAULT 0;
  DECLARE i INT;
  DECLARE base_date DATE;
  DECLARE cust BIGINT;
  DECLARE isbn1 VARCHAR(20);
  DECLARE isbn2 VARCHAR(20);
  DECLARE isbn3 VARCHAR(20);
  
  -- pick 3 ISBNs from existing books
  SELECT b1.isbn, b2.isbn, b3.isbn
  INTO isbn1, isbn2, isbn3
  FROM (SELECT isbn FROM book LIMIT 1) b1,
       (SELECT isbn FROM book LIMIT 1 OFFSET 1) b2,
       (SELECT isbn FROM book LIMIT 1 OFFSET 2) b3;
  
  IF isbn1 IS NULL THEN SET isbn1 = '978-604-1-00001-1'; END IF;
  IF isbn2 IS NULL THEN SET isbn2 = '978-604-1-00002-2'; END IF;
  IF isbn3 IS NULL THEN SET isbn3 = '978-604-1-00003-3'; END IF;

  WHILE m < 12 DO
    SET base_date = DATE_SUB(CURDATE(), INTERVAL m MONTH);
    SET i = 0;
    WHILE i < 15 DO
      -- alternate customers 1..3 if exist
      SET cust = 1 + (i % 3);
      -- create order
      INSERT INTO `order` (customer_id, placed_at, receiver_name, receiver_phone, shipping_address, delivery_date, status)
      VALUES (cust,
              DATE_ADD(base_date, INTERVAL FLOOR(RAND()*27) DAY),
              CONCAT('Cust ', cust),
              '0900000000',
              '123 Test Street, HCMC',
              DATE_ADD(base_date, INTERVAL FLOOR(RAND()*27) DAY),
              2); -- Delivered
      
      -- last_insert_id
      SET @oid = LAST_INSERT_ID();
      
      -- add 2-3 lines per order
      INSERT INTO order_line (order_id, isbn, qty, unit_price) VALUES
        (@oid, IF(i % 3 = 0, isbn1, IF(i % 3 = 1, isbn2, isbn3)), 1 + FLOOR(RAND()*4), 50000 + FLOOR(RAND()*5)*10000),
        (@oid, IF(i % 2 = 0, isbn2, isbn3), 1 + FLOOR(RAND()*3), 60000 + FLOOR(RAND()*5)*10000);
      
      IF (i % 5 = 0) THEN
        INSERT INTO order_line (order_id, isbn, qty, unit_price)
        VALUES (@oid, isbn1, 1 + FLOOR(RAND()*2), 70000 + FLOOR(RAND()*5)*10000);
      END IF;
      
      SET i = i + 1;
    END WHILE;
    SET m = m + 1;
  END WHILE;
END $$
DELIMITER ;

CALL seed_orders();
DROP PROCEDURE IF EXISTS seed_orders;


