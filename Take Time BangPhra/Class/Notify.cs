using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;

/// <summary>
/// ประตูเดียวของการแจ้งเตือนออกนอกระบบ (Telegram / LINE)
///
/// ทำไมต้องมี: เดิมทุกหน้าที่อยากแจ้งเตือนจะ new TelegramBot2(...) แล้วยิงตรงไป
/// api.telegram.org พร้อม chat id ที่ hard-code ไว้ในโค้ด ⇒
///   • ปิดเฉพาะบางเรื่องไม่ได้ (เช่น ไม่อยากได้ทุกออเดอร์ แต่อยากได้เฉพาะการจอง)
///   • ย้ายกลุ่มปลายทางต้องแก้โค้ดแล้ว build ใหม่
///   • นับไม่ได้ว่าส่งอะไรไปบ้าง หาไม่เจอว่าส่งพลาดเพราะอะไร
///   • ทุกครั้งสร้าง HttpClient ใหม่โดยไม่ dispose (เปลือง socket)
///
/// ตอนนี้ทุกจุดเรียก <c>Notify.Send(Notify.Ev.BookingNew, ข้อความ)</c> แล้วคลาสนี้
/// ตัดสินเองว่าจะส่งช่องทางไหน ไปที่ไหน หรือไม่ส่งเลย ตามตาราง Notification_Rules
///
/// วางที่ global namespace เช่นเดียวกับ AppCfg / Feature / Perm เพื่อให้เรียกได้ทุกไฟล์
/// </summary>
public static class Notify
{
    // ── รหัสเหตุการณ์ ─────────────────────────────────────────────────────────
    public static class Ev
    {
        public const string BookingNew = "BOOKING_NEW";
        public const string BookingEdit = "BOOKING_EDIT";
        public const string BookingPostpone = "BOOKING_POSTPONE";
        public const string BookingCancel = "BOOKING_CANCEL";

        public const string OtaBookingOk = "OTA_BOOKING_OK";
        public const string OtaBookingFail = "OTA_BOOKING_FAIL";
        public const string OtaSummary = "OTA_SUMMARY";
        public const string OtaIntakeStale = "OTA_INTAKE_STALE";

        public const string ChatGuest = "CHAT_GUEST";
        public const string ChatPublic = "CHAT_PUBLIC";
        public const string ChatOtaEmail = "CHAT_OTA_EMAIL";

        public const string OrderRoomService = "ORDER_ROOMSERVICE";
        public const string OrderAmenity = "ORDER_AMENITY";
        public const string OrderActivity = "ORDER_ACTIVITY";

        public const string PaymentOnline = "PAYMENT_ONLINE";
        public const string PaymentHold = "PAYMENT_HOLD";
        public const string EtaxRd = "ETAX_RD";

        public const string AccQueueAlert = "ACC_QUEUE_ALERT";
        public const string SystemError = "SYSTEM_ERROR";
    }

    public const string ChannelTelegram = "TELEGRAM";
    public const string ChannelLine = "LINE";

    /// <summary>คำอธิบายเหตุการณ์หนึ่งรายการ — ใช้วาดหน้าตั้งค่าและอธิบายให้คนอ่านเข้าใจ</summary>
    public class EventInfo
    {
        public string Code, Name, Category, Note, Source;
        /// <summary>เรื่องด่วน — ส่งได้แม้อยู่ในช่วงเวลาเงียบ (ถ้าเปิดข้อยกเว้นไว้)</summary>
        public bool Urgent;

        public EventInfo(string code, string category, string name, string note, string source, bool urgent = false)
        { Code = code; Category = category; Name = name; Note = note; Source = source; Urgent = urgent; }
    }

    /// <summary>เหตุการณ์ทั้งหมดที่ระบบแจ้งเตือนได้ — เรียงตามลำดับที่อยากให้เห็นในหน้าตั้งค่า</summary>
    public static readonly List<EventInfo> Catalog = new List<EventInfo>
    {
        new EventInfo(Ev.BookingNew,      "การจอง", "ลงจองใหม่",
            "พนักงานลงจอง หรือลูกค้าจองผ่านหน้าเว็บ", "หน้าจอง"),
        new EventInfo(Ev.BookingEdit,     "การจอง", "แก้ไขการจอง",
            "มีการเปลี่ยนวันที่ / ห้อง / ราคา ของใบจองเดิม", "หน้าจอง"),
        new EventInfo(Ev.BookingPostpone, "การจอง", "เลื่อนเข้าพัก",
            "ลูกค้าเลื่อนวันเข้าพักโดยยังไม่กำหนดวันใหม่", "หน้าจอง"),
        new EventInfo(Ev.BookingCancel,   "การจอง", "ยกเลิกการจอง",
            "ยกเลิกจากตารางผู้เข้าพักรายวัน", "ตารางรายวัน"),

        new EventInfo(Ev.OtaBookingOk,    "การจองจาก OTA (อีเมล)", "ลงจองจากอีเมลสำเร็จ",
            "อ่านอีเมล Agoda/Booking แล้วลงจองในระบบได้", "อ่านอีเมลจอง"),
        new EventInfo(Ev.OtaBookingFail,  "การจองจาก OTA (อีเมล)", "ลงจองจากอีเมลไม่สำเร็จ",
            "ห้องไม่ว่าง / ไม่มี mapping / ต้องให้คนตรวจเอง — ควรเปิดไว้เสมอ", "อ่านอีเมลจอง", true),
        new EventInfo(Ev.OtaSummary,      "การจองจาก OTA (อีเมล)", "สรุปผลแต่ละรอบ",
            "สรุปว่ารอบนี้อ่านกี่ฉบับ ลงจองกี่ใบ — ปิดได้ถ้ารำคาญ", "อ่านอีเมลจอง"),
        new EventInfo(Ev.OtaIntakeStale,  "การจองจาก OTA (อีเมล)", "ไม่ได้รับอีเมลนานผิดปกติ",
            "เตือนว่าอาจมีปัญหาการเชื่อมต่ออีเมล — ควรเปิดไว้เสมอ", "อ่านอีเมลจอง", true),

        new EventInfo(Ev.ChatGuest,       "ข้อความจากลูกค้า", "ข้อความจากแขกในที่พัก",
            "แขกที่เช็คอินแล้วทักผ่าน Guest Portal", "Guest Portal"),
        new EventInfo(Ev.ChatPublic,      "ข้อความจากลูกค้า", "ข้อความจากหน้าเว็บสาธารณะ",
            "คนทั่วไปทักผ่านกล่องแชทหน้าเว็บ", "เว็บไซต์"),
        new EventInfo(Ev.ChatOtaEmail,    "ข้อความจากลูกค้า", "อีเมลลูกค้า OTA",
            "อีเมลจากลูกค้าผ่าน relay ของ OTA เข้ากล่องแชทรวม", "กล่องแชทรวม"),

        new EventInfo(Ev.OrderRoomService, "บริการในที่พัก", "ออเดอร์รูมเซอร์วิสใหม่",
            "แขกสั่งอาหาร/เครื่องดื่มจากในห้อง", "Guest Portal"),
        new EventInfo(Ev.OrderAmenity,     "บริการในที่พัก", "คำขอเบิกของใช้",
            "แขกกดเบิกผ้าเช็ดตัว น้ำดื่ม ฯลฯ", "Guest Portal"),
        new EventInfo(Ev.OrderActivity,    "บริการในที่พัก", "จองกิจกรรมใหม่",
            "แขกจองรอบกิจกรรมในที่พัก", "Guest Portal"),

        new EventInfo(Ev.PaymentOnline,   "เงินและเอกสาร", "รับชำระเงินออนไลน์สำเร็จ",
            "ลูกค้าจ่ายผ่านบัตร/QR ตัดยอดอัตโนมัติสำเร็จ", "ระบบชำระเงิน"),
        new EventInfo(Ev.PaymentHold,     "เงินและเอกสาร", "วงเงินประกันความเสียหาย",
            "กันวงเงิน / ตัดค่าเสียหาย / คืนวงเงิน / ใกล้หมดอายุ (7 วัน) — ควรเปิดไว้เสมอ", "ระบบชำระเงิน", true),
        new EventInfo(Ev.EtaxRd,          "เงินและเอกสาร", "ผลตอบกลับ e-Tax จากสรรพากร",
            "กรมสรรพากรตอบรับ/ปฏิเสธใบกำกับอิเล็กทรอนิกส์", "e-Tax"),

        new EventInfo(Ev.AccQueueAlert,   "ระบบ", "คิวส่งบัญชีมีปัญหา",
            "รายการค้างคิว/ล้มเหลวสะสม — ควรเปิดไว้เสมอ", "NextAcc", true),
        new EventInfo(Ev.SystemError,     "ระบบ", "ข้อผิดพลาดของระบบ",
            "งานเบื้องหลังล้มเหลว — ควรเปิดไว้เสมอ", "งานเบื้องหลัง", true)
    };

    public static EventInfo Info(string code)
    {
        foreach (var e in Catalog) if (e.Code == code) return e;
        return null;
    }

    // ── ส่ง ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// ส่งแจ้งเตือนของเหตุการณ์นี้ไปทุกช่องทางที่เปิดไว้
    /// ข้อความรับเป็นรูปแบบ Telegram HTML (&lt;b&gt; &lt;i&gt; &lt;code&gt;) — ช่องทาง LINE
    /// จะถูกถอดแท็กออกให้เอง
    ///
    /// ไม่มีทาง throw — การแจ้งเตือนพังต้องไม่ทำให้งานหลักพัง
    /// </summary>
    /// <returns>true เมื่อส่งออกไปได้อย่างน้อยหนึ่งช่องทาง</returns>
    public static bool Send(string eventCode, string message)
    {
        bool any = false;
        try
        {
            if (string.IsNullOrWhiteSpace(message)) return false;

            var info = Info(eventCode);
            bool urgent = info != null && info.Urgent;

            if (InQuietHours() && !(urgent && AppCfg.GetBool("Notify_QuietHours_AllowUrgent", true)))
            {
                Log(eventCode, "ข้าม", "อยู่ในช่วงเวลาเงียบ");
                return false;
            }

            if (IsOn(eventCode, ChannelTelegram) && AppCfg.GetBool("Notify_Telegram_Enabled", true))
                any |= SendTelegram(eventCode, message);

            if (IsOn(eventCode, ChannelLine) && AppCfg.GetBool("Notify_Line_Enabled", false))
                any |= SendLine(eventCode, message);
        }
        catch (Exception ex)
        {
            try { Log(eventCode, "ล้มเหลว", ex.Message); } catch { }
        }
        return any;
    }

    /// <summary>เหตุการณ์นี้เปิดส่งช่องทางนี้อยู่ไหม (ไม่มีแถวในตาราง = ปิด)</summary>
    public static bool IsOn(string eventCode, string channel)
    {
        try
        {
            var rules = Rules();
            bool on;
            return rules.TryGetValue(Key(eventCode, channel), out on) && on;
        }
        catch { return false; }
    }

    /// <summary>ปลายทางของเหตุการณ์นี้ — ใช้ค่าเฉพาะเหตุการณ์ก่อน ไม่มีค่อยใช้ปลายทางกลาง</summary>
    public static string TargetFor(string eventCode, string channel)
    {
        string custom = null;
        try { Targets().TryGetValue(Key(eventCode, channel), out custom); }
        catch { }

        if (!string.IsNullOrWhiteSpace(custom)) return custom.Trim();

        return channel == ChannelLine
            ? (AppCfg.Get("Notify_Line_Target", "") ?? "")
            // ค่าเดิมที่โค้ดทุกจุดเคยใช้ — คงไว้เพื่อไม่ให้ปลายทางเปลี่ยนตอน deploy
            : (AppCfg.Get("TelegramChatId", "-4969611371") ?? "");
    }

    // ── ช่วงเวลาเงียบ ─────────────────────────────────────────────────────────

    /// <summary>ตอนนี้อยู่ในช่วงเวลาที่ผู้ดูแลขอไม่ให้รบกวนหรือเปล่า</summary>
    public static bool InQuietHours()
    {
        TimeSpan from, to;
        if (!TryTime(AppCfg.Get("Notify_QuietHours_From", ""), out from)) return false;
        if (!TryTime(AppCfg.Get("Notify_QuietHours_To", ""), out to)) return false;
        if (from == to) return false;

        TimeSpan now = DateTime.Now.TimeOfDay;
        // ข้ามเที่ยงคืนได้ เช่น 22:00 → 07:00
        return from < to ? (now >= from && now < to) : (now >= from || now < to);
    }

    private static bool TryTime(string hhmm, out TimeSpan t)
    {
        t = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(hhmm)) return false;
        return TimeSpan.TryParseExact(hhmm.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out t)
            || TimeSpan.TryParse(hhmm.Trim(), out t);
    }

    // ── Telegram ──────────────────────────────────────────────────────────────

    private static bool SendTelegram(string eventCode, string htmlMessage)
    {
        string token = AppCfg.Get("TelegramTokenTakeTime");
        if (string.IsNullOrWhiteSpace(token)) { Log(eventCode, "ข้าม", "ยังไม่ได้ตั้ง Telegram token"); return false; }

        string targets = TargetFor(eventCode, ChannelTelegram);
        if (string.IsNullOrWhiteSpace(targets)) { Log(eventCode, "ข้าม", "ยังไม่ได้ตั้งปลายทาง Telegram"); return false; }

        bool any = false;
        foreach (string chatId in Split(targets))
        {
            string err = PostTelegram(token, chatId, htmlMessage);
            if (err == null) { any = true; continue; }

            // HTML ไม่ถูกต้อง (แท็กเพี้ยน/ไม่ได้ escape) → ลองใหม่เป็นข้อความล้วน ดีกว่าเงียบหาย
            if (err.IndexOf("can't parse entities", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (PostTelegram(token, chatId, StripHtml(htmlMessage), false) == null) { any = true; continue; }
            }
            Log(eventCode, "ล้มเหลว", "Telegram → " + chatId + ": " + err);
        }
        return any;
    }

    /// <summary>คืน null เมื่อสำเร็จ, หรือข้อความผิดพลาดจาก Telegram</summary>
    private static string PostTelegram(string token, string chatId, string text, bool html = true)
    {
        try
        {
            var payload = new Dictionary<string, object>
            {
                { "chat_id", chatId },
                { "text", Trim(text, 4000) },
                { "disable_web_page_preview", true }
            };
            if (html) payload["parse_mode"] = "HTML";

            return PostJson("https://api.telegram.org/bot" + token + "/sendMessage",
                Newtonsoft.Json.JsonConvert.SerializeObject(payload));
        }
        catch (Exception ex) { return ex.Message; }
    }

    // ── LINE ──────────────────────────────────────────────────────────────────

    private static bool SendLine(string eventCode, string htmlMessage)
    {
        string token = LineToken();
        if (string.IsNullOrWhiteSpace(token)) { Log(eventCode, "ข้าม", "ยังไม่ได้ตั้ง LINE token"); return false; }

        string targets = TargetFor(eventCode, ChannelLine);
        if (string.IsNullOrWhiteSpace(targets)) { Log(eventCode, "ข้าม", "ยังไม่ได้ตั้งปลายทาง LINE"); return false; }

        // LINE ไม่รองรับ HTML — ส่งเป็นข้อความล้วน
        string text = Trim(StripHtml(htmlMessage), 4900);
        bool any = false;

        foreach (string to in Split(targets))
        {
            string body = Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                to = to,
                messages = new[] { new { type = "text", text = text } }
            });

            string err = PostJson("https://api.line.me/v2/bot/message/push", body, "Bearer " + token);
            if (err == null) any = true;
            else Log(eventCode, "ล้มเหลว", "LINE → " + to + ": " + err);
        }
        return any;
    }

    /// <summary>
    /// หา LINE channel access token — ลำดับเดียวกับรายงานรายวัน
    /// (ตั้งค่า LINE OA ที่มีอยู่ก่อน แล้วค่อยตกไป AppSettings เดิม)
    /// </summary>
    public static string LineToken()
    {
        try
        {
            using (var con = new SqlConnection(ConnStr))
            {
                con.Open();
                using (var cmd = new SqlCommand(
                    "SELECT TOP 1 Config FROM OmniChannel_Channels WHERE ChannelCode = 'LINE'", con))
                {
                    object o = cmd.ExecuteScalar();
                    string json = o == null || o == DBNull.Value ? null : o.ToString();
                    if (!string.IsNullOrEmpty(json))
                    {
                        var cfg = new System.Web.Script.Serialization.JavaScriptSerializer()
                            .Deserialize<Dictionary<string, object>>(json);
                        object tok;
                        if (cfg != null && cfg.TryGetValue("channelAccessToken", out tok))
                        {
                            string t = Convert.ToString(tok);
                            if (!string.IsNullOrWhiteSpace(t)) return t;
                        }
                    }
                }
            }
        }
        catch { }
        return AppCfg.Get("linechannelaccesstokentaketime", "") ?? "";
    }

    // ── ทดสอบจากหน้าตั้งค่า ───────────────────────────────────────────────────

    /// <summary>ยิงข้อความทดสอบไปช่องทางนี้ — คืนข้อความผลลัพธ์ภาษาไทยให้แสดงบนหน้าจอ</summary>
    public static string TestChannel(string channel, string targetOverride = null)
    {
        string stamp = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
        string msg = "🔔 <b>ทดสอบการแจ้งเตือน</b>\nระบบ TakeTime BangPhra\n<i>" + stamp + "</i>";

        if (channel == ChannelTelegram)
        {
            string token = AppCfg.Get("TelegramTokenTakeTime");
            if (string.IsNullOrWhiteSpace(token)) return "❌ ยังไม่ได้ตั้ง Telegram Bot Token (ศูนย์ตั้งค่า → ตั้งค่าระบบ)";

            string targets = string.IsNullOrWhiteSpace(targetOverride)
                ? TargetFor(Ev.SystemError, ChannelTelegram) : targetOverride;
            if (string.IsNullOrWhiteSpace(targets)) return "❌ ยังไม่ได้ตั้งปลายทาง (chat id)";

            var sb = new StringBuilder();
            foreach (string t in Split(targets))
            {
                string err = PostTelegram(token, t, msg);
                sb.AppendLine(err == null ? "✅ ส่งถึง " + t + " แล้ว" : "❌ " + t + " → " + err);
            }
            return sb.ToString().Trim();
        }

        if (channel == ChannelLine)
        {
            string token = LineToken();
            if (string.IsNullOrWhiteSpace(token)) return "❌ ยังไม่ได้ตั้ง LINE Channel Access Token";

            string targets = string.IsNullOrWhiteSpace(targetOverride)
                ? (AppCfg.Get("Notify_Line_Target", "") ?? "") : targetOverride;
            if (string.IsNullOrWhiteSpace(targets)) return "❌ ยังไม่ได้ตั้งปลายทาง LINE (userId / groupId)";

            var sb = new StringBuilder();
            foreach (string t in Split(targets))
            {
                string body = Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    to = t,
                    messages = new[] { new { type = "text", text = StripHtml(msg) } }
                });
                string err = PostJson("https://api.line.me/v2/bot/message/push", body, "Bearer " + token);
                sb.AppendLine(err == null ? "✅ ส่งถึง " + t + " แล้ว" : "❌ " + t + " → " + err);
            }
            return sb.ToString().Trim();
        }

        return "ไม่รู้จักช่องทางนี้";
    }

    // ── ตาราง Notification_Rules (cache สั้น ๆ) ──────────────────────────────

    private static readonly object _lock = new object();
    private static Dictionary<string, bool> _rules;
    private static Dictionary<string, string> _targets;
    private static DateTime _loadedAt = DateTime.MinValue;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    public static void Invalidate()
    {
        lock (_lock) { _rules = null; _targets = null; _loadedAt = DateTime.MinValue; }
    }

    private static string Key(string ev, string ch)
    {
        return (ev ?? "") + "|" + (ch ?? "");
    }

    private static Dictionary<string, bool> Rules() { Load(); return _rules; }
    private static Dictionary<string, string> Targets() { Load(); return _targets; }

    private static void Load()
    {
        lock (_lock)
        {
            if (_rules != null && (DateTime.UtcNow - _loadedAt) < CacheTtl) return;

            var rules = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var targets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using (var con = new SqlConnection(ConnStr))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(
                        "SELECT Event_Code, Channel, Enabled, Target FROM Notification_Rules", con))
                    using (var rd = cmd.ExecuteReader())
                        while (rd.Read())
                        {
                            string k = Key(Convert.ToString(rd[0]), Convert.ToString(rd[1]));
                            rules[k] = rd[2] != DBNull.Value && Convert.ToBoolean(rd[2]);
                            if (rd[3] != DBNull.Value) targets[k] = Convert.ToString(rd[3]);
                        }
                }
            }
            catch
            {
                // ยังไม่ได้รันไมเกรชัน → ไม่มีกฎเลย
                // ⚠ ถ้าปล่อยให้ "ไม่มีกฎ = ปิด" การแจ้งเตือนจะเงียบไปทั้งระบบทันทีที่ deploy
                //   ก่อนรัน SQL — จึงถอยไปใช้ค่าตั้งต้นของแต่ละเหตุการณ์แทน (= พฤติกรรมเดิม)
                foreach (var e in Catalog)
                    rules[Key(e.Code, ChannelTelegram)] = DefaultTelegramOn(e.Code);
            }

            _rules = rules;
            _targets = targets;
            _loadedAt = DateTime.UtcNow;
        }
    }

    /// <summary>ค่าตั้งต้นเมื่อยังไม่มีตาราง — ตรงกับพฤติกรรมก่อนมีระบบนี้</summary>
    private static bool DefaultTelegramOn(string code)
    {
        switch (code)
        {
            case Ev.OrderRoomService:
            case Ev.OrderActivity:
            case Ev.PaymentOnline:
                return false;   // เดิมไม่เคยส่งเข้า Telegram
            default:
                return true;    // เดิมส่งอยู่แล้ว
        }
    }

    // ── บันทึกผล ──────────────────────────────────────────────────────────────

    private static void Log(string eventCode, string result, string detail)
    {
        try
        {
            new Take_Time_BangPhra.code().Logs(ConnStr, "Notify",
                eventCode + " — " + result + (string.IsNullOrEmpty(detail) ? "" : ": " + detail), "System");
        }
        catch { }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static string ConnStr =>
        ConfigurationManager.ConnectionStrings["TaketimeConnectionString"]?.ConnectionString;

    private static List<string> Split(string raw)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(raw)) return list;
        foreach (string p in raw.Split(new[] { ',', ';', '\r', '\n', ' ', '\t' },
                                       StringSplitOptions.RemoveEmptyEntries))
        {
            string v = p.Trim();
            if (v.Length > 0 && !list.Contains(v)) list.Add(v);
        }
        return list;
    }

    private static string Trim(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
    }

    /// <summary>ถอดแท็ก HTML ของ Telegram ออกให้เหลือข้อความล้วนสำหรับ LINE</summary>
    public static string StripHtml(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        string t = System.Text.RegularExpressions.Regex.Replace(s, "<[^>]+>", "");
        return t.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"").Replace("&amp;", "&");
    }

    /// <summary>escape ข้อความที่จะฝังในข้อความ HTML ของ Telegram</summary>
    public static string E(string s)
    {
        return string.IsNullOrEmpty(s) ? "" :
            s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }

    /// <summary>POST JSON แบบ synchronous — คืน null เมื่อสำเร็จ ไม่งั้นคืนข้อความผิดพลาด</summary>
    private static string PostJson(string url, string json, string authHeader = null)
    {
        try
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12
                | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = "application/json; charset=utf-8";
            req.Timeout = 20000;
            req.ReadWriteTimeout = 20000;
            if (!string.IsNullOrEmpty(authHeader)) req.Headers["Authorization"] = authHeader;

            byte[] body = Encoding.UTF8.GetBytes(json);
            req.ContentLength = body.Length;
            using (Stream s = req.GetRequestStream()) s.Write(body, 0, body.Length);

            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var rd = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                rd.ReadToEnd();
                return null;
            }
        }
        catch (WebException wex)
        {
            var resp = wex.Response as HttpWebResponse;
            if (resp != null)
            {
                try
                {
                    using (var rd = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                        return "HTTP " + (int)resp.StatusCode + " " + Trim(rd.ReadToEnd(), 300);
                }
                catch { return "HTTP " + (int)resp.StatusCode; }
            }
            return wex.Message;
        }
        catch (Exception ex) { return ex.Message; }
    }
}
