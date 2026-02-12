namespace TakeTime.Accounting.Application.DTOs;

/// <summary>
/// Analytics overview DTO for the dashboard, including occupancy,
/// revenue, and booking summary metrics.
/// </summary>
public sealed class AnalyticsOverviewDto
{
    public DateTime AsOfDate { get; set; }
    public string TenantId { get; set; } = string.Empty;

    // Occupancy
    public int TotalRooms { get; set; }
    public int OccupiedRooms { get; set; }
    public decimal OccupancyPercent { get; set; }

    // Revenue
    public decimal TodayRevenue { get; set; }
    public decimal WeekRevenue { get; set; }
    public decimal MonthRevenue { get; set; }

    // Bookings
    public int TodayCheckIns { get; set; }
    public int TodayCheckOuts { get; set; }
    public int PendingReservations { get; set; }
    public int ConfirmedReservations { get; set; }

    // Averages
    public decimal AverageDailyRate { get; set; }
    public decimal RevPAR { get; set; }

    public string Currency { get; set; } = "THB";
}

/// <summary>
/// Revenue and occupancy trends over time periods.
/// </summary>
public sealed class TrendsDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Period { get; set; } = "daily";
    public List<TrendDataPointDto> RevenueData { get; set; } = [];
    public List<TrendDataPointDto> OccupancyData { get; set; } = [];
    public string Currency { get; set; } = "THB";
}

/// <summary>
/// A single data point in a trend series.
/// </summary>
public sealed class TrendDataPointDto
{
    public DateTime Date { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

/// <summary>
/// Guest demographics breakdown by source/origin.
/// </summary>
public sealed class GuestDemographicsDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public int TotalGuests { get; set; }
    public List<DemographicSegmentDto> BySource { get; set; } = [];
    public List<DemographicSegmentDto> ByNationality { get; set; } = [];
    public List<DemographicSegmentDto> ByStayDuration { get; set; } = [];
}

/// <summary>
/// A single segment in a demographics breakdown.
/// </summary>
public sealed class DemographicSegmentDto
{
    public string Segment { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

/// <summary>
/// Channel performance breakdown showing revenue by booking channel.
/// </summary>
public sealed class ChannelPerformanceDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public decimal TotalRevenue { get; set; }
    public int TotalBookings { get; set; }
    public List<ChannelDataDto> Channels { get; set; } = [];
    public string Currency { get; set; } = "THB";
}

/// <summary>
/// Revenue and booking data for a single booking channel.
/// </summary>
public sealed class ChannelDataDto
{
    public string ChannelName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int BookingCount { get; set; }
    public decimal RevenuePercentage { get; set; }
    public decimal BookingPercentage { get; set; }
    public decimal AverageBookingValue { get; set; }
    public string Currency { get; set; } = "THB";
}
