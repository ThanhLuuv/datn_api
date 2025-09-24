-- Add stock column to book table and backfill default
USE datn;

ALTER TABLE book
ADD COLUMN IF NOT EXISTS stock INT NOT NULL DEFAULT 0;

-- Optional: initialize stock based on any existing goods receipts if desired (kept simple -> 0)
-- You can compute from receipts if data exists.
