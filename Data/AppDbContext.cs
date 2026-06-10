using LeaveSystem.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace LeaveSystem.Data;

/// <summary>
/// 應用程式資料庫的進入點（EF Core DbContext）。
///
/// ── EF Core Code First 概念（給初學者） ──
/// 1. DbContext 是「資料庫連線 + 物件追蹤」的容器，每次 HTTP 請求建立一份。
/// 2. 每個 DbSet&lt;T&gt; 對應到資料庫中的一張表。
/// 3. 不需要手寫 CREATE TABLE，EF Core 會根據 Entity 類別自動產生。
/// 4. 透過「Migration」把模型變更同步到資料庫：
///       PowerShell: dotnet ef migrations add 名稱     ← 產生變更腳本
///                   dotnet ef database update         ← 套用到資料庫
/// 5. 不要直接到 SQL Server 改表結構，否則會與 Migration 不同步！
///
/// ── OnModelCreating ──
/// 當 EF Core 啟動時會呼叫此方法，由我們補充「靠標記屬性無法表達的設定」：
///   - 唯一索引（如 Username 不可重複）
///   - 複合主鍵（如 UserRole 用 UserId+RoleId 當主鍵）
///   - 多重 FK 關係（如 Cohort 對 UserAccount 有 4 個關聯）
///   - 外鍵刪除行為（避免循環刪除錯誤）
/// </summary>
public class AppDbContext : DbContext
{
    /// <summary>
    /// 由 DI 容器注入連線設定（在 Program.cs 用 AddDbContext 註冊）。
    /// </summary>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // ─── 資料表（DbSet） ───
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Cohort> Cohorts => Set<Cohort>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<ApprovalRule> ApprovalRules => Set<ApprovalRule>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<LeaveRequestStep> LeaveRequestSteps => Set<LeaveRequestStep>();
    public DbSet<AbsenceRecord> AbsenceRecords => Set<AbsenceRecord>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>
    /// 模型細部設定。
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ─── UserAccount：Username 唯一索引 ───
        modelBuilder.Entity<UserAccount>()
            .HasIndex(u => u.Username)
            .IsUnique();

        // UserAccount → Cohort（學員所屬班期）
        // 設 Restrict：班期被學員引用時，禁止直接刪除班期，避免孤兒資料。
        modelBuilder.Entity<UserAccount>()
            .HasOne(u => u.Cohort)
            .WithMany(c => c.Students)
            .HasForeignKey(u => u.CohortId)
            .OnDelete(DeleteBehavior.Restrict);

        // ─── Role：Name 唯一索引 ───
        modelBuilder.Entity<Role>()
            .HasIndex(r => r.Name)
            .IsUnique();

        // ─── UserRole：複合主鍵 ───
        modelBuilder.Entity<UserRole>()
            .HasKey(ur => new { ur.UserId, ur.RoleId });

        modelBuilder.Entity<UserRole>()
            .HasOne(ur => ur.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserRole>()
            .HasOne(ur => ur.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(ur => ur.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        // ─── Cohort 對 UserAccount 的 3 個簽核人關係 ───
        // EF Core 看到 UserAccount 與 Cohort 之間有多條路徑時無法自動推斷，
        // 必須明確指定每條 FK，並用 Restrict 避免循環刪除。
        modelBuilder.Entity<Cohort>()
            .HasOne(c => c.Tutor)
            .WithMany()
            .HasForeignKey(c => c.TutorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Cohort>()
            .HasOne(c => c.SectionChief)
            .WithMany()
            .HasForeignKey(c => c.SectionChiefUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Cohort>()
            .HasOne(c => c.BranchDirector)
            .WithMany()
            .HasForeignKey(c => c.BranchDirectorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // ─── LeaveType：Code 唯一 ───
        modelBuilder.Entity<LeaveType>()
            .HasIndex(t => t.Code)
            .IsUnique();

        // ─── LeaveRequest：精確度設定（decimal 預設精度不足） ───
        modelBuilder.Entity<LeaveRequest>()
            .Property(r => r.TotalHours)
            .HasPrecision(6, 2);

        modelBuilder.Entity<LeaveRequest>()
            .HasOne(r => r.Student)
            .WithMany()
            .HasForeignKey(r => r.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LeaveRequest>()
            .HasOne(r => r.LeaveType)
            .WithMany()
            .HasForeignKey(r => r.LeaveTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // ─── LeaveRequestStep ───
        modelBuilder.Entity<LeaveRequestStep>()
            .HasOne(s => s.LeaveRequest)
            .WithMany(r => r.Steps)
            .HasForeignKey(s => s.LeaveRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<LeaveRequestStep>()
            .HasOne(s => s.Approver)
            .WithMany()
            .HasForeignKey(s => s.ApproverUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LeaveRequestStep>()
            .HasIndex(s => new { s.LeaveRequestId, s.Level })
            .IsUnique();

        // ─── ApprovalRule：精確度 ───
        modelBuilder.Entity<ApprovalRule>()
            .Property(r => r.MinHours)
            .HasPrecision(6, 2);

        modelBuilder.Entity<ApprovalRule>()
            .Property(r => r.MaxHours)
            .HasPrecision(6, 2);

        // ─── AbsenceRecord ───
        modelBuilder.Entity<AbsenceRecord>()
            .Property(a => a.Hours)
            .HasPrecision(6, 2);

        modelBuilder.Entity<AbsenceRecord>()
            .HasOne(a => a.Student)
            .WithMany()
            .HasForeignKey(a => a.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AbsenceRecord>()
            .HasOne(a => a.CreatedByUser)
            .WithMany()
            .HasForeignKey(a => a.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // ─── Notification ───
        modelBuilder.Entity<Notification>()
            .HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Notification>()
            .HasIndex(n => new { n.UserId, n.IsRead });

        // ─── AuditLog ───
        modelBuilder.Entity<AuditLog>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<AuditLog>()
            .HasIndex(a => a.CreatedAt);
    }
}
