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
    /// Maximum salary base for Social Security calculation (15,000 THB)
    /// </summary>
    public static decimal SocialSecurityMaxBase
    {
        get
        {
            string configValue = ConfigurationManager.AppSettings["SocialSecurityMaxBase"];
            return decimal.TryParse(configValue, out decimal result) ? result : 15000m;
        }
    }

    /// <summary>
    /// Maximum Social Security deduction (750 THB)
    /// </summary>
    public static decimal SocialSecurityMaxDeduction
    {
        get
        {
            string configValue = ConfigurationManager.AppSettings["SocialSecurityMaxDeduction"];
            return decimal.TryParse(configValue, out decimal result) ? result : 750m;
        }
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
    /// Calculate Social Security deduction from salary
    /// </summary>
    public static decimal CalculateSocialSecurity(decimal baseSalary)
    {
        if (baseSalary < SocialSecurityMinBase)
            return 0;

        decimal ssBase = Math.Min(SocialSecurityMaxBase, baseSalary);
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
    /// Calculate OT amount
    /// </summary>
    public static decimal CalculateOTAmount(decimal monthlySalary, decimal otHours, bool isHoliday = false)
    {
        decimal hourlyRate = CalculateHourlyRate(monthlySalary);
        decimal multiplier = isHoliday ? OTRateHoliday : OTRateNormal;
        return Math.Round(hourlyRate * multiplier * otHours, 2);
    }

    #endregion
}
