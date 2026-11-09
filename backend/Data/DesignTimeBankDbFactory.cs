using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace SecureBank.Data;
public class DesignTimeBankDbFactory : IDesignTimeDbContextFactory<BankDb> {
 public BankDb CreateDbContext(string[] args) {
  var connection=Environment.GetEnvironmentVariable("ConnectionStrings__Bank")
   ??throw new InvalidOperationException("Set ConnectionStrings__Bank for EF tools.");
  return new BankDb(new DbContextOptionsBuilder<BankDb>().UseNpgsql(connection).Options);
 }
}
