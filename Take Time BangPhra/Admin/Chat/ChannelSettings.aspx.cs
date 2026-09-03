using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Web.Script.Serialization;
using System.Web.UI;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Admin.Chat
{
    public partial class ChannelSettings : Page
    {
        private string ConnStr => ConfigurationManager.ConnectionStrings["TaketimeConnectionString"]?.ConnectionString ?? "";
        private readonly code _code = new code();

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.SysChannel)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            if (!Feature.Guard(this, "Chat", "~/Default")) return;   // ฟีเจอร์ถูกปิด (ตั้งค่าระบบ → หมวดฟีเจอร์)
            if (Session["permission"]?.ToString() != "True" ||
                (Session["User"]?.ToString() != "Owner" && Session["User"]?.ToString() != "Admin"))
            {
                Response.Redirect("~/Admin/Login");
                return;
            }

            if (Request.HttpMethod == "POST" && Request.ContentType?.Contains("application/json") == true)
            {
                HandlePost();
                return;
            }

            if (!IsPostBack)
                LoadChannels();
        }

        private void LoadChannels()
        {
            try
            {
                var svc = new OmniChannelService(ConnStr);
                DataTable dt = svc.GetChannels();
                var serializer = new JavaScriptSerializer();
                var list = new List<object>();

                foreach (DataRow row in (dt?.Rows ?? new DataTable().Rows))
                {
                    Dictionary<string, object> config = new Dictionary<string, object>();
                    if (row["Config"] != DBNull.Value && !string.IsNullOrEmpty(row["Config"].ToString()))
                    {
                        try { config = serializer.Deserialize<Dictionary<string, object>>(row["Config"].ToString()); }
                        catch { }
                    }

                    list.Add(new
                    {
                        id = Convert.ToInt32(row["ID"]),
                        code = row["ChannelCode"].ToString(),
                        name = row["ChannelName"].ToString(),
                        type = row["ChannelType"].ToString(),
                        icon = row["IconClass"].ToString(),
                        color = row["BrandColor"].ToString(),
                        enabled = Convert.ToBoolean(row["IsEnabled"]),
                        config = config
                    });
                }

                hfChannels.Value = serializer.Serialize(list);
                // ตารางมีอยู่แต่ยังไม่มีข้อมูล (migration สร้างตารางแต่ seed ไม่ผ่าน) — บอกให้กดสร้างได้
                SetLoadError(list.Count == 0
                    ? "ตาราง OmniChannel_Channels ยังไม่มีช่องทางใด ๆ — กด \"สร้างช่องทางเริ่มต้น\" ด้านล่าง"
                    : "");
            }
            catch (Exception ex)
            {
                // ⚠️ เดิม catch เปล่า ๆ แล้วตั้ง "[]" → หน้าจอขึ้นแต่หัวเรื่องกับ Webhook URL
                // ไม่มีอะไรให้ตั้งค่า และไม่บอกสาเหตุ (เช่นยังไม่ได้รัน migration) — ต้องแสดงจริง
                if (hfChannels != null) hfChannels.Value = "[]";
                SetLoadError("อ่านรายการช่องทางไม่สำเร็จ: " + (ex.InnerException ?? ex).Message);
            }
        }

        /// <summary>
        /// ส่งข้อความสาเหตุไปให้หน้าเว็บผ่านตัวแปร JS ไม่ผ่าน server control
        /// — control ใหม่จะเป็น null ถ้า .aspx บนเซิร์ฟเวอร์ยังเป็นไฟล์เก่า (deploy DLL อย่างเดียว)
        ///   แล้ว handler ที่ควรอธิบาย error จะพังเสียเอง กลายเป็น NullReferenceException
        /// </summary>
        private void SetLoadError(string message)
        {
            try
            {
                ClientScript.RegisterStartupScript(GetType(), "chLoadErr",
                    "window.__channelLoadError = " + new JavaScriptSerializer().Serialize(message ?? "") + ";", true);
            }
            catch { }
        }

        /// <summary>
        /// สร้างแถวช่องทางมาตรฐานให้ครบ (idempotent — ข้ามตัวที่มีแล้ว)
        /// ใช้กรณีตารางว่างเพราะ seed ของ migration ไม่ผ่าน หรือมีช่องทางใหม่เพิ่มภายหลัง
        /// </summary>
        private Dictionary<string, object> SeedChannels()
        {
            var rows = new[]
            {
                new[]{ "GUEST_PORTAL", "Guest Portal",          "INTERNAL", "fas fa-hotel",                "#667eea" },
                new[]{ "WEBCHAT",      "แชทหน้าเว็บ",             "WEB",      "fas fa-comments",             "#8D9F7F" },
                new[]{ "LINE",         "LINE Official Account",  "SOCIAL",   "fab fa-line",                 "#06C755" },
                new[]{ "FACEBOOK",     "Facebook Messenger",     "SOCIAL",   "fab fa-facebook-messenger",   "#0084FF" },
                new[]{ "INSTAGRAM",    "Instagram DM",           "SOCIAL",   "fab fa-instagram",            "#E4405F" },
                new[]{ "WHATSAPP",     "WhatsApp Business",      "SOCIAL",   "fab fa-whatsapp",             "#25D366" },
                new[]{ "TIKTOK",       "TikTok",                 "SOCIAL",   "fab fa-tiktok",               "#000000" },
                new[]{ "TELEGRAM",     "Telegram",               "SOCIAL",   "fab fa-telegram",             "#0088CC" },
                new[]{ "AGODA",        "Agoda",                  "OTA",      "fas fa-bed",                  "#5542F6" },
                new[]{ "BOOKING",      "Booking.com",            "OTA",      "fas fa-globe",                "#003580" },
                new[]{ "TRIP",         "Trip.com",               "OTA",      "fas fa-plane",                "#287DFA" },
                new[]{ "EXPEDIA",      "Expedia",                "OTA",      "fas fa-suitcase",             "#FBAF17" },
                new[]{ "EMAIL",        "Email (แชท OTA)",         "EMAIL",    "fas fa-envelope",             "#EA4335" },
                new[]{ "SMS",          "SMS",                    "SMS",      "fas fa-sms",                  "#4CAF50" },
            };
            try
            {
                int added = 0;
                foreach (var r in rows)
                {
                    int n = _code.DatabaseInsertSafe(ConnStr,
                        @"IF NOT EXISTS (SELECT 1 FROM OmniChannel_Channels WHERE ChannelCode = @c)
                          INSERT INTO OmniChannel_Channels
                                (ChannelCode, ChannelName, ChannelType, IconClass, BrandColor, IsEnabled)
                          VALUES (@c, @n, @t, @i, @b, 0)",
                        new Dictionary<string, object>
                        { { "@c", r[0] }, { "@n", r[1] }, { "@t", r[2] }, { "@i", r[3] }, { "@b", r[4] } });
                    if (n > 0) added++;
                }
                return new Dictionary<string, object>
                { { "success", true }, { "message", $"สร้างช่องทางเพิ่ม {added} รายการ — รีเฟรชหน้านี้" } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object>
                {
                    { "success", false },
                    { "message", "สร้างไม่สำเร็จ: " + (ex.InnerException ?? ex).Message
                                 + " — ถ้าเป็น \"Invalid object name\" แปลว่ายังไม่ได้รัน "
                                 + "Database/PHASE15_Migration_01_OmniChannel.sql" }
                };
            }
        }

        private void HandlePost()
        {
            string body;
            using (var reader = new StreamReader(Request.InputStream))
                body = reader.ReadToEnd();

            string action = Request.QueryString["action"] ?? "";
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(body ?? "{}");
            Dictionary<string, object> result;

            switch (action)
            {
                case "toggle":
                    result = ToggleChannel(data);
                    break;
                case "saveConfig":
                    result = SaveConfig(data);
                    break;
                case "emailChatCheck":
                    result = EmailChatSelfCheck();
                    break;
                case "seedChannels":
                    result = SeedChannels();
                    break;
                case "emailChatPoll":
                    result = EmailChatPollNow();
                    break;
                default:
                    result = new Dictionary<string, object> { { "success", false }, { "message", "Unknown" } };
                    break;
            }

            Response.Clear();
            Response.ContentType = "application/json";
            Response.Write(new JavaScriptSerializer().Serialize(result));
            Response.End();
        }

        /// <summary>ดึงอีเมลแชท OTA เดี๋ยวนี้ (ไม่ต้องรอรอบ timer) — ใช้ตอนทดสอบ</summary>
        private Dictionary<string, object> EmailChatPollNow()
        {
            try
            {
                var svc = new Take_Time_BangPhra.Services.EmailChatService(ConnStr);
                var r = System.Threading.Tasks.Task.Run(() => svc.PollInbox()).Result;
                if (!string.IsNullOrEmpty(r.Error))
                    return new Dictionary<string, object> { { "success", false }, { "message", r.Error } };
                string msg = $"ดึง {r.Fetched} ฉบับ → เข้าแชท {r.Received}, ซ้ำ {r.Duplicate}, ล้มเหลว {r.Failed}";
                if (r.Fetched == 0)
                    msg += "\n(ค้นเฉพาะอีเมล \"ยังไม่อ่าน\" จากโดเมนที่ตั้งไว้ — ถ้าทดสอบกับเมลเก่า ให้ mark unread ใน Gmail ก่อน)";
                if (r.Messages != null && r.Messages.Count > 0)
                    msg += "\n" + string.Join("\n", r.Messages);
                return new Dictionary<string, object> { { "success", true }, { "message", msg } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object>
                { { "success", false }, { "message", "ดึงไม่สำเร็จ: " + (ex.InnerException ?? ex).Message } };
            }
        }

        /// <summary>ตรวจว่าแชท OTA (อีเมล) พร้อมใช้งานหรือยัง — ไล่ทีละเงื่อนไข ไม่แตะอีเมล</summary>
        private Dictionary<string, object> EmailChatSelfCheck()
        {
            try
            {
                var svc = new Take_Time_BangPhra.Services.EmailChatService(ConnStr);
                string report = System.Threading.Tasks.Task.Run(() => svc.SelfCheck()).Result;
                return new Dictionary<string, object>
                {
                    { "success", !report.Contains("⚠️ ยังไม่พร้อม") },
                    { "message", report }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object>
                { { "success", false }, { "message", "ตรวจสอบไม่สำเร็จ: " + (ex.InnerException ?? ex).Message } };
            }
        }

        private Dictionary<string, object> ToggleChannel(Dictionary<string, object> data)
        {
            try
            {
                int id = Convert.ToInt32(data["id"]);
                bool enabled = Convert.ToBoolean(data["enabled"]);
                var svc = new OmniChannelService(ConnStr);
                svc.UpdateChannel(id, enabled, null);
                return new Dictionary<string, object> { { "success", true } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> SaveConfig(Dictionary<string, object> data)
        {
            try
            {
                string code = data["code"]?.ToString();
                var config = data.ContainsKey("config") ? data["config"] as Dictionary<string, object> : new Dictionary<string, object>();
                var svc = new OmniChannelService(ConnStr);
                svc.SetChannelConfig(code, config);
                return new Dictionary<string, object> { { "success", true } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }
    }
}
