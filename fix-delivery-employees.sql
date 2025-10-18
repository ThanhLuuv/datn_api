-- Fix delivery employees issue
-- This script ensures we have employees with role 4 (DELIVERY_EMPLOYEE)

-- 1. Check current roles
SELECT 'CURRENT ROLES:' as info;
SELECT role_id, name, description FROM role ORDER BY role_id;

-- 2. Check current employees and their roles
SELECT 'CURRENT EMPLOYEES AND ROLES:' as info;
SELECT 
    e.employee_id,
    CONCAT(e.first_name, ' ', e.last_name) as employee_name,
    e.email,
    a.role_id,
    r.name as role_name
FROM employee e
JOIN account a ON e.account_id = a.account_id
JOIN role r ON a.role_id = r.role_id
ORDER BY e.employee_id;

-- 3. Create role 4 if not exists
INSERT INTO role (role_id, name, description) 
SELECT 4, 'Delivery Employee', 'Nhân viên giao hàng'
WHERE NOT EXISTS (SELECT 1 FROM role WHERE role_id = 4);

-- 4. Create accounts for delivery employees if not exists
INSERT INTO account (account_id, email, password_hash, role_id, is_active) 
SELECT 10, 'delivery1@example.com', 'test-hash', 4, TRUE
WHERE NOT EXISTS (SELECT 1 FROM account WHERE account_id = 10);

INSERT INTO account (account_id, email, password_hash, role_id, is_active) 
SELECT 11, 'delivery2@example.com', 'test-hash', 4, TRUE
WHERE NOT EXISTS (SELECT 1 FROM account WHERE account_id = 11);

INSERT INTO account (account_id, email, password_hash, role_id, is_active) 
SELECT 12, 'delivery3@example.com', 'test-hash', 4, TRUE
WHERE NOT EXISTS (SELECT 1 FROM account WHERE account_id = 12);

-- 5. Create delivery employees if not exists
INSERT INTO employee (employee_id, account_id, department_id, first_name, last_name, gender, phone, email) 
SELECT 10, 10, 1, 'Nguyen', 'Van Delivery 1', 1, '0901000001', 'delivery1@example.com'
WHERE NOT EXISTS (SELECT 1 FROM employee WHERE employee_id = 10);

INSERT INTO employee (employee_id, account_id, department_id, first_name, last_name, gender, phone, email) 
SELECT 11, 11, 1, 'Tran', 'Van Delivery 2', 1, '0901000002', 'delivery2@example.com'
WHERE NOT EXISTS (SELECT 1 FROM employee WHERE employee_id = 11);

INSERT INTO employee (employee_id, account_id, department_id, first_name, last_name, gender, phone, email) 
SELECT 12, 12, 1, 'Le', 'Thi Delivery 3', 2, '0901000003', 'delivery3@example.com'
WHERE NOT EXISTS (SELECT 1 FROM employee WHERE employee_id = 12);

-- 6. Assign delivery employees to areas (if employee_area table exists)
-- Check if employee_area table exists first
SET @table_exists = (
    SELECT COUNT(*) 
    FROM information_schema.tables 
    WHERE table_schema = DATABASE() 
    AND table_name = 'employee_area'
);

-- If employee_area table exists, assign areas
SET @sql = IF(@table_exists > 0, 
    'INSERT INTO employee_area (employee_id, area_id, assigned_at, is_active) 
     SELECT 10, 1, NOW(), TRUE WHERE NOT EXISTS (SELECT 1 FROM employee_area WHERE employee_id = 10 AND area_id = 1);
     INSERT INTO employee_area (employee_id, area_id, assigned_at, is_active) 
     SELECT 10, 2, NOW(), TRUE WHERE NOT EXISTS (SELECT 1 FROM employee_area WHERE employee_id = 10 AND area_id = 2);
     INSERT INTO employee_area (employee_id, area_id, assigned_at, is_active) 
     SELECT 10, 3, NOW(), TRUE WHERE NOT EXISTS (SELECT 1 FROM employee_area WHERE employee_id = 10 AND area_id = 3);
     INSERT INTO employee_area (employee_id, area_id, assigned_at, is_active) 
     SELECT 11, 4, NOW(), TRUE WHERE NOT EXISTS (SELECT 1 FROM employee_area WHERE employee_id = 11 AND area_id = 4);
     INSERT INTO employee_area (employee_id, area_id, assigned_at, is_active) 
     SELECT 11, 5, NOW(), TRUE WHERE NOT EXISTS (SELECT 1 FROM employee_area WHERE employee_id = 11 AND area_id = 5);
     INSERT INTO employee_area (employee_id, area_id, assigned_at, is_active) 
     SELECT 12, 6, NOW(), TRUE WHERE NOT EXISTS (SELECT 1 FROM employee_area WHERE employee_id = 12 AND area_id = 6);
     INSERT INTO employee_area (employee_id, area_id, assigned_at, is_active) 
     SELECT 12, 7, NOW(), TRUE WHERE NOT EXISTS (SELECT 1 FROM employee_area WHERE employee_id = 12 AND area_id = 7);',
    'SELECT "employee_area table does not exist, skipping area assignments" as message;'
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- 7. Verify the fix
SELECT 'AFTER FIX - DELIVERY EMPLOYEES:' as info;
SELECT 
    e.employee_id,
    CONCAT(e.first_name, ' ', e.last_name) as employee_name,
    e.email,
    e.phone,
    a.role_id,
    r.name as role_name
FROM employee e
JOIN account a ON e.account_id = a.account_id
JOIN role r ON a.role_id = r.role_id
WHERE a.role_id = 4
ORDER BY e.employee_id;

-- 8. Show employee-area assignments (if table exists)
SET @sql2 = IF(@table_exists > 0, 
    'SELECT "EMPLOYEE-AREA ASSIGNMENTS:" as info;
     SELECT 
         e.employee_id,
         CONCAT(e.first_name, " ", e.last_name) as employee_name,
         GROUP_CONCAT(a.name ORDER BY a.name SEPARATOR ", ") as assigned_areas
     FROM employee e
     LEFT JOIN employee_area ea ON e.employee_id = ea.employee_id AND ea.is_active = TRUE
     LEFT JOIN area a ON ea.area_id = a.area_id
     WHERE e.employee_id IN (10, 11, 12)
     GROUP BY e.employee_id, e.first_name, e.last_name
     ORDER BY e.employee_id;',
    'SELECT "employee_area table does not exist" as message;'
);

PREPARE stmt2 FROM @sql2;
EXECUTE stmt2;
DEALLOCATE PREPARE stmt2;

SELECT 'Fix completed! Now test the API again.' as message;












