namespace TakeTime.CRM.Application.DTOs;

public class CustomerDto
{
    public Guid Id { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}".Trim();
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? LoyaltyTier { get; set; }
    public int LoyaltyPoints { get; set; }
    public decimal TotalSpent { get; set; }
    public string? Source { get; set; }
    public DateTime? FirstVisitDate { get; set; }
    public DateTime? LastVisitDate { get; set; }
    public int TotalVisits { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public List<string> Tags { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}

public class CustomerSummaryDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? LoyaltyTier { get; set; }
    public int TotalVisits { get; set; }
    public decimal TotalSpent { get; set; }
}

public class CreateCustomerDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Source { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public List<string>? Tags { get; set; }
}

public class LoyaltyDto
{
    public Guid CustomerId { get; set; }
    public string Tier { get; set; } = string.Empty;
    public int Points { get; set; }
    public int PointsToNextTier { get; set; }
    public decimal DiscountPercentage { get; set; }
    public List<LoyaltyTransactionDto> RecentTransactions { get; set; } = [];
}

public class LoyaltyTransactionDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public int Points { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ReviewDto
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public Guid? ReservationId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string? Response { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CustomerAnalyticsDto
{
    public int TotalCustomers { get; set; }
    public int NewCustomersThisMonth { get; set; }
    public decimal AverageSpend { get; set; }
    public Dictionary<string, int> TierDistribution { get; set; } = new();
    public List<CustomerSummaryDto> TopCustomers { get; set; } = [];
    public double AverageRating { get; set; }
}
