using System;
using System.Configuration;

/// <summary>
/// HR System Configuration
/// Centralizes all configurable values for HR and Payroll systems
/// Values can be overridden in Web.config AppSettings
/// </summary>
public static class HRConfiguration
{
    #region Social Security Settings

    /// <summary>
    /// Social Security contribution rate (5%)
    /// </summary>
    public static decimal SocialSecurityRate
    {
        get
        {
            string configValue = ConfigurationManager.AppSettings["SocialSecurityRate"];
            return decimal.TryParse(configValue, out decimal result) ? result : 0.05m;
        }
    }

    /// <summary>
    /// Minimum salary base for Social Security calculation (1,650 THB)
    /// Employees earning less than this don't pay SS
    /// </summary>
    public static decimal SocialSecurityMinBase
    {
        get
        {
            string configValue = ConfigurationManager.AppSettings["SocialSecurityMinBase"];
            return decimal.TryParse(configValue, out decimal result) ? result : 1650m;
        }
    }

    /// <summary>
    /// Maximum salary base for Social Security calculation
    /// Uses stepped ceiling based on Thai Buddhist year:
    /// - Before 2569: 15,000 THB
    /// - 2569-2571: 17,500 THB
    /// - 2572-2574: 20,000 THB
    /// - 2575+: 23,000 THB
    /// </summary>
    public static decimal SocialSecurityMaxBase
    {
        get
        {
            // Allow override from config
            string configValue = ConfigurationManager.AppSettings["SocialSecurityMaxBase"];
            if (decimal.TryParse(configValue, out decimal result))
                return result;

            // Calculate based on Thai Buddhist year
            return GetSocialSecurityMaxBaseByYear(DateTime.Now.Year + 543);
        }
    }

    /// <summary>
    /// Get Social Security max base for a specific Thai Buddhist year
    /// Reference: ร่างกฎกระทรวง ปรับเพดานค่าจ้างแบบขั้นบันได
    /// </summary>
    public static decimal GetSocialSecurityMaxBaseByYear(int thaiBuddhistYear)
    {
        // ระยะที่ 1: ปี พ.ศ. 2569-2571 เพดาน 17,500 บาท
        // ระยะที่ 2: ปี พ.ศ. 2572-2574 เพดาน 20,000 บาท
        // ระยะที่ 3: ปี พ.ศ. 2575+ เพดาน 23,000 บาท
        if (thaiBuddhistYear >= 2575)
            return 23000m;
        else if (thaiBuddhistYear >= 2572)
            return 20000m;
        else if (thaiBuddhistYear >= 2569)
            return 17500m;
        else
            return 15000m; // Before 2569
    }

    /// <summary>
    /// Maximum Social Security deduction
    /// Uses stepped ceiling based on Thai Buddhist year:
    /// - Before 2569: 750 THB
    /// - 2569-2571: 875 THB
    /// - 2572-2574: 1,000 THB
    /// - 2575+: 1,150 THB
    /// </summary>
    public static decimal SocialSecurityMaxDeduction
    {
        get
        {
            // Allow override from config
            string configValue = ConfigurationManager.AppSettings["SocialSecurityMaxDeduction"];
            if (decimal.TryParse(configValue, out decimal result))
                return result;

            // Calculate based on Thai Buddhist year
            return GetSocialSecurityMaxDeductionByYear(DateTime.Now.Year + 543);
        }
    }

    /// <summary>
    /// Get Social Security max deduction for a specific Thai Buddhist year
    /// (MaxBase * 5% = MaxDeduction)
    /// </summary>
    public static decimal GetSocialSecurityMaxDeductionByYear(int thaiBuddhistYear)
    {
        // คำนวณจาก MaxBase * 5%
        // ระยะที่ 1: 17,500 * 5% = 875 บาท
        // ระยะที่ 2: 20,000 * 5% = 1,000 บาท
        // ระยะที่ 3: 23,000 * 5% = 1,150 บาท
        if (thaiBuddhistYear >= 2575)
            return 1150m;
        else if (thaiBuddhistYear >= 2572)
            return 1000m;
        else if (thaiBuddhistYear >= 2569)
            return 875m;
        else
            return 750m; // Before 2569
    }

    /// <summary>
    /// Get current Social Security phase info (for display)
    /// </summary>
    public static (int Phase, string Description, decimal MaxBase, decimal MaxDeduction) GetCurrentSocialSecurityPhase()
    {
        int thaiYear = DateTime.Now.Year + 543;

        if (thaiYear >= 2575)
            return (3, $"ระยะที่ 3 (พ.ศ.2575+)", 23000m, 1150m);
        else if (thaiYear >= 2572)
            return (2, $"ระยะที่ 2 (พ.ศ.2572-2574)", 20000m, 1000m);
        else if (thaiYear >= 2569)
            return (1, $"ระยะที่ 1 (พ.ศ.2569-2571)", 17500m, 875m);
        else
            return (0, $"ก่อน พ.ศ.2569", 15000m, 750m);
    }

    #endregion

    #region Salary Settings

    /// <summary>
    /// Minimum wage per month (default: 10,500 THB based on 350 THB/day * 30 days)
    /// </summary>
    public static decimal MinimumWage
    {
        get
        {
            string configValue = ConfigurationManager.AppSettings["MinimumWage"];
            return decimal.TryParse(configValue, out decimal result) ? result : 10500m;
        }
    }

    /// <summary>
    /// Maximum allowed salary for validation (default: 9,999,999 THB)
    /// </summary>
    public static decimal MaximumSalary
    {
        get
        {
            string configValue = ConfigurationManager.AppSettings["MaximumSalary"];
            return decimal.TryParse(configValue, out decimal result) ? result : 9999999m;
        }
    }

    /// <summary>
    /// Standard working hours per day (default: 8)
    /// </summary>
    public static decimal WorkingHoursPerDay
    {
        get
        {
            string configValue = ConfigurationManager.AppSettings["WorkingHoursPerDay"];
            return decimal.TryParse(configValue, out decimal result) ? result : 8m;
        }
    }

    /// <summary>
    /// Standard working days per month (default: 30)
    /// </summary>
    public static int WorkingDaysPerMonth
    {
        get
        {
            string configValue = ConfigurationManager.AppSettings["WorkingDaysPerMonth"];
            return int.TryParse(configValue, out int result) ? result : 30;
        }
    }

    #endregion

    #region OT Settings

    /// <summary>
    /// OT Rate multiplier for normal OT (default: 1.5x)
    /// </summary>
    public static decimal OTRateNormal
    {
        get
        {
            string configValue = ConfigurationManager.AppSettings["OTRateNormal"];
            return decimal.TryParse(configValue, out decimal result) ? result : 1.5m;
        }
    }

    /// <summary>
    /// OT Rate multiplier for holiday OT (default: 3x)
    /// </summary>
    public static decimal OTRateHoliday
    {
        get
        {
            string configValue = ConfigurationManager.AppSettings["OTRateHoliday"];
            return decimal.TryParse(configValue, out decimal result) ? result : 3m;
        }
    }

    /// <summary>
    /// Maximum OT hours per day (default: 4)
    /// </summary>
    public static decimal MaxOTHoursPerDay
    {
        get
        {
            string configValue = ConfigurationManager.AppSettings["MaxOTHoursPerDay"];
            return decimal.TryParse(configValue, out decimal result) ? result : 4m;
        }
    }

    #endregion

    #region Leave Settings

    /// <summary>
    /// Default annual leave quota for new employees (default: 6 days)
    /// </summary>
    public static decimal DefaultAnnualLeaveQuota
    {
        get
        {
            string configValue = ConfigurationManager.AppSettings["DefaultAnnualLeaveQuota"];
            return decimal.TryParse(configValue, out decimal result) ? result : 6m;
        }
    }

    /// <summary>
    /// Default sick leave quota (default: 30 days)
    /// </summary>
    public static decimal DefaultSickLeaveQuota
    {
        get
        {
            string configValue = ConfigurationManager.AppSettings["DefaultSickLeaveQuota"];
            return decimal.TryParse(configValue, out decimal result) ? result : 30m;
        }
    }

    /// <summary>
    /// Maximum carry forward days for annual leave (default: 6)
    /// </summary>
    public static decimal MaxCarryForwardDays
    {
        get
        {
            string configValue = ConfigurationManager.AppSettings["MaxCarryForwardDays"];
            return decimal.TryParse(configValue, out decimal result) ? result : 6m;
        }
    }

    #endregion

    #region Thai Months

    /// <summary>
    /// Get Thai month name from month number (1-12)
    /// </summary>
    public static string GetThaiMonthName(int month)
    {
        switch (month)
        {
            case 1: return "มกราคม";
            case 2: return "กุมภาพันธ์";
            case 3: return "มีนาคม";
            case 4: return "เมษายน";
            case 5: return "พฤษภาคม";
            case 6: return "มิถุนายน";
            case 7: return "กรกฎาคม";
            case 8: return "สิงหาคม";
            case 9: return "กันยายน";
            case 10: return "ตุลาคม";
            case 11: return "พฤศจิกายน";
            case 12: return "ธันวาคม";
            default: return "";
        }
    }

    /// <summary>
    /// Get Thai month short name from month number (1-12)
    /// </summary>
    public static string GetThaiMonthShortName(int month)
    {
        switch (month)
        {
            case 1: return "ม.ค.";
            case 2: return "ก.พ.";
            case 3: return "มี.ค.";
            case 4: return "เม.ย.";
            case 5: return "พ.ค.";
            case 6: return "มิ.ย.";
            case 7: return "ก.ค.";
            case 8: return "ส.ค.";
            case 9: return "ก.ย.";
            case 10: return "ต.ค.";
            case 11: return "พ.ย.";
            case 12: return "ธ.ค.";
            default: return "";
        }
    }

    #endregion

    #region Calculation Methods

    /// <summary>
    /// Calculate Social Security deduction from total earnings
    /// Total earnings = base salary + OT + bonus + allowance
    /// </summary>
    public static decimal CalculateSocialSecurity(decimal totalEarnings)
    {
        if (totalEarnings < SocialSecurityMinBase)
            return 0;

        decimal ssBase = Math.Min(SocialSecurityMaxBase, totalEarnings);
        decimal ss = Math.Round(ssBase * SocialSecurityRate, 0);
        return Math.Min(ss, SocialSecurityMaxDeduction);
    }

    /// <summary>
    /// Calculate hourly rate from monthly salary
    /// </summary>
    public static decimal CalculateHourlyRate(decimal monthlySalary)
    {
        decimal totalHours = WorkingDaysPerMonth * WorkingHoursPerDay;
        return totalHours > 0 ? Math.Round(monthlySalary / totalHours, 2) : 0;
    }

    /// <summary>
    /// Calculate daily rate from monthly salary
    /// </summary>
    public static decimal CalculateDailyRate(decimal monthlySalary)
    {
        return WorkingDaysPerMonth > 0 ? Math.Round(monthlySalary / WorkingDaysPerMonth, 2) : 0;
    }

    /// <summary>
    /// Calculate OT amount with multiplier (1.5x normal, 3x holiday)
    /// </summary>
    public static decimal CalculateOTAmount(decimal monthlySalary, decimal otHours, bool isHoliday = false)
    {
        decimal hourlyRate = CalculateHourlyRate(monthlySalary);
        decimal multiplier = isHoliday ? OTRateHoliday : OTRateNormal;
        return Math.Round(hourlyRate * multiplier * otHours, 2);
    }

    /// <summary>
    /// Calculate basic OT amount without multiplier
    /// Formula: (salary / days in month / 8) * OT hours
    /// </summary>
    public static decimal CalculateBasicOTAmount(decimal monthlySalary, decimal otHours, int daysInMonth = 0)
    {
        if (daysInMonth <= 0) daysInMonth = WorkingDaysPerMonth;
        decimal hourlyRate = monthlySalary / daysInMonth / WorkingHoursPerDay;
        return Math.Round(hourlyRate * otHours, 0);
    }

    /// <summary>
    /// Calculate leave deduction
    /// Formula: (salary / days in month) * leave days (rounded)
    /// </summary>
    public static decimal CalculateLeaveDeduction(decimal monthlySalary, decimal leaveDays, int daysInMonth = 0)
    {
        if (daysInMonth <= 0) daysInMonth = WorkingDaysPerMonth;
        if (leaveDays <= 0) return 0;
        decimal dailyRate = monthlySalary / daysInMonth;
        return Math.Round(dailyRate * leaveDays, 0);
    }

    /// <summary>
    /// Get number of days in a specific month
    /// </summary>
    public static int GetDaysInMonth(int year, int month)
    {
        return DateTime.DaysInMonth(year, month);
    }

    #endregion
}
