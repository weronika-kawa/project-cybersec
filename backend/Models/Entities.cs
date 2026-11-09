using Microsoft.AspNetCore.Identity;
namespace SecureBank.Models;
public class User : IdentityUser { public bool IsBlocked { get; set; } public string DisplayName { get; set; } = ""; }
public class Account {
 public Guid Id { get; set; } = Guid.NewGuid(); public string UserId { get; set; } = "";
 public User User { get; set; } = null!; public string AccountNumber { get; set; } = "";
 public decimal Balance { get; set; } public string Currency { get; set; } = "PLN";
 public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; public bool IsActive { get; set; } = true;
}
public class Transaction {
 public Guid Id { get; set; } = Guid.NewGuid(); public Guid FromAccountId { get; set; } public Guid ToAccountId { get; set; }
 public Account FromAccount { get; set; } = null!; public Account ToAccount { get; set; } = null!;
 public decimal Amount { get; set; } public string Title { get; set; } = ""; public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
 public string Status { get; set; } = "COMPLETED";
}
public class SecurityLog {
 public long Id { get; set; } public string? UserId { get; set; } public string EventType { get; set; } = "";
 public string IpAddress { get; set; } = ""; public string Endpoint { get; set; } = ""; public string Description { get; set; } = "";
 public string Severity { get; set; } = "INFO"; public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
public class SecurityAlert {
 public long Id { get; set; } public string? UserId { get; set; } public string Type { get; set; } = "";
 public string Severity { get; set; } = "WARNING"; public string Description { get; set; } = "";
 public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; public bool Resolved { get; set; } public DateTimeOffset? ResolvedAt { get; set; }
}
public class LoginAttempt { public long Id { get; set; } public string? UserId { get; set; } public bool Success { get; set; } public string IpAddress { get; set; } = ""; public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow; }
public class OtpChallenge {
 public Guid Id { get; set; } = Guid.NewGuid(); public string UserId { get; set; } = "";
 public Guid FromAccountId { get; set; } public Guid ToAccountId { get; set; } public decimal Amount { get; set; } public string Title { get; set; } = "";
 public string CodeHash { get; set; } = ""; public int Attempts { get; set; } public bool Used { get; set; }
 public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddMinutes(3);
}
