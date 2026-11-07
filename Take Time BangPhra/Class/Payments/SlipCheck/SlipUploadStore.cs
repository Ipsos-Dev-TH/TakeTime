using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Caching;
using System.Web.SessionState;

namespace Take_Time_BangPhra.Payments.SlipCheck
{
    /// <summary>
    /// ที่เก็บสลิปชั่วคราวของหน้าจอง (CLAUDE.md ข้อ 18 — ตรวจสลิปแบบ async + สลิปเติมยอด)
    ///   ชื่อไฟล์เดียวกับ Reserve.SlipTempFilename เดิมทุกตัวอักษร: {เบอร์}_{sha1(session|id|command)[0..10]}.jpg
    ///   สลิปเติมยอด (ลูกค้า): {เบอร์}_{แท็ก}_t{n}.jpg (n = 2..MaxSlots)
    ///   แท็กผูก SessionID ⇒ คนอื่นเดา/เขียนทับไฟล์ของ session อื่นไม่ได้
    /// + ตัวช่วยความปลอดภัยของ API/SlipCheck.ashx: nonce ต่อหน้า, จำกัดความถี่, ตรวจชนิดไฟล์จาก magic bytes
    /// </summary>
    public static class SlipUploadStore
    {
        /// <summary>จำนวนสลิปสูงสุดต่อการจอง (สลิปหลัก 1 + เติมยอด 2)</summary>
        public const int MaxSlots = 3;
        /// <summary>ขนาดไฟล์สูงสุดที่รับ (เบราว์เซอร์ย่อรูปก่อนส่งแล้ว — ค่านี้เท่ากับกล่องแนบสลิปเดิม)</summary>
        public const int MaxBytes = 8 * 1024 * 1024;

        public static string Dir => AppDomain.CurrentDomain.BaseDirectory + "\\Upload\\Slip";

        /// <summary>แท็ก session+การจอง (สูตรเดิมของ Reserve.SlipTempFilename)</summary>
        public static string Tag(string sessionId, string queryId, string command)
        {
            string raw = (sessionId ?? "") + "|" + (queryId ?? "new") + "|" + (command ?? "");
            using (var sha = SHA1.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw))).Replace("-", "").Substring(0, 10).ToLowerInvariant();
        }

        public static string PhoneKey(string phone) => Regex.Replace(phone ?? "", @"[^0-9A-Za-z]", "");

        public static string TempFilename(string sessionId, string queryId, string command, string phone, int slot)
        {
            string baseName = PhoneKey(phone) + "_" + Tag(sessionId, queryId, command);
            return slot <= 1 ? baseName + ".jpg" : baseName + "_t" + slot + ".jpg";
        }

        public static string PathOf(string fileName) => Dir + "\\" + fileName;

        /// <summary>path ของสลิปทุกช่องที่มีไฟล์อยู่ (index 0 = ช่อง 1) — ช่องที่ไม่มีไฟล์ = null</summary>
        public static string[] SlotPaths(string sessionId, string queryId, string command, string phone)
        {
            var arr = new string[MaxSlots];
            if (PhoneKey(phone).Length == 0) return arr;
            for (int s = 1; s <= MaxSlots; s++)
            {
                try
                {
                    string p = PathOf(TempFilename(sessionId, queryId, command, phone, s));
                    if (File.Exists(p)) arr[s - 1] = p;
                }
                catch { }
            }
            return arr;
        }

        /// <summary>ลบสลิปเติมยอดทั้งหมด (เปลี่ยนสลิปหลัก = ยอดเติมที่คำนวณไว้ใช้ไม่ได้แล้ว)</summary>
        public static void ClearTopUps(string sessionId, string queryId, string command, string phone)
        {
            for (int s = 2; s <= MaxSlots; s++)
            {
                try
                {
                    string p = PathOf(TempFilename(sessionId, queryId, command, phone, s));
                    if (File.Exists(p)) File.Delete(p);
                }
                catch { }
            }
        }

        /// <summary>ชนิดรูปจาก magic bytes (ไม่เชื่อนามสกุล/Content-Type ของเบราว์เซอร์) — null = ไม่รับ</summary>
        public static string SniffImage(byte[] b)
        {
            if (b == null || b.Length < 12) return null;
            if (b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
            if (b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return "image/png";
            if (b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x38) return "image/gif";
            if (b[0] == 0x42 && b[1] == 0x4D) return "image/bmp";
            return null;
        }

        // ── nonce ต่อหน้า (กัน CSRF + ไม่เชื่อค่าจากเบราว์เซอร์) ──
        //   หน้าจองออก nonce ผูกกับ (id, command, พนักงาน/ลูกค้า) ไว้ใน Session — API รับแค่ nonce แล้วอ่านบริบทจาก Session เอง
        //   ⇒ เว็บอื่นยิงแทนไม่ได้ (ไม่รู้ nonce) และเปลี่ยนการจอง/โหมดผ่านพารามิเตอร์ไม่ได้

        public sealed class PageContext
        {
            /// <summary>Request.QueryString["id"] ของหน้า (null = ไม่มี)</summary>
            public string QueryId;
            public string Command;
            public bool Staff;
        }

        private static string NonceKey(string tag) => "slipck:n:" + tag;
        private static string CtxKey(string nonce) => "slipck:c:" + nonce;

        public static string IssueContext(HttpSessionState session, string queryId, string command, bool staff)
        {
            if (session == null) return "";
            string tag = Tag(session.SessionID, queryId, command) + (staff ? "s" : "c");
            string n = session[NonceKey(tag)] as string;
            if (string.IsNullOrEmpty(n))
            {
                var b = new byte[24];
                using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(b);
                n = BitConverter.ToString(b).Replace("-", "").ToLowerInvariant();
                session[NonceKey(tag)] = n;
            }
            // ค่าธรรมดา (string) — ใช้ได้ทั้ง session แบบ InProc และแบบ serialize
            session[CtxKey(n)] = (queryId == null ? "\u0001" : queryId) + "\n" + (command ?? "") + "\n" + (staff ? "1" : "0");
            return n;
        }

        public static PageContext GetContext(HttpSessionState session, string nonce)
        {
            if (session == null || string.IsNullOrEmpty(nonce) || nonce.Length != 48) return null;
            for (int i = 0; i < nonce.Length; i++)
            {
                char c = nonce[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return null;
            }
            string v = session[CtxKey(nonce)] as string;
            if (string.IsNullOrEmpty(v)) return null;
            string[] p = v.Split('\n');
            if (p.Length != 3) return null;
            return new PageContext { QueryId = p[0] == "\u0001" ? null : p[0], Command = p[1], Staff = p[2] == "1" };
        }

        // ── จำกัดความถี่ (ต่อ session และต่อ IP — กันยิงเปลืองโควต้า SlipOK/CPU OCR) ──

        private sealed class Counter { public int Count; public DateTime Until; }

        public static bool TryRate(string key, int max, TimeSpan window)
        {
            try
            {
                var cache = HttpRuntime.Cache;
                lock (cache)
                {
                    var c = cache[key] as Counter;
                    if (c == null || c.Until <= DateTime.UtcNow)
                    {
                        c = new Counter { Count = 0, Until = DateTime.UtcNow.Add(window) };
                        cache.Insert(key, c, null, c.Until, Cache.NoSlidingExpiration);
                    }
                    if (c.Count >= max) return false;
                    c.Count++;
                    return true;
                }
            }
            catch { return true; }
        }

        // ── สลิปหลายใบ (ลูกค้าเติมยอดเมื่อสลิปแรกต่ำกว่ามัดจำขั้นต่ำ) ──

        public sealed class SetItem
        {
            public int Slot;
            public string Path;
            public SlipVerifyResult Result;
            public SlipAmountLock Lock;
            /// <summary>ยอดที่ใช้ (ยอดล็อกตามสลิป) — 0 = อ่านไม่ได้</summary>
            public decimal Amount;
            /// <summary>เหตุที่ใช้สลิปใบนี้ไม่ได้ (null = ใช้ได้)</summary>
            public string Problem;
            /// <summary>ใช้ได้แต่ไม่ผ่านเงื่อนไขออกเอกสารอัตโนมัติ (ALLOW_MANUAL) → การจองรอพนักงานตรวจ</summary>
            public bool NeedsStaff;
        }

        /// <summary>
        /// ตรวจสลิปทุกใบของการจองลูกค้า (ใช้ทั้ง API/SlipCheck.ashx และด่านบันทึก RunSlipGate — กฎชุดเดียว):
        ///   แต่ละใบตรวจแยก (VerifyWithLock — ซ้ำในฐานข้อมูล/ผู้รับ/อ่านยอด) + ต้อง "ล็อกยอด" ได้ (ยอดน่าเชื่อถือ) ·
        ///   ใบเดียวกันแนบซ้ำในชุด (hash เดียวกัน หรือ ธนาคาร+เลขอ้างอิงเดียวกัน) = ใช้ไม่ได้ ·
        ///   ตรวจไม่ผ่านตามกฎลูกค้า: CONTACT_ADMIN = ใช้ไม่ได้ · ALLOW_MANUAL = ใช้ได้แต่รอพนักงาน
        /// ไม่ throw — ตรวจไม่ได้ = ใบนั้น Problem
        /// </summary>
        public static List<SetItem> EvaluateCustomerSet(string[] slotPaths, long? reservationId, string user)
        {
            var list = new List<SetItem>();
            if (slotPaths == null) return list;
            bool allowManual = SlipVerificationService.CustomerFailMode == CustomerSlipOutcome.ModeAllowManual;
            var seenHash = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenRef = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < slotPaths.Length; i++)
            {
                string p = slotPaths[i];
                if (string.IsNullOrEmpty(p)) continue;
                var it = new SetItem { Slot = i + 1, Path = p };
                list.Add(it);
                try
                {
                    byte[] bytes = File.ReadAllBytes(p);
                    SlipAmountLock lk;
                    decimal? rec;
                    var r = SlipVerificationService.VerifyWithLock(bytes, System.IO.Path.GetFileName(p), null,
                        new SlipVerifyContext { SourceType = "RESERVATION", SourceId = reservationId, CustomerSelfService = true, User = user },
                        false, out lk, out rec);
                    it.Result = r;
                    it.Lock = lk;
                    if (lk != null && lk.Locked && lk.Amount.HasValue) it.Amount = lk.Amount.Value;

                    string refKey = r != null && !string.IsNullOrEmpty(r.TransRef) ? (r.SendingBank ?? "") + "|" + r.TransRef : null;
                    if (r != null && r.Status == SlipVerifyStatus.Duplicate && !r.IsAllocationContinuation)
                        it.Problem = "สลิปนี้ถูกใช้ชำระไปแล้ว";
                    else if (r != null && !string.IsNullOrEmpty(r.FileHash) && !seenHash.Add(r.FileHash))
                        it.Problem = "สลิปใบนี้แนบไปแล้ว";
                    else if (refKey != null && !seenRef.Add(refKey))
                        it.Problem = "สลิปใบนี้แนบไปแล้ว (รายการโอนเดียวกัน)";
                    else if (lk == null || !lk.Locked || it.Amount <= 0m)
                        it.Problem = "อ่านยอดเงินในสลิปไม่ชัดเจน — แนบรูปที่ชัดกว่า หรือติดต่อเจ้าหน้าที่";
                    else
                    {
                        string reason = SlipVerificationService.CustomerFailReason(r, lk, false);
                        if (reason != null)
                        {
                            if (allowManual) it.NeedsStaff = true;
                            else it.Problem = reason;
                        }
                    }
                }
                catch (Exception ex)
                {
                    it.Problem = "ตรวจสลิปไม่ได้ (" + ex.GetType().Name + ")";
                }
            }
            return list;
        }

        public static decimal Sum(IEnumerable<SetItem> set)
        {
            return set == null ? 0m : set.Where(x => x != null && x.Problem == null).Sum(x => x.Amount);
        }
    }
}
