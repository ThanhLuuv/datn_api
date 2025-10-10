-- Stored Procedure: sp_revenue_by_month
-- Params: IN p_from_date DATE, IN p_to_date DATE
-- Returns: rows (Year INT, Month INT, Revenue DECIMAL)
DELIMITER $$
CREATE PROCEDURE sp_revenue_by_month(IN p_from_date DATE, IN p_to_date DATE)
BEGIN
    -- Aggregate delivered orders by year-month within date range
    SELECT YEAR(o.placed_at) AS `Year`,
           MONTH(o.placed_at) AS `Month`,
           COALESCE(SUM(ol.qty * ol.unit_price), 0) AS `Revenue`
    FROM `order` o
    JOIN `order_line` ol ON o.order_id = ol.order_id
    -- Status uses integer enum in DB (0 Pending, 1 Assigned, 2 Delivered)
    WHERE o.status = 2
      AND o.placed_at >= p_from_date
      AND o.placed_at < DATE_ADD(p_to_date, INTERVAL 1 DAY)
    GROUP BY YEAR(o.placed_at), MONTH(o.placed_at)
    ORDER BY `Year`, `Month`;
END $$
DELIMITER ;

-- Stored Procedure: sp_revenue_by_quarter
-- Params: IN p_from_date DATE, IN p_to_date DATE
-- Returns: rows (Year INT, Quarter INT, Revenue DECIMAL)
DELIMITER $$
CREATE PROCEDURE sp_revenue_by_quarter(IN p_from_date DATE, IN p_to_date DATE)
BEGIN
    -- Aggregate delivered orders by quarter within date range
    SELECT YEAR(o.placed_at) AS `Year`,
           QUARTER(o.placed_at) AS `Quarter`,
           COALESCE(SUM(ol.qty * ol.unit_price), 0) AS `Revenue`
    FROM `order` o
    JOIN `order_line` ol ON o.order_id = ol.order_id
    -- Status uses integer enum in DB (0 Pending, 1 Assigned, 2 Delivered)
    WHERE o.status = 2
      AND o.placed_at >= p_from_date
      AND o.placed_at < DATE_ADD(p_to_date, INTERVAL 1 DAY)
    GROUP BY YEAR(o.placed_at), QUARTER(o.placed_at)
    ORDER BY `Year`, `Quarter`;
END $$
DELIMITER ;

-- Area and employee relation
-- Create area table
CREATE TABLE IF NOT EXISTS `area` (
  `area_id` BIGINT NOT NULL AUTO_INCREMENT,
  `name` VARCHAR(150) NOT NULL,
  `keywords` VARCHAR(300) NULL,
  PRIMARY KEY (`area_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Add area_id to employee if not exists
ALTER TABLE `employee`
  ADD COLUMN IF NOT EXISTS `area_id` BIGINT NULL,
  ADD CONSTRAINT `fk_emp_area` FOREIGN KEY (`area_id`) REFERENCES `area`(`area_id`);

-- Helper: Create invoice for a given order (example for order_id = 15)
-- Assumes order_line exists and unit_price/qty are set
-- Recompute total and tax (10%)
SET @orderId := 15;
SET @total := (
  SELECT COALESCE(SUM(ol.qty * ol.unit_price), 0)
  FROM `order_line` ol WHERE ol.order_id = @orderId
);
SET @tax := ROUND(@total * 0.10, 2);

INSERT INTO `invoice` (order_id, created_at, total_amount, tax_amount)
VALUES (@orderId, UTC_TIMESTAMP(), @total, @tax);
-- Create payment_transaction table for storing payment operations
USE datn;

CREATE TABLE IF NOT EXISTS payment_transaction (
  transaction_id BIGINT NOT NULL AUTO_INCREMENT,
  order_id BIGINT NOT NULL,
  amount DECIMAL(18,2) NOT NULL,
  currency VARCHAR(10) NOT NULL DEFAULT 'VND',
  provider VARCHAR(50) NOT NULL DEFAULT 'PAYOS',
  provider_txn_id VARCHAR(191) NULL,
  status VARCHAR(50) NOT NULL DEFAULT 'PENDING',
  return_url VARCHAR(500) NULL,
  checkout_url VARCHAR(500) NULL,
  raw_request JSON NULL,
  raw_response JSON NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (transaction_id),
  INDEX ix_payment_txn_order_id (order_id),
  CONSTRAINT fk_payment_txn_order FOREIGN KEY (order_id) REFERENCES `order`(order_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;


