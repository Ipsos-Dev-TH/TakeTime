using System;
using System.Web.Script.Serialization;
using System.Web.UI;
using Take_Time_BangPhra.Dining;

namespace Take_Time_BangPhra.Admin.Dining
{
    /// <summary>
    /// แผนผังโต๊ะสด (ทีม B) — tile ตามโซน สี FREE/OCCUPIED/BILL_REQUESTED/NEEDS_STAFF/DISABLED, ยอดค้าง, อายุออเดอร์ล่าสุด
    /// คลิก = แผงข้างสรุป session + ปุ่มรับเงินสด/ตรวจสลิป/ปิดโต๊ะ/เปิดโต๊ะแทนลูกค้า — ทุก action เรียก WebMethod ของ <see cref="Orders"/>
    /// </summary>
    public partial class Tables : Page
    {
        protected string BootJson = "{}";

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Feature.Guard(this, DiningConfig.FeatureName)) return;
            if (!Perm.Guard(this, Perm.OpsDining)) return;
            if (Session["permission"]?.ToString() != "True") { Response.Redirect("~/Admin/Login"); return; }
            BootJson = new JavaScriptSerializer().Serialize(Orders.BuildBoot(this, "tables"));
        }
    }
}
