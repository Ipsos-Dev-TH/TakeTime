using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Web.UI;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Admin.Settings
{
    /// <summary>
    /// หน้า Admin จัดการกิจกรรม: ตั้งค่ากิจกรรม (รูป/รายละเอียด/ราคา), กิจกรรมที่ต้องจองเวลา
    /// (โควตา/เวลาเปิด-ปิด/ความยาวช่วง) และอนุมัติการจอง/ตรวจสลิปโอนเงิน
    /// </summary>
    public partial class ActivityManagement : Page
    {
        private readonly string _conn =
            System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private ActivityService _svc;

        protected void Page_Load(object sender, EventArgs e)
        {
            _svc = new ActivityService(_conn);

            if (!IsOwnerOrAdmin())
            {
                Response.Redirect("~/Default");
                return;
            }

            if (!IsPostBack)
            {
                txtFrom.Text = DateTime.Today.ToString("yyyy-MM-dd");
                txtTo.Text = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd");
                BindActivities();
                UpdatePendingBadge();
            }
        }

        private bool IsOwnerOrAdmin()
        {
            try
            {
                return Session["permission"]?.ToString() == "True"
                       && (Session["User"]?.ToString() == "Owner" || Session["User"]?.ToString() == "Admin");
            }
            catch { return false; }
        }

        private short? CurrentAdminId()
        {
            if (Session["AdminID"] != null && short.TryParse(Session["AdminID"].ToString(), out var id)) return id;
            return null;
        }

        // ── แท็บ ──────────────────────────────────────────────────────────────────
        protected void ShowTab_Click(object sender, EventArgs e)
        {
            string tab = ((LinkButton)sender).CommandArgument;
            ShowTab(tab);
            if (tab == "edit" && hfActivityId.Value != "0") ClearForm();
        }

        private void ShowTab(string tab)
        {
            pnlList.Visible = tab == "list";
            pnlEdit.Visible = tab == "edit";
            pnlBookings.Visible = tab == "bookings";

            btnTabList.CssClass = "am-tab" + (tab == "list" ? " active" : "");
            btnTabEdit.CssClass = "am-tab" + (tab == "edit" ? " active" : "");
            btnTabBookings.CssClass = "am-tab" + (tab == "bookings" ? " active" : "");

            if (tab == "list") BindActivities();
            if (tab == "bookings") BindBookings();
        }

        private void BindActivities()
        {
            gvActivities.DataSource = _svc.GetAllActivities();
            gvActivities.DataBind();
        }

        private void UpdatePendingBadge()
        {
            try
            {
                int n = _svc.GetPendingCount();
                litPendingBadge.Text = n > 0
                    ? $"<span class='pill p-red' style='margin-left:6px;'>{n}</span>"
                    : "";
            }
            catch { litPendingBadge.Text = ""; }
        }

        // ── บันทึกกิจกรรม ─────────────────────────────────────────────────────────
        protected void btnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                ShowMessage("กรุณากรอกชื่อกิจกรรม", false);
                ShowTab("edit");
                return;
            }

            try
            {
                string imagePath = SaveUploadedImage();

                var f = new Dictionary<string, object>
                {
                    { "ActivityName", txtName.Text.Trim() },
                    { "ShortDescription", txtShortDesc.Text.Trim() },
                    { "Description", txtDescription.Text.Trim() },
                    { "Rules", txtRules.Text.Trim() },
                    { "Category", ddlCategory.SelectedValue },
                    { "ImagePath", imagePath ?? "" },
                    { "Price", txtPrice.Text.Trim() },
                    { "PricingMode", ddlPricingMode.SelectedValue },
                    { "IsBookable", chkBookable.Checked },
                    { "Capacity", txtCapacity.Text.Trim() },
                    { "OpenTime", txtOpenTime.Text.Trim() },
                    { "CloseTime", txtCloseTime.Text.Trim() },
                    { "SlotMinutes", txtSlotMinutes.Text.Trim() },
                    { "MaxSlotsPerBooking", txtMaxSlots.Text.Trim() },
                    { "AdvanceBookingDays", txtAdvanceDays.Text.Trim() },
                    { "MaxParticipants", txtMaxParticipants.Text.Trim() },
                    { "RequireApproval", chkRequireApproval.Checked },
                    { "ShowOnWebsite", chkShowWebsite.Checked },
                    { "ShowInPortal", chkShowPortal.Checked },
                    { "Duration", txtDuration.Text.Trim() },
                    { "Location", txtLocation.Text.Trim() },
                    { "ContactInfo", txtContact.Text.Trim() },
                    { "MapUrl", txtMapUrl.Text.Trim() },
                    { "IconClass", txtIcon.Text.Trim() },
                    { "DisplayOrder", txtOrder.Text.Trim() },
                    { "IsActive", chkActive.Checked }
                };

                int existingId = int.TryParse(hfActivityId.Value, out var eid) ? eid : 0;
                int savedId = _svc.SaveActivity(f, existingId > 0 ? existingId : (int?)null, CurrentAdminId());

                ShowMessage(existingId > 0
                    ? $"บันทึกการแก้ไข \"{txtName.Text.Trim()}\" แล้ว"
                    : $"เพิ่มกิจกรรม \"{txtName.Text.Trim()}\" แล้ว", true);

                ClearForm();
                ShowTab("list");
            }
            catch (Exception ex)
            {
                ShowMessage("บันทึกไม่สำเร็จ: " + ex.Message, false);
                ShowTab("edit");
            }
        }

        private string SaveUploadedImage()
        {
            if (!fuImage.HasFile) return null;
            try
            {
                string ext = Path.GetExtension(fuImage.FileName).ToLowerInvariant();
                var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                if (Array.IndexOf(allowed, ext) < 0)
                    throw new Exception("รองรับเฉพาะไฟล์รูปภาพ (jpg, png, gif, webp)");
                if (fuImage.PostedFile.ContentLength > 8 * 1024 * 1024)
                    throw new Exception("ไฟล์ใหญ่เกิน 8 MB");

                string folder = Server.MapPath("~/Images/Activities");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                string fileName = $"act_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 6)}{ext}";
                fuImage.SaveAs(Path.Combine(folder, fileName));
                return "/Images/Activities/" + fileName;
            }
            catch (Exception ex)
            {
                throw new Exception("อัปโหลดรูปไม่สำเร็จ: " + ex.Message);
            }
        }

        protected void btnCancelEdit_Click(object sender, EventArgs e)
        {
            ClearForm();
            ShowTab("list");
        }

        private void ClearForm()
        {
            hfActivityId.Value = "0";
            litEditTitle.Text = "เพิ่มกิจกรรมใหม่";
            txtName.Text = txtShortDesc.Text = txtDescription.Text = txtRules.Text = "";
            txtLocation.Text = txtContact.Text = txtMapUrl.Text = txtIcon.Text = txtDuration.Text = "";
            txtPrice.Text = "0";
            txtOrder.Text = "0";
            txtCapacity.Text = "1";
            txtOpenTime.Text = "08:00";
            txtCloseTime.Text = "21:00";
            txtSlotMinutes.Text = "60";
            txtMaxSlots.Text = "3";
            txtAdvanceDays.Text = "14";
            txtMaxParticipants.Text = "0";
            ddlCategory.SelectedValue = "ON_PROPERTY";
            ddlPricingMode.SelectedValue = "FREE";
            chkBookable.Checked = chkRequireApproval.Checked = false;
            chkShowWebsite.Checked = chkShowPortal.Checked = chkActive.Checked = true;
        }

        private void LoadForEdit(int activityId)
        {
            var r = _svc.GetActivity(activityId);
            if (r == null) { ShowMessage("ไม่พบกิจกรรมนี้", false); return; }

            hfActivityId.Value = activityId.ToString();
            litEditTitle.Text = "แก้ไข: " + r["ActivityName"];
            txtName.Text = S(r, "ActivityName");
            txtShortDesc.Text = S(r, "ShortDescription");
            txtDescription.Text = S(r, "Description");
            txtRules.Text = S(r, "Rules");
            txtLocation.Text = S(r, "Location");
            txtContact.Text = S(r, "ContactInfo");
            txtMapUrl.Text = S(r, "MapUrl");
            txtIcon.Text = S(r, "IconClass");
            txtDuration.Text = S(r, "Duration");
            txtPrice.Text = r["Price"] != DBNull.Value ? Convert.ToDecimal(r["Price"]).ToString("0.##") : "0";
            txtOrder.Text = S(r, "DisplayOrder", "0");
            txtCapacity.Text = S(r, "Capacity", "1");
            txtSlotMinutes.Text = S(r, "SlotMinutes", "60");
            txtMaxSlots.Text = S(r, "MaxSlotsPerBooking", "3");
            txtAdvanceDays.Text = S(r, "AdvanceBookingDays", "14");
            txtMaxParticipants.Text = S(r, "MaxParticipants", "0");
            txtOpenTime.Text = r["OpenTime"] != DBNull.Value ? ((TimeSpan)r["OpenTime"]).ToString(@"hh\:mm") : "08:00";
            txtCloseTime.Text = r["CloseTime"] != DBNull.Value ? ((TimeSpan)r["CloseTime"]).ToString(@"hh\:mm") : "21:00";

            SetDdl(ddlCategory, S(r, "Category", "ON_PROPERTY"));
            SetDdl(ddlPricingMode, S(r, "PricingMode", "FREE"));
            chkBookable.Checked = B(r, "IsBookable");
            chkRequireApproval.Checked = B(r, "RequireApproval");
            chkShowWebsite.Checked = B(r, "ShowOnWebsite");
            chkShowPortal.Checked = B(r, "ShowInPortal");
            chkActive.Checked = B(r, "IsActive");
        }

        protected void gvActivities_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (!int.TryParse(e.CommandArgument?.ToString(), out int id) || id <= 0) return;

            if (e.CommandName == "EditItem")
            {
                LoadForEdit(id);
                ShowTab("edit");
            }
            else if (e.CommandName == "DeleteItem")
            {
                try
                {
                    _svc.DeleteActivity(id);
                    ShowMessage("ลบ/ปิดการใช้งานกิจกรรมแล้ว", true);
                }
                catch (Exception ex) { ShowMessage("ลบไม่สำเร็จ: " + ex.Message, false); }
                ShowTab("list");
            }
        }

        // ── การจอง ───────────────────────────────────────────────────────────────
        protected void btnFilter_Click(object sender, EventArgs e) => ShowTab("bookings");

        private void BindBookings()
        {
            DateTime? from = DateTime.TryParse(txtFrom.Text, out var f) ? f : (DateTime?)null;
            DateTime? to = DateTime.TryParse(txtTo.Text, out var t) ? t : (DateTime?)null;
            string status = string.IsNullOrEmpty(ddlStatusFilter.SelectedValue) ? null : ddlStatusFilter.SelectedValue;

            gvBookings.DataSource = _svc.GetBookings(from, to, status);
            gvBookings.DataBind();
            UpdatePendingBadge();
        }

        protected void gvBookings_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (!long.TryParse(e.CommandArgument?.ToString(), out long id) || id <= 0) return;
            var admin = CurrentAdminId();

            try
            {
                switch (e.CommandName)
                {
                    case "ApproveItem":
                        {
                            var (ok, msg) = _svc.ReviewBooking(id, true, null, admin);
                            ShowMessage(msg, ok);
                            break;
                        }
                    case "RejectItem":
                        {
                            var (ok, msg) = _svc.ReviewBooking(id, false, "เจ้าหน้าที่ปฏิเสธการจอง", admin);
                            ShowMessage(msg, ok);
                            break;
                        }
                    case "MarkPaidItem":
                        _svc.MarkPaid(id, admin);
                        ShowMessage("บันทึกว่าชำระเงินแล้ว", true);
                        break;
                    case "CancelItem":
                        {
                            var (ok, msg) = _svc.CancelBooking(id, "ยกเลิกโดยเจ้าหน้าที่", admin);
                            ShowMessage(msg, ok);
                            break;
                        }
                }
            }
            catch (Exception ex) { ShowMessage("ดำเนินการไม่สำเร็จ: " + ex.Message, false); }

            ShowTab("bookings");
        }

        // ── formatters (ใช้จาก markup) ─────────────────────────────────────────────
        protected string FormatPrice(object item)
        {
            var r = (DataRowView)item;
            decimal price = r["Price"] != DBNull.Value ? Convert.ToDecimal(r["Price"]) : 0m;
            string mode = r["PricingMode"]?.ToString() ?? "FREE";
            if (price <= 0 || mode == "FREE") return "<span class='pill p-green'>ฟรี</span>";
            string suffix = mode == "PER_HOUR" ? " /ชม." : mode == "PER_PERSON" ? " /คน" : " /ครั้ง";
            return $"฿{price:N0}<span style='color:#8a9a90;font-size:12px;'>{suffix}</span>";
        }

        protected string FormatBookable(object item)
        {
            var r = (DataRowView)item;
            if (!ToBool(r["IsBookable"])) return "<span style='color:#b0bcb5;'>—</span>";
            string open = r["OpenTime"] != DBNull.Value ? ((TimeSpan)r["OpenTime"]).ToString(@"hh\:mm") : "-";
            string close = r["CloseTime"] != DBNull.Value ? ((TimeSpan)r["CloseTime"]).ToString(@"hh\:mm") : "-";
            int cap = r["Capacity"] != DBNull.Value ? Convert.ToInt32(r["Capacity"]) : 1;
            return $"<span class='pill p-blue'>จองเวลา</span><div style='font-size:12px;color:#8a9a90;'>" +
                   $"{open}-{close} · {cap} คิว</div>";
        }

        protected string FormatVisibility(object item)
        {
            var r = (DataRowView)item;
            var parts = new List<string>();
            if (ToBool(r["ShowOnWebsite"])) parts.Add("เว็บไซต์");
            if (ToBool(r["ShowInPortal"])) parts.Add("Portal");
            return parts.Count > 0
                ? string.Join(" · ", parts)
                : "<span style='color:#b0bcb5;'>ซ่อน</span>";
        }

        protected string FormatSlot(object item)
        {
            var r = (DataRowView)item;
            DateTime d = Convert.ToDateTime(r["BookingDate"]);
            var s = (TimeSpan)r["StartTime"];
            var e2 = (TimeSpan)r["EndTime"];
            return $"{d:dd/MM/yyyy}<div style='font-size:12px;color:#8a9a90;'>{s:hh\\:mm} - {e2:hh\\:mm} น.</div>";
        }

        protected string FormatGuestRef(object item)
        {
            var r = (DataRowView)item;
            var bits = new List<string>();
            if (r["Reservation_ID"] != DBNull.Value) bits.Add("จอง #" + r["Reservation_ID"]);
            if (r.Row.Table.Columns.Contains("Accommodation_Name") && r["Accommodation_Name"] != DBNull.Value)
                bits.Add(r["Accommodation_Name"].ToString());
            if (r["Customer_MobilePhone"] != DBNull.Value) bits.Add(r["Customer_MobilePhone"].ToString());
            return string.Join(" · ", bits);
        }

        protected string FormatPayment(object item)
        {
            var r = (DataRowView)item;
            string m = r["PaymentMethod"]?.ToString() ?? "NONE";
            string s = r["PaymentStatus"]?.ToString() ?? "UNPAID";

            string method = m == "ROOM_CHARGE" ? "ชาร์จเข้าห้อง"
                          : m == "TRANSFER" ? "โอนเงิน"
                          : m == "CASH" ? "เงินสด" : "—";
            string badge = s == "PAID" ? "<span class='pill p-green'>ชำระแล้ว</span>"
                         : s == "PENDING_VERIFY" ? "<span class='pill p-orange'>รอตรวจสลิป</span>"
                         : s == "WAIVED" ? "<span class='pill p-grey'>ไม่มีค่าใช้จ่าย</span>"
                         : "<span class='pill p-orange'>ยังไม่ชำระ</span>";
            return $"{badge}<div style='font-size:12px;color:#8a9a90;'>{method}</div>";
        }

        protected string FormatStatus(object item)
        {
            var r = (DataRowView)item;
            switch (r["Status"]?.ToString())
            {
                case "CONFIRMED": return "<span class='pill p-green'>ยืนยันแล้ว</span>";
                case "PENDING": return "<span class='pill p-orange'>รอดำเนินการ</span>";
                case "CANCELLED": return "<span class='pill p-grey'>ยกเลิก</span>";
                case "COMPLETED": return "<span class='pill p-blue'>ใช้บริการแล้ว</span>";
                case "NO_SHOW": return "<span class='pill p-red'>ไม่มาใช้บริการ</span>";
                default: return "";
            }
        }

        protected bool NeedsReview(object item)
        {
            var r = (DataRowView)item;
            return r["Status"]?.ToString() == "PENDING"
                   || r["PaymentStatus"]?.ToString() == "PENDING_VERIFY";
        }

        protected bool CanMarkPaid(object item)
        {
            var r = (DataRowView)item;
            decimal amt = Convert.ToDecimal(r["TotalAmount"]);
            string ps = r["PaymentStatus"]?.ToString();
            string st = r["Status"]?.ToString();
            // ชาร์จเข้าห้อง = ไปเก็บตอนเช็คเอาท์ ไม่ต้องกดรับเงินที่นี่
            return amt > 0 && ps != "PAID" && ps != "WAIVED"
                   && st != "CANCELLED" && r["PaymentMethod"]?.ToString() != "ROOM_CHARGE";
        }

        protected bool CanCancel(object item)
        {
            var r = (DataRowView)item;
            return r["Status"]?.ToString() != "CANCELLED" && r["PaymentStatus"]?.ToString() != "PAID";
        }

        protected bool HasSlip(object item)
        {
            var r = (DataRowView)item;
            return r["SlipFileURL"] != DBNull.Value && !string.IsNullOrEmpty(r["SlipFileURL"].ToString());
        }

        // ── helpers ───────────────────────────────────────────────────────────────
        private void ShowMessage(string msg, bool success)
        {
            pnlMessage.Visible = true;
            string color = success ? "#27ae60" : "#c0392b";
            string icon = success ? "fa-circle-check" : "fa-circle-exclamation";
            litMessage.Text = $"<div style='color:{color};font-weight:600;'>" +
                              $"<i class='fas {icon}'></i> {Server.HtmlEncode(msg)}</div>";
        }

        private static string S(DataRow r, string col, string def = "")
        {
            return r.Table.Columns.Contains(col) && r[col] != DBNull.Value ? r[col].ToString() : def;
        }

        private static bool B(DataRow r, string col)
        {
            if (!r.Table.Columns.Contains(col) || r[col] == DBNull.Value) return false;
            return ToBool(r[col]);
        }

        private static bool ToBool(object v)
        {
            if (v == null || v == DBNull.Value) return false;
            if (v is bool b) return b;
            string s = v.ToString();
            return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private static void SetDdl(DropDownList ddl, string value)
        {
            var item = ddl.Items.FindByValue(value);
            if (item != null) ddl.SelectedValue = value;
        }
    }
}
