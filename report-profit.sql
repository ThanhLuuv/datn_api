-- MySQL stored procedure: report_profit
-- Calculates revenue, cost (based on book.average_price), expenses (approved vouchers), and profit
-- Usage:
--   CALL report_profit('2025-01-01', '2025-01-31');

DELIMITER $$
DROP PROCEDURE IF EXISTS report_profit $$
CREATE PROCEDURE report_profit(IN p_from_date DATETIME, IN p_to_date DATETIME)
BEGIN
  DECLARE v_from DATETIME;
  DECLARE v_to DATETIME;

  SET v_from = IFNULL(p_from_date, DATE_SUB(UTC_TIMESTAMP(), INTERVAL 30 DAY));
  SET v_to = IFNULL(p_to_date, UTC_TIMESTAMP());

  /*
    More realistic profit model:
    - Revenue: sum of PAID invoices paid within window
    - Refunds: sum of REFUNDED invoices updated within window (subtract from revenue)
    - COGS: sum of book average price * qty for orders whose invoices are PAID in window
      and minus COGS of refunded invoices updated in window
    - Operating expenses: approved expense vouchers in window excluding refund types
      (RETURN_REFUND, ORDER_REFUND)
  */

  SELECT
    v_from AS from_date,
    v_to   AS to_date,
    -- Orders count considered as number of PAID invoices in period
    (SELECT COUNT(*)
       FROM invoice i
      WHERE i.payment_status = 'PAID' AND i.paid_at IS NOT NULL
        AND i.paid_at >= v_from AND i.paid_at <= v_to) AS orders_count,

    -- Revenue recognized on payment date
    (SELECT COALESCE(SUM(i.total_amount), 0)
       FROM invoice i
      WHERE i.payment_status = 'PAID' AND i.paid_at IS NOT NULL
        AND i.paid_at >= v_from AND i.paid_at <= v_to)
    -
    -- Refunds reduce revenue (recognized on refund update date)
    (SELECT COALESCE(SUM(i.total_amount), 0)
       FROM invoice i
      WHERE i.payment_status = 'REFUNDED'
        AND i.updated_at >= v_from AND i.updated_at <= v_to) AS revenue,

    -- COGS for paid invoices within period
    (SELECT COALESCE(SUM(ol.qty * b.average_price), 0)
       FROM invoice i
       JOIN `order` o ON o.order_id = i.order_id
       JOIN order_line ol ON ol.order_id = o.order_id
       JOIN book b ON b.isbn = ol.isbn
      WHERE i.payment_status = 'PAID' AND i.paid_at IS NOT NULL
        AND i.paid_at >= v_from AND i.paid_at <= v_to)
    -
    -- Less: COGS associated with refunded invoices in period
    (SELECT COALESCE(SUM(ol.qty * b.average_price), 0)
       FROM invoice i
       JOIN `order` o ON o.order_id = i.order_id
       JOIN order_line ol ON ol.order_id = o.order_id
       JOIN book b ON b.isbn = ol.isbn
      WHERE i.payment_status = 'REFUNDED'
        AND i.updated_at >= v_from AND i.updated_at <= v_to) AS cost_of_goods,

    -- Operating expenses (exclude refund vouchers)
    (SELECT COALESCE(SUM(ev.total_amount), 0)
       FROM expense_voucher ev
      WHERE ev.status = 'APPROVED'
        AND ev.voucher_date >= v_from AND ev.voucher_date <= v_to
        AND (ev.expense_type IS NULL OR ev.expense_type NOT IN ('RETURN_REFUND','ORDER_REFUND'))) AS operating_expenses,

    -- Profit = Revenue - COGS - OPEX (with revenue and cogs already net of refunds)
    (
      (
        (SELECT COALESCE(SUM(i.total_amount), 0)
           FROM invoice i
          WHERE i.payment_status = 'PAID' AND i.paid_at IS NOT NULL
            AND i.paid_at >= v_from AND i.paid_at <= v_to)
        -
        (SELECT COALESCE(SUM(i.total_amount), 0)
           FROM invoice i
          WHERE i.payment_status = 'REFUNDED'
            AND i.updated_at >= v_from AND i.updated_at <= v_to)
      )
      -
      (
        (SELECT COALESCE(SUM(ol.qty * b.average_price), 0)
           FROM invoice i
           JOIN `order` o ON o.order_id = i.order_id
           JOIN order_line ol ON ol.order_id = o.order_id
           JOIN book b ON b.isbn = ol.isbn
          WHERE i.payment_status = 'PAID' AND i.paid_at IS NOT NULL
            AND i.paid_at >= v_from AND i.paid_at <= v_to)
        -
        (SELECT COALESCE(SUM(ol.qty * b.average_price), 0)
           FROM invoice i
           JOIN `order` o ON o.order_id = i.order_id
           JOIN order_line ol ON ol.order_id = o.order_id
           JOIN book b ON b.isbn = ol.isbn
          WHERE i.payment_status = 'REFUNDED'
            AND i.updated_at >= v_from AND i.updated_at <= v_to)
      )
      -
      (
        (SELECT COALESCE(SUM(ev.total_amount), 0)
           FROM expense_voucher ev
          WHERE ev.status = 'APPROVED'
            AND ev.voucher_date >= v_from AND ev.voucher_date <= v_to
            AND (ev.expense_type IS NULL OR ev.expense_type NOT IN ('RETURN_REFUND','ORDER_REFUND')))
      )
    ) AS profit;
END $$
DELIMITER ;


