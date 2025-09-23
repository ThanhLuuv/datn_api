using BookStore.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Data;

public class BookStoreDbContext : DbContext
{
    public BookStoreDbContext(DbContextOptions<BookStoreDbContext> options) : base(options)
    {
    }

    // Core entities
    public DbSet<Account> Accounts { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<Permission> Permissions { get; set; }
    public DbSet<RolePermission> RolePermissions { get; set; }

    // User management
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Employee> Employees { get; set; }
    public DbSet<Department> Departments { get; set; }

    // Product management
    public DbSet<Category> Categories { get; set; }
    public DbSet<Book> Books { get; set; }
    public DbSet<Author> Authors { get; set; }
    public DbSet<AuthorBook> AuthorBooks { get; set; }
    public DbSet<Publisher> Publishers { get; set; }
    public DbSet<PriceChange> PriceChanges { get; set; }

    // Order management
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderLine> OrderLines { get; set; }
    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<Return> Returns { get; set; }
    public DbSet<ReturnLine> ReturnLines { get; set; }

    // Purchase management
    public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
    public DbSet<PurchaseOrderLine> PurchaseOrderLines { get; set; }
    public DbSet<GoodsReceipt> GoodsReceipts { get; set; }
    public DbSet<GoodsReceiptLine> GoodsReceiptLines { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Account relationships
        modelBuilder.Entity<Account>()
            .HasOne(a => a.Role)
            .WithMany(r => r.Accounts)
            .HasForeignKey(a => a.RoleId)
            .HasConstraintName("fk_account_role");

        modelBuilder.Entity<Account>()
            .HasOne(a => a.Customer)
            .WithOne(c => c.Account)
            .HasForeignKey<Customer>(c => c.AccountId)
            .HasConstraintName("fk_cus_account");

        modelBuilder.Entity<Account>()
            .HasOne(a => a.Employee)
            .WithOne(e => e.Account)
            .HasForeignKey<Employee>(e => e.AccountId)
            .HasConstraintName("fk_emp_account");

        // Configure Customer relationships
        modelBuilder.Entity<Customer>()
            .HasMany(c => c.Orders)
            .WithOne(o => o.Customer)
            .HasForeignKey(o => o.CustomerId)
            .HasConstraintName("fk_order_customer");

        // Configure Employee relationships
        modelBuilder.Entity<Employee>()
            .HasOne(e => e.Department)
            .WithMany(d => d.Employees)
            .HasForeignKey(e => e.DepartmentId)
            .HasConstraintName("fk_emp_department");

        modelBuilder.Entity<Employee>()
            .HasMany(e => e.CreatedPurchaseOrders)
            .WithOne(po => po.CreatedByEmployee)
            .HasForeignKey(po => po.CreatedBy)
            .HasConstraintName("fk_po_employee");

        modelBuilder.Entity<Employee>()
            .HasMany(e => e.CreatedGoodsReceipts)
            .WithOne(gr => gr.CreatedByEmployee)
            .HasForeignKey(gr => gr.CreatedBy)
            .HasConstraintName("fk_gr_employee");

        modelBuilder.Entity<Employee>()
            .HasMany(e => e.ApprovedOrders)
            .WithOne(o => o.ApprovedByEmployee)
            .HasForeignKey(o => o.ApprovedBy)
            .HasConstraintName("fk_order_approved_by");

        modelBuilder.Entity<Employee>()
            .HasMany(e => e.DeliveredOrders)
            .WithOne(o => o.DeliveredByEmployee)
            .HasForeignKey(o => o.DeliveredBy)
            .HasConstraintName("fk_order_delivered_by");

        // Configure Book relationships
        modelBuilder.Entity<Book>()
            .HasOne(b => b.Category)
            .WithMany(c => c.Books)
            .HasForeignKey(b => b.CategoryId)
            .HasConstraintName("fk_book_category");

        modelBuilder.Entity<Book>()
            .HasOne(b => b.Publisher)
            .WithMany(p => p.Books)
            .HasForeignKey(b => b.PublisherId)
            .HasConstraintName("fk_book_publisher");

        // Configure Author-Book many-to-many
        modelBuilder.Entity<AuthorBook>()
            .HasKey(ab => new { ab.AuthorId, ab.Isbn });

        modelBuilder.Entity<AuthorBook>()
            .HasOne(ab => ab.Author)
            .WithMany(a => a.AuthorBooks)
            .HasForeignKey(ab => ab.AuthorId)
            .HasConstraintName("fk_author_book_author");

        modelBuilder.Entity<AuthorBook>()
            .HasOne(ab => ab.Book)
            .WithMany(b => b.AuthorBooks)
            .HasForeignKey(ab => ab.Isbn)
            .HasConstraintName("fk_author_book_book");

        // Configure Order relationships
        modelBuilder.Entity<Order>()
            .HasMany(o => o.OrderLines)
            .WithOne(ol => ol.Order)
            .HasForeignKey(ol => ol.OrderId)
            .HasConstraintName("fk_ol_order");

        modelBuilder.Entity<OrderLine>()
            .HasOne(ol => ol.Book)
            .WithMany(b => b.OrderLines)
            .HasForeignKey(ol => ol.Isbn)
            .HasConstraintName("fk_ol_book");

        // Configure Invoice relationships
        modelBuilder.Entity<Invoice>()
            .HasOne(i => i.Order)
            .WithMany(o => o.Invoices)
            .HasForeignKey(i => i.OrderId)
            .HasConstraintName("fk_invoice_order");

        // Configure Return relationships
        modelBuilder.Entity<Return>()
            .HasOne(r => r.Invoice)
            .WithMany(i => i.Returns)
            .HasForeignKey(r => r.InvoiceId)
            .HasConstraintName("fk_return_invoice");

        modelBuilder.Entity<ReturnLine>()
            .HasOne(rl => rl.Return)
            .WithMany(r => r.ReturnLines)
            .HasForeignKey(rl => rl.ReturnId)
            .HasConstraintName("fk_rl_return");

        modelBuilder.Entity<ReturnLine>()
            .HasOne(rl => rl.OrderLine)
            .WithMany(ol => ol.ReturnLines)
            .HasForeignKey(rl => rl.OrderLineId)
            .HasConstraintName("fk_rl_order_line");

        // Configure Purchase Order relationships
        modelBuilder.Entity<PurchaseOrder>()
            .HasOne(po => po.Publisher)
            .WithMany(p => p.PurchaseOrders)
            .HasForeignKey(po => po.PublisherId)
            .HasConstraintName("fk_po_publisher");

        modelBuilder.Entity<PurchaseOrder>()
            .HasMany(po => po.PurchaseOrderLines)
            .WithOne(pol => pol.PurchaseOrder)
            .HasForeignKey(pol => pol.PoId)
            .HasConstraintName("fk_pol_po");

        modelBuilder.Entity<PurchaseOrderLine>()
            .HasOne(pol => pol.Book)
            .WithMany(b => b.PurchaseOrderLines)
            .HasForeignKey(pol => pol.Isbn)
            .HasConstraintName("fk_pol_book");

        // Configure Goods Receipt relationships
        modelBuilder.Entity<GoodsReceipt>()
            .HasOne(gr => gr.PurchaseOrder)
            .WithMany(po => po.GoodsReceipts)
            .HasForeignKey(gr => gr.PoId)
            .HasConstraintName("fk_gr_po");

        modelBuilder.Entity<GoodsReceipt>()
            .HasMany(gr => gr.GoodsReceiptLines)
            .WithOne(grl => grl.GoodsReceipt)
            .HasForeignKey(grl => grl.GrId)
            .HasConstraintName("fk_grl_gr");

        // Configure Role-Permission many-to-many
        modelBuilder.Entity<RolePermission>()
            .HasKey(rp => new { rp.RoleId, rp.PermissionId });

        modelBuilder.Entity<RolePermission>()
            .HasOne(rp => rp.Role)
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(rp => rp.RoleId)
            .HasConstraintName("fk_rp_role");

        modelBuilder.Entity<RolePermission>()
            .HasOne(rp => rp.Permission)
            .WithMany(p => p.RolePermissions)
            .HasForeignKey(rp => rp.PermissionId)
            .HasConstraintName("fk_rp_permission");

        // Configure unique constraints
        modelBuilder.Entity<Account>()
            .HasIndex(a => a.Email)
            .IsUnique();

        modelBuilder.Entity<Role>()
            .HasIndex(r => r.Name)
            .IsUnique();

        modelBuilder.Entity<Category>()
            .HasIndex(c => c.Name)
            .IsUnique();

        modelBuilder.Entity<Publisher>()
            .HasIndex(p => p.Name)
            .IsUnique();

        modelBuilder.Entity<Permission>()
            .HasIndex(p => p.Code)
            .IsUnique();

        // PriceChange mapping to existing table price_change
        modelBuilder.Entity<PriceChange>(entity =>
        {
            entity.ToTable("price_change");
            entity.HasKey(pc => new { pc.Isbn, pc.ChangedAt });
            entity.Property(pc => pc.Isbn).HasColumnName("isbn");
            entity.Property(pc => pc.OldPrice).HasColumnName("old_price").HasColumnType("decimal(12,2)");
            entity.Property(pc => pc.NewPrice).HasColumnName("new_price").HasColumnType("decimal(12,2)");
            entity.Property(pc => pc.ChangedAt).HasColumnName("changed_at");
            entity.Property(pc => pc.EmployeeId).HasColumnName("employee_id");
        });

        // Configure enum conversions
        modelBuilder.Entity<Author>()
            .Property(a => a.Gender)
            .HasConversion<string>();

        // Configure check constraints
        modelBuilder.Entity<Book>()
            .HasCheckConstraint("book_chk_1", "page_count > 0")
            .HasCheckConstraint("book_chk_2", "unit_price >= 0");

        modelBuilder.Entity<OrderLine>()
            .HasCheckConstraint("order_line_chk_1", "qty > 0")
            .HasCheckConstraint("order_line_chk_2", "unit_price >= 0");

        modelBuilder.Entity<PurchaseOrderLine>()
            .HasCheckConstraint("purchase_order_line_chk_1", "qty_ordered > 0")
            .HasCheckConstraint("purchase_order_line_chk_2", "unit_price >= 0");

        modelBuilder.Entity<GoodsReceiptLine>()
            .HasCheckConstraint("goods_receipt_line_chk_1", "qty_received > 0")
            .HasCheckConstraint("goods_receipt_line_chk_2", "unit_cost >= 0");

        modelBuilder.Entity<ReturnLine>()
            .HasCheckConstraint("return_line_chk_1", "qty_returned > 0")
            .HasCheckConstraint("return_line_chk_2", "amount >= 0");

        modelBuilder.Entity<Invoice>()
            .HasCheckConstraint("invoice_chk_1", "total_amount >= 0")
            .HasCheckConstraint("invoice_chk_2", "tax_amount >= 0");

        // Seed default roles
        modelBuilder.Entity<Role>().HasData(
            new Role { RoleId = 1, Name = "CUSTOMER", Description = "Khách hàng" },
            new Role { RoleId = 2, Name = "EMPLOYEE", Description = "Nhân viên" },
            new Role { RoleId = 3, Name = "ADMIN", Description = "Quản trị viên" }
        );
    }
}
