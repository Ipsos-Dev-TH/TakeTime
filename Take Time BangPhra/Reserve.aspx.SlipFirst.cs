using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Payments.SlipCheck;

namespace Take_Time_BangPhra
{
    /// <summary>
    /// หน้าจอง: อัปโหลดสลิปก่อนเห็นช่องยอด (CLAUDE.md ข้อ 18) — ส่วนแสดงผลล้วน
    ///   ช่องทางที่ต้องแนบสลิป (โอน/QR ตาม IsTransferPaidHow — กฎเดียวกับด่าน RunSlipGate) + เปิดยอดล็อกตามสลิป (AmountLockActive)
    ///   → ซ่อนช่องยอดจนกว่าจะอ่านสลิป: เลือกวิธี → ยอดที่ต้องโอน/บัญชี (แนะนำ) → แนบสลิป (อ่านทันที) → เห็นยอด (ล็อก/กรอกเอง/หยุด) → ยืนยัน
    ///   ปิดฟีเจอร์/ไม่มี PHASE19_34/เงินสด/เกตเวย์/ทดรอง = หน้าตาเดิมทุกอย่าง
    /// เซิร์ฟเวอร์ยังบังคับยอดตามสลิปที่ RunSlipGate เหมือนเดิม — ไม่เชื่อค่าจากเบราว์เซอร์
    /// </summary>
    public partial class Reserve
    {
        private const string SfStateKey = "rvSfState";
        private const string SfMsgKey = "rvSfMsg";
        private const string SfToneKey = "rvSfTone";
        private const string SfHintKey = "rvSfHint";

        internal const string SfPick = "pick";      // ยังไม่ได้เลือกวิธีชำระ (พนักงาน)
        internal const string SfWait = "wait";      // ต้องแนบสลิป — ยังไม่ได้อ่าน
        internal const string SfLocked = "locked";  // อ่านยอดได้น่าเชื่อถือ → ยอด = ยอดสลิป แก้ไม่ได้
        internal const string SfEdit = "edit";      // อ่านไม่ชัด/ตรวจไม่ได้ → กรอกยอดตามสลิปเอง
        internal const string SfStop = "stop";      // ใช้สลิปนี้ไม่ได้ (ซ้ำ / ลูกค้าตรวจไม่ผ่านโหมดติดต่อเจ้าหน้าที่)

        private string _sfTransferKey;
        private bool _sfTransfer;
        private string _sfAttrs = "";
        private bool _sfFresh;

        /// <summary>attribute ของกล่อง "การชำระเงิน" (#rvStepPay) — ว่าง = หน้าตาเดิม</summary>
        public string SlipFirstAttrs { get { return _sfAttrs ?? ""; } }

        /// <summary>ช่องทางที่เลือกเป็นโอน/QR (ต้องแนบสลิป) — กฎเดียวกับ SlipCheckApplies · cache ต่อคำขอ</summary>
        private bool SfMethodIsTransfer()
        {
            if (DropDownList2 == null || DropDownList2.SelectedItem == null) return false;
            string v = DropDownList2.SelectedValue ?? "";
            string t = DropDownList2.SelectedItem.Text ?? "";
            string key = v + "|" + t;
            if (_sfTransferKey == key) return _sfTransfer;
            bool r = false;
            try
            {
                if (!v.StartsWith("CH:", StringComparison.Ordinal) && !IsGatewayChannelType(SelectedChannelType()))
                    r = SlipVerificationService.IsTransferPaidHow(v, t);
            }
            catch { r = false; }
            _sfTransferKey = key;
            _sfTransfer = r;
            return r;
        }

        private string SfTempSlipPath()
        {
            return AppDomain.CurrentDomain.BaseDirectory + "\\Upload\\Slip\\" + SlipTempFilename;
        }

        private bool SfTempSlipExists()
        {
            try { return File.Exists(SfTempSlipPath()); } catch { return false; }
        }

        /// <summary>"" = ปิด (หน้าตาเดิม) · pick · wait · locked · edit · stop</summary>
        private string SlipFirstState()
        {
            try
            {
                if (!SlipVerificationService.AmountLockActive) return "";
                string command = Request.QueryString["command"] ?? "";
                if (command != "reserve" && command != "edit" && command != "checkin") return "";
                if (TextBox1.Text == "02" || PayNowChosen) return "";
                if (SlipAmountBox(command) == null) return "";
                if (rowSlip == null || !rowSlip.Visible || FileUpload1 == null || !FileUpload1.Visible) return "";
                if (DropDownList2 == null || DropDownList2.SelectedItem == null) return "";
                if (IsStaffUser && DropDownList2.SelectedIndex <= 0 && DropDownList2.SelectedValue == "0") return SfPick;
                if (!SfMethodIsTransfer()) return "";
                string st = ViewState[SfStateKey] as string;
                if (string.IsNullOrEmpty(st) || !SfTempSlipExists()) return SfWait;
                return st;
            }
            catch { return ""; }
        }

        /// <summary>จำผลอ่านสลิปล่าสุดของหน้านี้ (เรียกจาก ShowSlipCheck) — state null = ล้าง</summary>
        private void SetSlipFirstResult(string state, string msg, string tone, string hint)
        {
            try
            {
                ViewState[SfStateKey] = state;
                ViewState[SfMsgKey] = msg;
                ViewState[SfToneKey] = tone;
                ViewState[SfHintKey] = hint;
                _sfFresh = state != null;
            }
            catch { }
        }

        /// <summary>
        /// กดยืนยันโดยเลือกไฟล์สลิปแต่ยังไม่ได้อ่าน (ไม่มี JS / กดเร็ว) → อ่านสลิปก่อนแล้วเติม/ล็อกยอด
        /// ล็อกได้ = ไปต่อตามปกติ (RunSlipGate ตรวจซ้ำจาก cache) · ต้องกรอกเอง/ใช้ไม่ได้ = หยุดให้ดูผลก่อน
        /// </summary>
        private bool SlipFirstPreSubmit(string command)
        {
            try
            {
                if (SlipFirstState() != SfWait || !FileUpload1.HasFile) return true;
                PreviewSlipCheck();
                string st = ViewState[SfStateKey] as string;
                if (string.IsNullOrEmpty(st) || st == SfLocked) return true;
                if (st == SfStop)
                    ShowUserError("slipFirstStop", "สลิปนี้ใช้ไม่ได้ — ดูรายละเอียดใต้รูปสลิป แล้วแนบสลิปใบอื่นหรือเลือกวิธีชำระอื่น", FileUpload1);
                else
                    ShowUserError("slipFirstEdit", "ระบบอ่านยอดจากสลิปไม่ชัดเจน — ตรวจ/กรอกยอดตามสลิปแล้วกดยืนยันอีกครั้ง", SlipAmountBox(command));
                return false;
            }
            catch { return true; }
        }

        /// <summary>เปลี่ยนวิธีชำระ: ยังเป็นการโอนและมีสลิปค้าง = อ่านซ้ำ (cache — ไม่เสียโควต้า) · ไม่ใช่การโอนแล้ว = ปลดล็อกช่องยอด</summary>
        private void SlipFirstAfterMethodChange()
        {
            try
            {
                if (!SlipVerificationService.AmountLockActive) return;
                string st = SlipFirstState();
                if ((st == SfWait || st == SfLocked || st == SfEdit || st == SfStop) && SfTempSlipExists())
                    PreviewSlipCheck();
            }
            catch { }
        }

        /// <summary>PreRender: คำนวณสถานะ + การ์ดแนะนำ + attribute ให้สคริปต์</summary>
        private void PrepareSlipFirstUi()
        {
            _sfAttrs = "";
            if (litSlipFirstGuide != null) litSlipFirstGuide.Text = "";
            string command = Request.QueryString["command"] ?? "";
            string st = SlipFirstState();
            TextBox tb = SlipAmountBox(command);
            if (st.Length == 0)
            {
                // ไม่ใช่การโอนแล้ว (เช่น เปลี่ยนเป็นเงินสดหลังแนบสลิป) → ช่องยอดไม่ต้องล็อกตามสลิป (ด่านบันทึกตรวจตามช่องทางจริง)
                try
                {
                    if (tb != null && tb.Attributes["data-slip-locked"] != null && SlipVerificationService.AmountLockActive && !SfMethodIsTransfer())
                        SlipAmountLockUi.Apply(tb, null);
                }
                catch { }
                return;
            }
            SlipAmountLockUi.RegisterSlipFirst(this);

            bool cust = IsCustomerReserveMode;
            string mode = command == "reserve" ? "reserve" : "pay";
            decimal total;
            if (!decimal.TryParse((TextBox4.Text ?? "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out total)) total = 0m;
            int minDep;
            if (!int.TryParse((Label2.Text ?? "").Replace(",", "").Trim(), out minDep)) minDep = 0;
            decimal? due = mode == "reserve" ? (decimal?)total : (st == SfPick ? null : SlipDueAmount(command));

            string thumb = "";
            if (st == SfLocked || st == SfEdit || st == SfStop)
            {
                try { thumb = "./Upload/Slip/" + SlipTempFilename + "?v=" + File.GetLastWriteTimeUtc(SfTempSlipPath()).Ticks; } catch { thumb = ""; }
            }

            var a = new StringBuilder();
            Attr(a, "data-sf", st);
            Attr(a, "data-sf-mode", mode);
            Attr(a, "data-sf-cust", cust ? "1" : "0");
            Attr(a, "data-sf-total", total.ToString("0.##", CultureInfo.InvariantCulture));
            Attr(a, "data-sf-min", minDep.ToString(CultureInfo.InvariantCulture));
            Attr(a, "data-sf-due", due.HasValue ? due.Value.ToString("0.##", CultureInfo.InvariantCulture) : "");
            Attr(a, "data-sf-amt", tb != null ? tb.ClientID : "");
            Attr(a, "data-sf-amtrow", command == "reserve" ? "rvAmountRow" : "");
            Attr(a, "data-sf-fixed", tb != null && !tb.Enabled ? "1" : "0");
            Attr(a, "data-sf-up", Button3.ClientID);
            Attr(a, "data-sf-file", FileUpload1.ClientID);
            Attr(a, "data-sf-img", Image1.ClientID);
            Attr(a, "data-sf-phone", TextBox1.ClientID);
            Attr(a, "data-sf-thumb", thumb);
            Attr(a, "data-sf-msg", st == SfWait || st == SfPick ? "" : (ViewState[SfMsgKey] as string ?? ""));
            Attr(a, "data-sf-tone", st == SfWait || st == SfPick ? "" : (ViewState[SfToneKey] as string ?? ""));
            Attr(a, "data-sf-hint", st == SfEdit ? (ViewState[SfHintKey] as string ?? "") : "");
            Attr(a, "data-sf-fresh", _sfFresh ? "1" : "0");
            _sfAttrs = a.ToString();

            if (litSlipFirstGuide != null)
                litSlipFirstGuide.Text = BuildSlipFirstGuideHtml(st, mode, cust, total, minDep, due);
        }

        private static void Attr(StringBuilder sb, string name, string value)
        {
            sb.Append(' ').Append(name).Append("=\"").Append(HttpUtility.HtmlAttributeEncode(value ?? "")).Append('"');
        }

        // ── การ์ดแนะนำ ─────────────────────────────────────────────────────────

        private static string SfStepsHtml(string st)
        {
            string[] names = { "เลือกวิธีชำระ", "โอน + แนบสลิป", "ตรวจยอด", "ยืนยัน" };
            int on = st == SfPick ? 0 : st == SfWait || st == SfStop ? 1 : st == SfEdit ? 2 : 3;
            var sb = new StringBuilder("<ol class=\"sf-steps\" id=\"rvSfSteps\" aria-label=\"ขั้นตอนการชำระเงิน\">");
            for (int i = 0; i < names.Length; i++)
            {
                string cls = i < on ? "is-done" : i == on ? (st == SfStop ? "is-err" : "is-on") : "";
                sb.Append("<li").Append(cls.Length > 0 ? " class=\"" + cls + "\"" : "")
                  .Append(i == on ? " aria-current=\"step\"" : "").Append('>').Append(names[i]).Append("</li>");
            }
            return sb.Append("</ol>").ToString();
        }

        private static string SfAmountCopy(decimal v)
        {
            return decimal.Round(v, 2).ToString(v == decimal.Truncate(v) ? "0" : "0.00", CultureInfo.InvariantCulture);
        }

        private static string SfLine(string label, string sub, string valueHtml, string copyValue, string copyLabel, string rvKey, bool mono)
        {
            var sb = new StringBuilder("<div class=\"sf-line\"><div class=\"sf-line-l\">").Append(HttpUtility.HtmlEncode(label));
            if (!string.IsNullOrEmpty(sub)) sb.Append("<small>").Append(HttpUtility.HtmlEncode(sub)).Append("</small>");
            sb.Append("</div><div class=\"sf-line-v").Append(mono ? " sf-mono" : "").Append("\">").Append(valueHtml).Append("</div>");
            if (!string.IsNullOrEmpty(copyValue))
            {
                sb.Append("<button type=\"button\" class=\"sf-copy\" data-sf-copy=\"").Append(HttpUtility.HtmlAttributeEncode(copyValue)).Append('"');
                if (!string.IsNullOrEmpty(rvKey)) sb.Append(" data-sf-rv=\"").Append(rvKey).Append('"');
                sb.Append(" aria-label=\"").Append(HttpUtility.HtmlAttributeEncode(copyLabel)).Append("\">คัดลอก</button>");
            }
            return sb.Append("</div>").ToString();
        }

        private static string SfBankName(string code)
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

        private static string SfFormatAccount(string digits)
        {
            if (string.IsNullOrEmpty(digits)) return "";
            if (digits.Length == 10) return digits.Substring(0, 3) + "-" + digits.Substring(3, 1) + "-" + digits.Substring(4, 5) + "-" + digits.Substring(9, 1);
            return digits;
        }

        private static string SfFormatPromptPay(string digits)
        {
            if (string.IsNullOrEmpty(digits)) return "";
            if (digits.Length == 10) return digits.Substring(0, 3) + "-" + digits.Substring(3, 3) + "-" + digits.Substring(6);
            if (digits.Length == 13) return digits.Substring(0, 1) + "-" + digits.Substring(1, 4) + "-" + digits.Substring(5, 5) + "-" + digits.Substring(10, 2) + "-" + digits.Substring(12);
            return digits;
        }

        private string BuildSlipFirstGuideHtml(string st, string mode, bool cust, decimal total, int minDep, decimal? due)
        {
            var sb = new StringBuilder("<div class=\"rv-sf-guide\">");
            sb.Append(SfStepsHtml(st));
            if (st == SfPick)
            {
                sb.Append("<div class=\"sf-note\">เลือกวิธีชำระเงินด้านบนก่อน — ถ้าเป็นการโอน/QR ระบบจะแสดงยอดและบัญชีที่ต้องโอน แล้วอ่านยอดจากสลิปให้เอง</div></div>");
                return sb.ToString();
            }

            var body = new StringBuilder();
            if (mode == "reserve")
            {
                if (total > 0m)
                {
                    if (minDep > 0 && minDep < total)
                        body.Append(SfLine("มัดจำขั้นต่ำ", "ยืนยันการจองได้ทันที", "฿<span data-rv=\"mindep\">" + minDep.ToString("N0", CultureInfo.InvariantCulture) + "</span>",
                            minDep.ToString(CultureInfo.InvariantCulture), "คัดลอกยอดมัดจำขั้นต่ำ", "mindep", false));
                    body.Append(SfLine("ชำระเต็มจำนวน", minDep > 0 && minDep < total ? "ไม่ต้องจ่ายเพิ่มตอนเช็คอิน" : null,
                        "฿<span data-rv=\"total\">" + total.ToString("N2", CultureInfo.InvariantCulture) + "</span>",
                        SfAmountCopy(total), "คัดลอกยอดเต็มจำนวน", "total", false));
                }
                else
                {
                    body.Append("<div class=\"sf-note\">เลือกวันที่และห้องพักก่อน แล้วยอดที่ต้องโอนจะแสดงที่นี่</div>");
                }
            }
            else if (due.HasValue)
            {
                body.Append(SfLine("ยอดค้างชำระ", null, "฿" + due.Value.ToString("N2", CultureInfo.InvariantCulture),
                    due.Value > 0m ? SfAmountCopy(due.Value) : null, "คัดลอกยอดค้างชำระ", null, false));
            }

            // บัญชีรับเงินของแหล่งเงินที่เลือก (PHASE19_34 Bank_Code/Bank_Account_No/PromptPay_Id หรือเลขบัญชีจาก NextAcc)
            bool hasAccount = false;
            int pid;
            if (int.TryParse(DropDownList2.SelectedValue, out pid) && pid > 0)
            {
                try
                {
                    foreach (var acc in SlipVerificationService.LoadReceiverAccounts())
                    {
                        if (acc == null || acc.PaidHowId != pid) continue;
                        if (!string.IsNullOrEmpty(acc.AccountNo))
                        {
                            body.Append(SfLine(SfBankName(acc.BankCode), "เลขบัญชี", HttpUtility.HtmlEncode(SfFormatAccount(acc.AccountNo)),
                                acc.AccountNo, "คัดลอกเลขบัญชี", null, true));
                            hasAccount = true;
                        }
                        if (!string.IsNullOrEmpty(acc.PromptPayId))
                        {
                            body.Append(SfLine("พร้อมเพย์", null, HttpUtility.HtmlEncode(SfFormatPromptPay(acc.PromptPayId)),
                                acc.PromptPayId, "คัดลอกพร้อมเพย์", null, true));
                            hasAccount = true;
                        }
                        break;
                    }
                }
                catch { }
            }

            // QR ของช่องทาง: ลูกค้าเห็นในกล่องช่องทางด้านบนแล้ว · พนักงานเปิดดู/ให้ลูกค้าสแกนจากจอได้
            string qr = "";
            try
            {
                Take_Time_BangPhra.Payments.PaymentChannel pc = FindSelectedCatalogChannel();
                qr = SafeImageUrl(pc != null ? pc.QrImageUrl : null);
            }
            catch { qr = ""; }
            if (!cust && qr.Length > 0)
            {
                body.Append("<details><summary>แสดง QR รับเงินให้ลูกค้าสแกน</summary><img class=\"sf-qr\" alt=\"QR รับเงิน\" src=\"")
                    .Append(HttpUtility.HtmlAttributeEncode(qr)).Append("\" /></details>");
            }
            else if (cust && qr.Length == 0 && !hasAccount)
            {
                body.Append("<img class=\"sf-qr\" alt=\"บัญชีสำหรับโอนเงิน\" src=\"./Images/บัญชี.png\" style=\"max-width:320px;\" />");
            }

            string tip = cust
                ? "โอนยอดใดก็ได้ตั้งแต่มัดจำขั้นต่ำถึงเต็มจำนวน แล้วแนบสลิปด้านล่าง — ระบบอ่านยอดจากสลิปให้เอง ไม่ต้องพิมพ์ยอด"
                : "ให้ลูกค้าโอน แล้วแนบสลิปด้านล่าง — ระบบอ่านยอดจากสลิปให้เอง (ช่องยอดจะแสดงหลังอ่านสลิป)";

            if (st == SfWait)
            {
                sb.Append("<div class=\"sf-guide\"><div class=\"sf-guide-h\">")
                  .Append(cust ? "💸 โอนเงินตามยอดนี้" : "💸 ยอดที่ต้องรับ")
                  .Append("</div>").Append(body).Append("<div class=\"sf-guide-sub\">").Append(HttpUtility.HtmlEncode(tip)).Append("</div></div>");
            }
            else
            {
                // อ่านสลิปแล้ว: ย่อการ์ดแนะนำไว้ (เปิดดูซ้ำได้) — เน้นผลอ่านสลิปและยอดที่รับ
                sb.Append("<details class=\"sf-guide\"><summary>ดูยอดและบัญชีที่ต้องโอนอีกครั้ง</summary>").Append(body).Append("</details>");
            }
            return sb.Append("</div>").ToString();
        }
    }
}
