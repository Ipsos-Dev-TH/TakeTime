using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra.Payments.SlipCheck
{
    /// <summary>
    /// ตัวช่วยหน้าเว็บของ "ยอดล็อกตามสลิป" (PHASE19_36): ล็อก/ปลดล็อกช่องยอด, สคริปต์กันแก้, ตรวจสลิปแบบ AJAX
    /// เซิร์ฟเวอร์ยังบังคับยอดซ้ำตอนบันทึกเสมอ (SlipVerificationService.VerifyWithLock / Decide) — ตรงนี้แค่ UX
    /// </summary>
    public static class SlipAmountLockUi
    {
        private const string Css =
            ".slip-locked{background:#eef7ee !important;border-color:#2e7d32 !important;color:#1b5e20 !important;font-weight:700;cursor:not-allowed;}"
            + ".slip-lock-msg{display:block;margin-top:6px;padding:6px 10px;border-radius:6px;font-size:13.5px;line-height:1.6;max-width:560px;}"
            + ".slip-lock-msg.ok{background:#e8f5e9;color:#1b5e20;border:1px dashed #2e7d32;}"
            + ".slip-lock-msg.warn{background:#fff8e1;color:#e65100;border:1px dashed #e65100;}"
            + ".slip-lock-msg.wait{background:#eceff1;color:#455a64;}"
            + "@media (max-width:600px){.slip-lock-msg{max-width:100%;font-size:13px;}}";

        /// <summary>ใส่สคริปต์ + สไตล์ (ครั้งเดียวต่อหน้า)</summary>
        public static void Register(Page page)
        {
            if (page == null) return;
            try
            {
                var cs = page.ClientScript;
                if (!cs.IsClientScriptIncludeRegistered(typeof(SlipAmountLockUi), "slipLockJs"))
                    cs.RegisterClientScriptInclude(typeof(SlipAmountLockUi), "slipLockJs", page.ResolveUrl("~/Scripts/slip-amount-lock.js?v=38"));
                if (!cs.IsStartupScriptRegistered(typeof(SlipAmountLockUi), "slipLockCss"))
                    cs.RegisterStartupScript(typeof(SlipAmountLockUi), "slipLockCss",
                        "(function(){if(document.getElementById('slipLockCss'))return;var s=document.createElement('style');s.id='slipLockCss';s.appendChild(document.createTextNode("
                        + HttpUtility.JavaScriptStringEncode(Css, true) + "));document.head.appendChild(s);})();", true);
            }
            catch { }
        }

        public static string AmountText(decimal v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>
        /// สลิปก่อนยอด (หน้าจอง/รับชำระ): ใส่ Content/slip-first.css + Scripts/slip-first.js (ครั้งเดียวต่อหน้า)
        /// css ใส่ผ่านสคริปต์เพราะหน้าที่ใช้มาสเตอร์ไม่มี head ของตัวเอง — ไม่มีไฟล์/สคริปต์ปิด = หน้าตาเดิม (ไม่มีกล่องลากวาง)
        /// </summary>
        public static void RegisterSlipFirst(Page page)
        {
            if (page == null) return;
            try
            {
                var cs = page.ClientScript;
                if (!cs.IsClientScriptIncludeRegistered(typeof(SlipAmountLockUi), "slipFirstJs"))
                    cs.RegisterClientScriptInclude(typeof(SlipAmountLockUi), "slipFirstJs", page.ResolveUrl("~/Scripts/slip-first.js?v=2"));
                if (!cs.IsClientScriptBlockRegistered(typeof(SlipAmountLockUi), "slipFirstCss"))
                    cs.RegisterClientScriptBlock(typeof(SlipAmountLockUi), "slipFirstCss",
                        "(function(){if(document.getElementById('slipFirstCss'))return;var l=document.createElement('link');l.id='slipFirstCss';l.rel='stylesheet';l.href='"
                        + HttpUtility.JavaScriptStringEncode(page.ResolveUrl("~/Content/slip-first.css?v=2")) + "';document.getElementsByTagName('head')[0].appendChild(l);})();", true);
            }
            catch { }
        }

        /// <summary>
        /// ปุ่มติดต่อเจ้าหน้าที่ (โทร / LINE) จากข้อความ CustomerContactText — ใช้ในการ์ด "สลิปใช้ไม่ได้" ของลูกค้า
        /// ไม่พบเบอร์/LINE = คืนข้อความติดต่อแบบเดิม (ว่างได้)
        /// </summary>
        public static string ContactButtonsHtml()
        {
            string text = "";
            try { text = SlipVerificationService.CustomerContactText() ?? ""; } catch { }
            text = text.Trim();
            if (text.Length == 0) return "";
            var sb = new System.Text.StringBuilder();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, @"(?<!\d)0\d[\d\- ]{7,11}\d"))
            {
                string digits = System.Text.RegularExpressions.Regex.Replace(m.Value, @"\D", "");
                if (digits.Length < 9 || digits.Length > 10 || !seen.Add(digits)) continue;
                sb.Append("<a href=\"tel:").Append(digits).Append("\">📞 โทร ").Append(HttpUtility.HtmlEncode(m.Value.Trim())).Append("</a>");
            }
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, @"@[A-Za-z0-9_.\-]{2,40}"))
            {
                if (!seen.Add(m.Value)) continue;
                sb.Append("<a class=\"sf-line-btn\" href=\"https://line.me/R/ti/p/").Append(HttpUtility.UrlEncode(m.Value))
                  .Append("\" target=\"_blank\" rel=\"noopener\">💬 LINE ").Append(HttpUtility.HtmlEncode(m.Value)).Append("</a>");
            }
            if (sb.Length == 0)
                return "<div class=\"sf-note\" style=\"margin-top:6px;white-space:pre-line;\">" + HttpUtility.HtmlEncode(text) + "</div>";
            return "<div class=\"sf-contact\">" + sb + "</div>";
        }

        /// <summary>
        /// ล็อก (ใส่ยอดสลิป + readonly + data-slip-locked) หรือปลดล็อก (เฉพาะที่ตัวช่วยนี้ล็อกไว้ — readonly เดิมของหน้าไม่แตะ)
        /// ใช้ attribute readonly ไม่ใช่ TextBox.ReadOnly — กล่องยังรับค่าที่โพสต์ (ติ๊กแบ่งสลิปแล้วกรอกยอดส่วนของการจองนี้ได้)
        /// </summary>
        public static void Apply(TextBox tb, SlipAmountLock lk)
        {
            if (tb == null) return;
            if (lk != null && lk.Locked && lk.Amount.HasValue && lk.Amount.Value > 0m)
            {
                string t = AmountText(lk.Amount.Value);
                tb.Text = t;
                if (!tb.ReadOnly) tb.Attributes["readonly"] = "readonly";
                tb.Attributes["data-slip-locked"] = "1";
                tb.Attributes["data-slip-value"] = t;
                tb.Attributes["title"] = lk.BadgeText() + " — แก้ไม่ได้";
                if ((" " + tb.CssClass + " ").IndexOf(" slip-locked ", StringComparison.Ordinal) < 0)
                    tb.CssClass = (tb.CssClass + " slip-locked").Trim();
                if (tb.Page != null) Register(tb.Page);
            }
            else
            {
                if (tb.Attributes["data-slip-locked"] != null || tb.Attributes["data-slip-value"] != null)
                {
                    tb.Attributes.Remove("readonly");
                    tb.Attributes.Remove("title");
                }
                tb.Attributes.Remove("data-slip-locked");
                tb.Attributes.Remove("data-slip-value");
                tb.CssClass = (" " + tb.CssClass + " ").Replace(" slip-locked ", " ").Trim();
            }
        }

        /// <summary>ติ๊ก "แบ่งสลิป" = ปลดล็อกฝั่งเบราว์เซอร์ให้กรอกยอดส่วนของการจองนี้ (เซิร์ฟเวอร์ตรวจว่าไม่เกินยอดสลิป)</summary>
        public static void BindAllocate(Page page, TextBox tb, CheckBox chk)
        {
            if (page == null || tb == null || chk == null) return;
            Register(page);
            page.ClientScript.RegisterStartupScript(typeof(SlipAmountLockUi), "slipAlloc_" + tb.ClientID,
                "(function(){function go(){if(window.SlipLock)SlipLock.bindAllocate('" + HttpUtility.JavaScriptStringEncode(tb.ClientID) + "','"
                + HttpUtility.JavaScriptStringEncode(chk.ClientID) + "');}if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',go);else go();})();",
                true);
        }

        /// <summary>ตรวจสลิปทันทีเมื่อเลือกไฟล์ (AJAX — หน้าที่ไม่มีปุ่มอัปโหลดแยก) ต้องเรียก TryHandlePreview ใน Page_Load ของหน้าเดียวกัน</summary>
        public static void BindPreview(Page page, FileUpload file, TextBox amount, string badgeElementId)
        {
            if (page == null || file == null || amount == null) return;
            if (!SlipVerificationService.AmountLockActive) return;
            Register(page);
            page.ClientScript.RegisterStartupScript(typeof(SlipAmountLockUi), "slipPreview_" + file.ClientID,
                "(function(){function go(){if(window.SlipLock)SlipLock.preview({fileId:'" + HttpUtility.JavaScriptStringEncode(file.ClientID)
                + "',amountId:'" + HttpUtility.JavaScriptStringEncode(amount.ClientID) + "',badgeId:'" + HttpUtility.JavaScriptStringEncode(badgeElementId ?? "")
                + "'});}if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',go);else go();})();",
                true);
        }

        /// <summary>
        /// ตอบคำขอตรวจสลิปแบบ AJAX (POST + header X-Slip-Preview: 1 + ไฟล์ slipPreviewFile) — คืน true = ตอบแล้ว ผู้เรียกต้อง return ทันที
        /// ไม่บันทึกอะไร (ผลตรวจถูก cache ตาม hash — ตอนกดบันทึกไม่เสียโควต้า SlipOK ซ้ำ) · due = ยอดค้าง (แสดง "รับเกิน")
        /// </summary>
        public static bool TryHandlePreview(HttpContext ctx, decimal? due)
        {
            try
            {
                if (ctx == null || !string.Equals(ctx.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)) return false;
                if (ctx.Request.Headers["X-Slip-Preview"] != "1") return false;
                var json = new Dictionary<string, object> { { "active", false } };
                HttpPostedFile f = ctx.Request.Files["slipPreviewFile"];
                if (f != null && f.ContentLength > 0 && f.ContentLength <= 8 * 1024 * 1024 && SlipVerificationService.AmountLockActive)
                {
                    byte[] bytes;
                    using (var ms = new MemoryStream())
                    {
                        f.InputStream.CopyTo(ms);
                        bytes = ms.ToArray();
                    }
                    SlipAmountLock lk = SlipVerificationService.InspectAmountLock(bytes, Path.GetFileName(f.FileName));
                    json["active"] = lk.Active;
                    json["locked"] = lk.Locked;
                    json["notice"] = lk.NoticeText();
                    if (lk.Locked && lk.Amount.HasValue)
                    {
                        json["amountText"] = AmountText(lk.Amount.Value);
                        string over = due.HasValue ? SlipVerificationService.OverpayText(lk.Amount.Value, due.Value) : null;
                        if (over != null) json["extra"] = "ยอดสลิปมากกว่ายอดค้าง " + due.Value.ToString("N2") + " บาท — " + over;
                    }
                }
                ctx.Response.Clear();
                ctx.Response.ContentType = "application/json";
                ctx.Response.Cache.SetCacheability(HttpCacheability.NoCache);
                ctx.Response.Write(new JavaScriptSerializer().Serialize(json));
                ctx.Response.Flush();
                ctx.Response.SuppressContent = true;
                ctx.ApplicationInstance.CompleteRequest();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
