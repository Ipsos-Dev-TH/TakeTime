using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Take_Time_BangPhra.Integration
{
    // ══════════════════════════════════════════════════════════════════════════════════════════
    //  CLAUDE.md ข้อ 22 — "ดึงเอกสารล่าสุด" + "ส่งชื่อผู้ซื้อใหม่ไป NextAcc"
    //  ตรรกะล้วน (ไม่แตะ DB/API) แยกไว้ให้ทดสอบด้วย mono ได้ — AccountingSyncService เป็นผู้เรียก
    // ══════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// กติกา cache PDF เอกสาร NextAcc ฝั่ง TakeTime:
    ///  • ลายนิ้วมือ (fingerprint) ของ "สิ่งที่พิมพ์บนกระดาษ" จาก GET document — เลข/สถานะ/ยอด/VAT/วันที่/ผู้ซื้อ/e-Tax/หักมัดจำ
    ///    (NextAcc DocumentResponse ไม่มี UpdatedAt — DocumentDtos.cs:625-820) เก็บใน .docstate ช่องที่ 3
    ///  • ไฟล์ cache ใช้ได้ก็ต่อเมื่อลายนิ้วมือที่เก็บ = ของเอกสารปัจจุบัน (อ่าน NextAcc ไม่ได้ = กติกาเดิม)
    ///  • เขียนไฟล์ใหม่แบบ atomic: ไฟล์ชั่วคราว → ตรวจหัว %PDF → แทนที่ไฟล์เดิม; แทนที่ไม่ได้ (ไฟล์ถูกล็อก) → เขียนชื่อใหม่ _v{ticks}
    /// </summary>
    public static class NextAccPdfCache
    {
        /// <summary>ลายนิ้วมือสั้น (16 hex) — ค่าเปลี่ยนเมื่อสิ่งที่พิมพ์บนเอกสารเปลี่ยน</summary>
        public static string Fingerprint(string documentNumber, int status, decimal total, decimal vat, DateTime documentDate,
            Guid contactId, string contactName, string contactTaxId, bool buyerDeclined, string etaxStatus, decimal depositApplied)
        {
            var inv = CultureInfo.InvariantCulture;
            string raw = string.Join("\u001f", new[]
            {
                (documentNumber ?? "").Trim(),
                status.ToString(inv),
                Math.Round(total, 2).ToString("0.00", inv),
                Math.Round(vat, 2).ToString("0.00", inv),
                documentDate.ToString("yyyy-MM-dd", inv),
                contactId.ToString("N"),
                NormalizeName(contactName),
                (contactTaxId ?? "").Trim(),
                buyerDeclined ? "1" : "0",
                (etaxStatus ?? "").Trim(),
                Math.Round(depositApplied, 2).ToString("0.00", inv)
            });
            using (var sha = SHA1.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
                var sb = new StringBuilder();
                for (int i = 0; i < 8; i++) sb.Append(h[i].ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>รูปแบบ .docstate: "{เลขเอกสาร}|{รหัสสถานะ}|{fingerprint}" (2 ช่องแรกเหมือนเดิม — ตัวอ่านเก่ายังใช้ได้)</summary>
        public static string BuildStateMarker(string documentNumber, string status, string fingerprint)
        {
            string s = (documentNumber ?? "").Replace("|", "/") + "|" + (status ?? "");
            if (!string.IsNullOrEmpty(fingerprint)) s += "|" + fingerprint;
            return s;
        }

        /// <summary>fingerprint ที่เก็บใน marker ("" = marker รุ่นเก่า/ไม่มี)</summary>
        public static string MarkerFingerprint(string markerText)
        {
            if (string.IsNullOrWhiteSpace(markerText)) return "";
            string[] parts = markerText.Trim().Split('|');
            return parts.Length >= 3 ? parts[2].Trim() : "";
        }

        /// <summary>
        /// cache ใช้ต่อได้ไหม. forceRefresh (ปุ่ม "ดึงล่าสุด") = ไม่ใช้เสมอ.
        /// liveFingerprint = null → อ่านเอกสารจาก NextAcc ไม่ได้ → ใช้กติกาเดิม (มีไฟล์ + marker พ้นร่าง + คิวไม่ใหม่กว่าไฟล์)
        /// liveFingerprint มีค่า → ต้องตรงกับที่เก็บ (marker รุ่นเก่าไม่มีค่า = ดึงใหม่ 1 ครั้งแล้วจำ)
        /// </summary>
        public static bool CacheIsCurrent(bool fileOk, bool forceRefresh, bool markerFinal, bool queueNewerThanFile,
            string markerFingerprint, string liveFingerprint)
        {
            if (forceRefresh || !fileOk || !markerFinal || queueNewerThanFile) return false;
            if (liveFingerprint == null) return true;
            return !string.IsNullOrEmpty(markerFingerprint)
                && string.Equals(markerFingerprint, liveFingerprint, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>ไบต์ที่ได้เป็น PDF จริง (หัว %PDF- ภายใน 1 KB แรก — บางตัวสร้างมี BOM/ช่องว่างนำ) และไม่สั้นผิดปกติ</summary>
        public static bool LooksLikePdf(byte[] b)
        {
            if (b == null || b.Length < 64) return false;
            int max = Math.Min(b.Length - 5, 1024);
            for (int i = 0; i <= max; i++)
                if (b[i] == 0x25 && b[i + 1] == 0x50 && b[i + 2] == 0x44 && b[i + 3] == 0x46 && b[i + 4] == 0x2D)
                    return true;
            return false;
        }

        /// <summary>ชื่อไฟล์รุ่นสำรองเมื่อแทนที่ไฟล์เดิมไม่ได้: {stem}_v{ticks}{suffix}.pdf (suffix "_Cancel" อยู่ท้ายเสมอ)</summary>
        public static string VersionedFileName(string stem, string suffix, long ticks)
        {
            return stem + "_v" + ticks.ToString(CultureInfo.InvariantCulture) + (suffix ?? "") + ".pdf";
        }

        private static Regex VersionPattern(string stem, string suffix)
        {
            return new Regex("^" + Regex.Escape(stem) + "(_v\\d+)?" + Regex.Escape(suffix ?? "") + "\\.pdf$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        /// <summary>ไฟล์ cache ล่าสุดของเอกสารนี้ ({stem}{suffix}.pdf หรือรุ่น _v ที่ใหม่กว่า) — null = ไม่มี</summary>
        public static string ResolveCachedPdf(string folder, string stem, string suffix)
        {
            try
            {
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return null;
                var rx = VersionPattern(stem, suffix);
                return Directory.GetFiles(folder, stem + "*" + (suffix ?? "") + ".pdf")
                    .Where(f => rx.IsMatch(Path.GetFileName(f)))
                    .Select(f => new FileInfo(f))
                    .Where(fi => fi.Exists && fi.Length > 0)
                    .OrderByDescending(fi => fi.LastWriteTimeUtc)
                    .Select(fi => fi.FullName)
                    .FirstOrDefault();
            }
            catch { return null; }
        }

        /// <summary>
        /// เขียน PDF ทับไฟล์เดิมแบบ atomic. คืน null = สำเร็จ (writtenPath = ไฟล์ที่ใช้จริง) / ข้อความ = ล้มเหลว (ไฟล์เดิมคงอยู่)
        /// </summary>
        public static string WritePdfAtomically(string targetPath, byte[] pdf, string stem, string suffix, out string writtenPath)
        {
            writtenPath = null;
            if (!LooksLikePdf(pdf))
                return "ไฟล์ที่ได้จาก NextAcc ไม่ใช่ PDF (" + (pdf == null ? 0 : pdf.Length) + " ไบต์) — คงไฟล์เดิมไว้";
            string dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string tmp = targetPath + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp";
            try { File.WriteAllBytes(tmp, pdf); }
            catch (Exception exTmp) { return "เขียนไฟล์ชั่วคราวไม่ได้: " + exTmp.Message; }

            try
            {
                if (File.Exists(targetPath)) File.Replace(tmp, targetPath, null, true);
                else File.Move(tmp, targetPath);
                writtenPath = targetPath;
                return null;
            }
            catch (Exception exReplace)
            {
                // ไฟล์เดิมถูกล็อก (IIS กำลังส่งไฟล์/โปรแกรมสแกนไวรัส/เปิดค้าง) → ใช้ชื่อใหม่แทน ให้ผู้ใช้ได้ฉบับล่าสุดเสมอ
                try
                {
                    string alt = Path.Combine(dir ?? "", VersionedFileName(stem, suffix, DateTime.UtcNow.Ticks));
                    File.Move(tmp, alt);
                    writtenPath = alt;
                    return null;
                }
                catch
                {
                    try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                    return "บันทึกไฟล์ใหม่ทับไฟล์เดิมไม่ได้: " + exReplace.Message;
                }
            }
        }

        /// <summary>ลบรุ่นเก่า (ไฟล์หลัก/รุ่น _v) ที่ไม่ใช่ไฟล์ที่เพิ่งเขียน — best-effort (ไฟล์ที่ยังถูกล็อกค่อยลบรอบหน้า)</summary>
        public static void CleanupOlderVersions(string folder, string stem, string suffix, string keepPath)
        {
            try
            {
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;
                var rx = VersionPattern(stem, suffix);
                string mainName = stem + (suffix ?? "") + ".pdf";
                foreach (var f in Directory.GetFiles(folder, stem + "*" + (suffix ?? "") + ".pdf"))
                {
                    string fn = Path.GetFileName(f);
                    if (!rx.IsMatch(fn)) continue;
                    if (string.Equals(Path.GetFullPath(f), Path.GetFullPath(keepPath ?? ""), StringComparison.OrdinalIgnoreCase)) continue;
                    // ไฟล์หลักเก็บไว้เมื่อไฟล์ที่ใช้เป็นรุ่น _v (ไฟล์หลักถูกล็อก — ลบไม่ได้อยู่ดี) ; รุ่น _v อื่นลบได้
                    if (string.Equals(fn, mainName, StringComparison.OrdinalIgnoreCase)) continue;
                    try { File.Delete(f); } catch { }
                }
                foreach (var t in Directory.GetFiles(folder, stem + "*.tmp"))
                {
                    try { if ((DateTime.UtcNow - File.GetLastWriteTimeUtc(t)).TotalMinutes > 10) File.Delete(t); } catch { }
                }
            }
            catch { }
        }

        /// <summary>ต่อ "?v={เวลาแก้ไฟล์}" ให้ URL ไฟล์ static — เบราว์เซอร์ไม่ใช้ไฟล์รุ่นก่อนจาก cache</summary>
        public static string VersionedUrl(string relativeUrl, string localPath)
        {
            if (string.IsNullOrEmpty(relativeUrl)) return relativeUrl;
            try
            {
                if (!string.IsNullOrEmpty(localPath) && File.Exists(localPath))
                {
                    var fi = new FileInfo(localPath);
                    string v = fi.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture) + "-" + fi.Length.ToString(CultureInfo.InvariantCulture);
                    return relativeUrl + (relativeUrl.IndexOf('?') >= 0 ? "&" : "?") + "v=" + v;
                }
            }
            catch { }
            return relativeUrl;
        }

        /// <summary>ตัดช่องว่างหัวท้าย + ยุบช่องว่างซ้ำ (เทียบชื่อผู้ซื้อ)</summary>
        public static string NormalizeName(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            return Regex.Replace(s.Trim(), "\\s+", " ");
        }
    }

    /// <summary>ผลตัดสินเมื่อกด "ส่งไป NextAcc ใหม่" หลังแก้ชื่อผู้ซื้อ</summary>
    public enum BuyerChangeAction
    {
        /// <summary>ตัดสินไม่ได้ (ข้อมูลไม่พอ) → ทำตามเส้นเดิม</summary>
        Unknown = 0,
        /// <summary>ผู้ติดต่อเดียวกัน ชื่อเดียวกัน — ไม่มีอะไรเกี่ยวกับผู้ซื้อให้ทำ</summary>
        None,
        /// <summary>เอกสารผูกผู้ติดต่อของผู้ซื้อเองอยู่แล้ว → แก้ชื่อผู้ติดต่อ (PUT contacts) เอกสาร/PDF เปลี่ยนตาม ไม่ต้องยกเลิกใบ</summary>
        RenameContact,
        /// <summary>เอกสารผูกผู้ติดต่ออื่นที่เลขภาษีเดียวกัน (int_ จับคู่ด้วยเลขภาษี) → แก้ชื่อผู้ติดต่อตัวที่เอกสารใช้</summary>
        RenameDocumentContact,
        /// <summary>เอกสารผูกผู้ติดต่ออื่น (คนละคน/ผู้ติดต่อกลาง แต่ตอนนี้ข้อมูลภาษีครบ) → ต้องยกเลิกแล้วออกใหม่</summary>
        RecreateDocument,
        /// <summary>ใบแบบ "ไม่ประสงค์รับใบกำกับภาษี" (ผู้ติดต่อกลาง) และผู้ซื้อยังไม่มีเลขภาษี+ที่อยู่ — ชื่อจะไม่ขึ้นบนเอกสาร (ถูกต้อง)</summary>
        DeclinedNoName,
        /// <summary>ผู้ติดต่อของผู้ซื้อชี้ไปที่ผู้ติดต่อกลาง — ห้ามแก้ชื่อ (กระทบเอกสารลูกค้าเงินสดทุกใบ)</summary>
        SharedContactBlocked
    }

    public static class ReceiptBuyerChangePlanner
    {
        /// <summary>ชื่อแบบผู้ติดต่อกลาง "ลูกค้าเงินสด (ไม่ประสงค์รับใบกำกับภาษี)" / "ลูกค้าทั่วไป" ของ NextAcc</summary>
        public static bool LooksLikeSharedWalkIn(string contactName)
        {
            string n = NextAccPdfCache.NormalizeName(contactName);
            if (n.Length == 0) return false;
            return n.IndexOf("ไม่ประสงค์รับใบกำกับ", StringComparison.Ordinal) >= 0
                || n.StartsWith("ลูกค้าเงินสด", StringComparison.Ordinal)
                || n == "ลูกค้าทั่วไป";
        }

        private static bool ValidTaxId(string t)
        {
            return !string.IsNullOrWhiteSpace(t) && Regex.IsMatch(t.Trim(), "^\\d{13}$");
        }

        /// <param name="docContactId">ผู้ติดต่อที่เอกสาร NextAcc ใช้อยู่ (GET document → contact.id)</param>
        /// <param name="docContactName">ชื่อผู้ติดต่อบนเอกสาร "ก่อน" ส่งข้อมูลผู้ซื้อใหม่</param>
        /// <param name="docBuyerDeclined">ธง buyerDeclinedTaxInvoice บนเอกสาร</param>
        /// <param name="wantContactId">ผู้ติดต่อของผู้ซื้อตามใบเสร็จ (upsert ด้วยเบอร์โทร)</param>
        /// <param name="wantDeclined">เส้นทางปัจจุบันจะออกแบบไม่ประสงค์รับใบกำกับ (ผู้ติดต่อกลาง) หรือไม่</param>
        public static BuyerChangeAction Decide(Guid docContactId, string docContactName, string docContactTaxId,
            bool docBuyerDeclined, Guid? wantContactId, string wantName, string wantTaxId, bool wantDeclined)
        {
            if (docContactId == Guid.Empty || wantContactId == null || wantContactId.Value == Guid.Empty
                || string.IsNullOrWhiteSpace(wantName))
                return BuyerChangeAction.Unknown;

            bool docShared = LooksLikeSharedWalkIn(docContactName);
            bool sameName = string.Equals(NextAccPdfCache.NormalizeName(docContactName),
                NextAccPdfCache.NormalizeName(wantName), StringComparison.OrdinalIgnoreCase);

            if (docContactId == wantContactId.Value)
            {
                if (docShared) return BuyerChangeAction.SharedContactBlocked;
                return sameName ? BuyerChangeAction.None : BuyerChangeAction.RenameContact;
            }

            // คนละผู้ติดต่อ
            if (docShared || docBuyerDeclined)
            {
                // เอกสารแบบผู้ติดต่อกลาง: ผู้ซื้อยังไม่ครบ §86/4 → ชื่อไม่ขึ้นเป็นเรื่องปกติ / ครบแล้ว → ออกใหม่ในนามผู้ซื้อ
                if (wantDeclined) return BuyerChangeAction.DeclinedNoName;
                return BuyerChangeAction.RecreateDocument;
            }
            if (wantDeclined)
            {
                // เส้นทางปัจจุบันจะใช้ผู้ติดต่อกลาง แต่เอกสารเดิมออกในนามผู้ติดต่อจริง — ไม่ใช่เรื่องชื่อ ปล่อยเส้นเดิมตัดสิน
                return BuyerChangeAction.Unknown;
            }
            // ผู้ติดต่อซ้ำชื่อเดียวกัน (และเลขภาษีไม่ต่าง) — ชื่อบนเอกสารถูกแล้ว ไม่ยกเลิกใบเพื่อสลับแถวผู้ติดต่อเปล่า ๆ
            if (sameName && (!ValidTaxId(wantTaxId)
                || string.Equals((docContactTaxId ?? "").Trim(), wantTaxId.Trim(), StringComparison.Ordinal)))
                return BuyerChangeAction.None;
            // ผู้ติดต่อคนละแถวแต่เลขภาษีเดียวกัน (NextAcc integration จับคู่ผู้ซื้อด้วยเลขภาษี — IntegrationService.cs:1801-1826)
            // → ยกเลิก/ออกใหม่ก็จะได้ผู้ติดต่อแถวเดิม ⇒ แก้ชื่อผู้ติดต่อแถวที่เอกสารใช้
            if (ValidTaxId(docContactTaxId) && ValidTaxId(wantTaxId)
                && string.Equals(docContactTaxId.Trim(), wantTaxId.Trim(), StringComparison.Ordinal))
                return sameName ? BuyerChangeAction.None : BuyerChangeAction.RenameDocumentContact;
            return BuyerChangeAction.RecreateDocument;
        }

        /// <summary>
        /// "แก้แค่ผู้ซื้อ" — เอกสาร NextAcc ยอด/วันที่ตรงกับใบเสร็จปัจจุบัน และวิธีรับเงินไม่เปลี่ยนจากตอนส่ง
        /// ⇒ แก้ชื่อผู้ติดต่ออย่างเดียวพอ ไม่ต้อง resync/ยกเลิกใบ (เลขเอกสารคงเดิม)
        /// </summary>
        public static bool NothingElseChanged(decimal docTotal, decimal docDepositApplied, DateTime docDate,
            decimal receiptTotal, DateTime receiptDate, string sentPaymentMethod, string currentPaymentMethod)
        {
            bool totalOk = Math.Abs(docTotal - receiptTotal) <= 0.01m
                || (docDepositApplied > 0.005m && Math.Abs(docTotal - docDepositApplied - receiptTotal) <= 0.01m);
            bool dateOk = docDate.Date == receiptDate.Date;
            bool payOk = string.IsNullOrWhiteSpace(sentPaymentMethod) || string.IsNullOrWhiteSpace(currentPaymentMethod)
                || string.Equals(sentPaymentMethod.Trim(), currentPaymentMethod.Trim(), StringComparison.OrdinalIgnoreCase);
            return totalOk && dateOk && payOk;
        }
    }
}
