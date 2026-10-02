using System;
using System.Data;
using System.Web.UI;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Admin.RoomService
{
    public partial class OrderSettings : Page
    {
        private readonly string _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private GuestPortalService _service;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.SvcGuest)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            if (!Feature.Guard(this, "RoomService", "~/Default")) return;   // ฟีเจอร์ถูกปิด (ตั้งค่าระบบ → หมวดฟีเจอร์)
            // Admin login check (เหมือนหน้า OrderManagement)
            // ตรวจสิทธิ์แบบเดียวกับหน้าผู้ดูแลอื่น ๆ (เดิมเช็คแค่ว่ามีชื่อผู้ใช้ใน session
            // ไม่ได้เช็คสิทธิ์จริง และใช้คีย์คนละตัวกับทั้งระบบ)
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("~/Admin/Login");
                return;
            }

            _service = new GuestPortalService(_connectionString);

            if (!IsPostBack)
            {
                LoadSettings();
            }
        }

        private void LoadSettings()
        {
            DataRow s = _service.GetRoomServiceSettings();
            if (s == null)
            {
                // ยังไม่ได้รัน migration — แสดงค่าเริ่มต้น
                chkEnabled.Checked = true;
                ddlMode.SelectedValue = "AUTO";
                txtOpenTime.Text = "08:00";
                txtCloseTime.Text = "20:00";
                txtClosedMessage.Text = "ขณะนี้อยู่นอกเวลาให้บริการสั่งอาหาร กรุณาสั่งในเวลาทำการ";
            }
            else
            {
                chkEnabled.Checked = s["Is_Enabled"] != DBNull.Value && Convert.ToBoolean(s["Is_Enabled"]);
                string mode = (s["Manual_Mode"]?.ToString() ?? "AUTO").Trim().ToUpper();
                if (ddlMode.Items.FindByValue(mode) != null) ddlMode.SelectedValue = mode;
                txtOpenTime.Text = FormatTime(s["Open_Time"], "08:00");
                txtCloseTime.Text = FormatTime(s["Close_Time"], "20:00");
                txtClosedMessage.Text = s["Closed_Message"] == DBNull.Value ? "" : s["Closed_Message"].ToString();
            }

            LoadServiceChargeSettings();
            RefreshStatusBadge();
        }

        /// <summary>โหลดการตั้งค่าค่าบริการ (PHASE18_21) — ไม่มีคอลัมน์ = แสดงค่าเริ่มต้น "ไม่คิด"</summary>
        private void LoadServiceChargeSettings()
        {
            var svc = _service.GetServiceChargeSetting();
            if (ddlServiceChargeMode.Items.FindByValue(svc.Mode) != null)
                ddlServiceChargeMode.SelectedValue = svc.Mode;
            txtServiceChargeValue.Text = svc.Value > 0m ? svc.Value.ToString("0.##") : "";
            txtServiceChargeMax.Text = svc.MaxAmount > 0m ? svc.MaxAmount.ToString("0.##") : "";
            txtServiceChargeLabel.Text = svc.Label ?? "";
        }

        private static string FormatTime(object value, string fallback)
        {
            if (value == null || value == DBNull.Value) return fallback;
            if (value is TimeSpan ts) return ts.ToString(@"hh\:mm");
            TimeSpan parsed;
            return TimeSpan.TryParse(value.ToString(), out parsed) ? parsed.ToString(@"hh\:mm") : fallback;
        }

        private void RefreshStatusBadge()
        {
            string msg;
            bool open = _service.IsRoomServiceOpen(out msg);
            if (open)
            {
                lblCurrentStatus.Text = "🟢 เปิดรับออเดอร์";
                lblCurrentStatus.CssClass = "rs-status rs-open";
            }
            else
            {
                lblCurrentStatus.Text = "🔴 ปิดรับออเดอร์";
                lblCurrentStatus.CssClass = "rs-status rs-closed";
            }
        }

        protected void btnSave_Click(object sender, EventArgs e)
        {
            try
            {
                string updatedBy = Session["username"]?.ToString() ?? "ADMIN";
                bool ok = _service.SaveRoomServiceSettings(
                    chkEnabled.Checked,
                    ddlMode.SelectedValue,
                    string.IsNullOrWhiteSpace(txtOpenTime.Text) ? "08:00" : txtOpenTime.Text.Trim(),
                    string.IsNullOrWhiteSpace(txtCloseTime.Text) ? "20:00" : txtCloseTime.Text.Trim(),
                    txtClosedMessage.Text.Trim(),
                    updatedBy);

                // ค่าบริการ — เก็บแยก (คอลัมน์จาก PHASE18_21)
                decimal svcValue, svcMax;
                decimal.TryParse((txtServiceChargeValue.Text ?? "").Trim(), out svcValue);
                decimal.TryParse((txtServiceChargeMax.Text ?? "").Trim(), out svcMax);
                bool okSvc = _service.SaveServiceChargeSettings(
                    ddlServiceChargeMode.SelectedValue, svcValue, svcMax, txtServiceChargeLabel.Text);

                lblSaved.Visible = true;
                if (ok && !okSvc)
                    lblSaved.Text = "✔ บันทึกเวลาทำการแล้ว — แต่บันทึกค่าบริการไม่สำเร็จ (ต้องรันสคริปต์ PHASE18_21 ก่อน)";
                else
                    lblSaved.Text = ok ? "✔ บันทึกแล้ว" : "⚠ บันทึกไม่สำเร็จ (ตรวจสอบว่ารันสคริปต์ PHASE13 แล้ว)";

                // โหลดค่ากลับ + อัปเดตป้ายสถานะตามค่าใหม่
                LoadSettings();
            }
            catch (Exception ex)
            {
                lblSaved.Visible = true;
                lblSaved.Text = "⚠ ผิดพลาด: " + ex.Message;
            }
        }
    }
}
