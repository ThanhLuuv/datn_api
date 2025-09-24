-- Add status column (0/1) to book table
USE datn;

ALTER TABLE book
ADD COLUMN IF NOT EXISTS status TINYINT(1) NOT NULL DEFAULT 1;
