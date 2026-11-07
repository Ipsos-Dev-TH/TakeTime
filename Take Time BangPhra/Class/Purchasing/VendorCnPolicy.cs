using System;

namespace Take_Time_BangPhra.Purchasing
{
    /// <summary>สถานะใบลดหนี้ของการคืนสินค้า (Purchase_Return_Cn_Link.Cn_Status — PHASE19_39)</summary>
    public static class ReturnCnStatus
    {
        /// <summary>วางบิลแล้ว (คู่บัญชีเจ้าหนี้/เงินคืน) แต่ยังไม่ได้ใบลดหนี้ — PRT ลงแบบไม่กลับภาษีซื้อ รอ CN</summary>
        public const string Pending = "PENDING";
        /// <summary>ผูกใบลดหนี้ NextAcc แล้ว — CN เป็นตัวจริง (PRT ถูกกลับ/ไม่เคยลง) + ปรับต้นทุน PRT-…-DIF</summary>
        public const string Linked = "LINKED";
        /// <summary>ไม่ต้องมีใบลดหนี้ (ยังไม่วางบิล = ลด GRNI) หรือผู้ใช้เลือกว่าผู้ขายไม่ออก — PRT เป็นรายการจริง</summary>
        public const string NotRequired = "NOT_REQUIRED";
    }

    /// <summary>JE ของ PRT ตามโหมด (Purchase_Return_Cn_Link.Prt_Journal)</summary>
    public static class ReturnPrtJournal
    {
        /// <summary>JE PRT ครบ (เดิม — รวมกลับภาษีซื้อตามที่ติ๊ก)</summary>
        public const string Full = "FULL";
        /// <summary>JE PRT ระหว่างรอใบลดหนี้: ไม่กลับภาษีซื้อ (§82/10 กลับเดือนที่ได้ใบ) — ถูกกลับทั้งใบเมื่อผูก CN</summary>
        public const string Pending = "PENDING";
        /// <summary>ไม่มี JE PRT (ผูกใบลดหนี้ที่บันทึกก่อนแล้ว) — ตัดจำนวน/ต้นทุน + PRT-…-DIF เท่านั้น</summary>
        public const string None = "NONE";
    }

    /// <summary>ผู้ใช้เลือกในหน้าคืนสินค้า</summary>
    public static class ReturnCnChoice
    {
        public const string Pending = "PENDING";   // รอใบลดหนี้ (ค่าเริ่มต้นเมื่อวางบิลแล้ว)
        public const string Link = "LINK";         // ใบลดหนี้บันทึกในหน้า OCR แล้ว
        public const string NoCn = "NONE";         // ผู้ขายไม่ออกใบลดหนี้ — ลงครบที่นี่
    }

    /// <summary>ผลตัดสินโหมดของการคืนหนึ่งครั้ง</summary>
    public sealed class ReturnCnDecision
    {
        public string CnStatus;
        public string PrtJournal;
        /// <summary>ภาษีซื้อที่ JE PRT กลับ (0 = รอใบลดหนี้/ไม่มี JE)</summary>
        public decimal PrtVatReversal;
        /// <summary>คืนเงินสดระหว่างรอใบลดหนี้: ส่วน VAT ที่ได้คืนพักที่เจ้าหนี้ (ไม่แตะ 11610 จนได้ใบ — §82/10)</summary>
        public bool HoldVatOnPayable;
        public string Error;
        public string Explanation;
    }

    /// <summary>
    /// กติกาใบลดหนี้ผู้ขาย ↔ คืนสินค้า ↔ เครดิตผู้ขาย (pure — ทดสอบด้วย mono ได้; CLAUDE.md ข้อ 3 "ใบลดหนี้ผู้ขาย = ตัวจริง").
    ///
    /// หลักการ: ภ.พ.30 ของ NextAcc อ่าน "เอกสาร" ใบลดหนี้ ไม่อ่าน JE (TaxService.cs:675-690) ⇒ การลดภาษีซื้อ §82/10 ต้องมาจากใบลดหนี้ใน NextAcc
    /// ⇒ เมื่อมีใบลดหนี้ ใบลดหนี้เป็นรายการบัญชี/ภาษีตัวจริง ส่วนหน้า "คืนสินค้า" เหลือแค่จำนวน/ต้นทุน; ถ้าคืนก่อนได้ใบ JE PRT ลงไปก่อน
    /// (ไม่กลับ VAT) แล้วถูกกลับทั้งใบ (PRT-…-REV) ตอนผูกใบลดหนี้ ⇒ ยอดไม่ซ้อน.
    /// </summary>
    public static class VendorCnPolicy
    {
        public const decimal Tol = 0.005m;

        public static decimal R2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

        /// <summary>คู่บัญชีของ PRT ที่ "วางบิลแล้ว" (ใบกำกับ/ภาษีซื้อบันทึกแล้ว) — มีใบลดหนี้ตามมาได้</summary>
        public static bool IsBilledCounter(string counterKey) =>
            counterKey == "ACCOUNTS_PAYABLE" || counterKey == "CASH";

        /// <summary>
        /// ตัดสินโหมดของการคืน:
        ///   • ยังไม่วางบิล (GRNI/NONE) → NOT_REQUIRED + JE PRT ตามเดิม (ลด GRNI; ใบแจ้งหนี้ผู้ขายควรออกยอดสุทธิ) — เลือก "ผูก" ไม่ได้
        ///   • วางบิลแล้ว + PENDING → JE PRT ไม่กลับ VAT (คืนเงินสด: ส่วน VAT ที่ได้คืนพักเจ้าหนี้) รอ CN
        ///   • วางบิลแล้ว + LINK    → ไม่มี JE PRT (CN ลงแล้ว) — ตัดจำนวน/ต้นทุน + DIF
        ///   • วางบิลแล้ว + NONE    → JE PRT ครบเดิม (กลับ VAT ตามที่ติ๊ก) — ผู้ขายไม่ออกใบลดหนี้
        /// </summary>
        public static ReturnCnDecision Decide(string counterKey, string choice, decimal planVatReversal)
        {
            var d = new ReturnCnDecision();
            choice = (choice ?? "").Trim().ToUpperInvariant();
            if (!IsBilledCounter(counterKey))
            {
                if (choice == ReturnCnChoice.Link)
                {
                    d.Error = "การรับของนี้ยังไม่วางบิล (ลดยอด GRNI/ไม่มี JE) — ผูกใบลดหนี้ไม่ได้: ใบแจ้งหนี้ของผู้ขายควรออกยอดสุทธิหลังหักของที่คืน";
                    return d;
                }
                d.CnStatus = ReturnCnStatus.NotRequired;
                d.PrtJournal = ReturnPrtJournal.Full;
                d.PrtVatReversal = 0m;
                d.Explanation = "ยังไม่วางบิล — การคืนลดยอดพักรับของ ไม่ต้องรอใบลดหนี้";
                return d;
            }
            switch (choice)
            {
                case ReturnCnChoice.Link:
                    d.CnStatus = ReturnCnStatus.Linked;
                    d.PrtJournal = ReturnPrtJournal.None;
                    d.PrtVatReversal = 0m;
                    d.Explanation = "ผูกใบลดหนี้ที่บันทึกแล้ว — ใบลดหนี้ลงเจ้าหนี้/ภาษีซื้อไว้แล้ว หน้านี้ตัดจำนวน + ปรับต้นทุนเฉลี่ย (PRT-…-DIF) เท่านั้น";
                    return d;
                case ReturnCnChoice.NoCn:
                    d.CnStatus = ReturnCnStatus.NotRequired;
                    d.PrtJournal = ReturnPrtJournal.Full;
                    d.PrtVatReversal = R2(Math.Max(0m, planVatReversal));
                    d.Explanation = "ผู้ขายไม่ออกใบลดหนี้ — ลงบัญชีครบที่หน้านี้ (ภ.พ.30 ใน NextAcc จะไม่ลดภาษีซื้อเพราะไม่มีเอกสาร)";
                    return d;
                default:
                    d.CnStatus = ReturnCnStatus.Pending;
                    d.PrtJournal = ReturnPrtJournal.Pending;
                    d.PrtVatReversal = 0m;
                    d.HoldVatOnPayable = counterKey == "CASH" && planVatReversal > Tol;
                    d.Explanation = "รอใบลดหนี้จากผู้ขาย — ลงบัญชีชั่วคราวแบบไม่กลับภาษีซื้อ (§82/10 กลับเดือนที่ได้รับใบ)"
                        + (d.HoldVatOnPayable ? "; ส่วน VAT ที่ได้เงินคืนพักไว้ที่เจ้าหนี้ผู้ขาย" : "")
                        + " · เมื่อสแกนใบลดหนี้ในหน้า OCR แล้วผูกเลข PRT นี้ ระบบกลับรายการนี้ทั้งใบให้อัตโนมัติ";
                    return d;
            }
        }

        /// <summary>
        /// JE ปรับต้นทุน (PRT-…-DIF) หลังผูกใบลดหนี้: ใบลดหนี้ Cr บัญชีสินค้าคงเหลือด้วยราคาซื้อ (ยอดบรรทัดที่ลงบัญชีสินค้า = cnInventoryNet)
        /// แต่ TakeTime ตัดสต๊อก ณ ต้นทุนเฉลี่ย (carryingCost — CostService.ApplyReturn) ⇒ ส่วนต่างไป PURCHASE_RETURNS.
        /// คืน &gt; 0 = Dr INVENTORY / Cr PURCHASE_RETURNS · &lt; 0 = Dr PURCHASE_RETURNS / Cr INVENTORY · 0 = ไม่ต้องลง
        /// (สูตรเดียวกันใช้ได้แม้บรรทัดใบลดหนี้ลงบัญชีอื่น: inventory net = 0 ⇒ Cr สินค้าคงเหลือเต็ม carrying)
        /// </summary>
        public static decimal CostDifference(decimal cnInventoryNet, decimal carryingCost) =>
            R2(R2(cnInventoryNet) - R2(carryingCost));

        /// <summary>
        /// ขาเงินที่ใช้จริงของใบลด/เพิ่มหนี้ผู้ขาย. NextAcc ลงคู่บัญชี "111 เงินสด" เมื่อใบต้นทางชำระครบ (DocumentService.cs:17899, :17920-17922)
        /// ⇒ ไม่มีเงินสดเข้า/ออกจริงในวันนั้น: AUTO + ใบต้นทางจ่ายครบ = VENDOR (CN: เครดิตกับผู้ขาย · DN: ตั้งเจ้าหนี้รอจ่าย).
        /// การคืนสินค้าที่ผูกไว้บันทึกว่า "ผู้ขายคืนเงิน" ⇒ BANK เข้าบัญชีเดียวกับที่ PRT ลง (ถ้ายังไม่เลือก).
        /// </summary>
        public static string EffectiveSettlement(string settlement, bool sourcePaidInFull, bool linkedReturnIsRefund)
        {
            string s = string.IsNullOrWhiteSpace(settlement) ? "AUTO" : settlement.Trim().ToUpperInvariant();
            if (s != "AUTO") return s;
            if (!sourcePaidInFull) return "AUTO";
            return linkedReturnIsRefund ? "BANK" : "VENDOR";
        }

        /// <summary>บัญชีเครดิตกับผู้ขาย: VENDOR_CREDIT_RECEIVABLE ถ้าผูก mapping ไว้ ไม่งั้นเจ้าหนี้การค้า (ยอดเดบิต)</summary>
        public static string VendorCreditAccount(string vendorCreditCode, string payableCode, out bool usedFallback)
        {
            usedFallback = string.IsNullOrWhiteSpace(vendorCreditCode);
            return usedFallback ? (string.IsNullOrWhiteSpace(payableCode) ? null : payableCode.Trim()) : vendorCreditCode.Trim();
        }

        /// <summary>ตรวจการหักเครดิตผู้ขายตอนจ่ายใบซื้อเชื่อ (หน้า CreditPurchasePayment). null = ผ่าน</summary>
        public static string CheckOffset(decimal billTotal, decimal cashAmount, decimal offset, decimal available)
        {
            if (offset < 0 || cashAmount < 0) return "ยอดติดลบไม่ได้";
            if (offset <= Tol) return cashAmount > Tol ? null : "ยอดจ่ายต้องมากกว่า 0";
            if (offset > available + Tol) return "หักเครดิตผู้ขายได้ไม่เกินยอดคงเหลือ " + available.ToString("#,##0.00");
            if (cashAmount + offset > billTotal + 0.01m) return "ยอดจ่าย + หักเครดิต (" + (cashAmount + offset).ToString("#,##0.00") + ") มากกว่ายอดค้างจ่าย " + billTotal.ToString("#,##0.00");
            return null;
        }

        /// <summary>ถึงเวลาเตือน "คืนสินค้าแล้วยังไม่ได้ใบลดหนี้" หรือยัง (เกิน days วัน และไม่เตือนซ้ำภายใน 24 ชม.)</summary>
        public static bool ReminderDue(DateTime createdAt, DateTime? lastRemindedAt, DateTime now, int days)
        {
            if (days < 1) days = 1;
            if ((now - createdAt).TotalDays < days) return false;
            return !lastRemindedAt.HasValue || (now - lastRemindedAt.Value).TotalHours >= 23.5;
        }

        /// <summary>เลขอ้างอิง JE ที่สร้างจากการผูก (คงที่ต่อการคืน ⇒ กันซ้ำทั้งคิวและ NextAcc)</summary>
        public static string ReverseRef(string returnRef) => (returnRef ?? "").Trim() + "-REV";
        public static string CostDiffRef(string returnRef) => (returnRef ?? "").Trim() + "-DIF";
    }
}
