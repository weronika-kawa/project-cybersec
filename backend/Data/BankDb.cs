using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SecureBank.Models;
namespace SecureBank.Data;
public class BankDb(DbContextOptions<BankDb> options) : IdentityDbContext<User>(options) {
 public DbSet<Account> Accounts => Set<Account>(); public DbSet<Transaction> Transactions => Set<Transaction>();
 public DbSet<SecurityLog> SecurityLogs => Set<SecurityLog>(); public DbSet<SecurityAlert> SecurityAlerts => Set<SecurityAlert>();
 public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>(); public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();
 protected override void OnModelCreating(ModelBuilder b) {
  base.OnModelCreating(b);
  b.Entity<Account>().HasIndex(x=>x.AccountNumber).IsUnique(); b.Entity<Account>().HasIndex(x=>x.UserId);
  b.Entity<Account>().Property(x=>x.Balance).HasPrecision(18,2);
  b.Entity<Account>().ToTable(t=>t.HasCheckConstraint("CK_balance", "\"Balance\" >= 0"));
  b.Entity<Account>().HasOne(x=>x.User).WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Restrict);
  b.Entity<Transaction>().Property(x=>x.Amount).HasPrecision(18,2);
  b.Entity<Transaction>().ToTable(t=>t.HasCheckConstraint("CK_amount", "\"Amount\" > 0 AND \"FromAccountId\" <> \"ToAccountId\""));
  b.Entity<Transaction>().HasOne(x=>x.FromAccount).WithMany().HasForeignKey(x=>x.FromAccountId).OnDelete(DeleteBehavior.Restrict);
  b.Entity<Transaction>().HasOne(x=>x.ToAccount).WithMany().HasForeignKey(x=>x.ToAccountId).OnDelete(DeleteBehavior.Restrict);
  b.Entity<Transaction>().HasIndex(x=>new {x.FromAccountId,x.Timestamp}); b.Entity<Transaction>().HasIndex(x=>new {x.ToAccountId,x.Timestamp});
  b.Entity<SecurityLog>().HasIndex(x=>new {x.UserId,x.EventType,x.Timestamp});
  b.Entity<SecurityAlert>().HasIndex(x=>new {x.Resolved,x.CreatedAt});
  b.Entity<LoginAttempt>().HasIndex(x=>new {x.UserId,x.Timestamp});
  b.Entity<OtpChallenge>().Property(x=>x.Amount).HasPrecision(18,2);
  b.Entity<OtpChallenge>().HasOne<User>().WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Restrict);
  b.Entity<OtpChallenge>().HasOne<Account>().WithMany().HasForeignKey(x=>x.FromAccountId).OnDelete(DeleteBehavior.Restrict);
  b.Entity<OtpChallenge>().HasOne<Account>().WithMany().HasForeignKey(x=>x.ToAccountId).OnDelete(DeleteBehavior.Restrict);
  b.Entity<OtpChallenge>().ToTable(t=>t.HasCheckConstraint("CK_otp_amount", "\"Amount\" > 0"));
 }
}
