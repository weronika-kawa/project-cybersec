using Microsoft.EntityFrameworkCore;
using SecureBank.Data;
using SecureBank.Models;
namespace SecureBank.Services;
public class Audit(BankDb db, IHttpContextAccessor accessor, IConfiguration config) {
 public async Task Write(string? userId,string type,string description,string severity="INFO",bool alert=false) {
  var h=accessor.HttpContext;
  db.SecurityLogs.Add(new SecurityLog {UserId=userId,EventType=type,Description=description,Severity=severity,IpAddress=h?.Connection.RemoteIpAddress?.ToString()??"",Endpoint=h?.Request.Path.Value??""});
  if(alert) db.SecurityAlerts.Add(new SecurityAlert {UserId=userId,Type=type,Description=description,Severity=severity});
  await db.SaveChangesAsync();
 }
 public async Task Denied(string? userId,string detail) {
  var adminAttempt=accessor.HttpContext?.Request.Path.StartsWithSegments("/api/admin")==true;
  await Write(userId,"UNAUTHORIZED_ACCESS",detail,"WARNING",adminAttempt);
  if(userId is null) return;
  var since=DateTimeOffset.UtcNow.AddMinutes(-10);
  var count=await db.SecurityLogs.CountAsync(x=>x.UserId==userId&&x.EventType=="UNAUTHORIZED_ACCESS"&&x.Timestamp>since);
  if(count>=config.GetValue("Risk:AccessLimit",3)) {
   var u=await db.Users.FindAsync(userId);
   if(u is not null&&!u.IsBlocked) {u.IsBlocked=true; u.SecurityStamp=Guid.NewGuid().ToString(); await Write(userId,"ACCOUNT_LOCKED","Powtarzający się niedozwolony dostęp. Konto zablokowane.","CRITICAL",true);}
  }
 }
}
