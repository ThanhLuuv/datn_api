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


