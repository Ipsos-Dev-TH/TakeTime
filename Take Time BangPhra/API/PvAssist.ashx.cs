using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.SessionState;
using Take_Time_BangPhra.Integration;

namespace Take_Time_BangPhra.API
{
    /// <summary>
    /// ผู้ช่วยหน้าใบสำคัญจ่าย (Account/PaymentVoucher — CLAUDE.md ข้อ 3b): คำแนะนำจาก AI ของ NextAcc + ตรวจใบซ้ำ แบบ async
    /// POST form: action = suggest | dup | feedback, nonce (ออกโดยหน้า, เก็บใน Session["PvAssistNonce"])
    ///   suggest: vendorId, desc, amount, line (ค่า ddlLineAccount), vatPct, wht, credit (1/0), want (line|vendor|both)
    ///   dup:     vendorId, inv (เลขที่ใบกำกับ/ใบเสร็จของผู้ขาย), amount, date (yyyy-MM-dd), exclude (เลขใบตัวเองตอนแก้ไข)
    ///   feedback: fid, chosen, accepted (1/0)
    /// สิทธิ์เท่าหน้า: ล็อกอินพนักงาน (Owner/Admin) + Perm.FinVoucher · header X-PV-Assist + Origin/Referer โฮสต์เดียวกัน · จำกัดความถี่ต่อ session
    /// ไม่เขียนอะไรลงฐานข้อมูล TakeTime — ผลทุกอย่างเป็นคำแนะนำให้ผู้ใช้กดใช้เอง
    /// </summary>
    public class PvAssistApi : IHttpHandler, IRequiresSessionState
    {
        public bool IsReusable => false;

        private const int RatePerSession = 240;
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
                if (!string.Equals(req.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)) { Write(ctx, 405, Fail("method")); return; }
                if (req.Headers["X-PV-Assist"] != "1" || !SameOrigin(req)) { Write(ctx, 403, Fail("forbidden")); return; }
                var session = ctx.Session;
                if (session == null || !IsStaff(session) || !Perm.CanAccess(Perm.FinVoucher)) { Write(ctx, 403, Fail("forbidden")); return; }
                string nonce = session["PvAssistNonce"] as string;
                if (string.IsNullOrEmpty(nonce) || !string.Equals(nonce, req.Form["nonce"], StringComparison.Ordinal)) { Write(ctx, 403, Fail("expired")); return; }
                if (!Payments.SlipCheck.SlipUploadStore.TryRate("pvassist:s:" + session.SessionID, RatePerSession, RateWindow))
                {
                    Write(ctx, 429, Fail("rate"));
                    return;
                }

                string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
                var assist = new PaymentVoucherAssist(conn);
                string action = (req.Form["action"] ?? "").Trim().ToLowerInvariant();

                if (action == "suggest")
                {
                    if (!assist.AiEnabled) { Write(ctx, 200, new Dictionary<string, object> { { "ok", true }, { "enabled", false }, { "suggestions", new object[0] } }); return; }
                    string want = (req.Form["want"] ?? "both").Trim().ToLowerInvariant();
                    var list = assist.Suggest(new PaymentVoucherAssist.SuggestRequest
                    {
                        VendorId = req.Form["vendorId"],
                        Description = Cut(req.Form["desc"], 300),
                        Amount = Num(req.Form["amount"]),
                        CurrentLineAccount = Cut(req.Form["line"], 80),
                        CurrentVatPercent = Cut(req.Form["vatPct"], 8),
                        CurrentWhtRate = Cut(req.Form["wht"], 8),
                        CurrentIsCredit = req.Form["credit"] == "1",
                        WantLine = want == "line" || want == "both",
                        WantVendor = want == "vendor" || want == "both"
                    });
                    Write(ctx, 200, new Dictionary<string, object>
                    {
                        { "ok", true }, { "enabled", true },
                        { "suggestions", list.Select(s => new Dictionary<string, object>
                            {
                                { "kind", s.Kind }, { "target", s.Target }, { "value", s.Value ?? "" }, { "label", s.Label ?? "" },
                                { "confidence", s.Confidence }, { "reasoning", Cut(s.Reasoning, 400) ?? "" }, { "fid", s.FeedbackId ?? "" },
                                { "answer", s.Answer ?? "" }, { "same", s.Same }
                            }).ToArray() }
                    });
                    return;
                }

                if (action == "dup")
                {
                    DateTime date;
                    if (!DateTime.TryParseExact(req.Form["date"] ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                        date = DateTime.Today;
                    var hits = assist.FindDuplicates(req.Form["vendorId"], Cut(req.Form["inv"], 100), Num(req.Form["amount"]), date,
                        Cut(req.Form["exclude"], 60), includeRemote: true);
                    Write(ctx, 200, new Dictionary<string, object>
                    {
                        { "ok", true },
                        { "hits", hits.Select(h => new Dictionary<string, object>
                            {
                                { "source", h.Source }, { "sourceLabel", PaymentVoucherForm.SourceLabel(h.Source) }, { "number", h.Number ?? "" },
                                { "date", h.Date ?? "" }, { "amount", h.Amount }, { "status", h.Status ?? "" }, { "url", SafeUrl(h.Url) },
                                { "reason", h.Reason ?? "" }, { "strong", h.Strong }
                            }).ToArray() }
                    });
                    return;
                }

                if (action == "feedback")
                {
                    bool ok = assist.RecordFeedback(Cut(req.Form["fid"], 60), Cut(req.Form["chosen"], 200), req.Form["accepted"] == "1");
                    Write(ctx, 200, new Dictionary<string, object> { { "ok", ok } });
                    return;
                }

                Write(ctx, 400, Fail("bad"));
            }
            catch (Exception ex)
            {
                try { new code().Logs(ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString, "Accounting Sync", "PvAssist: " + ex.Message, "SYSTEM"); } catch { }
                Write(ctx, 200, Fail("error"));
            }
        }

        private static bool IsStaff(HttpSessionState s)
        {
            try
            {
                return s["permission"]?.ToString() == "True"
                    && (s["User"]?.ToString() == "Owner" || s["User"]?.ToString() == "Admin");
            }
            catch { return false; }
        }

        /// <summary>ลิงก์ที่ส่งให้หน้า: เฉพาะ path ภายใน (/…) หรือ http(s) — อย่างอื่นตัดทิ้ง</summary>
        private static string SafeUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            if (url.StartsWith("/", StringComparison.Ordinal) && !url.StartsWith("//", StringComparison.Ordinal)) return url;
            Uri u;
            return Uri.TryCreate(url, UriKind.Absolute, out u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp) ? url : "";
        }

        private static decimal Num(string s)
        {
            decimal d;
            return decimal.TryParse((s ?? "").Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out d) && d > 0m ? d : 0m;
        }

        private static string Cut(string s, int max)
        {
            if (s == null) return null;
            s = s.Trim();
            return s.Length > max ? s.Substring(0, max) : s;
        }

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
            return true;   // ไม่มีทั้งคู่ — header + nonce ใน session กันแล้ว
        }

        private static Dictionary<string, object> Fail(string code)
            => new Dictionary<string, object> { { "ok", false }, { "code", code } };

        private static void Write(HttpContext ctx, int status, Dictionary<string, object> json)
        {
            ctx.Response.StatusCode = status;
            ctx.Response.Write(new JavaScriptSerializer().Serialize(json));
        }
    }
}
