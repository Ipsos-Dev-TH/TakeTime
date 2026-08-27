using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Script.Serialization;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.API
{
    /// <summary>
    /// ปลายทางแชทสาธารณะสำหรับ "ลูกค้าทั่วไป" ที่กดไอคอนแชทลอยบนหน้าเว็บ — ไม่ต้องล็อกอิน
    ///
    /// เชื่อมกับสมองที่มีอยู่แล้ว: AIKnowledgeService.ProcessAutoReply
    ///   → หา Knowledge Base ก่อน (เร็ว/ฟรี) → ถ้าไม่เจอใช้ DeepSeek (เรียนสไตล์จากที่ป้อนไว้)
    ///   → รองรับเช็คห้องว่าง + สร้างการจอง (สถานะ "รอยืนยัน") ผ่านคำสั่ง {{BOOK:…}}
    ///
    /// ทุกบทสนทนาลงในกล่องแชทรวม (OmniChannel ช่องทาง WEBCHAT) → พนักงานเห็นและรับช่วงต่อได้
    /// การจองจากแชท = สถานะ "รอยืนยัน" เสมอ → ลูกค้าแนบสลิป → แจ้งพนักงานยืนยัน (เหมือนคนทำจริง)
    ///
    /// ตัวตนลูกค้า = sid สุ่มเก็บฝั่ง client (ไม่มี PII) → PlatformUserId = "web_{sid}"
    /// </summary>
    public class PublicChat : IHttpHandler
    {
        private static string ConnStr => ConfigurationManager.ConnectionStrings["TaketimeConnectionString"]?.ConnectionString ?? "";
        private const string Channel = "WEBCHAT";
        private const int MaxMsgLen = 1000;

        public void ProcessRequest(HttpContext ctx)
        {
            ctx.Response.ContentType = "application/json; charset=utf-8";
            string action = (ctx.Request.QueryString["action"] ?? "send").ToLowerInvariant();
            try
            {
                switch (action)
                {
                    case "send": Write(ctx, HandleSend(ctx)); break;
                    case "history": Write(ctx, HandleHistory(ctx)); break;
                    case "slip": Write(ctx, HandleSlip(ctx)); break;
                    default: Write(ctx, new Dictionary<string, object> { { "ok", false }, { "error", "unknown action" } }); break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("PublicChat [{0}]: {1}", action, ex.Message);
                Write(ctx, new Dictionary<string, object> { { "ok", false }, { "error", "เกิดข้อผิดพลาด กรุณาลองใหม่" } });
            }
        }

        // ── ส่งข้อความ ────────────────────────────────────────────────────────────
        private Dictionary<string, object> HandleSend(HttpContext ctx)
        {
            string body = ReadBody(ctx);
            var data = Deserialize(body);
            string sid = CleanSid(Get(data, "sid"));
            string msg = (Get(data, "message") ?? "").Trim();

            if (string.IsNullOrEmpty(sid)) return Fail("session ไม่ถูกต้อง");
            if (string.IsNullOrEmpty(msg)) return Fail("กรุณาพิมพ์ข้อความ");
            if (msg.Length > MaxMsgLen) msg = msg.Substring(0, MaxMsgLen);

            var omni = new OmniChannelService(ConnStr);

            // 1) ลงข้อความลูกค้าเข้ากล่องแชทรวม (สร้าง contact/conversation อัตโนมัติ)
            var inRes = omni.ReceiveMessage(Channel, "web_" + sid, "ผู้เยี่ยมชมเว็บ", msg,
                displayName: "ผู้เยี่ยมชมเว็บ #" + sid.Substring(0, Math.Min(6, sid.Length)));
            if (!inRes.Success)
                return Fail("ระบบแชทไม่พร้อม กรุณาลองใหม่");

            long convId = inRes.ConversationID;

            // จับคู่กับการจองถ้าลูกค้าพิมพ์เบอร์/เลขจองมา → ปุ่มแชทขึ้นในตารางผู้เข้าพัก
            try { new ChatBookingLinker(ConnStr).TryLink(convId, msg); } catch { }

            // 2) ถาม AI (KB → DeepSeek → booking) — gate ด้วย AUTO_REPLY
            var ai = new AIKnowledgeService(ConnStr);
            bool autoReply = false;
            try { autoReply = ai.IsFeatureEnabled("AUTO_REPLY"); } catch { }

            string reply = null;
            int? pendingResId = null;
            object bookingSummary = null;

            if (autoReply)
            {
                var r = ai.ProcessAutoReply(msg, Channel, null, convId);
                if (r != null && r.Success && !string.IsNullOrEmpty(r.Reply))
                {
                    reply = r.Reply;
                    if (r.BookingData != null && r.BookingData.ContainsKey("reservationId")
                        && Convert.ToBoolean(r.BookingData["success"]))
                    {
                        pendingResId = Convert.ToInt32(r.BookingData["reservationId"]);
                        bookingSummary = BuildBookingSummary(r.BookingData);
                        // ผูกบทสนทนากับการจองที่เพิ่งสร้าง → ปุ่มแชทขึ้นทันทีฝั่งพนักงาน
                        try { new ChatBookingLinker(ConnStr).TryLink(convId, msg); } catch { }
                    }
                }
            }

            // 3) ตอบกลับ — ถ้า AI ไม่ตอบ (ปิด/ไม่มั่นใจ) แจ้งว่าพนักงานจะติดต่อกลับ
            //    ข้อความยังอยู่ในกล่องแชทให้พนักงานตอบเองได้
            bool aiAnswered = !string.IsNullOrEmpty(reply);
            if (!aiAnswered)
                reply = "ขอบคุณสำหรับข้อความค่ะ 🙏 พนักงานจะรีบติดต่อกลับโดยเร็วที่สุด " +
                        "หากต้องการสอบถามด่วน โทร 099-xxx-xxxx ได้เลยค่ะ";

            // บันทึกคำตอบลงกล่องแชท (isAI = ให้พนักงานรู้ว่าบอทตอบ)
            omni.SendMessage(convId, reply, aiAnswered ? "ผู้ช่วย AI" : "ระบบ",
                isAI: aiAnswered, aiSource: aiAnswered ? "WEBCHAT_AI" : "AUTO");

            if (pendingResId.HasValue)
                NotifyStaff($"🆕 มีการจองใหม่ผ่านแชทหน้าเว็บ (รอยืนยัน)\nเลขจอง #{pendingResId}\nดูในกล่องแชท/หน้าจองรอยืนยัน");

            var res = new Dictionary<string, object>
            {
                { "ok", true },
                { "reply", reply },
                { "aiAnswered", aiAnswered }
            };
            if (pendingResId.HasValue)
            {
                res["pendingReservationId"] = pendingResId.Value;
                res["booking"] = bookingSummary;
                res["needSlip"] = true;   // ให้ widget โชว์ปุ่มแนบสลิป
            }
            return res;
        }

        // ── ประวัติแชท ────────────────────────────────────────────────────────────
        private Dictionary<string, object> HandleHistory(HttpContext ctx)
        {
            string sid = CleanSid(ctx.Request.QueryString["sid"]);
            if (string.IsNullOrEmpty(sid)) return new Dictionary<string, object> { { "ok", true }, { "messages", new List<object>() } };

            var dt = new code().DatabaseQuerySafe(ConnStr,
                @"SELECT TOP 50 m.Direction, m.Content, m.MediaUrl, m.MessageType, m.Created_Date
                    FROM OmniChannel_Messages m
                    JOIN OmniChannel_Conversations c ON c.ID = m.ConversationID
                    JOIN OmniChannel_Contact_Identifiers ci ON ci.ContactID = c.ContactID AND ci.ChannelCode = c.ChannelCode
                   WHERE c.ChannelCode = @ch AND ci.PlatformUserId = @uid
                   ORDER BY m.Created_Date ASC",
                new Dictionary<string, object> { { "@ch", Channel }, { "@uid", "web_" + sid } });

            var list = new List<object>();
            if (dt != null)
                foreach (DataRow r in dt.Rows)
                    list.Add(new Dictionary<string, object>
                    {
                        { "from", r["Direction"]?.ToString() == "IN" ? "user" : "bot" },
                        { "text", r["Content"]?.ToString() },
                        { "mediaUrl", r["MediaUrl"] == DBNull.Value ? null : r["MediaUrl"].ToString() },
                        { "type", r["MessageType"]?.ToString() }
                    });
            return new Dictionary<string, object> { { "ok", true }, { "messages", list } };
        }

        // ── แนบสลิป → ปิดวงจรการจอง ────────────────────────────────────────────────
        private Dictionary<string, object> HandleSlip(HttpContext ctx)
        {
            string sid = CleanSid(ctx.Request.Form["sid"]);
            int resId;
            int.TryParse(ctx.Request.Form["resId"], out resId);
            if (string.IsNullOrEmpty(sid) || resId <= 0) return Fail("ข้อมูลไม่ครบ");
            if (ctx.Request.Files.Count == 0 || ctx.Request.Files[0] == null) return Fail("ไม่พบไฟล์สลิป");

            // ตรวจว่าเลขจองนี้เป็นการจองผ่านแชทที่ยังรอยืนยันจริง (กันแนบสลิปมั่วเลขจองคนอื่น)
            var chk = new code().DatabaseQuerySafe(ConnStr,
                @"SELECT ba.ID, ba.ConversationID
                    FROM AI_Booking_Actions ba
                    JOIN OmniChannel_Conversations c ON c.ID = ba.ConversationID
                    JOIN OmniChannel_Contact_Identifiers ci ON ci.ContactID = c.ContactID AND ci.ChannelCode = c.ChannelCode
                   WHERE ba.ReservationID = @res AND ba.Status = 'PENDING'
                     AND ci.PlatformUserId = @uid",
                new Dictionary<string, object> { { "@res", resId }, { "@uid", "web_" + sid } });
            if (chk == null || chk.Rows.Count == 0) return Fail("ไม่พบการจองที่รอยืนยันของคุณ");

            long convId = Convert.ToInt64(chk.Rows[0]["ConversationID"]);

            var slip = UploadHelper.Save(ctx.Request.Files[0], "~/Uploads/BookingSlips",
                "webbook_" + resId, UploadHelper.ImageDoc);
            if (!slip.Success) return Fail(slip.Error);

            var c = new code();
            c.DatabaseInsertSafe(ConnStr,
                "UPDATE AI_Booking_Actions SET Slip_Path = @p WHERE ReservationID = @res AND Status = 'PENDING'",
                new Dictionary<string, object> { { "@p", slip.WebPath }, { "@res", resId } });

            // ลงสลิปในกล่องแชทเป็นรูป + ข้อความระบบ ให้พนักงานเห็นและกดยืนยัน
            var omni = new OmniChannelService(ConnStr);
            omni.ReceiveMessage(Channel, "web_" + sid, "ผู้เยี่ยมชมเว็บ", "แนบสลิปการโอนเงิน",
                "IMAGE", mediaUrl: slip.WebPath);

            NotifyStaff($"💸 ลูกค้าแนบสลิปการจอง #{resId} ผ่านแชทหน้าเว็บแล้ว — กรุณาตรวจสอบและกดยืนยันในกล่องแชท/หน้าจองรอยืนยัน");

            string done = "ได้รับสลิปเรียบร้อยแล้วค่ะ ✅ ทางเราจะตรวจสอบและยืนยันการจองให้โดยเร็วที่สุด " +
                          "แล้วจะแจ้งกลับทางแชทนี้นะคะ ขอบคุณค่ะ 🙏";
            omni.SendMessage(convId, done, "ระบบ", aiSource: "AUTO");

            return new Dictionary<string, object> { { "ok", true }, { "reply", done } };
        }

        // ── helpers ────────────────────────────────────────────────────────────────
        private object BuildBookingSummary(Dictionary<string, object> b)
        {
            return new Dictionary<string, object>
            {
                { "reservationId", b.ContainsKey("reservationId") ? b["reservationId"] : null },
                { "roomName", b.ContainsKey("roomName") ? b["roomName"] : "" },
                { "checkIn", b.ContainsKey("checkIn") ? b["checkIn"] : "" },
                { "checkOut", b.ContainsKey("checkOut") ? b["checkOut"] : "" },
                { "nights", b.ContainsKey("nights") ? b["nights"] : 0 },
                { "total", b.ContainsKey("total") ? b["total"] : 0 }
            };
        }

        /// <summary>แจ้งพนักงานผ่านประตูกลาง — เปิด/ปิดได้ที่ ศูนย์ตั้งค่า → การแจ้งเตือน</summary>
        private void NotifyStaff(string text)
        {
            Notify.Send(Notify.Ev.ChatPublic, text);
        }

        private static string CleanSid(string sid)
        {
            if (string.IsNullOrEmpty(sid)) return null;
            sid = new string(sid.Where(char.IsLetterOrDigit).ToArray());
            return sid.Length < 6 || sid.Length > 64 ? null : sid;
        }

        private static string ReadBody(HttpContext ctx)
        {
            using (var r = new StreamReader(ctx.Request.InputStream, System.Text.Encoding.UTF8))
                return r.ReadToEnd();
        }

        private static Dictionary<string, object> Deserialize(string body)
        {
            try { return new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(body ?? "{}") ?? new Dictionary<string, object>(); }
            catch { return new Dictionary<string, object>(); }
        }

        private static string Get(Dictionary<string, object> d, string k) =>
            d != null && d.ContainsKey(k) ? d[k]?.ToString() : null;

        private static Dictionary<string, object> Fail(string msg) =>
            new Dictionary<string, object> { { "ok", false }, { "error", msg } };

        private static void Write(HttpContext ctx, object o) =>
            ctx.Response.Write(new JavaScriptSerializer().Serialize(o));

        public bool IsReusable => false;
    }
}
