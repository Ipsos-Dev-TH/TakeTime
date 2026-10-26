using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Caching;
using System.Web.Hosting;

namespace Take_Time_BangPhra.Payments.SlipCheck
{
    /// <summary>
    /// ตรวจสลิปโอนก่อนออกเอกสาร (CLAUDE.md ข้อ 18) — นโยบายเจ้าของ:
    ///   ระดับ 1 SlipOK (ฟรี 100 สลิป/เดือน — นับเองต่อเดือน + ตรวจรหัสโควต้าหมด 1003/1004)
    ///   ระดับ 2 อ่านตัวเลขยอดโอนจากรูปเอง (Tesseract ในเครื่อง)
    ///   ระดับ 3 มีปัญหาอะไรก็ตาม → ข้ามการตรวจ (UNVERIFIED_BYPASS) ใช้ยอดที่พนักงานลง แต่ติดธงให้ตรวจภายหลัง
    ///   + อ่าน mini-QR ในเครื่องทุกครั้ง (ฟรี) ไว้กันสลิปซ้ำด้วย (ธนาคารผู้โอน + transRef)
    ///
    /// ปิดสวิตช์ (SlipVerify_Enabled ≠ 1) หรือยังไม่รัน PHASE19_34 = ทุกหน้าทำงานเหมือนเดิม (ไม่เรียกอะไรเลย)
    /// ทุกเมธอดไม่ throw ออกไปถึงหน้า — ตรวจไม่ได้ = bypass
    /// </summary>
    public static class SlipVerificationService
    {
        public const string CfgEnabled = "SlipVerify_Enabled";
        public const string CfgBranchId = "SlipOK_BranchId";
        public const string CfgApiKey = "SlipOK_ApiKey_Encrypted";   // เข้ารหัสด้วย code.Crypt (AppCfg SecretKeys) แบบ Nexaacc_ApiKey_Encrypted
        public const string CfgQuota = "SlipOK_Monthly_Quota";
        public const string CfgTolerance = "SlipVerify_Amount_Tolerance";
        public const string CfgCheckReceiver = "SlipVerify_Check_Receiver";
        public const string CfgTimeout = "SlipOK_Timeout_Sec";
        public const string QueueHoldStatus = "AWAITING_SLIP";

        private static string ConnStr => ConfigurationManager.ConnectionStrings["TaketimeConnectionString"]?.ConnectionString;

        // ── ค่าตั้ง ─────────────────────────────────────────────────────────────

        public static bool IsEnabled
        {
            get { try { return AppCfg.GetBool(CfgEnabled, false); } catch { return false; } }
        }

        public static decimal Tolerance
        {
            get
            {
                decimal t;
                return decimal.TryParse(AppCfg.Get(CfgTolerance, "1.00"), NumberStyles.Any, CultureInfo.InvariantCulture, out t) && t >= 0m
                    ? t : 1.00m;
            }
        }

        public static int MonthlyQuota => Math.Max(0, AppCfg.GetInt(CfgQuota, 100));
        public static bool CheckReceiver => AppCfg.GetBool(CfgCheckReceiver, true);

        public static ISlipVerifier CreateVendor()
        {
            return new SlipOkVerifier(AppCfg.Get(CfgBranchId, ""), AppCfg.Get(CfgApiKey, ""), AppCfg.GetInt(CfgTimeout, 10));
        }

        public static string YearMonth(DateTime d) => d.ToString("yyyyMM", CultureInfo.InvariantCulture);

        // ── schema (guarded — ยังไม่รัน PHASE19_34 = ไม่บันทึกอะไร) ───────────────

        private static readonly Dictionary<string, Tuple<bool, DateTime>> _schema = new Dictionary<string, Tuple<bool, DateTime>>();

        private static bool Has(string key, string sql)
        {
            lock (_schema)
            {
                Tuple<bool, DateTime> c;
                if (_schema.TryGetValue(key, out c) && (c.Item1 || DateTime.Now - c.Item2 < TimeSpan.FromMinutes(5))) return c.Item1;
            }
            bool has = false;
            try
            {
                using (var con = new SqlConnection(ConnStr))
                using (var cmd = new SqlCommand(sql, con))
                {
                    con.Open();
                    object o = cmd.ExecuteScalar();
                    has = o != null && o != DBNull.Value;
                }
            }
            catch { has = false; }
            lock (_schema) _schema[key] = Tuple.Create(has, DateTime.Now);
            return has;
        }

        public static bool SchemaReady => Has("verifycol", "SELECT COL_LENGTH('dbo.Payment_Slips','Verify_Status')");
        public static bool UsageReady => Has("usage", "SELECT OBJECT_ID('dbo.Slip_Verify_Usage','U')");
        public static bool AllocReady => Has("alloc", "SELECT OBJECT_ID('dbo.Payment_Slip_Allocation','U')");
        public static bool LogReady => Has("log", "SELECT OBJECT_ID('dbo.Slip_Verify_Log','U')");
        private static bool OcrColsReady => Has("ocrcol", "SELECT COL_LENGTH('dbo.Payment_Slips','OCR_Amount')");
        private static bool PaidHowBankCols => Has("phbank", "SELECT COL_LENGTH('dbo.Account_Paid_How','Bank_Account_No')");
        private static bool PaidHowNexaBank => Has("phnexa", "SELECT COL_LENGTH('dbo.Account_Paid_How','Nexaacc_BankAccountId')");
        private static bool PaidHowChannelType => Has("phct", "SELECT COL_LENGTH('dbo.Account_Paid_How','Channel_Type')");

        public static void InvalidateSchema() { lock (_schema) _schema.Clear(); }

        // ── ตรวจ ────────────────────────────────────────────────────────────────

        /// <summary>ตรวจไฟล์บนดิสก์ (path จริง หรือ URL ในเว็บแบบ ~/… / Upload/…)</summary>
        public static SlipVerifyResult VerifyFile(string pathOrUrl, decimal? expected, SlipVerifyContext ctx)
        {
            byte[] bytes = null;
            string name = null;
            try
            {
                string p = MapSlipPath(pathOrUrl);
                if (!string.IsNullOrEmpty(p) && File.Exists(p)) { bytes = File.ReadAllBytes(p); name = Path.GetFileName(p); }
            }
            catch { }
            if (bytes == null)
            {
                return new SlipVerifyResult
                {
                    Status = SlipVerifyStatus.UnverifiedBypass,
                    ExpectedAmount = expected,
                    Message = "ไม่พบไฟล์สลิปบนเซิร์ฟเวอร์ — ข้ามการตรวจ (ตรวจด้วยตาภายหลัง)"
                };
            }
            return Verify(bytes, name, expected, ctx);
        }

        /// <summary>ตรวจรูปสลิป: อ่านข้อเท็จจริง (cache ตาม hash) แล้วตัดสินเทียบยอดที่ลง</summary>
        public static SlipVerifyResult Verify(byte[] bytes, string fileName, decimal? expected, SlipVerifyContext ctx)
        {
            ctx = ctx ?? new SlipVerifyContext();
            try
            {
                SlipInspection insp = Inspect(bytes, fileName, ctx.ForceRefresh);
                SlipVerifyResult r = Evaluate(insp, expected, ctx);
                TryLog(ctx, r, false);
                return r;
            }
            catch (Exception ex)
            {
                Log("SlipVerify error: " + ex.GetType().Name + " " + ex.Message);
                return new SlipVerifyResult
                {
                    Status = SlipVerifyStatus.UnverifiedBypass,
                    ExpectedAmount = expected,
                    Message = "ตรวจสลิปไม่ได้ (" + ex.GetType().Name + ") — ข้ามการตรวจ ใช้ยอดที่ลง และติดธงให้ตรวจภายหลัง"
                };
            }
        }

        public static string Sha256(byte[] b)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(b)).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>ข้อเท็จจริงของรูป: QR (ฟรี) → SlipOK (ถ้ามีโควต้า) → OCR ในเครื่อง (ถ้า SlipOK ไม่ได้ยอด)</summary>
        public static SlipInspection Inspect(byte[] bytes, string fileName, bool force)
        {
            string hash = Sha256(bytes ?? new byte[0]);
            string key = "slipinsp:" + hash;
            if (!force)
            {
                var cached = HttpRuntime.Cache[key] as SlipInspection;
                if (cached != null) return cached;
            }

            var insp = new SlipInspection { FileHash = hash };
            insp.Qr = SlipQrDecoder.TryDecode(bytes);
            if (insp.Qr == null) insp.Notes.Add("ไม่พบ/อ่าน QR ในรูปไม่ได้");
            else if (insp.Qr.IsPaymentQr) insp.Notes.Add("รูปนี้เป็น QR รับเงิน (PromptPay) ไม่ใช่สลิปโอน");
            else if (insp.Qr.IsSlip && !insp.Qr.CrcValid) insp.Notes.Add("QR ของสลิป CRC ไม่ถูกต้อง (รูปอาจถูกแก้ไข/อ่านผิด)");

            // ── ระดับ 1: SlipOK ──
            var vendor = CreateVendor();
            if (!vendor.IsConfigured)
                insp.VendorSkipReason = "ยังไม่ได้ตั้งค่า SlipOK";
            else if (IsQuotaExhausted(SlipOkVerifier.ProviderName))
                insp.VendorSkipReason = "โควต้า SlipOK เดือนนี้หมดแล้ว (" + MonthlyQuota + ")";
            else if (insp.Qr != null && insp.Qr.IsPaymentQr)
                insp.VendorSkipReason = "ไม่ใช่สลิป";
            else
            {
                string payload = insp.Qr != null && insp.Qr.IsSlip && !insp.Qr.IsTrueMoney ? insp.Qr.RawPayload : null;
                SlipVendorResponse vr = vendor.Verify(bytes, fileName, payload);
                insp.Vendor = vr;
                if (vr.Charged) IncrementUsage(SlipOkVerifier.ProviderName);
                if (vr.Kind == SlipVendorResponse.KindQuota)
                {
                    MarkQuotaExhausted(SlipOkVerifier.ProviderName);
                    insp.Notes.Add("SlipOK แจ้งโควต้า/แพ็กเกจหมด (" + vr.Code + ") — ใช้ OCR ในเครื่องแทน");
                }
                else if (vr.Kind == SlipVendorResponse.KindConfig)
                    insp.Notes.Add("SlipOK ปฏิเสธการตั้งค่า (" + vr.Code + " " + vr.Message + ") — ตรวจ Branch ID / API key");
                Log("SlipOK " + vr.Kind + " code=" + (vr.Code ?? "-") + " " + vr.ElapsedMs + "ms hash=" + hash.Substring(0, 12));
            }

            // ── ระดับ 2: อ่านยอดจากรูปเอง (เมื่อ SlipOK ไม่ได้ข้อมูลรายการ) ──
            bool vendorHasAmount = insp.Vendor != null && insp.Vendor.HasData && insp.Vendor.Amount.HasValue;
            if (!vendorHasAmount)
            {
                insp.OcrAttempted = true;
                var ocr = LocalSlipAmountReader.ReadText(bytes);
                if (ocr.Ok) insp.OcrText = ocr.Text;
                else insp.OcrError = ocr.Error;
            }

            // ผลชั่วคราวของธนาคาร (1009/1010) ห้าม cache นาน — ให้กดตรวจใหม่ได้ผลจริง
            bool transient = insp.Vendor != null && (insp.Vendor.Kind == SlipVendorResponse.KindPendingBank
                                                     || insp.Vendor.Kind == SlipVendorResponse.KindError);
            try
            {
                HttpRuntime.Cache.Insert(key, insp, null, DateTime.UtcNow.AddMinutes(transient ? 2 : 60), Cache.NoSlidingExpiration);
            }
            catch { }
            return insp;
        }

        /// <summary>ตัดสินผลจากข้อเท็จจริง + ยอดที่ลง (ไม่เรียกเน็ต — เรียกซ้ำได้)</summary>
        public static SlipVerifyResult Evaluate(SlipInspection insp, decimal? expected, SlipVerifyContext ctx)
        {
            ctx = ctx ?? new SlipVerifyContext();
            var r = new SlipVerifyResult { ExpectedAmount = expected, FileHash = insp.FileHash };
            r.Notes.AddRange(insp.Notes);
            decimal tol = Tolerance;
            SlipVendorResponse v = insp.Vendor;
            bool vendorData = v != null && v.HasData && v.Amount.HasValue;

            if (insp.Qr != null && insp.Qr.IsSlip)
            {
                r.QrPayload = insp.Qr.RawPayload;
                r.TransRef = insp.Qr.TransRef;
                r.SendingBank = insp.Qr.SendingBank;
                r.Source = SlipVerifySource.QrOnly;
            }

            if (vendorData)
            {
                r.Source = SlipVerifySource.SlipOk;
                r.Amount = v.Amount;
                r.TransDate = v.TransDate;
                if (!string.IsNullOrEmpty(v.TransRef)) r.TransRef = v.TransRef;
                if (!string.IsNullOrEmpty(v.SendingBank)) r.SendingBank = v.SendingBank;
                r.SenderName = v.SenderName;
                r.ReceiverName = v.ReceiverName;
                r.ReceiverAcctMasked = !string.IsNullOrEmpty(v.ReceiverAcctMasked) ? v.ReceiverAcctMasked : v.ReceiverProxyMasked;
                if (v.Kind == SlipVendorResponse.KindDuplicate)
                    r.Notes.Add("SlipOK เคยเห็นสลิปนี้แล้ว (ตรวจซ้ำ) — ตัดสินสลิปซ้ำจากฐานข้อมูลของเราเอง");
            }
            else if (!string.IsNullOrEmpty(insp.OcrText))
            {
                int conf; string why;
                decimal? amt = LocalSlipAmountReader.ExtractAmount(insp.OcrText, expected, out conf, out why);
                r.OcrRaw = insp.OcrText.Length > 1500 ? insp.OcrText.Substring(0, 1500) : insp.OcrText;
                r.OcrConfidence = conf;
                if (amt.HasValue)
                {
                    r.Amount = amt;
                    r.OcrAmount = amt;
                    r.Source = SlipVerifySource.LocalOcr;
                    r.Notes.Add("OCR: " + why);
                }
                else r.Notes.Add("OCR อ่านยอดไม่ได้: " + why);
            }
            else if (insp.OcrAttempted && !string.IsNullOrEmpty(insp.OcrError))
                r.Notes.Add(insp.OcrError);

            if (v != null && !vendorData)
            {
                if (v.Kind == SlipVendorResponse.KindNotFound) r.Notes.Add("SlipOK: ธนาคารไม่พบรายการนี้ / QR หมดอายุ (1011) — สลิปอาจปลอม ตรวจกับบัญชีธนาคาร");
                else if (v.Kind == SlipVendorResponse.KindPendingBank) r.Notes.Add("SlipOK: ธนาคารยังไม่ยืนยัน (" + v.Code + ") " + (v.Code == "1010" && !string.IsNullOrEmpty(v.Message) ? v.Message : "ลองตรวจใหม่อีกครู่"));
                else if (v.Kind == SlipVendorResponse.KindNoQr) r.Notes.Add("SlipOK: ไม่พบ QR สลิปในรูป (" + v.Code + ")");
                else if (v.Kind == SlipVendorResponse.KindError) r.Notes.Add(v.Message ?? "SlipOK ผิดพลาด");
            }
            if (!string.IsNullOrEmpty(insp.VendorSkipReason)) r.Notes.Add(insp.VendorSkipReason);

            // ── ลำดับตัดสิน: ซ้ำ > ผู้รับผิด > ยอดไม่ตรง > ตรง ──
            var prior = string.IsNullOrEmpty(r.TransRef) ? null : FindPriorUse(r.SendingBank, r.TransRef, ctx);
            if (prior != null)
            {
                r.DuplicateOfSlipId = prior.SlipId;
                decimal slipTotal = prior.SlipAmount ?? r.Amount ?? 0m;
                if (prior.HasAllocation && slipTotal > 0m)
                {
                    decimal remaining = slipTotal - prior.Allocated;
                    r.AllocationRemaining = remaining;
                    if (expected.HasValue && expected.Value <= remaining + tol && remaining > 0m && !ctx.CustomerSelfService)
                    {
                        r.IsAllocationContinuation = true;
                        r.Status = SlipVerifyStatus.Duplicate;
                        r.Message = "สลิปนี้ถูกแบ่งใช้กับ " + prior.Describe + " แล้ว — ใช้ยอดคงเหลือ " + remaining.ToString("N2")
                            + " บาท กับรายการนี้ " + expected.Value.ToString("N2") + " บาท";
                        return Finish(r);
                    }
                }
                r.Status = SlipVerifyStatus.Duplicate;
                r.CanOverride = false;
                r.Message = "สลิปนี้ (อ้างอิง " + r.TransRef + ") เคยใช้แล้วกับ " + prior.Describe
                    + (r.AllocationRemaining.HasValue ? " — ยอดคงเหลือของสลิป " + r.AllocationRemaining.Value.ToString("N2") + " บาท ไม่พอ" : "")
                    + " · ห้ามใช้สลิปเดิมซ้ำ";
                return Finish(r);
            }

            if (r.Source == SlipVerifySource.SlipOk && (CheckReceiver || v.Kind == SlipVendorResponse.KindReceiverMismatch))
            {
                string why;
                int? matched = MatchReceiver(v, out why);
                if (matched.HasValue) r.MatchedPaidHowId = matched;
                else if (why != null)
                {
                    r.Status = SlipVerifyStatus.WrongReceiver;
                    r.CanOverride = true;
                    r.Message = "โอนเข้าบัญชีที่ไม่ใช่ของร้าน (" + (r.ReceiverName ?? "-") + " " + (r.ReceiverAcctMasked ?? "") + ") — " + why;
                    return Finish(r);
                }
            }

            if (r.Amount.HasValue && expected.HasValue)
            {
                decimal diff = r.Amount.Value - expected.Value;
                r.Difference = diff;
                if (Math.Abs(diff) <= tol)
                {
                    r.Status = SlipVerifyStatus.Verified;
                    r.Message = r.Source == SlipVerifySource.SlipOk ? "ยอดตรงกับธนาคาร" : "ยอดที่อ่านจากรูปตรงกับยอดที่ลง (ไม่ใช่ข้อมูลธนาคาร)";
                }
                else
                {
                    r.Status = SlipVerifyStatus.Mismatch;
                    r.CanOverride = true;
                    r.CanAllocate = diff > 0m;   // โอนมากกว่า = อาจรวมหลายการจองในสลิปเดียว
                    r.Message = "สลิป " + r.Amount.Value.ToString("N2") + " / ลง " + expected.Value.ToString("N2") + " / ต่าง "
                        + diff.ToString("+#,##0.00;-#,##0.00") + " บาท"
                        + (r.Source == SlipVerifySource.LocalOcr ? " (อ่านจากรูป — ถ้าอ่านผิดให้ยืนยันด้วยเหตุผล)" : "");
                }
                return Finish(r);
            }
            if (r.Amount.HasValue)
            {
                r.Status = SlipVerifyStatus.Verified;
                r.Message = "บันทึกยอดจากสลิปไว้ (ไม่มียอดให้เทียบ)";
                return Finish(r);
            }

            r.Status = v != null && v.Kind == SlipVendorResponse.KindPendingBank ? SlipVerifyStatus.PendingBank : SlipVerifyStatus.UnverifiedBypass;
            r.Message = (r.Status == SlipVerifyStatus.PendingBank ? "ธนาคารยังไม่ยืนยันรายการ" : "ตรวจยอดจากสลิปไม่ได้")
                + " — ข้ามการตรวจ ใช้ยอดที่ลง และติดธงให้ตรวจภายหลัง";
            return Finish(r);
        }

        private static SlipVerifyResult Finish(SlipVerifyResult r)
        {
            if (r.Notes.Count > 0)
            {
                string extra = string.Join(" · ", r.Notes.Distinct().Take(4));
                r.Message = string.IsNullOrEmpty(r.Message) ? extra : r.Message + " [" + extra + "]";
            }
            return r;
        }

        // ── การตัดสินที่หน้า ───────────────────────────────────────────────────

        /// <summary>
        /// ไป/หยุด/พักรอพนักงาน — customerSelfService: ออกเอกสารอัตโนมัติเฉพาะ "ธนาคารยืนยัน + ยอดตรง + ผู้รับถูก" เท่านั้น
        /// นอกนั้น (OCR/bypass/ไม่ตรง) = พักรอพนักงานตรวจ (HoldForStaff)
        /// </summary>
        public static SlipGateDecision Decide(SlipVerifyResult r, bool customerSelfService, bool allocateRequested,
            bool overrideRequested, string overrideReason, bool canOverride)
        {
            var d = new SlipGateDecision();
            if (r == null) return d;
            if (customerSelfService)
            {
                if (r.Status == SlipVerifyStatus.Verified && r.BankConfirmed && !r.IsAllocationContinuation)
                {
                    d.UseAmount = r.Amount;
                    return d;
                }
                d.Proceed = false;
                d.HoldForStaff = true;
                return d;
            }

            string reason = (overrideReason ?? "").Trim();
            switch (r.Status)
            {
                case SlipVerifyStatus.Verified:
                    if (r.BankConfirmed && r.Amount.HasValue) d.UseAmount = r.Amount;   // ยอดธนาคารแทนยอดที่ลง (ต่าง ≤ tolerance)
                    return d;
                case SlipVerifyStatus.Mismatch:
                    if (allocateRequested && r.CanAllocate) { d.AllocationUsed = true; return d; }
                    if (overrideRequested && canOverride && reason.Length >= 3) { d.OverrideUsed = true; d.OverrideReason = reason; return d; }
                    d.Proceed = false;
                    d.BlockMessage = r.Message + (r.CanAllocate
                        ? " — แก้ยอดตามสลิป / ติ๊ก \"แบ่งสลิปนี้กับหลายการจอง\" / หรือ Owner-Admin ยืนยันพร้อมเหตุผล"
                        : " — แก้ยอดตามสลิป หรือ Owner-Admin ยืนยันพร้อมเหตุผล");
                    return d;
                case SlipVerifyStatus.Duplicate:
                    if (r.IsAllocationContinuation) { d.AllocationUsed = true; return d; }
                    d.Proceed = false;
                    d.BlockMessage = r.Message;
                    return d;
                case SlipVerifyStatus.WrongReceiver:
                    if (overrideRequested && canOverride && reason.Length >= 3) { d.OverrideUsed = true; d.OverrideReason = reason; return d; }
                    d.Proceed = false;
                    d.BlockMessage = r.Message + " — ตรวจบัญชีรับเงิน หรือ Owner-Admin ยืนยันพร้อมเหตุผล";
                    return d;
                default:
                    return d;   // UNVERIFIED_BYPASS / PENDING_BANK → ผ่าน (ติดธงในแถวสลิป)
            }
        }

        /// <summary>Owner/Admin เท่านั้นที่ override ได้</summary>
        public static bool CanOverrideCurrentUser()
        {
            try
            {
                string role = HttpContext.Current?.Session?["User"]?.ToString() ?? "";
                return string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        public static string CurrentUserName()
        {
            try
            {
                var s = HttpContext.Current?.Session;
                return s?["UserName"]?.ToString() ?? s?["username"]?.ToString() ?? s?["User"]?.ToString() ?? "SYSTEM";
            }
            catch { return "SYSTEM"; }
        }

        /// <summary>
        /// ยืนยันซ้ำสองครั้ง (หน้าที่ไม่มีช่องเหตุผล — กิจกรรม/รูมเซอร์วิส): ครั้งแรกจำไว้ 10 นาที ครั้งที่สองภายในเวลา = ยืนยันแล้ว
        /// </summary>
        public static bool ConsumeSecondClick(string key)
        {
            try
            {
                var s = HttpContext.Current?.Session;
                if (s == null) return false;
                string k = "slipack:" + key;
                object o = s[k];
                if (o is DateTime && DateTime.Now - (DateTime)o < TimeSpan.FromMinutes(10)) { s.Remove(k); return true; }
                s[k] = DateTime.Now;
            }
            catch { }
            return false;
        }

        // ── สลิปซ้ำ ─────────────────────────────────────────────────────────────

        private class PriorUse
        {
            public long? SlipId;
            public decimal? SlipAmount;
            public decimal Allocated;
            public bool HasAllocation;
            public string Describe;
        }

        private static PriorUse FindPriorUse(string bank, string transRef, SlipVerifyContext ctx)
        {
            if (string.IsNullOrEmpty(transRef)) return null;
            try
            {
                using (var con = new SqlConnection(ConnStr))
                {
                    con.Open();
                    if (SchemaReady)
                    {
                        using (var cmd = new SqlCommand(@"
                            SELECT TOP 1 ps.ID, ps.Reservation_ID, ps.Slip_Amount, CAST(ps.Account_Receipt_ID AS NVARCHAR(50)) AS Rcpt
                              FROM Payment_Slips ps
                             WHERE ps.Slip_TransRef = @ref AND ISNULL(ps.Slip_Sending_Bank,'') = ISNULL(@bank,'')
                               AND ISNULL(ps.Verify_Status,'') NOT IN ('REJECTED','ALLOCATED')
                               AND ISNULL(ps.VerificationStatus,'') <> 'REJECTED' AND ISNULL(ps.IsActive,1) = 1
                               AND (@ex IS NULL OR ps.ID <> @ex)
                             ORDER BY ps.ID", con))
                        {
                            cmd.Parameters.AddWithValue("@ref", transRef);
                            cmd.Parameters.AddWithValue("@bank", (object)bank ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ex", ctx.ExcludeSlipId.HasValue ? (object)ctx.ExcludeSlipId.Value : DBNull.Value);
                            using (var rd = cmd.ExecuteReader())
                            {
                                if (rd.Read())
                                {
                                    var p = new PriorUse
                                    {
                                        SlipId = Convert.ToInt64(rd["ID"]),
                                        SlipAmount = rd["Slip_Amount"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(rd["Slip_Amount"]),
                                        Describe = "การจอง #" + Convert.ToString(rd["Reservation_ID"])
                                            + (rd["Rcpt"] == DBNull.Value ? "" : " ใบเสร็จ " + Convert.ToString(rd["Rcpt"]))
                                            + " (สลิป #" + Convert.ToString(rd["ID"]) + ")"
                                    };
                                    rd.Close();
                                    if (AllocReady)
                                    {
                                        using (var c2 = new SqlCommand(
                                            "SELECT COUNT(*), ISNULL(SUM(Amount),0) FROM Payment_Slip_Allocation WHERE Slip_ID = @id", con))
                                        {
                                            c2.Parameters.AddWithValue("@id", p.SlipId.Value);
                                            using (var r2 = c2.ExecuteReader())
                                            {
                                                if (r2.Read()) { p.HasAllocation = r2.GetInt32(0) > 0; p.Allocated = r2.GetDecimal(1); }
                                            }
                                        }
                                    }
                                    return p;
                                }
                            }
                        }
                    }
                    if (LogReady)
                    {
                        // สลิปที่ใช้กับกิจกรรม/รูมเซอร์วิส (ไม่มีแถว Payment_Slips) — บันทึกใน Slip_Verify_Log ที่ Consumed = 1
                        using (var cmd = new SqlCommand(@"
                            SELECT TOP 1 Source_Type, Source_ID FROM Slip_Verify_Log
                             WHERE Consumed = 1 AND Trans_Ref = @ref AND ISNULL(Sending_Bank,'') = ISNULL(@bank,'')
                               AND NOT (Source_Type = @st AND ISNULL(Source_ID,-1) = ISNULL(@sid,-1))
                             ORDER BY ID", con))
                        {
                            cmd.Parameters.AddWithValue("@ref", transRef);
                            cmd.Parameters.AddWithValue("@bank", (object)bank ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@st", ctx.SourceType ?? "");
                            cmd.Parameters.AddWithValue("@sid", ctx.SourceId.HasValue ? (object)ctx.SourceId.Value : DBNull.Value);
                            using (var rd = cmd.ExecuteReader())
                            {
                                if (rd.Read())
                                    return new PriorUse { Describe = SourceThai(Convert.ToString(rd["Source_Type"])) + " #" + Convert.ToString(rd["Source_ID"]) };
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { Log("FindPriorUse: " + ex.Message); }
            return null;
        }

        private static string SourceThai(string st)
        {
            switch (st)
            {
                case "ACTIVITY": return "การจองกิจกรรม";
                case "ROOM_SERVICE": return "ออเดอร์รูมเซอร์วิส";
                case "RESERVATION": return "การจอง";
                default: return st;
            }
        }

        // ── ตรวจบัญชีผู้รับ ─────────────────────────────────────────────────────

        public class ReceiverAccount
        {
            public int PaidHowId;
            public string PaidHowName;
            public string BankCode;
            public string AccountNo;     // ตัวเลขล้วน
            public string PromptPayId;   // ตัวเลขล้วน
        }

        /// <summary>บัญชีรับเงินของร้าน: Account_Paid_How (Bank_Code/Bank_Account_No/PromptPay_Id — PHASE19_34)
        /// + เลขบัญชีจาก NextAcc (Nexaacc_BankAccountId → Accounting_Nexaacc_BankAccounts.Account_Number)</summary>
        public static List<ReceiverAccount> LoadReceiverAccounts()
        {
            var list = new List<ReceiverAccount>();
            try
            {
                bool bankCols = PaidHowBankCols, nexa = PaidHowNexaBank;
                if (!bankCols && !nexa) return list;
                string sql = "SELECT ph.ID, ph.Paid_How"
                    + (bankCols ? ", ph.Bank_Code, ph.Bank_Account_No, ph.PromptPay_Id" : ", NULL AS Bank_Code, NULL AS Bank_Account_No, NULL AS PromptPay_Id")
                    + (nexa ? ", nb.Account_Number AS Nexa_No FROM Account_Paid_How ph LEFT JOIN Accounting_Nexaacc_BankAccounts nb ON nb.Nexaacc_BankAccountId = ph.Nexaacc_BankAccountId"
                            : ", NULL AS Nexa_No FROM Account_Paid_How ph")
                    + " WHERE ISNULL(CAST(ph.Status AS NVARCHAR(10)),'True') IN ('True','1')";
                using (var con = new SqlConnection(ConnStr))
                using (var cmd = new SqlCommand(sql, con))
                {
                    con.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            string no = Digits(rd["Bank_Account_No"] as string);
                            if (string.IsNullOrEmpty(no)) no = Digits(rd["Nexa_No"] as string);
                            string pp = Digits(rd["PromptPay_Id"] as string);
                            if (string.IsNullOrEmpty(no) && string.IsNullOrEmpty(pp)) continue;
                            list.Add(new ReceiverAccount
                            {
                                PaidHowId = Convert.ToInt32(rd["ID"]),
                                PaidHowName = Convert.ToString(rd["Paid_How"]),
                                BankCode = (rd["Bank_Code"] as string ?? "").Trim(),
                                AccountNo = no,
                                PromptPayId = pp
                            });
                        }
                    }
                }
            }
            catch (Exception ex) { Log("LoadReceiverAccounts: " + ex.Message); }
            return list;
        }

        /// <summary>คืน Paid_How ที่ตรง / null + why=null (เทียบไม่ได้ — ข้าม) / null + why (ไม่ตรง)</summary>
        private static int? MatchReceiver(SlipVendorResponse v, out string why)
        {
            why = null;
            var accounts = LoadReceiverAccounts();
            if (accounts.Count == 0)
            {
                if (v.Kind == SlipVendorResponse.KindReceiverMismatch)
                    why = "SlipOK แจ้งว่าบัญชีผู้รับไม่ตรงกับบัญชีหลักของร้าน (1014)";
                return null;   // ยังไม่ได้ตั้งเลขบัญชี → ตัดสินไม่ได้
            }
            string acct = v.ReceiverAcctMasked, proxy = v.ReceiverProxyMasked;
            if (string.IsNullOrEmpty(acct) && string.IsNullOrEmpty(proxy))
            {
                if (v.Kind == SlipVendorResponse.KindReceiverMismatch) why = "SlipOK แจ้งบัญชีผู้รับไม่ตรง (1014)";
                return null;
            }
            foreach (var a in accounts)
            {
                bool bankOk = string.IsNullOrEmpty(a.BankCode) || string.IsNullOrEmpty(v.ReceivingBank) || a.BankCode == v.ReceivingBank;
                if (bankOk && !string.IsNullOrEmpty(acct) && MaskedMatch(acct, a.AccountNo)) return a.PaidHowId;
                if (!string.IsNullOrEmpty(proxy) && MaskedMatch(proxy, a.PromptPayId)) return a.PaidHowId;
                if (!string.IsNullOrEmpty(proxy) && MaskedMatch(proxy, a.AccountNo) && bankOk) return a.PaidHowId;
            }
            why = "ไม่ตรงกับบัญชีรับเงินที่ตั้งไว้ใน แหล่งเงิน (Account_Paid_How) " + accounts.Count + " บัญชี";
            return null;
        }

        /// <summary>
        /// เทียบเลขที่ปิดบัง (xxx-x-x3426-x / x-6213 / xxx-xxx-4421) กับเลขเต็ม: ความยาวเท่ากัน = เทียบรายตำแหน่ง (x = อะไรก็ได้)
        /// ไม่เท่ากัน = ตัวเลขช่วงที่เห็นยาวสุด (≥3 หลัก) ต้องเป็นส่วนหนึ่งของเลขเต็ม
        /// </summary>
        public static bool MaskedMatch(string masked, string full)
        {
            full = Digits(full);
            if (string.IsNullOrEmpty(full) || string.IsNullOrEmpty(masked)) return false;
            string m = new string(masked.ToLowerInvariant().Where(c => char.IsDigit(c) || c == 'x' || c == '*' || c == '•').ToArray())
                .Replace('*', 'x').Replace('•', 'x');
            if (m.Length == full.Length)
            {
                for (int i = 0; i < m.Length; i++)
                    if (m[i] != 'x' && m[i] != full[i]) return false;
                return m.Any(char.IsDigit);
            }
            string longest = Regex.Matches(m, @"\d+").Cast<Match>().Select(x => x.Value).OrderByDescending(x => x.Length).FirstOrDefault();
            return !string.IsNullOrEmpty(longest) && longest.Length >= 3 && full.Contains(longest);
        }

        private static string Digits(string s) => string.IsNullOrEmpty(s) ? "" : new string(s.Where(char.IsDigit).ToArray());

        /// <summary>แหล่งเงินนี้เป็นการโอน/QR (ต้องตรวจสลิป) — อ่าน Channel_Type ก่อน ไม่มีคอลัมน์/ค่า = เดาจากชื่อ</summary>
        public static bool IsTransferPaidHow(string paidHowIdOrName, string paidHowText)
        {
            string name = paidHowText ?? "";
            try
            {
                if (PaidHowChannelType)
                {
                    using (var con = new SqlConnection(ConnStr))
                    using (var cmd = new SqlCommand(
                        "SELECT TOP 1 Channel_Type, Paid_How FROM Account_Paid_How WHERE CAST(ID AS NVARCHAR(20)) = @v OR Paid_How = @t", con))
                    {
                        cmd.Parameters.AddWithValue("@v", paidHowIdOrName ?? "");
                        cmd.Parameters.AddWithValue("@t", paidHowText ?? "");
                        con.Open();
                        using (var rd = cmd.ExecuteReader())
                        {
                            if (rd.Read())
                            {
                                string ct = (rd["Channel_Type"] as string ?? "").Trim().ToUpperInvariant();
                                if (ct == "TRANSFER" || ct == "QR") return true;
                                if (ct.Length > 0 && ct != "OTHER") return false;   // CASH/CARD/GATEWAY/DIRECTOR_LOAN/OTA
                                name = Convert.ToString(rd["Paid_How"]);
                            }
                        }
                    }
                }
            }
            catch { }
            string n = (name ?? "").ToLowerInvariant();
            if (n.Contains("เงินสด") || n.Contains("cash")) return false;
            return n.Contains("โอน") || n.Contains("ธนาคาร") || n.Contains("พร้อมเพย์") || n.Contains("promptpay")
                || n.Contains("qr") || n.Contains("transfer") || n.Contains("bank") || n.StartsWith("ธ.");
        }

        // ── บันทึกผลลงแถวสลิป ──────────────────────────────────────────────────

        /// <summary>
        /// เขียนผลตรวจลง Payment_Slips (+ ผูกใบเสร็จ, บันทึกการแบ่งสลิป, override) — ไม่มีคอลัมน์ = ไม่ทำอะไร
        /// สถานะที่บล็อกและยังไม่ override → พักคิวบัญชีของใบเสร็จ (AWAITING_SLIP) + แจ้ง Telegram (ซ้ำ/ผู้รับผิด)
        /// </summary>
        public static void SaveResult(long slipId, SlipVerifyResult r, SlipGateDecision d, string receiptId, int? reservationId,
            decimal? amountUsed, string user)
        {
            if (r == null || slipId <= 0 || !SchemaReady) return;
            try
            {
                string status = r.IsAllocationContinuation ? SlipVerifyStatus.Allocated : r.Status;
                bool overridden = d != null && d.OverrideUsed;
                if (d != null && d.AllocationUsed && status == SlipVerifyStatus.Mismatch)
                {
                    // แบ่งสลิปครั้งแรก: ยอดสลิปมากกว่ายอดรายการโดยตั้งใจ (ส่วนที่เหลือใช้กับการจองอื่น) = ผ่าน ไม่พักคิวบัญชี
                    status = SlipVerifyStatus.Verified;
                    r.Message = "แบ่งสลิป: ใช้ " + (amountUsed ?? 0m).ToString("N2") + " จากยอดสลิป " + (r.Amount ?? 0m).ToString("N2")
                        + " บาท (ที่เหลือใช้กับการจองอื่นได้) · " + r.Message;
                }
                using (var con = new SqlConnection(ConnStr))
                {
                    con.Open();
                    string sql = @"UPDATE Payment_Slips SET
                            Slip_Amount = @amt, Slip_TransRef = @ref, Slip_Sending_Bank = @bank, Slip_Trans_Date = @dt,
                            Slip_Sender_Name = @sender, Slip_Receiver_Acct = @recv, Verify_Status = @st, Verify_Source = @src,
                            Verify_Message = @msg, Verify_At = GETDATE(), Matched_Paid_How_ID = @ph"
                        + (overridden ? ", Override_By = @ob, Override_Reason = @or, Override_At = GETDATE()" : "")
                        + (!string.IsNullOrEmpty(receiptId) ? ", Account_Receipt_ID = ISNULL(Account_Receipt_ID, @rc)" : "")
                        + " WHERE ID = @id";
                    Func<SqlCommand> build = () =>
                    {
                        var cmd = new SqlCommand(sql, con);
                        cmd.Parameters.AddWithValue("@id", slipId);
                        cmd.Parameters.AddWithValue("@amt", (object)r.Amount ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ref", string.IsNullOrEmpty(r.TransRef) ? (object)DBNull.Value : r.TransRef);
                        cmd.Parameters.AddWithValue("@bank", string.IsNullOrEmpty(r.SendingBank) ? (object)DBNull.Value : r.SendingBank);
                        cmd.Parameters.AddWithValue("@dt", (object)r.TransDate ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@sender", Cut(r.SenderName, 150));
                        cmd.Parameters.AddWithValue("@recv", Cut(((r.ReceiverName ?? "") + " " + (r.ReceiverAcctMasked ?? "")).Trim(), 150));
                        cmd.Parameters.AddWithValue("@st", status);
                        cmd.Parameters.AddWithValue("@src", r.Source ?? SlipVerifySource.None);
                        cmd.Parameters.AddWithValue("@msg", Cut(r.Message, 1000));
                        cmd.Parameters.AddWithValue("@ph", (object)r.MatchedPaidHowId ?? DBNull.Value);
                        if (overridden)
                        {
                            cmd.Parameters.AddWithValue("@ob", Cut(user, 100));
                            cmd.Parameters.AddWithValue("@or", Cut(d.OverrideReason, 500));
                        }
                        if (!string.IsNullOrEmpty(receiptId)) cmd.Parameters.AddWithValue("@rc", receiptId);
                        return cmd;
                    };
                    try
                    {
                        using (var cmd = build()) cmd.ExecuteNonQuery();
                    }
                    catch (SqlException sx) when (sx.Number == 2601 || sx.Number == 2627)
                    {
                        // ชน unique index (ธนาคาร + transRef) = มีคนใช้สลิปนี้ไปพร้อมกัน → บันทึกเป็นซ้ำ (ไม่เก็บ transRef ในแถวนี้)
                        status = SlipVerifyStatus.Duplicate;
                        r.Status = SlipVerifyStatus.Duplicate;
                        r.Message = "สลิปนี้ (อ้างอิง " + r.TransRef + ") ถูกใช้กับรายการอื่นพร้อมกัน — " + r.Message;
                        string keepRef = r.TransRef;
                        r.TransRef = null;
                        using (var cmd = build()) cmd.ExecuteNonQuery();
                        r.TransRef = keepRef;
                        overridden = false;
                    }

                    // OCR ในเครื่อง → เติมคอลัมน์ OCR เดิม (PHASE2) ให้หน้า SlipVerification แสดง
                    if (r.Source == SlipVerifySource.LocalOcr && OcrColsReady)
                    {
                        using (var cmd = new SqlCommand(@"UPDATE Payment_Slips SET OCR_Amount = @a, OCR_Status = @s, OCR_Confidence = @c,
                                    OCR_RawText = @t, OCR_ProcessedDate = GETDATE() WHERE ID = @id", con))
                        {
                            cmd.Parameters.AddWithValue("@id", slipId);
                            cmd.Parameters.AddWithValue("@a", (object)r.OcrAmount ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@s", r.Status == SlipVerifyStatus.Verified ? "SUCCESS" : "MANUAL_REVIEW");
                            cmd.Parameters.AddWithValue("@c", (decimal)Math.Max(0, Math.Min(100, r.OcrConfidence)));
                            cmd.Parameters.AddWithValue("@t", Cut(r.OcrRaw, 4000));
                            try { cmd.ExecuteNonQuery(); } catch { }
                        }
                    }

                    // แบ่งสลิป: แถวแรก (ยอดที่ใช้กับรายการนี้) หรือแถวต่อ (ยอดคงเหลือของสลิปเดิม)
                    if (AllocReady && d != null && d.AllocationUsed && amountUsed.HasValue && amountUsed.Value > 0m)
                    {
                        long primary = r.IsAllocationContinuation && r.DuplicateOfSlipId.HasValue ? r.DuplicateOfSlipId.Value : slipId;
                        using (var cmd = new SqlCommand(@"INSERT INTO Payment_Slip_Allocation (Slip_ID, Reservation_ID, Account_Receipt_ID, Amount, Created_By, Created_At, Child_Slip_ID)
                                                          VALUES (@s, @res, @rc, @a, @by, GETDATE(), @child)", con))
                        {
                            cmd.Parameters.AddWithValue("@s", primary);
                            cmd.Parameters.AddWithValue("@res", (object)reservationId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@rc", string.IsNullOrEmpty(receiptId) ? (object)DBNull.Value : receiptId);
                            cmd.Parameters.AddWithValue("@a", amountUsed.Value);
                            cmd.Parameters.AddWithValue("@by", Cut(user, 100));
                            cmd.Parameters.AddWithValue("@child", primary == slipId ? (object)DBNull.Value : slipId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                if (SlipVerifyStatus.IsBlocking(status) && !overridden && !string.IsNullOrEmpty(receiptId))
                    HoldQueueForReceipt(receiptId);
                if (overridden && !string.IsNullOrEmpty(receiptId))
                    ReleaseQueueForReceipt(receiptId);
                NotifyIfSerious(r, reservationId, receiptId, overridden ? d.OverrideReason : null, user);
                Log("Slip #" + slipId + " " + status + (overridden ? " (override: " + d.OverrideReason + ")" : "") + " · " + r.ShortText());
            }
            catch (Exception ex) { Log("SaveResult slip #" + slipId + ": " + ex.Message); }
        }

        /// <summary>override ภายหลัง (หน้า SlipVerification) — บันทึกผู้อนุมัติ/เหตุผล แล้วปล่อยคิวบัญชีที่พักไว้</summary>
        public static bool RecordOverride(long slipId, string user, string reason)
        {
            if (!SchemaReady || string.IsNullOrWhiteSpace(reason)) return false;
            try
            {
                string receipt = null, status = null;
                using (var con = new SqlConnection(ConnStr))
                {
                    con.Open();
                    using (var cmd = new SqlCommand("SELECT CAST(Account_Receipt_ID AS NVARCHAR(50)), Verify_Status FROM Payment_Slips WHERE ID = @id", con))
                    {
                        cmd.Parameters.AddWithValue("@id", slipId);
                        using (var rd = cmd.ExecuteReader())
                        {
                            if (!rd.Read()) return false;
                            receipt = rd.IsDBNull(0) ? null : rd.GetString(0);
                            status = rd.IsDBNull(1) ? null : rd.GetString(1);
                        }
                    }
                    if (status == SlipVerifyStatus.Duplicate) return false;   // สลิปซ้ำ override ไม่ได้
                    using (var cmd = new SqlCommand(@"UPDATE Payment_Slips SET Override_By = @by, Override_Reason = @r, Override_At = GETDATE()
                                                       WHERE ID = @id", con))
                    {
                        cmd.Parameters.AddWithValue("@id", slipId);
                        cmd.Parameters.AddWithValue("@by", Cut(user, 100));
                        cmd.Parameters.AddWithValue("@r", Cut(reason.Trim(), 500));
                        cmd.ExecuteNonQuery();
                    }
                }
                if (!string.IsNullOrEmpty(receipt)) ReleaseQueueForReceipt(receipt);
                Log("Slip #" + slipId + " override โดย " + user + ": " + reason);
                return true;
            }
            catch (Exception ex) { Log("RecordOverride: " + ex.Message); return false; }
        }

        /// <summary>ผูกใบเสร็จที่เพิ่งออกให้แถวสลิป (อนุมัติจากหน้า SlipVerification) แล้วปล่อยคิวบัญชีที่อาจพักไว้</summary>
        public static void LinkReceiptAndRelease(long slipId, string receiptId)
        {
            if (slipId <= 0 || string.IsNullOrEmpty(receiptId)) return;
            try
            {
                using (var con = new SqlConnection(ConnStr))
                using (var cmd = new SqlCommand("UPDATE Payment_Slips SET Account_Receipt_ID = @rc WHERE ID = @id AND Account_Receipt_ID IS NULL", con))
                {
                    cmd.Parameters.AddWithValue("@id", slipId);
                    cmd.Parameters.AddWithValue("@rc", receiptId);
                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch { }
            ReleaseQueueForReceipt(receiptId);
        }

        /// <summary>ปฏิเสธสลิป → ปล่อย transRef ให้ใช้ใหม่ได้ (แถวที่เป็น "ซ้ำ" ไม่ได้ถือ transRef อยู่แล้ว)</summary>
        public static void MarkRejected(long slipId)
        {
            if (!SchemaReady) return;
            try
            {
                using (var con = new SqlConnection(ConnStr))
                using (var cmd = new SqlCommand(@"UPDATE Payment_Slips SET Verify_Status = 'REJECTED', Verify_At = GETDATE()
                                                   WHERE ID = @id AND Verify_Status IS NOT NULL AND Verify_Status <> 'DUPLICATE'", con))
                {
                    cmd.Parameters.AddWithValue("@id", slipId);
                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch { }
        }

        /// <summary>อนุมัติสลิป (แบบเดิม — สลิปที่ลงเงินไปแล้ว) → ปล่อยคิวบัญชีของใบเสร็จที่ผูกอยู่</summary>
        public static void ReleaseQueueForSlip(long slipId)
        {
            try
            {
                string receipt = null;
                using (var con = new SqlConnection(ConnStr))
                using (var cmd = new SqlCommand("SELECT CAST(Account_Receipt_ID AS NVARCHAR(50)) FROM Payment_Slips WHERE ID = @id", con))
                {
                    cmd.Parameters.AddWithValue("@id", slipId);
                    con.Open();
                    object o = cmd.ExecuteScalar();
                    receipt = o == null || o == DBNull.Value ? null : o.ToString();
                }
                if (!string.IsNullOrEmpty(receipt)) ReleaseQueueForReceipt(receipt);
            }
            catch { }
        }

        /// <summary>อ่านแถวสลิปแล้วตรวจ (หน้า SlipVerification: แสดงผล / ตรวจใหม่ / ตอนอนุมัติ)</summary>
        public static SlipVerifyResult VerifySlipRow(long slipId, decimal? expected, bool force, bool save)
        {
            string url = null; int? resId = null; string receipt = null;
            try
            {
                using (var con = new SqlConnection(ConnStr))
                using (var cmd = new SqlCommand("SELECT SlipFileURL, Reservation_ID, CAST(Account_Receipt_ID AS NVARCHAR(50)) FROM Payment_Slips WHERE ID = @id", con))
                {
                    cmd.Parameters.AddWithValue("@id", slipId);
                    con.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.Read()) return null;
                        url = rd.IsDBNull(0) ? null : rd.GetString(0);
                        resId = rd.IsDBNull(1) ? (int?)null : Convert.ToInt32(rd.GetValue(1));
                        receipt = rd.IsDBNull(2) ? null : rd.GetString(2);
                    }
                }
            }
            catch (Exception ex) { Log("VerifySlipRow read: " + ex.Message); return null; }
            var r = VerifyFile(url, expected, new SlipVerifyContext
            {
                SourceType = "RESERVATION", SourceId = resId, ExcludeSlipId = slipId, ForceRefresh = force, User = CurrentUserName()
            });
            if (save) SaveResult(slipId, r, null, null, resId, null, CurrentUserName());
            return r;
        }

        /// <summary>ผลตรวจที่บันทึกไว้ของแถวสลิป (null = ยังไม่ตรวจ/ไม่มีคอลัมน์)</summary>
        public static DataRow LoadSavedResult(long slipId)
        {
            if (!SchemaReady) return null;
            try
            {
                var dt = new DataTable();
                using (var con = new SqlConnection(ConnStr))
                using (var da = new SqlDataAdapter(@"SELECT Slip_Amount, Slip_TransRef, Slip_Sending_Bank, Slip_Trans_Date, Verify_Status, Verify_Source,
                        Verify_Message, Verify_At, Override_By, Override_Reason FROM Payment_Slips WHERE ID = @id", con))
                {
                    da.SelectCommand.Parameters.AddWithValue("@id", slipId);
                    da.Fill(dt);
                }
                return dt.Rows.Count > 0 ? dt.Rows[0] : null;
            }
            catch { return null; }
        }

        // ── คิวบัญชี: พัก/ปล่อย ────────────────────────────────────────────────

        /// <summary>
        /// เรียกจาก AccountingSyncService.EnqueueReceipt หลัง InsertQueue — ใบเสร็จที่ผูกสลิปสถานะบล็อก (ไม่ตรง/ซ้ำ/ผู้รับผิด)
        /// และยังไม่ override → คิวเป็น AWAITING_SLIP (worker อ่านเฉพาะ PENDING/FAILED จึงข้ามเอง) ไม่ throw
        /// </summary>
        public static void HoldQueueIfSlipBlocked(string connStr, long queueId, string receiptNumber)
        {
            if (queueId <= 0 || string.IsNullOrEmpty(receiptNumber)) return;
            try
            {
                if (!IsEnabled || !SchemaReady) return;
                using (var con = new SqlConnection(connStr ?? ConnStr))
                using (var cmd = new SqlCommand(@"
                    UPDATE Accounting_Sync_Queue SET Status = @hold
                     WHERE ID = @q AND Status = 'PENDING'
                       AND EXISTS (SELECT 1 FROM Payment_Slips ps
                                    WHERE CAST(ps.Account_Receipt_ID AS NVARCHAR(50)) = @rc
                                      AND ps.Verify_Status IN ('MISMATCH','DUPLICATE','WRONG_RECEIVER')
                                      AND ps.Override_By IS NULL AND ISNULL(ps.VerificationStatus,'') <> 'APPROVED')", con))
                {
                    cmd.Parameters.AddWithValue("@hold", QueueHoldStatus);
                    cmd.Parameters.AddWithValue("@q", queueId);
                    cmd.Parameters.AddWithValue("@rc", receiptNumber);
                    con.Open();
                    if (cmd.ExecuteNonQuery() > 0) Log("คิว #" + queueId + " ใบเสร็จ " + receiptNumber + " → AWAITING_SLIP (สลิปยังไม่ผ่านการตรวจ)");
                }
            }
            catch { }
        }

        private static void HoldQueueForReceipt(string receiptNumber)
        {
            try
            {
                using (var con = new SqlConnection(ConnStr))
                using (var cmd = new SqlCommand(@"UPDATE Accounting_Sync_Queue SET Status = @hold
                     WHERE Status = 'PENDING' AND Action_Type = 'CREATE_RECEIPT_DOCUMENT' AND Payload LIKE @p", con))
                {
                    cmd.Parameters.AddWithValue("@hold", QueueHoldStatus);
                    cmd.Parameters.AddWithValue("@p", "%\"receiptNumber\":\"" + receiptNumber + "\"%");
                    con.Open();
                    int n = cmd.ExecuteNonQuery();
                    if (n > 0) Log("พักคิวบัญชีใบเสร็จ " + receiptNumber + " " + n + " รายการ (AWAITING_SLIP)");
                }
            }
            catch { }
        }

        /// <summary>ปล่อยคิวที่พักไว้ (override/อนุมัติ) — ปล่อยตัวล่าสุด ตัวเก่ากว่าเป็น SUPERSEDED (กันส่งซ้ำ)</summary>
        public static void ReleaseQueueForReceipt(string receiptNumber)
        {
            if (string.IsNullOrEmpty(receiptNumber)) return;
            try
            {
                using (var con = new SqlConnection(ConnStr))
                using (var cmd = new SqlCommand(@"
                    DECLARE @last BIGINT = (SELECT MAX(ID) FROM Accounting_Sync_Queue WHERE Status = @hold AND Payload LIKE @p);
                    IF @last IS NOT NULL
                    BEGIN
                        UPDATE Accounting_Sync_Queue SET Status = 'SUPERSEDED' WHERE Status = @hold AND Payload LIKE @p AND ID <> @last;
                        UPDATE Accounting_Sync_Queue SET Status = 'PENDING' WHERE ID = @last AND Status = @hold;
                    END", con))
                {
                    cmd.Parameters.AddWithValue("@hold", QueueHoldStatus);
                    cmd.Parameters.AddWithValue("@p", "%\"receiptNumber\":\"" + receiptNumber + "\"%");
                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch { }
        }

        // ── โควต้ารายเดือน ─────────────────────────────────────────────────────

        public static int GetUsage(string provider, string yearMonth, out bool exhaustedFlag)
        {
            exhaustedFlag = false;
            if (!UsageReady) return 0;
            try
            {
                using (var con = new SqlConnection(ConnStr))
                using (var cmd = new SqlCommand("SELECT Used_Count, Exhausted FROM Slip_Verify_Usage WHERE Year_Month = @ym AND Provider = @p", con))
                {
                    cmd.Parameters.AddWithValue("@ym", yearMonth);
                    cmd.Parameters.AddWithValue("@p", provider);
                    con.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (rd.Read())
                        {
                            exhaustedFlag = !rd.IsDBNull(1) && Convert.ToBoolean(rd.GetValue(1));
                            return rd.IsDBNull(0) ? 0 : Convert.ToInt32(rd.GetValue(0));
                        }
                    }
                }
            }
            catch { }
            return 0;
        }

        public static bool IsQuotaExhausted(string provider)
        {
            bool flag;
            int used = GetUsage(provider, YearMonth(DateTime.Now), out flag);
            int quota = MonthlyQuota;
            return flag || (quota > 0 && used >= quota) || quota == 0;
        }

        private static void Upsert(string provider, bool increment, bool exhausted)
        {
            if (!UsageReady) return;
            try
            {
                using (var con = new SqlConnection(ConnStr))
                using (var cmd = new SqlCommand(@"
                    UPDATE Slip_Verify_Usage SET Used_Count = Used_Count + @inc, Exhausted = CASE WHEN @ex = 1 THEN 1 ELSE Exhausted END,
                           Updated_At = GETDATE()
                     WHERE Year_Month = @ym AND Provider = @p;
                    IF @@ROWCOUNT = 0
                        INSERT INTO Slip_Verify_Usage (Year_Month, Provider, Used_Count, Exhausted, Updated_At)
                        VALUES (@ym, @p, @inc, @ex, GETDATE());", con))
                {
                    cmd.Parameters.AddWithValue("@ym", YearMonth(DateTime.Now));
                    cmd.Parameters.AddWithValue("@p", provider);
                    cmd.Parameters.AddWithValue("@inc", increment ? 1 : 0);
                    cmd.Parameters.AddWithValue("@ex", exhausted ? 1 : 0);
                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch { }
        }

        public static void IncrementUsage(string provider) => Upsert(provider, true, false);
        public static void MarkQuotaExhausted(string provider) => Upsert(provider, false, true);

        /// <summary>ล้างธงโควต้าหมดของเดือนนี้ (หน้าตั้งค่า — หลังต่อแพ็กเกจ)</summary>
        public static void ResetExhausted(string provider)
        {
            if (!UsageReady) return;
            try
            {
                using (var con = new SqlConnection(ConnStr))
                using (var cmd = new SqlCommand("UPDATE Slip_Verify_Usage SET Exhausted = 0 WHERE Year_Month = @ym AND Provider = @p", con))
                {
                    cmd.Parameters.AddWithValue("@ym", YearMonth(DateTime.Now));
                    cmd.Parameters.AddWithValue("@p", provider);
                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch { }
        }

        // ── log / แจ้งเตือน ────────────────────────────────────────────────────

        /// <summary>บันทึกการตรวจ (Slip_Verify_Log) — consumed = สลิปถูกใช้รับเงินแล้ว (กันซ้ำของกิจกรรม/รูมเซอร์วิส)</summary>
        public static void TryLog(SlipVerifyContext ctx, SlipVerifyResult r, bool consumed)
        {
            if (r == null || !LogReady) return;
            try
            {
                using (var con = new SqlConnection(ConnStr))
                using (var cmd = new SqlCommand(@"INSERT INTO Slip_Verify_Log (Created_At, Source_Type, Source_ID, File_Hash, Sending_Bank, Trans_Ref,
                            Amount, Expected_Amount, Verify_Status, Verify_Source, Message, Consumed, Created_By)
                        VALUES (GETDATE(), @st, @sid, @h, @b, @r, @a, @e, @s, @src, @m, @c, @by)", con))
                {
                    cmd.Parameters.AddWithValue("@st", Cut(ctx?.SourceType ?? "", 30));
                    cmd.Parameters.AddWithValue("@sid", ctx != null && ctx.SourceId.HasValue ? (object)ctx.SourceId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@h", Cut(r.FileHash, 64));
                    cmd.Parameters.AddWithValue("@b", Cut(r.SendingBank, 10));
                    cmd.Parameters.AddWithValue("@r", Cut(r.TransRef, 40));
                    cmd.Parameters.AddWithValue("@a", (object)r.Amount ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@e", (object)r.ExpectedAmount ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@s", r.Status ?? "");
                    cmd.Parameters.AddWithValue("@src", r.Source ?? "");
                    cmd.Parameters.AddWithValue("@m", Cut(r.Message, 1000));
                    cmd.Parameters.AddWithValue("@c", consumed);
                    cmd.Parameters.AddWithValue("@by", Cut(ctx?.User ?? CurrentUserName(), 100));
                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch { }
        }

        /// <summary>Telegram เมื่อสลิปซ้ำ/โอนเข้าบัญชีอื่น (หรือมีการ override)</summary>
        public static void NotifyIfSerious(SlipVerifyResult r, int? reservationId, string receiptId, string overrideReason, string user)
        {
            try
            {
                if (r == null || r.IsAllocationContinuation) return;
                bool serious = r.Status == SlipVerifyStatus.Duplicate || r.Status == SlipVerifyStatus.WrongReceiver;
                if (!serious && string.IsNullOrEmpty(overrideReason)) return;
                string head = r.Status == SlipVerifyStatus.Duplicate ? "⚠️ <b>สลิปซ้ำ</b>"
                    : r.Status == SlipVerifyStatus.WrongReceiver ? "⚠️ <b>สลิปโอนเข้าบัญชีอื่น</b>"
                    : "📝 <b>ยืนยันสลิปที่ตรวจไม่ผ่าน (override)</b>";
                string msg = head
                    + (reservationId.HasValue ? "\nการจอง #" + reservationId.Value : "")
                    + (string.IsNullOrEmpty(receiptId) ? "" : "\nใบเสร็จ " + HttpUtility.HtmlEncode(receiptId))
                    + "\n" + HttpUtility.HtmlEncode(r.ShortText())
                    + (string.IsNullOrEmpty(overrideReason) ? "" : "\nเหตุผล: " + HttpUtility.HtmlEncode(overrideReason) + " (โดย " + HttpUtility.HtmlEncode(user) + ")");
                Notify.Send(Notify.Ev.PaymentOnline, msg);
            }
            catch { }
        }

        private static void Log(string detail)
        {
            try { new code().Logs(ConnStr, "SlipVerify", detail, CurrentUserName()); } catch { }
        }

        // ── ไฟล์ ────────────────────────────────────────────────────────────────

        /// <summary>URL ในเว็บ (~/Documents/…, Upload/Slip/…, ./Upload/…) หรือ path จริง → path จริง</summary>
        public static string MapSlipPath(string pathOrUrl)
        {
            if (string.IsNullOrWhiteSpace(pathOrUrl)) return null;
            string p = pathOrUrl.Trim();
            if (p.Length > 2 && (p[1] == ':' || p.StartsWith("\\\\", StringComparison.Ordinal))) return p;
            int q = p.IndexOfAny(new[] { '?', '#' });
            if (q >= 0) p = p.Substring(0, q);
            if (p.StartsWith("./", StringComparison.Ordinal)) p = p.Substring(2);
            if (p.StartsWith("../", StringComparison.Ordinal)) p = p.Substring(3);
            if (!p.StartsWith("~/", StringComparison.Ordinal)) p = "~/" + p.TrimStart('/');
            try { return HostingEnvironment.MapPath(p); } catch { return null; }
        }

        private static object Cut(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return DBNull.Value;
            return s.Length > n ? s.Substring(0, n) : s;
        }
    }
}
