using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureBank.Data;
using SecureBank.DTOs;
using SecureBank.Models;
using SecureBank.Services;
using System.Security.Claims;
namespace SecureBank.Controllers;
[ApiController,Route("api/auth"),Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
public class AuthController(UserManager<User> users,SignInManager<User> signIn,BankDb db,Audit audit,IConfiguration config,IHostEnvironment env) : ControllerBase {
 [HttpGet("csrf"),AllowAnonymous] public IActionResult Csrf([FromServices] IAntiforgery antiforgery)=>Ok(new {token=antiforgery.GetAndStoreTokens(HttpContext).RequestToken});
 [HttpPost("register"),AllowAnonymous] public async Task<IActionResult> Register(RegisterRequest r) {
  var u=new User {UserName=r.Email,Email=r.Email,DisplayName=r.DisplayName};
  await using var tx=await db.Database.BeginTransactionAsync();
  var result=await users.CreateAsync(u,r.Password);if(!result.Succeeded)return BadRequest(new {message=string.Join(" ",result.Errors.Select(x=>x.Description))});
  var role=await users.AddToRoleAsync(u,"CLIENT");if(!role.Succeeded)throw new InvalidOperationException("Cannot assign client role");
  db.Accounts.Add(new Account {UserId=u.Id,AccountNumber="SB"+Guid.NewGuid().ToString("N").ToUpperInvariant(),Balance=0});
  await db.SaveChangesAsync();await tx.CommitAsync();return Ok(new {message="Konto utworzone. Zaloguj się."});
 }
 [HttpPost("login"),AllowAnonymous] public async Task<IActionResult> Login(LoginRequest r) {
  var u=await users.FindByEmailAsync(r.Email);
  Microsoft.AspNetCore.Identity.SignInResult result=Microsoft.AspNetCore.Identity.SignInResult.Failed;
  if(u is not null) {
   await using var tx=await db.Database.BeginTransactionAsync();
   u=await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\"={u.Id} FOR UPDATE").SingleAsync();
   await db.Entry(u).ReloadAsync();
   if(!u.IsBlocked)result=await signIn.CheckPasswordSignInAsync(u,r.Password,true);
   db.LoginAttempts.Add(new LoginAttempt {UserId=u.Id,Success=result.Succeeded,IpAddress=HttpContext.Connection.RemoteIpAddress?.ToString()??""});
   await audit.Write(u.Id,result.Succeeded?"LOGIN_SUCCESS":"LOGIN_FAILED",result.Succeeded?"Poprawne logowanie.":"Nieudana próba logowania.",result.Succeeded?"INFO":"WARNING");
   if(result.IsLockedOut)await audit.Write(u.Id,"ACCOUNT_LOCKED","Blokada logowania przez Identity.","CRITICAL",true);
   await tx.CommitAsync();
  } else {
   // Run the same expensive password hash check for unknown accounts.
   _=new PasswordHasher<User>().VerifyHashedPassword(new User(),DummyHash.Value,r.Password);
   db.LoginAttempts.Add(new LoginAttempt {Success=false,IpAddress=HttpContext.Connection.RemoteIpAddress?.ToString()??""});await audit.Write(null,"LOGIN_FAILED","Nieudana próba logowania.","WARNING");
  }
  if(!result.Succeeded)return Unauthorized(new {message="Niepoprawne dane lub konto zablokowane."});
  await signIn.SignInAsync(u!,false);return Ok(new {message="Zalogowano."});
 }
 private static readonly Lazy<string> DummyHash=new(()=>new PasswordHasher<User>().HashPassword(new User(),Guid.NewGuid().ToString()));
 [HttpPost("logout"),Authorize] public async Task<IActionResult> Logout() {var u=await users.GetUserAsync(User);if(u is not null){var invalidated=await users.UpdateSecurityStampAsync(u);if(!invalidated.Succeeded)throw new InvalidOperationException("Session invalidation failed");}await audit.Write(User.FindFirstValue(ClaimTypes.NameIdentifier),"LOGOUT","Wylogowanie i unieważnienie sesji użytkownika.");await signIn.SignOutAsync();return Ok(new {message="Wylogowano."});}
 [HttpGet("me"),Authorize] public async Task<IActionResult> Me() {var u=(await users.GetUserAsync(User))!;return Ok(new {u.Id,u.Email,u.DisplayName,roles=await users.GetRolesAsync(u)});}
 [HttpPost("password"),Authorize] public async Task<IActionResult> Password(PasswordRequest r) {var u=(await users.GetUserAsync(User))!;var result=await users.ChangePasswordAsync(u,r.CurrentPassword,r.NewPassword);if(!result.Succeeded)return BadRequest(new {message=string.Join(" ",result.Errors.Select(x=>x.Description))});await audit.Write(u.Id,"PASSWORD_CHANGED","Zmieniono hasło.");await signIn.SignOutAsync();return Ok(new {message="Hasło zmienione. Zaloguj się ponownie."});}
 [HttpPost("reset-request"),AllowAnonymous] public async Task<IActionResult> Reset(ResetRequest r) {
  var u=await users.FindByEmailAsync(r.Email);string? token=null;
  if(u is not null&&env.IsDevelopment()&&config.GetValue<bool>("Demo:Enabled"))token=await users.GeneratePasswordResetTokenAsync(u);
  return Ok(new {message="Jeśli konto istnieje, przygotowano reset.",demoToken=token});
 }
 [HttpPost("reset-confirm"),AllowAnonymous] public async Task<IActionResult> ResetConfirm(ResetConfirmRequest r) {
  if(!env.IsDevelopment()||!config.GetValue<bool>("Demo:Enabled"))return NotFound();
  var u=await users.FindByEmailAsync(r.Email);if(u is null)return BadRequest(new {message="Niepoprawny reset."});
  var result=await users.ResetPasswordAsync(u,r.Token,r.Password);if(!result.Succeeded)return BadRequest(new {message="Niepoprawny token lub za słabe hasło."});
  await audit.Write(u.Id,"PASSWORD_CHANGED","Reset hasła w trybie laboratoryjnym.");return Ok(new {message="Hasło zresetowane."});
 }
}
