using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SecureBank.Data;
using SecureBank.Models;
using SecureBank.Services;
using Xunit;
namespace SecureBank.Tests;
public class Factory : WebApplicationFactory<Program> {
 static string TestConnection() {
  var connection=Environment.GetEnvironmentVariable("TEST_DB")??throw new InvalidOperationException("Set TEST_DB to a dedicated PostgreSQL test database");
  if(!new Npgsql.NpgsqlConnectionStringBuilder(connection).Database.EndsWith("_tests",StringComparison.Ordinal))throw new InvalidOperationException("Safety guard: TEST_DB database name must end in _tests");
  return connection;
 }
 protected override void ConfigureWebHost(IWebHostBuilder b)=>b.UseEnvironment("Development").ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?> {
  ["ConnectionStrings:Bank"]=TestConnection(),["Demo:Enabled"]="true",["Demo:Password"]="SecureBank!Demo2026"
 }));
}
public class SecurityTests {
 const string Password="SecureBank!Demo2026";
 async Task<(HttpClient client,string uid)> Client(Factory f,bool login=true){
  var c=f.CreateClient(new WebApplicationFactoryClientOptions {HandleCookies=true});
  async Task Csrf(){var j=await c.GetFromJsonAsync<JsonElement>("/api/auth/csrf");c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");c.DefaultRequestHeaders.Add("X-CSRF-TOKEN",j.GetProperty("token").GetString());}
  await Csrf();var email=$"test-{Guid.NewGuid():N}@securebank.test";
  var reg=await c.PostAsJsonAsync("/api/auth/register",new {email,password=Password,displayName="Test"});reg.EnsureSuccessStatusCode();
  if(login){var r=await c.PostAsJsonAsync("/api/auth/login",new {email,password=Password});r.EnsureSuccessStatusCode();await Csrf();}
  using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<BankDb>();var u=await db.Users.SingleAsync(x=>x.Email==email);var a=await db.Accounts.SingleAsync(x=>x.UserId==u.Id);a.Balance=20000;await db.SaveChangesAsync();return(c,u.Id);
 }
 async Task<Account> Account(Factory f,string uid){using var s=f.Services.CreateScope();return await s.ServiceProvider.GetRequiredService<BankDb>().Accounts.AsNoTracking().SingleAsync(x=>x.UserId==uid);}
 object Request(Account a,Account b,decimal amount)=>new {fromAccountId=a.Id,toAccountNumber=b.AccountNumber,amount,title="Test"};
 [Fact] public async Task T01_FiveFailuresLockAccount(){using var f=new Factory();var(c,id)=await Client(f,false);using var s=f.Services.CreateScope();var db=s.ServiceProvider.GetRequiredService<BankDb>();var u=await db.Users.AsNoTracking().SingleAsync(x=>x.Id==id);for(int i=0;i<5;i++)Assert.Equal(HttpStatusCode.Unauthorized,(await c.PostAsJsonAsync("/api/auth/login",new {email=u.Email,password="WrongPassword!123"})).StatusCode);Assert.Equal(HttpStatusCode.Unauthorized,(await c.PostAsJsonAsync("/api/auth/login",new {email=u.Email,password=Password})).StatusCode);Assert.True(await db.SecurityAlerts.AnyAsync(x=>x.UserId==id&&x.Type=="ACCOUNT_LOCKED"));}
 [Fact] public async Task T02_ClientAdminIs403(){using var f=new Factory();var(c,_)=await Client(f);Assert.Equal(HttpStatusCode.Forbidden,(await c.GetAsync("/api/admin/users")).StatusCode);}
 [Fact] public async Task T03_OwnershipRead(){using var f=new Factory();var(c,_)=await Client(f);var(_,id)=await Client(f);var a=await Account(f,id);Assert.Equal(HttpStatusCode.NotFound,(await c.GetAsync("/api/accounts/"+a.Id)).StatusCode);}
 [Fact] public async Task T04_ForeignSourceBlocked(){using var f=new Factory();var(c,id)=await Client(f);var(_,other)=await Client(f);var a=await Account(f,id);var b=await Account(f,other);Assert.Equal(HttpStatusCode.NotFound,(await c.PostAsJsonAsync("/api/transactions",Request(b,a,100))).StatusCode);Assert.Equal(b.Balance,(await Account(f,other)).Balance);}
 [Fact] public async Task T05_InsufficientBalance(){using var f=new Factory();var(c,id)=await Client(f);var(_,other)=await Client(f);Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync("/api/transactions",Request(await Account(f,id),await Account(f,other),30000))).StatusCode);}
 [Fact] public async Task T06_RiskOtpAndReplay(){using var f=new Factory();var(c,id)=await Client(f);var(_,other)=await Client(f);var a=await Account(f,id);var b=await Account(f,other);var r=await c.PostAsJsonAsync("/api/transactions",Request(a,b,5000));Assert.Equal(HttpStatusCode.Accepted,r.StatusCode);Assert.Equal(a.Balance,(await Account(f,id)).Balance);var j=await r.Content.ReadFromJsonAsync<JsonElement>();var path="/api/transactions/"+j.GetProperty("challengeId").GetString()+"/verify";var otp=new {code=j.GetProperty("demoCode").GetString()};Assert.Equal(HttpStatusCode.OK,(await c.PostAsJsonAsync(path,otp)).StatusCode);Assert.Equal(a.Balance-5000,(await Account(f,id)).Balance);Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync(path,otp)).StatusCode);}
 [Fact] public async Task T07_RepeatedAccessAlertsAndBlocks(){using var f=new Factory();var(c,id)=await Client(f);for(int i=0;i<3;i++)await c.GetAsync("/api/accounts/"+Guid.NewGuid());using var s=f.Services.CreateScope();var db=s.ServiceProvider.GetRequiredService<BankDb>();Assert.True(await db.SecurityAlerts.AnyAsync(x=>x.UserId==id));Assert.True((await db.Users.FindAsync(id))!.IsBlocked);Assert.Equal(HttpStatusCode.Unauthorized,(await c.GetAsync("/api/accounts")).StatusCode);}
 [Fact] public async Task T08_DeniedIsLogged(){using var f=new Factory();var(c,id)=await Client(f);await c.GetAsync("/api/admin/logs");using var s=f.Services.CreateScope();Assert.True(await s.ServiceProvider.GetRequiredService<BankDb>().SecurityLogs.AnyAsync(x=>x.UserId==id&&x.EventType=="UNAUTHORIZED_ACCESS"));}
 [Fact] public async Task T09_RoleCannotBeAssignedByClient(){using var f=new Factory();var(c,id)=await Client(f);Assert.Equal(HttpStatusCode.Forbidden,(await c.PutAsJsonAsync("/api/admin/users/"+id+"/role",new {role="ADMIN"})).StatusCode);var email=$"mass-{Guid.NewGuid():N}@securebank.test";Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync("/api/auth/register",new {email,password=Password,displayName="Test",role="ADMIN",balance=999999})).StatusCode);}
 [Fact] public async Task T10_AtomicTransferAndDatabaseFailureRollback(){using var f=new Factory();var(c,id)=await Client(f);var(_,other)=await Client(f);var a=await Account(f,id);var b=await Account(f,other);Assert.Equal(HttpStatusCode.OK,(await c.PostAsJsonAsync("/api/transactions",Request(a,b,100))).StatusCode);Assert.Equal(a.Balance-100,(await Account(f,id)).Balance);Assert.Equal(b.Balance+100,(await Account(f,other)).Balance);
  // Real PostgreSQL trigger fails the credit AFTER the debit, proving rollback.
  using var s=f.Services.CreateScope();var db=s.ServiceProvider.GetRequiredService<BankDb>();
  await db.Database.ExecuteSqlRawAsync("CREATE OR REPLACE FUNCTION test_fail_credit() RETURNS trigger AS $$ BEGIN IF NEW.\"Balance\">OLD.\"Balance\" THEN RAISE EXCEPTION 'test credit failure'; END IF; RETURN NEW; END; $$ LANGUAGE plpgsql;");
  await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER test_credit_failure BEFORE UPDATE ON \"Accounts\" FOR EACH ROW EXECUTE FUNCTION test_fail_credit();");
  try {Assert.Equal(HttpStatusCode.InternalServerError,(await c.PostAsJsonAsync("/api/transactions",Request(a,b,100))).StatusCode);Assert.Equal(a.Balance-100,(await Account(f,id)).Balance);Assert.Equal(b.Balance+100,(await Account(f,other)).Balance);}finally{await db.Database.ExecuteSqlRawAsync("DROP TRIGGER test_credit_failure ON \"Accounts\"; DROP FUNCTION test_fail_credit();");}
 }
 [Fact] public async Task CsrfIsRequired(){using var f=new Factory();var c=f.CreateClient();Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync("/api/auth/login",new {email="anna@securebank.test",password=Password})).StatusCode);}
 [Fact] public async Task ConcurrentTransfersCannotOverspend(){using var f=new Factory();var(c,id)=await Client(f);var(_,other)=await Client(f);var a=await Account(f,id);var b=await Account(f,other);
  using(var s=f.Services.CreateScope()){var db=s.ServiceProvider.GetRequiredService<BankDb>();var tracked=await db.Accounts.FindAsync(a.Id);tracked!.Balance=150;await db.SaveChangesAsync();}
  var results=await Task.WhenAll(c.PostAsJsonAsync("/api/transactions",Request(a,b,100)),c.PostAsJsonAsync("/api/transactions",Request(a,b,100)));
  Assert.Single(results.Where(x=>x.StatusCode==HttpStatusCode.OK));Assert.Single(results.Where(x=>x.StatusCode==HttpStatusCode.BadRequest));Assert.Equal(50,(await Account(f,id)).Balance);Assert.Equal(b.Balance+100,(await Account(f,other)).Balance);
 }
 [Fact] public async Task ThreeBadOtpAttemptsBlockAndAlert(){using var f=new Factory();var(c,id)=await Client(f);var(_,other)=await Client(f);var r=await c.PostAsJsonAsync("/api/transactions",Request(await Account(f,id),await Account(f,other),5000));var j=await r.Content.ReadFromJsonAsync<JsonElement>();var path="/api/transactions/"+j.GetProperty("challengeId").GetString()+"/verify";
  for(int i=0;i<3;i++)Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync(path,new {code="000000"})).StatusCode);
  Assert.Equal(HttpStatusCode.Unauthorized,(await c.GetAsync("/api/accounts")).StatusCode);using var s=f.Services.CreateScope();Assert.True(await s.ServiceProvider.GetRequiredService<BankDb>().SecurityAlerts.AnyAsync(x=>x.UserId==id&&x.Type=="OTP_FAILED"));
 }
 [Theory][InlineData("0",false)][InlineData("-1",false)][InlineData("0.001",false)][InlineData("0.01",true)][InlineData("5000",true)] public void AmountValidation(string value,bool valid)=>Assert.Equal(valid,TransferService.ValidAmount(decimal.Parse(value,System.Globalization.CultureInfo.InvariantCulture)));
}
