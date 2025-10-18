-- Aggregate Stored Procedures for BookStore (MySQL 8.0)
-- Run this script to create or replace all required stored procedures.

-- NOTE: Ensure schema objects (tables, views) exist before running.

/* ============================
   Inventory & Average Price SPs
   ============================ */
DELIMITER $$
DROP PROCEDURE IF EXISTS SP_InventoryReport_AsOfDate $$
CREATE PROCEDURE SP_InventoryReport_AsOfDate(IN p_ReportDate DATE)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;
    START TRANSACTION;

    DROP TEMPORARY TABLE IF EXISTS tmp_gr_map;
    CREATE TEMPORARY TABLE tmp_gr_map AS
    SELECT m.gr_id,
           m.received_at,
           m.gr_line_id,
           m.qty_received,
           m.unit_cost,
           m.isbn
    FROM (
        SELECT gr.gr_id,
               gr.received_at,
               grl.gr_line_id,
               grl.qty_received,
               grl.unit_cost,
               pol.isbn,
               ROW_NUMBER() OVER (PARTITION BY gr.gr_id ORDER BY grl.gr_line_id) AS rn_gr,
               ROW_NUMBER() OVER (PARTITION BY gr.po_id ORDER BY pol.po_line_id)   AS rn_po
        FROM goods_receipt gr
        JOIN goods_receipt_line grl ON grl.gr_id = gr.gr_id
        JOIN purchase_order po ON po.po_id = gr.po_id
        JOIN purchase_order_line pol ON pol.po_id = po.po_id
    ) m
    WHERE m.rn_gr = m.rn_po;

    DROP TEMPORARY TABLE IF EXISTS tmp_receipts_after;
    CREATE TEMPORARY TABLE tmp_receipts_after AS
    SELECT gm.isbn,
           SUM(COALESCE(gm.qty_received, 0)) AS qty_receipts_after
    FROM tmp_gr_map gm
    WHERE gm.received_at >= p_ReportDate
    GROUP BY gm.isbn;

    DROP TEMPORARY TABLE IF EXISTS tmp_sales_after;
    CREATE TEMPORARY TABLE tmp_sales_after AS
    SELECT ol.isbn,
           SUM(COALESCE(ol.qty, 0)) AS qty_sales_after
    FROM invoice i
    JOIN `order` o ON o.order_id = i.order_id
    JOIN order_line ol ON ol.order_id = o.order_id
    WHERE i.payment_status = 'PAID'
      AND i.paid_at IS NOT NULL
      AND DATE(i.paid_at) >= p_ReportDate
    GROUP BY ol.isbn;

    DROP TEMPORARY TABLE IF EXISTS tmp_returns_after;
    CREATE TEMPORARY TABLE tmp_returns_after AS
    SELECT ol.isbn,
           SUM(COALESCE(rl.qty_returned, 0)) AS qty_returns_after
    FROM `return` r
    JOIN return_line rl ON rl.return_id = r.return_id
    JOIN order_line ol ON ol.order_line_id = rl.order_line_id
    WHERE r.status IN (1, 3)
      AND r.processed_at IS NOT NULL
      AND DATE(r.processed_at) >= p_ReportDate
    GROUP BY ol.isbn;

    SELECT c.name                           AS `Category`,
           b.isbn                           AS `ISBN`,
           b.title                          AS `Title`,
           (COALESCE(b.stock, 0)
            - COALESCE(r.qty_receipts_after, 0)
            + COALESCE(s.qty_sales_after, 0)
            - COALESCE(ret.qty_returns_after, 0)) AS `QuantityOnHand`,
           b.average_price                  AS `AveragePrice`
    FROM book b
    JOIN category c ON c.category_id = b.category_id
    LEFT JOIN tmp_receipts_after r ON r.isbn = b.isbn
    LEFT JOIN tmp_sales_after s ON s.isbn = b.isbn
    LEFT JOIN tmp_returns_after ret ON ret.isbn = b.isbn
    WHERE b.status = TRUE
    HAVING `QuantityOnHand` >= 0
    ORDER BY c.name ASC, b.isbn ASC;

    COMMIT;
END $$

DROP PROCEDURE IF EXISTS SP_UpdateAveragePrice_Last4Receipts $$
CREATE PROCEDURE SP_UpdateAveragePrice_Last4Receipts(IN p_Isbn VARCHAR(20))
BEGIN
    DECLARE v_sum_amount DECIMAL(18,2) DEFAULT 0;
    DECLARE v_sum_qty    DECIMAL(18,2) DEFAULT 0;
    DECLARE v_avg_price  DECIMAL(18,2) DEFAULT 0;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;
    START TRANSACTION;

    DROP TEMPORARY TABLE IF EXISTS tmp_gr_map2;
    CREATE TEMPORARY TABLE tmp_gr_map2 AS
    SELECT m.gr_id,
           m.received_at,
           m.gr_line_id,
           m.qty_received,
           m.unit_cost,
           m.isbn
    FROM (
        SELECT gr.gr_id,
               gr.received_at,
               grl.gr_line_id,
               grl.qty_received,
               grl.unit_cost,
               pol.isbn,
               ROW_NUMBER() OVER (PARTITION BY gr.gr_id ORDER BY grl.gr_line_id) AS rn_gr,
               ROW_NUMBER() OVER (PARTITION BY gr.po_id ORDER BY pol.po_line_id)   AS rn_po
        FROM goods_receipt gr
        JOIN goods_receipt_line grl ON grl.gr_id = gr.gr_id
        JOIN purchase_order po ON po.po_id = gr.po_id
        JOIN purchase_order_line pol ON pol.po_id = po.po_id
        WHERE pol.isbn = p_Isbn
    ) m
    WHERE m.rn_gr = m.rn_po;

    DROP TEMPORARY TABLE IF EXISTS tmp_last4_receipts;
    CREATE TEMPORARY TABLE tmp_last4_receipts AS
    SELECT gm.gr_id,
           MAX(gm.received_at) AS received_at
    FROM tmp_gr_map2 gm
    GROUP BY gm.gr_id
    ORDER BY received_at DESC
    LIMIT 4;

    SELECT COALESCE(SUM(gm.qty_received * gm.unit_cost), 0),
           COALESCE(SUM(gm.qty_received), 0)
      INTO v_sum_amount, v_sum_qty
    FROM tmp_gr_map2 gm
    JOIN tmp_last4_receipts r ON r.gr_id = gm.gr_id;

    SET v_avg_price = COALESCE(ROUND(v_sum_amount / NULLIF(v_sum_qty, 0), 0), 0);

    UPDATE book
       SET average_price = v_avg_price,
           updated_at = CURRENT_TIMESTAMP(6)
     WHERE isbn = p_Isbn;

    SELECT v_avg_price AS AveragePrice;

    COMMIT;
END $$
DELIMITER ;

/* ============================
   Revenue Reporting SPs
   ============================ */
DELIMITER $$
DROP PROCEDURE IF EXISTS sp_revenue_by_month $$
CREATE PROCEDURE sp_revenue_by_month(IN p_from_date DATE, IN p_to_date DATE)
BEGIN
    SELECT YEAR(o.placed_at) AS `Year`,
           MONTH(o.placed_at) AS `Month`,
           COALESCE(SUM(ol.qty * ol.unit_price), 0) AS `Revenue`
    FROM `order` o
    JOIN `order_line` ol ON o.order_id = ol.order_id
    WHERE o.status = 2
      AND o.placed_at >= p_from_date
      AND o.placed_at < DATE_ADD(p_to_date, INTERVAL 1 DAY)
    GROUP BY YEAR(o.placed_at), MONTH(o.placed_at)
    ORDER BY `Year`, `Month`;
END $$

DROP PROCEDURE IF EXISTS sp_revenue_by_quarter $$
CREATE PROCEDURE sp_revenue_by_quarter(IN p_from_date DATE, IN p_to_date DATE)
BEGIN
    SELECT YEAR(o.placed_at) AS `Year`,
           QUARTER(o.placed_at) AS `Quarter`,
           COALESCE(SUM(ol.qty * ol.unit_price), 0) AS `Revenue`
    FROM `order` o
    JOIN `order_line` ol ON o.order_id = ol.order_id
    WHERE o.status = 2
      AND o.placed_at >= p_from_date
      AND o.placed_at < DATE_ADD(p_to_date, INTERVAL 1 DAY)
    GROUP BY YEAR(o.placed_at), QUARTER(o.placed_at)
    ORDER BY `Year`, `Quarter`;
END $$
DELIMITER ;

/* ============================
   Book Search & Promotions SPs
   ============================ */
-- Recreate from book-search-system.sql (kept as SOURCE to avoid duplication and ensure views present)
SOURCE book-search-system.sql;

/* ============================
   Seed Utility SPs (dev/test only)
   ============================ */
-- Recreate seed_orders; will also call and drop inside its own file
SOURCE orders-seed.sql;
