using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecureBank.Data;
using SecureBank.Models;
using SecureBank.Services;
var builder=WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<BankDb>(o=>o.UseNpgsql(builder.Configuration.GetConnectionString("Bank")??throw new InvalidOperationException("Configure ConnectionStrings:Bank")));
builder.Services.AddIdentity<User,IdentityRole>(o=>{
 o.Password.RequiredLength=12;o.Password.RequireDigit=true;o.Password.RequireLowercase=true;o.Password.RequireUppercase=true;o.Password.RequireNonAlphanumeric=true;
 o.User.RequireUniqueEmail=true;o.Lockout.MaxFailedAccessAttempts=5;o.Lockout.DefaultLockoutTimeSpan=TimeSpan.FromMinutes(15);
}).AddEntityFrameworkStores<BankDb>().AddDefaultTokenProviders();
builder.Services.Configure<DataProtectionTokenProviderOptions>(o=>o.TokenLifespan=TimeSpan.FromMinutes(10));
builder.Services.ConfigureApplicationCookie(o=>{
 o.Cookie.Name="SecureBank.Session";o.Cookie.HttpOnly=true;o.Cookie.SameSite=SameSiteMode.Strict;
 o.Cookie.SecurePolicy=builder.Environment.IsDevelopment()?CookieSecurePolicy.SameAsRequest:CookieSecurePolicy.Always;
 o.ExpireTimeSpan=TimeSpan.FromMinutes(20);o.SlidingExpiration=false;
 o.Events.OnRedirectToLogin=c=>{c.Response.StatusCode=401;return Task.CompletedTask;};
 o.Events.OnRedirectToAccessDenied=c=>{c.Response.StatusCode=403;return Task.CompletedTask;};
 o.Events.OnValidatePrincipal=async c=>{
  var manager=c.HttpContext.RequestServices.GetRequiredService<UserManager<User>>();var u=await manager.GetUserAsync(c.Principal!);
  var stamp=c.Principal?.FindFirstValue("AspNet.Identity.SecurityStamp");
  if(u is null||u.IsBlocked||(u.LockoutEnd>DateTimeOffset.UtcNow)||u.SecurityStamp!=stamp){c.RejectPrincipal();await c.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);}
 };
});
builder.Services.AddAuthorization(o=>o.FallbackPolicy=new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddAntiforgery(o=>{o.HeaderName="X-CSRF-TOKEN";o.Cookie.SameSite=SameSiteMode.Strict;o.Cookie.HttpOnly=true;o.Cookie.SecurePolicy=builder.Environment.IsDevelopment()?CookieSecurePolicy.SameAsRequest:CookieSecurePolicy.Always;});
builder.Services.AddControllers().AddJsonOptions(o=>o.JsonSerializerOptions.UnmappedMemberHandling=System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow);
builder.Services.AddEndpointsApiExplorer();builder.Services.AddSwaggerGen();builder.Services.AddHttpContextAccessor();builder.Services.AddScoped<Audit>();builder.Services.AddScoped<TransferService>();
if(Directory.Exists("/keys"))builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo("/keys")).SetApplicationName("SecureBank");
builder.Services.AddRateLimiter(o=>{
 o.RejectionStatusCode=429;
 o.GlobalLimiter=PartitionedRateLimiter.Create<HttpContext,string>(c=>RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions {PermitLimit=120,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
 o.AddPolicy("auth",c=>RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions {PermitLimit=20,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
});
var app=builder.Build();
app.UseExceptionHandler(error=>error.Run(async c=>{
 if(c.Request.Path.StartsWithSegments("/api/transactions")&&HttpMethods.IsPost(c.Request.Method)) {
  // A separate context records the failure after the financial transaction rolls back.
  try{using var scope=c.RequestServices.CreateScope();await scope.ServiceProvider.GetRequiredService<Audit>().Write(c.User.FindFirstValue(ClaimTypes.NameIdentifier),"TRANSFER_FAILED","Operacja przerwana błędem serwera; brak potwierdzenia commit.","CRITICAL");}
  catch(Exception e){app.Logger.LogError(e,"Failed to persist transfer failure audit");}
 }
 c.Response.StatusCode=500;await c.Response.WriteAsJsonAsync(new {message="Błąd serwera. Operacja nie została potwierdzona."});
}));
app.Use(async(c,next)=>{c.Response.Headers["X-Content-Type-Options"]="nosniff";c.Response.Headers["Referrer-Policy"]="no-referrer";c.Response.Headers["Cache-Control"]="no-store";await next();});
app.UseRouting();app.UseRateLimiter();app.UseAuthentication();
app.Use(async(c,next)=>{
 if(c.Request.Path.StartsWithSegments("/api")&&!HttpMethods.IsGet(c.Request.Method)&&!HttpMethods.IsHead(c.Request.Method)) {
  try{await c.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(c);}catch(AntiforgeryValidationException){c.Response.StatusCode=400;await c.Response.WriteAsJsonAsync(new {message="Niepoprawny CSRF. Odśwież stronę."});return;}
 }
 await next();
 if(c.Response.StatusCode is 401 or 403)await c.RequestServices.GetRequiredService<Audit>().Denied(c.User.FindFirstValue(ClaimTypes.NameIdentifier),"Odmowa dostępu do endpointu.");
});
app.UseAuthorization();
if(app.Environment.IsDevelopment()){app.UseSwagger();app.UseSwaggerUI();}
app.MapControllers();
using(var scope=app.Services.CreateScope())await Seed.Run(scope.ServiceProvider);
app.Run();
public partial class Program { }
