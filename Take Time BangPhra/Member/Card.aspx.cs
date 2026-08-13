using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Text;
using System.Web.UI;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Member
{
    /// <summary>
    /// บัตรสมาชิกของฉัน — หน้าบัตร (รูป/สี tier) + เลเวล + วันหมดอายุ + ส่วนลดค่าห้อง
    /// ตามวันเข้าพัก + คูปอง (กดใช้ → โชว์โค้ดให้พนักงานภายในเวลาที่กำหนด)
    /// </summary>
    public partial class MemberCard : Page
    {
        private readonly string _conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private MemberPortalService _svc;
        private string Phone => Session["MemberPhone"]?.ToString();

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Feature.Guard(this, "Loyalty", "~/Default")) return;
            _svc = new MemberPortalService(_conn);

            if (string.IsNullOrEmpty(Phone) || Session["MemberMustSetPin"] != null)
            {
                Response.Redirect("~/Member/Login", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            if (!IsPostBack) BindAll();
        }

        private void BindAll()
        {
            var card = _svc.GetCard(Phone);
            if (card == null)
            {
                Session["MemberPhone"] = null;
                Response.Redirect("~/Member/Login", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            // ── บัตร ──
            string tierColor = card["TierColor"] == DBNull.Value ? "#8D6E63" : card["TierColor"].ToString();
            string img = card["Card_Image_Path"] == DBNull.Value ? "" : card["Card_Image_Path"].ToString();
            litCardBg.Text = !string.IsNullOrEmpty(img)
                ? $"<img class='bgimg' src='{ResolveUrl(img)}' alt='' />"
                : "";   // ไม่มีรูป → ใช้พื้นไล่สีตาม tier
            if (string.IsNullOrEmpty(img))
                divCard.Style["background"] = $"linear-gradient(135deg, {tierColor}, #37474f)";

            litTier.Text = Server.HtmlEncode(card["TierName"].ToString());
            litName.Text = Server.HtmlEncode(card["CustomerName"] == DBNull.Value || card["CustomerName"].ToString() == ""
                ? "สมาชิก Take Time" : "คุณ" + card["CustomerName"]);
            litPhone.Text = MaskPhone(Phone);
            litSince.Text = card["MemberSince"] == DBNull.Value ? "-"
                : Convert.ToDateTime(card["MemberSince"]).ToString("MM/yyyy");
            litPoints.Text = card["AvailablePoints"] == DBNull.Value ? "0"
                : Convert.ToInt32(card["AvailablePoints"]).ToString("N0");
            litExpiry.Text = card["Membership_Expiry"] == DBNull.Value ? "ตลอดชีพ"
                : Convert.ToDateTime(card["Membership_Expiry"]).ToString("dd/MM/yyyy");
            pnlExpired.Visible = _svc.IsMembershipExpired(card);

            // ── ส่วนลดค่าห้องของ tier ตัวเอง ──
            int tierId = Convert.ToInt32(card["CurrentTier_ID"]);
            decimal wd = 0, we = 0;
            var rules = _svc.GetRoomDiscountRules();
            if (rules != null)
                foreach (DataRow r in rules.Rows)
                    if (Convert.ToInt32(r["Tier_ID"]) == tierId)
                    { wd = Convert.ToDecimal(r["Weekday_Pct"]); we = Convert.ToDecimal(r["Weekend_Pct"]); }
            litWeekday.Text = wd > 0 ? $"ลด {wd:0.##}%" : "-";
            litWeekend.Text = we > 0 ? $"ลด {we:0.##}%" : "-";
            pnlDisc.Visible = wd > 0 || we > 0;

            // ── สิทธิ์อื่นของ tier ──
            var ben = _svc.GetTierBenefits(tierId);
            if (ben != null && ben.Rows.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (DataRow b in ben.Rows)
                {
                    sb.Append("<div class='mc-benefit'><i class='fas fa-check-circle'></i><div>");
                    sb.Append("<b>" + Server.HtmlEncode(b["BenefitName"].ToString()) + "</b>");
                    string d = b["Description"] == DBNull.Value ? "" : b["Description"].ToString();
                    if (!string.IsNullOrEmpty(d))
                        sb.Append("<div style='font-size:12px; color:#90a4ae;'>" + Server.HtmlEncode(d) + "</div>");
                    sb.Append("</div></div>");
                }
                litBenefits.Text = sb.ToString();
                pnlBenefits.Visible = true;
            }

            BindVouchers();
        }

        private void BindVouchers()
        {
            var dt = _svc.GetMemberVouchers(Phone);
            var list = new List<object>();
            if (dt != null)
            {
                foreach (DataRow r in dt.Rows)
                {
                    string status = r["Status"].ToString();
                    bool active = status == "ACTIVATED";
                    bool issued = status == "ISSUED";
                    bool used = status == "REDEEMED";
                    if (status == "EXPIRED" || status == "CANCELLED") continue;   // ไม่โชว์ให้รก

                    string windowText = "";
                    if (active && r["Activation_Expiry"] != DBNull.Value)
                    {
                        var mins = (int)Math.Max(0, (Convert.ToDateTime(r["Activation_Expiry"]) - DateTime.Now).TotalMinutes);
                        windowText = mins + " นาที";
                    }

                    list.Add(new
                    {
                        ID = Convert.ToInt64(r["ID"]),
                        Name = Server.HtmlEncode(r["Name"].ToString()),
                        Description = Server.HtmlEncode(r["Description"] == DBNull.Value ? "" : r["Description"].ToString()),
                        ExpiryText = Convert.ToDateTime(r["Expiry_Date"]).ToString("dd/MM/yyyy"),
                        Code = Server.HtmlEncode(r["Code"].ToString()),
                        ShowCode = active,
                        ShowUseButton = issued,
                        WindowText = windowText,
                        CssClass = used ? "used" : "",
                        BadgeText = used ? "ใช้แล้ว" : active ? "รอพนักงานแลก" : "พร้อมใช้",
                        BadgeCss = used ? "b-used" : active ? "b-active" : "b-issued"
                    });
                }
            }
            rptVouchers.DataSource = list;
            rptVouchers.DataBind();
            pnlNoVoucher.Visible = list.Count == 0;
        }

        protected void rptVouchers_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            if (e.CommandName != "activate") return;
            long id;
            if (!long.TryParse(e.CommandArgument?.ToString(), out id)) return;
            _svc.Activate(id, Phone);   // ผล/ข้อความสะท้อนผ่านสถานะบนการ์ดหลัง rebind
            BindAll();
        }

        protected void btnLogout_Click(object sender, EventArgs e)
        {
            Session["MemberPhone"] = null;
            Session["MemberMustSetPin"] = null;
            Response.Redirect("~/Member/Login", false);
            Context.ApplicationInstance.CompleteRequest();
        }

        private static string MaskPhone(string p)
        {
            if (string.IsNullOrEmpty(p) || p.Length < 6) return p ?? "";
            return p.Substring(0, 3) + "-xxx-" + p.Substring(p.Length - 4);
        }
    }
}
