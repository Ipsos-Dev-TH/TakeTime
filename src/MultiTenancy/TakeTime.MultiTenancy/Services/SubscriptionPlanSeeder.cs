using TakeTime.MultiTenancy.Core;

namespace TakeTime.MultiTenancy.Services;

/// <summary>
/// Provides default subscription plans for seeding the database.
/// Plans are tailored for the Thai hospitality market with THB pricing.
/// </summary>
public static class SubscriptionPlanSeeder
{
    /// <summary>
    /// Returns the four default subscription plans: Free, Starter, Professional, and Enterprise.
    /// </summary>
    public static List<SubscriptionPlan> GetDefaultPlans()
    {
        return
        [
            new SubscriptionPlan
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Code = "free",
                Name = "Free",
                NameTh = "แพ็กเกจฟรี",
                Description = "Basic plan for small properties just getting started. Includes essential reservation management.",
                DescriptionTh = "แพ็กเกจพื้นฐานสำหรับที่พักขนาดเล็ก รวมระบบจัดการการจองเบื้องต้น",
                Tier = SubscriptionTier.Free,
                MonthlyPrice = 0m,
                YearlyPrice = 0m,
                Currency = "THB",
                MaxRooms = 5,
                MaxUsers = 2,
                MaxStorageGB = 1,
                IncludedFeatures = ["reservation_basic", "calendar_view", "guest_list"],
                IncludedModules = ["Reservation"],
                TrialAvailable = false,
                TrialDays = 0,
                IsActive = true,
                SortOrder = 0
            },
            new SubscriptionPlan
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                Code = "starter",
                Name = "Starter",
                NameTh = "แพ็กเกจสตาร์ทเตอร์",
                Description = "Ideal for small to medium properties. Includes reservation, payment, and basic CRM features.",
                DescriptionTh = "เหมาะสำหรับที่พักขนาดเล็กถึงกลาง รวมระบบจอง ระบบชำระเงิน และ CRM เบื้องต้น",
                Tier = SubscriptionTier.Starter,
                MonthlyPrice = 1990m,
                YearlyPrice = 19900m,
                Currency = "THB",
                MaxRooms = 20,
                MaxUsers = 5,
                MaxStorageGB = 5,
                IncludedFeatures =
                [
                    "reservation_basic", "reservation_advanced", "calendar_view",
                    "guest_list", "payment_basic", "reports_basic", "crm_basic"
                ],
                IncludedModules = ["Reservation", "Payment", "CRM"],
                TrialAvailable = true,
                TrialDays = 14,
                IsActive = true,
                SortOrder = 1
            },
            new SubscriptionPlan
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                Code = "professional",
                Name = "Professional",
                NameTh = "แพ็กเกจโปรเฟสชันนอล",
                Description = "For established properties needing full operational management including HR, inventory, and channel management.",
                DescriptionTh = "สำหรับที่พักที่ต้องการระบบจัดการครบวงจร รวม HR สต็อกสินค้า และ Channel Manager",
                Tier = SubscriptionTier.Professional,
                MonthlyPrice = 4990m,
                YearlyPrice = 49900m,
                Currency = "THB",
                MaxRooms = 100,
                MaxUsers = 20,
                MaxStorageGB = 25,
                IncludedFeatures =
                [
                    "reservation_basic", "reservation_advanced", "calendar_view",
                    "guest_list", "payment_basic", "payment_advanced", "reports_basic",
                    "reports_advanced", "crm_basic", "crm_advanced", "inventory_basic",
                    "hr_basic", "channel_manager", "housekeeping", "notifications"
                ],
                IncludedModules =
                [
                    "Reservation", "Payment", "CRM", "Inventory",
                    "HumanResources", "ChannelManager", "GuestExperience", "Notification"
                ],
                TrialAvailable = true,
                TrialDays = 30,
                IsActive = true,
                SortOrder = 2
            },
            new SubscriptionPlan
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000004"),
                Code = "enterprise",
                Name = "Enterprise",
                NameTh = "แพ็กเกจเอ็นเตอร์ไพรส์",
                Description = "Full-featured plan for large properties and hotel chains. All modules, unlimited features, priority support, and accounting.",
                DescriptionTh = "แพ็กเกจเต็มรูปแบบสำหรับที่พักขนาดใหญ่และเครือโรงแรม ทุกโมดูล ทุกฟีเจอร์ และการสนับสนุนพิเศษ",
                Tier = SubscriptionTier.Enterprise,
                MonthlyPrice = 9990m,
                YearlyPrice = 99900m,
                Currency = "THB",
                MaxRooms = 999,
                MaxUsers = 100,
                MaxStorageGB = 100,
                IncludedFeatures =
                [
                    "reservation_basic", "reservation_advanced", "calendar_view",
                    "guest_list", "payment_basic", "payment_advanced", "reports_basic",
                    "reports_advanced", "crm_basic", "crm_advanced", "inventory_basic",
                    "inventory_advanced", "hr_basic", "hr_advanced", "channel_manager",
                    "housekeeping", "maintenance", "room_service", "notifications",
                    "accounting_basic", "accounting_advanced", "affiliate_management",
                    "api_access", "custom_branding", "priority_support"
                ],
                IncludedModules =
                [
                    "Reservation", "Payment", "CRM", "Inventory",
                    "HumanResources", "ChannelManager", "GuestExperience",
                    "Notification", "Accounting", "Affiliate"
                ],
                TrialAvailable = true,
                TrialDays = 30,
                IsActive = true,
                SortOrder = 3
            }
        ];
    }
}
