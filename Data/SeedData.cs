using BookStore.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Data;

public static class SeedData
{
    public static async Task SeedAsync(BookStoreDbContext context)
    {
        // Seed Roles
        if (!await context.Roles.AnyAsync())
        {
            var roles = new List<Role>
            {
                new Role { RoleId = 1, Name = "ADMIN", Description = "Quản trị viên" },
                new Role { RoleId = 2, Name = "SALES_EMPLOYEE", Description = "Nhân viên bán hàng" },
                new Role { RoleId = 3, Name = "DELIVERY_EMPLOYEE", Description = "Nhân viên giao hàng" },
                new Role { RoleId = 4, Name = "CUSTOMER", Description = "Khách hàng" }
            };

            context.Roles.AddRange(roles);
            await context.SaveChangesAsync();
        }

        // Seed Permissions
        if (!await context.Permissions.AnyAsync())
        {
            var permissions = new List<Permission>
            {
                new Permission { PermissionId = 1, Code = "READ_CATEGORY", Name = "Đọc danh mục", Description = "Xem danh sách danh mục" },
                new Permission { PermissionId = 2, Code = "WRITE_CATEGORY", Name = "Ghi danh mục", Description = "Tạo, sửa, xóa danh mục" },
                new Permission { PermissionId = 3, Code = "READ_BOOK", Name = "Đọc sách", Description = "Xem danh sách sách" },
                new Permission { PermissionId = 4, Code = "WRITE_BOOK", Name = "Ghi sách", Description = "Tạo, sửa, xóa sách" },
                new Permission { PermissionId = 5, Code = "READ_PURCHASE_ORDER", Name = "Đọc đơn đặt mua", Description = "Xem danh sách đơn đặt mua" },
                new Permission { PermissionId = 6, Code = "WRITE_PURCHASE_ORDER", Name = "Ghi đơn đặt mua", Description = "Tạo, sửa, xóa đơn đặt mua" },
                new Permission { PermissionId = 7, Code = "READ_GOODS_RECEIPT", Name = "Đọc phiếu nhập", Description = "Xem danh sách phiếu nhập" },
                new Permission { PermissionId = 8, Code = "WRITE_GOODS_RECEIPT", Name = "Ghi phiếu nhập", Description = "Tạo, sửa, xóa phiếu nhập" },
                new Permission { PermissionId = 9, Code = "SALES_MANAGEMENT", Name = "Quản lý bán hàng", Description = "Quản lý đơn hàng, khách hàng" },
                new Permission { PermissionId = 10, Code = "DELIVERY_MANAGEMENT", Name = "Quản lý giao hàng", Description = "Quản lý vận chuyển, giao hàng" }
            };

            context.Permissions.AddRange(permissions);
            await context.SaveChangesAsync();
        }

        // Seed Role Permissions
        if (!await context.RolePermissions.AnyAsync())
        {
            var rolePermissions = new List<RolePermission>
            {
                // ADMIN (RoleId = 1) có tất cả quyền
                new RolePermission { RoleId = 1, PermissionId = 1 },
                new RolePermission { RoleId = 1, PermissionId = 2 },
                new RolePermission { RoleId = 1, PermissionId = 3 },
                new RolePermission { RoleId = 1, PermissionId = 4 },
                new RolePermission { RoleId = 1, PermissionId = 5 },
                new RolePermission { RoleId = 1, PermissionId = 6 },
                new RolePermission { RoleId = 1, PermissionId = 7 },
                new RolePermission { RoleId = 1, PermissionId = 8 },
                new RolePermission { RoleId = 1, PermissionId = 9 },
                new RolePermission { RoleId = 1, PermissionId = 10 },
                
                // SALES_EMPLOYEE (RoleId = 2) - Nhân viên bán hàng
                new RolePermission { RoleId = 2, PermissionId = 1 }, // Đọc danh mục
                new RolePermission { RoleId = 2, PermissionId = 3 }, // Đọc sách
                new RolePermission { RoleId = 2, PermissionId = 5 }, // Đọc đơn đặt mua
                new RolePermission { RoleId = 2, PermissionId = 7 }, // Đọc phiếu nhập
                new RolePermission { RoleId = 2, PermissionId = 9 }, // Quản lý bán hàng
                
                // DELIVERY_EMPLOYEE (RoleId = 3) - Nhân viên giao hàng
                new RolePermission { RoleId = 3, PermissionId = 1 }, // Đọc danh mục
                new RolePermission { RoleId = 3, PermissionId = 3 }, // Đọc sách
                new RolePermission { RoleId = 3, PermissionId = 5 }, // Đọc đơn đặt mua
                new RolePermission { RoleId = 3, PermissionId = 7 }, // Đọc phiếu nhập
                new RolePermission { RoleId = 3, PermissionId = 10 }, // Quản lý giao hàng
                
                // CUSTOMER (RoleId = 4) chỉ có quyền đọc
                new RolePermission { RoleId = 4, PermissionId = 1 }, // Đọc danh mục
                new RolePermission { RoleId = 4, PermissionId = 3 }  // Đọc sách
            };

            context.RolePermissions.AddRange(rolePermissions);
            await context.SaveChangesAsync();
        }

        // Seed Categories
        if (!await context.Categories.AnyAsync())
        {
            var categories = new List<Category>
            {
                new Category { Name = "Tiểu thuyết", Description = "Thể loại tiểu thuyết văn học" },
                new Category { Name = "Khoa học", Description = "Sách khoa học và công nghệ" },
                new Category { Name = "Lịch sử", Description = "Sách lịch sử và địa lý" },
                new Category { Name = "Kinh tế", Description = "Sách kinh tế và tài chính" },
                new Category { Name = "Ngoại ngữ", Description = "Sách học ngoại ngữ" }
            };

            context.Categories.AddRange(categories);
            await context.SaveChangesAsync();
        }

        // Seed Publishers
        if (!await context.Publishers.AnyAsync())
        {
            var publishers = new List<Publisher>
            {
                new Publisher { Name = "NXB Kim Đồng", Address = "Hà Nội", Phone = "024-1234567", Email = "info@kimdong.com.vn" },
                new Publisher { Name = "NXB Trẻ", Address = "TP.HCM", Phone = "028-1234567", Email = "info@nxbtre.com.vn" },
                new Publisher { Name = "NXB Giáo dục", Address = "Hà Nội", Phone = "024-2345678", Email = "info@nxbgd.com.vn" },
                new Publisher { Name = "NXB Thế giới", Address = "Hà Nội", Phone = "024-3456789", Email = "info@nxbtg.com.vn" }
            };

            context.Publishers.AddRange(publishers);
            await context.SaveChangesAsync();
        }

        // Seed Authors
        if (!await context.Authors.AnyAsync())
        {
            var authors = new List<Author>
            {
                new Author { FirstName = "Nguyễn", LastName = "Du", Gender = Gender.Male, Email = "nguyendu@example.com" },
                new Author { FirstName = "Hồ", LastName = "Chí Minh", Gender = Gender.Male, Email = "hochiminh@example.com" },
                new Author { FirstName = "Tố", LastName = "Hữu", Gender = Gender.Male, Email = "tohuu@example.com" },
                new Author { FirstName = "Xuân", LastName = "Quỳnh", Gender = Gender.Female, Email = "xuanquynh@example.com" },
                new Author { FirstName = "J.K.", LastName = "Rowling", Gender = Gender.Female, Email = "jkrowling@example.com" }
            };

            context.Authors.AddRange(authors);
            await context.SaveChangesAsync();
        }

        // Seed Books
        if (!await context.Books.AnyAsync())
        {
            var books = new List<Book>
            {
                new Book
                {
                    Isbn = "978-604-1-00001-1",
                    Title = "Truyện Kiều",
                    PageCount = 300,
                    AveragePrice = 50.000m,
                    PublishYear = 2020,
                    CategoryId = 1,
                    PublisherId = 1,
                    ImageUrl = "https://example.com/truyen-kieu.jpg"
                },
                new Book
                {
                    Isbn = "978-604-1-00002-2",
                    Title = "Nhật ký trong tù",
                    PageCount = 200,
                    AveragePrice = 40.000m,
                    PublishYear = 2021,
                    CategoryId = 3,
                    PublisherId = 2,
                    ImageUrl = "https://example.com/nhat-ky-trong-tu.jpg"
                },
                new Book
                {
                    Isbn = "978-604-1-00003-3",
                    Title = "Harry Potter và Hòn đá phù thủy",
                    PageCount = 350,
                    AveragePrice = 80.000m,
                    PublishYear = 2022,
                    CategoryId = 1,
                    PublisherId = 3,
                    ImageUrl = "https://example.com/harry-potter.jpg"
                }
            };

            context.Books.AddRange(books);
            await context.SaveChangesAsync();

            // Seed Author-Book relationships
            var authorBooks = new List<AuthorBook>
            {
                new AuthorBook { AuthorId = 1, Isbn = "978-604-1-00001-1" },
                new AuthorBook { AuthorId = 2, Isbn = "978-604-1-00002-2" },
                new AuthorBook { AuthorId = 5, Isbn = "978-604-1-00003-3" }
            };

            context.AuthorBooks.AddRange(authorBooks);
            await context.SaveChangesAsync();
        }

        // Seed Departments
        if (!await context.Departments.AnyAsync())
        {
            var departments = new List<Department>
            {
                new Department { Name = "Kinh doanh", Description = "Phòng kinh doanh" },
                new Department { Name = "Kế toán", Description = "Phòng kế toán" },
                new Department { Name = "Kho", Description = "Phòng kho" },
                new Department { Name = "Marketing", Description = "Phòng marketing" }
            };

            context.Departments.AddRange(departments);
            await context.SaveChangesAsync();
        }

        // Seed Areas
        if (!await context.Areas.AnyAsync())
        {
            var areas = new List<Area>
            {
                new Area { Name = "Quận 1", Keywords = "quan 1,Q1,quan1,district 1,700000" },
                new Area { Name = "Quận 3", Keywords = "quan 3,Q3,quan3,district 3,700000" },
                new Area { Name = "Quận 7", Keywords = "quan 7,Q7,quan7,district 7,700000,Phu My Hung" }
            };

            context.Areas.AddRange(areas);
            await context.SaveChangesAsync();
        }

        // Seed Delivery Employees (with Accounts) for testing delivery suggestion
        if (!await context.Employees.AnyAsync())
        {
            // Get references
            var dept = await context.Departments.FirstAsync();
            var areaIds = await context.Areas.OrderBy(a => a.AreaId).Select(a => a.AreaId).ToListAsync();

            var accounts = new List<Account>
            {
                new Account { Email = "shipper1@example.com", PasswordHash = "test-hash", RoleId = 3, IsActive = true },
                new Account { Email = "shipper2@example.com", PasswordHash = "test-hash", RoleId = 3, IsActive = true },
                new Account { Email = "shipper3@example.com", PasswordHash = "test-hash", RoleId = 3, IsActive = true }
            };
            context.Accounts.AddRange(accounts);
            await context.SaveChangesAsync();

            var employees = new List<Employee>
            {
                new Employee { AccountId = accounts[0].AccountId, DepartmentId = dept.DepartmentId, FirstName = "Nguyen", LastName = "A", Gender = Gender.Male, Phone = "0901111111", Email = "shipper1@example.com" },
                new Employee { AccountId = accounts[1].AccountId, DepartmentId = dept.DepartmentId, FirstName = "Tran", LastName = "B", Gender = Gender.Male, Phone = "0902222222", Email = "shipper2@example.com" },
                new Employee { AccountId = accounts[2].AccountId, DepartmentId = dept.DepartmentId, FirstName = "Le", LastName = "C", Gender = Gender.Female, Phone = "0903333333", Email = "shipper3@example.com" }
            };
            context.Employees.AddRange(employees);
            await context.SaveChangesAsync();

            // Assign employees to areas using the new many-to-many relationship
            var employeeAreas = new List<EmployeeArea>();
            for (int i = 0; i < employees.Count && i < areaIds.Count; i++)
            {
                employeeAreas.Add(new EmployeeArea 
                { 
                    EmployeeId = employees[i].EmployeeId, 
                    AreaId = areaIds[i] 
                });
            }
            context.EmployeeAreas.AddRange(employeeAreas);
            await context.SaveChangesAsync();
        }

        // Seed Purchase Order Statuses
        if (!await context.PurchaseOrderStatuses.AnyAsync())
        {
            var statuses = new List<PurchaseOrderStatus>
            {
                new PurchaseOrderStatus { StatusId = 1, StatusName = "Pending", Description = "Đơn đặt mua đang chờ xử lý" },
                new PurchaseOrderStatus { StatusId = 2, StatusName = "Sent", Description = "Đã gửi đơn đặt mua cho nhà cung cấp" },
                new PurchaseOrderStatus { StatusId = 3, StatusName = "Confirmed", Description = "Nhà cung cấp đã xác nhận đơn hàng" },
                new PurchaseOrderStatus { StatusId = 4, StatusName = "Delivered", Description = "Đã giao hàng" },
                new PurchaseOrderStatus { StatusId = 5, StatusName = "Cancelled", Description = "Đã hủy đơn đặt mua" }
            };

            context.PurchaseOrderStatuses.AddRange(statuses);
            await context.SaveChangesAsync();
        }

        // Seed Promotions (requires at least 1 employee and some books)
        if (!await context.Promotions.AnyAsync())
        {
            // Try to find an existing employee to set as issuer
            var issuerId = await context.Employees
                .Select(e => e.EmployeeId)
                .FirstOrDefaultAsync();

            if (issuerId != 0)
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
                var promo1 = new Promotion
                {
                    Name = "Giảm 10% tuần lễ sách",
                    Description = "Khuyến mãi 10% cho một số tựa sách nổi bật",
                    DiscountPct = 10m,
                    StartDate = today.AddDays(-3),
                    EndDate = today.AddDays(7),
                    IssuedBy = issuerId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var promo2 = new Promotion
                {
                    Name = "Sale cuối tháng 20%",
                    Description = "Khuyến mãi 20% cuối tháng cho các tựa chọn lọc",
                    DiscountPct = 20m,
                    StartDate = new DateOnly(today.Year, today.Month, 1).AddDays(20),
                    EndDate = new DateOnly(today.Year, today.Month, 1).AddDays(27),
                    IssuedBy = issuerId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                context.Promotions.AddRange(promo1, promo2);
                await context.SaveChangesAsync();

                // Link a few books to each promotion if available
                var firstIsbns = await context.Books
                    .OrderBy(b => b.Isbn)
                    .Select(b => b.Isbn)
                    .Take(2)
                    .ToListAsync();

                var nextIsbns = await context.Books
                    .OrderByDescending(b => b.Isbn)
                    .Select(b => b.Isbn)
                    .Take(2)
                    .ToListAsync();

                var bookPromotions = new List<BookPromotion>();
                foreach (var isbn in firstIsbns)
                {
                    bookPromotions.Add(new BookPromotion { Isbn = isbn, PromotionId = promo1.PromotionId });
                }
                foreach (var isbn in nextIsbns)
                {
                    bookPromotions.Add(new BookPromotion { Isbn = isbn, PromotionId = promo2.PromotionId });
                }

                if (bookPromotions.Count > 0)
                {
                    context.BookPromotions.AddRange(bookPromotions);
                    await context.SaveChangesAsync();
                }
            }
            // If there is no employee yet, skip promotion seeding silently
        }
    }
}
