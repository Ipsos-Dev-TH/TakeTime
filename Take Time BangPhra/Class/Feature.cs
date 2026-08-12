using System;
using System.Collections.Generic;

/// <summary>
/// สวิตช์เปิด/ปิดฟีเจอร์รายโมดูล — ตั้งค่าได้ที่ ศูนย์รวมการตั้งค่าระบบ (หมวด "ฟีเจอร์")
/// เก็บใน System_Config คีย์ "Feature_&lt;ชื่อ&gt;" ผ่าน AppCfg (cache 30 วิ มีผลแทบทันที)
///
/// ปิดแล้วเกิดอะไร: เมนูใน Site.Master ซ่อน, เข้าหน้าของโมดูลตรง ๆ ถูก redirect ออก
/// (Feature.Guard), การ์ดใน Guest Portal ซ่อน — ข้อมูลเดิมในตารางไม่ถูกแตะ เปิดกลับมา
/// ก็ใช้ต่อได้ทันที
///
/// ค่าเริ่มต้น: เปิดทุกโมดูล ยกเว้นที่ยังไม่ได้ใช้งานจริง/ยังไม่ได้ต่อกับระบบหลัก
/// (แม่บ้าน, งานซ่อมบำรุง, Dynamic Pricing) — ดู DefaultOff ด้านล่าง
/// (วางที่ global namespace เช่นเดียวกับ AppCfg เพื่อให้ทุกไฟล์เรียกได้โดยไม่ต้อง using)
/// </summary>
public static class Feature
{
    /// <summary>โมดูลที่ "ปิด" เป็นค่าเริ่มต้น — เปิดใช้เมื่อพร้อมจากหน้าตั้งค่า</summary>
    private static readonly HashSet<string> DefaultOff = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Housekeeping",     // สถานะทำความสะอาด — ยังไม่ได้ใช้งานจริง
        "Maintenance",      // งานซ่อมบำรุง — ยังไม่ได้ใช้งานจริง
        "DynamicPricing"    // ราคาไดนามิก — ยังไม่ได้ต่อเข้ากับราคาจองจริง
    };

    public static bool On(string name)
    {
        bool def = !DefaultOff.Contains(name);
        return AppCfg.GetBool("Feature_" + name, def);
    }

    public static bool Off(string name) => !On(name);

    /// <summary>
    /// กันเข้าหน้าของโมดูลที่ปิดอยู่ — เรียกบรรทัดแรกใน Page_Load:
    /// <c>if (!Feature.Guard(this, "Housekeeping")) return;</c>
    /// คืน false (พร้อม redirect แล้ว) เมื่อฟีเจอร์ปิด
    /// </summary>
    public static bool Guard(System.Web.UI.Page page, string name, string redirect = "~/Default")
    {
        if (On(name)) return true;
        page.Response.Redirect(redirect, false);
        System.Web.HttpContext.Current?.ApplicationInstance?.CompleteRequest();
        return false;
    }
}
