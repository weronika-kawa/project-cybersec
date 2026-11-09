using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecureBank.Data;
using SecureBank.DTOs;
using SecureBank.Models;
namespace SecureBank.Services;
public record TransferResult(int Status,object Body);
public class TransferService(BankDb db, Audit audit,IConfiguration config,UserManager<User> users,IHostEnvironment env) {
 public static bool ValidAmount(decimal amount)=>amount>0&&amount<=1000000000m&&decimal.Round(amount,2)==amount;
 public async Task<TransferResult> Start(string uid,TransferRequest r) {
  if(!ValidAmount(r.Amount)||string.IsNullOrWhiteSpace(r.Title)||r.Title.Length>140) return await Fail(uid,"Nieprawidłowa kwota lub tytuł.");
  var from=await db.Accounts.FindAsync(r.FromAccountId);
  if(from is null||from.UserId!=uid) {await audit.Denied(uid,"Próba przelewu z cudzego rachunku.");return new(404,new {message="Nie znaleziono rachunku."});}
  var to=await db.Accounts.SingleOrDefaultAsync(x=>x.AccountNumber==r.ToAccountNumber);
  if(to is null||to.Id==from.Id||!from.IsActive||!to.IsActive||from.Currency!=to.Currency) return await Fail(uid,"Nieprawidłowy odbiorca lub nieaktywny rachunek.");
  if(from.Balance<r.Amount) return await Fail(uid,"Niewystarczające saldo.");
  if(r.Amount>=config.GetValue("Risk:OtpThreshold",5000m)) {
   var code=RandomNumberGenerator.GetInt32(100000,1000000).ToString();
   var otp=new OtpChallenge {UserId=uid,FromAccountId=from.Id,ToAccountId=to.Id,Amount=r.Amount,Title=r.Title};
   otp.CodeHash=Hash(otp.Id,code); db.OtpChallenges.Add(otp);
   await audit.Write(uid,"HIGH_RISK_TRANSFER","Wymagane potwierdzenie OTP.","WARNING",r.Amount>=config.GetValue("Risk:AlertThreshold",15000m));
   var demo=env.IsDevelopment()&&config.GetValue<bool>("Demo:Enabled");
   return new(202,new {challengeId=otp.Id,expiresAt=otp.ExpiresAt,demoCode=demo?code:null,message="Potwierdź przelew OTP."});
  }
  return await Execute(uid,from.Id,to.Id,r.Amount,r.Title,null,null);
 }
 public Task<TransferResult> Confirm(string uid,Guid id,string code)=>Execute(uid,Guid.Empty,Guid.Empty,0,"",id,code);
 private async Task<TransferResult> Execute(string uid,Guid fromId,Guid toId,decimal amount,string title,Guid? challengeId,string? code) {
  await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
  // A user lock serializes transfers, role/block updates, and OTP attempts for this user.
  var user=await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\"={uid} FOR UPDATE").SingleAsync();
  await db.Entry(user).ReloadAsync();
  if(user.IsBlocked||!await users.IsInRoleAsync(user,"CLIENT")) {await tx.RollbackAsync();return new(403,new {message="Operacja niedozwolona."});}
  OtpChallenge? otp=null;
  if(challengeId.HasValue) {
   otp=await db.OtpChallenges.SingleOrDefaultAsync(x=>x.Id==challengeId&&x.UserId==uid);
   if(otp is null||otp.Used||otp.ExpiresAt<=DateTimeOffset.UtcNow||otp.Attempts>=3) {await tx.RollbackAsync();return await Fail(uid,"OTP wygasł lub został wykorzystany.");}
   if(!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(otp.CodeHash),Convert.FromHexString(Hash(otp.Id,code??"")))) {
    otp.Attempts++; if(otp.Attempts>=3) {user.IsBlocked=true;user.SecurityStamp=Guid.NewGuid().ToString();}
    await audit.Write(uid,"OTP_FAILED","Niepoprawny kod OTP.","WARNING",otp.Attempts>=3);
    if(user.IsBlocked) await audit.Write(uid,"ACCOUNT_LOCKED","Blokada po błędnych OTP.","CRITICAL",true);
    await tx.CommitAsync(); return new(400,new {message="Niepoprawny OTP."});
   }
   fromId=otp.FromAccountId;toId=otp.ToAccountId;amount=otp.Amount;title=otp.Title;
  }
  // Stable ordering prevents deadlocks for opposite-direction transfers.
  var accounts=await db.Accounts.FromSqlInterpolated($"SELECT * FROM \"Accounts\" WHERE \"Id\"={fromId} OR \"Id\"={toId} ORDER BY \"Id\" FOR UPDATE").AsNoTracking().ToListAsync();
  var from=accounts.SingleOrDefault(x=>x.Id==fromId);var to=accounts.SingleOrDefault(x=>x.Id==toId);
  if(otp is not null&&otp.ExpiresAt<=DateTimeOffset.UtcNow){await tx.RollbackAsync();return await Fail(uid,"OTP wygasł.");}
  if(from is null||to is null||from.UserId!=uid||!from.IsActive||!to.IsActive||from.Currency!=to.Currency||fromId==toId||!ValidAmount(amount)||from.Balance<amount) {
   await tx.RollbackAsync(); return await Fail(uid,"Przelew odrzucony po ponownej autoryzacji lub sprawdzeniu salda.");
  }
  var recent=DateTimeOffset.UtcNow.AddMinutes(-1);
  var count=await db.Transactions.CountAsync(x=>x.FromAccount.UserId==uid&&x.Timestamp>recent);
  if(count>=config.GetValue("Risk:BurstCount",5)) {
   user.IsBlocked=true;user.SecurityStamp=Guid.NewGuid().ToString(); await audit.Write(uid,"TRANSFER_FAILED","Zbyt wiele przelewów w krótkim czasie. Blokada konta.","CRITICAL",true);await audit.Write(uid,"ACCOUNT_LOCKED","Automatyczna blokada po serii przelewów.","CRITICAL");await tx.CommitAsync();return new(403,new {message="Ograniczenie po podejrzanej aktywności."});
  }
  // Raw, parameterized updates use the fresh locked balances, avoiding stale EF tracking.
  await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Accounts\" SET \"Balance\"=\"Balance\"-{amount} WHERE \"Id\"={fromId}");
  await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Accounts\" SET \"Balance\"=\"Balance\"+{amount} WHERE \"Id\"={toId}");
  var transfer=new Transaction {FromAccountId=fromId,ToAccountId=toId,Amount=amount,Title=title};db.Transactions.Add(transfer);
  if(otp is not null) otp.Used=true;
  await audit.Write(uid,"TRANSFER_SUCCESS","Przelew wykonany atomowo.");
  await tx.CommitAsync();return new(200,new {transfer.Id,transfer.Status,transfer.Amount,transfer.Timestamp});
 }
 private async Task<TransferResult> Fail(string uid,string message) {await audit.Write(uid,"TRANSFER_FAILED",message,"WARNING");return new(400,new {message});}
 private static string Hash(Guid id,string code)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id+":"+code)));
}
