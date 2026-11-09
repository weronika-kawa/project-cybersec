using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureBank.Data;
using SecureBank.DTOs;
using SecureBank.Services;
namespace SecureBank.Controllers;
[ApiController,Route("api"),Authorize(Roles="CLIENT")]
public class BankController(BankDb db,Audit audit,TransferService transfers) : ControllerBase {
 string Uid=>User.FindFirstValue(ClaimTypes.NameIdentifier)!;
 [HttpGet("accounts")] public async Task<IActionResult> Accounts()=>Ok(await db.Accounts.Where(x=>x.UserId==Uid).Select(x=>new {x.Id,x.AccountNumber,x.Balance,x.Currency,x.CreatedAt,x.IsActive}).ToListAsync());
 [HttpGet("accounts/{id:guid}")] public async Task<IActionResult> Account(Guid id) {
  var a=await db.Accounts.Where(x=>x.Id==id&&x.UserId==Uid).Select(x=>new {x.Id,x.AccountNumber,x.Balance,x.Currency,x.CreatedAt,x.IsActive}).SingleOrDefaultAsync();
  if(a is null){await audit.Denied(Uid,"Próba odczytu niedostępnego rachunku.");return NotFound(new {message="Nie znaleziono rachunku."});}return Ok(a);
 }
 [HttpGet("transactions")] public async Task<IActionResult> History([FromQuery] Guid? accountId) {
  if(accountId.HasValue&&!await db.Accounts.AnyAsync(x=>x.Id==accountId&&x.UserId==Uid)){await audit.Denied(Uid,"Próba odczytu cudzej historii.");return NotFound();}
  var query=db.Transactions.Where(x=>x.FromAccount.UserId==Uid||x.ToAccount.UserId==Uid);
  if(accountId.HasValue)query=query.Where(x=>x.FromAccountId==accountId||x.ToAccountId==accountId);
  return Ok(await query.OrderByDescending(x=>x.Timestamp).Take(200).Select(x=>new {x.Id,x.FromAccountId,x.ToAccountId,from=x.FromAccount.AccountNumber,to=x.ToAccount.AccountNumber,x.Amount,x.Title,x.Timestamp,x.Status}).ToListAsync());
 }
 [HttpPost("transactions")] public async Task<IActionResult> Transfer(TransferRequest r){var result=await transfers.Start(Uid,r);return StatusCode(result.Status,result.Body);}
 [HttpPost("transactions/{id:guid}/verify")] public async Task<IActionResult> Verify(Guid id,OtpRequest r){var result=await transfers.Confirm(Uid,id,r.Code);return StatusCode(result.Status,result.Body);}
}
