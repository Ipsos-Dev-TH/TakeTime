using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Threading.Tasks;

namespace Take_Time_BangPhra.Integration
{
    // ══════════════════════════════════════════════════════════════════════════════════════════
    //  CLAUDE.md ข้อ 22 (ต.ค. 2026)
    //   (1) "ดึงเอกสารล่าสุด" ไม่ทับไฟล์เดิม — หา GUID เอกสารจาก Account_Receipt.Nexaacc_Doc_Id ก่อน, ไม่เสิร์ฟ cache เก่า
    //       ตอนผู้ใช้สั่งดึงล่าสุด, เทียบลายนิ้วมือเอกสาร NextAcc กับ .docstate, เขียนไฟล์แบบ atomic, ล้าง cache ตามโฟลเดอร์ที่ใช้จริง
    //   (2) แก้ชื่อผู้ซื้อแล้ว "ส่งไป NextAcc ใหม่" — ส่งข้อมูลผู้ซื้อของใบเสร็จขึ้นผู้ติดต่อของตัวเอง แล้วเลือกทาง:
    //       แก้ชื่อผู้ติดต่อ (เลขเอกสารเดิม) / ยกเลิก→ออกใหม่ (เปลี่ยนผู้ติดต่อของเอกสารที่ลงบัญชีแล้วไม่ได้) /
    //       อธิบายเคส "ไม่ประสงค์รับใบกำกับภาษี" (ห้ามแก้ชื่อผู้ติดต่อกลาง)
    // ══════════════════════════════════════════════════════════════════════════════════════════
    public partial class AccountingSyncService
    {
        /// <summary>ผลของการตรวจผู้ซื้อก่อน repost</summary>
        internal sealed class BuyerChangeOutcome
        {
            public BuyerChangeAction Action;
            public string Message;
            /// <summary>ไม่ต้องทำอะไรต่อ (แก้ชื่อเสร็จ + ยอด/วันที่/วิธีรับเงินไม่เปลี่ยน) → repost คืน 0 ทันที</summary>
            public bool Done;
            /// <summary>ต้องยกเลิกแล้วออกใหม่ (ข้าม resyncUpdate/แก้ JE ในที่ ซึ่งไม่เปลี่ยนผู้ติดต่อของเอกสาร)</summary>
            public bool ForceRecreate;
            /// <summary>ทำต่อไม่ได้ (e-Tax ถึงกรมสรรพากรแล้ว ฯลฯ) → repost คืน -1 พร้อมข้อความนี้</summary>
            public string Blocked;
        }

        /// <summary>ตัวชี้เอกสาร NextAcc บนใบเสร็จ (PHASE18_32) — ถูกปลดเมื่อ void ยืนยันแล้ว (AfterVerifiedReceiptVoid)</summary>
        private Guid LookupLinkedReceiptDocId(string receiptNumber)
        {
            if (string.IsNullOrWhiteSpace(receiptNumber)) return Guid.Empty;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT TOP 1 Nexaacc_Doc_Id FROM Account_Receipt WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", receiptNumber.Trim() } });
                Guid g;
                if (dt != null && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value
                    && Guid.TryParse(dt.Rows[0][0].ToString(), out g))
                    return g;
            }
            catch { /* ยังไม่รัน PHASE18_32 */ }
            return Guid.Empty;
        }

        /// <summary>
        /// GUID เอกสารของใบเสร็จสำหรับเปิด PDF: ตัวชี้บนใบ (Nexaacc_Doc_Id) ก่อน → คิว CREATE ที่ COMPLETED ล่าสุด.
        /// เดิมดูแต่คิว — หลัง void→สร้างใหม่/resync (แถวเดิม SUPERSEDED, แถวใหม่ยังไม่ COMPLETED) หาไม่เจอ แล้วตกไปเสิร์ฟไฟล์ cache เก่า
        /// </summary>
        private Guid LookupReceiptDocIdForPdf(string receiptNumber)
        {
            Guid g = LookupLinkedReceiptDocId(receiptNumber);
            return g != Guid.Empty ? g : LookupNexaaccDocIdByReceipt(receiptNumber);
        }

        /// <summary>ลายนิ้วมือของเอกสารตามที่ NextAcc ตอบ (null = ไม่มีข้อมูล)</summary>
        private static string LiveDocFingerprint(DocumentResponse d)
        {
            if (d == null) return null;
            return NextAccPdfCache.Fingerprint(d.DocumentNumber, d.Status, d.TotalAmount, d.VatAmount, d.DocumentDate,
                d.Contact?.Id ?? Guid.Empty, d.Contact?.Name, d.Contact?.TaxId, d.BuyerDeclinedTaxInvoice,
                d.EtaxStatus, d.DepositAppliedAmount);
        }

        private static string ReadPdfStateMarker(string statePath)
        {
            try { return File.Exists(statePath) ? File.ReadAllText(statePath) : ""; }
            catch { return ""; }
        }

        private static void WritePdfStateMarkerFp(string statePath, string documentNumber, string status, string fingerprint)
        {
            try { File.WriteAllText(statePath, NextAccPdfCache.BuildStateMarker(documentNumber, status, fingerprint)); }
            catch { }
        }

        /// <summary>โฟลเดอร์ฐานที่ใช้จริง — AppCfg (ค่าใน DB ก่อน Web.config) เหมือนตัวดึง PDF + ค่าใน Web.config ถ้าต่างกัน
        /// (เดิมล้าง cache จาก Web.config อย่างเดียว ⇒ ตั้งโฟลเดอร์ในหน้าตั้งค่าระบบแล้วล้างผิดที่ ไฟล์เก่าค้าง)</summary>
        private static List<string> DocumentFolderBases(string key)
        {
            var list = new List<string>();
            try
            {
                string a = AppCfg.Get(key);
                if (!string.IsNullOrWhiteSpace(a)) list.Add(a);
            }
            catch { }
            try
            {
                string w = ConfigurationManager.AppSettings[key];
                if (!string.IsNullOrWhiteSpace(w) && !list.Exists(x => string.Equals(x, w, StringComparison.OrdinalIgnoreCase)))
                    list.Add(w);
            }
            catch { }
            return list;
        }

        /// <summary>
        /// ก่อน repost ใบเสร็จ: ส่งข้อมูลผู้ซื้อล่าสุดของใบ (Account_Receipt.Customer_ID → Customer) ขึ้นผู้ติดต่อของตัวเองบน NextAcc
        /// แล้วตัดสินว่าเอกสารเดิมจะได้ชื่อใหม่ทางไหน. null = ไม่เกี่ยว/ตัดสินไม่ได้ (ทำตามเส้นเดิมทุกอย่าง)
        /// สัญญา NextAcc: PDF พิมพ์ชื่อจากผู้ติดต่อของเอกสาร (PdfGenerationService.cs:2148 doc.Contact.Name — ไม่มี snapshot ชื่อบนเอกสาร);
        /// แก้ผู้ติดต่อ = PUT document/contacts/{id} (DocumentController.cs:1486, DocumentService.cs:13898);
        /// เปลี่ยน ContactId ของเอกสารที่ลงบัญชีแล้วไม่ได้ (UpdateDocumentAsync แก้ได้เฉพาะร่าง DocumentService.cs:2611-2621)
        /// และ resyncUpdate ไม่แตะผู้ติดต่อ (IntegrationService.cs:3311-3342) ⇒ เปลี่ยนตัวผู้ซื้อ = ยกเลิก→ออกใหม่
        /// </summary>
        private async Task<BuyerChangeOutcome> PlanBuyerChangeForRepostAsync(string receiptNumber, Dictionary<string, object> p,
            string docType, string responseId)
        {
            if (string.IsNullOrEmpty(receiptNumber) || p == null) return null;
            if (docType == "JOURNAL") return null;   // JE ไม่มีผู้ซื้อ
            if (!_config.IsReceiptDocumentMode || !_config.CanUseCompanyEndpoints) return null;   // อ่านเอกสาร/แก้ผู้ติดต่อไม่ได้
            if (_config.GetConfigValue("Nexaacc_Repost_Buyer_Sync", "1") == "0") return null;

            Guid docId = ExtractGuid(responseId);
            if (docId == Guid.Empty) docId = LookupLinkedReceiptDocId(receiptNumber);
            if (docId == Guid.Empty) return null;

            int resId = 0;
            if (p.ContainsKey("reservationId") && p["reservationId"] != null) int.TryParse(p["reservationId"].ToString(), out resId);
            string phone = p.ContainsKey("customerPhone") ? (p["customerPhone"]?.ToString() ?? "").Trim() : "";
            bool isDeposit = p.ContainsKey("isDeposit") && p["isDeposit"] != null
                && string.Equals(p["isDeposit"].ToString(), "true", StringComparison.OrdinalIgnoreCase);

            // 1) เอกสารเดิม "ก่อน" ส่งข้อมูลผู้ซื้อ (ได้ชื่อเดิมบนเอกสาร)
            DocumentResponse before;
            try { before = (await _apiClient.GetDocumentAsync(docId))?.data; }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync", $"RepostBuyerSync: receipt={receiptNumber} อ่านเอกสาร {docId} ไม่ได้ ({ex.Message}) → เส้นเดิม", "SYSTEM");
                return null;
            }
            if (before == null || before.Contact == null) return null;
            if (before.Status == NexaaccDocumentStatus.Voided) return null;   // เส้น "เอกสารเดิมถูกยกเลิก" จัดการเอง

            // 2) ผู้ซื้อของใบ — ตัวเดียวกับตอนออกเอกสาร (ResolveReceiptBuyer); การจองไม่มีเบอร์ = ผู้ติดต่อ RES-{id} ของการจอง
            string src;
            var buyer = ResolveReceiptBuyer(receiptNumber, resId, phone, out src);
            bool synthetic = false;
            if (buyer == null && resId > 0)
            {
                buyer = new ContactInfo { ExternalId = $"RES-{resId}", Name = LookupGuestName(resId) };
                if (string.IsNullOrWhiteSpace(buyer.Name)) buyer = null; else { synthetic = true; src = $"ผู้เข้าพักการจอง #{resId}"; }
            }
            if (buyer == null || string.IsNullOrWhiteSpace(buyer.Name)) return null;

            string oldName = NextAccPdfCache.NormalizeName(before.Contact.Name);
            string newName = NextAccPdfCache.NormalizeName(buyer.Name);

            // กันแก้ชื่อผู้ติดต่อกลาง: ผู้ติดต่อของผู้ซื้อ (cache) ชี้ไปที่ผู้ติดต่อที่ชื่อแบบลูกค้าเงินสด → ไม่ push
            Guid mappedId = Guid.Empty;
            try
            {
                var m = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT TOP 1 Nexaacc_Contact_Id FROM Accounting_Contact_Map WHERE External_Id = @e AND Contact_Type = 'CUSTOMER'",
                    new Dictionary<string, object> { { "@e", buyer.ExternalId ?? "" } });
                if (m != null && m.Rows.Count > 0 && m.Rows[0][0] != DBNull.Value) Guid.TryParse(m.Rows[0][0].ToString(), out mappedId);
            }
            catch { }
            if (mappedId != Guid.Empty && mappedId == before.Contact.Id && ReceiptBuyerChangePlanner.LooksLikeSharedWalkIn(before.Contact.Name))
            {
                return new BuyerChangeOutcome
                {
                    Action = BuyerChangeAction.SharedContactBlocked,
                    Message = $"⚠ ผู้ติดต่อของผู้ซื้อ \"{newName}\" ผูกกับผู้ติดต่อกลาง \"{oldName}\" บน NextAcc — ไม่แก้ชื่อผู้ติดต่อกลาง (กระทบใบลูกค้าเงินสดทุกใบ). "
                        + "แก้: Admin → NextAcc → กล่อง 🔗 ส่งผู้ติดต่อของลูกค้ารายนี้ใหม่ แล้วกดส่งอีกครั้ง"
                };
            }

            // 3) ส่งข้อมูลผู้ซื้อล่าสุดขึ้นผู้ติดต่อของตัวเอง (upsert ด้วยเบอร์ + PUT ชื่อ/เลขภาษี/ที่อยู่) — ไม่ใช่ผู้ติดต่อกลาง
            string pushErr = await PushCustomerContactAsync(buyer, "RepostBuyerSync");
            if (pushErr != null || buyer.NexaaccContactId == null)
            {
                return new BuyerChangeOutcome
                {
                    Action = BuyerChangeAction.Unknown,
                    Message = $"⚠ ส่งข้อมูลผู้ซื้อ \"{newName}\" ขึ้น NextAcc ไม่สำเร็จ ({pushErr ?? "ไม่ได้รหัสผู้ติดต่อ"}) — ชื่อบนเอกสารอาจยังเป็น \"{oldName}\""
                };
            }

            bool wantDeclined = docType == "INVOICE"
                && (isDeposit ? !HasFullBuyerTaxData(buyer) : !WantsFullTaxInvoice(buyer));
            var action = ReceiptBuyerChangePlanner.Decide(before.Contact.Id, before.Contact.Name, before.Contact.TaxId,
                before.BuyerDeclinedTaxInvoice, buyer.NexaaccContactId, buyer.Name, buyer.TaxId, wantDeclined);
            _code.Logs(_connectionString, "AccountingSync",
                $"RepostBuyerSync: receipt={receiptNumber} doc={before.DocumentNumber} ผู้ติดต่อบนเอกสาร={oldName} ({before.Contact.Id.ToString().Substring(0, 8)}) " +
                $"declined={(before.BuyerDeclinedTaxInvoice ? "Y" : "N")} → ผู้ซื้อของใบ={newName} [{src}{(synthetic ? ", synthetic" : "")}] " +
                $"({buyer.NexaaccContactId.Value.ToString().Substring(0, 8)}) wantDeclined={(wantDeclined ? "Y" : "N")} ⇒ {action}", "SYSTEM");

            var outc = new BuyerChangeOutcome { Action = action };
            switch (action)
            {
                case BuyerChangeAction.None:
                case BuyerChangeAction.Unknown:
                    return outc;

                case BuyerChangeAction.SharedContactBlocked:
                    outc.Message = $"⚠ เอกสารผูกผู้ติดต่อกลาง \"{oldName}\" — ไม่แก้ชื่อผู้ติดต่อกลาง";
                    return outc;

                case BuyerChangeAction.DeclinedNoName:
                    outc.Message = $"ℹ️ ใบนี้ออกแบบ \"ลูกค้าไม่ประสงค์รับใบกำกับภาษี\" (ผู้ติดต่อกลาง \"{oldName}\") — ชื่อ \"{newName}\" จะไม่ขึ้นบนเอกสาร "
                        + "ซึ่งถูกต้องตาม §86/6. ต้องการใบกำกับในนามผู้ซื้อ: กรอกเลขผู้เสียภาษี 13 หลัก + ที่อยู่ ในหน้าใบเสร็จ กดบันทึก แล้วกดส่งใหม่";
                    return outc;

                case BuyerChangeAction.RecreateDocument:
                    if (IsEtaxReachedRd(before.EtaxStatus))
                    {
                        outc.Blocked = $"เปลี่ยนผู้ซื้อของ {before.DocumentNumber} (\"{oldName}\" → \"{newName}\") ไม่ได้: ส่ง e-Tax ถึงกรมสรรพากรแล้ว ({before.EtaxStatus}) — "
                            + "ต้องออกใบลดหนี้ยกเลิกใบเดิมแล้วออกใบใหม่ในนามผู้ซื้อ (หน้า ใบลดหนี้)";
                        return outc;
                    }
                    outc.ForceRecreate = true;
                    outc.Message = $"ผู้ซื้อบนเอกสาร {before.DocumentNumber} = \"{oldName}\" ≠ ผู้ซื้อของใบ \"{newName}\" — NextAcc เปลี่ยนผู้ซื้อของเอกสารที่ลงบัญชีแล้วไม่ได้ "
                        + "(และ resync ไม่เปลี่ยนผู้ติดต่อ) → ยกเลิกเอกสารเดิมแล้วออกใหม่ในนามผู้ซื้อ";
                    return outc;
            }

            // RenameContact / RenameDocumentContact
            Guid targetContact = action == BuyerChangeAction.RenameDocumentContact ? before.Contact.Id : buyer.NexaaccContactId.Value;
            if (action == BuyerChangeAction.RenameDocumentContact)
            {
                try
                {
                    await _apiClient.UpdateContactAsync(targetContact, new UpdateContactRequest { Name = buyer.Name.Trim() });
                }
                catch (Exception ex)
                {
                    outc.Message = $"⚠ แก้ชื่อผู้ติดต่อบนเอกสาร ({oldName} → {newName}) ไม่สำเร็จ: {ex.Message}";
                    return outc;
                }
            }

            DocumentResponse after = null;
            try { after = (await _apiClient.GetDocumentAsync(docId))?.data; } catch { }
            bool nameOk = after?.Contact != null && string.Equals(NextAccPdfCache.NormalizeName(after.Contact.Name), newName, StringComparison.OrdinalIgnoreCase);
            if (!nameOk)
            {
                outc.Message = $"⚠ ส่งชื่อผู้ซื้อ \"{newName}\" แล้ว แต่เอกสาร {before.DocumentNumber} บน NextAcc ยังแสดง \"{after?.Contact?.Name ?? oldName}\" "
                    + "(สิทธิ์แก้ผู้ติดต่อของคีย์ API? ดู log RepostBuyerSync)";
                return outc;
            }
            ClearReceiptPdfCache(receiptNumber);
            outc.Message = $"✅ อัปเดตชื่อผู้ซื้อใน NextAcc แล้ว: \"{oldName}\" → \"{newName}\" (เอกสาร {before.DocumentNumber} เลขเดิม — ผู้ติดต่อนี้ทุกใบจะแสดงชื่อใหม่)"
                + (IsEtaxReachedRd(before.EtaxStatus)
                    ? $" ⚠ ใบนี้ส่ง e-Tax ถึงกรมสรรพากรแล้ว ({before.EtaxStatus}) — ข้อมูลผู้ซื้อใน e-Tax ที่ยื่นไปไม่เปลี่ยน ถ้าต้องแก้ทางภาษีให้ออกใบลดหนี้แล้วออกใบใหม่"
                    : "");

            // ยอด/วันที่/วิธีรับเงินเหมือนเอกสาร → ไม่ต้อง resync/ยกเลิกใบ
            try
            {
                var r = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT TOP 1 Total_Amount, Created_Date, Paid_Type FROM Account_Receipt WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", receiptNumber } });
                if (r != null && r.Rows.Count > 0 && r.Rows[0]["Total_Amount"] != DBNull.Value && r.Rows[0]["Created_Date"] != DBNull.Value
                    && _config.GetConfigValue("Nexaacc_Repost_NameOnly_Shortcut", "1") != "0")
                {
                    string sentPay = p.ContainsKey("paymentMethod") ? p["paymentMethod"]?.ToString() : null;
                    outc.Done = ReceiptBuyerChangePlanner.NothingElseChanged(after.TotalAmount, after.DepositAppliedAmount, after.DocumentDate,
                        Convert.ToDecimal(r.Rows[0]["Total_Amount"]), Convert.ToDateTime(r.Rows[0]["Created_Date"]),
                        sentPay, r.Rows[0]["Paid_Type"]?.ToString());
                }
            }
            catch { outc.Done = false; }
            return outc;
        }
    }
}
