using System;
using System.IO;
using System.Text;
using System.Web;
using Take_Time_BangPhra.Payments;

namespace Take_Time_BangPhra.API
{
    /// <summary>
    /// ปลายทางที่เกตเวย์รับชำระเงินเรียกกลับมาบอกผลการจ่าย
    ///
    /// ที่อยู่ที่ต้องนำไปใส่ในระบบของเกตเวย์:
    ///   https://&lt;โดเมนของเว็บ&gt;/API/PaymentWebhook.ashx
    /// (ดูค่าจริงได้ในหน้า ศูนย์ตั้งค่า → รับชำระเงินออนไลน์)
    ///
    /// กฎที่ยึด:
    ///   • ตรวจลายเซ็นก่อนเสมอ — ไม่ผ่าน = 401 ไม่แตะข้อมูลใด ๆ
    ///   • เก็บของดิบทุกครั้งก่อนตัดสินใจ (ตาราง Payment_Transaction_Event)
    ///   • ส่งซ้ำมาก็ไม่ทำงานซ้ำ (unique index บน Provider + Event_ID)
    ///   • ตอบ 200 เมื่อรับไว้แล้ว เพื่อไม่ให้เกตเวย์ยิงซ้ำไม่หยุด
    ///   • ฟีเจอร์ปิดอยู่ = ตอบ 503 เฉย ๆ ไม่มีผลกับระบบเดิม
    /// </summary>
    public class PaymentWebhook : IHttpHandler
    {
        public void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "application/json; charset=utf-8";

            if (context.Request.HttpMethod == "GET")
            {
                // ให้เกตเวย์/ผู้ดูแลตรวจได้ว่าปลายทางนี้มีอยู่จริง — ไม่เปิดเผยค่าตั้งใด ๆ
                context.Response.StatusCode = 200;
                context.Response.Write("{\"ok\":true,\"endpoint\":\"payment-webhook\"}");
                return;
            }

            string body = "";
            try
            {
                context.Request.InputStream.Position = 0;
                using (var rd = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                    body = rd.ReadToEnd();
            }
            catch { }

            try
            {
                var svc = new OnlinePaymentService();
                OnlinePaymentService.WebhookOutcome r =
                    svc.HandleWebhook(context.Request.Headers, body, ClientIp(context));

                context.Response.StatusCode = r.HttpStatus;
                context.Response.Write("{\"ok\":" + (r.Accepted ? "true" : "false")
                    + ",\"message\":" + Escape(r.Message) + "}");
            }
            catch (Exception ex)
            {
                // อย่าเปิดเผยรายละเอียดภายในให้ผู้เรียกภายนอก — บันทึกไว้ฝั่งเราแทน
                try
                {
                    new code().Logs(
                        System.Configuration.ConfigurationManager
                            .ConnectionStrings["TaketimeConnectionString"].ConnectionString,
                        "OnlinePayment", "Webhook error: " + ex, "System");
                }
                catch { }

                context.Response.StatusCode = 500;
                context.Response.Write("{\"ok\":false,\"message\":\"internal error\"}");
            }
        }

        private static string ClientIp(HttpContext ctx)
        {
            try
            {
                string fwd = ctx.Request.Headers["X-Forwarded-For"];
                if (!string.IsNullOrEmpty(fwd))
                {
                    int comma = fwd.IndexOf(',');
                    return (comma > 0 ? fwd.Substring(0, comma) : fwd).Trim();
                }
                return ctx.Request.UserHostAddress;
            }
            catch { return null; }
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "\"\"";
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\r') sb.Append("\\r");
                else if (c < ' ') sb.Append(' ');
                else sb.Append(c);
            }
            return sb.Append('"').ToString();
        }

        public bool IsReusable { get { return false; } }
    }
}
