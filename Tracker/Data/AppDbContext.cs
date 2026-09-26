using Microsoft.EntityFrameworkCore;
using Tracker.Data.Entity;

namespace Tracker.Data
{
    public sealed class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<LocalSessionEntity> LocalSessions => Set<LocalSessionEntity>();
        public DbSet<EmployeeEntity> EmployeeRecord => Set<EmployeeEntity>();
        public DbSet<EventsEntity> Events => Set<EventsEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            ConfigureLocalSession(modelBuilder);
            ConfigureEmployeeRecord(modelBuilder);
            ConfigureEvents(modelBuilder);
        }

        private static void ConfigureLocalSession(ModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<LocalSessionEntity>();
            entity.ToTable("local_sessions");

            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever().IsRequired();
            entity.Property(x => x.EmployeeId).ValueGeneratedNever().IsRequired();
            entity.Property(x => x.Date).ValueGeneratedNever().IsRequired();

            // Fixed: Added string conversion
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).IsRequired();

            entity.Property(x => x.CheckInTime).IsRequired();
            entity.Property(x => x.CheckOutTime);
            entity.Property(x => x.LastHeartbeatTime);
            entity.Property(x => x.IsOfflineSession).IsRequired();

            entity.HasIndex(x => x.Date);
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.Status);
        }

        private static void ConfigureEmployeeRecord(ModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<EmployeeEntity>();
            entity.ToTable("employee_record");

            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.EmployeeId).ValueGeneratedNever().IsRequired();
            entity.Property(x => x.EmployeeCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.EmployeeName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PinHash).IsRequired();
            entity.Property(x => x.Department);
            entity.Property(x => x.Designation);

            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.Email);
        }

        private static void ConfigureEvents(ModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<EventsEntity>();
            entity.ToTable("events");

            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.LocalSessionId).ValueGeneratedNever().IsRequired();

            // Fixed: Added string conversion
            entity.Property(x => x.EventType).HasConversion<string>().HasMaxLength(50).IsRequired();

            entity.Property(x => x.EventTime).IsRequired();

            // Fixed: Removed .IsRequired() to allow nulls
            entity.Property(x => x.MetaData);
            entity.Property(x => x.IsSynced).IsRequired();

            entity.HasIndex(x => x.LocalSessionId);
            entity.HasIndex(x => x.EventType);
            entity.HasIndex(x => x.IsSynced);
        }
    }
}