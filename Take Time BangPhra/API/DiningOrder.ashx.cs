using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.SessionState;
using Take_Time_BangPhra.Dining;
using SC = Take_Time_BangPhra.Payments.SlipCheck;

namespace Take_Time_BangPhra.API
{
    /// <summary>
    /// ตัวช่วยร่วมของหน้าลูกค้า QR สั่งอาหาร (Order/Default, Order/Status, API/DiningOrder.ashx):
    ///   cookie `dnsess` (HttpOnly, SameSite=Lax, path /, ~12 ชม.) · nonce ต่อหน้า ผูกกับ session token ใน ASP.NET Session
    ///   (API รับแค่ nonce แล้วอ่าน token จาก Session เอง — เว็บอื่นยิงแทนไม่ได้) · แปลงโมเดล Dining เป็น JSON ที่ไม่รั่วข้อมูลภายใน
    /// </summary>
    public static class DiningGuest
    {
        public const string CookieName = "dnsess";
        public const string TableTokenCookie = "dntable";
        private const int CookieHours = 12;

        public static string ConnStr
        {
            get { return ConfigurationManager.ConnectionStrings["TaketimeConnectionString"] != null ? ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString : null; }
        }

        // ── cookie ──
        public static string CookieToken(HttpRequest req)
        {
            try
            {
                var c = req.Cookies[CookieName];
                string v = c != null ? c.Value : null;
                return DiningTokens.IsValidToken(v) ? v : null;
            }
            catch { return null; }
        }

        public static void SetCookie(HttpResponse res, string token)
        {
            if (!DiningTokens.IsValidToken(token)) return;
            var c = new HttpCookie(CookieName, token) { HttpOnly = true, Path = "/", Expires = DateTime.Now.AddHours(CookieHours) };
            try { c.SameSite = SameSiteMode.Lax; } catch { }
            res.Cookies.Set(c);
        }

        public static void ClearCookie(HttpResponse res)
        {
            var c = new HttpCookie(CookieName, "") { HttpOnly = true, Path = "/", Expires = DateTime.Now.AddDays(-1) };
            try { c.SameSite = SameSiteMode.Lax; } catch { }
            res.Cookies.Set(c);
        }

        /// <summary>จำ QR token ของโต๊ะไว้ (ปุ่ม "สั่งเพิ่ม"/สแกนใหม่หลังปิดบิล ไม่ต้องสแกนซ้ำในเครื่องเดิม)</summary>
        public static void RememberTableToken(HttpResponse res, string qrToken)
        {
            if (!DiningTokens.IsValidToken(qrToken)) return;
            var c = new HttpCookie(TableTokenCookie, qrToken) { HttpOnly = true, Path = "/", Expires = DateTime.Now.AddHours(CookieHours) };
            try { c.SameSite = SameSiteMode.Lax; } catch { }
            res.Cookies.Set(c);
        }

        public static string RememberedTableToken(HttpRequest req)
        {
            try
            {
                var c = req.Cookies[TableTokenCookie];
                string v = c != null ? c.Value : null;
                return DiningTokens.IsValidToken(v) ? v : null;
            }
            catch { return null; }
        }

        public static string ClientIp(HttpRequest req)
        {
            try { return req.UserHostAddress ?? "-"; } catch { return "-"; }
        }

        // ── nonce ต่อหน้า (ผูก session token) ──
        private static string NonceKey(string token) { return "dn:n:" + token; }
        private static string CtxKey(string nonce) { return "dn:c:" + nonce; }

        public static string IssueNonce(HttpSessionState session, string sessionToken)
        {
            if (session == null || !DiningTokens.IsValidToken(sessionToken)) return "";
            string n = session[NonceKey(sessionToken)] as string;
            if (string.IsNullOrEmpty(n))
            {
                var b = new byte[24];
                using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(b);
                n = BitConverter.ToString(b).Replace("-", "").ToLowerInvariant();
                session[NonceKey(sessionToken)] = n;
            }
            session[CtxKey(n)] = sessionToken;
            return n;
        }

        /// <summary>session token ที่ nonce นี้ผูกอยู่ — null = nonce ไม่ถูกต้อง/หมดอายุ</summary>
        public static string TokenForNonce(HttpSessionState session, string nonce)
        {
            if (session == null || string.IsNullOrEmpty(nonce) || nonce.Length != 48) return null;
            for (int i = 0; i < nonce.Length; i++)
            {
                char c = nonce[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return null;
            }
            string t = session[CtxKey(nonce)] as string;
            return DiningTokens.IsValidToken(t) ? t : null;
        }

        /// <summary>JSON สำหรับฝังใน &lt;script type="application/json"&gt; — กัน "&lt;/script" หลุด</summary>
        public static string EmbedJson(object o)
        {
            var js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            return js.Serialize(o).Replace("</", "<\\/").Replace("\u2028", "\\u2028").Replace("\u2029", "\\u2029");
        }

        // ── โมเดล → JSON (เฉพาะฟิลด์ที่ลูกค้าควรเห็น) ──
        private static string Iso(DateTime? d) { return d.HasValue ? d.Value.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) : null; }
        private static string Hm(TimeSpan? t) { return t.HasValue ? new DateTime(2000, 1, 1).Add(t.Value).ToString("HH:mm", CultureInfo.InvariantCulture) : null; }

        public static Dictionary<string, object> SessionJson(DiningTableSession s, bool zoneOpen, string zoneMsg)
        {
            if (s == null) return null;
            return new Dictionary<string, object>
            {
                { "id", s.Id }, { "table", s.TableName ?? s.TableCode }, { "tableCode", s.TableCode }, { "zone", s.ZoneName ?? s.ZoneCode }, { "zoneCode", s.ZoneCode },
                { "label", s.Label }, { "status", s.Status }, { "billRequested", s.Status == DiningStatus.Session.BillRequested },
                { "allowCash", s.AllowCash }, { "allowTransfer", s.AllowTransfer }, { "allowRoomCharge", s.AllowRoomCharge },
                { "guestName", s.GuestName }, { "guestPhone", s.GuestPhone },
                { "zoneOpen", zoneOpen }, { "zoneMsg", zoneMsg },
                { "openedAt", Iso(s.OpenedAt) }
            };
        }

        public static Dictionary<string, object> MenuJson(DiningMenu m, string zoneCode)
        {
            var cats = new List<object>();
            var items = new List<object>();
            var now = DateTime.Now;
            if (m != null)
            {
                foreach (var c in m.Categories)
                    cats.Add(new Dictionary<string, object> { { "id", c.Id }, { "name", c.NameTh }, { "nameEn", c.NameEn }, { "icon", c.Icon }, { "count", c.ItemCount } });
                foreach (var it in m.Items)
                {
                    if (!it.IsDining || !it.ProductActive || !it.InZone(zoneCode)) continue;
                    var groups = new List<object>();
                    foreach (var g in it.OptionGroups)
                    {
                        if (!g.IsActive) continue;
                        var opts = new List<object>();
                        foreach (var o in g.Options)
                            if (o.IsActive) opts.Add(new Dictionary<string, object> { { "id", o.Id }, { "name", o.NameTh }, { "delta", o.PriceDelta } });
                        if (opts.Count == 0) continue;
                        groups.Add(new Dictionary<string, object> { { "id", g.Id }, { "name", g.NameTh }, { "min", g.MinSelect }, { "max", g.MaxSelect }, { "req", g.IsRequired }, { "options", opts } });
                    }
                    var tags = new List<string>();
                    if (!string.IsNullOrWhiteSpace(it.AllergenTags))
                        foreach (string t in it.AllergenTags.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries))
                            if (t.Trim().Length > 0) tags.Add(t.Trim());
                    bool soldOut = !it.Orderable(zoneCode, now);
                    items.Add(new Dictionary<string, object>
                    {
                        { "id", it.ProductId }, { "name", it.Name ?? it.ProductName }, { "nameEn", it.NameEn }, { "desc", it.Description },
                        { "price", it.Price }, { "img", it.ImageUrl }, { "thumb", it.ThumbUrl ?? it.ImageUrl }, { "cat", it.CategoryId ?? 0 },
                        { "soldOut", soldOut }, { "soldOutUntil", it.SoldOutNow(now) && it.SoldOutUntil.HasValue ? it.SoldOutUntil.Value.ToString("HH:mm", CultureInfo.InvariantCulture) : null },
                        { "prep", it.PrepMinutes }, { "allergens", tags }, { "rec", it.IsRecommended },
                        { "trackStock", it.TrackStock }, { "stock", it.TrackStock ? it.StockQty : null }, { "groups", groups }
                    });
                }
            }
            return new Dictionary<string, object> { { "categories", cats }, { "items", items }, { "loadedAt", Iso(m != null ? m.LoadedAt : DateTime.Now) } };
        }

        public static Dictionary<string, object> QuoteJson(DiningQuote q)
        {
            var lines = new List<object>();
            if (q != null)
                foreach (var l in q.Lines)
                {
                    var opts = new List<object>();
                    foreach (var o in l.Options) opts.Add(new Dictionary<string, object> { { "id", o.OptionId }, { "name", o.Name }, { "group", o.GroupName }, { "delta", o.PriceDelta } });
                    lines.Add(new Dictionary<string, object>
                    {
                        { "pid", l.ProductId }, { "name", l.Name }, { "qty", l.Qty }, { "unit", l.UnitPrice }, { "optsPrice", l.OptionsPrice },
                        { "total", l.LineTotal }, { "opts", opts }, { "note", l.Note }
                    });
                }
            return new Dictionary<string, object>
            {
                { "ok", q != null && q.Ok }, { "code", q != null ? q.ErrorCode : null }, { "errors", q != null ? q.Errors : new List<string>() },
                { "lines", lines }, { "subtotal", q != null ? q.Subtotal : 0m }, { "total", q != null ? q.Total : 0m },
                { "vat", q != null ? q.VatAmount : 0m }, { "net", q != null ? q.NetAmount : 0m }, { "count", q != null ? q.ItemCount : 0 }
            };
        }

        public static Dictionary<string, object> OrderJson(DiningOrder o)
        {
            var items = new List<object>();
            foreach (var it in o.Items)
            {
                if (it.KitchenStatus == DiningStatus.Kitchen.Void) continue;
                items.Add(new Dictionary<string, object>
                {
                    { "name", it.ProductName }, { "qty", it.Qty }, { "unit", it.UnitPrice }, { "optsPrice", it.OptionsPrice }, { "total", it.LineTotal },
                    { "opts", it.OptionsText }, { "note", it.ItemNote }
                });
            }
            return new Dictionary<string, object>
            {
                { "id", o.Id }, { "no", o.OrderNo }, { "round", o.RoundNo }, { "status", o.OrderStatus }, { "statusTh", DiningStatus.Order.Thai(o.OrderStatus) },
                { "pay", o.PaymentStatus }, { "payTh", DiningStatus.Pay.Thai(o.PaymentStatus) }, { "pendingSlip", o.PendingPaymentId.HasValue && o.IsOutstanding },
                { "total", o.TotalAmount }, { "note", o.GuestNote }, { "createdAt", Iso(o.CreatedAt) }, { "acceptedAt", Iso(o.AcceptedAt) },
                { "servedAt", Iso(o.ServedAt) }, { "cancelledAt", Iso(o.CancelledAt) }, { "cancelReason", o.CancelReason },
                { "cancelled", o.IsCancelled }, { "settled", o.IsSettled }, { "items", items }
            };
        }

        public static Dictionary<string, object> PaymentJson(DiningPayment p)
        {
            return new Dictionary<string, object>
            {
                { "id", p.Id }, { "method", p.Method }, { "methodTh", DiningStatus.Method.Thai(p.Method) }, { "status", p.Status },
                { "amount", p.Amount }, { "paidHow", p.PaidHowName }, { "paidAt", Iso(p.PaidAt ?? p.CreatedAt) },
                { "pending", p.Status == DiningStatus.Payment.PendingSlip }, { "rejected", p.Status == DiningStatus.Payment.Rejected },
                { "confirmed", p.Status == DiningStatus.Payment.Confirmed }, { "roomRef", p.RoomChargeRef }, { "note", p.SlipNote }
            };
        }

        public static Dictionary<string, object> SummaryJson(SessionSummary s)
        {
            var orders = new List<object>();
            foreach (var o in s.Orders) orders.Add(OrderJson(o));
            var pays = new List<object>();
            foreach (var p in s.Payments) if (p.Status != DiningStatus.Payment.Void) pays.Add(PaymentJson(p));
            return new Dictionary<string, object>
            {
                { "ok", true }, { "v", s.VersionTicks }, { "session", SessionJson(s.Session, s.ZoneOpen, s.ZoneClosedMessage) },
                { "orders", orders }, { "payments", pays },
                { "outstanding", new Dictionary<string, object>
                    {
                        { "amount", s.Outstanding.Amount }, { "payable", s.Outstanding.PayableAmount }, { "pendingSlip", s.Outstanding.PendingSlipAmount },
                        { "payableIds", s.Outstanding.PayableOrderIds }, { "pendingIds", s.Outstanding.PendingSlipOrderIds }
                    } },
                { "totalOrdered", s.TotalOrdered }, { "totalPaid", s.TotalPaid }, { "billRequested", s.BillRequested },
                { "zoneOpen", s.ZoneOpen }, { "zoneMsg", s.ZoneClosedMessage }
            };
        }

        public static Dictionary<string, object> ZoneHoursJson(DiningZone z)
        {
            if (z == null) return null;
            return new Dictionary<string, object> { { "name", z.NameTh }, { "open", Hm(z.OpenTime) }, { "close", Hm(z.CloseTime) }, { "mode", z.ManualMode }, { "closedMessage", z.ClosedMessage } };
        }

        // ── การ์ดบัญชีรับโอน (ลอกแบบ Reserve.aspx.SlipFirst.cs:707-751) ──
        public sealed class ReceiverCard
        {
            public int PaidHowId;
            public string Name;
            public string BankName;
            public string AccountNo;
            public string PromptPayId;
            public string QrImageUrl;
        }

        /// <summary>บัญชีรับโอนที่ลูกค้าเห็น: แคตตาล็อกช่องทาง DINING (TRANSFER/QR ไม่ใช่เกตเวย์) + เลขบัญชี/พร้อมเพย์จาก LoadReceiverAccounts</summary>
        public static List<ReceiverCard> ReceiverCards()
        {
            var list = new List<ReceiverCard>();
            List<SC.SlipVerificationService.ReceiverAccount> accounts;
            try { accounts = SC.SlipVerificationService.LoadReceiverAccounts() ?? new List<SC.SlipVerificationService.ReceiverAccount>(); }
            catch { accounts = new List<SC.SlipVerificationService.ReceiverAccount>(); }
            var seen = new HashSet<int>();
            try
            {
                foreach (var ch in Take_Time_BangPhra.Payments.PaymentChannelCatalog.ForCustomer(DiningConfig.SalesChannel))
                {
                    if (ch == null || ch.IsGateway) continue;
                    string t = (ch.Type ?? "").ToUpperInvariant();
                    if (t != "TRANSFER" && t != "QR") continue;
                    if (ch.PaidHowId <= 0 || !seen.Add(ch.PaidHowId)) continue;
                    var card = new ReceiverCard { PaidHowId = ch.PaidHowId, Name = ch.Name ?? ch.PaidHowName, QrImageUrl = SafeImageUrl(ch.QrImageUrl) };
                    foreach (var a in accounts)
                    {
                        if (a == null || a.PaidHowId != ch.PaidHowId) continue;
                        card.BankName = BankName(a.BankCode); card.AccountNo = a.AccountNo; card.PromptPayId = a.PromptPayId;
                        break;
                    }
                    list.Add(card);
                }
            }
            catch { }
            if (list.Count == 0)
            {
                foreach (var a in accounts)
                {
                    if (a == null || (string.IsNullOrEmpty(a.AccountNo) && string.IsNullOrEmpty(a.PromptPayId))) continue;
                    if (!seen.Add(a.PaidHowId)) continue;
                    list.Add(new ReceiverCard { PaidHowId = a.PaidHowId, Name = a.PaidHowName, BankName = BankName(a.BankCode), AccountNo = a.AccountNo, PromptPayId = a.PromptPayId });
                }
            }
            return list;
        }

        public static string ReceiverCardsHtml(List<ReceiverCard> cards)
        {
            var sb = new StringBuilder();
            bool any = false;
            foreach (var c in cards)
            {
                bool has = !string.IsNullOrEmpty(c.AccountNo) || !string.IsNullOrEmpty(c.PromptPayId) || !string.IsNullOrEmpty(c.QrImageUrl);
                sb.Append("<div class=\"dn-acct\" data-paidhow=\"").Append(c.PaidHowId).Append("\">");
                sb.Append("<div class=\"dn-acct-name\">").Append(HttpUtility.HtmlEncode(c.Name ?? "บัญชีรับโอน")).Append("</div>");
                if (!string.IsNullOrEmpty(c.AccountNo))
                    sb.Append(Line(c.BankName ?? "เลขบัญชี", "เลขบัญชี", FormatAccount(c.AccountNo), c.AccountNo, "คัดลอกเลขบัญชี"));
                if (!string.IsNullOrEmpty(c.PromptPayId))
                    sb.Append(Line("พร้อมเพย์", null, FormatPromptPay(c.PromptPayId), c.PromptPayId, "คัดลอกพร้อมเพย์"));
                if (!string.IsNullOrEmpty(c.QrImageUrl))
                    sb.Append("<details class=\"dn-acct-qr\"><summary>แสดง QR สำหรับสแกนจ่าย</summary><img alt=\"QR รับเงิน\" src=\"")
                      .Append(HttpUtility.HtmlAttributeEncode(c.QrImageUrl)).Append("\" /></details>");
                if (!has) sb.Append("<div class=\"dn-muted\">สอบถามเลขบัญชีจากพนักงาน</div>");
                sb.Append("</div>");
                any = true;
            }
            if (!any)
                sb.Append("<div class=\"dn-acct\"><img class=\"dn-acct-img\" alt=\"บัญชีสำหรับโอนเงิน\" src=\"/Images/บัญชี.png\" /><div class=\"dn-muted\">สอบถามเลขบัญชีจากพนักงานได้</div></div>");
            return sb.ToString();
        }

        private static string Line(string label, string sub, string value, string copy, string copyLabel)
        {
            var sb = new StringBuilder("<div class=\"dn-line\"><div class=\"dn-line-l\"><b>").Append(HttpUtility.HtmlEncode(label)).Append("</b>");
            if (!string.IsNullOrEmpty(sub)) sb.Append("<small>").Append(HttpUtility.HtmlEncode(sub)).Append("</small>");
            sb.Append("</div><div class=\"dn-line-v dn-mono\">").Append(HttpUtility.HtmlEncode(value)).Append("</div>");
            if (!string.IsNullOrEmpty(copy))
                sb.Append("<button type=\"button\" class=\"dn-copy\" data-sf-copy=\"").Append(HttpUtility.HtmlAttributeEncode(copy)).Append("\" aria-label=\"")
                  .Append(HttpUtility.HtmlAttributeEncode(copyLabel)).Append("\">คัดลอก</button>");
            return sb.Append("</div>").ToString();
        }

        public static string SafeImageUrl(string url)
        {
            string u = (url ?? "").Trim();
            if (u.Length == 0 || u.Length > 500) return "";
            if (u.StartsWith("/", StringComparison.Ordinal) || u.StartsWith("~/", StringComparison.Ordinal)) return u.StartsWith("~/", StringComparison.Ordinal) ? u.Substring(1) : u;
            Uri abs;
            if (Uri.TryCreate(u, UriKind.Absolute, out abs) && (abs.Scheme == Uri.UriSchemeHttps || abs.Scheme == Uri.UriSchemeHttp)) return u;
            return "";
        }

        public static string BankName(string code)
        {
            string c = (code ?? "").Trim().ToUpperInvariant();
            switch (c)
            {
                case "002": case "BBL": return "ธนาคารกรุงเทพ";
                case "004": case "KBANK": return "ธนาคารกสิกรไทย";
                case "006": case "KTB": return "ธนาคารกรุงไทย";
                case "011": case "TTB": return "ธนาคารทหารไทยธนชาต (ttb)";
                case "014": case "SCB": return "ธนาคารไทยพาณิชย์";
                case "022": case "CIMBT": return "ธนาคารซีไอเอ็มบี ไทย";
                case "024": case "UOBT": return "ธนาคารยูโอบี";
                case "025": case "BAY": return "ธนาคารกรุงศรีอยุธยา";
                case "030": case "GSB": return "ธนาคารออมสิน";
                case "033": case "GHB": return "ธนาคารอาคารสงเคราะห์";
                case "034": case "BAAC": return "ธ.ก.ส.";
                case "069": case "KKP": return "ธนาคารเกียรตินาคินภัทร";
                case "073": case "LHB": return "ธนาคารแลนด์ แอนด์ เฮ้าส์";
                default: return c.Length > 0 ? "ธนาคาร " + c : "เลขบัญชี";
            }
        }

        public static string FormatAccount(string digits)
        {
            if (string.IsNullOrEmpty(digits)) return "";
            if (digits.Length == 10) return digits.Substring(0, 3) + "-" + digits.Substring(3, 1) + "-" + digits.Substring(4, 5) + "-" + digits.Substring(9, 1);
            return digits;
        }

        public static string FormatPromptPay(string digits)
        {
            if (string.IsNullOrEmpty(digits)) return "";
            if (digits.Length == 10) return digits.Substring(0, 3) + "-" + digits.Substring(3, 3) + "-" + digits.Substring(6);
            if (digits.Length == 13) return digits.Substring(0, 1) + "-" + digits.Substring(1, 4) + "-" + digits.Substring(5, 5) + "-" + digits.Substring(10, 2) + "-" + digits.Substring(12);
            return digits;
        }

        public static string ContactHtml()
        {
            try { return SC.SlipAmountLockUi.ContactButtonsHtml() ?? ""; } catch { return ""; }
        }

        public static void Log(string action, string detail)
        {
            try { new code().Logs(ConnStr, action, detail, "GUEST"); } catch { }
        }
    }

    /// <summary>
    /// JSON API ของหน้าลูกค้า QR สั่งอาหาร (CLAUDE.md/README_API.md ข้อ 9) — POST อย่างเดียว, form fields:
    ///   a = menu|quote|place|status|call|bill|slip|roomcandidates|roomcharge · nonce (จากหน้า) · อื่น ๆ ตาม action
    /// ความปลอดภัย (ลอก API/SlipCheck.ashx): header X-Dining:1 + Origin/Referer โฮสต์เดียวกัน · nonce ผูก session token ใน ASP.NET Session
    ///   (ไม่เชื่อ token จากเบราว์เซอร์) · จำกัดความถี่ต่อ session/IP (SlipUploadStore.TryRate) · สลิป ≤ 8 MB + magic bytes
    /// ราคา/ยอดไม่รับจาก client — service คิดใหม่เสมอ · ทุกข้อผิดพลาด = {ok:false, code, message} ไทย ไม่รั่ว exception
    /// </summary>
    public class DiningOrderApi : IHttpHandler, IRequiresSessionState
    {
        public bool IsReusable { get { return false; } }

        private static readonly TimeSpan RateWindow = TimeSpan.FromMinutes(10);
        private const int RatePerIpMutations = 120;

#pragma warning disable 0649   // ฟิลด์ถูกเติมโดย JavaScriptSerializer.Deserialize — ไม่มีโค้ดกำหนดค่าตรง
        private sealed class CartLineDto
        {
            public int pid;
            public decimal qty;
            public int[] opt;
            public string note;
        }
#pragma warning restore 0649

        public void ProcessRequest(HttpContext ctx)
        {
            var res = ctx.Response;
            res.ContentType = "application/json; charset=utf-8";
            res.Cache.SetCacheability(HttpCacheability.NoCache);
            res.Cache.SetNoStore();
            res.AddHeader("X-Content-Type-Options", "nosniff");
            string action = "";
            try
            {
                var req = ctx.Request;
                if (!string.Equals(req.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)) { Write(ctx, 405, Fail("method", "ใช้ POST เท่านั้น")); return; }
                if (req.Headers["X-Dining"] != "1" || !SameOrigin(req)) { Write(ctx, 403, Fail("forbidden", "คำขอไม่ถูกต้อง")); return; }
                if (!DiningConfig.Enabled) { Write(ctx, 200, Fail(DiningError.FeatureOff, "ระบบสั่งอาหารผ่าน QR ยังไม่เปิดให้บริการ")); return; }
                HttpSessionState session = ctx.Session;
                if (session == null) { Write(ctx, 403, Fail("forbidden", "คำขอไม่ถูกต้อง")); return; }

                action = (req.Form["a"] ?? "").Trim().ToLowerInvariant();
                string token = DiningGuest.TokenForNonce(session, req.Form["nonce"] ?? req.Headers["X-Dining-Nonce"]);
                if (token == null)
                {
                    // nonce หมดอายุ (ASP.NET session หมด) แต่ cookie ยังอยู่ — ให้หน้ารีโหลดเพื่อออก nonce ใหม่
                    Write(ctx, 200, Fail("expired", "หน้านี้เปิดค้างนานเกินไป — กำลังโหลดใหม่", true));
                    return;
                }
                string cookie = DiningGuest.CookieToken(req);
                if (cookie != null && !DiningTokens.ConstantTimeEquals(cookie, token))
                {
                    // เครื่องเดิมสแกนโต๊ะอื่น (cookie เปลี่ยน) — หน้าเก่าใช้ต่อไม่ได้
                    Write(ctx, 200, Fail("expired", "เปิดโต๊ะใหม่ในเครื่องนี้แล้ว — กำลังโหลดใหม่", true));
                    return;
                }

                string ip = DiningGuest.ClientIp(req);
                bool mutating = action != "status" && action != "menu" && action != "quote";
                int perSession;
                switch (action)
                {
                    case "place": perSession = 10; break;
                    case "slip": perSession = 5; break;
                    case "call": perSession = 3; break;
                    case "roomcharge": perSession = 8; break;
                    case "bill": perSession = 10; break;
                    case "roomcandidates": perSession = 20; break;
                    case "quote": perSession = 120; break;
                    case "menu": perSession = 60; break;
                    case "status": perSession = 0; break;   // poll ทุก 2-60 วิ — จำกัดโดยช่วง poll ของหน้าเอง
                    default: Write(ctx, 400, Fail("bad", "ไม่รู้จักคำสั่ง")); return;
                }
                if (perSession > 0 && !SC.SlipUploadStore.TryRate("dn:s:" + action + ":" + token, perSession, RateWindow))
                {
                    Write(ctx, 429, Fail(DiningError.RateLimited, action == "call" ? "เรียกพนักงานไปแล้วหลายครั้ง — พนักงานกำลังมา รอสักครู่นะคะ" : "ส่งคำขอถี่เกินไป — รอสักครู่แล้วลองใหม่"));
                    return;
                }
                if (mutating && !SC.SlipUploadStore.TryRate("dn:ip:" + ip, RatePerIpMutations, RateWindow))
                {
                    Write(ctx, 429, Fail(DiningError.RateLimited, "มีการใช้งานหนาแน่น — รอสักครู่แล้วลองใหม่"));
                    return;
                }

                switch (action)
                {
                    case "menu": DoMenu(ctx, token); break;
                    case "quote": DoQuote(ctx, token, req); break;
                    case "place": DoPlace(ctx, token, req, ip); break;
                    case "status": DoStatus(ctx, token, req); break;
                    case "call": DoCall(ctx, token, req); break;
                    case "bill": DoBill(ctx, token); break;
                    case "slip": DoSlip(ctx, token, req, ip); break;
                    case "roomcandidates": DoRoomCandidates(ctx, token); break;
                    case "roomcharge": DoRoomCharge(ctx, token, req, ip); break;
                }
            }
            catch (Exception ex)
            {
                DiningGuest.Log("DiningOrder API", action + ": " + ex.GetType().Name + ": " + ex.Message);
                Write(ctx, 500, Fail(DiningError.Internal, "ระบบขัดข้องชั่วคราว กรุณาลองใหม่ หรือเรียกพนักงาน"));
            }
        }

        // ── actions ──

        private static void DoMenu(HttpContext ctx, string token)
        {
            var s = new DiningTableService().GetSession(token);
            if (s == null) { Closed(ctx); return; }
            var menu = new DiningMenuService().GetMenu(s.ZoneCode);
            var j = new Dictionary<string, object> { { "ok", true }, { "menu", DiningGuest.MenuJson(menu, s.ZoneCode) }, { "minOrder", DiningConfig.MinOrder } };
            Write(ctx, 200, j);
        }

        private static List<DiningCartLine> ParseCart(string json, out string error)
        {
            error = null;
            var cart = new List<DiningCartLine>();
            if (string.IsNullOrWhiteSpace(json)) { error = "ตะกร้าว่าง"; return cart; }
            if (json.Length > 64 * 1024) { error = "ตะกร้าใหญ่เกินไป"; return cart; }
            List<CartLineDto> raw;
            try { raw = new JavaScriptSerializer().Deserialize<List<CartLineDto>>(json); }
            catch { error = "ข้อมูลตะกร้าไม่ถูกต้อง — โหลดหน้าใหม่แล้วลองอีกครั้ง"; return cart; }
            if (raw == null || raw.Count == 0) { error = "ตะกร้าว่าง"; return cart; }
            if (raw.Count > DiningConfig.MaxLines) { error = "รายการในตะกร้าเกิน " + DiningConfig.MaxLines + " บรรทัด"; return cart; }
            foreach (var l in raw)
            {
                if (l == null) continue;
                cart.Add(new DiningCartLine
                {
                    ProductId = l.pid,
                    Qty = decimal.Truncate(l.qty),
                    OptionIds = l.opt ?? new int[0],
                    Note = string.IsNullOrWhiteSpace(l.note) ? null : (l.note.Length > 300 ? l.note.Substring(0, 300) : l.note).Trim()
                });
            }
            return cart;
        }

        private static void DoQuote(HttpContext ctx, string token, HttpRequest req)
        {
            string err;
            var cart = ParseCart(req.Form["cart"], out err);
            if (err != null) { Write(ctx, 200, Fail(DiningError.Validation, err)); return; }
            var q = new DiningOrderService().Quote(token, cart);
            if (q.ErrorCode == DiningError.SessionClosed) { Closed(ctx); return; }
            var j = DiningGuest.QuoteJson(q);
            if (!q.Ok) j["message"] = string.Join("\n", q.Errors.ToArray());
            Write(ctx, 200, j);
        }

        private static void DoPlace(HttpContext ctx, string token, HttpRequest req, string ip)
        {
            string err;
            var cart = ParseCart(req.Form["cart"], out err);
            if (err != null) { Write(ctx, 200, Fail(DiningError.Validation, err)); return; }
            string note = Trunc(req.Form["note"], 500);
            string name = Trunc(req.Form["name"], 100);
            string phone = Trunc(req.Form["phone"], 30);
            string method = (req.Form["method"] ?? "").Trim().ToUpperInvariant();
            if (method != DiningStatus.Method.Cash && method != DiningStatus.Method.Transfer && method != DiningStatus.Method.RoomCharge) method = null;
            if (method == DiningStatus.Method.Transfer && DiningTokens.NormalizePhone(phone).Length < 9)
            {
                var f = Fail(DiningError.Validation, "กรุณากรอกเบอร์โทรศัพท์สำหรับติดต่อเมื่อเลือกโอนเงิน");
                f["field"] = "phone";
                Write(ctx, 200, f);
                return;
            }
            string idem = DiningTokens.SanitizeIdempotencyKey(req.Form["idem"]);
            if (idem == null) { Write(ctx, 200, Fail(DiningError.Validation, "คีย์กันซ้ำไม่ถูกต้อง — โหลดหน้าใหม่แล้วลองอีกครั้ง")); return; }

            var svc = new DiningOrderService();
            var r = svc.PlaceOrder(token, cart, note, name, phone, idem, ip, method, DiningStatus.Source.Qr);
            if (!r.Ok)
            {
                if (r.ErrorCode == DiningError.SessionClosed) { Closed(ctx); return; }
                var f = Fail(r.ErrorCode ?? DiningError.Validation, r.Message ?? "สั่งไม่สำเร็จ");
                if (r.Quote != null) f["quote"] = DiningGuest.QuoteJson(r.Quote);
                Write(ctx, 200, f);
                return;
            }
            try
            {
                if (!string.IsNullOrEmpty(name) || !string.IsNullOrEmpty(phone))
                    new DiningTableService().SetGuestInfo(r.Order.SessionId, string.IsNullOrEmpty(name) ? null : name, string.IsNullOrEmpty(phone) ? null : phone, null);
            }
            catch { }
            var j = new Dictionary<string, object>
            {
                { "ok", true }, { "duplicate", r.Duplicate }, { "orderId", r.Order.Id }, { "orderNo", r.Order.OrderNo }, { "round", r.Order.RoundNo },
                { "total", r.Order.TotalAmount }, { "message", r.Duplicate ? "รับออเดอร์นี้ไว้แล้ว" : "ส่งออเดอร์เข้าครัวแล้ว" },
                { "statusUrl", VirtualPathUtility.ToAbsolute("~/Order/Status") + (method == DiningStatus.Method.Transfer ? "#pay=transfer" : "") }
            };
            Write(ctx, 200, j);
        }

        private static void DoStatus(HttpContext ctx, string token, HttpRequest req)
        {
            var svc = new DiningOrderService();
            var sum = svc.GetSessionSummary(token);
            if (!sum.Ok)
            {
                if (sum.ErrorCode == DiningError.SessionClosed) { Closed(ctx); return; }
                Write(ctx, 200, Fail(sum.ErrorCode ?? DiningError.Internal, sum.Message ?? "โหลดสถานะไม่ได้"));
                return;
            }
            try { new DiningTableService().TouchSession(sum.Session.Id); } catch { }
            long v;
            if (long.TryParse(req.Form["v"], NumberStyles.Integer, CultureInfo.InvariantCulture, out v) && v == sum.VersionTicks && req.Form["full"] != "1")
            {
                Write(ctx, 200, new Dictionary<string, object> { { "ok", true }, { "same", true }, { "v", v } });
                return;
            }
            Write(ctx, 200, DiningGuest.SummaryJson(sum));
        }

        private static void DoCall(HttpContext ctx, string token, HttpRequest req)
        {
            string msg = Trunc(req.Form["msg"], 200);
            var r = new DiningOrderService().CallStaff(token, msg);
            if (!r.Ok && r.ErrorCode == DiningError.SessionClosed) { Closed(ctx); return; }
            Write(ctx, 200, Result(r));
        }

        private static void DoBill(HttpContext ctx, string token)
        {
            var r = new DiningTableService().RequestBill(token);
            if (!r.Ok && r.ErrorCode == DiningError.SessionClosed) { Closed(ctx); return; }
            var j = Result(r);
            if (r.Ok)
            {
                var sum = new DiningOrderService().GetSessionSummary(token);
                if (sum.Ok) j["summary"] = DiningGuest.SummaryJson(sum);
            }
            Write(ctx, 200, j);
        }

        private static void DoSlip(HttpContext ctx, string token, HttpRequest req, string ip)
        {
            HttpPostedFile f = req.Files["file"];
            if (f == null || f.ContentLength <= 0) { Write(ctx, 200, Fail(DiningError.Validation, "ไม่พบไฟล์สลิป — เลือกรูปอีกครั้ง")); return; }
            if (f.ContentLength > SC.SlipUploadStore.MaxBytes)
            {
                Write(ctx, 200, Fail(DiningError.Validation, "ไฟล์ใหญ่เกินไป (เกิน " + (SC.SlipUploadStore.MaxBytes / 1024 / 1024) + " MB) — ลองแคปหน้าจอสลิปแทน"));
                return;
            }
            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                f.InputStream.CopyTo(ms);
                bytes = ms.ToArray();
            }
            if (SC.SlipUploadStore.SniffImage(bytes) == null) { Write(ctx, 200, Fail(DiningError.Validation, "ไฟล์นี้ไม่ใช่รูปภาพ — กรุณาแนบรูปสลิป (JPG / PNG)")); return; }
            int paidHow;
            if (!int.TryParse(req.Form["paidHow"], out paidHow) || paidHow < 0) paidHow = 0;
            string fileName = Path.GetFileName(f.FileName ?? "slip.jpg");
            if (string.IsNullOrEmpty(fileName)) fileName = "slip.jpg";

            var o = new DiningPaymentService().SubmitTransferSlip(token, bytes, fileName, null, paidHow, ip);
            if (!o.Ok && !o.Rejected && o.ErrorCode == DiningError.SessionClosed) { Closed(ctx); return; }
            var j = new Dictionary<string, object>
            {
                { "ok", o.Ok }, { "code", o.ErrorCode }, { "message", o.Message ?? "" }, { "verified", o.Verified }, { "pending", o.Pending },
                { "rejected", o.Rejected }, { "duplicate", o.DuplicateSlip }, { "reason", o.Reason }, { "slipAmount", o.SlipAmount },
                { "amount", o.Payment != null ? o.Payment.Amount : (decimal?)null }, { "paidHow", o.Payment != null ? o.Payment.PaidHowName : null },
                { "covered", o.CoveredOrderIds }
            };
            if (o.Rejected) j["contactHtml"] = DiningGuest.ContactHtml();
            if (o.Ok)
            {
                var sum = new DiningOrderService().GetSessionSummary(token);
                if (sum.Ok) j["summary"] = DiningGuest.SummaryJson(sum);
            }
            Write(ctx, 200, j);
        }

        private static void DoRoomCandidates(HttpContext ctx, string token)
        {
            var s = new DiningTableService().GetSession(token);
            if (s == null) { Closed(ctx); return; }
            if (!s.AllowRoomCharge) { Write(ctx, 200, Fail(DiningError.Forbidden, "โซนนี้ไม่เปิดให้ลงบิลห้องพัก")); return; }
            var list = new List<object>();
            foreach (var c in new DiningPaymentService().ListRoomChargeCandidates())
                list.Add(new Dictionary<string, object> { { "id", c.ReservationId }, { "room", c.RoomLabel } });
            Write(ctx, 200, new Dictionary<string, object> { { "ok", true }, { "rooms", list }, { "locked", s.RoomChargeLockedUntil.HasValue && s.RoomChargeLockedUntil.Value > DateTime.Now ? s.RoomChargeLockedUntil.Value.ToString("HH:mm", CultureInfo.InvariantCulture) : null } });
        }

        private static void DoRoomCharge(HttpContext ctx, string token, HttpRequest req, string ip)
        {
            int rid;
            if (!int.TryParse(req.Form["rid"], out rid) || rid <= 0) { Write(ctx, 200, Fail(DiningError.Validation, "กรุณาเลือกห้องพัก")); return; }
            string phone = Trunc(req.Form["phone"], 30);
            var r = new DiningPaymentService().ChargeToRoom(token, rid, phone, ip);
            if (!r.Ok && r.ErrorCode == DiningError.SessionClosed) { Closed(ctx); return; }
            var j = Result(r);
            j["room"] = r.RoomLabel;
            j["attemptsLeft"] = r.AttemptsLeft;
            j["lockedUntil"] = r.LockedUntil.HasValue ? r.LockedUntil.Value.ToString("HH:mm", CultureInfo.InvariantCulture) : null;
            j["amount"] = r.Payment != null ? r.Payment.Amount : (decimal?)null;
            if (r.Ok)
            {
                var sum = new DiningOrderService().GetSessionSummary(token);
                if (sum.Ok) j["summary"] = DiningGuest.SummaryJson(sum);
            }
            Write(ctx, 200, j);
        }

        // ── helpers ──

        /// <summary>โต๊ะปิดบิลแล้ว — ลบ cookie ให้หน้าแสดง "สแกน QR ใหม่"</summary>
        private static void Closed(HttpContext ctx)
        {
            DiningGuest.ClearCookie(ctx.Response);
            var f = Fail(DiningError.SessionClosed, "โต๊ะนี้ปิดบิลแล้ว — สแกน QR ที่โต๊ะใหม่เพื่อสั่งอีกครั้ง");
            f["closed"] = true;
            Write(ctx, 200, f);
        }

        private static string Trunc(string s, int max)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
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
            return true;   // ไม่มีทั้งคู่ (เบราว์เซอร์เก่า/ปิด referrer) — header + nonce ใน session กันแล้ว
        }

        private static Dictionary<string, object> Result(DiningResult r)
        {
            return new Dictionary<string, object> { { "ok", r.Ok }, { "code", r.ErrorCode }, { "message", r.Message ?? "" } };
        }

        private static Dictionary<string, object> Fail(string code, string msg, bool reload = false)
        {
            var d = new Dictionary<string, object> { { "ok", false }, { "code", code }, { "message", msg ?? "" } };
            if (reload) d["reload"] = true;
            return d;
        }

        private static void Write(HttpContext ctx, int status, Dictionary<string, object> json)
        {
            ctx.Response.StatusCode = status;
            ctx.Response.Write(new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(json));
        }
    }
}
