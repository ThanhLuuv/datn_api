-- Migration to change Employee-Area relationship from one-to-many to many-to-many
-- Create intermediate table employee_area

-- 1. Create employee_area table
CREATE TABLE employee_area (
    employee_area_id BIGINT AUTO_INCREMENT PRIMARY KEY,
    employee_id BIGINT NOT NULL,
    area_id BIGINT NOT NULL,
    assigned_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    
    -- Foreign key constraints
    CONSTRAINT fk_employee_area_employee 
        FOREIGN KEY (employee_id) REFERENCES employee(employee_id) 
        ON DELETE CASCADE,
    CONSTRAINT fk_employee_area_area 
        FOREIGN KEY (area_id) REFERENCES area(area_id) 
        ON DELETE CASCADE,
    
    -- Unique constraint to prevent duplicate assignments
    CONSTRAINT uk_employee_area_unique 
        UNIQUE (employee_id, area_id)
);

-- 2. Create indexes for performance optimization
CREATE INDEX idx_employee_area_employee_id ON employee_area(employee_id);
CREATE INDEX idx_employee_area_area_id ON employee_area(area_id);
CREATE INDEX idx_employee_area_active ON employee_area(is_active);

-- 3. Migrate data from old area_id column to employee_area table
-- Only migrate employees that have area_id not null
INSERT INTO employee_area (employee_id, area_id, assigned_at, is_active)
SELECT 
    employee_id, 
    area_id, 
    created_at,  -- Use created_at as assigned_at
    TRUE         -- Default to active
FROM employee 
WHERE area_id IS NOT NULL;

-- 4. Drop old area_id column from employee table
-- Note: Backup data before running this command
ALTER TABLE employee DROP COLUMN area_id;

-- 5. Drop old foreign key constraint if exists
-- ALTER TABLE employee DROP CONSTRAINT IF EXISTS fk_emp_area;

-- 6. Insert sample data for areas from KhuVuc table
-- Convert data from KhuVuc table to area table
INSERT IGNORE INTO area (area_id, name, keywords)
SELECT 
    CAST(SUBSTRING(MaKhuVuc, 3) AS UNSIGNED) as area_id,  -- Extract number from PX001 -> 1
    TenKhuVuc as name,
    CONCAT(TenKhuVuc, ',', REPLACE(TenKhuVuc, 'Phường ', ''), ',', REPLACE(TenKhuVuc, 'Xã ', '')) as keywords
FROM KhuVuc
WHERE CAST(SUBSTRING(MaKhuVuc, 3) AS UNSIGNED) NOT IN (SELECT area_id FROM area);

-- Alternative: Direct insert of all KhuVuc data (uncomment if KhuVuc table doesn't exist)
/*
INSERT IGNORE INTO area (area_id, name, keywords) VALUES
(1, 'Phường Vũng Tàu', 'Phường Vũng Tàu,Vũng Tàu,Vũng Tàu'),
(2, 'Phường Tam Thắng', 'Phường Tam Thắng,Tam Thắng,Tam Thắng'),
(3, 'Phường Rạch Dừa', 'Phường Rạch Dừa,Rạch Dừa,Rạch Dừa'),
(4, 'Phường Phước Thắng', 'Phường Phước Thắng,Phước Thắng,Phước Thắng'),
(5, 'Phường Bà Rịa', 'Phường Bà Rịa,Bà Rịa,Bà Rịa'),
(6, 'Phường Long Hương', 'Phường Long Hương,Long Hương,Long Hương'),
(7, 'Phường Phú Mỹ', 'Phường Phú Mỹ,Phú Mỹ,Phú Mỹ'),
(8, 'Phường Tam Long', 'Phường Tam Long,Tam Long,Tam Long'),
(9, 'Phường Tân Thành', 'Phường Tân Thành,Tân Thành,Tân Thành'),
(10, 'Phường Tân Phước', 'Phường Tân Phước,Tân Phước,Tân Phước'),
(11, 'Phường Tân Hải', 'Phường Tân Hải,Tân Hải,Tân Hải'),
(12, 'Xã Châu Pha', 'Xã Châu Pha,Châu Pha,Châu Pha'),
(13, 'Xã Ngãi Giao', 'Xã Ngãi Giao,Ngãi Giao,Ngãi Giao'),
(14, 'Xã Bình Giã', 'Xã Bình Giã,Bình Giã,Bình Giã'),
(15, 'Xã Kim Long', 'Xã Kim Long,Kim Long,Kim Long'),
(16, 'Xã Châu Đức', 'Xã Châu Đức,Châu Đức,Châu Đức'),
(17, 'Xã Xuân Sơn', 'Xã Xuân Sơn,Xuân Sơn,Xuân Sơn'),
(18, 'Xã Nghĩa Thành', 'Xã Nghĩa Thành,Nghĩa Thành,Nghĩa Thành'),
(19, 'Xã Hồ Tràm', 'Xã Hồ Tràm,Hồ Tràm,Hồ Tràm'),
(20, 'Xã Xuyên Mộc', 'Xã Xuyên Mộc,Xuyên Mộc,Xuyên Mộc'),
(21, 'Xã Hòa Hội', 'Xã Hòa Hội,Hòa Hội,Hòa Hội'),
(22, 'Xã Bàu Lâm', 'Xã Bàu Lâm,Bàu Lâm,Bàu Lâm'),
(23, 'Xã Phước Hải', 'Xã Phước Hải,Phước Hải,Phước Hải'),
(24, 'Xã Long Hải', 'Xã Long Hải,Long Hải,Long Hải'),
(25, 'Xã Đất Đỏ', 'Xã Đất Đỏ,Đất Đỏ,Đất Đỏ'),
(26, 'Xã Long Điền', 'Xã Long Điền,Long Điền,Long Điền'),
(27, 'Đặc khu Côn Đảo', 'Đặc khu Côn Đảo,Côn Đảo,Côn Đảo'),
(28, 'Phường Đông Hoà', 'Phường Đông Hoà,Đông Hoà,Đông Hoà'),
(29, 'Phường Dĩ An', 'Phường Dĩ An,Dĩ An,Dĩ An'),
(30, 'Phường Tân Đông Hiệp', 'Phường Tân Đông Hiệp,Tân Đông Hiệp,Tân Đông Hiệp'),
(31, 'Phường Thuận An', 'Phường Thuận An,Thuận An,Thuận An'),
(32, 'Phường Thuận Giao', 'Phường Thuận Giao,Thuận Giao,Thuận Giao'),
(33, 'Phường Bình Hoà', 'Phường Bình Hoà,Bình Hoà,Bình Hoà'),
(34, 'Phường Lái Thiêu', 'Phường Lái Thiêu,Lái Thiêu,Lái Thiêu'),
(35, 'Phường An Phú', 'Phường An Phú,An Phú,An Phú'),
(36, 'Phường Bình Dương', 'Phường Bình Dương,Bình Dương,Bình Dương'),
(37, 'Phường Chánh Hiệp', 'Phường Chánh Hiệp,Chánh Hiệp,Chánh Hiệp'),
(38, 'Phường Thủ Dầu Một', 'Phường Thủ Dầu Một,Thủ Dầu Một,Thủ Dầu Một'),
(39, 'Phường Phú Lợi', 'Phường Phú Lợi,Phú Lợi,Phú Lợi'),
(40, 'Phường Vĩnh Tân', 'Phường Vĩnh Tân,Vĩnh Tân,Vĩnh Tân'),
(41, 'Phường Bình Cơ', 'Phường Bình Cơ,Bình Cơ,Bình Cơ'),
(42, 'Phường Tân Uyên', 'Phường Tân Uyên,Tân Uyên,Tân Uyên'),
(43, 'Phường Tân Hiệp', 'Phường Tân Hiệp,Tân Hiệp,Tân Hiệp'),
(44, 'Phường Tân Khánh', 'Phường Tân Khánh,Tân Khánh,Tân Khánh'),
(45, 'Phường Hoà Lợi', 'Phường Hoà Lợi,Hoà Lợi,Hoà Lợi'),
(46, 'Phường Phú An', 'Phường Phú An,Phú An,Phú An'),
(47, 'Phường Tây Nam', 'Phường Tây Nam,Tây Nam,Tây Nam'),
(48, 'Phường Long Nguyên', 'Phường Long Nguyên,Long Nguyên,Long Nguyên'),
(49, 'Phường Bến Cát', 'Phường Bến Cát,Bến Cát,Bến Cát'),
(50, 'Phường Chánh Phú Hoà', 'Phường Chánh Phú Hoà,Chánh Phú Hoà,Chánh Phú Hoà'),
(51, 'Xã Bắc Tân Uyên', 'Xã Bắc Tân Uyên,Bắc Tân Uyên,Bắc Tân Uyên'),
(52, 'Xã Thường Tân', 'Xã Thường Tân,Thường Tân,Thường Tân'),
(53, 'Xã An Long', 'Xã An Long,An Long,An Long'),
(54, 'Xã Phước Thành', 'Xã Phước Thành,Phước Thành,Phước Thành'),
(55, 'Xã Phước Hoà', 'Xã Phước Hoà,Phước Hoà,Phước Hoà'),
(56, 'Xã Phú Giáo', 'Xã Phú Giáo,Phú Giáo,Phú Giáo'),
(57, 'Xã Trừ Văn Thố', 'Xã Trừ Văn Thố,Trừ Văn Thố,Trừ Văn Thố'),
(58, 'Xã Bàu Bàng', 'Xã Bàu Bàng,Bàu Bàng,Bàu Bàng'),
(59, 'Xã Minh Thạnh', 'Xã Minh Thạnh,Minh Thạnh,Minh Thạnh'),
(60, 'Xã Long Hoà', 'Xã Long Hoà,Long Hoà,Long Hoà'),
(61, 'Xã Dầu Tiếng', 'Xã Dầu Tiếng,Dầu Tiếng,Dầu Tiếng'),
(62, 'Xã Thanh An', 'Xã Thanh An,Thanh An,Thanh An'),
(63, 'Phường Sài Gòn', 'Phường Sài Gòn,Sài Gòn,Sài Gòn'),
(64, 'Phường Tân Định', 'Phường Tân Định,Tân Định,Tân Định'),
(65, 'Phường Bến Thành', 'Phường Bến Thành,Bến Thành,Bến Thành'),
(66, 'Phường Cầu Ông Lãnh', 'Phường Cầu Ông Lãnh,Cầu Ông Lãnh,Cầu Ông Lãnh'),
(67, 'Phường Bàn Cờ', 'Phường Bàn Cờ,Bàn Cờ,Bàn Cờ'),
(68, 'Phường Xuân Hoà', 'Phường Xuân Hoà,Xuân Hoà,Xuân Hoà'),
(69, 'Phường Nhiêu Lộc', 'Phường Nhiêu Lộc,Nhiêu Lộc,Nhiêu Lộc'),
(70, 'Phường Xóm Chiếu', 'Phường Xóm Chiếu,Xóm Chiếu,Xóm Chiếu'),
(71, 'Phường Khánh Hội', 'Phường Khánh Hội,Khánh Hội,Khánh Hội'),
(72, 'Phường Vĩnh Hội', 'Phường Vĩnh Hội,Vĩnh Hội,Vĩnh Hội'),
(73, 'Phường Chợ Quán', 'Phường Chợ Quán,Chợ Quán,Chợ Quán'),
(74, 'Phường An Đông', 'Phường An Đông,An Đông,An Đông'),
(75, 'Phường Chợ Lớn', 'Phường Chợ Lớn,Chợ Lớn,Chợ Lớn'),
(76, 'Phường Bình Tây', 'Phường Bình Tây,Bình Tây,Bình Tây'),
(77, 'Phường Bình Tiên', 'Phường Bình Tiên,Bình Tiên,Bình Tiên'),
(78, 'Phường Bình Phú', 'Phường Bình Phú,Bình Phú,Bình Phú'),
(79, 'Phường Phú Lâm', 'Phường Phú Lâm,Phú Lâm,Phú Lâm'),
(80, 'Phường Tân Thuận', 'Phường Tân Thuận,Tân Thuận,Tân Thuận'),
(81, 'Phường Phú Thuận', 'Phường Phú Thuận,Phú Thuận,Phú Thuận'),
(82, 'Phường Tân Mỹ', 'Phường Tân Mỹ,Tân Mỹ,Tân Mỹ'),
(83, 'Phường Tân Hưng', 'Phường Tân Hưng,Tân Hưng,Tân Hưng'),
(84, 'Phường Chánh Hưng', 'Phường Chánh Hưng,Chánh Hưng,Chánh Hưng'),
(85, 'Phường Phú Định', 'Phường Phú Định,Phú Định,Phú Định'),
(86, 'Phường Bình Đông', 'Phường Bình Đông,Bình Đông,Bình Đông'),
(87, 'Phường Diên Hồng', 'Phường Diên Hồng,Diên Hồng,Diên Hồng'),
(88, 'Phường Vườn Lài', 'Phường Vườn Lài,Vườn Lài,Vườn Lài'),
(89, 'Phường Hoà Hưng', 'Phường Hoà Hưng,Hoà Hưng,Hoà Hưng'),
(90, 'Phường Minh Phụng', 'Phường Minh Phụng,Minh Phụng,Minh Phụng'),
(91, 'Phường Bình Thới', 'Phường Bình Thới,Bình Thới,Bình Thới'),
(92, 'Phường Hoà Bình', 'Phường Hoà Bình,Hoà Bình,Hoà Bình'),
(93, 'Phường Phú Thọ', 'Phường Phú Thọ,Phú Thọ,Phú Thọ'),
(94, 'Phường Đông Hưng Thuận', 'Phường Đông Hưng Thuận,Đông Hưng Thuận,Đông Hưng Thuận'),
(95, 'Phường Trung Mỹ Tây', 'Phường Trung Mỹ Tây,Trung Mỹ Tây,Trung Mỹ Tây'),
(96, 'Phường Tân Thới Hiệp', 'Phường Tân Thới Hiệp,Tân Thới Hiệp,Tân Thới Hiệp'),
(97, 'Phường Thới An', 'Phường Thới An,Thới An,Thới An'),
(98, 'Phường An Phú Đông', 'Phường An Phú Đông,An Phú Đông,An Phú Đông'),
(99, 'Phường An Lạc', 'Phường An Lạc,An Lạc,An Lạc'),
(100, 'Phường Tân Tạo', 'Phường Tân Tạo,Tân Tạo,Tân Tạo'),
(101, 'Phường Bình Tân', 'Phường Bình Tân,Bình Tân,Bình Tân'),
(102, 'Phường Bình Trị Đông', 'Phường Bình Trị Đông,Bình Trị Đông,Bình Trị Đông'),
(103, 'Phường Bình Hưng Hoà', 'Phường Bình Hưng Hoà,Bình Hưng Hoà,Bình Hưng Hoà'),
(104, 'Phường Gia Định', 'Phường Gia Định,Gia Định,Gia Định'),
(105, 'Phường Bình Thạnh', 'Phường Bình Thạnh,Bình Thạnh,Bình Thạnh'),
(106, 'Phường Bình Lợi Trung', 'Phường Bình Lợi Trung,Bình Lợi Trung,Bình Lợi Trung'),
(107, 'Phường Thạnh Mỹ Tây', 'Phường Thạnh Mỹ Tây,Thạnh Mỹ Tây,Thạnh Mỹ Tây'),
(108, 'Phường Bình Quới', 'Phường Bình Quới,Bình Quới,Bình Quới'),
(109, 'Phường Hạnh Thông', 'Phường Hạnh Thông,Hạnh Thông,Hạnh Thông'),
(110, 'Phường An Nhơn', 'Phường An Nhơn,An Nhơn,An Nhơn'),
(111, 'Phường Gò Vấp', 'Phường Gò Vấp,Gò Vấp,Gò Vấp'),
(112, 'Phường An Hội Đông', 'Phường An Hội Đông,An Hội Đông,An Hội Đông'),
(113, 'Phường Thông Tây Hội', 'Phường Thông Tây Hội,Thông Tây Hội,Thông Tây Hội'),
(114, 'Phường An Hội Tây', 'Phường An Hội Tây,An Hội Tây,An Hội Tây'),
(115, 'Phường Đức Nhuận', 'Phường Đức Nhuận,Đức Nhuận,Đức Nhuận'),
(116, 'Phường Cầu Kiệu', 'Phường Cầu Kiệu,Cầu Kiệu,Cầu Kiệu'),
(117, 'Phường Phú Nhuận', 'Phường Phú Nhuận,Phú Nhuận,Phú Nhuận'),
(118, 'Phường Tân Sơn Hoà', 'Phường Tân Sơn Hoà,Tân Sơn Hoà,Tân Sơn Hoà'),
(119, 'Phường Tân Sơn Nhất', 'Phường Tân Sơn Nhất,Tân Sơn Nhất,Tân Sơn Nhất'),
(120, 'Phường Tân Hoà', 'Phường Tân Hoà,Tân Hoà,Tân Hoà'),
(121, 'Phường Bảy Hiền', 'Phường Bảy Hiền,Bảy Hiền,Bảy Hiền'),
(122, 'Phường Tân Bình', 'Phường Tân Bình,Tân Bình,Tân Bình'),
(123, 'Phường Tân Sơn', 'Phường Tân Sơn,Tân Sơn,Tân Sơn'),
(124, 'Phường Tây Thạnh', 'Phường Tây Thạnh,Tây Thạnh,Tây Thạnh'),
(125, 'Phường Tân Sơn Nhì', 'Phường Tân Sơn Nhì,Tân Sơn Nhì,Tân Sơn Nhì'),
(126, 'Phường Phú Thọ Hoà', 'Phường Phú Thọ Hoà,Phú Thọ Hoà,Phú Thọ Hoà'),
(127, 'Phường Tân Phú', 'Phường Tân Phú,Tân Phú,Tân Phú'),
(128, 'Phường Phú Thạnh', 'Phường Phú Thạnh,Phú Thạnh,Phú Thạnh'),
(129, 'Phường Hiệp Bình', 'Phường Hiệp Bình,Hiệp Bình,Hiệp Bình'),
(130, 'Phường Thủ Đức', 'Phường Thủ Đức,Thủ Đức,Thủ Đức'),
(131, 'Phường Tam Bình', 'Phường Tam Bình,Tam Bình,Tam Bình'),
(132, 'Phường Linh Xuân', 'Phường Linh Xuân,Linh Xuân,Linh Xuân'),
(133, 'Phường Tăng Nhơn Phú', 'Phường Tăng Nhơn Phú,Tăng Nhơn Phú,Tăng Nhơn Phú'),
(134, 'Phường Long Bình', 'Phường Long Bình,Long Bình,Long Bình'),
(135, 'Phường Long Phước', 'Phường Long Phước,Long Phước,Long Phước'),
(136, 'Phường Long Trường', 'Phường Long Trường,Long Trường,Long Trường'),
(137, 'Phường Cát Lái', 'Phường Cát Lái,Cát Lái,Cát Lái'),
(138, 'Phường Bình Trưng', 'Phường Bình Trưng,Bình Trưng,Bình Trưng'),
(139, 'Phường Phước Long', 'Phường Phước Long,Phước Long,Phước Long'),
(140, 'Phường An Khánh', 'Phường An Khánh,An Khánh,An Khánh'),
(141, 'Xã Vĩnh Lộc', 'Xã Vĩnh Lộc,Vĩnh Lộc,Vĩnh Lộc'),
(142, 'Xã Tân Vĩnh Lộc', 'Xã Tân Vĩnh Lộc,Tân Vĩnh Lộc,Tân Vĩnh Lộc'),
(143, 'Xã Bình Lợi', 'Xã Bình Lợi,Bình Lợi,Bình Lợi'),
(144, 'Xã Tân Nhựt', 'Xã Tân Nhựt,Tân Nhựt,Tân Nhựt'),
(145, 'Xã Bình Chánh', 'Xã Bình Chánh,Bình Chánh,Bình Chánh'),
(146, 'Xã Hưng Long', 'Xã Hưng Long,Hưng Long,Hưng Long'),
(147, 'Xã Bình Hưng', 'Xã Bình Hưng,Bình Hưng,Bình Hưng'),
(148, 'Xã Bình Khánh', 'Xã Bình Khánh,Bình Khánh,Bình Khánh'),
(149, 'Xã An Thới Đông', 'Xã An Thới Đông,An Thới Đông,An Thới Đông'),
(150, 'Xã Cần Giờ', 'Xã Cần Giờ,Cần Giờ,Cần Giờ'),
(151, 'Xã Củ Chi', 'Xã Củ Chi,Củ Chi,Củ Chi'),
(152, 'Xã Tân An Hội', 'Xã Tân An Hội,Tân An Hội,Tân An Hội'),
(153, 'Xã Thái Mỹ', 'Xã Thái Mỹ,Thái Mỹ,Thái Mỹ'),
(154, 'Xã An Nhơn Tây', 'Xã An Nhơn Tây,An Nhơn Tây,An Nhơn Tây'),
(155, 'Xã Nhuận Đức', 'Xã Nhuận Đức,Nhuận Đức,Nhuận Đức'),
(156, 'Xã Phú Hoà Đông', 'Xã Phú Hoà Đông,Phú Hoà Đông,Phú Hoà Đông'),
(157, 'Xã Bình Mỹ', 'Xã Bình Mỹ,Bình Mỹ,Bình Mỹ'),
(158, 'Xã Đông Thạnh', 'Xã Đông Thạnh,Đông Thạnh,Đông Thạnh'),
(159, 'Xã Hóc Môn', 'Xã Hóc Môn,Hóc Môn,Hóc Môn'),
(160, 'Xã Xuân Thới Sơn', 'Xã Xuân Thới Sơn,Xuân Thới Sơn,Xuân Thới Sơn'),
(161, 'Xã Bà Điểm', 'Xã Bà Điểm,Bà Điểm,Bà Điểm'),
(162, 'Xã Nhà Bè', 'Xã Nhà Bè,Nhà Bè,Nhà Bè'),
(163, 'Xã Hiệp Phước', 'Xã Hiệp Phước,Hiệp Phước,Hiệp Phước'),
(164, 'Xã Long Sơn', 'Xã Long Sơn,Long Sơn,Long Sơn'),
(165, 'Xã Hòa Hiệp', 'Xã Hòa Hiệp,Hòa Hiệp,Hòa Hiệp'),
(166, 'Xã Bình Châu', 'Xã Bình Châu,Bình Châu,Bình Châu'),
(167, 'Phường Thới Hoà', 'Phường Thới Hoà,Thới Hoà,Thới Hoà'),
(168, 'Xã Thạnh An', 'Xã Thạnh An,Thạnh An,Thạnh An');
*/

-- 7. Example: Assign employees to multiple areas
-- (Run after having employee and area data)
-- Uncomment and modify these examples based on your actual employee IDs

-- Assign employee 1 to multiple areas
INSERT INTO employee_area (employee_id, area_id, assigned_at, is_active) VALUES
(1, 1, NOW(), TRUE),   -- Phường Vũng Tàu
(1, 2, NOW(), TRUE),   -- Phường Tam Thắng
(1, 3, NOW(), TRUE),   -- Phường Rạch Dừa
(1, 63, NOW(), TRUE),  -- Phường Sài Gòn
(1, 64, NOW(), TRUE);  -- Phường Tân Định

-- Assign employee 2 to different areas
INSERT INTO employee_area (employee_id, area_id, assigned_at, is_active) VALUES
(2, 4, NOW(), TRUE),   -- Phường Phước Thắng
(2, 5, NOW(), TRUE),   -- Phường Bà Rịa
(2, 6, NOW(), TRUE),   -- Phường Long Hương
(2, 65, NOW(), TRUE),  -- Phường Bến Thành
(2, 66, NOW(), TRUE);  -- Phường Cầu Ông Lãnh

-- Assign employee 3 to more areas
INSERT INTO employee_area (employee_id, area_id, assigned_at, is_active) VALUES
(3, 7, NOW(), TRUE),   -- Phường Phú Mỹ
(3, 8, NOW(), TRUE),   -- Phường Tam Long
(3, 9, NOW(), TRUE),   -- Phường Tân Thành
(3, 67, NOW(), TRUE),  -- Phường Bàn Cờ
(3, 68, NOW(), TRUE);  -- Phường Xuân Hoà

-- You can add more assignments as needed
-- INSERT INTO employee_area (employee_id, area_id, assigned_at, is_active) VALUES
-- (employee_id, area_id, NOW(), TRUE);

-- 8. Query to check data after migration
-- View all employees and their assigned areas
SELECT 
    e.employee_id,
    e.first_name,
    e.last_name,
    GROUP_CONCAT(a.name ORDER BY a.name SEPARATOR ', ') as assigned_areas,
    COUNT(ea.area_id) as total_areas
FROM employee e
LEFT JOIN employee_area ea ON e.employee_id = ea.employee_id AND ea.is_active = TRUE
LEFT JOIN area a ON ea.area_id = a.area_id
GROUP BY e.employee_id, e.first_name, e.last_name
ORDER BY e.employee_id;

-- 9. Query to find suitable employees for a delivery address
-- Example: find employees suitable for address containing "Vũng Tàu"
/*
SELECT DISTINCT
    e.employee_id,
    e.first_name,
    e.last_name,
    e.phone,
    e.email,
    GROUP_CONCAT(a.name ORDER BY a.name SEPARATOR ', ') as assigned_areas,
    COUNT(DISTINCT o.order_id) as active_orders
FROM employee e
INNER JOIN employee_area ea ON e.employee_id = ea.employee_id AND ea.is_active = TRUE
INNER JOIN area a ON ea.area_id = a.area_id
LEFT JOIN `order` o ON e.employee_id = o.delivered_by AND o.status = 'Assigned'
WHERE a.keywords LIKE '%Vũng Tàu%' OR a.name LIKE '%Vũng Tàu%'
GROUP BY e.employee_id, e.first_name, e.last_name, e.phone, e.email
ORDER BY active_orders ASC, e.employee_id;
*/

-- 10. Rollback script (if needed to revert)
/*
-- Add back area_id column to employee table
ALTER TABLE employee ADD COLUMN area_id BIGINT NULL;

-- Migrate data from employee_area back to employee (take only first area)
UPDATE employee e
SET area_id = (
    SELECT ea.area_id 
    FROM employee_area ea 
    WHERE ea.employee_id = e.employee_id 
    AND ea.is_active = TRUE 
    ORDER BY ea.assigned_at ASC 
    LIMIT 1
);

-- Add foreign key constraint
ALTER TABLE employee ADD CONSTRAINT fk_emp_area 
    FOREIGN KEY (area_id) REFERENCES area(area_id);

-- Drop employee_area table
DROP TABLE employee_area;
*/
