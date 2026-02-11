namespace TakeTime.MultiTenancy.Features;

/// <summary>
/// Configuration for a single module (bounded context) within a tenant.
/// Controls whether the module is enabled and which sub-features within
/// the module are available.
/// </summary>
public class ModuleConfiguration
{
    /// <summary>The module this configuration applies to.</summary>
    public FeatureModule Module { get; set; }

    /// <summary>Whether the entire module is enabled for this tenant.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Sub-features within this module that can be individually toggled.
    /// Key = sub-feature identifier (e.g., "DynamicPricing", "Waitlist").
    /// Value = whether it is enabled.
    /// </summary>
    public Dictionary<string, bool> SubFeatures { get; set; } = [];

    /// <summary>
    /// Menu items associated with this module. Each menu item can be
    /// individually shown or hidden without disabling the underlying feature.
    /// </summary>
    public List<MenuItemConfiguration> MenuItems { get; set; } = [];

    /// <summary>Checks whether a specific sub-feature is enabled.</summary>
    public bool IsSubFeatureEnabled(string subFeature)
    {
        if (!IsEnabled) return false;
        return SubFeatures.TryGetValue(subFeature, out var enabled) ? enabled : true;
    }

    /// <summary>Checks whether a specific menu item is visible.</summary>
    public bool IsMenuVisible(string menuKey)
    {
        if (!IsEnabled) return false;
        var item = MenuItems.FirstOrDefault(m =>
            m.Key.Equals(menuKey, StringComparison.OrdinalIgnoreCase));
        return item?.IsVisible ?? true;
    }

    /// <summary>
    /// Creates a default configuration for a module with all
    /// sub-features enabled.
    /// </summary>
    public static ModuleConfiguration CreateDefault(FeatureModule module, bool enabled = true)
    {
        return new ModuleConfiguration
        {
            Module = module,
            IsEnabled = enabled,
            SubFeatures = GetDefaultSubFeatures(module),
            MenuItems = GetDefaultMenuItems(module)
        };
    }

    private static Dictionary<string, bool> GetDefaultSubFeatures(FeatureModule module) => module switch
    {
        FeatureModule.Reservation => new()
        {
            ["OnlineBooking"] = true,
            ["WalkIn"] = true,
            ["GroupBooking"] = true,
            ["Waitlist"] = false,
            ["DynamicPricing"] = false,
            ["SeasonalPricing"] = false,
            ["AutoConfirmation"] = true,
            ["OverbookingProtection"] = true
        },
        FeatureModule.Payment => new()
        {
            ["CashPayment"] = true,
            ["BankTransfer"] = true,
            ["PromptPay"] = true,
            ["CreditCard"] = false,
            ["PartialPayment"] = false,
            ["Installments"] = false,
            ["AutoReceipt"] = true,
            ["TaxInvoice"] = true,
            ["CreditNote"] = true
        },
        FeatureModule.Inventory => new()
        {
            ["ProductCatalog"] = true,
            ["StockManagement"] = true,
            ["POS"] = false,
            ["LowStockAlerts"] = true,
            ["Barcode"] = false,
            ["StockTransfer"] = false
        },
        FeatureModule.HumanResources => new()
        {
            ["EmployeeManagement"] = true,
            ["LeaveManagement"] = true,
            ["Payroll"] = true,
            ["OvertimeTracking"] = false,
            ["Attendance"] = false,
            ["Performance"] = false,
            ["Scheduling"] = true
        },
        FeatureModule.CRM => new()
        {
            ["CustomerProfiles"] = true,
            ["LoyaltyProgram"] = false,
            ["GuestReviews"] = true,
            ["CustomerAnalytics"] = false,
            ["MarketingCampaigns"] = false,
            ["GuestPortal"] = false
        },
        FeatureModule.ChannelManager => new()
        {
            ["OTASync"] = true,
            ["RateManager"] = true,
            ["AvailabilitySync"] = true,
            ["ReservationImport"] = true,
            ["AutoMapping"] = false
        },
        FeatureModule.GuestExperience => new()
        {
            ["Housekeeping"] = true,
            ["RoomService"] = false,
            ["MaintenanceTracking"] = true,
            ["GuestRequests"] = false,
            ["MiniBar"] = false
        },
        FeatureModule.Accounting => new()
        {
            ["DailyRevenue"] = true,
            ["MonthlyReport"] = true,
            ["ProfitAndLoss"] = true,
            ["OccupancyReport"] = true,
            ["TaxReport"] = true,
            ["ExportExcel"] = true,
            ["ExportPDF"] = true
        },
        FeatureModule.Notification => new()
        {
            ["Email"] = true,
            ["SMS"] = false,
            ["LINE"] = false,
            ["Telegram"] = false,
            ["BookingConfirmation"] = true,
            ["PaymentReminder"] = true,
            ["CheckInReminder"] = true
        },
        FeatureModule.Affiliate => new()
        {
            ["PartnerRegistration"] = true,
            ["CommissionTracking"] = true,
            ["ReferralLinks"] = true,
            ["PayoutManagement"] = true,
            ["PerformanceReport"] = true
        },
        _ => []
    };

    private static List<MenuItemConfiguration> GetDefaultMenuItems(FeatureModule module) => module switch
    {
        FeatureModule.Reservation => [
            new("reservations", "การจอง", "Reservations", "calendar", 1),
            new("reservations.calendar", "ปฏิทินการจอง", "Booking Calendar", "calendar-days", 2),
            new("reservations.new", "จองใหม่", "New Booking", "plus-circle", 3),
            new("reservations.checkin", "เช็คอินวันนี้", "Today Check-in", "arrow-right-to-bracket", 4),
            new("reservations.checkout", "เช็คเอาท์วันนี้", "Today Check-out", "arrow-right-from-bracket", 5),
            new("accommodations", "ห้องพัก", "Accommodations", "bed", 6),
            new("accommodations.types", "ประเภทห้อง", "Room Types", "tags", 7),
            new("accommodations.rates", "อัตราค่าห้อง", "Room Rates", "money-bill", 8)
        ],
        FeatureModule.Payment => [
            new("payments", "การเงิน", "Payments", "credit-card", 1),
            new("payments.pending", "รอชำระ", "Pending Payments", "clock", 2),
            new("payments.history", "ประวัติการชำระ", "Payment History", "history", 3),
            new("receipts", "ใบเสร็จ", "Receipts", "receipt", 4),
            new("tax-invoices", "ใบกำกับภาษี", "Tax Invoices", "file-invoice", 5),
            new("credit-notes", "ใบลดหนี้", "Credit Notes", "file-minus", 6)
        ],
        FeatureModule.Inventory => [
            new("inventory", "สินค้า/คลัง", "Inventory", "boxes-stacked", 1),
            new("inventory.products", "รายการสินค้า", "Products", "box", 2),
            new("inventory.stock", "สต็อก", "Stock", "warehouse", 3),
            new("inventory.pos", "ขายหน้าร้าน", "Point of Sale", "cash-register", 4),
            new("inventory.sales", "รายงานยอดขาย", "Sales Report", "chart-line", 5)
        ],
        FeatureModule.HumanResources => [
            new("hr", "บุคลากร", "Human Resources", "users", 1),
            new("hr.employees", "พนักงาน", "Employees", "user", 2),
            new("hr.leave", "การลา", "Leave Management", "calendar-xmark", 3),
            new("hr.payroll", "เงินเดือน", "Payroll", "money-bill-wave", 4),
            new("hr.overtime", "ล่วงเวลา", "Overtime", "clock-rotate-left", 5),
            new("hr.schedule", "ตารางงาน", "Work Schedule", "calendar-check", 6)
        ],
        FeatureModule.CRM => [
            new("crm", "ลูกค้า", "CRM", "address-book", 1),
            new("crm.customers", "รายชื่อลูกค้า", "Customers", "users", 2),
            new("crm.loyalty", "สะสมแต้ม", "Loyalty Program", "star", 3),
            new("crm.reviews", "รีวิว", "Guest Reviews", "comments", 4),
            new("crm.analytics", "วิเคราะห์ลูกค้า", "Customer Analytics", "chart-pie", 5)
        ],
        FeatureModule.ChannelManager => [
            new("channels", "ช่องทางจอง", "Channel Manager", "globe", 1),
            new("channels.connections", "การเชื่อมต่อ", "Connections", "link", 2),
            new("channels.rates", "จัดการราคา", "Rate Manager", "tags", 3),
            new("channels.sync", "ซิงค์ข้อมูล", "Sync Status", "rotate", 4)
        ],
        FeatureModule.GuestExperience => [
            new("guest", "บริการแขก", "Guest Experience", "concierge-bell", 1),
            new("guest.housekeeping", "แม่บ้าน", "Housekeeping", "broom", 2),
            new("guest.roomservice", "รูมเซอร์วิส", "Room Service", "utensils", 3),
            new("guest.maintenance", "ซ่อมบำรุง", "Maintenance", "wrench", 4)
        ],
        FeatureModule.Accounting => [
            new("reports", "รายงาน", "Reports", "chart-bar", 1),
            new("reports.daily", "รายงานรายวัน", "Daily Revenue", "calendar-day", 2),
            new("reports.monthly", "รายงานรายเดือน", "Monthly Report", "calendar", 3),
            new("reports.pnl", "กำไรขาดทุน", "Profit & Loss", "scale-balanced", 4),
            new("reports.occupancy", "อัตราเข้าพัก", "Occupancy", "bed", 5),
            new("reports.tax", "รายงานภาษี", "Tax Report", "file-invoice-dollar", 6)
        ],
        FeatureModule.Notification => [
            new("notifications", "การแจ้งเตือน", "Notifications", "bell", 1),
            new("notifications.templates", "เทมเพลต", "Templates", "file-lines", 2),
            new("notifications.history", "ประวัติการส่ง", "Send History", "clock-rotate-left", 3),
            new("notifications.settings", "ตั้งค่า", "Settings", "gear", 4)
        ],
        FeatureModule.Affiliate => [
            new("affiliate", "พาร์ทเนอร์", "Affiliate", "handshake", 1),
            new("affiliate.partners", "รายชื่อพาร์ทเนอร์", "Partners", "users", 2),
            new("affiliate.commissions", "ค่าคอมมิชชัน", "Commissions", "coins", 3),
            new("affiliate.payouts", "การจ่ายเงิน", "Payouts", "money-bill-transfer", 4)
        ],
        _ => []
    };
}
