using System;
using System.Web;

namespace Take_Time_BangPhra.Payments.SlipCheck
{
    /// <summary>
    /// ผลอ่านสลิปของหน้าจองที่ "หน้าจอ" ต้องแสดง (สลิปก่อนยอด CLAUDE.md ข้อ 18) — แยกจาก Reserve.ShowSlipCheck เดิมทุกเงื่อนไข
    /// ใช้ร่วมกันระหว่างการอ่านผ่าน postback (ShowSlipCheck) และ API/SlipCheck.ashx (async) ⇒ ข้อความ/สถานะ/ตัวเลือกพนักงานตรงกันเสมอ
    /// ไม่ตัดสินการบันทึก — ด่าน RunSlipGate ตรวจซ้ำจากไฟล์ที่เก็บไว้ฝั่งเซิร์ฟเวอร์ตอนกดยืนยันเหมือนเดิม
    /// </summary>
    public sealed class SlipFirstVerdict
    {
        public const string Locked = "locked";
        public const string Edit = "edit";
        public const string Stop = "stop";

        /// <summary>locked | edit | stop</summary>
        public string State;
        public string Message;
        /// <summary>ok | warn | err</summary>
        public string Tone;
        /// <summary>คำใบ้ยอด (กรอกเอง) — null = ไม่มี</summary>
        public string Hint;
        /// <summary>การ์ดรายละเอียด (HTML ที่ encode ค่าแล้ว)</summary>
        public string CardHtml = "";
        /// <summary>เป็นเส้นพนักงาน (ตัวเลือกแบ่งสลิป/override มีผล)</summary>
        public bool StaffBranch;
        public bool ShowStaffOpts;
        public bool ShowUseAmount;
        public bool ShowAllocate;
        public bool ShowOverride;
        /// <summary>ต้องเรียก ApplySlipAmountLockUi (ล็อก/ปลดล็อกช่องยอด)</summary>
        public bool ApplyLockUi;
        /// <summary>ส่งให้ ApplySlipAmountLockUi — true = ไม่ล็อก (แบ่งสลิป/ใช้ไม่ได้)</summary>
        public bool SuppressLock;
        public SlipAmountLock Lock;
        public decimal? SlipAmount;

        /// <summary>ยอดที่ช่องยอดถูกล็อก (null = ไม่ล็อก — กรอกเอง/หยุด)</summary>
        public decimal? LockedAmount
        {
            get
            {
                return ApplyLockUi && !SuppressLock && Lock != null && Lock.Locked && Lock.Amount.HasValue && Lock.Amount.Value > 0m
                    ? Lock.Amount : null;
            }
        }

        /// <summary>
        /// customerMode = ลูกค้าจองเอง (IsCustomerReserveMode) · due = ยอดค้าง/ยอดรวมของหน้า (แสดง "รับเกิน") ·
        /// canOverride = SlipVerificationService.CanOverrideCurrentUser() ของผู้ใช้หน้านี้
        /// </summary>
        public static SlipFirstVerdict Build(SlipVerifyResult r, SlipAmountLock lkIn, bool customerMode, decimal? due, bool canOverride)
        {
            var v = new SlipFirstVerdict();
            if (r == null) return null;
            var lk = r.AmountLock ?? lkIn;
            v.SlipAmount = r.Amount;
            if (customerMode && SlipVerificationService.AmountLockActive)
            {
                if (lk == null) lk = new SlipAmountLock { Active = true, Reason = "ตรวจสลิปไม่ได้" };
                v.Lock = lk;
                // ลูกค้า + ยอดล็อกตามสลิป (PHASE19_36): ตรวจไม่ผ่าน → ตามโหมด SlipVerify_Customer_Fail_Mode
                bool dup = r.Status == SlipVerifyStatus.Duplicate;
                string reason = dup ? "สลิปนี้ถูกใช้ชำระไปแล้ว" : SlipVerificationService.CustomerFailReason(r, lk, false);
                bool allowManual = SlipVerificationService.CustomerFailMode == CustomerSlipOutcome.ModeAllowManual;
                bool blocked = reason != null && (!allowManual || dup);
                string cmsg;
                if (reason == null)
                {
                    string over = null;
                    if (lk.Locked && due.HasValue) over = SlipVerificationService.OverpayText(lk.Amount.Value, due.Value);
                    cmsg = (r.BankConfirmed ? "✅ ตรวจสลิปกับธนาคารแล้ว" : "✅ อ่านสลิปแล้ว — เจ้าหน้าที่จะยืนยันการจองให้")
                        + " ยอด " + lk.Amount.GetValueOrDefault().ToString("N2") + " บาท (ยอดตามสลิป แก้ไม่ได้)"
                        + (over != null ? " · " + over : "");
                }
                else if (blocked)
                    cmsg = "⛔ " + (dup
                            ? "สลิปนี้ถูกใช้ชำระไปแล้ว ไม่สามารถใช้ซ้ำได้ กรุณาติดต่อเจ้าหน้าที่"
                            : "ระบบตรวจสอบสลิปไม่ผ่าน กรุณาติดต่อเจ้าหน้าที่ (สาเหตุ: " + reason + ")")
                        + SlipVerificationService.CustomerContactText()
                        + "\nหรือเลือกวิธีชำระแบบอื่น";
                else
                    cmsg = "✏️ ระบบตรวจสลิปอัตโนมัติไม่ผ่าน (" + reason + ") — "
                        + (lk.Locked ? "" : "กรุณากรอกยอดตามสลิปในช่องมัดจำ ")
                        + "เจ้าหน้าที่จะตรวจสอบและยืนยันการจองให้"
                        + (!lk.Locked && lk.HintAmount.HasValue ? " (ระบบอ่านได้ประมาณ " + lk.HintAmount.Value.ToString("N2") + " บาท)" : "");
                v.CardHtml = "<div class='rv-hint' style='margin:6px 0;white-space:pre-line;'>" + HttpUtility.HtmlEncode(cmsg) + "</div>"
                    + (reason == null || !blocked ? lk.ToHtml() : "")
                    // สลิปก่อนยอด: การ์ดหยุดมีปุ่มโทร/LINE ให้กดติดต่อได้ทันที
                    + (blocked ? SlipAmountLockUi.ContactButtonsHtml() : "");
                v.State = blocked ? Stop : (lk.Locked ? Locked : Edit);
                v.Message = blocked
                    ? (dup ? "⛔ สลิปนี้ถูกใช้ชำระไปแล้ว" : "⛔ ตรวจสลิปไม่ผ่าน — กรุณาติดต่อเจ้าหน้าที่")
                    : reason == null
                        ? (r.BankConfirmed ? "✅ ธนาคารยืนยันยอดแล้ว" : "✅ อ่านยอดจากสลิปแล้ว")
                        : (lk.Locked ? "⚠️ ตรวจอัตโนมัติไม่ผ่าน — เจ้าหน้าที่จะตรวจให้" : "✏️ อ่านยอดไม่ชัด — กรอกยอดตามสลิป");
                v.Tone = blocked ? "err" : reason == null ? "ok" : "warn";
                v.Hint = !lk.Locked && lk.HintAmount.HasValue ? "ระบบอ่านได้ประมาณ " + lk.HintAmount.Value.ToString("N2") + " บาท — ตรวจให้ตรงสลิปก่อนยืนยัน" : null;
                v.ApplyLockUi = true;
                v.SuppressLock = blocked;
                return v;
            }
            if (customerMode)
            {
                // ลูกค้า (ปิดยอดล็อก): บอกผลแบบสั้น — ทุกกรณีที่ไม่ใช่ธนาคารยืนยัน การจองจะรอเจ้าหน้าที่ตรวจ
                string msg = r.Status == SlipVerifyStatus.Verified && r.BankConfirmed
                    ? "✅ ตรวจสลิปกับธนาคารแล้ว ยอด " + (r.Amount ?? 0m).ToString("N2") + " บาท"
                    : r.Status == SlipVerifyStatus.Mismatch && r.Amount.HasValue
                        ? "⚠️ ยอดในสลิป " + r.Amount.Value.ToString("N2") + " บาท ไม่ตรงกับยอดที่กรอก — กรุณาแก้ยอดให้ตรงสลิป (หรือกดยืนยัน เจ้าหน้าที่จะตรวจให้)"
                        : "ℹ️ ได้รับสลิปแล้ว — เจ้าหน้าที่จะตรวจสอบและยืนยันการจองให้";
                v.CardHtml = "<div class='rv-hint' style='margin:6px 0;'>" + HttpUtility.HtmlEncode(msg) + "</div>";
                v.State = Edit;
                v.Message = "ℹ️ ได้รับสลิปแล้ว";
                v.Tone = "warn";
                v.Lock = lk;
                return v;
            }

            // ── พนักงาน ──
            v.StaffBranch = true;
            v.Lock = lk;
            v.CardHtml = r.ToHtml();
            bool problem = r.IsBlocking;
            bool locked = lk != null && lk.Locked;
            if (locked)
            {
                // ยอดล็อกตามสลิป: ช่องยอด = ยอดสลิป แก้ไม่ได้ (ไม่มีปุ่ม "ใช้ยอดตามสลิป"/ยืนยันยอดไม่ตรง) · แบ่งสลิปหลายการจองยังทำได้
                string over = due.HasValue ? SlipVerificationService.OverpayText(lk.Amount.Value, due.Value) : null;
                if (over != null)
                    v.CardHtml += "<div class='slip-lock-msg ok'>ℹ️ ยอดสลิปมากกว่ายอดค้าง " + due.Value.ToString("N2") + " บาท — "
                        + HttpUtility.HtmlEncode(over) + " (บันทึกเต็มยอดสลิป)</div>";
            }
            v.ShowStaffOpts = problem || locked || (r.Amount.HasValue && r.Difference.HasValue && r.Difference.Value != 0m);
            v.ShowUseAmount = !locked && r.Amount.HasValue && r.Difference.HasValue && r.Difference.Value != 0m
                              && r.Status != SlipVerifyStatus.Duplicate;
            v.ShowAllocate = r.CanAllocate
                || (locked && r.Status != SlipVerifyStatus.Duplicate && !r.IsAllocationContinuation);
            v.ShowOverride = r.CanOverride && canOverride
                && !(locked && r.Status == SlipVerifyStatus.Mismatch);
            // สลิปที่แบ่งใช้กับการจองอื่นไว้แล้ว → ไม่ล็อก ให้กรอกยอดส่วนของการจองนี้ (ไม่เกินยอดคงเหลือ — ตรวจซ้ำตอนบันทึก)
            bool splitRemainder = r.IsAllocationContinuation
                || (r.Status == SlipVerifyStatus.Duplicate && r.AllocationRemaining.HasValue && r.AllocationRemaining.Value > 0m);
            v.ApplyLockUi = lk != null && lk.Active;
            v.SuppressLock = splitRemainder;
            if (splitRemainder && r.AllocationRemaining.HasValue)
                v.CardHtml += "<div class='slip-lock-msg warn'>✏️ สลิปนี้แบ่งใช้กับการจองอื่นแล้ว — กรอกยอดของการจองนี้ไม่เกินยอดคงเหลือ "
                    + r.AllocationRemaining.Value.ToString("N2") + " บาท</div>";
            // สลิปก่อนยอด (หน้าจอง): จำผลไว้ให้หน้าเปิดช่องยอดตามผล — ซ้ำ (ไม่มียอดคงเหลือให้แบ่ง) = หยุด
            bool dupStop = r.Status == SlipVerifyStatus.Duplicate && !splitRemainder;
            bool lockedNow = locked && !splitRemainder;
            v.State = dupStop ? Stop : lockedNow ? Locked : Edit;
            v.Message = dupStop ? "⛔ สลิปนี้ถูกใช้ชำระไปแล้ว — แนบสลิปใบอื่น"
                : splitRemainder ? "✏️ สลิปนี้แบ่งใช้แล้ว — กรอกยอดส่วนของการจองนี้"
                : problem ? "⚠️ ตรวจพบปัญหา — ดูรายละเอียดใต้รูปสลิป"
                : lockedNow ? (lk.Source == SlipVerifySource.SlipOk
                    ? "✅ ธนาคารยืนยันยอดแล้ว" : "✅ อ่านยอดจากสลิปแล้ว (" + lk.Confidence + "%)")
                : "✏️ อ่านยอดไม่ชัด — ตรวจ/กรอกยอดตามสลิป";
            v.Tone = dupStop ? "err" : problem || !lockedNow ? "warn" : "ok";
            v.Hint = !lockedNow && lk != null && lk.HintAmount.HasValue
                ? "ระบบอ่านได้ประมาณ " + lk.HintAmount.Value.ToString("N2") + " บาท — ตรวจให้ตรงสลิป" : null;
            return v;
        }
    }
}
