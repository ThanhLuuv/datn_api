-- ============================================
-- Script cập nhật role name từ "EMPLOYEE" thành "SALES_EMPLOYEE"
-- Để thống nhất với SeedData.cs và các controllers
-- ============================================

-- Cập nhật role name cho role_id = 2
UPDATE `role` 
SET `name` = 'SALES_EMPLOYEE', 
    `description` = 'Nhân viên bán hàng'
WHERE `role_id` = 2 AND `name` = 'EMPLOYEE';

-- Kiểm tra kết quả
SELECT 
    role_id,
    name AS role_name,
    description
FROM `role`
WHERE role_id = 2;

-- ============================================
-- Lưu ý: Sau khi chạy script này, cần:
-- 1. Đăng xuất và đăng nhập lại để nhận token mới với role name đúng
-- 2. Hoặc có thể giữ nguyên "EMPLOYEE" nếu database đang dùng tên này
--    (các controllers đã được cập nhật để chấp nhận cả hai)
-- ============================================

