using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecureBank.Models;
namespace SecureBank.Data;
public static class Seed {
 public static async Task Run(IServiceProvider provider) {
  var db=provider.GetRequiredService<BankDb>();await db.Database.MigrateAsync();
  var roles=provider.GetRequiredService<RoleManager<IdentityRole>>();foreach(var role in new[]{"CLIENT","EMPLOYEE","ADMIN"})if(!await roles.RoleExistsAsync(role)){var result=await roles.CreateAsync(new IdentityRole(role));if(!result.Succeeded)throw new InvalidOperationException("Seed role failed");}
  var cfg=provider.GetRequiredService<IConfiguration>();var env=provider.GetRequiredService<IHostEnvironment>();
  if(!env.IsDevelopment()||!cfg.GetValue<bool>("Demo:Enabled"))return;
  var password=cfg["Demo:Password"]??throw new InvalidOperationException("Set Demo:Password through environment.");
  var users=provider.GetRequiredService<UserManager<User>>();
  await using var tx=await db.Database.BeginTransactionAsync();
  await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(731905)");
  foreach(var (name,role,number,balance) in new[]{("anna","CLIENT","SB00000000000000000000000001",25000m),("jan","CLIENT","SB00000000000000000000000002",12000m),("ewa","CLIENT","SB00000000000000000000000003",8000m),("employee","EMPLOYEE","",0m),("admin","ADMIN","",0m)}) {
   var email=name+"@securebank.test";var u=await users.FindByEmailAsync(email);
   if(u is null) {
    u=new User {UserName=email,Email=email,DisplayName=name,EmailConfirmed=true};var created=await users.CreateAsync(u,password);if(!created.Succeeded)throw new InvalidOperationException(string.Join(";",created.Errors.Select(x=>x.Description)));
    var assigned=await users.AddToRoleAsync(u,role);if(!assigned.Succeeded)throw new InvalidOperationException("Seed role failed");
    if(role=="CLIENT")db.Accounts.Add(new Account {UserId=u.Id,AccountNumber=number,Balance=balance});
   }
  }
  await db.SaveChangesAsync();
  if(!await db.Transactions.AnyAsync()) {
   var a=await db.Accounts.SingleAsync(x=>x.AccountNumber=="SB00000000000000000000000001");var b=await db.Accounts.SingleAsync(x=>x.AccountNumber=="SB00000000000000000000000002");
   for(var i=1;i<=3;i++){a.Balance-=100;b.Balance+=100;db.Transactions.Add(new Transaction {FromAccountId=a.Id,ToAccountId=b.Id,Amount=100,Title="Demonstracyjny przelew "+i,Timestamp=DateTimeOffset.UtcNow.AddDays(-i)});}
   await db.SaveChangesAsync();
  }
  await tx.CommitAsync();
 }
}
