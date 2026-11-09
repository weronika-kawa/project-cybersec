using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureBank.Data;
using SecureBank.DTOs;
using SecureBank.Models;
using SecureBank.Services;
namespace SecureBank.Controllers;
[ApiController,Route("api/admin"),Authorize(Roles="ADMIN")]
public class AdminController(BankDb db,UserManager<User> users,Audit audit) : ControllerBase {
 [HttpGet("users")] public async Task<IActionResult> Users() {
  var list=await db.Users.OrderBy(x=>x.Email).Take(200).ToListAsync();var output=new List<object>();
  foreach(var u in list) output.Add(new {u.Id,u.Email,u.DisplayName,u.IsBlocked,u.LockoutEnd,roles=await users.GetRolesAsync(u)});
  return Ok(output);
 }
 [HttpPut("users/{id}/block")] public async Task<IActionResult> Block(string id,BlockRequest r) {
  if(id==User.FindFirstValue(ClaimTypes.NameIdentifier))return BadRequest(new {message="Nie można blokować własnego konta administratora."});
  await using var tx=await db.Database.BeginTransactionAsync();
  var u=await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\"={id} FOR UPDATE").SingleOrDefaultAsync();if(u is null)return NotFound();
  // Admins cannot be blocked here; demotion also preserves the last admin.
  if(r.Blocked&&await users.IsInRoleAsync(u,"ADMIN"))return BadRequest(new {message="Najpierw kontrolowana zmiana roli administratora."});
  u.IsBlocked=r.Blocked;u.LockoutEnd=null;u.AccessFailedCount=0;u.SecurityStamp=Guid.NewGuid().ToString();
  await audit.Write(u.Id,r.Blocked?"ACCOUNT_LOCKED":"ACCOUNT_UNLOCKED","Administrator: "+User.FindFirstValue(ClaimTypes.NameIdentifier),"WARNING");await tx.CommitAsync();return Ok(new {message="Status konta zmieniony."});
 }
 [HttpPut("users/{id}/role")] public async Task<IActionResult> Role(string id,RoleRequest r) {
  if(id==User.FindFirstValue(ClaimTypes.NameIdentifier))return BadRequest(new {message="Nie można zmieniać własnej roli."});
  await using var tx=await db.Database.BeginTransactionAsync();
  // Global role lock prevents concurrent removal of the last administrator.
  await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(731904)");
  var u=await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\"={id} FOR UPDATE").SingleOrDefaultAsync();if(u is null)return NotFound();
  var old=await users.GetRolesAsync(u);
  if(old.Contains("ADMIN")&&r.Role!="ADMIN"&&(await users.GetUsersInRoleAsync("ADMIN")).Count<=1)return BadRequest(new {message="Ostatni administrator musi pozostać."});
  var removed=await users.RemoveFromRolesAsync(u,old);if(!removed.Succeeded)throw new InvalidOperationException("Role removal failed");
  var added=await users.AddToRoleAsync(u,r.Role);if(!added.Succeeded)throw new InvalidOperationException("Role assignment failed");
  if(r.Role=="CLIENT"&&!await db.Accounts.AnyAsync(x=>x.UserId==id))db.Accounts.Add(new Account {UserId=id,AccountNumber="SB"+Guid.NewGuid().ToString("N").ToUpperInvariant()});
  u.SecurityStamp=Guid.NewGuid().ToString();await audit.Write(id,"ROLE_CHANGED",$"Administrator {User.FindFirstValue(ClaimTypes.NameIdentifier)}: {string.Join(',',old)} → {r.Role}","WARNING");await tx.CommitAsync();return Ok(new {message="Rola zmieniona; poprzednie sesje unieważnione."});
 }
 [HttpGet("logs")] public async Task<IActionResult> Logs(string? userId,string? type,string? severity,DateTimeOffset? from,DateTimeOffset? to) {
  var q=db.SecurityLogs.AsQueryable();if(userId is not null)q=q.Where(x=>x.UserId==userId);if(type is not null)q=q.Where(x=>x.EventType==type);if(severity is not null)q=q.Where(x=>x.Severity==severity);if(from.HasValue)q=q.Where(x=>x.Timestamp>=from);if(to.HasValue)q=q.Where(x=>x.Timestamp<=to);
  return Ok(await q.OrderByDescending(x=>x.Timestamp).Take(500).ToListAsync());
 }
 [HttpGet("alerts")] public async Task<IActionResult> Alerts(string? userId,string? type,string? severity,DateTimeOffset? from,DateTimeOffset? to) {
  var q=db.SecurityAlerts.AsQueryable();if(userId is not null)q=q.Where(x=>x.UserId==userId);if(type is not null)q=q.Where(x=>x.Type==type);if(severity is not null)q=q.Where(x=>x.Severity==severity);if(from.HasValue)q=q.Where(x=>x.CreatedAt>=from);if(to.HasValue)q=q.Where(x=>x.CreatedAt<=to);
  return Ok(await q.OrderByDescending(x=>x.CreatedAt).Take(500).ToListAsync());
 }
 [HttpPut("alerts/{id:long}/resolve")] public async Task<IActionResult> Resolve(long id) {var a=await db.SecurityAlerts.FindAsync(id);if(a is null)return NotFound();a.Resolved=true;a.ResolvedAt=DateTimeOffset.UtcNow;await audit.Write(User.FindFirstValue(ClaimTypes.NameIdentifier),"ALERT_RESOLVED",$"Zamknięto alert {id}.");return Ok(new {message="Alert rozwiązany."});}
}
[ApiController,Route("api/users"),Authorize(Roles="EMPLOYEE")]
public class EmployeeController(BankDb db,Audit audit) : ControllerBase {
 [HttpGet] public async Task<IActionResult> Clients() {
  await audit.Write(User.FindFirstValue(ClaimTypes.NameIdentifier),"CLIENT_LIST_VIEWED","Pracownik przegląda ograniczony katalog klientów.");
  return Ok(await db.Users.Where(u=>db.UserRoles.Any(ur=>ur.UserId==u.Id&&db.Roles.Any(r=>r.Id==ur.RoleId&&r.Name=="CLIENT"))).Select(x=>new {x.Id,x.DisplayName,x.IsBlocked}).Take(100).ToListAsync());
 }
}
