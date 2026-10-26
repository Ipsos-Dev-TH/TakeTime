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
                // ผลนี้เป็นของไฟล์ชั่วคราวรุ่นไหน — ไฟล์ถูกเขียนใหม่โดย API/SlipCheck.ashx (async) = ต้องอ่านใหม่ (SfAdoptAsyncSlip)
                ViewState[SfTicksKey] = state != null ? SfTempTicks() : null;
                _sfFresh = state != null;
            }
            catch { }
        }

        private const string SfTicksKey = "rvSfTicks";
        private bool _sfAdopted;

        private string SfTempTicks()
        {
            try
            {
                string p = SfTempSlipPath();
                return File.Exists(p) ? File.GetLastWriteTimeUtc(p).Ticks.ToString(CultureInfo.InvariantCulture) : null;
            }
            catch { return null; }
        }

        /// <summary>
        /// สลิปที่อ่านผ่าน API/SlipCheck.ashx (ไม่มี postback) — ViewState ยังไม่รู้ผล → อ่านซ้ำจากไฟล์ชั่วคราวที่ API เก็บไว้
        /// (ผลอยู่ใน cache ตาม hash ไม่เสียโควต้า) ให้สถานะหน้า/ช่องยอด/รูปตัวอย่างตรงกับที่ผู้ใช้เห็น · ครั้งเดียวต่อคำขอ
        /// </summary>
        private void SfAdoptAsyncSlip()
        {
            if (_sfAdopted) return;
            _sfAdopted = true;
            try
            {
                if (!SlipVerificationService.AmountLockActive) return;
                string command = Request.QueryString["command"] ?? "";
                if (command != "reserve" && command != "edit" && command != "checkin") return;
                if (FileUpload1 != null && FileUpload1.HasFile) return;   // เลือกไฟล์มากับคำขอนี้ = ทางเดิม (ReadPendingSlip)
                if (!SfMethodIsTransfer()) return;
                string ticks = SfTempTicks();
                if (ticks == null) return;
                if (!string.IsNullOrEmpty(ViewState[SfStateKey] as string) && ticks == (ViewState[SfTicksKey] as string)) return;
                Image1.ImageUrl = "./Upload/Slip/" + SlipTempFilename;
                Image1.Visible = true;
                PreviewSlipCheck();
                _sfFresh = false;
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
            SfAdoptAsyncSlip();
            string st = SlipFirstState();
            TextBox tb = SlipAmountBox(command);
            try { if (divSlipStaffOpts != null) divSlipStaffOpts.Style.Remove("display"); } catch { }
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

            // ── อ่านสลิปแบบ async (API/SlipCheck.ashx) — nonce ผูกบริบทหน้านี้ไว้ใน Session; ไม่มีสคริปต์ = ปุ่มอัปโหลดเดิม ──
            SlipAmountLockUi.Register(this);   // SlipLock.setLocked ให้สคริปต์ล็อกช่องยอดหลังได้ผล async
            Attr(a, "data-sf-api", ResolveUrl("~/API/SlipCheck.ashx"));
            Attr(a, "data-sf-nonce", SlipUploadStore.IssueContext(Session, Request.QueryString["id"], command, IsStaffUser));
            Attr(a, "data-sf-ddl", DropDownList2.ClientID);
            if (st == SfWait)
            {
                // ผล async (การ์ดรายละเอียด/ปุ่มติดต่อ) ต้องมีที่วาง — แผงผลตรวจว่างไว้ก่อน (ไม่มีสคริปต์ = มองไม่เห็นอะไร)
                pnlSlipCheck.Visible = true;
                litSlipCheck.Text = "";
                if (cust) divSlipStaffOpts.Visible = false;
            }
            if (!cust)
            {
                Attr(a, "data-sf-opts", divSlipStaffOpts.ClientID);
                Attr(a, "data-sf-optov", spanSlipOverride.ClientID);
                Attr(a, "data-sf-alloc", chkSlipAllocate.ClientID);
                Attr(a, "data-sf-useamt", btnSlipUseAmount.ClientID);
                if (st == SfWait)
                {
                    // ตัวเลือกพนักงาน (แบ่งสลิป / override) ต้องมีในฟอร์มก่อน — ผล async เปิดให้เห็นตามสถานะ (เซิร์ฟเวอร์ตัดสินซ้ำตอนบันทึก)
                    divSlipStaffOpts.Visible = true;
                    divSlipStaffOpts.Style["display"] = "none";
                    btnSlipUseAmount.Visible = false;
                    chkSlipAllocate.Visible = true;
                    spanSlipOverride.Visible = SlipVerificationService.CanOverrideCurrentUser();
                }
            }
            else if (mode == "reserve")
            {
                // ลูกค้า: สลิปเติมยอด (ยอดรวมจากสลิปหลายใบ เมื่อใบแรกต่ำกว่ามัดจำขั้นต่ำ) + ปุ่มติดต่อเจ้าหน้าที่
                Attr(a, "data-sf-contact", SlipAmountLockUi.ContactButtonsHtml());
                Attr(a, "data-sf-maxslots", SlipUploadStore.MaxSlots.ToString(CultureInfo.InvariantCulture));
                Attr(a, "data-sf-topups", st == SfLocked ? SfTopUpsJson() : "[]");
            }
            _sfAttrs = a.ToString();

            if (litSlipFirstGuide != null)
                litSlipFirstGuide.Text = BuildSlipFirstGuideHtml(st, mode, cust, total, minDep, due);
        }

        private static void Attr(StringBuilder sb, string name, string value)
        {
            sb.Append(' ').Append(name).Append("=\"").Append(HttpUtility.HtmlAttributeEncode(value ?? "")).Append('"');
        }

        // ── สลิปเติมยอด (ลูกค้าจองเอง): สลิปแรกต่ำกว่ามัดจำขั้นต่ำ → โอนส่วนที่ขาดแล้วแนบสลิปเพิ่ม (สูงสุด SlipUploadStore.MaxSlots ใบ) ──
        //   ทุกใบตรวจแยก (ซ้ำ/ผู้รับ/ล็อกยอด) · ยอดรับ = ผลรวมยอดล็อกของทุกใบ · ใบเสร็จมัดจำ "ใบเดียว" ยอดรวม + แถว Payment_Slips ต่อใบ
        //   (ผูก Account_Receipt_ID เดียวกัน → แนบไฟล์ให้ NextAcc ครบ) · มีใบที่ต้องรอพนักงาน = การจองรอตรวจ + สลิปรอตรวจแยกใบ (อนุมัติทีละใบ)

        private List<SlipUploadStore.SetItem> _sfTopUpSet;

        private string[] SfSlotPaths()
        {
            return SlipUploadStore.SlotPaths(Session != null ? Session.SessionID : "", Request.QueryString["id"],
                Request.QueryString["command"], TextBox1.Text);
        }

        private bool SfHasTopUps()
        {
            if (!IsCustomerReserveMode) return false;
            string[] p = SfSlotPaths();
            for (int i = 1; i < p.Length; i++) if (p[i] != null) return true;
            return false;
        }

        private void SfClearTopUps()
        {
            try { SlipUploadStore.ClearTopUps(Session != null ? Session.SessionID : "", Request.QueryString["id"], Request.QueryString["command"], TextBox1.Text); }
            catch { }
        }

        /// <summary>สลิปเติมยอดที่ค้างอยู่ (ช่อง 2..n) → JSON ให้สคริปต์วาดรายการ (ยอดจาก cache — ไม่ log ซ้ำ)</summary>
        private string SfTopUpsJson()
        {
            var list = new List<object>();
            try
            {
                if (IsCustomerReserveMode)
                {
                    string[] p = SfSlotPaths();
                    for (int i = 1; i < p.Length; i++)
                    {
                        if (p[i] == null) continue;
                        var lk = SlipVerificationService.InspectAmountLockFile(p[i]);
                        list.Add(new Dictionary<string, object>
                        {
                            { "slot", i + 1 },
                            { "amount", lk != null && lk.Locked && lk.Amount.HasValue ? lk.Amount.Value : 0m },
                            { "thumb", "./Upload/Slip/" + Path.GetFileName(p[i]) + "?v=" + File.GetLastWriteTimeUtc(p[i]).Ticks.ToString(CultureInfo.InvariantCulture) }
                        });
                    }
                }
            }
            catch { }
            return new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(list);
        }

        private int SfMinDeposit()
        {
            int minDep;
            if (!int.TryParse((Label2.Text ?? "").Replace(",", "").Trim(), out minDep)) minDep = 0;
            return minDep;
        }

        /// <summary>เกณฑ์เดียวกับ ValidateCustomerBookingGate / TextBox5_TextChanged (80% ของมัดจำขั้นต่ำ)</summary>
        private bool SfMeetsMinDeposit(decimal amount)
        {
            int minDep = SfMinDeposit();
            return minDep <= 0 || amount >= minDep * 0.8m;
        }

        private string SfBelowMinMessage(decimal amount)
        {
            int minDep = SfMinDeposit();
            decimal need = Math.Max(0m, minDep - amount);
            return "ยอดรวมจากสลิป ฿" + amount.ToString("N2") + " ต่ำกว่ามัดจำขั้นต่ำ ฿" + minDep.ToString("N0")
                + " — โอนเพิ่มอีก ฿" + need.ToString("N2") + " แล้วกด \"แนบสลิปเพิ่ม\" (สูงสุด " + SlipUploadStore.MaxSlots + " ใบ)"
                + " หรือติดต่อเจ้าหน้าที่" + SlipVerificationService.CustomerContactText();
        }

        /// <summary>
        /// ด่านบันทึกเมื่อลูกค้ามีสลิปหลายใบ (แทน RunSlipGate ทางใบเดียว) — ตรวจทุกใบจากไฟล์ที่เก็บไว้ฝั่งเซิร์ฟเวอร์ (ไม่เชื่อยอดจากหน้า)
        /// false = หยุด (แจ้งแล้ว) · true = ไปต่อ: ยอดรับ (TextBox5) = ผลรวม, _slipVerify/_slipGate = ใบแรก, ใบที่เหลือบันทึกใน uploadSlip
        /// </summary>
        private bool RunSlipGateTopUp(string command)
        {
            int rid;
            long? resId = int.TryParse(Request.QueryString["id"], out rid) && rid > 0 ? (long?)rid : null;
            var set = SlipUploadStore.EvaluateCustomerSet(SfSlotPaths(), resId, SlipVerificationService.CurrentUserName());
            var first = set.Find(x => x.Slot == 1);
            if (first == null)
            {
                SfClearTopUps();
                ShowUserError("slipTopUp", "กรุณาแนบสลิปการโอนเงินอีกครั้ง", FileUpload1);
                return false;
            }
            var bad = set.Find(x => x.Problem != null);
            if (bad != null)
            {
                // แจ้ง/บันทึกแบบเดียวกับใบเดียว (Telegram กันถี่ตาม hash)
                try
                {
                    SlipVerificationService.FinishCustomerOutcome(new CustomerSlipOutcome
                    {
                        Mode = SlipVerificationService.CustomerFailMode,
                        Amount = bad.Amount > 0m ? (decimal?)bad.Amount : null,
                        Result = bad.Result,
                        Lock = bad.Lock
                    }, false, new SlipVerifyContext { SourceType = "RESERVATION", SourceId = resId, CustomerSelfService = true }, bad.Path);
                }
                catch { }
                ShowUserError("slipTopUp", "สลิปใบที่ " + bad.Slot + " ใช้ไม่ได้: " + bad.Problem
                    + (bad.Slot > 1 ? " — กด \"ลบ\" ที่สลิปใบนั้นแล้วแนบใบใหม่" : "")
                    + SlipVerificationService.CustomerContactText(), FileUpload1);
                code2.Logs(conn, "SlipVerify", "ลูกค้าจองพร้อมสลิปหลายใบ — ไม่บันทึกการจอง: ใบที่ " + bad.Slot + " " + bad.Problem,
                    Session["User"]?.ToString() ?? "Customer");
                return false;
            }
            decimal sum = SlipUploadStore.Sum(set);
            if (!SfMeetsMinDeposit(sum))
            {
                ShowUserError("slipTopUpMin", SfBelowMinMessage(sum), FileUpload1);
                return false;
            }
            SetSlipExpectedAmount(command, sum);   // ยอดรับ = ผลรวมยอดล็อกของทุกใบ (ไม่ใช่ค่าที่หน้าส่งมา)
            _slipAmountApplied = true;
            _slipAmountUsed = first.Amount;        // แถวสลิปใบแรกบันทึกยอดของใบนั้นเอง
            _slipVerify = first.Result;
            _slipLock = first.Lock;
            _sfTopUpSet = set;

            bool hold = false;
            foreach (var it in set)
            {
                var d = SlipVerificationService.Decide(it.Result, true, false, false, null, false);
                if (it.NeedsStaff || !d.Proceed || d.HoldForStaff) hold = true;
                if (it.Slot == 1) _slipGate = d;
            }
            if (hold)
            {
                if (new PaymentService(conn).HasSlipClaimColumn())
                {
                    _custSlipHold = true;
                    _custSlipManual = false;
                    code2.Logs(conn, "SlipVerify", "ลูกค้าจองพร้อมสลิป " + set.Count + " ใบ รวม " + sum.ToString("N2") + " — รอเจ้าหน้าที่ตรวจ (ยังไม่ออกใบเสร็จ)",
                        Session["User"]?.ToString() ?? "Customer");
                }
                else
                {
                    // ยังไม่รัน PHASE19_25 — ทำแบบเดิม (ออกใบเสร็จ) แต่ผลตรวจถูกบันทึกในแถวสลิปให้ตรวจภายหลัง
                    _slipGate = new SlipGateDecision();
                }
            }
            else
            {
                code2.Logs(conn, "SlipVerify", "ลูกค้าจองพร้อมสลิป " + set.Count + " ใบ (ธนาคารยืนยันทุกใบ) รวม " + sum.ToString("N2") + " — ใบเสร็จมัดจำใบเดียว",
                    Session["User"]?.ToString() ?? "Customer");
            }
            return true;
        }

        /// <summary>หลังออกใบเสร็จ (uploadSlip): สลิปเติมยอดแต่ละใบ → แถว Payment_Slips ผูกใบเสร็จเดียวกัน + ผลตรวจของใบนั้น</summary>
        private void SfSaveTopUpRows(string reservationId, string receiptId)
        {
            if (_sfTopUpSet == null || CustomerSlipHold) return;
            int rid;
            if (!int.TryParse(reservationId, out rid) || rid <= 0) return;
            foreach (var it in _sfTopUpSet)
            {
                if (it.Slot < 2 || it.Problem != null) continue;
                try
                {
                    if (!File.Exists(it.Path)) continue;
                    string finalName = rid + "_" + SlipUploadStore.PhoneKey(TextBox1.Text) + "_" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)
                        + "_t" + it.Slot + ".jpg";
                    string dest = SlipUploadStore.PathOf(finalName);
                    if (File.Exists(dest)) File.Delete(dest);
                    File.Move(it.Path, dest);
                    int? adminId = Session["UserID"] != null ? (int?)Convert.ToInt32(Session["UserID"]) : null;
                    var dt = code2.DatabaseQuerySafe(conn,
                        @"INSERT INTO [dbo].[Payment_Slips] (Account_Receipt_ID, Reservation_ID, SlipFileURL, FileName, FileType, FileSize,
                                UploadedBy_ID, UploadedBy_CustomerPhone, UploadedDate, VerificationStatus, IsVerified, Notes, IsActive, Status)
                          OUTPUT INSERTED.ID
                          VALUES (@rcp, @rid, @url, @name, 'image/jpeg', @size, @admin, @phone, GETDATE(), 'PENDING', 0, @notes, 1, 1)",
                        new Dictionary<string, object>
                        {
                            { "@rcp", string.IsNullOrEmpty(receiptId) ? (object)DBNull.Value : receiptId },
                            { "@rid", rid },
                            { "@url", "Upload/Slip/" + finalName },
                            { "@name", finalName },
                            { "@size", (int)new FileInfo(dest).Length },
                            { "@admin", adminId.HasValue ? (object)adminId.Value : DBNull.Value },
                            { "@phone", TextBox1.Text },
                            { "@notes", "สลิปเติมยอด ใบที่ " + it.Slot + " (ยอด " + it.Amount.ToString("N2") + ") — อัพโหลดพร้อมบันทึก" }
                        });
                    if (dt != null && dt.Rows.Count > 0)
                    {
                        long slipId = Convert.ToInt64(dt.Rows[0][0]);
                        SlipVerificationService.SaveResult(slipId, it.Result, SlipVerificationService.Decide(it.Result, true, false, false, null, false),
                            receiptId, rid, it.Amount, SlipVerificationService.CurrentUserName());
                    }
                }
                catch (Exception ex)
                {
                    try { code2.Logs(conn, "SlipVerify", "การจอง " + rid + ": บันทึกสลิปเติมยอดใบที่ " + it.Slot + " ไม่สำเร็จ — " + ex.Message, "SYSTEM"); } catch { }
                }
            }
        }

        /// <summary>การจองรอตรวจ (CustomerSlipHold) + สลิปหลายใบ: สลิปเติมยอดแต่ละใบ → สลิปรอตรวจแยกใบ (Claimed_Amount = ยอดของใบนั้น)</summary>
        private void SfSubmitTopUpHolds(int reservationId)
        {
            if (_sfTopUpSet == null) return;
            var ps = new PaymentService(conn);
            foreach (var it in _sfTopUpSet)
            {
                if (it.Slot < 2 || it.Problem != null) continue;
                try
                {
                    var pr = ps.SubmitReservationSlipFileForVerification(reservationId, it.Amount, it.Path, TextBox1.Text,
                        "สลิปเติมยอด ใบที่ " + it.Slot + " ตอนจอง (ลูกค้า) — " + (it.Result != null ? it.Result.ShortText() : "ไม่ได้ตรวจ"), false);
                    if (pr != null && pr.Success) { try { File.Delete(it.Path); } catch { } }
                }
                catch (Exception ex)
                {
                    try { code2.Logs(conn, "SlipVerify", "การจอง " + reservationId + ": เก็บสลิปเติมยอดใบที่ " + it.Slot + " ไม่สำเร็จ — " + ex.Message, "SYSTEM"); } catch { }
                }
            }
        }

        /// <summary>ยอดของสลิปใบแรกในชุด (การจองรอตรวจ) — null = ใบเดียว</summary>
        private decimal? SfFirstSlipAmount()
        {
            if (_sfTopUpSet == null) return null;
            var f = _sfTopUpSet.Find(x => x.Slot == 1);
            return f != null && f.Amount > 0m ? (decimal?)f.Amount : null;
        }

        // ── พนักงาน: วิธีชำระล่าสุด (ไม่ต้องเลือกใหม่ทุกครั้ง) ──

        private string SfLastMethodCookieName()
        {
            string user = SlipVerificationService.CurrentUserName() ?? "";
            using (var sha = System.Security.Cryptography.SHA1.Create())
                return "ttRvPH_" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(user.ToLowerInvariant()))).Replace("-", "").Substring(0, 8).ToLowerInvariant();
        }

        /// <summary>จำวิธีชำระที่พนักงานคนนี้เลือกล่าสุด (cookie ต่อผู้ใช้ 180 วัน — เก็บแค่รหัสแหล่งเงิน)</summary>
        private void SfRememberStaffMethod()
        {
            try
            {
                if (!IsStaffUser || !SlipVerificationService.AmountLockActive) return;
                string v = DropDownList2.SelectedValue ?? "";
                int id;
                if (!int.TryParse(v, out id) || id <= 0) return;
                Response.Cookies.Add(new HttpCookie(SfLastMethodCookieName(), v)
                {
                    Expires = DateTime.Now.AddDays(180),
                    HttpOnly = true,
                    Secure = Request.IsSecureConnection,
                    Path = "/"
                });
            }
            catch { }
        }

        /// <summary>
        /// เปิดหน้าจองใหม่ (พนักงาน): เลือกวิธีชำระล่าสุดของผู้ใช้ไว้ให้ → เห็นยอด/ช่องสลิปทันที (เปลี่ยนได้)
        /// เฉพาะเมื่อเปิดยอดล็อกตามสลิป (ปิดฟีเจอร์ = เดิม) และวิธีนั้นยังอยู่ในรายการ
        /// </summary>
        private void SfPreselectStaffMethod()
        {
            try
            {
                if (IsPostBack || !IsStaffUser || !SlipVerificationService.AmountLockActive) return;
                if (DropDownList2.SelectedIndex > 0 || DropDownList2.Items.Count < 2) return;
                HttpCookie c = Request.Cookies[SfLastMethodCookieName()];
                string v = c != null ? (c.Value ?? "") : "";
                int id;
                if (!int.TryParse(v, out id) || id <= 0) return;
                ListItem it = DropDownList2.Items.FindByValue(v);
                if (it == null) return;
                DropDownList2.ClearSelection();
                it.Selected = true;
                ViewState["rvSfPreset"] = it.Value;
                DropDownList2_SelectedIndexChanged(DropDownList2, EventArgs.Empty);
            }
            catch { }
        }

        /// <summary>ปุ่มลัดเลือกวิธีชำระ (สถานะ "เลือกวิธีชำระ") — วิธีล่าสุดก่อน แล้วตามลำดับรายการ สูงสุด 6 ปุ่ม</summary>
        private string SfPickChipsHtml()
        {
            var sb = new StringBuilder();
            try
            {
                string last = null;
                try { HttpCookie c = Request.Cookies[SfLastMethodCookieName()]; last = c != null ? c.Value : null; } catch { }
                var items = new List<ListItem>();
                foreach (ListItem it in DropDownList2.Items)
                {
                    if (string.IsNullOrEmpty(it.Value) || it.Value == "0") continue;
                    if (it.Value == last) items.Insert(0, it); else items.Add(it);
                }
                int n = 0;
                foreach (var it in items)
                {
                    if (n++ >= 6) break;
                    sb.Append("<button type=\"button\" class=\"sf-pick-chip").Append(it.Value == last ? " is-last" : "")
                      .Append("\" data-sf-pick=\"").Append(HttpUtility.HtmlAttributeEncode(it.Value)).Append("\">")
                      .Append(HttpUtility.HtmlEncode(it.Text)).Append(it.Value == last ? " <small>(ล่าสุด)</small>" : "").Append("</button>");
                }
            }
            catch { }
            return sb.Length == 0 ? "" : "<div class=\"sf-pick\" role=\"group\" aria-label=\"เลือกวิธีชำระเงิน\">" + sb + "</div>";
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
                // พนักงาน: แตะปุ่มเดียวเลือกวิธีชำระ (วิธีล่าสุดของผู้ใช้มาก่อน) — dropdown เดิมยังใช้ได้
                sb.Append("<div class=\"sf-note\">เลือกวิธีชำระเงิน — ถ้าเป็นการโอน/QR ระบบจะแสดงยอดและบัญชีที่ต้องโอน แล้วอ่านยอดจากสลิปให้เอง</div>")
                  .Append(SfPickChipsHtml()).Append("</div>");
                return sb.ToString();
            }
            string preset = ViewState["rvSfPreset"] as string;
            if (!cust && !string.IsNullOrEmpty(preset) && DropDownList2.SelectedValue == preset && DropDownList2.SelectedItem != null)
                sb.Append("<div class=\"sf-note sf-preset\">ใช้วิธีชำระล่าสุดของคุณ: <b>").Append(HttpUtility.HtmlEncode(DropDownList2.SelectedItem.Text))
                  .Append("</b> — เปลี่ยนได้ที่ช่องวิธีชำระด้านบน</div>");

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
