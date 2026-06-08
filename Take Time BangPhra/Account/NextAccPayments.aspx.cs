using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Web.UI;
using Take_Time_BangPhra.Integration;

namespace Take_Time_BangPhra.Account
{
    public partial class NextAccPayments : System.Web.UI.Page
    {
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            // Admin-only (เหมือนหน้าบัญชีอื่น)
            try
            {
                if (!(Session["permission"] != null && Session["permission"].ToString() == "True"
                    && (Session["User"].ToString() == "Owner" || Session["User"].ToString() == "Admin")))
                {
                    Response.Redirect("/Default");
                    return;
                }
            }
            catch
            {
                Response.Redirect("/Default");
                return;
            }

            if (!IsPostBack)
            {
                var now = DateTime.Today;
                txtFrom.Text = new DateTime(now.Year, now.Month, 1).ToString("yyyy-MM-dd");
                txtTo.Text = now.ToString("yyyy-MM-dd");
            }
        }

        protected void btnFetch_Click(object sender, EventArgs e)
        {
            DateTime from, to;
            if (!DateTime.TryParse(txtFrom.Text, CultureInfo.InvariantCulture, DateTimeStyles.None, out from))
                from = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            if (!DateTime.TryParse(txtTo.Text, CultureInfo.InvariantCulture, DateTimeStyles.None, out to))
                to = DateTime.Today;

            try
            {
                var config = new AccountingConfig(conn);
                if (!config.IsConfigured || !config.Enabled)
                {
                    lblStatus.Text = "⚠️ ยังไม่ได้ตั้งค่าการเชื่อมต่อ NextAcc (ตรวจสอบที่หน้า Accounting Integration)";
                    BindDocs(new List<NextAccPaymentDoc>());
                    return;
                }

                var sync = new AccountingSyncService(conn);
                // WebForms: รัน async แบบ blocking เหมือน ProcessQueueAsync ในหน้าตั้งค่า
                var docs = System.Threading.Tasks.Task.Run(() =>
                    sync.FetchNextAccPaymentDocumentsAsync(from, to.Date.AddDays(1).AddSeconds(-1), true)).Result;

                int attCount = 0;
                foreach (var d in docs) attCount += d.Attachments != null ? d.Attachments.Count : 0;

                lblStatus.Text = $"✅ พบเอกสารฝั่งจ่าย {docs.Count} รายการ (ไฟล์แนบ {attCount} ไฟล์) ช่วง {from:dd/MM/yyyy} - {to:dd/MM/yyyy}";
                BindDocs(docs);
            }
            catch (Exception ex)
            {
                lblStatus.Text = "❌ ดึงข้อมูลไม่สำเร็จ: " + ex.Message;
                BindDocs(new List<NextAccPaymentDoc>());
            }
        }

        private void BindDocs(List<NextAccPaymentDoc> docs)
        {
            rptDocs.DataSource = docs;
            rptDocs.DataBind();
            phEmpty.Visible = docs.Count == 0;
        }
    }
}
