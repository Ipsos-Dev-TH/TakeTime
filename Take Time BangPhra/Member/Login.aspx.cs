using System;
using System.Configuration;
using System.Web.UI;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Member
{
    /// <summary>
    /// ล็อกอินสมาชิก — เบอร์โทร + PIN (ครั้งแรกใช้เลขท้ายเบอร์ 4 ตัว แล้วบังคับตั้ง PIN ใหม่)
    /// สำเร็จ → Session["MemberPhone"] → หน้าบัตร (Member/Card)
    /// </summary>
    public partial class MemberLogin : Page
    {
        private readonly string _conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private MemberPortalService _svc;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Feature.Guard(this, "Loyalty", "~/Default")) return;
            _svc = new MemberPortalService(_conn);

            if (!IsPostBack && Session["MemberPhone"] != null && Session["MemberMustSetPin"] == null)
            {
                Response.Redirect("~/Member/Card", false);
                Context.ApplicationInstance.CompleteRequest();
            }
        }

        protected void btnLogin_Click(object sender, EventArgs e)
        {
            var r = _svc.Login(txtPhone.Text, txtPin.Text);
            txtPin.Text = "";
            if (!r.Success) { Err(r.Error); return; }

            Session["MemberPhone"] = r.Phone;
            if (r.MustSetPin)
            {
                Session["MemberMustSetPin"] = "1";
                pnlLogin.Visible = false;
                pnlSetPin.Visible = true;
                pnlErr.Visible = false;
            }
            else
            {
                Session["MemberMustSetPin"] = null;
                Response.Redirect("~/Member/Card", false);
                Context.ApplicationInstance.CompleteRequest();
            }
        }

        protected void btnSetPin_Click(object sender, EventArgs e)
        {
            string phone = Session["MemberPhone"]?.ToString();
            if (string.IsNullOrEmpty(phone)) { Response.Redirect("~/Member/Login"); return; }

            if ((txtNewPin.Text ?? "") != (txtNewPin2.Text ?? ""))
            { pnlSetPin.Visible = true; pnlLogin.Visible = false; Err("รหัส PIN ทั้งสองช่องไม่ตรงกัน"); return; }

            var (ok, msg) = _svc.SetPin(phone, txtNewPin.Text);
            if (!ok) { pnlSetPin.Visible = true; pnlLogin.Visible = false; Err(msg); return; }

            Session["MemberMustSetPin"] = null;
            Response.Redirect("~/Member/Card", false);
            Context.ApplicationInstance.CompleteRequest();
        }

        private void Err(string msg)
        {
            pnlErr.Visible = true;
            litErr.Text = Server.HtmlEncode(msg ?? "เกิดข้อผิดพลาด");
        }
    }
}
