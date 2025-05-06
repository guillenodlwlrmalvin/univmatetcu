using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using UnivMate.Models;
using System;
using UnivMate.Utilities;

namespace UnivMate.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Reports> Reports { get; set; } // Changed from Reports to Report (singular)
        public DbSet<ReportStatusHistory> ReportStatusHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<ReportStatusHistory>()
        .HasOne(h => h.Report)
        .WithMany(r => r.StatusHistory)
        .HasForeignKey(h => h.ReportId);
            // Configure indexes and relationships first
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<Reports>()
                .HasOne(r => r.User)
                .WithMany(u => u.SubmittedReports)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Reports>()
                .HasOne(r => r.ResolvedBy)
                .WithMany()
                .HasForeignKey(r => r.ResolvedById)
                .OnDelete(DeleteBehavior.Restrict);

            // Seed admin user with STATIC values
            modelBuilder.Entity<User>().HasData(
    new User
    {
        Id = 1,
        Username = "admin",
        Email = "admin@univmate.com",
        PasswordHash = PasswordHelper.HashPassword("Admin@123"),
        FirstName = "System",
        LastName = "Administrator",
        Role = "Admin",
        StaffId = "ADM001",
        OfficeLocation = "Main Administration",
        CreatedAt = new DateTime(2023, 1, 1), // Static date instead of DateTime.UtcNow
        LastLoginDate = null, // Initially null
        SubmittedReports = new List<Reports>() // Initialize collection
    }
            );

        }
    }
}