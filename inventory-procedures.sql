-- Stored procedures for inventory report and average price update (MySQL 8.0)
-- Assumptions:
-- - Current stock is maintained in table `book`.`stock`.
-- - Stock increases only via goods receipts (`goods_receipt` + `goods_receipt_line`).
-- - Stock decreases only when an invoice is paid (PaymentStatus = 'PAID'), via `order_line` quantities.
-- - Returns and order cancellations currently do not mutate stock in code, so they are not included in rewind math.
-- - `goods_receipt_line` does not carry `isbn`; we map its lines to `purchase_order_line` by aligning ordered rows within the same PO/GR.

DELIMITER $$

DROP PROCEDURE IF EXISTS SP_InventoryReport_AsOfDate $$
CREATE PROCEDURE SP_InventoryReport_AsOfDate(IN p_ReportDate DATE)
BEGIN
    /* Error handler */
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;

    /* Map goods receipt lines to isbn using row alignment within each PO/GR */
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

    /* Aggregate receipts after report date by isbn */
    DROP TEMPORARY TABLE IF EXISTS tmp_receipts_after;
    CREATE TEMPORARY TABLE tmp_receipts_after AS
    SELECT gm.isbn,
           SUM(COALESCE(gm.qty_received, 0)) AS qty_receipts_after
    FROM tmp_gr_map gm
    WHERE gm.received_at >= p_ReportDate
    GROUP BY gm.isbn;

    /* Aggregate sales (paid invoices) after report date by isbn */
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

    /* Aggregate approved/processed returns after report date by isbn
       Note: This rewind assumes returns increase current stock at approval/processing time */
    DROP TEMPORARY TABLE IF EXISTS tmp_returns_after;
    CREATE TEMPORARY TABLE tmp_returns_after AS
    SELECT ol.isbn,
           SUM(COALESCE(rl.qty_returned, 0)) AS qty_returns_after
    FROM `return` r
    JOIN return_line rl ON rl.return_id = r.return_id
    JOIN order_line ol ON ol.order_line_id = rl.order_line_id
    WHERE r.status IN (1, 3) /* Approved or Processed */
      AND r.processed_at IS NOT NULL
      AND DATE(r.processed_at) >= p_ReportDate
    GROUP BY ol.isbn;

    /* Compute stock as of date by rewinding from current */
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
    /* Variables */
    DECLARE v_sum_amount DECIMAL(18,2) DEFAULT 0;
    DECLARE v_sum_qty    DECIMAL(18,2) DEFAULT 0;
    DECLARE v_avg_price  DECIMAL(18,2) DEFAULT 0;

    /* Error handler */
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;

    /* Map GR lines to isbn as above */
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

    /* Identify last 4 receipts that include this isbn */
    DROP TEMPORARY TABLE IF EXISTS tmp_last4_receipts;
    CREATE TEMPORARY TABLE tmp_last4_receipts AS
    SELECT gm.gr_id,
           MAX(gm.received_at) AS received_at
    FROM tmp_gr_map2 gm
    GROUP BY gm.gr_id
    ORDER BY received_at DESC
    LIMIT 4;

    /* Sum amount and quantity for those receipts restricted to isbn */
    SELECT COALESCE(SUM(gm.qty_received * gm.unit_cost), 0),
           COALESCE(SUM(gm.qty_received), 0)
      INTO v_sum_amount, v_sum_qty
    FROM tmp_gr_map2 gm
    JOIN tmp_last4_receipts r ON r.gr_id = gm.gr_id;

    /* Compute weighted average price */
    SET v_avg_price = COALESCE(ROUND(v_sum_amount / NULLIF(v_sum_qty, 0), 0), 0);

    /* Update book.average_price */
    UPDATE book
       SET average_price = v_avg_price,
           updated_at = CURRENT_TIMESTAMP(6)
     WHERE isbn = p_Isbn;

    /* Return value for logging */
    SELECT v_avg_price AS AveragePrice;

    COMMIT;
END $$

DELIMITER ;


