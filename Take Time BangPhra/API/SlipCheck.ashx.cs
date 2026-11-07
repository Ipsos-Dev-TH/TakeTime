using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.SessionState;
using SC = Take_Time_BangPhra.Payments.SlipCheck;

namespace Take_Time_BangPhra.API
{
    /// <summary>
    /// ตรวจสลิปของหน้าจองแบบ async (CLAUDE.md ข้อ 18 — แทน postback ทั้งหน้าตอนเลือกรูปสลิป)
    /// POST multipart: file, nonce, phone, method, slot (1 = สลิปหลัก · 2..3 = สลิปเติมยอดของลูกค้า), action (check|remove|list),
    ///                 typed (ยอดที่กรอกอยู่ — พนักงาน), alloc (ติ๊กแบ่งสลิป), total (ยอดรวมของหน้า — ใช้แสดง "รับเกิน" เท่านั้น)
    /// ความปลอดภัย: ต้องมี session ของหน้าจอง + nonce ที่หน้าออกให้ (บริบท id/command/พนักงาน อ่านจาก Session ไม่เชื่อค่าที่ส่งมา) ·
    ///   header X-Slip-Check (บังคับ preflight ข้ามโดเมน) + Origin/Referer ต้องเป็นโฮสต์เดียวกัน · จำกัดความถี่ต่อ session/IP ·
    ///   ขนาด ≤ 8 MB + ชนิดรูปจาก magic bytes · ไฟล์เขียนเฉพาะชื่อชั่วคราวของ session นี้ (SlipUploadStore)
    /// ตรวจด้วยกฎชุดเดียวกับ postback: VerifyWithLock → SlipFirstVerdict.Build (ซ้ำ/ผู้รับ/โหมดลูกค้า/ยอดล็อก) ·
    /// ไม่บันทึกอะไรลงการจอง — ตอนกดยืนยัน Reserve.RunSlipGate ตรวจซ้ำจากไฟล์ที่เก็บไว้นี้เสมอ
    /// ปิดยอดล็อกตามสลิป/ผิดเงื่อนไข = {"ok":false,"code":...} → หน้ากลับไปใช้ปุ่มอัปโหลดแบบเดิม (postback)
    /// </summary>
    public class SlipCheckApi : IHttpHandler, IRequiresSessionState
    {
        public bool IsReusable => false;

        private const int RatePerSession = 20;
        private const int RatePerIp = 60;
        private static readonly TimeSpan RateWindow = TimeSpan.FromMinutes(10);

        public void ProcessRequest(HttpContext ctx)
        {
            var res = ctx.Response;
            res.ContentType = "application/json; charset=utf-8";
            res.Cache.SetCacheability(HttpCacheability.NoCache);
            res.Cache.SetNoStore();
            res.AddHeader("X-Content-Type-Options", "nosniff");
            try
            {
                var req = ctx.Request;
                if (!string.Equals(req.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)) { Write(ctx, 405, Fail("method", null)); return; }
                if (req.Headers["X-Slip-Check"] != "1" || !SameOrigin(req)) { Write(ctx, 403, Fail("forbidden", null)); return; }
                if (!SC.SlipVerificationService.AmountLockActive) { Write(ctx, 200, Fail("off", null)); return; }
                HttpSessionState session = ctx.Session;
                if (session == null) { Write(ctx, 403, Fail("forbidden", null)); return; }

                string ip = req.UserHostAddress ?? "-";
                if (!SC.SlipUploadStore.TryRate("slipck:s:" + session.SessionID, RatePerSession, RateWindow)
                    || !SC.SlipUploadStore.TryRate("slipck:ip:" + ip, RatePerIp, RateWindow))
                {
                    Write(ctx, 429, Fail("rate", "ส่งสลิปถี่เกินไป — รอสักครู่แล้วลองใหม่"));
                    return;
                }

                var pc = SC.SlipUploadStore.GetContext(session, req.Form["nonce"] ?? req.Headers["X-Slip-Nonce"]);
                if (pc == null) { Write(ctx, 403, Fail("expired", "หน้านี้เปิดค้างนานเกินไป — ระบบจะอัปโหลดแบบเดิมให้")); return; }
                bool staff = session["permission"] != null && session["permission"].ToString() == "True";
                if (pc.Staff != staff) { Write(ctx, 403, Fail("expired", null)); return; }
                if (pc.Command != "reserve" && pc.Command != "edit" && pc.Command != "checkin") { Write(ctx, 400, Fail("bad", null)); return; }
                if (!staff && pc.Command != "reserve") { Write(ctx, 403, Fail("forbidden", null)); return; }
                bool customer = !staff && pc.Command == "reserve";

                string phone = req.Form["phone"] ?? "";
                if (SC.SlipUploadStore.PhoneKey(phone).Length == 0)
                {
                    Write(ctx, 200, Input("กรอกเบอร์โทรศัพท์ก่อนแนบสลิป (ขั้น \"ข้อมูลผู้เข้าพัก\")", "phone"));
                    return;
                }

                int slot;
                if (!int.TryParse(req.Form["slot"], out slot) || slot < 1) slot = 1;
                if (slot > SC.SlipUploadStore.MaxSlots || (slot > 1 && !customer)) { Write(ctx, 400, Fail("bad", null)); return; }

                string sid = session.SessionID;
                string action = (req.Form["action"] ?? "check").Trim().ToLowerInvariant();
                int rid;
                long? resId = int.TryParse(pc.QueryId, out rid) && rid > 0 ? (long?)rid : null;
                string user = SC.SlipVerificationService.CurrentUserName();

                if (action == "remove" || action == "list")
                {
                    if (action == "remove" && slot >= 2)
                    {
                        try { File.Delete(SC.SlipUploadStore.PathOf(SC.SlipUploadStore.TempFilename(sid, pc.QueryId, pc.Command, phone, slot))); } catch { }
                    }
                    var lj = new Dictionary<string, object> { { "ok", true }, { "action", action } };
                    if (customer) AddCustomerSlips(lj, sid, pc, phone, null);
                    Write(ctx, 200, lj);
                    return;
                }
                if (action != "check") { Write(ctx, 400, Fail("bad", null)); return; }

                // ช่องทางต้องเป็นโอน/QR (กฎเดียวกับ RunSlipGate) — ไม่ใช่ = ให้หน้าใช้ทางเดิม
                string method = req.Form["method"] ?? "";
                if (!IsTransferMethod(method)) { Write(ctx, 200, Fail("not_transfer", null)); return; }

                HttpPostedFile f = req.Files["file"];
                if (f == null || f.ContentLength <= 0) { Write(ctx, 200, Input("ไม่พบไฟล์สลิป — เลือกรูปอีกครั้ง", null)); return; }
                if (f.ContentLength > SC.SlipUploadStore.MaxBytes)
                {
                    Write(ctx, 200, Input("ไฟล์ใหญ่เกินไป (เกิน " + (SC.SlipUploadStore.MaxBytes / 1024 / 1024) + " MB) — ลองแคปหน้าจอสลิปแทน", null));
                    return;
                }
                byte[] bytes;
                using (var ms = new MemoryStream())
                {
                    f.InputStream.CopyTo(ms);
                    bytes = ms.ToArray();
                }
                if (SC.SlipUploadStore.SniffImage(bytes) == null)
                {
                    Write(ctx, 200, Input("ไฟล์นี้ไม่ใช่รูปภาพ — กรุณาแนบรูปสลิป (JPG / PNG)", null));
                    return;
                }

                string[] before = SC.SlipUploadStore.SlotPaths(sid, pc.QueryId, pc.Command, phone);
                if (slot > 1 && before[0] == null) { Write(ctx, 200, Input("แนบสลิปแรกก่อน แล้วค่อยแนบสลิปเพิ่ม", null)); return; }

                string fileName = SC.SlipUploadStore.TempFilename(sid, pc.QueryId, pc.Command, phone, slot);
                string target = SC.SlipUploadStore.PathOf(fileName);
                if (!Directory.Exists(SC.SlipUploadStore.Dir)) Directory.CreateDirectory(SC.SlipUploadStore.Dir);
                File.WriteAllBytes(target, bytes);
                if (slot == 1) SC.SlipUploadStore.ClearTopUps(sid, pc.QueryId, pc.Command, phone);   // สลิปหลักใหม่ = ยอดเติมเดิมใช้ไม่ได้

                var json = new Dictionary<string, object> { { "ok", true }, { "slot", slot }, { "compressed", req.Form["cmp"] == "1" } };
                json["thumb"] = "./Upload/Slip/" + fileName + "?v=" + File.GetLastWriteTimeUtc(target).Ticks.ToString(CultureInfo.InvariantCulture);

                if (slot == 1)
                {
                    decimal typed;
                    if (!decimal.TryParse(req.Form["typed"], NumberStyles.Number, CultureInfo.InvariantCulture, out typed) || typed < 0m) typed = 0m;
                    bool alloc = !customer && req.Form["alloc"] == "1";
                    SC.SlipAmountLock lk;
                    decimal? rec;
                    var r = SC.SlipVerificationService.VerifyWithLock(bytes, fileName, typed, new SC.SlipVerifyContext
                    {
                        SourceType = "RESERVATION",
                        SourceId = resId,
                        CustomerSelfService = customer,
                        User = user
                    }, alloc, out lk, out rec);
                    if (typed <= 0m && r != null) { r.ExpectedAmount = null; r.Difference = null; }   // เหมือน PreviewSlipCheck (ยังไม่มียอดเทียบ)
                    var v = SC.SlipFirstVerdict.Build(r, lk, customer, DueFor(pc, resId, req.Form["total"]),
                        !customer && SC.SlipVerificationService.CanOverrideCurrentUser());
                    if (v == null) { Write(ctx, 200, Fail("error", null)); return; }
                    json["state"] = v.State;
                    json["msg"] = v.Message;
                    json["tone"] = v.Tone;
                    json["hint"] = v.Hint;
                    json["cardHtml"] = v.CardHtml;
                    decimal? locked = v.LockedAmount;
                    json["locked"] = locked.HasValue;
                    if (locked.HasValue) json["amountText"] = SC.SlipAmountLockUi.AmountText(locked.Value);
                    if (v.StaffBranch)
                        json["opts"] = new Dictionary<string, object>
                        {
                            { "show", v.ShowStaffOpts }, { "useAmount", v.ShowUseAmount }, { "allocate", v.ShowAllocate }, { "override", v.ShowOverride }
                        };
                    if (v.State == SC.SlipFirstVerdict.Stop && customer) json["contactHtml"] = SC.SlipAmountLockUi.ContactButtonsHtml();
                }
                else
                {
                    // สลิปเติมยอด: ตรวจทั้งชุด (ซ้ำในชุด/ในฐานข้อมูล, ต้องล็อกยอดได้, กฎลูกค้า) — ใบที่ใช้ไม่ได้ลบทิ้งทันที
                    var set = SC.SlipUploadStore.EvaluateCustomerSet(SC.SlipUploadStore.SlotPaths(sid, pc.QueryId, pc.Command, phone), resId, user);
                    var first = set.FirstOrDefault(x => x.Slot == 1);
                    var me = set.FirstOrDefault(x => x.Slot == slot);
                    string problem = first == null || first.Problem != null
                        ? "สลิปแรกยังใช้ไม่ได้ — " + (first != null ? first.Problem : "แนบสลิปแรกใหม่")
                        : me == null ? "ตรวจสลิปไม่ได้" : me.Problem;
                    if (problem != null)
                    {
                        try { File.Delete(target); } catch { }
                        json["state"] = SC.SlipFirstVerdict.Stop;
                        json["tone"] = "err";
                        json["msg"] = "⛔ " + problem;
                        json.Remove("thumb");
                    }
                    else
                    {
                        json["state"] = SC.SlipFirstVerdict.Locked;
                        json["tone"] = me.NeedsStaff ? "warn" : "ok";
                        json["msg"] = (me.NeedsStaff ? "⚠️ อ่านยอดได้ — เจ้าหน้าที่จะตรวจให้" : me.Result != null && me.Result.BankConfirmed ? "✅ ธนาคารยืนยันยอดแล้ว" : "✅ อ่านยอดจากสลิปแล้ว")
                            + " ฿" + me.Amount.ToString("N2");
                        json["amountText"] = SC.SlipAmountLockUi.AmountText(me.Amount);
                        json["locked"] = true;
                    }
                }

                // QR ยังอ่านได้ไหม (รูปที่เบราว์เซอร์ย่อมา) — อ่านไม่ได้ + ตัวอ่าน QR พร้อม = หน้าส่งรูปต้นฉบับซ้ำ
                try
                {
                    var insp = SC.SlipVerificationService.Inspect(bytes, fileName, false);   // cache ตาม hash — ไม่เรียก SlipOK ซ้ำ
                    json["qr"] = insp != null && insp.Qr != null;
                    json["vendor"] = insp != null && insp.Vendor != null && insp.Vendor.HasData;   // มีผลจากธนาคาร (SlipOK) — ไม่ส่งซ้ำ (กันโควต้า/1012)
                    bool ready;
                    SC.SlipQrDecoder.EngineStatus(out ready);
                    json["qrReady"] = ready;
                }
                catch { json["qr"] = false; json["qrReady"] = false; }

                if (customer) AddCustomerSlips(json, sid, pc, phone, slot == 1 && json.ContainsKey("amountText") ? json["amountText"] as string : null);
                Write(ctx, 200, json);
            }
            catch (Exception ex)
            {
                try { new code().Logs(ConnStr, "SlipCheck API", ex.GetType().Name + ": " + ex.Message, "SYSTEM"); } catch { }
                Write(ctx, 500, Fail("error", null));
            }
        }

        private static string ConnStr => ConfigurationManager.ConnectionStrings["TaketimeConnectionString"]?.ConnectionString;

        /// <summary>รายการสลิปของลูกค้า (ยอดล็อกของแต่ละใบ — ไม่บันทึก log ซ้ำ) + ยอดรวม</summary>
        private static void AddCustomerSlips(Dictionary<string, object> json, string sid, SC.SlipUploadStore.PageContext pc, string phone, string slot1AmountText)
        {
            var list = new List<object>();
            decimal sum = 0m;
            string[] paths = SC.SlipUploadStore.SlotPaths(sid, pc.QueryId, pc.Command, phone);
            for (int i = 0; i < paths.Length; i++)
            {
                if (paths[i] == null) continue;
                decimal amt = 0m;
                if (i == 0 && slot1AmountText != null)
                    decimal.TryParse(slot1AmountText, NumberStyles.Number, CultureInfo.InvariantCulture, out amt);
                else
                {
                    var lk = SC.SlipVerificationService.InspectAmountLockFile(paths[i]);
                    if (lk != null && lk.Locked && lk.Amount.HasValue) amt = lk.Amount.Value;
                }
                sum += amt;
                list.Add(new Dictionary<string, object>
                {
                    { "slot", i + 1 },
                    { "amount", amt },
                    { "thumb", "./Upload/Slip/" + Path.GetFileName(paths[i]) + "?v=" + File.GetLastWriteTimeUtc(paths[i]).Ticks.ToString(CultureInfo.InvariantCulture) }
                });
            }
            json["slips"] = list;
            json["sum"] = sum;
            json["maxSlots"] = SC.SlipUploadStore.MaxSlots;
        }

        private static decimal? DueFor(SC.SlipUploadStore.PageContext pc, long? resId, string total)
        {
            try
            {
                decimal v;
                if (pc.Command == "reserve")
                    return decimal.TryParse(total, NumberStyles.Number, CultureInfo.InvariantCulture, out v) ? v : (decimal?)null;
                if (resId.HasValue)
                {
                    var bal = ReservationBalance.Load(ConnStr, (int)resId.Value);
                    if (bal != null) return bal.Due;
                }
            }
            catch { }
            return null;
        }

        /// <summary>แหล่งเงินที่เลือกเป็นโอน/QR — ชื่อจากฐานข้อมูล (ไม่เชื่อข้อความจากหน้า) · ช่องทางเกตเวย์ (CH:) = ไม่ใช่</summary>
        private static bool IsTransferMethod(string value)
        {
            int id;
            if (string.IsNullOrEmpty(value) || value.StartsWith("CH:", StringComparison.Ordinal) || !int.TryParse(value, out id) || id <= 0) return false;
            string text = "";
            try
            {
                using (var con = new SqlConnection(ConnStr))
                using (var cmd = new SqlCommand("SELECT TOP 1 Paid_How FROM Account_Paid_How WHERE ID = @id", con))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    con.Open();
                    object o = cmd.ExecuteScalar();
                    if (o == null || o == DBNull.Value) return false;
                    text = Convert.ToString(o);
                }
            }
            catch { return false; }
            return SC.SlipVerificationService.IsTransferPaidHow(value, text);
        }

        /// <summary>Origin/Referer (ถ้ามี) ต้องเป็นโฮสต์เดียวกับคำขอ — เทียบ host:port ไม่สน scheme (ระบบอยู่หลัง proxy ที่ถอด https)</summary>
        private static bool SameOrigin(HttpRequest req)
        {
            string self = req.Url.Authority;
            string origin = req.Headers["Origin"];
            if (!string.IsNullOrEmpty(origin) && origin != "null")
            {
                Uri o;
                return Uri.TryCreate(origin, UriKind.Absolute, out o) && string.Equals(o.Authority, self, StringComparison.OrdinalIgnoreCase);
            }
            if (req.UrlReferrer != null)
                return string.Equals(req.UrlReferrer.Authority, self, StringComparison.OrdinalIgnoreCase);
            return true;   // ไม่มีทั้งคู่ (เบราว์เซอร์เก่า/ปิด referrer) — header + nonce ใน session กันแล้ว
        }

        private static Dictionary<string, object> Fail(string code, string msg)
        {
            var d = new Dictionary<string, object> { { "ok", false }, { "code", code } };
            if (msg != null) d["msg"] = msg;
            return d;
        }

        private static Dictionary<string, object> Input(string msg, string field)
        {
            var d = Fail("input", msg);
            if (field != null) d["field"] = field;
            return d;
        }

        private static void Write(HttpContext ctx, int status, Dictionary<string, object> json)
        {
            ctx.Response.StatusCode = status;
            ctx.Response.Write(new JavaScriptSerializer().Serialize(json));
        }
    }
}
