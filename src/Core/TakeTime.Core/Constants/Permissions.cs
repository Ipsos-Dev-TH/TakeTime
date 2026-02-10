namespace TakeTime.Core.Constants;

/// <summary>
/// Static permission constants organized by functional module.
/// These permission strings are used in authorization policies,
/// claim-based checks, and UI feature toggles.
/// </summary>
public static class Permissions
{
    /// <summary>
    /// Permissions for the Reservation module.
    /// </summary>
    public static class Reservation
    {
        public const string View = "Reservation.View";
        public const string Create = "Reservation.Create";
        public const string Update = "Reservation.Update";
        public const string Delete = "Reservation.Delete";
        public const string Cancel = "Reservation.Cancel";
        public const string Approve = "Reservation.Approve";
        public const string ViewAll = "Reservation.ViewAll";
        public const string ManageWaitlist = "Reservation.ManageWaitlist";
    }

    /// <summary>
    /// Permissions for the Payment module.
    /// </summary>
    public static class Payment
    {
        public const string View = "Payment.View";
        public const string Process = "Payment.Process";
        public const string Refund = "Payment.Refund";
        public const string ViewReports = "Payment.ViewReports";
        public const string ManageInvoices = "Payment.ManageInvoices";
        public const string ApplyDiscount = "Payment.ApplyDiscount";
        public const string VoidTransaction = "Payment.VoidTransaction";
    }

    /// <summary>
    /// Permissions for the Customer module.
    /// </summary>
    public static class Customer
    {
        public const string View = "Customer.View";
        public const string Create = "Customer.Create";
        public const string Update = "Customer.Update";
        public const string Delete = "Customer.Delete";
        public const string ViewHistory = "Customer.ViewHistory";
        public const string ManageMembership = "Customer.ManageMembership";
        public const string Export = "Customer.Export";
    }

    /// <summary>
    /// Permissions for the Service/Treatment module.
    /// </summary>
    public static class Service
    {
        public const string View = "Service.View";
        public const string Create = "Service.Create";
        public const string Update = "Service.Update";
        public const string Delete = "Service.Delete";
        public const string ManagePricing = "Service.ManagePricing";
        public const string ManageCategories = "Service.ManageCategories";
    }

    /// <summary>
    /// Permissions for the Staff/Employee module.
    /// </summary>
    public static class Staff
    {
        public const string View = "Staff.View";
        public const string Create = "Staff.Create";
        public const string Update = "Staff.Update";
        public const string Delete = "Staff.Delete";
        public const string ManageSchedule = "Staff.ManageSchedule";
        public const string ViewPerformance = "Staff.ViewPerformance";
        public const string ManageLeave = "Staff.ManageLeave";
        public const string ManageCommission = "Staff.ManageCommission";
    }

    /// <summary>
    /// Permissions for the Inventory module.
    /// </summary>
    public static class Inventory
    {
        public const string View = "Inventory.View";
        public const string Create = "Inventory.Create";
        public const string Update = "Inventory.Update";
        public const string Delete = "Inventory.Delete";
        public const string ManageStock = "Inventory.ManageStock";
        public const string ViewReports = "Inventory.ViewReports";
        public const string ManageSuppliers = "Inventory.ManageSuppliers";
    }

    /// <summary>
    /// Permissions for the Reporting module.
    /// </summary>
    public static class Report
    {
        public const string ViewDashboard = "Report.ViewDashboard";
        public const string ViewFinancial = "Report.ViewFinancial";
        public const string ViewOperational = "Report.ViewOperational";
        public const string ViewStaffPerformance = "Report.ViewStaffPerformance";
        public const string Export = "Report.Export";
        public const string ViewAnalytics = "Report.ViewAnalytics";
    }

    /// <summary>
    /// Permissions for the System/Settings module.
    /// </summary>
    public static class System
    {
        public const string ManageSettings = "System.ManageSettings";
        public const string ManageUsers = "System.ManageUsers";
        public const string ManageRoles = "System.ManageRoles";
        public const string ManageTenants = "System.ManageTenants";
        public const string ViewAuditLog = "System.ViewAuditLog";
        public const string ManageIntegrations = "System.ManageIntegrations";
        public const string ManageNotifications = "System.ManageNotifications";
    }

    /// <summary>
    /// Permissions for the Branch/Location module.
    /// </summary>
    public static class Branch
    {
        public const string View = "Branch.View";
        public const string Create = "Branch.Create";
        public const string Update = "Branch.Update";
        public const string Delete = "Branch.Delete";
        public const string ManageRooms = "Branch.ManageRooms";
        public const string ManageHours = "Branch.ManageHours";
    }

    /// <summary>
    /// Returns all defined permissions as a flat collection.
    /// Useful for seeding the permissions table or generating documentation.
    /// </summary>
    public static IReadOnlyCollection<string> All =>
    [
        // Reservation
        Reservation.View, Reservation.Create, Reservation.Update, Reservation.Delete,
        Reservation.Cancel, Reservation.Approve, Reservation.ViewAll, Reservation.ManageWaitlist,
        // Payment
        Payment.View, Payment.Process, Payment.Refund, Payment.ViewReports,
        Payment.ManageInvoices, Payment.ApplyDiscount, Payment.VoidTransaction,
        // Customer
        Customer.View, Customer.Create, Customer.Update, Customer.Delete,
        Customer.ViewHistory, Customer.ManageMembership, Customer.Export,
        // Service
        Service.View, Service.Create, Service.Update, Service.Delete,
        Service.ManagePricing, Service.ManageCategories,
        // Staff
        Staff.View, Staff.Create, Staff.Update, Staff.Delete,
        Staff.ManageSchedule, Staff.ViewPerformance, Staff.ManageLeave, Staff.ManageCommission,
        // Inventory
        Inventory.View, Inventory.Create, Inventory.Update, Inventory.Delete,
        Inventory.ManageStock, Inventory.ViewReports, Inventory.ManageSuppliers,
        // Report
        Report.ViewDashboard, Report.ViewFinancial, Report.ViewOperational,
        Report.ViewStaffPerformance, Report.Export, Report.ViewAnalytics,
        // System
        System.ManageSettings, System.ManageUsers, System.ManageRoles, System.ManageTenants,
        System.ViewAuditLog, System.ManageIntegrations, System.ManageNotifications,
        // Branch
        Branch.View, Branch.Create, Branch.Update, Branch.Delete,
        Branch.ManageRooms, Branch.ManageHours
    ];
}
