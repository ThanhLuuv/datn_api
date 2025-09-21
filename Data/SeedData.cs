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
                    UnitPrice = 50.000m,
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
                    UnitPrice = 40.000m,
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
                    UnitPrice = 80.000m,
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
    }
}
