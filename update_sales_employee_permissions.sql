-- ============================================
-- Script cập nhật permissions cho SALES_EMPLOYEE (role_id = 2)
-- Cập nhật để SALES_EMPLOYEE có tất cả quyền như ADMIN trừ:
-- - READ_EMPLOYEE (34) - Xem danh sách nhân viên
-- - WRITE_EMPLOYEE (35) - Tạo, sửa, xóa nhân viên
-- - READ_ROLE (40) - Xem danh sách vai trò
-- - WRITE_ROLE (41) - Tạo, sửa vai trò
-- - ASSIGN_PERMISSION (42) - Gán quyền cho vai trò
-- ============================================

-- Bước 1: Xóa tất cả permissions hiện tại của SALES_EMPLOYEE
DELETE FROM `role_permission` WHERE `role_id` = 2;

-- Bước 2: Thêm lại permissions mới cho SALES_EMPLOYEE
-- Tất cả permissions từ 1 đến 42, trừ 34, 35, 40, 41, 42 (quản lý nhân viên và quyền)
INSERT INTO `role_permission` (`role_id`, `permission_id`) VALUES
(2, 1), (2, 2), (2, 3), (2, 4), (2, 5), (2, 6), (2, 7), (2, 8), (2, 9), (2, 10),
(2, 11), (2, 12), (2, 13), (2, 14), (2, 15), (2, 16), (2, 17), (2, 18), (2, 19), (2, 20),
(2, 21), (2, 22), (2, 23), (2, 24), (2, 25), (2, 26), (2, 27), (2, 28), (2, 29), (2, 30),
(2, 31), (2, 32), (2, 33), (2, 36), (2, 37), (2, 38), (2, 39);
-- Không có: 34 (READ_EMPLOYEE), 35 (WRITE_EMPLOYEE), 40 (READ_ROLE), 41 (WRITE_ROLE), 42 (ASSIGN_PERMISSION)

-- Bước 3: Kiểm tra kết quả
SELECT 
    r.role_id,
    r.name AS role_name,
    COUNT(rp.permission_id) AS permission_count,
    GROUP_CONCAT(p.code ORDER BY p.permission_id SEPARATOR ', ') AS permissions
FROM `role` r
LEFT JOIN `role_permission` rp ON r.role_id = rp.role_id
LEFT JOIN `permission` p ON rp.permission_id = p.permission_id
WHERE r.role_id = 2
GROUP BY r.role_id, r.name;

-- ============================================
-- Kết quả mong đợi:
-- role_id = 2 (SALES_EMPLOYEE): 37 permissions
-- (tất cả trừ READ_EMPLOYEE, WRITE_EMPLOYEE, READ_ROLE, WRITE_ROLE, ASSIGN_PERMISSION)
-- ============================================

