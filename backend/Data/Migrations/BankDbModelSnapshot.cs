using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace SecureBank.Data.Migrations;
// Frozen schema metadata for future EF migrations; do not reuse the live model here.
[DbContext(typeof(BankDb))]
public class BankDbModelSnapshot : ModelSnapshot {
 protected override void BuildModel(ModelBuilder b) {
  b.HasAnnotation("ProductVersion","8.0.31").HasAnnotation("Relational:MaxIdentifierLength",63);
  b.UseIdentityByDefaultColumns();
  b.Entity("SecureBank.Models.User",e=>{
   Text(e,"Id");foreach(var p in new[]{"UserName","NormalizedUserName","Email","NormalizedEmail"})e.Property<string>(p).HasMaxLength(256).HasColumnType("character varying(256)");
   foreach(var p in new[]{"PasswordHash","SecurityStamp","PhoneNumber"})Text(e,p,false);
   Text(e,"ConcurrencyStamp",false);e.Property<string>("ConcurrencyStamp").IsConcurrencyToken();Text(e,"DisplayName");
   foreach(var p in new[]{"EmailConfirmed","PhoneNumberConfirmed","TwoFactorEnabled","LockoutEnabled","IsBlocked"})e.Property<bool>(p).HasColumnType("boolean");
   e.Property<int>("AccessFailedCount").HasColumnType("integer");e.Property<DateTimeOffset?>("LockoutEnd").HasColumnType("timestamp with time zone");
   e.HasKey("Id");e.HasIndex("NormalizedEmail").HasDatabaseName("EmailIndex");e.HasIndex("NormalizedUserName").IsUnique().HasDatabaseName("UserNameIndex");e.ToTable("AspNetUsers");
  });
  b.Entity("Microsoft.AspNetCore.Identity.IdentityRole",e=>{
   Text(e,"Id");e.Property<string>("Name").HasMaxLength(256).HasColumnType("character varying(256)");e.Property<string>("NormalizedName").HasMaxLength(256).HasColumnType("character varying(256)");Text(e,"ConcurrencyStamp",false);e.Property<string>("ConcurrencyStamp").IsConcurrencyToken();e.HasKey("Id");e.HasIndex("NormalizedName").IsUnique().HasDatabaseName("RoleNameIndex");e.ToTable("AspNetRoles");
  });
  foreach(var (name,table,fk,target) in new[]{("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>","AspNetUserClaims","UserId","SecureBank.Models.User"),("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>","AspNetRoleClaims","RoleId","Microsoft.AspNetCore.Identity.IdentityRole")}) {
   b.Entity(name,e=>{e.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("integer").UseIdentityByDefaultColumn();Text(e,fk);Text(e,"ClaimType",false);Text(e,"ClaimValue",false);e.HasKey("Id");e.HasIndex(fk);e.ToTable(table);e.HasOne(target,null).WithMany().HasForeignKey(fk).OnDelete(DeleteBehavior.Cascade).IsRequired();});
  }
  b.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<string>",e=>{foreach(var p in new[]{"LoginProvider","ProviderKey","UserId"})Text(e,p);Text(e,"ProviderDisplayName",false);e.HasKey("LoginProvider","ProviderKey");e.HasIndex("UserId");e.ToTable("AspNetUserLogins");e.HasOne("SecureBank.Models.User",null).WithMany().HasForeignKey("UserId").OnDelete(DeleteBehavior.Cascade).IsRequired();});
  b.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>",e=>{Text(e,"UserId");Text(e,"RoleId");e.HasKey("UserId","RoleId");e.HasIndex("RoleId");e.ToTable("AspNetUserRoles");e.HasOne("SecureBank.Models.User",null).WithMany().HasForeignKey("UserId").OnDelete(DeleteBehavior.Cascade).IsRequired();e.HasOne("Microsoft.AspNetCore.Identity.IdentityRole",null).WithMany().HasForeignKey("RoleId").OnDelete(DeleteBehavior.Cascade).IsRequired();});
  b.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<string>",e=>{foreach(var p in new[]{"UserId","LoginProvider","Name"})Text(e,p);Text(e,"Value",false);e.HasKey("UserId","LoginProvider","Name");e.ToTable("AspNetUserTokens");e.HasOne("SecureBank.Models.User",null).WithMany().HasForeignKey("UserId").OnDelete(DeleteBehavior.Cascade).IsRequired();});
  b.Entity("SecureBank.Models.Account",e=>{GuidKey(e);Text(e,"UserId");Text(e,"AccountNumber");Text(e,"Currency");Decimal(e,"Balance");Time(e,"CreatedAt");e.Property<bool>("IsActive");e.HasIndex("AccountNumber").IsUnique();e.HasIndex("UserId");e.ToTable("Accounts",t=>t.HasCheckConstraint("CK_balance","\"Balance\" >= 0"));e.HasOne("SecureBank.Models.User","User").WithMany().HasForeignKey("UserId").OnDelete(DeleteBehavior.Restrict).IsRequired();});
  b.Entity("SecureBank.Models.Transaction",e=>{GuidKey(e);e.Property<Guid>("FromAccountId");e.Property<Guid>("ToAccountId");Decimal(e,"Amount");Text(e,"Title");Text(e,"Status");Time(e,"Timestamp");e.HasIndex("FromAccountId","Timestamp");e.HasIndex("ToAccountId","Timestamp");e.ToTable("Transactions",t=>t.HasCheckConstraint("CK_amount","\"Amount\" > 0 AND \"FromAccountId\" <> \"ToAccountId\""));e.HasOne("SecureBank.Models.Account","FromAccount").WithMany().HasForeignKey("FromAccountId").OnDelete(DeleteBehavior.Restrict).IsRequired();e.HasOne("SecureBank.Models.Account","ToAccount").WithMany().HasForeignKey("ToAccountId").OnDelete(DeleteBehavior.Restrict).IsRequired();});
  b.Entity("SecureBank.Models.SecurityLog",e=>{LongKey(e);Text(e,"UserId",false);foreach(var p in new[]{"EventType","Description","IpAddress","Endpoint","Severity"})Text(e,p);Time(e,"Timestamp");e.HasIndex("UserId","EventType","Timestamp");e.ToTable("SecurityLogs");});
  b.Entity("SecureBank.Models.SecurityAlert",e=>{LongKey(e);Text(e,"UserId",false);foreach(var p in new[]{"Type","Description","Severity"})Text(e,p);Time(e,"CreatedAt");e.Property<bool>("Resolved");e.Property<DateTimeOffset?>("ResolvedAt").HasColumnType("timestamp with time zone");e.HasIndex("Resolved","CreatedAt");e.ToTable("SecurityAlerts");});
  b.Entity("SecureBank.Models.LoginAttempt",e=>{LongKey(e);Text(e,"UserId",false);Text(e,"IpAddress");e.Property<bool>("Success");Time(e,"Timestamp");e.HasIndex("UserId","Timestamp");e.ToTable("LoginAttempts");});
  b.Entity("SecureBank.Models.OtpChallenge",e=>{GuidKey(e);Text(e,"UserId");Text(e,"Title");Text(e,"CodeHash");e.Property<Guid>("FromAccountId");e.Property<Guid>("ToAccountId");Decimal(e,"Amount");e.Property<int>("Attempts");e.Property<bool>("Used");Time(e,"ExpiresAt");e.HasIndex("UserId");e.HasIndex("FromAccountId");e.HasIndex("ToAccountId");e.ToTable("OtpChallenges",t=>t.HasCheckConstraint("CK_otp_amount","\"Amount\" > 0"));e.HasOne("SecureBank.Models.User",null).WithMany().HasForeignKey("UserId").OnDelete(DeleteBehavior.Restrict).IsRequired();e.HasOne("SecureBank.Models.Account",null).WithMany().HasForeignKey("FromAccountId").OnDelete(DeleteBehavior.Restrict).IsRequired();e.HasOne("SecureBank.Models.Account",null).WithMany().HasForeignKey("ToAccountId").OnDelete(DeleteBehavior.Restrict).IsRequired();});
 }
 static void Text(EntityTypeBuilder e,string name,bool required=true)=>e.Property<string>(name).IsRequired(required).HasColumnType("text");
 static void Time(EntityTypeBuilder e,string name)=>e.Property<DateTimeOffset>(name).HasColumnType("timestamp with time zone");
 static void Decimal(EntityTypeBuilder e,string name)=>e.Property<decimal>(name).HasPrecision(18,2).HasColumnType("numeric(18,2)");
 static void GuidKey(EntityTypeBuilder e){e.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");e.HasKey("Id");}
 static void LongKey(EntityTypeBuilder e){e.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint").UseIdentityByDefaultColumn();e.HasKey("Id");}
}
