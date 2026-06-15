using BomChecker.Models;
using Microsoft.EntityFrameworkCore;

namespace BomChecker.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<BomReport> BomReports { get; set; }
        public DbSet<BomRowEntity> BomRows { get; set; }
        public DbSet<PartResultEntity> PartResults { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BomReport>()
                .HasMany(r => r.Rows)
                .WithOne(r => r.Report)
                .HasForeignKey(r => r.ReportId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BomRowEntity>()
                .HasMany(r => r.PartResults)
                .WithOne(r => r.BomRow)
                .HasForeignKey(r => r.BomRowId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
