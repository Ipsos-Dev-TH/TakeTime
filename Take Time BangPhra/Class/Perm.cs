using System;
using System.Collections.Generic;
using System.Configuration;

using System.Data.SqlClient;
using System.Web;

/// <summary>
/// สิทธิ์การใช้งานรายโมดูล — "มองเห็น" (เมนู) และ "เข้าใช้งาน" (เปิดหน้า)
///
/// ทำงานเป็นชั้นบนของระบบเดิม ไม่ทับของเก่า:
///   • ผู้ใช้ที่ถูกกำหนด "กลุ่มสิทธิ์" (Admin.Permission_Group_ID) → ใช้สิทธิ์ตามกลุ่ม
///   • ผู้ใช้ที่ยังไม่ถูกกำหนดกลุ่ม → ใช้สิทธิ์ตาม Role เดิม (Owner/Admin/Staff) เป๊ะ ๆ
///     ⟹ ติดตั้งแล้วระบบทำงานเหมือนเดิมจนกว่าจะเริ่มจัดกลุ่ม
///   • Role = Owner เข้าถึงได้ทุกอย่างเสมอ (กันตั้งค่าพลาดแล้วล็อกตัวเองออก)
///
/// วางที่ global namespace เช่นเดียวกับ AppCfg / Feature เพื่อให้เรียกได้ทุกไฟล์
/// </summary>
public static class Perm
{
    // ── รหัสโมดูล (ต้องตรงกับที่ seed ใน PHASE18_23) ──────────────────────────
    public const string OpsBooking = "OPS_BOOKING";
    public const string OpsHousekeeping = "OPS_HOUSEKEEPING";
    public const string OpsMaintenance = "OPS_MAINTENANCE";
    public const string OpsChat = "OPS_CHAT";
    public const string OpsRoomService = "OPS_ROOMSERVICE";
    public const string OpsActivity = "OPS_ACTIVITY";
    public const string SalesPos = "SALES_POS";
    public const string SalesVoucher = "SALES_VOUCHER";
    public const string SalesStock = "SALES_STOCK";
    public const string FinReceipt = "FIN_RECEIPT";
    public const string FinVoucher = "FIN_VOUCHER";
    public const string FinReport = "FIN_REPORT";
    public const string CrmCustomer = "CRM_CUSTOMER";
    public const string CrmLoyalty = "CRM_LOYALTY";
    public const string CrmReview = "CRM_REVIEW";
    public const string CrmAffiliate = "CRM_AFFILIATE";
    public const string MgtDashboard = "MGT_DASHBOARD";
    public const string MgtReport = "MGT_REPORT";
    public const string MgtChannel = "MGT_CHANNEL";
    public const string HrEmployee = "HR_EMPLOYEE";
    public const string HrLeave = "HR_LEAVE";
    public const string HrPayroll = "HR_PAYROLL";
    public const string HrAsset = "HR_ASSET";
    public const string SysSettings = "SYS_SETTINGS";
    public const string SysDatabase = "SYS_DATABASE";
    // ── แยกย่อยจาก SYS_SETTINGS (PHASE19_04) ────────────────────────────────
    // เดิมศูนย์ตั้งค่าทั้งหมดอยู่ใต้ SYS_SETTINGS โมดูลเดียว ⇒ ให้สิทธิ์คนแก้เนื้อหาเว็บ
    // เท่ากับให้กุญแจตั้งค่าบัญชี/Token ไปด้วย. แยกออกมาเพื่อมอบสิทธิ์เฉพาะส่วนได้จริง
    public const string WebContent = "WEB_CONTENT";
    public const string SysAccounting = "SYS_ACCOUNTING";
    public const string SysChannel = "SYS_CHANNEL";
    public const string SvcGuest = "SVC_GUEST";
    // ── รับชำระเงินออนไลน์ (PHASE19_05) ─────────────────────────────────────
    // แยกจาก SYS_ACCOUNTING เพราะเป็นกุญแจรับเงินจริง ควรจำกัดคนได้แคบกว่าตั้งค่าบัญชี
    public const string SysPayment = "SYS_PAYMENT";

    /// <summary>รายการโมดูลพร้อมชื่อไทย + หมวด — ใช้วาดตารางสิทธิ์ในหน้าจัดการ</summary>
    public class ModuleInfo
    {
        public string Code, Name, Category, Note;
        public ModuleInfo(string code, string name, string category, string note = null)
        { Code = code; Name = name; Category = category; Note = note; }
    }

    public static readonly List<ModuleInfo> Catalog = new List<ModuleInfo>
    {
        new ModuleInfo(OpsBooking,      "การจอง / ผู้เข้าพัก",      "งานประจำวัน", "ผู้เข้าพักรายวัน ปฏิทิน รายการจอง เลื่อนเข้าพัก บอร์ดรายวัน"),
        new ModuleInfo(OpsHousekeeping, "แม่บ้าน / สถานะห้อง",     "งานประจำวัน"),
        new ModuleInfo(OpsMaintenance,  "งานซ่อมบำรุง",             "งานประจำวัน"),
        new ModuleInfo(OpsChat,         "แชทลูกค้า",                "บริการลูกค้า", "กล่องแชทรวมทุกช่องทาง"),
        new ModuleInfo(OpsRoomService,  "รูมเซอร์วิส (ออเดอร์)",    "บริการลูกค้า"),
        new ModuleInfo(OpsActivity,     "กิจกรรม / การจองรอบ",      "บริการลูกค้า"),
        new ModuleInfo(SalesPos,        "ขายสินค้า (POS)",          "ขายหน้าร้าน"),
        new ModuleInfo(SalesVoucher,    "บัตรกำนัล",                "ขายหน้าร้าน"),
        new ModuleInfo(SalesStock,      "สต๊อกสินค้า",              "ขายหน้าร้าน", "รับเข้า ตรวจนับ รายงานขาย"),
        new ModuleInfo(FinReceipt,      "ใบเสร็จ / ใบกำกับ",        "การเงิน & บัญชี", "ออกใบเสร็จ ตรวจเอกสารขาย e-Tax ตรวจสลิป"),
        new ModuleInfo(FinVoucher,      "ใบสำคัญจ่าย",              "การเงิน & บัญชี", "ใบสำคัญจ่าย OCR จับคู่ใบกำกับซื้อ"),
        new ModuleInfo(FinReport,       "รายงานบัญชี",              "การเงิน & บัญชี"),
        new ModuleInfo(CrmCustomer,     "ข้อมูลลูกค้า",             "ลูกค้า & การตลาด", "จัดการลูกค้า Guest Profile"),
        new ModuleInfo(CrmLoyalty,      "สมาชิก / แต้มสะสม",        "ลูกค้า & การตลาด"),
        new ModuleInfo(CrmReview,       "รีวิว",                    "ลูกค้า & การตลาด"),
        new ModuleInfo(CrmAffiliate,    "Affiliate",                "ลูกค้า & การตลาด", "จัดการตัวแทน + จ่ายค่าคอม"),
        new ModuleInfo(MgtDashboard,    "Dashboard ภาพรวม",         "ผู้บริหาร"),
        new ModuleInfo(MgtReport,       "รายงานผู้บริหาร",          "ผู้บริหาร", "กำไร/ขาดทุน วิเคราะห์ลูกค้า สถิติเว็บ"),
        new ModuleInfo(MgtChannel,      "Channel Manager",          "ผู้บริหาร"),
        new ModuleInfo(HrEmployee,      "พนักงาน",                  "บุคคล (HR)"),
        new ModuleInfo(HrLeave,         "การลา",                    "บุคคล (HR)"),
        new ModuleInfo(HrPayroll,       "เงินเดือน / OT",           "บุคคล (HR)"),
        new ModuleInfo(HrAsset,         "ทรัพย์สิน",                "บุคคล (HR)"),
        new ModuleInfo(WebContent,      "เนื้อหาเว็บไซต์ & รูปภาพ", "ตั้งค่า",
            "หน้าแรก โปรโมชั่น สิ่งอำนวยความสะดวก สถานที่ใกล้เคียง เบิกของใช้ ข้อมูลฉุกเฉิน เกี่ยวกับเรา รูปสินค้า"),
        new ModuleInfo(SvcGuest,        "ตั้งค่าบริการในที่พัก",    "ตั้งค่า",
            "รูมเซอร์วิส (เวลา/ค่าบริการ) กิจกรรม Guest Portal QR ประจำห้อง"),
        new ModuleInfo(SysChannel,      "ช่องทางติดต่อ & AI",       "ตั้งค่า",
            "Token LINE/Facebook อีเมล OTA ตั้งค่า AI คลังความรู้"),
        new ModuleInfo(SysAccounting,   "ตั้งค่าบัญชี & ภาษี",      "ตั้งค่า",
            "NextAcc ผังบัญชี โหมด sync ลงบัญชีรายสินค้า สิทธิ์ระดับสมาชิก"),
        new ModuleInfo(SysPayment,      "รับชำระเงินออนไลน์",       "ตั้งค่า",
            "เกตเวย์บัตรเครดิต (Payso) QR ของร้าน กุญแจ API รายการชำระเงิน"),
        new ModuleInfo(SysSettings,     "ตั้งค่าระบบ (ส่วนที่เหลือ)", "ระบบ",
            "การเชื่อมต่อ/ระบบ ราคา&ช่องทางขาย ข้อมูลหลัก&ขั้นสูง — Token/API และกลุ่มสิทธิ์ยังเป็นของ Owner เท่านั้น"),
        new ModuleInfo(SysDatabase,     "ฐานข้อมูล / ข้อมูลหลัก",   "ระบบ")
    };

    // ── สิทธิ์เริ่มต้นตาม Role เดิม (ใช้เมื่อผู้ใช้ยังไม่ถูกกำหนดกลุ่ม) ────────
    private static readonly HashSet<string> AdminModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        OpsBooking, OpsHousekeeping, OpsMaintenance, OpsChat, OpsRoomService, OpsActivity,
        SalesPos, SalesVoucher, SalesStock,
        FinReceipt, FinVoucher, FinReport,
        CrmCustomer, CrmLoyalty, CrmReview, CrmAffiliate,
        // เดิม Admin เข้าศูนย์ตั้งค่าได้ (หน้าจะกรองรายการที่เป็นของ Owner ออกเอง)
        // — คงไว้เพื่อไม่ให้พฤติกรรมเปลี่ยนก่อนผู้ดูแลจะเริ่มจัดกลุ่ม
        // โมดูลที่แยกใหม่ต้องให้ครบด้วย ไม่งั้น Admin จะเสียสิทธิ์ที่เคยมีทันทีที่อัปเดต
        SysSettings, WebContent, SvcGuest, SysChannel, SysAccounting, SysPayment
    };

    private static readonly HashSet<string> StaffModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        OpsBooking, OpsHousekeeping, OpsMaintenance, OpsChat, OpsRoomService, OpsActivity,
        SalesPos, SalesVoucher, SalesStock,
        // Role เดิมของ Staff เห็นเมนูการเงิน/ลูกค้าด้วย — คงไว้เพื่อไม่ให้พฤติกรรมเปลี่ยน
        // ก่อนที่ผู้ดูแลจะตั้งกลุ่มเอง (กลุ่มมาตรฐาน "พนักงานหน้าร้าน" ปิดส่วนนี้ไว้แล้ว)
        FinReceipt, FinVoucher, FinReport,
        CrmCustomer, CrmLoyalty, CrmReview, CrmAffiliate
    };

    private static string ConnStr =>
        ConfigurationManager.ConnectionStrings["TaketimeConnectionString"]?.ConnectionString;

    // ── API หลัก ──────────────────────────────────────────────────────────────

    /// <summary>เห็นเมนูของโมดูลนี้ไหม</summary>
    public static bool CanView(string moduleCode) => Check(moduleCode, view: true);

    /// <summary>เปิดหน้าของโมดูลนี้ได้ไหม</summary>
    public static bool CanAccess(string moduleCode) => Check(moduleCode, view: false);

    /// <summary>
    /// กันเข้าหน้าที่ไม่มีสิทธิ์ — เรียกหลังเช็คว่าล็อกอินแล้ว:
    /// <c>if (!Perm.Guard(this, Perm.FinReceipt)) return;</c>
    /// </summary>
    public static bool Guard(System.Web.UI.Page page, string moduleCode, string redirect = "~/ReserveTable")
    {
        if (CanAccess(moduleCode)) return true;
        try
        {
            // ยังไม่ล็อกอิน → ไปหน้าล็อกอินเลย (ไม่งั้นจะโดนเด้งต่อเป็นทอด ๆ)
            bool loggedIn = HttpContext.Current?.Session?["permission"]?.ToString() == "True";
            string target = loggedIn
                ? redirect + "?denied=" + HttpUtility.UrlEncode(moduleCode)
                : "~/Admin/Login";
            page.Response.Redirect(target, false);
            HttpContext.Current?.ApplicationInstance?.CompleteRequest();
        }
        catch { }
        return false;
    }

    /// <summary>ล้าง cache สิทธิ์ของ request ปัจจุบัน (เรียกหลังบันทึกการตั้งค่ากลุ่ม)</summary>
    public static void Invalidate()
    {
        try { HttpContext.Current?.Items.Remove(CacheKey); } catch { }
        lock (_groupLock) { _groupCache = null; _groupLoadedAt = DateTime.MinValue; }
    }

    // ── การตัดสินสิทธิ์ ────────────────────────────────────────────────────────

    private static bool Check(string moduleCode, bool view)
    {
        if (string.IsNullOrEmpty(moduleCode)) return false;
        try
        {
            var ctx = HttpContext.Current;
            if (ctx == null) return true;   // งานเบื้องหลัง (timer) — ไม่จำกัดสิทธิ์

            // ยังไม่ล็อกอิน = ไม่มีสิทธิ์อะไรเลย
            if (ctx.Session == null || ctx.Session["permission"]?.ToString() != "True") return false;

            string role = ctx.Session["User"]?.ToString() ?? "";

            // Owner เข้าถึงได้ทุกอย่างเสมอ — กันตั้งค่าพลาดแล้วไม่มีใครเข้าหน้าตั้งค่าได้อีก
            if (string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase)) return true;

            var map = GetUserPermissions(ctx);
            if (map == null)
            {
                // ยังไม่ถูกกำหนดกลุ่ม (หรืออ่านตารางไม่ได้) → ใช้สิทธิ์ตาม Role เดิม
                return RoleDefault(role, moduleCode);
            }

            Tuple<bool, bool> perm;
            if (!map.TryGetValue(moduleCode, out perm)) return false;   // กลุ่มไม่ได้ให้สิทธิ์โมดูลนี้
            return view ? (perm.Item1 || perm.Item2) : perm.Item2;
        }
        catch
        {
            return true;   // ระบบสิทธิ์มีปัญหา → ไม่บล็อกการทำงาน (ของเดิมยังกันด้วย Role อยู่แล้ว)
        }
    }

    private static bool RoleDefault(string role, string moduleCode)
    {
        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            return AdminModules.Contains(moduleCode);
        if (string.Equals(role, "Staff", StringComparison.OrdinalIgnoreCase))
            return StaffModules.Contains(moduleCode);
        return false;
    }

    // ── โหลดสิทธิ์ของผู้ใช้ปัจจุบัน (cache ต่อ request) ────────────────────────

    private const string CacheKey = "__PermMap";

    /// <summary>คืน map โมดูล → (CanView, CanAccess) ของผู้ใช้ปัจจุบัน; null = ไม่ได้อยู่กลุ่มใด</summary>
    private static Dictionary<string, Tuple<bool, bool>> GetUserPermissions(HttpContext ctx)
    {
        if (ctx.Items.Contains(CacheKey))
            return ctx.Items[CacheKey] as Dictionary<string, Tuple<bool, bool>>;

        Dictionary<string, Tuple<bool, bool>> result = null;
        try
        {
            int adminId;
            if (int.TryParse(ctx.Session["UserID"]?.ToString(), out adminId) && adminId > 0)
            {
                int groupId = GetGroupIdForAdmin(adminId);
                if (groupId > 0) result = LoadGroupPermissions(groupId);
            }
        }
        catch { result = null; }

        ctx.Items[CacheKey] = result;
        return result;
    }

    private static int GetGroupIdForAdmin(int adminId)
    {
        try
        {
            using (var con = new SqlConnection(ConnStr))
            using (var cmd = new SqlCommand(
                @"SELECT g.ID
                    FROM [dbo].[Admin] a
                    JOIN Permission_Groups g ON g.ID = a.Permission_Group_ID AND g.Is_Active = 1
                   WHERE a.ID = @id", con))
            {
                cmd.Parameters.AddWithValue("@id", adminId);
                con.Open();
                object o = cmd.ExecuteScalar();
                return o == null || o == DBNull.Value ? 0 : Convert.ToInt32(o);
            }
        }
        catch { return 0; }   // ตาราง/คอลัมน์ยังไม่มี (ยังไม่รัน migration) → ใช้ Role เดิม
    }

    // cache สิทธิ์รายกลุ่ม (ทั้งระบบ) 60 วินาที — ลด query ต่อ request
    private static readonly object _groupLock = new object();
    private static Dictionary<int, Dictionary<string, Tuple<bool, bool>>> _groupCache;
    private static DateTime _groupLoadedAt = DateTime.MinValue;

    private static Dictionary<string, Tuple<bool, bool>> LoadGroupPermissions(int groupId)
    {
        lock (_groupLock)
        {
            if (_groupCache == null || (DateTime.UtcNow - _groupLoadedAt).TotalSeconds > 60)
            {
                var all = new Dictionary<int, Dictionary<string, Tuple<bool, bool>>>();
                try
                {
                    using (var con = new SqlConnection(ConnStr))
                    using (var cmd = new SqlCommand(
                        "SELECT Group_ID, Module_Code, Can_View, Can_Access FROM Permission_Group_Modules", con))
                    {
                        con.Open();
                        using (var rd = cmd.ExecuteReader())
                            while (rd.Read())
                            {
                                int gid = Convert.ToInt32(rd[0]);
                                if (!all.ContainsKey(gid))
                                    all[gid] = new Dictionary<string, Tuple<bool, bool>>(StringComparer.OrdinalIgnoreCase);
                                all[gid][rd[1].ToString()] =
                                    Tuple.Create(Convert.ToBoolean(rd[2]), Convert.ToBoolean(rd[3]));
                            }
                    }
                }
                catch { }
                _groupCache = all;
                _groupLoadedAt = DateTime.UtcNow;
            }

            Dictionary<string, Tuple<bool, bool>> map;
            return _groupCache.TryGetValue(groupId, out map) ? map : null;
        }
    }

    // ── ใช้ในหน้าจัดการกลุ่มสิทธิ์ ─────────────────────────────────────────────

    /// <summary>ชื่อกลุ่มของผู้ใช้ปัจจุบัน (ว่าง = ยังไม่กำหนดกลุ่ม ใช้ Role เดิม)</summary>
    public static string CurrentGroupName()
    {
        try
        {
            var ctx = HttpContext.Current;
            int adminId;
            if (ctx?.Session == null || !int.TryParse(ctx.Session["UserID"]?.ToString(), out adminId)) return "";
            using (var con = new SqlConnection(ConnStr))
            using (var cmd = new SqlCommand(
                @"SELECT g.Group_Name FROM [dbo].[Admin] a
                    JOIN Permission_Groups g ON g.ID = a.Permission_Group_ID
                   WHERE a.ID = @id", con))
            {
                cmd.Parameters.AddWithValue("@id", adminId);
                con.Open();
                object o = cmd.ExecuteScalar();
                return o == null || o == DBNull.Value ? "" : o.ToString();
            }
        }
        catch { return ""; }
    }
}
