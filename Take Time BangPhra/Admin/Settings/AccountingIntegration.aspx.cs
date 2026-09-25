using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Web.UI;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Admin.Settings
{
    public partial class AccountingIntegration : Page
    {
        private readonly code _code = new code();
        private string ConnStr => ConfigurationManager.ConnectionStrings["TaketimeConnectionString"]?.ConnectionString ?? "";

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.SysAccounting)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("~/Admin/Login");
                return;
            }

            if (Request.HttpMethod == "POST" && Request.ContentType?.Contains("application/json") == true)
            {
                HandlePost();
                return;
            }

            string action = Request.QueryString["action"];
            if (!string.IsNullOrEmpty(action))
            {
                HandleAction(action);
                return;
            }

            if (!IsPostBack)
            {
                LoadConfig();
                LoadQueue();
            }
        }

        /// <summary>ปกปิด API key สำหรับแสดงผล: เผยให้เห็น prefix (int_/acc_ + 4 ตัว) และ 4 ตัวท้าย</summary>
        private static string MaskKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            if (key.Length <= 12) return key.Substring(0, Math.Min(4, key.Length)) + "••••";
            return key.Substring(0, 8) + "••••" + key.Substring(key.Length - 4);
        }

        private void LoadConfig()
        {
            try
            {
                var config = new Integration.AccountingConfig(ConnStr);
                var data = new Dictionary<string, object>
                {
                    { "baseUrl", config.BaseUrl },
                    { "hasApiKey", !string.IsNullOrEmpty(config.ApiKey) },
                    { "apiKeyMask", MaskKey(config.ApiKey) },
                    { "hasCompanyApiKey", config.HasDedicatedCompanyKey },
                    { "companyApiKeyMask", config.HasDedicatedCompanyKey ? MaskKey(config.CompanyApiKey) : "" },
                    { "companyId", config.CompanyId != Guid.Empty ? config.CompanyId.ToString() : "" },
                    { "enabled", config.Enabled },
                    { "syncMode", config.SyncMode },
                    { "receiptSyncMode", config.ReceiptSyncMode },
                    { "voucherSyncMode", config.VoucherSyncMode },
                    { "payrollSyncMode", config.PayrollSyncMode },
                    { "posDailyRollup", config.IsPosDailyRollupEnabled },
                    { "roomServiceRevenue", config.IsRoomServiceRevenueEnabled },
                    { "etaxRdWatch", config.IsEtaxRdWatchEnabled },
                    { "etaxRdFrom", config.EtaxRdFromContains },
                    { "otaRoomRevenue", config.IsOtaRoomRevenueEnabled },
                    { "otaDocumentMode", config.OtaDocumentMode },
                    { "stockInUseGRNI", config.IsStockInUseGRNI },
                    { "stockInSkipJournal", config.IsStockInSkipJournal },
                    { "stockQtySync", config.IsStockQtySyncEnabled },
                    { "stockQtyPull", config.IsStockQtyPullEnabled },
                    { "attachFiles", config.AttachFiles },
                    { "depositVatRecognition", config.DepositVatRecognition },
                    { "depositDeferOutputVat", config.IsDepositOutputVatDeferred },
                    { "depositDrivesJournal", config.IsDepositAppliedDrivesJournal },
                    { "depositDrivesJournalRef", config.IsDrivesJournalRefEnabled },
                    { "autoRecoverDeposit", config.IsAutoRecoverDeposit },
                    { "postSyncVerify", config.IsPostSyncVerifyEnabled },
                    { "autoReconcileDeposit", config.IsAutoReconcileDeposit },
                    { "cashSaleUseReceipt", config.IsCashSaleUseReceipt },
                    { "cashSaleCompanyDoc", config.IsCashSaleCompanyDoc },
                    { "receiptHeaderType", config.ReceiptHeaderType },
                    { "etaxAutoGenerate", config.IsEtaxAutoGenerate },
                    { "etaxAutoSign", config.IsEtaxAutoSign },
                    { "etaxAutoSubmit", config.IsEtaxAutoSubmit },
                    { "etaxAutoSendEmail", config.IsEtaxAutoSendEmail },
                    { "etaxEmailSubject", config.EtaxEmailSubject },
                    { "etaxEmailCc", config.EtaxEmailCc },
                    { "etaxEmailBody", config.EtaxEmailBody },
                    { "etaxEmailAttachPdf", config.EtaxEmailAttachPdf },
                    { "etaxEmailAttachXml", config.EtaxEmailAttachXml },
                    { "etaxEmailLocalOnly", config.EtaxEmailLocalOnly },
                    { "etaxEmailFallback", config.EtaxEmailFallback },
                    { "syncInterval", config.SyncIntervalSeconds },
                    { "maxRetries", config.MaxRetries },
                    { "timeout", config.TimeoutSeconds },
                    { "isConfigured", config.IsConfigured },
                    // Email reservation intake (STAAH)
                    { "emailRsvEnabled", config.IsEmailReservationEnabled },
                    { "emailRsvImapServer", config.EmailRsvImapServer },
                    { "emailRsvImapPort", config.EmailRsvImapPort },
                    { "emailRsvUsername", config.EmailRsvUsername },
                    { "emailRsvHasPassword", config.EmailRsvHasPassword },
                    { "emailRsvPollMinutes", config.EmailRsvPollMinutes },
                    { "emailRsvProcessedLabel", config.EmailRsvProcessedLabel },
                    { "emailRsvFailedLabel", config.EmailRsvFailedLabel },
                    { "emailRsvIgnoredLabel", config.EmailRsvIgnoredLabel },
                    { "emailRsvDefaultCollect", config.EmailRsvDefaultCollect },
                    { "emailRsvMaxStayDays", config.EmailRsvMaxStayDays },
                    { "emailRsvMaxDaysFuture", config.EmailRsvMaxDaysFuture },
                    { "emailRsvNotifyTelegram", config.EmailRsvNotifyTelegram },
                    { "emailRsvCreateDocument", config.EmailRsvCreateDocument },
                    { "emailRsvMoveFailed", config.EmailRsvMoveFailed },
                    { "emailRsvFromContains", config.EmailRsvFromContains },
                    { "emailRsvRetryFailed", config.EmailRsvRetryFailed },
                    { "emailRsvRetryHours", config.EmailRsvRetryHours },
                    { "emailRsvMapAnyChannel", config.EmailRsvMapAnyChannel },
                    { "emailRsvRoomPriority", config.EmailRsvRoomPriority },
                    { "emailRsvDefaultPhone", config.EmailRsvDefaultPhone },
                    { "emailRsvCancelStatus", config.EmailRsvCancelStatus },
                    // Daily reservation board → LINE
                    { "lineDailyEnabled", config.IsDailyLineReportEnabled },
                    { "lineDailyRecipients", config.LineDailyRecipients },
                    { "lineDailySendTime", config.LineDailySendTime },
                    { "lineDailySourceUrl", config.LineDailySourceUrl },
                    { "lineDailyImageWidth", config.LineDailyImageWidth },
                    { "lineDailyImageHeight", config.LineDailyImageHeight },
                    { "lineDailyAutoHeight", config.LineDailyAutoHeight },
                    { "lineDailyCaption", config.LineDailyCaption },
                    { "lineDailyPublicBaseUrl", config.LineDailyPublicBaseUrl },
                    { "lineDailyImageFolder", config.LineDailyImageFolder },
                    { "lineDailyHasTokenOverride", config.LineDailyHasTokenOverride },
                    { "lineDailyJpegQuality", config.LineDailyJpegQuality },
                    { "lineDailyFontScale", config.LineDailyFontScale },
                    { "lineDailyLastSent", config.LineDailyLastSent }
                };
                hfConfigData.Value = new JavaScriptSerializer().Serialize(data);
            }
            catch
            {
                hfConfigData.Value = "{}";
            }
        }

        private void LoadQueue()
        {
            // Queue data loaded via AJAX
        }

        private void HandleAction(string action)
        {
            Dictionary<string, object> result;

            switch (action)
            {
                case "testApi":
                    result = TestApiLogin();
                    break;
                case "fetchAccounts":
                    result = FetchChartOfAccounts();
                    break;
                case "processQueue":
                    result = ProcessQueueNow();
                    break;
                case "reconcileDeleted":
                    result = ReconcileDeletedDocuments();
                    break;
                case "cleanupOrphanReceipts":
                    result = CleanupOrphanReceipts();
                    break;
                case "cleanupDepositDebris":
                    result = CleanupDepositDebris();
                    break;
                case "resetReservation":
                    result = ResetReservationAccounting();
                    break;
                case "grniReconcile":
                    result = GrniReconcile();
                    break;
                case "applyRecommendedPreset":
                    result = ApplyRecommendedPreset();
                    break;
                case "queueData":
                    result = GetQueueData();
                    break;
                case "retryItem":
                    result = RetryQueueItem();
                    break;
                case "itemLogs":
                    result = GetItemLogs();
                    break;
                case "resyncItem":
                    result = ResyncCompletedItem();
                    break;
                case "retryAllFailed":
                    result = RetryAllFailed();
                    break;
                case "syncAccounts":
                    result = SyncChartOfAccounts();
                    break;
                case "nexaaccAccounts":
                    result = GetNexaaccAccounts();
                    break;
                case "mappings":
                    result = GetAccountMappings();
                    break;
                case "healthCheck":
                    result = RunIntegrationHealthCheck();
                    break;
                case "relinkDoc":
                    result = RelinkReceiptDocument();
                    break;
                case "inspectReceipt":
                    result = InspectReceiptBuyer();
                    break;
                case "pushBuyerContact":
                    result = PushBuyerContactNow();
                    break;
                case "updateMapping":
                    result = UpdateAccountMapping();
                    break;
                case "cleanupAutoSync":
                    result = CleanupOldAutoSync();
                    break;
                case "previewCleanup":
                    result = PreviewAutoSyncCleanup();
                    break;
                case "getPaidHowMapping":
                    result = GetPaidHowMapping();
                    break;
                case "getPaidTypeMapping":
                    result = GetPaidTypeMapping();
                    break;
                case "updatePaidHowAccount":
                    result = UpdatePaidHowAccount();
                    break;
                case "updatePaidTypeAccount":
                    result = UpdatePaidTypeAccount();
                    break;
                case "syncWallets":
                    result = SyncNexaaccWallets();
                    break;
                case "walletOptions":
                    result = GetWalletOptions();
                    break;
                case "getGatewayPaidHow":
                    result = GetGatewayPaidHow();
                    break;
                case "getOtaChannelMap":
                    result = GetOtaChannelMap();
                    break;
                case "lookupDocSource":
                    result = LookupDocumentSource();
                    break;
                case "depositStatus":
                    result = LookupDepositStatus();
                    break;
                case "emailIntakeRun":
                    result = RunEmailIntakeNow();
                    break;
                case "emailIntakeTest":
                    result = TestEmailIntakeConnection();
                    break;
                case "emailIntakeLog":
                    result = GetEmailIntakeLog();
                    break;
                case "emailIntakeDiagnose":
                    result = DiagnoseEmailIntake();
                    break;
                case "emailIntakeTestTelegram":
                    result = TestEmailIntakeTelegram();
                    break;
                case "emailIntakePreview":
                    result = PreviewEmailIntake();
                    break;
                case "emailIntakeRecover":
                    result = RecoverEmailBacklog();
                    break;
                case "closeQueueItem":
                    result = CloseQueueItemManually();
                    break;
                case "lineDailySend":
                    result = SendDailyLineNow();
                    break;
                case "lineDailyPreview":
                    result = PreviewDailyLine();
                    break;
                case "lineDailyTest":
                    result = TestDailyLine();
                    break;
                case "lineDailyLog":
                    result = GetDailyLineLog();
                    break;
                default:
                    result = new Dictionary<string, object> { { "success", false }, { "message", "Unknown action" } };
                    break;
            }

            WriteJson(result);
        }

        private void HandlePost()
        {
            string body;
            using (var reader = new StreamReader(Request.InputStream))
                body = reader.ReadToEnd();

            string action = Request.QueryString["action"] ?? "";
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(body);
            Dictionary<string, object> result;

            switch (action)
            {
                case "saveApi":
                    result = SaveApiConfig(data);
                    break;
                case "saveSyncSettings":
                    result = SaveSyncSettings(data);
                    break;
                case "deleteQueueItems":
                    result = DeleteQueueItems(data);
                    break;
                case "etaxGenerate":
                    result = ManualEtaxGenerate(data);
                    break;
                case "etaxSendEmail":
                    result = ManualEtaxSendEmail(data);
                    break;
                case "depositManual":
                    result = ManualDepositOperation(data);
                    break;
                case "stockAdjustment":
                    result = ManualStockAdjustment(data);
                    break;
                case "stockProductSync":
                    result = ManualProductSync(data);
                    break;
                case "saveEmailIntake":
                    result = SaveEmailIntakeConfig(data);
                    break;
                case "saveLineDaily":
                    result = SaveLineDailyConfig(data);
                    break;
                case "saveGatewayPaidHow":
                    result = SaveGatewayPaidHow(data);
                    break;
                case "saveOtaChannelMap":
                    result = SaveOtaChannelMap(data);
                    break;
                default:
                    result = new Dictionary<string, object> { { "success", false }, { "message", "Unknown action" } };
                    break;
            }

            WriteJson(result);
        }

        private Dictionary<string, object> SaveApiConfig(Dictionary<string, object> data)
        {
            try
            {
                var config = new Integration.AccountingConfig(ConnStr);
                if (data.ContainsKey("baseUrl")) config.SetConfig("Nexaacc_BaseUrl", data["baseUrl"]?.ToString() ?? "");
                if (data.ContainsKey("apiKey") && !string.IsNullOrEmpty(data["apiKey"]?.ToString()))
                    config.SetConfig("Nexaacc_ApiKey_Encrypted", _code.Crypt(data["apiKey"].ToString()));
                // acc_ key สำหรับ company endpoints (ไม่บังคับ): ส่ง "-" เพื่อล้าง (กลับไปใช้ int_ ตัวเดียว)
                if (data.ContainsKey("companyApiKey"))
                {
                    string ck = data["companyApiKey"]?.ToString() ?? "";
                    if (ck == "-")
                        config.SetConfig("Nexaacc_CompanyApiKey_Encrypted", "");
                    else if (!string.IsNullOrEmpty(ck))
                        config.SetConfig("Nexaacc_CompanyApiKey_Encrypted", _code.Crypt(ck));
                }
                if (data.ContainsKey("companyId")) config.SetConfig("Nexaacc_CompanyId", data["companyId"]?.ToString() ?? "");

                return new Dictionary<string, object> { { "success", true }, { "message", "บันทึก API Config สำเร็จ" } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> SaveSyncSettings(Dictionary<string, object> data)
        {
            try
            {
                var config = new Integration.AccountingConfig(ConnStr);
                if (data.ContainsKey("enabled")) config.SetConfig("Nexaacc_Enabled", data["enabled"]?.ToString() ?? "false");
                if (data.ContainsKey("syncMode")) config.SetConfig("Nexaacc_SyncMode", data["syncMode"]?.ToString() ?? "DOCUMENT");
                if (data.ContainsKey("receiptSyncMode")) config.SetConfig("Nexaacc_SyncMode_Receipt", data["receiptSyncMode"]?.ToString() ?? "");
                if (data.ContainsKey("voucherSyncMode")) config.SetConfig("Nexaacc_SyncMode_Voucher", data["voucherSyncMode"]?.ToString() ?? "");
                if (data.ContainsKey("payrollSyncMode")) config.SetConfig("Nexaacc_SyncMode_Payroll", data["payrollSyncMode"]?.ToString() ?? "");
                if (data.ContainsKey("posDailyRollup")) config.SetConfig("Nexaacc_PosDailyRollup", BoolToFlag(data["posDailyRollup"]));
                if (data.ContainsKey("roomServiceRevenue")) config.SetConfig("Nexaacc_RoomServiceRevenue", BoolToFlag(data["roomServiceRevenue"]));
                if (data.ContainsKey("etaxRdWatch")) config.SetConfig("Etax_Rd_Watch_Enabled", BoolToFlag(data["etaxRdWatch"]));
                if (data.ContainsKey("etaxRdFrom")) config.SetConfig("Etax_Rd_FromContains", data["etaxRdFrom"]?.ToString() ?? "rd.go.th, etax, teda.th");
                if (data.ContainsKey("otaRoomRevenue")) config.SetConfig("Nexaacc_OtaRoomRevenue", BoolToFlag(data["otaRoomRevenue"]));
                // โหมดเอกสาร OTA = นโยบายบัญชี → เขียนเฉพาะเมื่อเปลี่ยนจริง + log (หน้าเว็บส่งมาเฉพาะเมื่อโหลดค่าจริงสำเร็จ)
                if (data.ContainsKey("otaDocumentMode"))
                {
                    string odm = (data["otaDocumentMode"]?.ToString() ?? "OFF").Trim().ToUpperInvariant();
                    if (odm != "RECEIPT_DOC") odm = "OFF";
                    string curOdm = config.OtaDocumentMode;
                    if (!string.Equals(curOdm, odm, StringComparison.OrdinalIgnoreCase))
                    {
                        _code.Logs(ConnStr, "AccountingConfig",
                            $"โหมดเอกสาร OTA ถูกเปลี่ยน: Nexaacc_OtaDocument_Mode {curOdm} → {odm} " +
                            $"โดย {Session["UserName"] ?? "?"} — มีผลกับการจอง Channel Collect ที่ยังไม่เคยโพสต์ (Ota_Revenue_Ref ว่าง) เท่านั้น",
                            Session["UserName"]?.ToString() ?? "SYSTEM");
                        config.SetConfig("Nexaacc_OtaDocument_Mode", odm);
                    }
                }
                if (data.ContainsKey("stockInUseGRNI")) config.SetConfig("Nexaacc_StockIn_UseGRNI", BoolToFlag(data["stockInUseGRNI"]));
                if (data.ContainsKey("stockInSkipJournal")) config.SetConfig("Nexaacc_StockIn_SkipJournal", BoolToFlag(data["stockInSkipJournal"]));
                if (data.ContainsKey("stockQtySync")) config.SetConfig("Nexaacc_StockQtySync", BoolToFlag(data["stockQtySync"]));
                if (data.ContainsKey("stockQtyPull")) config.SetConfig("Nexaacc_StockQtyPull", BoolToFlag(data["stockQtyPull"]));
                if (data.ContainsKey("attachFiles")) config.SetConfig("Nexaacc_AttachFiles", data["attachFiles"]?.ToString() ?? "true");
                // ── นโยบาย VAT มัดจำ = ค่าที่ "หายเงียบ" มาแล้วสองรอบ ─────────────────────
                // หน้าเว็บส่งสองคีย์นี้มา "ทุกครั้งที่กดบันทึก" ตามสถานะ dropdown/checkbox บนจอ
                // ถ้าตอนโหลดหน้า AJAX พลาด ค่าบนจอจะเป็น default (CHECKOUT/ไม่ติ๊ก) แล้วการบันทึก
                // เพื่อแก้เรื่องอื่นจะรีเซ็ตนโยบายทิ้งโดยไม่มีใครรู้
                // ⇒ เขียนเฉพาะเมื่อ "เปลี่ยนจริง" + log เก่า→ใหม่+ใคร ทุกครั้ง (Updated_Date ของแถว
                //   จะกลายเป็นหลักฐานว่าโดนแก้เมื่อไหร่จริง ๆ ไม่ใช่แค่โดนเซฟทับด้วยค่าเดิม)
                if (data.ContainsKey("depositVatRecognition"))
                {
                    string dvr = (data["depositVatRecognition"]?.ToString() ?? "CHECKOUT").ToUpper();
                    if (dvr != "RECEIPT" && dvr != "CHECKOUT") dvr = "CHECKOUT";
                    string curDvr = config.DepositVatRecognition;
                    if (!string.Equals(curDvr, dvr, StringComparison.OrdinalIgnoreCase))
                    {
                        _code.Logs(ConnStr, "AccountingConfig",
                            $"⚠ นโยบาย VAT มัดจำถูกเปลี่ยน: Deposit_Vat_Recognition {curDvr} → {dvr} " +
                            $"โดย {Session["UserName"] ?? "?"} — กระทบใบมัดจำทุกใบหลังจากนี้",
                            Session["UserName"]?.ToString() ?? "SYSTEM");
                        config.SetConfig("Deposit_Vat_Recognition", dvr);
                    }
                }
                if (data.ContainsKey("depositDeferOutputVat"))
                {
                    string newDefer = BoolToFlag(data["depositDeferOutputVat"]);
                    string curDefer = config.IsDepositOutputVatDeferred ? "1" : "0";
                    if (curDefer != newDefer)
                    {
                        _code.Logs(ConnStr, "AccountingConfig",
                            $"⚠ นโยบาย VAT มัดจำถูกเปลี่ยน: Deposit_Defer_Output_Vat {curDefer} → {newDefer} " +
                            $"โดย {Session["UserName"] ?? "?"} — {(newDefer == "1" ? "พัก VAT ที่ 21913" : "เลิกพัก VAT (เข้า 21911/ไม่แยก)")}",
                            Session["UserName"]?.ToString() ?? "SYSTEM");
                        config.SetConfig("Deposit_Defer_Output_Vat", newDefer);
                    }
                }
                // ⚠ เปิดได้เมื่อ NextAcc deploy รองรับ depositAppliedDrivesJournal แล้วเท่านั้น (spec §9.1) —
                //   เปิด flag = ส่ง drives=true + เลิกส่ง JV แยกพร้อมกัน; เปิดก่อน NextAcc พร้อม = GL พัง
                if (data.ContainsKey("depositDrivesJournal")) config.SetConfig("Nexaacc_Deposit_Drives_Journal", BoolToFlag(data["depositDrivesJournal"]));
                // ⚠ เปิดได้เมื่อ NextAcc deploy cb55e3b แล้วเท่านั้น (มัดจำ JV-INT → self-contained JE)
                if (data.ContainsKey("depositDrivesJournalRef")) config.SetConfig("Nexaacc_Drives_Journal_Ref", BoolToFlag(data["depositDrivesJournalRef"]));
                if (data.ContainsKey("autoRecoverDeposit")) config.SetConfig("Nexaacc_Auto_Recover_Deposit", BoolToFlag(data["autoRecoverDeposit"]));
                if (data.ContainsKey("postSyncVerify")) config.SetConfig("Nexaacc_Post_Sync_Verify", BoolToFlag(data["postSyncVerify"]));
                if (data.ContainsKey("autoReconcileDeposit")) config.SetConfig("Nexaacc_Auto_Reconcile_Deposit", BoolToFlag(data["autoReconcileDeposit"]));
                // toggle ทดลอง isCashSale (TaxReceipt_SingleDoc / CashSale_Deposit / NativeA) เอา UI ออกแล้ว
                // (2 ตัวแรกไม่มีผลต่อโค้ด; การหักมัดจำใช้ drives ผ่านค่าแนะนำ). preset ตั้งค่าให้ = 0
                if (data.ContainsKey("cashSaleUseReceipt")) config.SetConfig("Nexaacc_CashSale_UseReceipt", BoolToFlag(data["cashSaleUseReceipt"]));
                if (data.ContainsKey("cashSaleCompanyDoc")) config.SetConfig("Nexaacc_CashSale_CompanyDoc", BoolToFlag(data["cashSaleCompanyDoc"]));
                // หัวเอกสารขาย = นโยบายเอกสารภาษี → เขียนเฉพาะเมื่อเปลี่ยนจริง + log เก่า→ใหม่ (แนวเดียวกับ VAT มัดจำ)
                if (data.ContainsKey("receiptHeaderType"))
                {
                    string rht = (data["receiptHeaderType"]?.ToString() ?? "AUTO").Trim().ToUpperInvariant();
                    if (rht != "ABBREVIATED") rht = "AUTO";
                    string curRht = config.ReceiptHeaderType;
                    if (!string.Equals(curRht, rht, StringComparison.OrdinalIgnoreCase))
                    {
                        _code.Logs(ConnStr, "AccountingConfig",
                            $"หัวเอกสารขายถูกเปลี่ยน: Nexaacc_Receipt_Header_Type {curRht} → {rht} " +
                            $"โดย {Session["UserName"] ?? "?"} — มีผลกับเอกสารที่ sync หลังจากนี้ (ใบเดิมไม่เปลี่ยน)",
                            Session["UserName"]?.ToString() ?? "SYSTEM");
                        config.SetConfig("Nexaacc_Receipt_Header_Type", rht);
                    }
                }
                if (data.ContainsKey("etaxAutoGenerate")) config.SetConfig("Etax_AutoGenerate", BoolToFlag(data["etaxAutoGenerate"]));
                if (data.ContainsKey("etaxAutoSign")) config.SetConfig("Etax_AutoSign", BoolToFlag(data["etaxAutoSign"]));
                if (data.ContainsKey("etaxAutoSubmit")) config.SetConfig("Etax_AutoSubmit", BoolToFlag(data["etaxAutoSubmit"]));
                if (data.ContainsKey("etaxAutoSendEmail")) config.SetConfig("Etax_AutoSendEmail", BoolToFlag(data["etaxAutoSendEmail"]));
                if (data.ContainsKey("etaxEmailSubject")) config.SetConfig("Etax_EmailSubject", data["etaxEmailSubject"]?.ToString() ?? "");
                if (data.ContainsKey("etaxEmailCc")) config.SetConfig("Etax_EmailCc", data["etaxEmailCc"]?.ToString() ?? "");
                if (data.ContainsKey("etaxEmailBody")) config.SetConfig("Etax_EmailBody", data["etaxEmailBody"]?.ToString() ?? "");
                if (data.ContainsKey("etaxEmailAttachPdf")) config.SetConfig("Etax_EmailAttachPdf", data["etaxEmailAttachPdf"]?.ToString() ?? "true");
                if (data.ContainsKey("etaxEmailAttachXml")) config.SetConfig("Etax_EmailAttachXml", data["etaxEmailAttachXml"]?.ToString() ?? "false");
                if (data.ContainsKey("etaxEmailLocalOnly")) config.SetConfig("Etax_EmailLocalOnly", data["etaxEmailLocalOnly"]?.ToString() ?? "false");
                if (data.ContainsKey("etaxEmailFallback")) config.SetConfig("Etax_EmailFallback", data["etaxEmailFallback"]?.ToString() ?? "true");
                if (data.ContainsKey("syncInterval")) config.SetConfig("Nexaacc_SyncInterval_Sec", data["syncInterval"]?.ToString() ?? "30");
                if (data.ContainsKey("maxRetries")) config.SetConfig("Nexaacc_MaxRetries", data["maxRetries"]?.ToString() ?? "5");
                if (data.ContainsKey("timeout")) config.SetConfig("Nexaacc_TimeoutSec", data["timeout"]?.ToString() ?? "30");

                return new Dictionary<string, object> { { "success", true }, { "message", "บันทึก Sync Settings สำเร็จ" } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> TestApiLogin()
        {
            try
            {
                // ทดสอบเอง = ต้องยิงจริงเสมอ ไม่ใช่ตอบจากสถานะ "พักเพราะ NextAcc ล่ม" ที่ค้างอยู่
                Integration.AccountingApiClient.ClearServerDown();

                var config = new Integration.AccountingConfig(ConnStr);
                var client = new Integration.AccountingApiClient(config, ConnStr);

                // (1) Integration surface (int_) — /api/integration/contacts ผ่าน X-Integration-Key
                var intResult = System.Threading.Tasks.Task.Run(() => client.TestConnectionAsync()).Result;
                string intLine = (intResult.Success ? "✓" : "✗") + " Integration (int_): " + intResult.Message;

                // (2) Company surface — /api/companies/{id}/accounting/accounts ผ่าน X-Api-Key
                //     (acc_ ถ้าตั้งแยก, ไม่งั้น int_ ผ่าน fallback). ข้ามถ้า company endpoints ปิด.
                bool companyOk = true;
                string companyLine;
                if (!config.CanUseCompanyEndpoints)
                {
                    companyLine = "ℹ Company (/api/companies/*): ข้าม — ปิดอยู่ (ตั้ง Company ID + Nexaacc_Company_Endpoints=1)";
                }
                else
                {
                    string keyKind = config.HasDedicatedCompanyKey ? "acc_ แยก" : "int_ ผ่าน fallback";
                    try
                    {
                        var acc = System.Threading.Tasks.Task.Run(() => client.GetAccountsAsync()).Result;
                        companyOk = acc != null && acc.data != null;
                        companyLine = (companyOk ? "✓" : "✗") + $" Company ({keyKind}): " +
                            (companyOk ? $"เชื่อมต่อสำเร็จ (ผังบัญชี {acc.data.Count} รายการ)"
                                       : "เรียก /accounting/accounts ไม่สำเร็จ");
                    }
                    catch (AggregateException caex)
                    {
                        companyOk = false;
                        companyLine = $"✗ Company ({keyKind}): {(caex.InnerException ?? caex).Message}";
                    }
                    catch (Exception cex)
                    {
                        companyOk = false;
                        companyLine = $"✗ Company ({keyKind}): {cex.Message}";
                    }
                }

                bool overall = intResult.Success && companyOk;
                return new Dictionary<string, object>
                {
                    { "success", overall },
                    { "message", intLine + "\n" + companyLine }
                };
            }
            catch (AggregateException aex)
            {
                var inner = aex.InnerException ?? aex;
                return new Dictionary<string, object> { { "success", false }, { "message", inner.Message } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> FetchChartOfAccounts()
        {
            // Delegate to SyncChartOfAccounts (same functionality, now with cache)
            return SyncChartOfAccounts();
        }

        private Dictionary<string, object> ReconcileDeletedDocuments()
        {
            try
            {
                var sync = new Integration.AccountingSyncService(ConnStr);
                var r = System.Threading.Tasks.Task.Run(() => sync.ReconcileDeletedDocumentsAsync(500)).Result;
                string msg = $"ตรวจ {r.Checked} ใบ — ลบ {r.Deleted} ใบ (หายจาก NextAcc 404), ข้าม {r.Skipped}, ตรวจไม่ได้ {r.Errors} (ไม่ลบ)";
                if (r.DeletedDocs != null && r.DeletedDocs.Count > 0)
                    msg += "\nลบ: " + string.Join(", ", r.DeletedDocs);
                return new Dictionary<string, object> { { "success", true }, { "message", msg } };
            }
            catch (AggregateException aex)
            {
                var inner = aex.InnerException ?? aex;
                return new Dictionary<string, object> { { "success", false }, { "message", inner.Message } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> ProcessQueueNow()
        {
            try
            {
                // ผู้ใช้กดเอง = ตั้งใจลองใหม่ → ล้างสถานะ "พักเพราะ NextAcc ล่ม" ให้ยิงจริง
                Integration.AccountingApiClient.ClearServerDown();

                var sync = new Integration.AccountingSyncService(ConnStr);
                int processed = System.Threading.Tasks.Task.Run(() => sync.ProcessQueueAsync(50)).Result;

                // "0 รายการ" กำกวมมาก — แยกให้ชัดว่าคิวว่างจริง หรือรอบนี้ถูกข้าม
                if (processed == 0 && !string.IsNullOrEmpty(sync.LastRunSkippedReason))
                    return new Dictionary<string, object>
                    {
                        { "success", false },
                        { "message", "ยังไม่ได้ประมวลผล: " + sync.LastRunSkippedReason }
                    };

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", processed > 0 ? $"ประมวลผลสำเร็จ {processed} รายการ" : "ไม่มีรายการรอประมวลผล" }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "Process Error: " + ex.Message } };
            }
        }

        // ── Email reservation intake (STAAH) ─────────────────────────────────────
        private Dictionary<string, object> SaveEmailIntakeConfig(Dictionary<string, object> data)
        {
            try
            {
                var config = new Integration.AccountingConfig(ConnStr);
                if (data.ContainsKey("emailRsvEnabled")) config.SetConfig("Email_Rsv_Enabled", BoolToFlag(data["emailRsvEnabled"]));
                if (data.ContainsKey("emailRsvImapServer")) config.SetConfig("Email_Rsv_ImapServer", data["emailRsvImapServer"]?.ToString() ?? "imap.gmail.com");
                if (data.ContainsKey("emailRsvImapPort")) config.SetConfig("Email_Rsv_ImapPort", data["emailRsvImapPort"]?.ToString() ?? "993");
                if (data.ContainsKey("emailRsvUsername")) config.SetConfig("Email_Rsv_Username", data["emailRsvUsername"]?.ToString() ?? "");
                // รหัสผ่าน: บันทึกเฉพาะเมื่อมีการกรอกใหม่ (ไม่ทับด้วยค่าว่าง/mask)
                if (data.ContainsKey("emailRsvPassword"))
                {
                    string pw = data["emailRsvPassword"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(pw)) config.SetConfig("Email_Rsv_Password_Encrypted", _code.Crypt(pw));
                }
                if (data.ContainsKey("emailRsvPollMinutes")) config.SetConfig("Email_Rsv_PollMinutes", data["emailRsvPollMinutes"]?.ToString() ?? "5");
                if (data.ContainsKey("emailRsvProcessedLabel")) config.SetConfig("Email_Rsv_ProcessedLabel", data["emailRsvProcessedLabel"]?.ToString() ?? "STAAH-Processed");
                if (data.ContainsKey("emailRsvFailedLabel")) config.SetConfig("Email_Rsv_FailedLabel", data["emailRsvFailedLabel"]?.ToString() ?? "STAAH-Failed");
                if (data.ContainsKey("emailRsvIgnoredLabel")) config.SetConfig("Email_Rsv_IgnoredLabel", data["emailRsvIgnoredLabel"]?.ToString() ?? "STAAH-Other");
                if (data.ContainsKey("emailRsvDefaultCollect")) config.SetConfig("Email_Rsv_DefaultCollect", data["emailRsvDefaultCollect"]?.ToString() ?? "CHANNEL");
                if (data.ContainsKey("emailRsvMaxStayDays")) config.SetConfig("Email_Rsv_MaxStayDays", data["emailRsvMaxStayDays"]?.ToString() ?? "30");
                if (data.ContainsKey("emailRsvMaxDaysFuture")) config.SetConfig("Email_Rsv_MaxDaysFuture", data["emailRsvMaxDaysFuture"]?.ToString() ?? "365");
                if (data.ContainsKey("emailRsvNotifyTelegram")) config.SetConfig("Email_Rsv_NotifyTelegram", BoolToFlag(data["emailRsvNotifyTelegram"]));
                if (data.ContainsKey("emailRsvCreateDocument")) config.SetConfig("Email_Rsv_CreateDocument", BoolToFlag(data["emailRsvCreateDocument"]));
                if (data.ContainsKey("emailRsvMoveFailed")) config.SetConfig("Email_Rsv_MoveFailed", BoolToFlag(data["emailRsvMoveFailed"]));
                if (data.ContainsKey("emailRsvFromContains")) config.SetConfig("Email_Rsv_FromContains", data["emailRsvFromContains"]?.ToString() ?? "staah");
                if (data.ContainsKey("emailRsvRetryFailed")) config.SetConfig("Email_Rsv_RetryFailed", BoolToFlag(data["emailRsvRetryFailed"]));
                if (data.ContainsKey("emailRsvRetryHours")) config.SetConfig("Email_Rsv_RetryHours", data["emailRsvRetryHours"]?.ToString() ?? "72");
                if (data.ContainsKey("emailRsvMapAnyChannel")) config.SetConfig("Email_Rsv_MapAnyChannel", BoolToFlag(data["emailRsvMapAnyChannel"]));
                if (data.ContainsKey("emailRsvRoomPriority")) config.SetConfig("Email_Rsv_RoomPriority", data["emailRsvRoomPriority"]?.ToString() ?? "");
                if (data.ContainsKey("emailRsvDefaultPhone")) config.SetConfig("Email_Rsv_DefaultPhone", data["emailRsvDefaultPhone"]?.ToString() ?? "");
                if (data.ContainsKey("emailRsvCancelStatus")) config.SetConfig("Email_Rsv_CancelStatus", data["emailRsvCancelStatus"]?.ToString() ?? "ยกเลิก");

                return new Dictionary<string, object> { { "success", true }, { "message", "บันทึกการตั้งค่าอ่านอีเมลจองแล้ว" } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "Save Error: " + ex.Message } };
            }
        }

        private Dictionary<string, object> RunEmailIntakeNow()
        {
            try
            {
                var svc = new EmailReservationService(ConnStr);
                var r = System.Threading.Tasks.Task.Run(() => svc.ProcessEmails()).Result;
                if (r.Error != null)
                    return new Dictionary<string, object> { { "success", false }, { "message", r.Error } };
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", r.ToString() },
                    { "detail", string.Join("\n", r.Messages) }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "Run Error: " + (ex.InnerException ?? ex).Message } };
            }
        }

        /// <summary>
        /// ♻️ กู้อีเมลจองที่ตกหล่น — กวาดทุก folder ในกล่องเมลย้อนหลัง N วัน แล้วลงจองที่ยังไม่มี
        /// ใช้เมื่อระบบเคยหยุดอ่าน (ล็อกค้าง/เซิร์ฟเวอร์ล่ม) หรือสงสัยว่ามีตัวอ่านอื่นแย่งอ่านไป
        /// ปลอดภัย: dedup ด้วย Booking ID — ใบที่ลงแล้วนับเป็น "ซ้ำ" ไม่สร้างซ้อน
        /// </summary>
        private Dictionary<string, object> RecoverEmailBacklog()
        {
            try
            {
                int days;
                if (!int.TryParse(Request.QueryString["days"], out days) || days <= 0) days = 7;
                var svc = new EmailReservationService(ConnStr);
                var r = System.Threading.Tasks.Task.Run(() => svc.RecoverBacklog(days)).Result;
                if (r.Error != null)
                    return new Dictionary<string, object> { { "success", false }, { "message", r.Error } };
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", $"กวาดย้อนหลัง {days} วัน → สร้างเพิ่ม {r.Created}, ยกเลิก {r.Cancelled}, "
                                 + $"มีอยู่แล้ว {r.Duplicate}, ต้องตรวจเอง {r.Manual}, ไม่ใช่ใบจอง {r.Ignored}" },
                    { "detail", string.Join("\n", r.Messages) }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "Recover Error: " + (ex.InnerException ?? ex).Message } };
            }
        }

        /// <summary>
        /// ตอบคำถาม "ทำไมหน้าจอบอกห้องว่าง แต่อีเมล OTA ลงจองไม่ได้" — จำลองการ map + เช็คห้องว่าง
        /// ด้วยเงื่อนไขเดียวกับตัวอ่านอีเมล แล้วบอกว่าติดห้องไหน/ติดใบจองไหน (ไม่บันทึกอะไร)
        /// </summary>
        private Dictionary<string, object> DiagnoseEmailIntake()
        {
            try
            {
                string channel = Request.QueryString["channel"] ?? "";
                string roomType = Request.QueryString["roomType"] ?? "";
                if (string.IsNullOrWhiteSpace(roomType))
                    return new Dictionary<string, object> { { "success", false }, { "message", "ระบุชื่อห้อง (ROOM TYPE ในอีเมล) ก่อน" } };

                DateTime ci, co;
                if (!DateTime.TryParse(Request.QueryString["checkin"], out ci)) ci = DateTime.Today;
                if (!DateTime.TryParse(Request.QueryString["checkout"], out co)) co = ci.AddDays(1);
                int rooms, adults;
                if (!int.TryParse(Request.QueryString["rooms"], out rooms) || rooms <= 0) rooms = 1;
                if (!int.TryParse(Request.QueryString["adults"], out adults) || adults <= 0) adults = 1;

                var svc = new EmailReservationService(ConnStr);
                string report = svc.Diagnose(channel, roomType, ci, co, rooms, adults);
                return new Dictionary<string, object>
                {
                    { "success", !report.StartsWith("❌") },
                    { "message", report }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "Diagnose Error: " + (ex.InnerException ?? ex).Message } };
            }
        }

        /// <summary>ดูว่า parser แยกอะไรออกมาได้บ้างจากอีเมลจริง (read-only ไม่กระทบคิว)</summary>
        private Dictionary<string, object> PreviewEmailIntake()
        {
            try
            {
                int n;
                if (!int.TryParse(Request.QueryString["count"], out n)) n = 3;
                var svc = new EmailReservationService(ConnStr);
                string report = System.Threading.Tasks.Task.Run(() => svc.PreviewLatest(n)).Result;
                return new Dictionary<string, object> { { "success", true }, { "message", report } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "Preview Error: " + (ex.InnerException ?? ex).Message } };
            }
        }

        private Dictionary<string, object> TestEmailIntakeTelegram()
        {
            try
            {
                var svc = new EmailReservationService(ConnStr);
                var (ok, msg) = System.Threading.Tasks.Task.Run(() => svc.TestTelegram()).Result;
                return new Dictionary<string, object> { { "success", ok }, { "message", msg } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "Telegram Test Error: " + (ex.InnerException ?? ex).Message } };
            }
        }

        private Dictionary<string, object> TestEmailIntakeConnection()
        {
            try
            {
                var svc = new EmailReservationService(ConnStr);
                var (ok, msg) = System.Threading.Tasks.Task.Run(() => svc.TestConnection()).Result;
                return new Dictionary<string, object> { { "success", ok }, { "message", msg } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "Test Error: " + (ex.InnerException ?? ex).Message } };
            }
        }

        private Dictionary<string, object> GetEmailIntakeLog()
        {
            try
            {
                int limit = 100;
                int.TryParse(Request.QueryString["limit"], out limit);
                if (limit <= 0 || limit > 500) limit = 100;

                var dt = _code.DatabaseQuerySafe(ConnStr,
                    $"SELECT TOP {limit} LogDateTime, LogDetail, LogBy FROM Logs WHERE LogAction = 'EmailReservation' ORDER BY LogDateTime DESC",
                    null);

                var items = new List<Dictionary<string, object>>();
                if (dt != null)
                    foreach (DataRow r in dt.Rows)
                        items.Add(new Dictionary<string, object>
                        {
                            { "time", r["LogDateTime"] == DBNull.Value ? "" : Convert.ToDateTime(r["LogDateTime"]).ToString("yyyy-MM-dd HH:mm:ss") },
                            { "detail", r["LogDetail"]?.ToString() ?? "" },
                            { "by", r["LogBy"]?.ToString() ?? "" }
                        });

                return new Dictionary<string, object> { { "success", true }, { "items", items }, { "total", items.Count } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "Log Error: " + ex.Message } };
            }
        }

        // ── Daily reservation board → LINE ───────────────────────────────────────
        private Dictionary<string, object> SaveLineDailyConfig(Dictionary<string, object> data)
        {
            try
            {
                var config = new Integration.AccountingConfig(ConnStr);
                if (data.ContainsKey("lineDailyEnabled")) config.SetConfig("Line_DailyReport_Enabled", BoolToFlag(data["lineDailyEnabled"]));
                if (data.ContainsKey("lineDailyRecipients")) config.SetConfig("Line_DailyReport_Recipients", data["lineDailyRecipients"]?.ToString() ?? "");
                if (data.ContainsKey("lineDailySendTime")) config.SetConfig("Line_DailyReport_SendTime", data["lineDailySendTime"]?.ToString() ?? "08:00");
                if (data.ContainsKey("lineDailySourceUrl")) config.SetConfig("Line_DailyReport_SourceUrl", data["lineDailySourceUrl"]?.ToString() ?? "");
                if (data.ContainsKey("lineDailyImageWidth")) config.SetConfig("Line_DailyReport_ImageWidth", data["lineDailyImageWidth"]?.ToString() ?? "1600");
                if (data.ContainsKey("lineDailyImageHeight")) config.SetConfig("Line_DailyReport_ImageHeight", data["lineDailyImageHeight"]?.ToString() ?? "700");
                if (data.ContainsKey("lineDailyAutoHeight")) config.SetConfig("Line_DailyReport_AutoHeight", BoolToFlag(data["lineDailyAutoHeight"]));
                if (data.ContainsKey("lineDailyCaption")) config.SetConfig("Line_DailyReport_Caption", data["lineDailyCaption"]?.ToString() ?? "");
                if (data.ContainsKey("lineDailyPublicBaseUrl")) config.SetConfig("Line_DailyReport_PublicBaseUrl", data["lineDailyPublicBaseUrl"]?.ToString() ?? "");
                if (data.ContainsKey("lineDailyImageFolder")) config.SetConfig("Line_DailyReport_ImageFolder", data["lineDailyImageFolder"]?.ToString() ?? "~/Images/Reservation");
                if (data.ContainsKey("lineDailyJpegQuality")) config.SetConfig("Line_DailyReport_JpegQuality", data["lineDailyJpegQuality"]?.ToString() ?? "90");
                if (data.ContainsKey("lineDailyFontScale")) config.SetConfig("Line_DailyReport_FontScale", data["lineDailyFontScale"]?.ToString() ?? "100");
                // token override: บันทึกเฉพาะเมื่อกรอกใหม่ ("-" = ล้าง)
                if (data.ContainsKey("lineDailyTokenOverride"))
                {
                    string tk = data["lineDailyTokenOverride"]?.ToString() ?? "";
                    if (tk == "-") config.SetConfig("Line_DailyReport_TokenOverride_Encrypted", "");
                    else if (!string.IsNullOrEmpty(tk)) config.SetConfig("Line_DailyReport_TokenOverride_Encrypted", _code.Crypt(tk));
                }
                return new Dictionary<string, object> { { "success", true }, { "message", "บันทึกการตั้งค่าส่งรายงาน LINE แล้ว" } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "Save Error: " + ex.Message } };
            }
        }

        private Dictionary<string, object> SendDailyLineNow()
        {
            try
            {
                var s = new DailyReportLineService(ConnStr);
                var r = System.Threading.Tasks.Task.Run(() => s.SendNow(true)).Result;
                if (!r.Success && r.Error != null)
                    return new Dictionary<string, object> { { "success", false }, { "message", r.Error }, { "detail", string.Join("\n", r.Messages) } };
                return new Dictionary<string, object>
                {
                    { "success", r.Success },
                    { "message", r.ToString() },
                    { "detail", string.Join("\n", r.Messages) },
                    { "imageUrl", r.ImageUrl }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "Send Error: " + (ex.InnerException ?? ex).Message } };
            }
        }

        private Dictionary<string, object> PreviewDailyLine()
        {
            try
            {
                var s = new DailyReportLineService(ConnStr);
                var (ok, urlOrErr, _) = System.Threading.Tasks.Task.Run(() => s.GeneratePreview()).Result;
                return ok
                    ? new Dictionary<string, object> { { "success", true }, { "message", "สร้างรูปพรีวิวแล้ว" }, { "imageUrl", urlOrErr } }
                    : new Dictionary<string, object> { { "success", false }, { "message", urlOrErr } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "Preview Error: " + (ex.InnerException ?? ex).Message } };
            }
        }

        private Dictionary<string, object> TestDailyLine()
        {
            try
            {
                var s = new DailyReportLineService(ConnStr);
                var (ok, msg) = System.Threading.Tasks.Task.Run(() => s.SendTestText()).Result;
                return new Dictionary<string, object> { { "success", ok }, { "message", msg } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "Test Error: " + (ex.InnerException ?? ex).Message } };
            }
        }

        private Dictionary<string, object> GetDailyLineLog()
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(ConnStr,
                    "SELECT TOP 50 LogDateTime, LogDetail FROM Logs WHERE LogAction = 'DailyLineReport' ORDER BY LogDateTime DESC", null);
                var items = new List<Dictionary<string, object>>();
                if (dt != null)
                    foreach (DataRow r in dt.Rows)
                        items.Add(new Dictionary<string, object>
                        {
                            { "time", r["LogDateTime"] == DBNull.Value ? "" : Convert.ToDateTime(r["LogDateTime"]).ToString("yyyy-MM-dd HH:mm:ss") },
                            { "detail", r["LogDetail"]?.ToString() ?? "" }
                        });
                return new Dictionary<string, object> { { "success", true }, { "items", items } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "Log Error: " + ex.Message } };
            }
        }

        // เก็บกวาดใบเสร็จหลักฐานรับเงิน (settlement receipt) ที่ orphan บน NextAcc
        // (parent ใบกำกับถูกลบ/void) — soft-delete, ไม่กระทบ GL
        private Dictionary<string, object> CleanupOrphanReceipts()
        {
            try
            {
                var sync = new Integration.AccountingSyncService(ConnStr);
                var (deleted, message) = sync.SweepOrphanSettlementReceipts();
                return new Dictionary<string, object>
                {
                    { "success", deleted >= 0 },
                    { "message", message }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "Cleanup Error: " + ex.Message } };
            }
        }

        // กลับ JV มัดจำที่ TakeTime post เอง ซึ่งค้างเป็นซาก GL จาก churn (215xx/217xx/21913)
        private Dictionary<string, object> CleanupDepositDebris()
        {
            try
            {
                var sync = new Integration.AccountingSyncService(ConnStr);
                var (reversed, message) = sync.CleanupDepositGlDebrisJvs();
                return new Dictionary<string, object>
                {
                    { "success", reversed >= 0 },
                    { "message", message }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "Cleanup Error: " + ex.Message } };
            }
        }

        // 🧹 รีเซ็ตบัญชี "การจองเดียว" ทั้งหมดบน NextAcc (กดทีเดียวจบ) — churn หนักเกิน cleanup ปกติ
        private Dictionary<string, object> ResetReservationAccounting()
        {
            try
            {
                int resId;
                if (!int.TryParse((Request.QueryString["resId"] ?? "").Trim(), out resId) || resId <= 0)
                    return new Dictionary<string, object> { { "success", false }, { "message", "กรอกรหัสการจอง (Reservation ID) ให้ถูกต้อง" } };

                var sync = new Integration.AccountingSyncService(ConnStr);
                var (reversed, message) = sync.ResetReservationAccounting(resId);
                return new Dictionary<string, object>
                {
                    { "success", reversed >= 0 },
                    { "message", message }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "Reset Error: " + ex.Message } };
            }
        }

        // ⭐ ตั้งค่าแนะนำ (production) — เส้นทางที่ verified แล้ว: company /document + drives เดี่ยว
        //    ปิด flag ทดลอง (isCashSale single-doc / cash-sale deposit) ที่ทำให้เอกสารมั่ว/หักมัดจำไม่เข้าใบ
        //    เปิด safety-net (auto-recover / auto-reconcile / post-sync verify)
        private Dictionary<string, object> ApplyRecommendedPreset()
        {
            try
            {
                var cfg = new Integration.AccountingConfig(ConnStr);
                // เอกสารรับ/จ่าย = DOCUMENT (ออกเอกสารจริงบน NextAcc)
                cfg.SetConfig("Nexaacc_SyncMode_Receipt", "DOCUMENT");
                cfg.SetConfig("Nexaacc_SyncMode_Voucher", "DOCUMENT");
                cfg.SetConfig("Nexaacc_Company_Endpoints", "1");
                // ⚠️ แก้ความเข้าใจผิดเดิม (ส.ค. 2026) — ตรวจกับ Wachira-d/Accounting @HEAD แล้ว:
                //    หัวเอกสารคำนวณที่ `PdfGenerationService.ComputeDocumentTitle` จาก **flag บน Document**
                //    ไม่ใช่จาก DocumentType: ขายสด (IssuedAsCashReceipt) → "ใบกำกับภาษี/ใบเสร็จรับเงิน",
                //    BuyerDeclinedTaxInvoice → "ใบเสร็จรับเงิน" (ไม่ upgrade).
                //    ⇒ **Receipt(3) หัวเป็น "ใบเสร็จรับเงิน" เสมอ** ต่อให้ผู้ซื้อมีเลขภาษีครบ
                //    เหตุที่เคยคิดว่า UseReceipt=0 ทำหัวผิด คือตอนนั้นผู้ซื้อยังเป็น "คนจอง" (ไม่มีเลขภาษี)
                //    → โดน MarkBuyerDeclinedTaxInvoice → หัว downgrade เอง ไม่ใช่ความผิดของเส้น isCashSale
                //    (ต้นเหตุจริงแก้แล้ว: ผู้ซื้ออ่านจาก Account_Receipt.Customer_ID)
                //    ⇒ ตั้ง 0 เพื่อไปเส้น isCashSale integration invoice = ได้ "ใบกำกับภาษี/ใบเสร็จรับเงิน" + e-Tax
                cfg.SetConfig("Nexaacc_CashSale_UseReceipt", "0");
                // หักมัดจำ = drives (JE เดียว self-contained, Dr แหล่งเงินสุทธิ + Dr 21510) — เส้น verified
                cfg.SetConfig("Nexaacc_Deposit_Drives_Journal", "1");
                cfg.SetConfig("Nexaacc_Drives_Journal_Ref", "1");
                // ปิดเส้นทดลอง (toggle ที่ไม่ได้ใช้จริง/isCashSale) — เคลียร์ความสับสน
                cfg.SetConfig("Nexaacc_TaxReceipt_SingleDoc", "0");   // ⚠ dead toggle (ไม่มีโค้ดใช้) — ตั้ง 0 กันสับสน
                cfg.SetConfig("Nexaacc_CashSale_Deposit", "0");        // ⚠ dead toggle
                cfg.SetConfig("Nexaacc_CashSale_Deposit_NativeA", "0");
                // ── มัดจำ VAT = "นโยบายบัญชี" ไม่ใช่ toggle เทคนิค ────────────────────────
                // ⚠ ห้ามเขียนทับค่าที่ผู้ทำบัญชีตั้งไว้ (เคยเกิดจริง: กดปุ่มนี้เพื่อแก้เรื่องอื่น
                //   แล้ว Deposit_Defer_Output_Vat ถูกรีเซ็ตเป็น 0 เงียบ ๆ → VAT มัดจำเลิกเข้า 21913)
                //   ตั้งให้เฉพาะกรณี "ยังไม่เคยตั้ง" เท่านั้น
                string keptVatPolicy = null;
                if (!HasConfigValue("Deposit_Vat_Recognition")) cfg.SetConfig("Deposit_Vat_Recognition", "CHECKOUT");
                else keptVatPolicy = cfg.DepositVatRecognition;
                if (!HasConfigValue("Deposit_Defer_Output_Vat")) cfg.SetConfig("Deposit_Defer_Output_Vat", "0");
                else keptVatPolicy = (keptVatPolicy ?? cfg.DepositVatRecognition)
                    + (cfg.IsDepositOutputVatDeferred ? " + พัก VAT ที่ 21913" : " + ไม่พัก VAT");
                // safety-net: กัน GL เพี้ยน/มัดจำค้าง อัตโนมัติ
                cfg.SetConfig("Nexaacc_Auto_Recover_Deposit", "1");
                cfg.SetConfig("Nexaacc_Auto_Reconcile_Deposit", "1");
                cfg.SetConfig("Nexaacc_Post_Sync_Verify", "1");

                _code.Logs(ConnStr, "AccountingConfig",
                    "ApplyRecommendedPreset: ตั้งค่าแนะนำ production (DOCUMENT + drives, ปิด isCashSale, เปิด safety-net)", "SYSTEM");
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "message",
                        "✅ ตั้งค่าแนะนำเรียบร้อย — เอกสารรับ B2B = company Receipt(3) หัว 'ใบกำกับภาษี/ใบเสร็จรับเงิน' + หักมัดจำในใบ + e-Tax T03 " +
                        "(แก้ต้นเหตุที่เดิมไปเส้น isCashSale → หัวขึ้น 'ใบเสร็จรับเงิน' + มัดจำไม่หักในใบ 6,400 เต็ม), " +
                        "หักมัดจำ = drives (JE เดียว Dr แหล่งเงินสุทธิ + Dr 21510), เปิด safety-net. " +
                        (keptVatPolicy != null
                            ? $"🔒 คงนโยบาย VAT มัดจำเดิมไว้ ({keptVatPolicy}) — ปุ่มนี้ไม่แตะค่าที่ตั้งไว้แล้ว. "
                            : "มัดจำ VAT = CHECKOUT (ค่าเริ่มต้น). ") +
                        "โหลดหน้าใหม่เพื่อดูค่าที่อัปเดต แล้ว rebuild+deploy บน Windows" }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "ตั้งค่าแนะนำไม่สำเร็จ: " + ex.Message } };
            }
        }

        // 🔎 GR/IR reconcile: ยอดคงค้าง GRNI + รายการรับของ
        private Dictionary<string, object> GrniReconcile()
        {
            try
            {
                var sync = new Integration.AccountingSyncService(ConnStr);
                var r = sync.GetGrniReconcile();
                return new Dictionary<string, object>
                {
                    { "success", r.Success },
                    { "message", r.Message },
                    { "accountCode", r.AccountCode },
                    { "hasBalance", r.HasBalance },
                    { "debitBalance", r.DebitBalance },
                    { "creditBalance", r.CreditBalance },
                    { "netOpen", r.NetOpen },
                    { "interpretation", r.Interpretation },
                    { "items", r.StockInItems }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "GRNI Reconcile Error: " + ex.Message } };
            }
        }

        private Dictionary<string, object> GetQueueData()
        {
            try
            {
                // Parse pagination & filter params
                int page = 1, pageSize = 20;
                int.TryParse(Request.QueryString["page"] ?? "1", out page);
                int.TryParse(Request.QueryString["pageSize"] ?? "20", out pageSize);
                string statusFilter = Request.QueryString["status"] ?? "";
                if (page < 1) page = 1;
                if (pageSize < 5) pageSize = 5;
                if (pageSize > 100) pageSize = 100;

                // Get summary counts (all time)
                DataTable summary = _code.DatabaseQuerySafe(ConnStr,
                    @"SELECT Status, COUNT(*) as Cnt
                      FROM Accounting_Sync_Queue
                      GROUP BY Status", null);

                int pending = 0, processing = 0, completed = 0, failed = 0;
                if (summary != null)
                {
                    foreach (DataRow row in summary.Rows)
                    {
                        string s = row["Status"]?.ToString() ?? "";
                        int cnt = Convert.ToInt32(row["Cnt"]);
                        switch (s)
                        {
                            case "PENDING": pending = cnt; break;
                            case "PROCESSING": processing = cnt; break;
                            case "COMPLETED": completed = cnt; break;
                            case "FAILED": failed = cnt; break;
                        }
                    }
                }

                // Build WHERE clause for status filter
                string whereClause = "";
                var queryParams = new Dictionary<string, object>();
                var validStatuses = new HashSet<string> { "PENDING", "PROCESSING", "COMPLETED", "FAILED" };
                if (!string.IsNullOrEmpty(statusFilter) && validStatuses.Contains(statusFilter.ToUpper()))
                {
                    whereClause = "WHERE Status = @statusFilter";
                    queryParams["@statusFilter"] = statusFilter.ToUpper();
                }

                // Get total count for pagination
                DataTable countDt = _code.DatabaseQuerySafe(ConnStr,
                    $"SELECT COUNT(*) as Total FROM Accounting_Sync_Queue {whereClause}",
                    queryParams.Count > 0 ? queryParams : null);
                int totalItems = countDt?.Rows.Count > 0 ? Convert.ToInt32(countDt.Rows[0]["Total"]) : 0;
                int totalPages = totalItems > 0 ? (int)Math.Ceiling((double)totalItems / pageSize) : 1;
                if (page > totalPages) page = totalPages;

                // Get paginated items using OFFSET...FETCH (SQL Server 2012+)
                int offset = (page - 1) * pageSize;
                var itemParams = new Dictionary<string, object>
                {
                    { "@offset", offset },
                    { "@pageSize", pageSize }
                };
                if (queryParams.ContainsKey("@statusFilter"))
                    itemParams["@statusFilter"] = queryParams["@statusFilter"];

                // Detect optional doc-cache columns (PHASE12 Migration 14)
                DataTable colCheck = _code.DatabaseQuerySafe(ConnStr,
                    @"SELECT name FROM sys.columns
                      WHERE object_id = OBJECT_ID('Accounting_Sync_Queue')
                        AND name IN ('Nexaacc_Document_Number','Nexaacc_Document_Type')", null);
                bool hasDocCache = colCheck != null && colCheck.Rows.Count >= 2;

                string docCacheCols = hasDocCache
                    ? ", Nexaacc_Document_Number, Nexaacc_Document_Type"
                    : ", CAST(NULL AS NVARCHAR(50)) AS Nexaacc_Document_Number, CAST(NULL AS NVARCHAR(30)) AS Nexaacc_Document_Type";

                // Detect optional post-sync-verify columns (PHASE18 Migration 08)
                DataTable verifyColCheck = _code.DatabaseQuerySafe(ConnStr,
                    @"SELECT name FROM sys.columns
                      WHERE object_id = OBJECT_ID('Accounting_Sync_Queue')
                        AND name IN ('Verify_Status','Verify_Detail')", null);
                bool hasVerify = verifyColCheck != null && verifyColCheck.Rows.Count >= 2;
                string verifyCols = hasVerify
                    ? ", Verify_Status, Verify_Detail"
                    : ", CAST(NULL AS NVARCHAR(10)) AS Verify_Status, CAST(NULL AS NVARCHAR(1000)) AS Verify_Detail";

                DataTable items = _code.DatabaseQuerySafe(ConnStr,
                    $@"SELECT ID, Entity_Type, Entity_ID, Action_Type, Status,
                              Retry_Count, Max_Retries, Error_Message, Created_Date, Payload,
                              Nexaacc_Response_Id{docCacheCols}{verifyCols}
                       FROM Accounting_Sync_Queue
                       {whereClause}
                       ORDER BY Created_Date DESC
                       OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY",
                    itemParams);

                var config = new Integration.AccountingConfig(ConnStr);
                bool isOwner = Session["User"]?.ToString() == "Owner";
                var itemList = new List<Dictionary<string, object>>();
                if (items != null)
                {
                    foreach (DataRow row in items.Rows)
                    {
                      // แถวเดียวที่ข้อมูลเพี้ยน (Entity_ID ไม่ใช่ตัวเลข / ค่า NULL) ต้องไม่ทำให้
                      // ทั้งหน้าล้ม — เดิมพังทั้ง GetQueueData แล้วหน้านั้นเปิดไม่ได้เลย
                      try
                      {
                        string actionType = row["Action_Type"]?.ToString() ?? "";
                        string entityType = row["Entity_Type"]?.ToString() ?? "";
                        bool isPayroll = actionType == "CREATE_PAYROLL_ENTRY"
                            || entityType == "PAYROLL";
                        bool isEmployeeVoucher = false;
                        if (!isPayroll && actionType == "CREATE_VOUCHER_JOURNAL")
                        {
                            string payload = row.Table.Columns.Contains("Payload")
                                ? (row["Payload"]?.ToString() ?? "") : "";
                            isEmployeeVoucher = payload.Contains("\"expenseCategory\":\"เงินเดือน\"")
                                || payload.Contains("\"expenseCategory\":\"salary\"")
                                || payload.Contains("\"description\":\"เงินเดือน");
                        }
                        bool isSensitive = isPayroll || isEmployeeVoucher;
                        bool mask = isSensitive && !isOwner;

                        string nexaaccId = row.Table.Columns.Contains("Nexaacc_Response_Id")
                            ? (row["Nexaacc_Response_Id"]?.ToString() ?? "") : "";
                        string nexaaccDocNum = row.Table.Columns.Contains("Nexaacc_Document_Number")
                            ? (row["Nexaacc_Document_Number"]?.ToString() ?? "") : "";
                        string nexaaccDocType = row.Table.Columns.Contains("Nexaacc_Document_Type")
                            ? (row["Nexaacc_Document_Type"]?.ToString() ?? "") : "";
                        string nexaaccUrl = "";
                        if (!mask && !string.IsNullOrEmpty(nexaaccId) && !nexaaccId.StartsWith("SKIPPED") && config.IsConfigured)
                        {
                            string basePath = config.RawBaseUrl.TrimEnd('/');
                            string cid = config.CompanyId.ToString();
                            string typePath = "documents";
                            switch ((nexaaccDocType ?? "").ToUpper())
                            {
                                case "INVOICE": typePath = "invoices"; break;
                                case "EXPENSE": typePath = "expenses"; break;
                                case "JOURNAL": typePath = "journals"; break;
                                case "CREDIT_NOTE": typePath = "credit-notes"; break;
                                case "DEBIT_NOTE": typePath = "debit-notes"; break;
                            }
                            nexaaccUrl = $"{basePath}/{cid}/{typePath}/{nexaaccId}";
                        }

                        string errorMsg = row["Error_Message"]?.ToString() ?? "";
                        if (mask && !string.IsNullOrEmpty(errorMsg))
                            errorMsg = "🔒 ข้อมูลถูกจำกัดการเข้าถึง";

                        // Error_Message ของแถว FAILED เป็น response ดิบของ API ซึ่งยาวมากได้
                        // (บางครั้งเป็นหน้า HTML error page ทั้งหน้า) → ถอด tag + ตัดก่อนส่ง
                        // กัน payload บวมจนทะลุลิมิต serialize และกันเบราว์เซอร์อืด
                        const int MaxErrChars = 2000;
                        if (!mask && errorMsg.Length > MaxErrChars)
                            errorMsg = CondenseErrorForDisplay(errorMsg);
                        if (errorMsg.Length > MaxErrChars)
                            errorMsg = errorMsg.Substring(0, MaxErrChars) + "… (ตัดแสดง — กด Log เพื่อดูเต็ม)";

                        itemList.Add(new Dictionary<string, object>
                        {
                            { "id", ToLongSafe(row["ID"]) },
                            { "entityType", mask ? "PAYROLL" : entityType },
                            { "entityId", mask ? 0 : ToIntSafe(row["Entity_ID"]) },
                            { "actionType", actionType },
                            { "status", row["Status"]?.ToString() },
                            { "retryCount", ToIntSafe(row["Retry_Count"]) },
                            { "maxRetries", ToIntSafe(row["Max_Retries"]) },
                            { "error", errorMsg },
                            { "created", row["Created_Date"] == DBNull.Value ? ""
                                        : Convert.ToDateTime(row["Created_Date"]).ToString("dd/MM HH:mm") },
                            { "nexaaccId", mask ? "" : nexaaccId },
                            { "nexaaccDocNumber", mask ? "🔒" : nexaaccDocNum },
                            { "nexaaccDocType", mask ? "" : nexaaccDocType },
                            { "nexaaccUrl", mask ? "" : nexaaccUrl },
                            { "verifyStatus", mask ? "" : (row.Table.Columns.Contains("Verify_Status") && row["Verify_Status"] != DBNull.Value ? row["Verify_Status"].ToString() : "") },
                            { "verifyDetail", mask ? "" : (row.Table.Columns.Contains("Verify_Detail") && row["Verify_Detail"] != DBNull.Value ? row["Verify_Detail"].ToString() : "") },
                            // ⚠ ใช้ mask ไม่ใช่ isSensitive: หน้าเว็บซ่อน "ปุ่มทั้งแถว" เมื่อค่านี้เป็น true
                            //   เดิมส่ง isSensitive → แถวเงินเดือนไม่มีปุ่ม Retry/Log **แม้แต่เจ้าของระบบ**
                            //   ทั้งที่การปกปิดควรซ่อน "ข้อมูล" จากคนที่ไม่มีสิทธิ์ ไม่ใช่ริบ "เครื่องมือ"
                            //   ของคนที่มีสิทธิ์ (คิวเงินเดือนที่ล้มเหลวจึงแก้ไม่ได้เลยผ่านหน้าจอ)
                            { "sensitive", mask }
                        });
                      }
                      catch (Exception rowEx)
                      {
                          // ยังส่งแถวนี้กลับไป แต่บอกว่าอ่านไม่ได้ เพื่อให้ผู้ใช้เห็นว่ามีปัญหาที่แถวไหน
                          itemList.Add(new Dictionary<string, object>
                          {
                              { "id", ToLongSafe(row["ID"]) },
                              { "entityType", row["Entity_Type"]?.ToString() ?? "?" },
                              { "entityId", 0 },
                              { "actionType", row["Action_Type"]?.ToString() ?? "?" },
                              { "status", row["Status"]?.ToString() ?? "?" },
                              { "retryCount", 0 }, { "maxRetries", 0 },
                              { "error", "⚠️ อ่านรายการนี้ไม่ได้: " + rowEx.Message },
                              { "created", "" }, { "nexaaccId", "" }, { "nexaaccDocNumber", "" },
                              { "nexaaccDocType", "" }, { "nexaaccUrl", "" },
                              { "verifyStatus", "" }, { "verifyDetail", "" }, { "sensitive", false }
                          });
                          try { _code.Logs(ConnStr, "AccountingIntegration",
                              $"GetQueueData row failed (ID={row["ID"]}): {rowEx.Message}", "SYSTEM"); } catch { }
                      }
                    }
                }

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "pending", pending },
                    { "processing", processing },
                    { "completed", completed },
                    { "failed", failed },
                    { "items", itemList },
                    { "page", page },
                    { "pageSize", pageSize },
                    { "totalItems", totalItems },
                    { "totalPages", totalPages }
                };
            }
            catch (Exception ex)
            {
                try { _code.Logs(ConnStr, "AccountingIntegration", $"GetQueueData error: {ex.Message}", "SYSTEM"); }
                catch { }
                return new Dictionary<string, object>
                {
                    { "success", false },
                    { "message", "Queue query failed: " + ex.Message },
                    { "pending", 0 }, { "processing", 0 }, { "completed", 0 }, { "failed", 0 },
                    { "items", new List<object>() },
                    { "page", 1 }, { "pageSize", 20 }, { "totalItems", 0 }, { "totalPages", 1 }
                };
            }
        }

        private bool IsSensitiveQueueItem(long queueId)
        {
            var dt = _code.DatabaseQuerySafe(ConnStr,
                "SELECT Action_Type, Entity_Type FROM Accounting_Sync_Queue WHERE ID = @id",
                new Dictionary<string, object> { { "@id", queueId } });
            if (dt == null || dt.Rows.Count == 0) return false;
            string action = dt.Rows[0]["Action_Type"]?.ToString() ?? "";
            string entity = dt.Rows[0]["Entity_Type"]?.ToString() ?? "";
            return action == "CREATE_PAYROLL_ENTRY" || entity == "PAYROLL";
        }

        /// <summary>
        /// ✔ ปิดรายการคิวด้วยมือ — "งานนี้เสร็จจริงบน NextAcc แล้ว แค่คิวฝั่งเราไม่รู้"
        ///
        /// จำเป็นเพราะมีเคสที่ปลายทางถูกต้องแล้วแต่คิวปิดตัวเองไม่ได้ เช่น
        /// เอกสาร/ข้อมูลขึ้น NextAcc ครบแล้ว แต่ call สุดท้ายตอบ error, หรือมีคนจัดการต่อเองบน NextAcc
        /// ถ้าไม่มีทางปิด รายการจะค้าง FAILED ถาวร → แจ้งเตือน "คิวต้องตรวจสอบ" ทุก 6 ชม.
        /// จนกลบรายการที่มีปัญหาจริง
        ///
        /// **ไม่ยิง API ใด ๆ** — เปลี่ยนเฉพาะสถานะฝั่งเรา และเขียนกำกับไว้ว่าเป็นการปิดโดยคน
        /// (Nexaacc_Response_Id = 'CLOSED_BY_USER') เพื่อให้ย้อนตรวจได้ว่าไม่ใช่ระบบปิดเอง
        /// จำกัดเฉพาะ Owner เพราะเป็นการรับรองว่างานบัญชีเสร็จแล้ว
        /// </summary>
        private Dictionary<string, object> CloseQueueItemManually()
        {
            try
            {
                if (Session["User"]?.ToString() != "Owner")
                    return new Dictionary<string, object> { { "success", false },
                        { "message", "เฉพาะเจ้าของระบบเท่านั้นที่ปิดรายการคิวด้วยมือได้" } };

                long queueId = long.Parse(Request.QueryString["queueId"] ?? "0");
                if (queueId <= 0)
                    return new Dictionary<string, object> { { "success", false }, { "message", "ไม่พบรายการคิว" } };

                var dt = _code.DatabaseQuerySafe(ConnStr,
                    "SELECT Status, Action_Type FROM Accounting_Sync_Queue WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", queueId } });
                if (dt == null || dt.Rows.Count == 0)
                    return new Dictionary<string, object> { { "success", false }, { "message", $"ไม่พบคิว #{queueId}" } };

                string st = dt.Rows[0]["Status"]?.ToString() ?? "";
                if (st == "COMPLETED")
                    return new Dictionary<string, object> { { "success", true }, { "message", $"คิว #{queueId} ปิดอยู่แล้ว" } };
                if (st == "PROCESSING")
                    return new Dictionary<string, object> { { "success", false },
                        { "message", $"คิว #{queueId} กำลังทำงานอยู่ — รอให้จบก่อน" } };

                string note = (Request.QueryString["note"] ?? "").Trim();
                if (note.Length > 300) note = note.Substring(0, 300);
                string who = Session["Username"]?.ToString() ?? Session["User"]?.ToString() ?? "Owner";
                string stamp = $"✔ ปิดด้วยมือโดย {who} เมื่อ {DateTime.Now:dd/MM/yyyy HH:mm} "
                             + $"(ยืนยันว่างานเสร็จบน NextAcc แล้ว — ไม่ได้ยิง API ซ้ำ)"
                             + (note.Length > 0 ? $" · หมายเหตุ: {note}" : "");

                _code.DatabaseInsertSafe(ConnStr,
                    @"UPDATE Accounting_Sync_Queue
                      SET Status = 'COMPLETED',
                          Processed_Date = GETDATE(),
                          Nexaacc_Response_Id = ISNULL(NULLIF(Nexaacc_Response_Id, ''), 'CLOSED_BY_USER'),
                          Error_Message = CAST(@note AS NVARCHAR(MAX)) + CHAR(13) + CHAR(10)
                                          + N'— เหตุผลเดิม —' + CHAR(13) + CHAR(10)
                                          + CAST(ISNULL(Error_Message, N'') AS NVARCHAR(MAX))
                      WHERE ID = @id AND Status <> 'PROCESSING'",
                    new Dictionary<string, object> { { "@id", queueId }, { "@note", stamp } });

                _code.Logs(ConnStr, "AccountingSync",
                    $"ปิดคิว #{queueId} ({dt.Rows[0]["Action_Type"]}) ด้วยมือ — {stamp}", who);

                return new Dictionary<string, object> { { "success", true },
                    { "message", $"ปิดคิว #{queueId} แล้ว (ไม่ได้ยิง API ซ้ำ) — เหตุผลเดิมยังเก็บไว้ในรายการ" } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "Close Error: " + ex.Message } };
            }
        }

        private Dictionary<string, object> RetryQueueItem()
        {
            try
            {
                long queueId = long.Parse(Request.QueryString["queueId"] ?? "0");
                if (Session["User"]?.ToString() != "Owner" && IsSensitiveQueueItem(queueId))
                    return new Dictionary<string, object> { { "success", false }, { "message", "ไม่มีสิทธิ์ดำเนินการกับรายการเงินเดือน" } };

                // ผู้ใช้กด Retry เอง → ล้างสถานะพัก ไม่งั้นรายการจะถูกข้ามเงียบ ๆ
                Integration.AccountingApiClient.ClearServerDown();

                var sync = new Integration.AccountingSyncService(ConnStr);

                // Retry บนใบเสร็จที่ COMPLETED แล้ว = "re-post ตามหลักการบัญชีปัจจุบัน":
                // void เอกสารเก่าบน NextAcc + reset marker + สร้างใหม่เลขเดิม อัตโนมัติใน click เดียว
                // (NextAcc แก้เอกสารที่โพสต์แล้ว in-place ไม่ได้ — นี่คือวิธี "แก้ JE" ที่ระบบทำให้เองครบ)
                var rowDt = _code.DatabaseQuerySafe(ConnStr,
                    "SELECT Status, Action_Type FROM Accounting_Sync_Queue WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", queueId } });
                string rowStatus = rowDt?.Rows.Count > 0 ? rowDt.Rows[0]["Status"]?.ToString() : "";
                string rowAction = rowDt?.Rows.Count > 0 ? rowDt.Rows[0]["Action_Type"]?.ToString() : "";

                if (rowStatus == "COMPLETED" && rowAction == "CREATE_RECEIPT_DOCUMENT")
                {
                    long newQid = sync.RepostReceiptWithCurrentLogic(queueId);
                    if (newQid == 0)
                        return new Dictionary<string, object> { { "success", true },
                            { "message", sync.LastRepostMessage ?? "✅ แก้เอกสาร/JE เดิมบน NextAcc ตามหลักการปัจจุบันแล้ว (ไม่ void)" } };
                    if (newQid > 0)
                        return new Dictionary<string, object> { { "success", true },
                            { "message", $"Re-post แล้ว: NextAcc ไม่ให้แก้ตรง → void เอกสารเก่า + สร้างใหม่ (queue ใหม่ #{newQid})" } };
                    return new Dictionary<string, object> { { "success", false },
                        { "message", sync.LastRepostMessage ?? "Re-post ไม่สำเร็จ — ตรวจ payload/เลขใบเสร็จของรายการนี้" } };
                }

                // กัน double-post: Retry ซ้ำบนรายการอื่นที่ COMPLETED แล้ว จะรัน processor ใหม่ →
                // สร้าง JE/เอกสารซ้ำอีกใบบน NextAcc (processor ส่วนใหญ่ไม่ dedupe ระดับ NextAcc)
                if (rowStatus == "COMPLETED")
                {
                    return new Dictionary<string, object> { { "success", false },
                        { "message", "รายการนี้โพสต์สำเร็จแล้ว — Retry ซ้ำจะสร้าง JE ซ้ำบน NextAcc\n" +
                            "ถ้าต้องแก้ตัวเลข: JE ทั่วไป (ตัดมัดจำ/ปรับปรุง/เงินเดือน) แก้ตรงบน NextAcc ได้เลย " +
                            "(อนุมัติแล้วก็แก้ได้ ถ้างวดบัญชียังไม่ปิด) / เอกสารใบเสร็จ-ใบกำกับ ใช้ปุ่มแก้ไขที่หน้าเอกสาร (void→สร้างใหม่)" } };
                }

                if (rowStatus == "PROCESSING")
                {
                    return new Dictionary<string, object> { { "success", false },
                        { "message", "รายการนี้กำลังส่งไป NextAcc อยู่ — รอให้จบรอบก่อน (ถ้าค้างเกิน 15 นาที ระบบจะคืนเป็นรอส่งเอง)" } };
                }

                if (!sync.RetryItem(queueId))
                    return new Dictionary<string, object> { { "success", false },
                        { "message", $"รายการ #{queueId} อยู่ในสถานะ {rowStatus} — Retry ไม่ได้ " +
                            "(ถูกแทนด้วยรายการใหม่/ถูกยกเลิก — ให้ Retry ที่รายการล่าสุดของเอกสารนี้แทน)" } };
                return new Dictionary<string, object> { { "success", true }, { "message", $"Reset queue item #{queueId} to PENDING" } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        /// <summary>ดึง log AccountingSync ที่เกี่ยวข้องกับคิวนี้ (เต็ม ไม่ตัด) + Error_Message เต็ม —
        /// ค้นจากตัวระบุใน Payload (receiptNumber/documentNumber/reservationId) + Entity_ID.</summary>
        private Dictionary<string, object> GetItemLogs()
        {
            try
            {
                long queueId = long.Parse(Request.QueryString["queueId"] ?? "0");
                if (Session["User"]?.ToString() != "Owner" && IsSensitiveQueueItem(queueId))
                    return new Dictionary<string, object> { { "success", false }, { "message", "ไม่มีสิทธิ์ดูรายการเงินเดือน" } };

                var dt = _code.DatabaseQuerySafe(ConnStr,
                    @"SELECT Payload, Entity_ID, Entity_Type, Error_Message, Created_Date, Processed_Date
                      FROM Accounting_Sync_Queue WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", queueId } });
                if (dt == null || dt.Rows.Count == 0)
                    return new Dictionary<string, object> { { "success", false }, { "message", "ไม่พบรายการคิวนี้" } };

                var row = dt.Rows[0];
                string payload = row["Payload"]?.ToString() ?? "";
                string entityId = row["Entity_ID"]?.ToString() ?? "";
                string errorMsg = CondenseErrorForDisplay(row["Error_Message"]?.ToString() ?? "");

                // กรอบเวลาให้ SQL ใช้ index บน LogDateTime ได้ — ถ้าไม่จำกัด LIKE '%..%' จะ scan ทั้ง Logs
                // (ตาราง Logs โตเป็นล้านแถว → หน้าจอ "กำลังโหลด..." ค้างยาว)
                DateTime winFrom = DateTime.Now.AddDays(-30), winTo = DateTime.Now.AddDays(1);
                if (row["Created_Date"] != DBNull.Value)
                {
                    var created = Convert.ToDateTime(row["Created_Date"]);
                    winFrom = created.AddDays(-1);
                    var last = row["Processed_Date"] != DBNull.Value ? Convert.ToDateTime(row["Processed_Date"]) : created;
                    winTo = (last > created ? last : created).AddDays(7);
                    if (winTo > DateTime.Now.AddDays(1)) winTo = DateTime.Now.AddDays(1);
                }

                // ── ดึงตัวระบุจาก payload เพื่อค้น log ─────────────────────────────
                var tokens = new List<string>();
                foreach (string field in new[] { "receiptNumber", "documentNumber", "reservationId" })
                {
                    var m = System.Text.RegularExpressions.Regex.Match(
                        payload, "\"" + field + "\"\\s*:\\s*\"?([^\",}]+)");
                    if (m.Success)
                    {
                        string v = m.Groups[1].Value.Trim();
                        if (!string.IsNullOrEmpty(v) && v != "0" && !tokens.Contains(v)) tokens.Add(v);
                    }
                }

                var logs = new List<Dictionary<string, object>>();
                string note = "";
                if (tokens.Count > 0)
                {
                    // WHERE LogAction='AccountingSync' AND ช่วงเวลา AND (LogDetail LIKE '%tok0%' OR ...)
                    var whereOr = new List<string>();
                    var pars = new Dictionary<string, object>
                    {
                        { "@from", winFrom }, { "@to", winTo }
                    };
                    for (int i = 0; i < tokens.Count; i++)
                    {
                        whereOr.Add($"LogDetail LIKE @t{i}");
                        pars.Add($"@t{i}", "%" + tokens[i] + "%");
                    }
                    string sql = $@"SELECT TOP 300 LogDateTime, LogDetail
                                    FROM Logs
                                    WHERE LogAction = 'AccountingSync'
                                      AND LogDateTime >= @from AND LogDateTime < @to
                                      AND ({string.Join(" OR ", whereOr)})
                                    ORDER BY LogDateTime DESC";

                    bool timedOut;
                    var logDt = QueryWithTimeout(sql, pars, 20, out timedOut);
                    if (timedOut)
                        note = "ค้นหา log ใช้เวลานานเกิน 20 วินาที (ตาราง Logs ใหญ่มาก) — แสดงเฉพาะ Error เต็มด้านบน "
                             + "ถ้าต้องการ log ให้ค้นจากหน้า Logs ด้วยคำว่า \"" + string.Join("\" หรือ \"", tokens) + "\"";
                    else if (logDt != null)
                        foreach (DataRow lr in logDt.Rows)
                            logs.Add(new Dictionary<string, object>
                            {
                                { "time", lr["LogDateTime"] != DBNull.Value ? Convert.ToDateTime(lr["LogDateTime"]).ToString("dd/MM/yyyy HH:mm:ss") : "" },
                                { "detail", Truncate(lr["LogDetail"]?.ToString() ?? "", 8000) }
                            });

                    if (note.Length == 0)
                        note = "ช่วงที่ค้น: " + winFrom.ToString("dd/MM/yyyy HH:mm") + " – " + winTo.ToString("dd/MM/yyyy HH:mm");
                }

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "keys", tokens.Count > 0 ? string.Join(", ", tokens) : ("Entity #" + entityId) },
                    { "error", errorMsg },
                    { "note", note },
                    { "logs", logs }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        /// <summary>
        /// 🩺 ตรวจสุขภาพ integration ก่อนเจอปัญหาตอน sync จริง
        /// เกิดจากเคส "ไม่พบผังบัญชี: 21240" ที่รู้ตัวตอนเอกสารยิงไม่ผ่านแล้วเท่านั้น
        /// ตรวจ: mapping ที่ชี้รหัสบัญชีที่ NextAcc ไม่มี / แหล่งเงินที่ยังไม่ได้ผูกบัญชี /
        ///       ผังบัญชีเก่าค้าง / คิวที่ตายค้าง / สถานะ "NextAcc ล่ม"
        /// </summary>
        private Dictionary<string, object> RunIntegrationHealthCheck()
        {
            var issues = new List<Dictionary<string, object>>();
            Action<string, string, string> add = (level, title, detail) =>
                issues.Add(new Dictionary<string, object> { { "level", level }, { "title", title }, { "detail", detail } });

            try
            {
                // ── 1) NextAcc ล่มอยู่ตอนนี้ไหม ────────────────────────────────
                DateTime downUntil; string downErr;
                if (Integration.AccountingApiClient.IsServerDown(out downUntil, out downErr))
                    add("error", "NextAcc ไม่พร้อมใช้งาน",
                        $"ระบบพักการยิงถึง {downUntil:HH:mm:ss} — {downErr}");

                // ── 2) ผังบัญชีถูกดึงมาแล้วหรือยัง / เก่าแค่ไหน ────────────────
                bool haveCoa = false;
                DataTable coa = null;
                try
                {
                    coa = _code.DatabaseQuerySafe(ConnStr,
                        @"SELECT COUNT(*) AS N, MAX(Last_Synced) AS LastSync FROM Accounting_Nexaacc_Accounts", null);
                }
                catch
                {
                    add("warn", "ยังไม่มีตารางเก็บผังบัญชี",
                        "กดปุ่ม \"Sync บัญชี\" (ปุ่มสีน้ำเงินด้านบนสุดของหน้านี้) หนึ่งครั้ง ระบบจะสร้างตารางและดึงผังบัญชีจาก NextAcc ให้");
                }
                if (coa != null && coa.Rows.Count > 0)
                {
                    int n = coa.Rows[0]["N"] != DBNull.Value ? Convert.ToInt32(coa.Rows[0]["N"]) : 0;
                    haveCoa = n > 0;
                    if (n == 0)
                        add("warn", "ยังไม่ได้ดึงผังบัญชีจาก NextAcc",
                            "กดปุ่ม \"Sync บัญชี\" (ปุ่มสีน้ำเงินด้านบนสุดของหน้านี้) ก่อน ระบบจึงจะตรวจ mapping ให้ได้ และ dropdown เลือกบัญชีจะว่าง");
                    else if (coa.Rows[0]["LastSync"] != DBNull.Value)
                    {
                        var last = Convert.ToDateTime(coa.Rows[0]["LastSync"]);
                        if ((DateTime.Now - last).TotalDays > 30)
                            add("warn", "ผังบัญชีเก่ากว่า 30 วัน",
                                $"ดึงล่าสุด {last:dd/MM/yyyy HH:mm} — กดปุ่ม \"Sync บัญชี\" (สีน้ำเงิน ด้านบนสุดของหน้านี้) เพื่อดึงผังบัญชีล่าสุด "
                                + "แล้วกดตรวจสุขภาพซ้ำ — รหัสบัญชีที่ขึ้นว่า \"ไม่มี\" อาจถูกสร้างใน NextAcc ไปแล้ว");
                    }
                }

                // ── 3) mapping ที่ชี้รหัสบัญชีที่ NextAcc ไม่มี (เคส 21240) ─────
                if (haveCoa)
                {
                    try
                    {
                    var bad = _code.DatabaseQuerySafe(ConnStr,
                        @"SELECT m.TakeTime_Code, m.TakeTime_Description, m.Nexaacc_AccountCode
                          FROM Accounting_Account_Mapping m
                          WHERE ISNULL(m.Is_Active, 1) = 1
                            AND ISNULL(m.Nexaacc_AccountCode, '') <> ''
                            AND NOT EXISTS (SELECT 1 FROM Accounting_Nexaacc_Accounts a
                                            WHERE a.Account_Code = m.Nexaacc_AccountCode)
                          ORDER BY m.TakeTime_Code", null);
                    if (bad != null && bad.Rows.Count > 0)
                    {
                        var lines = new List<string>();
                        foreach (DataRow r in bad.Rows)
                            lines.Add($"{r["TakeTime_Code"]} → {r["Nexaacc_AccountCode"]} ({r["TakeTime_Description"]})");
                        add("error", $"mapping ชี้รหัสบัญชีที่ NextAcc ไม่มี ({bad.Rows.Count} รายการ)",
                            string.Join("\n", lines)
                            + "\n\nวิธีแก้: (1) กดปุ่ม \"Sync บัญชี\" ด้านบนสุดของหน้านี้ก่อนเสมอ — ผังบัญชีที่ใช้เทียบอาจเก่า "
                            + "(2) ยังไม่หาย = สร้างบัญชีรหัสนี้ใน NextAcc แล้ว Sync บัญชี อีกครั้ง หรือแก้ mapping ให้ชี้รหัสที่มีจริง"
                            + " — ถ้าไม่แก้ เอกสารที่ใช้ mapping นี้จะ sync ไม่ผ่าน (API 400 'ไม่พบผังบัญชี')");
                    }
                    }
                    catch { /* ตาราง mapping ยังไม่มี */ }
                }

                // ── 4) แหล่งรับ/จ่ายเงินที่ยังไม่ได้ผูกบัญชี NextAcc ────────────
                try
                {
                    var noAcc = _code.DatabaseQuerySafe(ConnStr,
                        @"SELECT Paid_How FROM Account_Paid_How
                          WHERE Status = 'True'
                            AND ISNULL(CAST(Nexaacc_AccountId AS NVARCHAR(50)), '') = ''", null);
                    if (noAcc != null && noAcc.Rows.Count > 0)
                    {
                        var names = new List<string>();
                        foreach (DataRow r in noAcc.Rows) names.Add(r["Paid_How"]?.ToString() ?? "");
                        add("warn", $"แหล่งเงินยังไม่ได้ผูกบัญชี NextAcc ({noAcc.Rows.Count} รายการ)",
                            string.Join(", ", names)
                            + "\n\nผลที่ตามมา: NextAcc จะเดาบัญชีเงินสด/ธนาคารเองตามวิธีชำระ (เช่น จ่ายผ่านกสิกร แต่ลงกรุงไทย)"
                            + " — ผูกได้ที่หัวข้อ 'แหล่งเงิน' ในหน้านี้");
                    }
                }
                catch { /* ตารางอาจยังไม่มีคอลัมน์นี้ */ }

                // ── 4b) แหล่งเงินที่ผูกไว้ "ผิดชนิด/ตายแล้ว" (กระเป๋าเงิน NextAcc) + เกตเวย์ + OTA ─────
                try
                {
                    var ph = GetPaidHowMapping();
                    var phItems = ph.ContainsKey("items") ? ph["items"] as List<Dictionary<string, object>> : null;
                    if (phItems != null)
                    {
                        var bad = new List<string>();
                        foreach (var it in phItems)
                        {
                            string w = it["warning"]?.ToString() ?? "";
                            string acc = it["accountId"]?.ToString() ?? "";
                            if (w.Length > 0 && acc.Length > 0)   // ยังไม่ผูก = รายงานในข้อ 4 แล้ว
                                bad.Add($"{it["name"]} → {it["accountCode"]}: {w}");
                        }
                        if (bad.Count > 0)
                            add("error", $"แหล่งเงินผูกบัญชีผิดชนิด/บัญชีใช้ไม่ได้ ({bad.Count} รายการ)",
                                string.Join("\n", bad)
                                + "\n\nวิธีแก้: หัวข้อ 'วิธีจ่ายเงิน → บัญชี NextAcc' กด \"ดึงรายการจาก NextAcc\" แล้วเลือกกระเป๋าเงิน/บัญชีใหม่");
                    }

                    // เกตเวย์ที่เปิดใช้อยู่ → ยอดรับลงแหล่งเงินชื่ออะไร และแหล่งเงินนั้นผูกบัญชีหรือยัง
                    string gwFallback = Take_Time_BangPhra.Payments.PaymentGatewayConfig.Get("Payment_PaidHow_Name", "Omise (จ่ายออนไลน์)");
                    string gwActive = "";
                    try
                    {
                        // ตรวจเฉพาะเมื่อเปิดใช้เกตเวย์จริง (ตั้งคีย์ครบ) — ไม่งั้นเตือนรกทั้งที่ไม่ได้รับเงินออนไลน์
                        if (Take_Time_BangPhra.Payments.PaymentGatewayConfig.IsGatewayReady)
                            gwActive = Take_Time_BangPhra.Payments.PaymentGatewayConfig.ActiveProvider;
                    }
                    catch { }
                    if (!string.IsNullOrEmpty(gwActive))
                    {
                        string gwName = Integration.AccountingSyncService.ResolveGatewayPaidHowName(ConnStr, gwActive, null, gwFallback);
                        var gwRow = _code.DatabaseQuerySafe(ConnStr,
                            @"SELECT TOP 1 ISNULL(CAST(Nexaacc_AccountId AS NVARCHAR(50)), '') AS Acc, ISNULL(Nexaacc_AccountCode, '') AS Code
                                FROM Account_Paid_How WHERE Paid_How = @n AND Status = 'True'",
                            new Dictionary<string, object> { { "@n", gwName } });
                        if (gwRow == null || gwRow.Rows.Count == 0)
                            add("error", $"เกตเวย์ {gwActive}: ไม่มีแหล่งเงินชื่อ \"{gwName}\"",
                                "ยอดที่ลูกค้าจ่ายออนไลน์ถูกบันทึกด้วยชื่อแหล่งเงินนี้ แต่ไม่มีแถวที่เปิดใช้ใน Account_Paid_How → "
                                + "NextAcc จะเดาบัญชีเอง (มักเป็นเงินสด) — สร้างแหล่งเงินชื่อนี้ หรือเลือกแหล่งเงินรายเกตเวย์ในหน้านี้");
                        else if (string.IsNullOrEmpty(gwRow.Rows[0]["Acc"]?.ToString()))
                            add("error", $"เกตเวย์ {gwActive}: แหล่งเงิน \"{gwName}\" ยังไม่ผูกบัญชี NextAcc",
                                "เงินที่เกตเวย์รับแทน (ยังไม่เข้าธนาคารจนกว่าจะ payout) ควรลงบัญชีพักเงิน/ลูกหนี้ผู้ให้บริการรับชำระเงิน "
                                + "(ผังมาตรฐาน NextAcc 11340) หรือกระเป๋าเงิน Digital (11113) — ผูกที่ 'วิธีจ่ายเงิน → บัญชี NextAcc'");
                        else
                            add("info", $"เกตเวย์ {gwActive}: ยอดรับลงแหล่งเงิน \"{gwName}\" → บัญชี {gwRow.Rows[0]["Code"]}",
                                "ค่าธรรมเนียมเกตเวย์/ส่วนต่างตอนโอนเข้าธนาคาร (payout) ยังไม่ลงอัตโนมัติ — ผู้ทำบัญชีบันทึกตอนกระทบยอด "
                                + "(Dr ธนาคาร + Dr ค่าธรรมเนียม + Dr ภาษีซื้อ / Cr บัญชีพักเงินเกตเวย์)");
                    }

                    // OTA: โหมดเอกสาร + ช่องทางที่ยังไม่มีบัญชี
                    var ocfg = new Integration.AccountingConfig(ConnStr);
                    if (ocfg.IsOtaDocumentMode)
                    {
                        var om = GetOtaChannelMap();
                        if (!(om.ContainsKey("success") && Convert.ToBoolean(om["success"])))
                            add("error", "โหมดเอกสาร OTA เปิดอยู่ แต่อ่านตาราง mapping ช่องทางไม่ได้",
                                (om.ContainsKey("message") ? om["message"]?.ToString() : "") + "\nรัน migration PHASE19_21 แล้วตรวจอีกครั้ง");
                        var oItems = om.ContainsKey("items") ? om["items"] as List<Dictionary<string, object>> : null;
                        bool haveArFallback = false;
                        try
                        {
                            var ar = _code.DatabaseQuerySafe(ConnStr,
                                @"SELECT TOP 1 1 FROM Accounting_Account_Mapping
                                   WHERE TakeTime_Code = 'OTA_RECEIVABLE' AND Is_Active = 1
                                     AND (Nexaacc_AccountId IS NOT NULL OR ISNULL(Nexaacc_AccountCode, '') <> '')", null);
                            haveArFallback = ar != null && ar.Rows.Count > 0;
                        }
                        catch { }
                        var missing = new List<string>();
                        if (oItems != null)
                            foreach (var it in oItems)
                                if (Convert.ToBoolean(it["isActive"]) && !Convert.ToBoolean(it["linked"]) && Convert.ToInt32(it["bookings"]) > 0)
                                    missing.Add($"{it["displayName"]} ({it["channelKey"]}, {it["bookings"]} การจอง)");
                        if (missing.Count > 0)
                            add(haveArFallback ? "warn" : "error",
                                $"โหมดเอกสาร OTA: {missing.Count} ช่องทางยังไม่ผูกบัญชีพักเงิน/ลูกหนี้รายช่องทาง",
                                string.Join("\n", missing)
                                + (haveArFallback
                                    ? "\n\nระหว่างนี้ใช้ลูกหนี้ OTA กลาง (OTA_RECEIVABLE) แทน — กระทบยอด payout รายช่องทางไม่ได้"
                                    : "\n\nและไม่ได้ map OTA_RECEIVABLE → การจองของช่องทางเหล่านี้จะถูกข้าม (ไม่ลงบัญชี)")
                                + "\nตั้งที่หัวข้อ 'OTA → บัญชี/ผู้ซื้อ'");
                        else if (oItems != null)
                            add("info", "โหมดเอกสาร OTA เปิดอยู่ (RECEIPT_DOC)",
                                "การจอง Channel Collect ที่เลยเช็คเอาท์ → ใบเสร็จรับเงินใน NextAcc ต่อการจอง (ผู้ซื้อ = OTA, "
                                + "Dr บัญชีพักเงิน/ลูกหนี้ของ OTA / Cr รายได้ห้อง + ภาษีขาย) — โหมดเดิม (Nexaacc_OtaRoomRevenue) ถูกแทนที่ ไม่โพสต์ซ้ำ");
                    }
                }
                catch { }

                // ── 5) คิวที่ตายค้าง / ค้างนาน ─────────────────────────────────
                var q = _code.DatabaseQuerySafe(ConnStr,
                    @"SELECT
                        SUM(CASE WHEN Status = 'FAILED' AND Retry_Count >= Max_Retries THEN 1 ELSE 0 END) AS Dead,
                        SUM(CASE WHEN Status = 'PROCESSING' THEN 1 ELSE 0 END) AS Busy,
                        SUM(CASE WHEN Status IN ('PENDING','FAILED')
                                  AND Created_Date < DATEADD(HOUR, -6, GETDATE()) THEN 1 ELSE 0 END) AS Stale
                      FROM Accounting_Sync_Queue", null);
                if (q != null && q.Rows.Count > 0)
                {
                    int dead = q.Rows[0]["Dead"] != DBNull.Value ? Convert.ToInt32(q.Rows[0]["Dead"]) : 0;
                    int busy = q.Rows[0]["Busy"] != DBNull.Value ? Convert.ToInt32(q.Rows[0]["Busy"]) : 0;
                    int stale = q.Rows[0]["Stale"] != DBNull.Value ? Convert.ToInt32(q.Rows[0]["Stale"]) : 0;
                    if (dead > 0)
                        add("error", $"คิวล้มเหลวจนหมด retry: {dead} รายการ",
                            "เอกสารเหล่านี้ยังไม่ขึ้น NextAcc — เปิดดูสาเหตุที่ตารางคิวด้านล่าง แก้ต้นเหตุแล้วกด 'Retry ทั้งหมด'");
                    if (stale > 0)
                        add("warn", $"คิวค้างเกิน 6 ชั่วโมง: {stale} รายการ", "ตรวจว่า timer ทำงานอยู่และ NextAcc ตอบปกติ");
                    if (busy > 3)
                        add("warn", $"รายการค้างสถานะ PROCESSING: {busy} รายการ",
                            "ปกติควรมีไม่กี่รายการ — ถ้าค้างนานแปลว่า worker ตายกลางคัน ระบบจะคืนเป็น PENDING ให้เองตามเวลาที่ตั้งไว้");
                }

                // ── 6) การตั้งค่าคีย์ ──────────────────────────────────────────
                var cfg = new Integration.AccountingConfig(ConnStr);
                if (!cfg.IsConfigured)
                    add("error", "ยังตั้งค่า NextAcc ไม่ครบ", "ต้องมี Base URL + API Key (+ Company ID สำหรับ company endpoints)");
                else
                {
                    if (!cfg.IsIntegrationKey)
                        add("error", "Integration Key ไม่ใช่คีย์ขึ้นต้น int_",
                            "endpoint /api/integration/* ต้องใช้คีย์ int_ เท่านั้น — คีย์ acc_ จะ TestConnection ไม่ผ่าน (401) และ sync หลักใช้ไม่ได้");
                    if (cfg.CompanyId == Guid.Empty)
                        add("warn", "ยังไม่ได้ตั้ง Company ID",
                            "ฟีเจอร์ที่ใช้ company endpoints (OCR, override บัญชีเงิน, ลายเซ็นผู้จ่าย, มัดจำ deferred VAT) จะถูกข้าม");
                    if (cfg.IsCashSaleUseReceipt)
                        add("error", "เปิด Nexaacc_CashSale_UseReceipt อยู่ — หัวเอกสารจะเป็น 'ใบเสร็จรับเงิน' เสมอ",
                            "flag นี้บังคับให้เช็คเอาท์/รับชำระออกเป็นเอกสาร Receipt (type 3) แม้ผู้ซื้อมีเลขผู้เสียภาษีครบ\n\n"
                            + "NextAcc คำนวณหัวเอกสารจาก flag บนตัวเอกสาร (PdfGenerationService.ComputeDocumentTitle) "
                            + "ไม่ใช่จากข้อมูลผู้ซื้อ:\n"
                            + "  • ขายสด (IssuedAsCashReceipt) → \"ใบกำกับภาษี/ใบเสร็จรับเงิน\"\n"
                            + "  • ไม่ประสงค์รับใบกำกับ → \"ใบเสร็จรับเงิน\"\n"
                            + "  • Receipt (type 3) → \"ใบเสร็จรับเงิน\" เสมอ\n\n"
                            + "ต้องการใบกำกับภาษี/ใบเสร็จรับเงิน + e-Tax → ตั้ง flag นี้เป็น 0 "
                            + "(ระบบจะออกเป็นใบกำกับขายสดใบเดียว หักมัดจำในใบ) แล้วกด 'ส่งแก้ไขขึ้น NextAcc' ที่ใบนั้น");

                    // หัวเอกสารขาย — TakeTime ตรวจสิทธิ์ ภ.พ.06 ของบริษัทใน NextAcc เองไม่ได้ จึงบอกเงื่อนไขไว้เสมอ
                    add("info", "หัวเอกสารขาย: " + (cfg.IsReceiptHeaderAbbreviated
                            ? "ABBREVIATED (อย่างย่อ ยกเว้นนิติบุคคลข้อมูลครบ)"
                            : "AUTO (เต็มรูปเมื่อลูกค้ามีเลขภาษี+ที่อยู่ / นอกนั้นอย่างย่อ)"),
                        "หัว \"ใบเสร็จรับเงิน/ใบกำกับภาษีอย่างย่อ\" NextAcc พิมพ์ให้เมื่อผ่านด่าน ภ.พ.06 ของ NextAcc เท่านั้น — "
                        + "ปลดที่ NextAcc ทางใดทางหนึ่ง: (1) แอดมินแพลตฟอร์ม site-settings ปิด \"บังคับ ภ.พ.06 ก่อนออกใบกำกับภาษีอย่างย่อ\" (RequirePhoR06ForAbbreviatedTaxInvoice=false — เหมาะกับกิจการที่ไม่ได้ใช้เครื่องบันทึกการเก็บเงิน) หรือ (2) ข้อมูลบริษัท ติ๊ก \"ได้รับอนุมัติ ภ.พ.06\" + วันที่ (ย้อนก่อนเอกสารใบแรก) — ยังไม่ปลด NextAcc จะลดหัวเป็น \"ใบเสร็จรับเงิน\" "
                        + "(PdfGenerationService.ComputeDocumentTitle / AbbreviatedTaxInvoiceRule). "
                        + "ดูหัวจริงของแต่ละใบได้จาก log \"หัวเอกสาร NextAcc:\" หลัง sync (ต้องเปิด Post-sync verify)"
                        + (cfg.IsPostSyncVerifyEnabled ? "" : " ⚠ ตอนนี้ Post-sync verify ปิดอยู่"));
                }

                // ── 6b) นโยบาย VAT เงินมัดจำ กับ mapping 21913 สอดคล้องกันไหม ─────────────
                // ตั้งให้พัก VAT ที่ 21913 แต่ไม่ได้ผูกบัญชี = โค้ด fallback ลง 21911 "เงียบ ๆ"
                // → VAT เข้า ภ.พ.30 เร็วไป 1 งวด โดยไม่มีใครรู้จนกว่าจะดูงบ
                try
                {
                    string vatMode = cfg.DepositVatRecognition;
                    bool wantDefer = cfg.IsDepositOutputVatDeferred;
                    bool deferMapped = false;
                    var dv = _code.DatabaseQuerySafe(ConnStr,
                        @"SELECT TOP 1 m.Nexaacc_AccountCode,
                                 CASE WHEN EXISTS (SELECT 1 FROM Accounting_Nexaacc_Accounts a
                                                   WHERE a.Account_Code = m.Nexaacc_AccountCode)
                                      THEN 1 ELSE 0 END AS CodeExists
                          FROM Accounting_Account_Mapping m
                          WHERE m.TakeTime_Code = 'OUTPUT_VAT_DEFERRED'
                            AND ISNULL(m.Is_Active, 1) = 1
                            AND ISNULL(m.Nexaacc_AccountCode, '') <> ''", null);
                    string deferCode = null;
                    if (dv != null && dv.Rows.Count > 0)
                    {
                        deferCode = dv.Rows[0]["Nexaacc_AccountCode"]?.ToString();
                        deferMapped = !haveCoa || Convert.ToInt32(dv.Rows[0]["CodeExists"]) == 1;
                    }

                    // ── ต้องบอก "ผลลัพธ์จริง" เสมอ ไม่ใช่เตือนเฉพาะตอนตั้งขัดกัน ───────────
                    // ค่าเริ่มต้น (CHECKOUT + ไม่พัก) ไม่เข้าเงื่อนไขเตือนข้อไหนเลย
                    // ⇒ ใบมัดจำออกมาไม่มี VAT โดยไม่มีอะไรบอกสักบรรทัด (เคสจริงที่เจอซ้ำ)
                    bool bizVat = false;
                    string rawUseVat = null;
                    try
                    {
                        var bv = _code.DatabaseQuerySafe(ConnStr, "SELECT TOP 1 Use_Vat FROM Business_Info", null);
                        if (bv != null && bv.Rows.Count > 0 && bv.Rows[0]["Use_Vat"] != DBNull.Value)
                        {
                            rawUseVat = bv.Rows[0]["Use_Vat"].ToString();
                            bizVat = rawUseVat == "1" || rawUseVat.Equals("true", StringComparison.OrdinalIgnoreCase);
                        }
                    }
                    catch { }

                    bool receiptMode = vatMode.Equals("RECEIPT", StringComparison.OrdinalIgnoreCase);
                    bool willSplit = bizVat && receiptMode;
                    bool willDefer = willSplit && wantDefer && deferMapped;

                    string effect = willDefer
                        ? "แยก VAT ออกจากมัดจำ แล้วพักที่ \"ภาษีขายรอเรียกเก็บ\" (" + (deferCode ?? "21913") + ")\n"
                          + "  Dr แหล่งเงิน / Cr เงินรับล่วงหน้า (ยอดสุทธิ) + Cr " + (deferCode ?? "21913") + " (VAT)\n"
                          + "  → VAT ยังไม่เข้า ภ.พ.30 จนกว่าจะเช็คเอาท์"
                        : willSplit
                        ? "แยก VAT ออกจากมัดจำ เข้า \"ภาษีขาย\" (21911) ทันที\n"
                          + "  Dr แหล่งเงิน / Cr เงินรับล่วงหน้า (ยอดสุทธิ) + Cr 21911 (VAT)\n"
                          + "  → VAT เข้า ภ.พ.30 เดือนที่รับมัดจำ"
                        : "ไม่แยก VAT เลย — Cr เงินรับล่วงหน้าเต็มก้อน\n"
                          + "  Dr แหล่งเงิน 1,000 / Cr เงินรับล่วงหน้า 1,000 (ไม่มีบรรทัดภาษีขาย)\n"
                          + "  → VAT รับรู้ทั้งก้อนตอนเช็คเอาท์";

                    string why = !bizVat
                        ? "เพราะ Business_Info.Use_Vat = " + (rawUseVat == null ? "(ไม่มีค่า)" : "\"" + rawUseVat + "\"")
                          + " ⇒ ระบบถือว่ากิจการไม่จด VAT (ต้องเป็น True / true / 1)"
                        : !receiptMode
                        ? "เพราะ Deposit_Vat_Recognition = " + vatMode + " (ต้องเป็น RECEIPT ถึงจะแยก VAT ตอนรับมัดจำ)"
                        : (wantDefer && !deferMapped)
                        ? "ตั้งให้พักที่ 21913 แต่บัญชียังใช้ไม่ได้ → ระบบ fallback ลง 21911"
                        : "";

                    add(willSplit ? "info" : "warn",
                        "ใบมัดจำใบต่อไปจะลงบัญชีแบบนี้: " + (willDefer ? "พัก VAT ที่ 21913"
                            : willSplit ? "VAT เข้า 21911 ทันที" : "ไม่มี VAT"),
                        effect + (why.Length > 0 ? "\n\n" + why : "")
                        + "\n\nค่าที่ใช้อยู่: Use_Vat=" + (rawUseVat ?? "-")
                        + " · Deposit_Vat_Recognition=" + vatMode
                        + " · Deposit_Defer_Output_Vat=" + (wantDefer ? "1" : "0")
                        + " · OUTPUT_VAT_DEFERRED=" + (deferCode ?? "ยังไม่ผูก"));

                    if (!bizVat)
                        add("error", "กิจการถูกตั้งว่า \"ไม่จด VAT\" — เอกสารรับทุกใบออกโดยไม่มีภาษีขาย",
                            "Business_Info.Use_Vat = " + (rawUseVat == null ? "(ไม่มีค่า)" : "\"" + rawUseVat + "\"")
                            + "\nมีผลกับใบเสร็จ/ใบมัดจำ/ใบกำกับที่ส่ง NextAcc ทั้งหมด ไม่ใช่แค่มัดจำ\n"
                            + "ถ้ากิจการจด VAT จริง ให้แก้ค่านี้เป็น True ที่ ข้อมูลกิจการ (Business_Info) "
                            + "แล้วออกเอกสารใหม่ — ใบที่ออกไปแล้วต้องแก้ด้วยใบปรับปรุง");

                    if (wantDefer && !deferMapped)
                        add("error", "ตั้งให้พัก VAT มัดจำที่ 21913 แต่บัญชียังใช้ไม่ได้",
                            (deferCode == null
                                ? "ยังไม่ได้ผูก mapping OUTPUT_VAT_DEFERRED"
                                : $"ผูกไว้ที่รหัส {deferCode} แต่ผังบัญชี NextAcc ไม่มีรหัสนี้")
                            + "\n\nผลที่เกิดขึ้นเงียบ ๆ: VAT ของเงินมัดจำจะถูกลง \"ภาษีขาย 21911\" ทันที "
                            + "→ เข้า ภ.พ.30 เร็วไป 1 งวด (ไม่ใช่ \"ภาษีขายรอเรียกเก็บ\" ตามที่ตั้งไว้)\n"
                            + "วิธีแก้: ผูก OUTPUT_VAT_DEFERRED กับบัญชี 21913 ในหัวข้อผังบัญชี/Mapping ของหน้านี้ "
                            + "→ Sync บัญชี → ออกใบมัดจำใหม่ (ใบที่ออกไปแล้วต้องแก้ด้วยใบปรับปรุง)");
                    else if (wantDefer && !vatMode.Equals("RECEIPT", StringComparison.OrdinalIgnoreCase))
                        add("warn", "ตั้ง \"พัก VAT ที่ 21913\" ไว้ แต่โหมดเป็น CHECKOUT — ไม่มีผล",
                            $"Deposit_Vat_Recognition = {vatMode} แปลว่าใบมัดจำ**ไม่แยก VAT เลย** (Cr เงินรับล่วงหน้าเต็มก้อน) "
                            + "VAT ทั้งหมดรับรู้ตอนเช็คเอาท์ → ไม่มี VAT ให้พักที่ 21913\n"
                            + "ต้องการให้มัดจำลง 21913 จริง ต้องตั้ง Deposit_Vat_Recognition = RECEIPT ด้วย");
                    else if (!wantDefer && vatMode.Equals("RECEIPT", StringComparison.OrdinalIgnoreCase))
                        add("warn", "โหมด RECEIPT + ไม่พัก VAT — มัดจำเข้า ภ.พ.30 ทันที",
                            "ใบมัดจำจะ Cr ภาษีขาย 21911 ตั้งแต่วันรับเงิน (เคร่ง §78/1) "
                            + "ถ้าต้องการให้เข้า ภ.พ.30 ตอนเช็คเอาท์แทน ให้เปิด Deposit_Defer_Output_Vat = 1");
                }
                catch { }

                // ── 7) ใบเสร็จที่ผู้ซื้อกรอกข้อมูลภาษีครบแล้ว แต่เอกสารยังค้างคิว/ไม่เคยขึ้น ─────
                try
                {
                    var stuck = _code.DatabaseQuerySafe(ConnStr,
                        @"SELECT TOP 20 q.ID, q.Status, q.Retry_Count, q.Max_Retries, q.Payload
                          FROM Accounting_Sync_Queue q
                          WHERE q.Action_Type = 'CREATE_RECEIPT_DOCUMENT'
                            AND q.Status IN ('PENDING', 'PROCESSING', 'FAILED')
                          ORDER BY q.ID", null);
                    if (stuck != null && stuck.Rows.Count > 0)
                    {
                        var lines = new List<string>();
                        foreach (DataRow r in stuck.Rows)
                        {
                            var m = System.Text.RegularExpressions.Regex.Match(
                                r["Payload"]?.ToString() ?? "", "\"receiptNumber\"\\s*:\\s*\"([^\"]*)\"");
                            lines.Add($"#{r["ID"]} {r["Status"]} — ใบเสร็จ {(m.Success ? m.Groups[1].Value : "?")}");
                        }
                        add("warn", $"ใบเสร็จที่ยังไม่ขึ้น NextAcc: {stuck.Rows.Count} ใบ",
                            string.Join("\n", lines)
                            + "\n\nระหว่างที่ยังค้างคิว การกดแก้ไขใบเสร็จจะไปอัปเดต payload ของคิวเดิม "
                            + "(ข้อมูลล่าสุดชนะ) แล้วยิงครั้งเดียวเมื่อ NextAcc พร้อม");
                    }
                }
                catch { }

                // ── 8) งานเบื้องหลังยังเดินอยู่จริงไหม (คิว / อ่านอีเมลจอง) ───────────────
                // เคสจริง 19–21 ส.ค. 2026: ล็อกรอบอ่านอีเมลค้างถาวร (sp_getapplock ผูกกับ
                // connection ที่ pool ไว้ แล้ว AppDomain เก่าถูกทิ้งทั้งที่ยังถือล็อก)
                // → ระบบข้ามรอบเงียบ ๆ ทุก 5 นาที ไม่มีการจองจาก OTA เข้าเลย 2 วัน
                // ตรงนี้ทำให้ "งานเบื้องหลังหยุด" มองเห็นได้จากหน้าเดียว
                try
                {
                    var lease = _code.DatabaseQuerySafe(ConnStr,
                        @"SELECT Lock_Name, Owner, Acquired_At, Heartbeat_At, Expires_At
                          FROM App_Run_Lease", null);
                    if (lease != null)
                        foreach (DataRow r in lease.Rows)
                        {
                            if (r["Expires_At"] == DBNull.Value) continue;
                            var exp = Convert.ToDateTime(r["Expires_At"]);
                            if (exp <= DateTime.Now) continue;          // ว่าง = ปกติ
                            var since = r["Acquired_At"] != DBNull.Value
                                ? Convert.ToDateTime(r["Acquired_At"]) : DateTime.Now;
                            if ((DateTime.Now - since).TotalMinutes > 30)
                                add("warn", $"งานเบื้องหลัง '{r["Lock_Name"]}' ถือล็อกมานาน",
                                    $"เริ่มตั้งแต่ {since:dd/MM HH:mm} โดย {r["Owner"]} (หมดอายุ {exp:dd/MM HH:mm})\n"
                                    + "ล็อกนี้หมดอายุเองเสมอ — ถ้าเลยเวลาหมดอายุแล้วยังค้าง แปลว่ามีรอบที่ทำงานนานผิดปกติ");
                        }
                }
                catch { /* ยังไม่ได้รันไมเกรชัน 33 */ }

                try
                {
                    var em = _code.DatabaseQuerySafe(ConnStr,
                        @"SELECT
                            (SELECT TOP 1 ConfigValue FROM Accounting_Integration_Config WHERE ConfigKey='Email_Rsv_Enabled')     AS Enabled,
                            (SELECT TOP 1 ConfigValue FROM Accounting_Integration_Config WHERE ConfigKey='Email_Rsv_LastSuccess') AS LastOk,
                            (SELECT TOP 1 ConfigValue FROM Accounting_Integration_Config WHERE ConfigKey='Email_Rsv_RetryHours')  AS RetryHrs", null);
                    if (em != null && em.Rows.Count > 0 && (em.Rows[0]["Enabled"]?.ToString() == "1"))
                    {
                        // หน้าต่าง retry สั้นเกินไป = อีเมลที่ลงจองไม่สำเร็จตกขบวนถาวรภายในวันเดียว
                        int retryHrs;
                        if (int.TryParse(em.Rows[0]["RetryHrs"]?.ToString(), out retryHrs) && retryHrs < 24)
                            add("warn", $"หน้าต่างลองใหม่สั้นผิดปกติ ({retryHrs} ชั่วโมง)",
                                "อีเมลที่ลงจองไม่สำเร็จจะถูกลองใหม่เฉพาะที่อายุไม่เกินนี้ — "
                                + $"ใบที่ล้มเหลวเมื่อวานจะไม่ถูกหยิบมาทำอีกเลย\n"
                                + "แนะนำตั้ง 72 ชั่วโมง (ช่อง 'ลองใหม่ภายใน (ชั่วโมง)' ในหัวข้ออีเมลจอง)\n"
                                + "ใบที่ตกไปแล้วกู้ได้ด้วยปุ่ม 'กู้อีเมลย้อนหลัง'");

                        DateTime lastOk;
                        string raw = em.Rows[0]["LastOk"]?.ToString() ?? "";
                        if (DateTime.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
                                System.Globalization.DateTimeStyles.None, out lastOk))
                        {
                            double mins = (DateTime.Now - lastOk).TotalMinutes;
                            if (mins > 60)
                                add(mins > 180 ? "error" : "warn",
                                    $"ไม่ได้อ่านอีเมลจองมา {(int)mins} นาที",
                                    $"รอบที่สำเร็จล่าสุด {lastOk:dd/MM/yyyy HH:mm}\n"
                                    + "การจองจาก OTA (STAAH) อาจไม่ถูกบันทึกเข้าระบบระหว่างนี้ — "
                                    + "ตรวจหน้า Admin → อีเมลจอง แล้วกด 'ดึงตอนนี้' เพื่อดูสาเหตุจริง");
                        }
                    }
                }
                catch { }

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "checkedAt", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss") },
                    { "build", Integration.AccountingSyncService.SyncBuildTag },
                    { "buildDate", GetDeployedBuildDate() },
                    { "issues", issues }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        /// <summary>📤 บังคับยิงข้อมูลผู้ซื้อของใบเสร็จขึ้น NextAcc contact เดี๋ยวนี้ + คืนผลดิบ</summary>
        private Dictionary<string, object> PushBuyerContactNow()
        {
            try
            {
                string receipt = (Request.QueryString["receipt"] ?? "").Trim();
                var svc = new Integration.AccountingSyncService(ConnStr);
                var (ok, msg) = svc.PushReceiptBuyerContactNow(receipt);
                return new Dictionary<string, object> { { "success", ok }, { "message", msg } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        /// <summary>
        /// 🔎 ตรวจว่า "ใบเสร็จใบนี้ ระบบจะออกเอกสารในนามใคร" — อ่านจากตัว resolve ชุดเดียว
        /// กับตอน sync จริง (ResolveReceiptBuyer) พร้อมข้อมูลดิบจาก Account_Receipt
        /// เพื่อตัดการเดาออกทั้งหมด: local ถูกแต่เอกสารผิด = ต้องรู้ว่าอ่านได้อะไรจริง ๆ
        /// </summary>
        private Dictionary<string, object> InspectReceiptBuyer()
        {
            try
            {
                string receipt = (Request.QueryString["receipt"] ?? "").Trim();
                if (receipt.Length == 0)
                    return new Dictionary<string, object> { { "success", false }, { "message", "ใส่เลขใบเสร็จก่อน" } };

                var sb = new System.Text.StringBuilder();

                var raw = _code.DatabaseQuerySafe(ConnStr,
                    @"SELECT TOP 1 ar.ID, ISNULL(CAST(ar.Customer_ID AS NVARCHAR(30)), '(null)') AS CustId,
                             ISNULL(CAST(ar.Reservation_ID AS NVARCHAR(30)), '(null)') AS ResId,
                             ISNULL(CAST(ar.Nexaacc_Doc_Number AS NVARCHAR(60)), '(ยังไม่ได้ผูก)') AS DocNo,
                             ISNULL(ar.Nexaacc_Receipt_Payment_Id, '(ว่าง)') AS Marker,
                             ISNULL(c.FullName, '(ไม่พบลูกค้า)') AS BuyerName,
                             ISNULL(c.MobilePhone, '') AS BuyerPhone,
                             ISNULL(NULLIF(LTRIM(RTRIM(c.IDNumber)), ''), ISNULL(c.TaxID, '')) AS BuyerTax,
                             ISNULL(c.Address, '') AS BuyerAddr,
                             ISNULL(r.Customer_MobilePhone, '') AS GuestPhone,
                             ISNULL(g.FullName, '') AS GuestName
                      FROM Account_Receipt ar
                      LEFT JOIN Customer c ON c.ID = ar.Customer_ID
                      LEFT JOIN Reservation r ON r.ID = ar.Reservation_ID
                      LEFT JOIN Customer g ON g.MobilePhone = r.Customer_MobilePhone
                      WHERE ar.ID = @id",
                    new Dictionary<string, object> { { "@id", receipt } });

                if (raw == null || raw.Rows.Count == 0)
                    return new Dictionary<string, object> { { "success", false }, { "message", $"ไม่พบใบเสร็จ {receipt} ในระบบ" } };

                var w = raw.Rows[0];
                sb.AppendLine("── ข้อมูลดิบในตาราง ──");
                sb.AppendLine($"Account_Receipt.Customer_ID = {w["CustId"]}   (การจอง {w["ResId"]})");
                sb.AppendLine($"ผู้ซื้อที่ผูกไว้ : {w["BuyerName"]} ({w["BuyerPhone"]})");
                sb.AppendLine($"   เลขภาษี      : {(string.IsNullOrWhiteSpace(w["BuyerTax"].ToString()) ? "✗ ไม่มี" : w["BuyerTax"].ToString())}");
                sb.AppendLine($"   ที่อยู่        : {(string.IsNullOrWhiteSpace(w["BuyerAddr"].ToString()) ? "✗ ไม่มี" : "✓ มี")}");
                sb.AppendLine($"ผู้จอง (เทียบ)  : {w["GuestName"]} ({w["GuestPhone"]})");
                sb.AppendLine($"เอกสาร NextAcc ที่ผูก : {w["DocNo"]}   marker = {w["Marker"]}");
                sb.AppendLine();

                // contact ของเบอร์ผู้ซื้อ ถูก sync ขึ้น NextAcc หรือยัง
                string bph = w["BuyerPhone"].ToString();
                if (!string.IsNullOrWhiteSpace(bph))
                {
                    sb.AppendLine("── สถานะ sync ผู้ติดต่อ (เบอร์ " + bph + ") ──");
                    var cm = _code.DatabaseQuerySafe(ConnStr,
                        @"SELECT TOP 1 ISNULL(CAST(Nexaacc_Contact_Id AS NVARCHAR(50)), '(ยังไม่มี)') AS Cid,
                                 ISNULL(Sync_Status, '-') AS St, ISNULL(Sync_Error, '') AS Er,
                                 ISNULL(CONVERT(NVARCHAR(20), Last_Synced, 120), '-') AS Ls
                          FROM Accounting_Contact_Map
                          WHERE External_Id = @p AND Contact_Type = 'CUSTOMER'",
                        new Dictionary<string, object> { { "@p", bph } });
                    if (cm != null && cm.Rows.Count > 0)
                        sb.AppendLine($"Accounting_Contact_Map: contactId={cm.Rows[0]["Cid"]} status={cm.Rows[0]["St"]} " +
                                      $"sync ล่าสุด {cm.Rows[0]["Ls"]} {cm.Rows[0]["Er"]}");
                    else
                        sb.AppendLine("Accounting_Contact_Map: ✗ ไม่มีแถวของเบอร์นี้ = ยังไม่เคย push contact สำเร็จเลย");

                    var cq = _code.DatabaseQuerySafe(ConnStr,
                        @"SELECT TOP 3 ID, Status, Retry_Count, Max_Retries, ISNULL(Error_Message, '') AS Er
                          FROM Accounting_Sync_Queue
                          WHERE Action_Type = 'SYNC_CUSTOMER_CONTACT' AND Payload LIKE @pat
                          ORDER BY ID DESC",
                        new Dictionary<string, object> { { "@pat", "%\"mobilePhone\":\"" + bph + "\"%" } });
                    if (cq != null && cq.Rows.Count > 0)
                        foreach (DataRow cr in cq.Rows)
                            sb.AppendLine($"คิว #{cr["ID"]} {cr["Status"]} retry {cr["Retry_Count"]}/{cr["Max_Retries"]} {Truncate(cr["Er"].ToString(), 300)}");
                    else
                        sb.AppendLine("คิว SYNC_CUSTOMER_CONTACT: ✗ ไม่มีรายการของเบอร์นี้");
                    sb.AppendLine();
                }

                var svc = new Integration.AccountingSyncService(ConnStr);
                var chk = svc.CheckBuyerTaxDataForReceipt(receipt);
                sb.AppendLine("── ตัว resolve ชุดเดียวกับตอน sync ──");
                sb.AppendLine(chk.Reason);
                sb.AppendLine();
                sb.AppendLine($"รุ่นโค้ดที่รันอยู่: {Integration.AccountingSyncService.SyncBuildTag}");

                return new Dictionary<string, object>
                {
                    { "success", true }, { "message", sb.ToString() }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        /// <summary>
        /// 🔗 ผูก/ปลดการจับคู่ใบเสร็จในระบบ ↔ เอกสารบน NextAcc ด้วยเลขเอกสารตรง ๆ
        /// ใช้เมื่อสายจับคู่พันกัน (void→สร้างใหม่หลายรอบ / เคยผูกผิดใบ) จนปุ่มในหน้าเอกสารไม่โผล่
        /// ปล่อยช่องเลขเอกสาร NextAcc ว่าง = ปลดการผูก
        /// </summary>
        private Dictionary<string, object> RelinkReceiptDocument()
        {
            try
            {
                string receipt = Request.QueryString["receipt"] ?? "";
                string docNum = Request.QueryString["doc"] ?? "";
                var svc = new Integration.AccountingSyncService(ConnStr);
                var (ok, msg) = svc.RelinkReceiptByDocumentNumber(receipt, docNum);
                return new Dictionary<string, object> { { "success", ok }, { "message", msg } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        /// <summary>ค่านี้ถูกตั้งไว้ในตาราง config แล้วหรือยัง (ต่างจาก "อ่านแล้วได้ค่า default")</summary>
        private bool HasConfigValue(string key)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(ConnStr,
                    "SELECT TOP 1 1 FROM Accounting_Integration_Config WHERE ConfigKey = @k AND ISNULL(ConfigValue,'') <> ''",
                    new Dictionary<string, object> { { "@k", key } });
                return dt != null && dt.Rows.Count > 0;
            }
            catch { return true; }   // อ่านไม่ได้ → ถือว่ามีค่า (ปลอดภัยกว่า: ไม่เขียนทับ)
        }

        /// <summary>วันเวลาที่ DLL ใน bin ถูก deploy — ใช้ยืนยันว่าโค้ดที่รันอยู่เป็นรุ่นล่าสุดจริง</summary>
        private string GetDeployedBuildDate()
        {
            try
            {
                string dll = Server.MapPath("~/bin/Take Time BangPhra.dll");
                if (File.Exists(dll))
                    return File.GetLastWriteTime(dll).ToString("dd/MM/yyyy HH:mm:ss");
            }
            catch { }
            try
            {
                var asm = typeof(Integration.AccountingSyncService).Assembly;
                if (!string.IsNullOrEmpty(asm.Location) && File.Exists(asm.Location))
                    return File.GetLastWriteTime(asm.Location).ToString("dd/MM/yyyy HH:mm:ss") + " (shadow copy)";
            }
            catch { }
            return "?";
        }

        /// <summary>ยิง query แบบมี CommandTimeout — คืน null + timedOut=true แทนที่จะค้างรอ</summary>
        private DataTable QueryWithTimeout(string sql, Dictionary<string, object> pars, int seconds, out bool timedOut)
        {
            timedOut = false;
            try
            {
                var dt = new DataTable();
                using (var con = new SqlConnection(ConnStr))
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.CommandTimeout = seconds;
                    if (pars != null)
                        foreach (var p in pars) cmd.Parameters.AddWithValue(p.Key, p.Value ?? DBNull.Value);
                    con.Open();
                    using (var rd = cmd.ExecuteReader()) dt.Load(rd);
                }
                return dt;
            }
            catch (SqlException ex) when (ex.Number == -2)   // timeout expired
            {
                timedOut = true;
                return null;
            }
            catch { return null; }
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s ?? "";
            return s.Substring(0, max) + "\n… (ตัดที่ " + max.ToString("N0") + " ตัวอักษร จากทั้งหมด " + s.Length.ToString("N0") + ")";
        }

        /// <summary>Error_Message บางครั้งเป็นหน้า HTML error page เต็ม ๆ ของ NextAcc (ASP.NET Core dev page)
        /// ขนาดหลายแสนตัวอักษร → ถอด tag/script/style เหลือข้อความจริง แล้วตัดให้พอดีอ่าน</summary>
        private static string CondenseErrorForDisplay(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            bool looksHtml = raw.IndexOf("<html", StringComparison.OrdinalIgnoreCase) >= 0
                          || raw.IndexOf("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase) >= 0;
            if (looksHtml)
            {
                string t = raw;
                t = System.Text.RegularExpressions.Regex.Replace(t, @"(?is)<(script|style)\b.*?</\1>", " ");
                t = System.Text.RegularExpressions.Regex.Replace(t, @"(?i)<(br|/p|/div|/li|/h\d|/tr)\s*/?>", "\n");
                t = System.Text.RegularExpressions.Regex.Replace(t, @"(?s)<[^>]+>", " ");
                t = System.Net.WebUtility.HtmlDecode(t);
                t = System.Text.RegularExpressions.Regex.Replace(t, @"[ \t]{2,}", " ");
                t = System.Text.RegularExpressions.Regex.Replace(t, @"(\s*\n\s*){2,}", "\n");
                raw = "⚠ NextAcc ตอบกลับเป็นหน้า HTML error page (แอปฝั่ง NextAcc start ไม่ขึ้น/พังทั้งแอป)\n"
                    + "— ถอด HTML ให้อ่านง่ายแล้ว —\n\n" + t.Trim();
            }
            return Truncate(raw, 20000);
        }

        private Dictionary<string, object> RetryAllFailed()
        {
            try
            {
                Integration.AccountingApiClient.ClearServerDown();

                bool isOwner = Session["User"]?.ToString() == "Owner";
                string sql = @"UPDATE Accounting_Sync_Queue
                      SET Status = 'PENDING', Retry_Count = 0, Next_Retry_Date = NULL, Error_Message = NULL
                      WHERE Status = 'FAILED' AND Retry_Count >= Max_Retries";
                if (!isOwner)
                    sql += " AND Action_Type != 'CREATE_PAYROLL_ENTRY' AND ISNULL(Entity_Type,'') != 'PAYROLL'";

                _code.DatabaseInsertSafe(ConnStr, sql, null);

                string msg = isOwner
                    ? "Reset failed items ทั้งหมดเป็น PENDING แล้ว"
                    : "Reset failed items เป็น PENDING แล้ว (ไม่รวมรายการเงินเดือน)";
                return new Dictionary<string, object> { { "success", true }, { "message", msg } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> ResyncCompletedItem()
        {
            try
            {
                long queueId = long.Parse(Request.QueryString["queueId"] ?? "0");
                if (Session["User"]?.ToString() != "Owner" && IsSensitiveQueueItem(queueId))
                    return new Dictionary<string, object> { { "success", false }, { "message", "ไม่มีสิทธิ์ดำเนินการกับรายการเงินเดือน" } };

                _code.DatabaseInsertSafe(ConnStr,
                    @"UPDATE Accounting_Sync_Queue
                      SET Status = 'PENDING', Retry_Count = 0, Nexaacc_Response_Id = NULL,
                          Next_Retry_Date = NULL, Error_Message = NULL, Processed_Date = NULL
                      WHERE ID = @id AND Status IN ('COMPLETED', 'FAILED')",
                    new Dictionary<string, object> { { "@id", queueId } });

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", $"Queue #{queueId} reset เป็น PENDING — จะยิง API ใหม่รอบถัดไป" }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> DeleteQueueItems(Dictionary<string, object> data)
        {
            try
            {
                if (!data.ContainsKey("ids"))
                    return new Dictionary<string, object> { { "success", false }, { "message", "ไม่มี ids" } };

                var rawIds = data["ids"] as System.Collections.ArrayList;
                if (rawIds == null || rawIds.Count == 0)
                    return new Dictionary<string, object> { { "success", false }, { "message", "ไม่ได้เลือกรายการ" } };

                bool isOwner = Session["User"]?.ToString() == "Owner";
                var idList = new List<string>();
                int blocked = 0;
                foreach (var id in rawIds)
                {
                    long qid = Convert.ToInt64(id);
                    if (!isOwner && IsSensitiveQueueItem(qid))
                    {
                        blocked++;
                        continue;
                    }
                    idList.Add(qid.ToString());
                }

                if (idList.Count == 0 && blocked > 0)
                    return new Dictionary<string, object> { { "success", false }, { "message", "ไม่มีสิทธิ์ลบรายการเงินเดือน" } };

                if (idList.Count > 0)
                {
                    string idsCsv = string.Join(",", idList);
                    _code.DatabaseInsertSafe(ConnStr,
                        $"DELETE FROM Accounting_Sync_Queue WHERE ID IN ({idsCsv})", null);
                }

                string msg = $"ลบ {idList.Count} รายการจาก Queue สำเร็จ";
                if (blocked > 0) msg += $" (ข้าม {blocked} รายการเงินเดือน — ต้องเป็น Owner)";

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", msg }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        // ──────────────────────────────────────────────
        // Chart of Accounts Sync — ดึงผังบัญชีจาก NextAcc แล้ว cache ไว้ local
        // ──────────────────────────────────────────────

        private void EnsureAccountsCacheTable()
        {
            _code.DatabaseInsertSafe(ConnStr, @"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Accounting_Nexaacc_Accounts')
                BEGIN
                    CREATE TABLE Accounting_Nexaacc_Accounts (
                        Nexaacc_AccountId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                        Account_Code NVARCHAR(20) NOT NULL,
                        Account_Name NVARCHAR(255),
                        Account_Name_En NVARCHAR(255),
                        Account_Type NVARCHAR(50),
                        Account_Type_Value INT DEFAULT 0,
                        Parent_Account_Id UNIQUEIDENTIFIER NULL,
                        Account_Level INT DEFAULT 0,
                        Is_Active BIT DEFAULT 1,
                        Is_System_Account BIT DEFAULT 0,
                        Description NVARCHAR(500),
                        Last_Synced DATETIME DEFAULT GETDATE()
                    )
                END", null);
        }

        private Dictionary<string, object> SyncChartOfAccounts()
        {
            try
            {
                var config = new Integration.AccountingConfig(ConnStr);
                if (!config.IsConfigured)
                    return new Dictionary<string, object> { { "success", false }, { "message", "ยังไม่ได้ตั้งค่า Nexaacc ครบถ้วน (Base URL, API Key, Company ID)" } };

                // Chart of Accounts endpoint ({company}/accounting/accounts) เรียกผ่าน X-Api-Key
                // ได้ทั้ง int_ และ acc_ (NextAcc ApiKeyMiddleware fallback) — บล็อกเฉพาะเมื่อ
                // company endpoint ปิด (ไม่มี CompanyId หรือ Nexaacc_Company_Endpoints=0)
                if (!config.CanUseCompanyEndpoints)
                {
                    return new Dictionary<string, object>
                    {
                        { "success", false },
                        { "message", "ℹ️ ไม่สามารถดึง Chart of Accounts ได้เพราะ company endpoint ปิดอยู่\n\n" +
                            "ระบบ sync เอกสาร (journal/invoice/ใบเสร็จ) ใช้รหัสบัญชี (AccountCode) ที่ตั้งค่าไว้แล้วใน " +
                            "ตาราง Accounting_Account_Mapping โดยตรง — sync ได้เลยโดยไม่ต้องดึง chart\n\n" +
                            "หมายเหตุ: ถ้าต้องการ refresh chart ให้ตั้ง Company ID และเปิด Nexaacc_Company_Endpoints" }
                    };
                }

                var client = new Integration.AccountingApiClient(config, ConnStr);
                var result = System.Threading.Tasks.Task.Run(() => client.GetAccountsAsync()).Result;
                bool success = result != null && result.data != null;

                if (!success)
                    return new Dictionary<string, object> { { "success", false }, { "message", "ไม่สามารถดึงข้อมูลจาก Nexaacc ได้" } };

                EnsureAccountsCacheTable();

                int upserted = 0;
                foreach (var acc in result.data)
                {
                    if (acc.Id == Guid.Empty || string.IsNullOrEmpty(acc.AccountCode)) continue;

                    _code.DatabaseInsertSafe(ConnStr, @"
                        IF EXISTS (SELECT 1 FROM Accounting_Nexaacc_Accounts WHERE Nexaacc_AccountId = @id)
                            UPDATE Accounting_Nexaacc_Accounts
                            SET Account_Code = @code, Account_Name = @name, Account_Name_En = @nameEn,
                                Account_Type = @type, Account_Type_Value = @typeVal,
                                Parent_Account_Id = @parentId, Account_Level = @level,
                                Is_Active = @isActive, Is_System_Account = @isSys,
                                Description = @desc, Last_Synced = GETDATE()
                            WHERE Nexaacc_AccountId = @id
                        ELSE
                            INSERT INTO Accounting_Nexaacc_Accounts
                                (Nexaacc_AccountId, Account_Code, Account_Name, Account_Name_En,
                                 Account_Type, Account_Type_Value, Parent_Account_Id, Account_Level,
                                 Is_Active, Is_System_Account, Description, Last_Synced)
                            VALUES (@id, @code, @name, @nameEn, @type, @typeVal, @parentId, @level,
                                    @isActive, @isSys, @desc, GETDATE())",
                        new Dictionary<string, object>
                        {
                            { "@id", acc.Id },
                            { "@code", acc.AccountCode.Trim() },
                            { "@name", acc.AccountName ?? "" },
                            { "@nameEn", acc.AccountNameEn ?? "" },
                            { "@type", acc.AccountType ?? "" },
                            { "@typeVal", acc.AccountTypeValue },
                            { "@parentId", (object)acc.ParentAccountId ?? DBNull.Value },
                            { "@level", acc.Level },
                            { "@isActive", acc.IsActive },
                            { "@isSys", acc.IsSystemAccount },
                            { "@desc", acc.Description ?? "" }
                        });
                    upserted++;
                }

                // Fix legacy wrong codes before matching
                int fixed5d = FixLegacyAccountCodes();

                // Auto-match: update Accounting_Account_Mapping with matched GUIDs (exact match only)
                int matched = AutoMatchMappings();

                string msg = $"Sync ผังบั���ชีสำเร็จ — ดึงมา {upserted} บัญชี, จับคู่ mapping ได้ {matched} รายการ";
                if (fixed5d > 0)
                    msg += $", แก้รหัสเก่า {fixed5d} รายการ";

                // Count unmatched for warning
                DataTable unmatchedDt = _code.DatabaseQuerySafe(ConnStr,
                    @"SELECT TakeTime_Code, Nexaacc_AccountCode FROM Accounting_Account_Mapping
                      WHERE Is_Active = 1 AND (Nexaacc_AccountId IS NULL OR Nexaacc_AccountCode = '')", null);
                int unmatched = unmatchedDt?.Rows.Count ?? 0;
                if (unmatched > 0)
                    msg += $"\n⚠ ยังไม่ได้จับคู่ {unmatched} รายการ — กรุณาเลือกบัญชีจาก dropdown ในหน้า Mapping";

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", msg },
                    { "totalAccounts", upserted },
                    { "matched", matched },
                    { "unmatched", unmatched }
                };
            }
            catch (AggregateException aex)
            {
                var inner = aex.InnerException ?? aex;
                if (inner is Integration.AccountingApiException apiEx)
                {
                    if (apiEx.StatusCode == 401)
                        return new Dictionary<string, object> { { "success", false }, { "message", "API Key ไม่ถูกต้องหรือหมดอายุ (401)" } };
                    return new Dictionary<string, object> { { "success", false }, { "message", $"Nexaacc API Error ({apiEx.StatusCode}): {apiEx.ResponseBody}" } };
                }
                return new Dictionary<string, object> { { "success", false }, { "message", inner.Message } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private int AutoMatchMappings()
        {
            DataTable mappings = _code.DatabaseQuerySafe(ConnStr,
                "SELECT ID, TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Nexaacc_AccountId FROM Accounting_Account_Mapping WHERE Is_Active = 1", null);

            if (mappings == null) return 0;

            int matched = 0;
            foreach (DataRow row in mappings.Rows)
            {
                // ผูกไว้แล้ว → ข้าม
                if (row["Nexaacc_AccountId"] != DBNull.Value) continue;

                int mappingId = Convert.ToInt32(row["ID"]);
                string accountCode = (row["Nexaacc_AccountCode"]?.ToString() ?? "").Trim();

                Guid? matchedId = null;
                string matchedCode = null;

                // Pass 1: จับคู่ด้วยรหัสบัญชีแบบเป๊ะ (ไม่ทำ prefix match — กัน "1111" → "11111")
                if (!string.IsNullOrEmpty(accountCode))
                {
                    DataTable found = _code.DatabaseQuerySafe(ConnStr,
                        "SELECT TOP 1 Nexaacc_AccountId, Account_Code FROM Accounting_Nexaacc_Accounts WHERE Account_Code = @code AND Is_Active = 1",
                        new Dictionary<string, object> { { "@code", accountCode } });
                    if (found?.Rows.Count > 0)
                    {
                        matchedId = (Guid)found.Rows[0]["Nexaacc_AccountId"];
                        matchedCode = found.Rows[0]["Account_Code"]?.ToString();
                    }
                }

                // Pass 2: จับคู่ด้วยชื่อบัญชีแบบเป๊ะ "และไม่กำกวม" (เจอบัญชีเดียวเท่านั้น)
                // ใช้ TakeTime_Description ↔ Account_Name / Account_Name_En — auto เฉพาะตัวที่ชื่อตรงพอดี
                // ตัวที่ชื่อไม่ตรง/ซ้ำ จะเว้นไว้ให้เลือกเองในหน้า Mapping (กันจับคู่ผิดเงียบ ๆ)
                if (matchedId == null)
                {
                    string desc = (row["TakeTime_Description"]?.ToString() ?? "").Trim();
                    if (!string.IsNullOrEmpty(desc))
                    {
                        DataTable byName = _code.DatabaseQuerySafe(ConnStr,
                            @"SELECT Nexaacc_AccountId, Account_Code FROM Accounting_Nexaacc_Accounts
                              WHERE Is_Active = 1
                                AND (LTRIM(RTRIM(Account_Name)) = @name OR LTRIM(RTRIM(Account_Name_En)) = @name)",
                            new Dictionary<string, object> { { "@name", desc } });
                        if (byName != null && byName.Rows.Count == 1)
                        {
                            matchedId = (Guid)byName.Rows[0]["Nexaacc_AccountId"];
                            matchedCode = byName.Rows[0]["Account_Code"]?.ToString();
                        }
                    }
                }

                if (matchedId != null)
                {
                    // เซ็ตทั้ง Id และ Code (ถ้ายังว่าง) เพื่อให้ resolve ด้วยรหัสในอนาคต + แสดงในหน้า UI ได้
                    _code.DatabaseInsertSafe(ConnStr,
                        @"UPDATE Accounting_Account_Mapping
                          SET Nexaacc_AccountId = @accId,
                              Nexaacc_AccountCode = CASE WHEN (Nexaacc_AccountCode IS NULL OR Nexaacc_AccountCode = '')
                                                         THEN @code ELSE Nexaacc_AccountCode END
                          WHERE ID = @id",
                        new Dictionary<string, object>
                        {
                            { "@accId", matchedId.Value },
                            { "@code", matchedCode ?? "" },
                            { "@id", mappingId }
                        });
                    matched++;
                }
            }
            return matched;
        }

        private int FixLegacyAccountCodes()
        {
            // Fix old 3-4 digit codes that don't match NextAcc's 5-digit codes
            // Only fix if the old code doesn't exist in cache but a correct 5-digit code does
            var fixes = new Dictionary<string, string>
            {
                // Asset codes known from NextAcc chart
                { "111", "11111" },   // เงินสด → exact leaf account
                { "1150", "11500" },  // สินค้าคงเหลือ
                { "1160", "11610" },  // ภาษีซื้อ → ภาษีซื้อ ภ.พ.30
            };

            int fixCount = 0;
            foreach (var fix in fixes)
            {
                // Only fix if old code is still in mapping AND new code exists in cache AND old code doesn't exist in cache
                DataTable hasOld = _code.DatabaseQuerySafe(ConnStr,
                    "SELECT TOP 1 1 FROM Accounting_Nexaacc_Accounts WHERE Account_Code = @code",
                    new Dictionary<string, object> { { "@code", fix.Key } });
                DataTable hasNew = _code.DatabaseQuerySafe(ConnStr,
                    "SELECT TOP 1 1 FROM Accounting_Nexaacc_Accounts WHERE Account_Code = @code",
                    new Dictionary<string, object> { { "@code", fix.Value } });

                if ((hasOld == null || hasOld.Rows.Count == 0) && hasNew?.Rows.Count > 0)
                {
                    _code.DatabaseInsertSafe(ConnStr,
                        @"UPDATE Accounting_Account_Mapping
                          SET Nexaacc_AccountCode = @newCode, Nexaacc_AccountId = NULL
                          WHERE Nexaacc_AccountCode = @oldCode AND Is_Active = 1",
                        new Dictionary<string, object> { { "@oldCode", fix.Key }, { "@newCode", fix.Value } });
                    fixCount++;
                }
            }

            // Clear codes that are definitely wrong (old 4-digit bank codes that don't exist in NextAcc)
            // These will show as "Not Linked" and user picks correct one from dropdown
            string[] legacyBankCodes = { "1111", "1112", "1113", "1114" };
            foreach (string code in legacyBankCodes)
            {
                DataTable exists = _code.DatabaseQuerySafe(ConnStr,
                    "SELECT TOP 1 1 FROM Accounting_Nexaacc_Accounts WHERE Account_Code = @code",
                    new Dictionary<string, object> { { "@code", code } });

                if (exists == null || exists.Rows.Count == 0)
                {
                    DataTable affected = _code.DatabaseQuerySafe(ConnStr,
                        "SELECT COUNT(*) AS cnt FROM Accounting_Account_Mapping WHERE Nexaacc_AccountCode = @code AND Is_Active = 1",
                        new Dictionary<string, object> { { "@code", code } });
                    int cnt = affected?.Rows.Count > 0 ? Convert.ToInt32(affected.Rows[0]["cnt"]) : 0;
                    if (cnt > 0)
                    {
                        _code.DatabaseInsertSafe(ConnStr,
                            @"UPDATE Accounting_Account_Mapping
                              SET Nexaacc_AccountCode = '', Nexaacc_AccountId = NULL
                              WHERE Nexaacc_AccountCode = @code AND Is_Active = 1",
                            new Dictionary<string, object> { { "@code", code } });
                        fixCount += cnt;
                    }
                }
            }

            // Also clear old liability/revenue/expense codes that don't exist
            string[] legacyOtherCodes = { "2110", "21510", "2140", "2150", "2160", "411", "4200", "4210", "4300", "4900", "5100", "5200", "5210", "5300", "5400", "5500", "5600", "5900" };
            foreach (string code in legacyOtherCodes)
            {
                DataTable exists = _code.DatabaseQuerySafe(ConnStr,
                    "SELECT TOP 1 1 FROM Accounting_Nexaacc_Accounts WHERE Account_Code = @code",
                    new Dictionary<string, object> { { "@code", code } });

                if (exists == null || exists.Rows.Count == 0)
                {
                    DataTable affected = _code.DatabaseQuerySafe(ConnStr,
                        "SELECT COUNT(*) AS cnt FROM Accounting_Account_Mapping WHERE Nexaacc_AccountCode = @code AND Is_Active = 1",
                        new Dictionary<string, object> { { "@code", code } });
                    int cnt = affected?.Rows.Count > 0 ? Convert.ToInt32(affected.Rows[0]["cnt"]) : 0;
                    if (cnt > 0)
                    {
                        _code.DatabaseInsertSafe(ConnStr,
                            @"UPDATE Accounting_Account_Mapping
                              SET Nexaacc_AccountCode = '', Nexaacc_AccountId = NULL
                              WHERE Nexaacc_AccountCode = @code AND Is_Active = 1",
                            new Dictionary<string, object> { { "@code", code } });
                        fixCount += cnt;
                    }
                }
            }

            return fixCount;
        }

        private Dictionary<string, object> GetNexaaccAccounts()
        {
            try
            {
                EnsureAccountsCacheTable();

                string typeFilter = Request.QueryString["type"] ?? "";
                string sql = @"SELECT Nexaacc_AccountId, Account_Code, Account_Name, Account_Name_En,
                                      Account_Type, Account_Type_Value, Account_Level, Is_Active, Last_Synced
                               FROM Accounting_Nexaacc_Accounts WHERE Is_Active = 1";
                Dictionary<string, object> sqlParams = null;

                if (!string.IsNullOrEmpty(typeFilter))
                {
                    sql += " AND Account_Type = @type";
                    sqlParams = new Dictionary<string, object> { { "@type", typeFilter } };
                }
                sql += " ORDER BY Account_Code";

                DataTable dt = _code.DatabaseQuerySafe(ConnStr, sql, sqlParams);
                var items = new List<Dictionary<string, object>>();
                DateTime? lastSync = null;

                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        items.Add(new Dictionary<string, object>
                        {
                            { "id", row["Nexaacc_AccountId"].ToString() },
                            { "code", row["Account_Code"]?.ToString() },
                            { "name", row["Account_Name"]?.ToString() },
                            { "nameEn", row["Account_Name_En"]?.ToString() },
                            { "type", row["Account_Type"]?.ToString() },
                            { "typeValue", Convert.ToInt32(row["Account_Type_Value"]) },
                            { "level", Convert.ToInt32(row["Account_Level"]) }
                        });
                        if (lastSync == null && row["Last_Synced"] != DBNull.Value)
                            lastSync = Convert.ToDateTime(row["Last_Synced"]);
                    }
                }

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "items", items },
                    { "total", items.Count },
                    { "lastSync", lastSync?.ToString("dd/MM/yyyy HH:mm") ?? "ยังไม่เคย sync" }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message }, { "items", new List<object>() } };
            }
        }

        private Dictionary<string, object> GetAccountMappings()
        {
            try
            {
                DataTable dt = _code.DatabaseQuerySafe(ConnStr,
                    @"SELECT ID, TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Nexaacc_AccountId, Mapping_Type, Is_Active
                      FROM Accounting_Account_Mapping ORDER BY Mapping_Type, TakeTime_Code", null);

                var items = new List<Dictionary<string, object>>();
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        items.Add(new Dictionary<string, object>
                        {
                            { "id", Convert.ToInt32(row["ID"]) },
                            { "code", row["TakeTime_Code"]?.ToString() },
                            { "description", row["TakeTime_Description"]?.ToString() },
                            { "accountCode", row["Nexaacc_AccountCode"]?.ToString() },
                            { "accountId", row["Nexaacc_AccountId"] != DBNull.Value ? row["Nexaacc_AccountId"].ToString() : "" },
                            { "mappingType", row["Mapping_Type"]?.ToString() },
                            { "isActive", Convert.ToBoolean(row["Is_Active"]) }
                        });
                    }
                }

                return new Dictionary<string, object> { { "success", true }, { "items", items } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> UpdateAccountMapping()
        {
            try
            {
                int id = int.Parse(Request.QueryString["id"] ?? "0");
                string newCode = Request.QueryString["accountCode"] ?? "";
                string accountId = Request.QueryString["accountId"] ?? "";

                if (id <= 0 || string.IsNullOrEmpty(newCode))
                    return new Dictionary<string, object> { { "success", false }, { "message", "ต้องระบุ id และ accountCode" } };

                if (!string.IsNullOrEmpty(accountId) && Guid.TryParse(accountId, out Guid parsedId))
                {
                    _code.DatabaseInsertSafe(ConnStr,
                        "UPDATE Accounting_Account_Mapping SET Nexaacc_AccountCode = @code, Nexaacc_AccountId = @accId WHERE ID = @id",
                        new Dictionary<string, object> { { "@code", newCode }, { "@accId", parsedId }, { "@id", id } });
                    return new Dictionary<string, object> { { "success", true }, { "message", $"จับคู่ {newCode} สำเร็จ (Account ID linked)" } };
                }
                else
                {
                    _code.DatabaseInsertSafe(ConnStr,
                        "UPDATE Accounting_Account_Mapping SET Nexaacc_AccountCode = @code, Nexaacc_AccountId = NULL WHERE ID = @id",
                        new Dictionary<string, object> { { "@code", newCode }, { "@id", id } });
                    return new Dictionary<string, object> { { "success", true }, { "message", $"อัปเดต Account Code เป็น {newCode} แล้ว — กด 'Sync บัญชี' เพื่อจับคู่ Account ID" } };
                }
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> PreviewAutoSyncCleanup()
        {
            try
            {
                var sync = new Integration.AccountingSyncService(ConnStr);
                DataTable dt = sync.GetAutoSyncCleanupPreview();
                var items = new List<Dictionary<string, object>>();
                int total = 0;
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        int count = Convert.ToInt32(row["Count"]);
                        total += count;
                        items.Add(new Dictionary<string, object>
                        {
                            { "status", row["Status"]?.ToString() },
                            { "actionType", row["Action_Type"]?.ToString() },
                            { "count", count }
                        });
                    }
                }
                return new Dictionary<string, object> { { "success", true }, { "total", total }, { "items", items } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> CleanupOldAutoSync()
        {
            try
            {
                var sync = new Integration.AccountingSyncService(ConnStr);
                int cancelled = sync.CancelOldAutoSyncEntries();
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", $"ยกเลิก auto-sync entries จำนวน {cancelled} รายการ เรียบร้อยแล้ว" },
                    { "cancelled", cancelled }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        // ──────────────────────────────────────────────
        // Payment Method → Account Mapping (Account_Paid_How)
        // ──────────────────────────────────────────────

        private bool ColumnExists(string table, string column)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(ConnStr,
                    "SELECT COL_LENGTH(@t, @c) AS L",
                    new Dictionary<string, object> { { "@t", "dbo." + table }, { "@c", column } });
                return dt != null && dt.Rows.Count > 0 && dt.Rows[0]["L"] != DBNull.Value;
            }
            catch { return false; }
        }

        private bool TableExists(string table)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(ConnStr,
                    "SELECT OBJECT_ID(@t, 'U') AS O",
                    new Dictionary<string, object> { { "@t", "dbo." + table } });
                return dt != null && dt.Rows.Count > 0 && dt.Rows[0]["O"] != DBNull.Value;
            }
            catch { return false; }
        }

        private Dictionary<string, object> GetPaidHowMapping()
        {
            try
            {
                // คอลัมน์เสริม (มีหรือไม่มีก็ได้): Nexaacc_BankAccountId (PHASE19_21), Channel_Type (PHASE19_20 ของทีมช่องทาง)
                bool hasBankCol = ColumnExists("Account_Paid_How", "Nexaacc_BankAccountId");
                bool hasChannelType = ColumnExists("Account_Paid_How", "Channel_Type");
                bool hasBankCache = TableExists("Accounting_Nexaacc_BankAccounts");
                bool hasCoaCache = TableExists("Accounting_Nexaacc_Accounts");

                string sql = @"SELECT p.ID, p.Paid_How,
                             ISNULL(CAST(p.Nexaacc_AccountId AS NVARCHAR(50)), '') AS Nexaacc_AccountId,
                             ISNULL(p.Nexaacc_AccountCode, '') AS Nexaacc_AccountCode,
                             p.Status"
                    + (hasBankCol ? ", ISNULL(CAST(p.Nexaacc_BankAccountId AS NVARCHAR(50)), '') AS BankId" : ", '' AS BankId")
                    + (hasChannelType ? ", ISNULL(CAST(p.Channel_Type AS NVARCHAR(50)), '') AS ChannelType" : ", '' AS ChannelType")
                    + (hasCoaCache ? ", a.Account_Type AS AccType, a.Account_Name AS AccName, a.Is_Active AS AccActive, CASE WHEN a.Nexaacc_AccountId IS NULL THEN 0 ELSE 1 END AS AccFound"
                                   : ", NULL AS AccType, NULL AS AccName, NULL AS AccActive, 0 AS AccFound")
                    + (hasBankCol && hasBankCache ? ", b.Bank_Name, b.Account_Number, b.Is_Active AS BankActive, b.Is_Present AS BankPresent"
                                                  : ", NULL AS Bank_Name, NULL AS Account_Number, NULL AS BankActive, NULL AS BankPresent")
                    + " FROM Account_Paid_How p"
                    + (hasCoaCache ? " LEFT JOIN Accounting_Nexaacc_Accounts a ON a.Nexaacc_AccountId = p.Nexaacc_AccountId" : "")
                    + (hasBankCol && hasBankCache ? " LEFT JOIN Accounting_Nexaacc_BankAccounts b ON b.Nexaacc_BankAccountId = p.Nexaacc_BankAccountId" : "")
                    + " WHERE p.Status = 'True' ORDER BY p.ID";

                DataTable dt = _code.DatabaseQuerySafe(ConnStr, sql, null);

                var items = new List<Dictionary<string, object>>();
                if (dt?.Rows.Count > 0)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        string accId = row["Nexaacc_AccountId"]?.ToString() ?? "";
                        bool linked = accId.Length > 0 && accId != Guid.Empty.ToString();
                        string accType = row["AccType"] == DBNull.Value ? "" : row["AccType"].ToString();
                        string chType = row["ChannelType"]?.ToString() ?? "";
                        string warn = PaidHowAccountWarning(linked, hasCoaCache && Convert.ToInt32(row["AccFound"]) == 1,
                            hasCoaCache, accType,
                            row["AccActive"] == DBNull.Value || Convert.ToBoolean(row["AccActive"]),
                            row["BankActive"] == DBNull.Value || Convert.ToBoolean(row["BankActive"]),
                            row["BankPresent"] == DBNull.Value || Convert.ToBoolean(row["BankPresent"]),
                            chType, row["Paid_How"]?.ToString() ?? "");
                        items.Add(new Dictionary<string, object>
                        {
                            { "id", Convert.ToInt32(row["ID"]) },
                            { "name", row["Paid_How"]?.ToString() ?? "" },
                            { "accountId", accId },
                            { "accountCode", row["Nexaacc_AccountCode"]?.ToString() ?? "" },
                            { "accountName", row["AccName"] == DBNull.Value ? "" : row["AccName"].ToString() },
                            { "accountType", accType },
                            { "bankAccountId", row["BankId"]?.ToString() ?? "" },
                            { "bankName", row["Bank_Name"] == DBNull.Value ? "" : row["Bank_Name"].ToString() },
                            { "bankNumber", row["Account_Number"] == DBNull.Value ? "" : row["Account_Number"].ToString() },
                            { "channelType", chType },
                            { "warning", warn ?? "" }
                        });
                    }
                }
                return new Dictionary<string, object> { { "success", true }, { "items", items }, { "hasBankColumn", hasBankCol } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        /// <summary>ช่องทาง "เงินเข้าจากลูกค้า" (เกตเวย์/OTA/e-Wallet/บัตร/QR) — ต้องลงบัญชีสินทรัพย์เท่านั้น.
        /// ดูจาก Channel_Type (ถ้ามี) หรือชื่อแถว</summary>
        private static bool IsCustomerInflowChannel(string channelType, string paidHowName)
        {
            string t = (channelType ?? "").Trim().ToUpperInvariant();
            string[] inflowTypes = { "GATEWAY", "ONLINE", "OTA", "EWALLET", "E_WALLET", "WALLET", "CARD", "CREDIT_CARD", "PROMPTPAY", "QR" };
            foreach (string k in inflowTypes) if (t == k || t.Contains(k)) return true;
            string n = (paidHowName ?? "").ToLowerInvariant();
            return n.Contains("omise") || n.Contains("payso") || n.Contains("ออนไลน์") || n.Contains("online")
                || n.Contains("agoda") || n.Contains("booking") || n.Contains("expedia") || n.Contains("trip.com")
                || n.Contains("wallet") || n.Contains("promptpay") || n.Contains("พร้อมเพย์") || n.Contains("บัตร");
        }

        /// <summary>ข้อความเตือน mapping แหล่งเงิน (null = ปกติ)</summary>
        private static string PaidHowAccountWarning(bool linked, bool foundInCoa, bool haveCoa, string accType,
            bool accActive, bool bankActive, bool bankPresent, string channelType, string paidHowName)
        {
            if (!linked) return "ยังไม่ผูก — NextAcc จะเดาบัญชีเงินจากวิธีชำระเอง";
            if (haveCoa && !foundInCoa) return "บัญชีที่ผูกไม่อยู่ในผังบัญชี NextAcc ล่าสุด (ถูกลบ/เปลี่ยน?) — กดดึงรายการแล้วเลือกใหม่";
            if (!accActive) return "บัญชีที่ผูกถูกปิดใช้งานใน NextAcc";
            if (!bankPresent) return "บัญชีธนาคาร/กระเป๋าเงินที่เลือกไม่อยู่ใน NextAcc แล้ว";
            if (!bankActive) return "บัญชีธนาคาร/กระเป๋าเงินที่เลือกถูกปิดใช้งานใน NextAcc";
            string t = (accType ?? "").Trim();
            if (t.Length > 0 && !t.Equals("Asset", StringComparison.OrdinalIgnoreCase) && !t.Equals("Liability", StringComparison.OrdinalIgnoreCase))
                return $"ผูกกับบัญชีประเภท {t} — แหล่งเงินต้องเป็นสินทรัพย์ (เงินสด/ธนาคาร/e-Wallet/พักเงิน) หรือหนี้สิน (เจ้าหนี้กรรมการ) เท่านั้น";
            if (t.Equals("Liability", StringComparison.OrdinalIgnoreCase) && IsCustomerInflowChannel(channelType, paidHowName))
                return "ช่องทางรับเงินลูกค้า/เกตเวย์/OTA ผูกกับบัญชีหนี้สิน — ควรเป็นบัญชีพักเงิน/ลูกหนี้ผู้ให้บริการ (11xxx)";
            return null;
        }

        private Dictionary<string, object> UpdatePaidHowAccount()
        {
            try
            {
                int id = int.Parse(Request.QueryString["id"]);
                string accountId = Request.QueryString["accountId"] ?? "";
                string accountCode = Request.QueryString["accountCode"] ?? "";
                string bankAccountId = Request.QueryString["bankAccountId"] ?? "";

                Guid accGuid;
                if (string.IsNullOrEmpty(accountId) || !Guid.TryParse(accountId, out accGuid) || accGuid == Guid.Empty)
                    return new Dictionary<string, object> { { "success", false }, { "message", "ไม่ได้เลือกบัญชี" } };

                DataTable name = _code.DatabaseQuerySafe(ConnStr,
                    "SELECT Paid_How FROM Account_Paid_How WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", id } });
                string paidHowName = name?.Rows.Count > 0 ? name.Rows[0]["Paid_How"]?.ToString() : "";
                string channelType = "";
                if (ColumnExists("Account_Paid_How", "Channel_Type"))
                {
                    var ct = _code.DatabaseQuerySafe(ConnStr,
                        "SELECT ISNULL(CAST(Channel_Type AS NVARCHAR(50)), '') FROM Account_Paid_How WHERE ID = @id",
                        new Dictionary<string, object> { { "@id", id } });
                    if (ct?.Rows.Count > 0) channelType = ct.Rows[0][0]?.ToString() ?? "";
                }

                // ── ตรวจประเภทบัญชีจาก cache ผังบัญชี: แหล่งเงินห้ามเป็นรายได้/ค่าใช้จ่าย/ทุน ──
                string accType = null;
                if (TableExists("Accounting_Nexaacc_Accounts"))
                {
                    var at = _code.DatabaseQuerySafe(ConnStr,
                        "SELECT TOP 1 Account_Type, Account_Code FROM Accounting_Nexaacc_Accounts WHERE Nexaacc_AccountId = @id",
                        new Dictionary<string, object> { { "@id", accGuid } });
                    if (at?.Rows.Count > 0)
                    {
                        accType = at.Rows[0]["Account_Type"]?.ToString();
                        if (string.IsNullOrEmpty(accountCode)) accountCode = at.Rows[0]["Account_Code"]?.ToString() ?? "";
                    }
                }
                if (!string.IsNullOrEmpty(accType)
                    && !accType.Equals("Asset", StringComparison.OrdinalIgnoreCase)
                    && !accType.Equals("Liability", StringComparison.OrdinalIgnoreCase))
                    return new Dictionary<string, object>
                    {
                        { "success", false },
                        { "message", $"บัญชี {accountCode} เป็นประเภท {accType} — แหล่งเงินต้องเป็นบัญชีสินทรัพย์ (เงินสด/ธนาคาร/e-Wallet/พักเงินเกตเวย์/ลูกหนี้ OTA) หรือหนี้สิน (เจ้าหนี้กรรมการ) เท่านั้น" }
                    };
                string warn = null;
                if (accType != null && accType.Equals("Liability", StringComparison.OrdinalIgnoreCase)
                    && IsCustomerInflowChannel(channelType, paidHowName))
                    warn = "⚠ \"" + paidHowName + "\" เป็นช่องทางรับเงินจากลูกค้า แต่ผูกกับบัญชีหนี้สิน — โดยปกติควรเป็นบัญชีพักเงิน/ลูกหนี้ผู้ให้บริการ (11xxx)";

                // ── เลือกจาก "กระเป๋าเงิน" (NextAcc BankAccount) → ต้องยังเปิดใช้ และผังที่ผูกต้องตรงกับที่ส่งมา ──
                Guid bankGuid = Guid.Empty;
                bool hasBankCol = ColumnExists("Account_Paid_How", "Nexaacc_BankAccountId");
                if (!string.IsNullOrEmpty(bankAccountId) && Guid.TryParse(bankAccountId, out bankGuid) && bankGuid != Guid.Empty
                    && TableExists("Accounting_Nexaacc_BankAccounts"))
                {
                    var bk = _code.DatabaseQuerySafe(ConnStr,
                        @"SELECT TOP 1 Is_Active, Is_Present, CAST(Linked_Account_Id AS NVARCHAR(50)) AS Linked, Bank_Name, Account_Number
                            FROM Accounting_Nexaacc_BankAccounts WHERE Nexaacc_BankAccountId = @id",
                        new Dictionary<string, object> { { "@id", bankGuid } });
                    if (bk == null || bk.Rows.Count == 0)
                        return new Dictionary<string, object> { { "success", false }, { "message", "ไม่พบบัญชีธนาคาร/กระเป๋าเงินนี้ใน cache — กด \"ดึงรายการจาก NextAcc\" ก่อน" } };
                    if (!Convert.ToBoolean(bk.Rows[0]["Is_Active"]) || !Convert.ToBoolean(bk.Rows[0]["Is_Present"]))
                        return new Dictionary<string, object> { { "success", false }, { "message", "บัญชีธนาคาร/กระเป๋าเงินนี้ถูกปิดใช้งานหรือถูกลบใน NextAcc แล้ว — เลือกบัญชีอื่น" } };
                    string linked = bk.Rows[0]["Linked"] == DBNull.Value ? "" : bk.Rows[0]["Linked"].ToString();
                    if (!string.Equals(linked, accGuid.ToString(), StringComparison.OrdinalIgnoreCase))
                        return new Dictionary<string, object> { { "success", false }, { "message", "บัญชีธนาคารนี้ผูกผังบัญชีคนละตัวกับที่เลือก — กดดึงรายการจาก NextAcc ใหม่แล้วเลือกอีกครั้ง" } };
                }
                else bankGuid = Guid.Empty;

                if (hasBankCol)
                    _code.DatabaseInsertSafe(ConnStr,
                        @"UPDATE Account_Paid_How SET Nexaacc_AccountId = @accId, Nexaacc_AccountCode = @accCode,
                                 Nexaacc_BankAccountId = @bankId WHERE ID = @id",
                        new Dictionary<string, object>
                        {
                            { "@accId", accGuid },
                            { "@accCode", accountCode },
                            { "@bankId", bankGuid == Guid.Empty ? (object)DBNull.Value : bankGuid },
                            { "@id", id }
                        });
                else
                    _code.DatabaseInsertSafe(ConnStr,
                        @"UPDATE Account_Paid_How SET Nexaacc_AccountId = @accId, Nexaacc_AccountCode = @accCode WHERE ID = @id",
                        new Dictionary<string, object>
                        {
                            { "@accId", accGuid },
                            { "@accCode", accountCode },
                            { "@id", id }
                        });

                try
                {
                    _code.Logs(ConnStr, "AccountingConfig",
                        $"แหล่งเงิน \"{paidHowName}\" (#{id}) → บัญชี NextAcc {accountCode} ({accGuid})"
                        + (bankGuid != Guid.Empty ? $" กระเป๋าเงิน {bankGuid}" : "")
                        + $" โดย {Session["UserName"] ?? "?"}", Session["UserName"]?.ToString() ?? "SYSTEM");
                }
                catch { }

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", $"ผูก \"{paidHowName}\" กับบัญชี {accountCode} เรียบร้อย" + (warn != null ? "\n" + warn : "") }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        // ──────────────────────────────────────────────
        // "กระเป๋าเงิน" จาก NextAcc (BankAccount + ผังเงินสด/พักเงิน) → dropdown แหล่งเงิน
        // ──────────────────────────────────────────────

        /// <summary>
        /// ปุ่ม "ดึงรายการจาก NextAcc": (1) Sync ผังบัญชี (2) GET /api/companies/{cid}/bank/accounts →
        /// cache Accounting_Nexaacc_BankAccounts (3) ซ่อมแหล่งเงินที่เลือกกระเป๋าเงินไว้ แต่ฝั่ง NextAcc
        /// เปลี่ยนผังที่ผูก (LinkedAccountId) → อัปเดต Nexaacc_AccountId ให้ตรง (JE ลงตามผังที่ผูกจริง)
        /// </summary>
        private Dictionary<string, object> SyncNexaaccWallets()
        {
            try
            {
                var config = new Integration.AccountingConfig(ConnStr);
                if (!config.IsConfigured || !config.CanUseCompanyEndpoints)
                    return new Dictionary<string, object> { { "success", false }, { "message", "ต้องตั้ง Base URL + API Key + Company ID (company endpoints เปิด) ก่อน" } };

                var coa = SyncChartOfAccounts();
                string coaMsg = coa != null && coa.ContainsKey("message") ? coa["message"]?.ToString() : "";
                bool coaOk = coa != null && coa.ContainsKey("success") && Convert.ToBoolean(coa["success"]);

                if (!TableExists("Accounting_Nexaacc_BankAccounts"))
                    return new Dictionary<string, object>
                    {
                        { "success", false },
                        { "message", (coaOk ? "✓ " + coaMsg + "\n" : "") + "ยังไม่มีตาราง Accounting_Nexaacc_BankAccounts — รัน migration PHASE19_21 ก่อน" }
                    };

                var client = new Integration.AccountingApiClient(config, ConnStr);
                var res = System.Threading.Tasks.Task.Run(() => client.GetBankAccountsAsync()).Result;
                if (res == null || res.data == null)
                    return new Dictionary<string, object> { { "success", false }, { "message", "ดึงบัญชีธนาคาร/กระเป๋าเงินจาก NextAcc ไม่สำเร็จ" + (res?.message != null ? ": " + res.message : "") } };

                _code.DatabaseInsertSafe(ConnStr, "UPDATE Accounting_Nexaacc_BankAccounts SET Is_Present = 0", null);
                int n = 0, active = 0;
                foreach (var b in res.data)
                {
                    if (b == null || b.Id == Guid.Empty) continue;
                    _code.DatabaseInsertSafe(ConnStr, @"
                        IF EXISTS (SELECT 1 FROM Accounting_Nexaacc_BankAccounts WHERE Nexaacc_BankAccountId = @id)
                            UPDATE Accounting_Nexaacc_BankAccounts
                               SET Account_Name = @name, Bank_Name = @bank, Account_Number = @num, Branch_Name = @branch,
                                   Account_Type = @type, Currency = @cur, Linked_Account_Id = @lid,
                                   Linked_Account_Code = @lcode, Linked_Account_Name = @lname,
                                   Is_Active = @active, Is_Present = 1, Last_Synced = GETDATE()
                             WHERE Nexaacc_BankAccountId = @id
                        ELSE
                            INSERT INTO Accounting_Nexaacc_BankAccounts
                                (Nexaacc_BankAccountId, Account_Name, Bank_Name, Account_Number, Branch_Name, Account_Type,
                                 Currency, Linked_Account_Id, Linked_Account_Code, Linked_Account_Name, Is_Active, Is_Present, Last_Synced)
                            VALUES (@id, @name, @bank, @num, @branch, @type, @cur, @lid, @lcode, @lname, @active, 1, GETDATE())",
                        new Dictionary<string, object>
                        {
                            { "@id", b.Id },
                            { "@name", (object)b.AccountName ?? DBNull.Value },
                            { "@bank", (object)b.BankName ?? DBNull.Value },
                            { "@num", (object)b.AccountNumber ?? DBNull.Value },
                            { "@branch", (object)b.BranchName ?? DBNull.Value },
                            { "@type", (object)b.AccountType ?? DBNull.Value },
                            { "@cur", (object)b.Currency ?? DBNull.Value },
                            { "@lid", b.LinkedAccountId.HasValue && b.LinkedAccountId.Value != Guid.Empty ? (object)b.LinkedAccountId.Value : DBNull.Value },
                            { "@lcode", (object)b.LinkedAccountCode ?? DBNull.Value },
                            { "@lname", (object)b.LinkedAccountName ?? DBNull.Value },
                            { "@active", b.IsActive }
                        });
                    n++;
                    if (b.IsActive) active++;
                }

                // ซ่อม mapping: เลือกกระเป๋าเงินไว้ แต่ผังที่ผูกฝั่ง NextAcc เปลี่ยน → ตามผังใหม่
                int healed = 0;
                if (ColumnExists("Account_Paid_How", "Nexaacc_BankAccountId"))
                {
                    var drift = _code.DatabaseQuerySafe(ConnStr,
                        @"SELECT p.ID, p.Paid_How, b.Linked_Account_Id, b.Linked_Account_Code
                            FROM Account_Paid_How p
                            JOIN Accounting_Nexaacc_BankAccounts b ON b.Nexaacc_BankAccountId = p.Nexaacc_BankAccountId
                           WHERE p.Status = 'True' AND b.Is_Present = 1 AND b.Linked_Account_Id IS NOT NULL
                             AND (p.Nexaacc_AccountId IS NULL OR p.Nexaacc_AccountId <> b.Linked_Account_Id)", null);
                    if (drift != null)
                        foreach (DataRow r in drift.Rows)
                        {
                            _code.DatabaseInsertSafe(ConnStr,
                                "UPDATE Account_Paid_How SET Nexaacc_AccountId = @acc, Nexaacc_AccountCode = @code WHERE ID = @id",
                                new Dictionary<string, object>
                                {
                                    { "@acc", r["Linked_Account_Id"] }, { "@code", r["Linked_Account_Code"] ?? "" }, { "@id", r["ID"] }
                                });
                            healed++;
                            try
                            {
                                _code.Logs(ConnStr, "AccountingConfig",
                                    $"แหล่งเงิน \"{r["Paid_How"]}\" ตามผังที่ผูกกับกระเป๋าเงินใน NextAcc ใหม่ → {r["Linked_Account_Code"]}", "SYSTEM");
                            }
                            catch { }
                        }
                }

                string msg = (coaOk ? "✓ " : "⚠ ") + coaMsg
                    + $"\n✓ ดึงบัญชีธนาคาร/กระเป๋าเงินจาก NextAcc {n} บัญชี (เปิดใช้ {active})"
                    + (healed > 0 ? $"\n✓ ซ่อมแหล่งเงินที่ผังใน NextAcc เปลี่ยน {healed} รายการ" : "");
                return new Dictionary<string, object> { { "success", true }, { "message", msg }, { "banks", n }, { "healed", healed } };
            }
            catch (AggregateException aex)
            {
                var inner = aex.InnerException ?? aex;
                var apiEx = inner as Integration.AccountingApiException;
                if (apiEx != null)
                    return new Dictionary<string, object> { { "success", false }, { "message", $"NextAcc ตอบ {apiEx.StatusCode}: {apiEx.ResponseBody}" } };
                return new Dictionary<string, object> { { "success", false }, { "message", inner.Message } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        /// <summary>ตัวเลือกบัญชีเงินสำหรับ dropdown แหล่งเงิน: กระเป๋าเงิน (BankAccount) + ผังบัญชีสินทรัพย์/หนี้สิน</summary>
        private Dictionary<string, object> GetWalletOptions()
        {
            try
            {
                var banks = new List<Dictionary<string, object>>();
                DateTime? lastSync = null;
                if (TableExists("Accounting_Nexaacc_BankAccounts"))
                {
                    var dt = _code.DatabaseQuerySafe(ConnStr,
                        @"SELECT Nexaacc_BankAccountId, Account_Name, Bank_Name, Account_Number, Account_Type,
                                 CAST(Linked_Account_Id AS NVARCHAR(50)) AS Linked, Linked_Account_Code, Linked_Account_Name,
                                 Is_Active, Last_Synced
                            FROM Accounting_Nexaacc_BankAccounts
                           WHERE Is_Present = 1
                           ORDER BY Is_Active DESC, Bank_Name, Account_Name", null);
                    if (dt != null)
                        foreach (DataRow r in dt.Rows)
                        {
                            banks.Add(new Dictionary<string, object>
                            {
                                { "id", r["Nexaacc_BankAccountId"].ToString() },
                                { "name", r["Account_Name"]?.ToString() ?? "" },
                                { "bank", r["Bank_Name"]?.ToString() ?? "" },
                                { "number", r["Account_Number"]?.ToString() ?? "" },
                                { "type", r["Account_Type"]?.ToString() ?? "" },
                                { "linkedId", r["Linked"] == DBNull.Value ? "" : r["Linked"].ToString() },
                                { "linkedCode", r["Linked_Account_Code"]?.ToString() ?? "" },
                                { "linkedName", r["Linked_Account_Name"]?.ToString() ?? "" },
                                { "active", Convert.ToBoolean(r["Is_Active"]) }
                            });
                            if (r["Last_Synced"] != DBNull.Value)
                            {
                                var ls = Convert.ToDateTime(r["Last_Synced"]);
                                if (lastSync == null || ls > lastSync) lastSync = ls;
                            }
                        }
                }

                var accounts = new List<Dictionary<string, object>>();
                if (TableExists("Accounting_Nexaacc_Accounts"))
                {
                    var dt = _code.DatabaseQuerySafe(ConnStr,
                        @"SELECT Nexaacc_AccountId, Account_Code, Account_Name, Account_Name_En, Account_Type, Account_Level
                            FROM Accounting_Nexaacc_Accounts
                           WHERE Is_Active = 1 AND Account_Type IN ('Asset', 'Liability')
                           ORDER BY Account_Code", null);
                    if (dt != null)
                        foreach (DataRow r in dt.Rows)
                        {
                            string accCode = r["Account_Code"]?.ToString() ?? "";
                            // กลุ่มตามหน้าที่ของเงิน (ผังมาตรฐาน NextAcc ChartOfAccountTemplates): 111xx เงินสด/ธนาคาร/
                            // กระเป๋าเงิน Digital, 113xx ลูกหนี้ (รวม 11340 พักเงินเกตเวย์ / ลูกหนี้ OTA), อื่น ๆ
                            string group = accCode.StartsWith("111") ? "CASH"
                                : accCode.StartsWith("113") ? "RECEIVABLE"
                                : (r["Account_Type"]?.ToString() == "Liability" ? "LIABILITY" : "OTHER_ASSET");
                            accounts.Add(new Dictionary<string, object>
                            {
                                { "id", r["Nexaacc_AccountId"].ToString() },
                                { "code", accCode },
                                { "name", r["Account_Name"]?.ToString() ?? (r["Account_Name_En"]?.ToString() ?? "") },
                                { "type", r["Account_Type"]?.ToString() ?? "" },
                                { "level", r["Account_Level"] == DBNull.Value ? 0 : Convert.ToInt32(r["Account_Level"]) },
                                { "group", group }
                            });
                        }
                }

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "banks", banks },
                    { "accounts", accounts },
                    { "bankCacheReady", TableExists("Accounting_Nexaacc_BankAccounts") },
                    { "lastSync", lastSync?.ToString("dd/MM/yyyy HH:mm") ?? "ยังไม่เคยดึง" }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        // ──────────────────────────────────────────────
        // เกตเวย์รับชำระออนไลน์ → แหล่งเงินรายผู้ให้บริการ (Nexaacc_Gateway_PaidHow_{PROVIDER})
        // ──────────────────────────────────────────────

        private static readonly string[] GatewayProviders = { "OMISE", "PAYSO", "MANUAL_QR" };

        private Dictionary<string, object> GetGatewayPaidHow()
        {
            try
            {
                var config = new Integration.AccountingConfig(ConnStr);
                string fallback = Take_Time_BangPhra.Payments.PaymentGatewayConfig.Get("Payment_PaidHow_Name", "Omise (จ่ายออนไลน์)");
                string activeProvider = "";
                try { activeProvider = Take_Time_BangPhra.Payments.PaymentGatewayConfig.ActiveProvider; } catch { }

                var rows = new List<Dictionary<string, object>>();
                foreach (string p in GatewayProviders)
                {
                    string cur = config.GetConfigValue("Nexaacc_Gateway_PaidHow_" + p, "");
                    string effective = Integration.AccountingSyncService.ResolveGatewayPaidHowName(ConnStr, p, null, fallback);
                    rows.Add(new Dictionary<string, object>
                    {
                        { "provider", p },
                        { "paidHow", cur },
                        { "effective", effective },
                        { "active", string.Equals(p, activeProvider, StringComparison.OrdinalIgnoreCase) }
                    });
                }

                var options = new List<Dictionary<string, object>>();
                var dt = _code.DatabaseQuerySafe(ConnStr,
                    @"SELECT Paid_How, ISNULL(Nexaacc_AccountCode, '') AS Code,
                             CASE WHEN Nexaacc_AccountId IS NULL THEN 0 ELSE 1 END AS Linked
                        FROM Account_Paid_How WHERE Status = 'True' ORDER BY ID", null);
                if (dt != null)
                    foreach (DataRow r in dt.Rows)
                        options.Add(new Dictionary<string, object>
                        {
                            { "name", r["Paid_How"]?.ToString() ?? "" },
                            { "code", r["Code"]?.ToString() ?? "" },
                            { "linked", Convert.ToInt32(r["Linked"]) == 1 }
                        });

                return new Dictionary<string, object>
                {
                    { "success", true }, { "rows", rows }, { "options", options }, { "fallback", fallback }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> SaveGatewayPaidHow(Dictionary<string, object> data)
        {
            try
            {
                string provider = Integration.AccountingSyncService.NormalizeConfigToken(data.ContainsKey("provider") ? data["provider"]?.ToString() : "");
                if (Array.IndexOf(GatewayProviders, provider) < 0)
                    return new Dictionary<string, object> { { "success", false }, { "message", "ผู้ให้บริการไม่ถูกต้อง" } };
                string paidHow = (data.ContainsKey("paidHow") ? data["paidHow"]?.ToString() : "") ?? "";
                paidHow = paidHow.Trim();
                if (paidHow.Length > 0)
                {
                    var chk = _code.DatabaseQuerySafe(ConnStr,
                        "SELECT TOP 1 1 FROM Account_Paid_How WHERE Paid_How = @n AND Status = 'True'",
                        new Dictionary<string, object> { { "@n", paidHow } });
                    if (chk == null || chk.Rows.Count == 0)
                        return new Dictionary<string, object> { { "success", false }, { "message", $"ไม่พบแหล่งเงิน \"{paidHow}\" ที่เปิดใช้อยู่" } };
                }
                var config = new Integration.AccountingConfig(ConnStr);
                config.SetConfig("Nexaacc_Gateway_PaidHow_" + provider, paidHow);
                try
                {
                    _code.Logs(ConnStr, "AccountingConfig",
                        $"เกตเวย์ {provider} → แหล่งเงิน \"{(paidHow.Length > 0 ? paidHow : "(ค่าเดิม Payment_PaidHow_Name)")}\" โดย {Session["UserName"] ?? "?"}",
                        Session["UserName"]?.ToString() ?? "SYSTEM");
                }
                catch { }
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", paidHow.Length > 0
                        ? $"ยอดรับผ่าน {provider} จะลงแหล่งเงิน \"{paidHow}\" (มีผลกับรายการที่ชำระหลังจากนี้)"
                        : $"{provider}: กลับไปใช้แหล่งเงินกลาง (Payment_PaidHow_Name)" }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        // ──────────────────────────────────────────────
        // OTA รายช่องทาง → แหล่งเงิน + ผู้ซื้อ (Accounting_Ota_Channel_Map, PHASE19_21)
        // ──────────────────────────────────────────────

        private Dictionary<string, object> GetOtaChannelMap()
        {
            try
            {
                if (!TableExists("Accounting_Ota_Channel_Map"))
                    return new Dictionary<string, object> { { "success", false }, { "message", "ยังไม่มีตาราง Accounting_Ota_Channel_Map — รัน migration PHASE19_21 ก่อน" } };

                var byKey = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
                var order = new List<string>();
                var dt = _code.DatabaseQuerySafe(ConnStr,
                    @"SELECT m.Channel_Key, m.Display_Name, m.Paid_How_ID, m.Buyer_Name, m.Buyer_Tax_Id, m.Buyer_Address,
                             m.Buyer_Branch, m.Is_Active, CASE WHEN m.Nexaacc_Contact_Id IS NULL THEN 0 ELSE 1 END AS HasContact,
                             p.Paid_How, ISNULL(p.Nexaacc_AccountCode, '') AS Code,
                             CASE WHEN p.Nexaacc_AccountId IS NULL THEN 0 ELSE 1 END AS Linked
                        FROM Accounting_Ota_Channel_Map m
                        LEFT JOIN Account_Paid_How p ON p.ID = m.Paid_How_ID AND p.Status = 'True'
                       ORDER BY m.Channel_Key", null);
                if (dt != null)
                    foreach (DataRow r in dt.Rows)
                    {
                        string k = r["Channel_Key"].ToString();
                        byKey[k] = new Dictionary<string, object>
                        {
                            { "channelKey", k },
                            { "displayName", r["Display_Name"]?.ToString() ?? k },
                            { "paidHowId", r["Paid_How_ID"] == DBNull.Value ? 0 : Convert.ToInt32(r["Paid_How_ID"]) },
                            { "paidHowName", r["Paid_How"] == DBNull.Value ? "" : r["Paid_How"].ToString() },
                            { "accountCode", r["Code"]?.ToString() ?? "" },
                            { "linked", Convert.ToInt32(r["Linked"]) == 1 },
                            { "buyerName", r["Buyer_Name"]?.ToString() ?? "" },
                            { "buyerTaxId", r["Buyer_Tax_Id"]?.ToString() ?? "" },
                            { "buyerAddress", r["Buyer_Address"]?.ToString() ?? "" },
                            { "buyerBranch", r["Buyer_Branch"]?.ToString() ?? "" },
                            { "isActive", Convert.ToBoolean(r["Is_Active"]) },
                            { "hasContact", Convert.ToInt32(r["HasContact"]) == 1 },
                            { "saved", true },
                            { "bookings", 0 }
                        };
                        order.Add(k);
                    }

                // ช่องทางที่เจอจริงในการจอง (ยังไม่มีแถว mapping ก็โผล่ให้ตั้ง)
                try
                {
                    var seen = _code.DatabaseQuerySafe(ConnStr,
                        @"SELECT LTRIM(RTRIM(OTA_Channel)) AS Ch, COUNT(*) AS N
                            FROM Reservation
                           WHERE OTA_Channel IS NOT NULL AND LTRIM(RTRIM(OTA_Channel)) <> ''
                           GROUP BY LTRIM(RTRIM(OTA_Channel))", null);
                    if (seen != null)
                        foreach (DataRow r in seen.Rows)
                        {
                            string ch = r["Ch"].ToString();
                            string k = Integration.AccountingSyncService.OtaChannelKey(ch);
                            if (string.IsNullOrEmpty(k)) continue;
                            int cnt = Convert.ToInt32(r["N"]);
                            if (!byKey.ContainsKey(k))
                            {
                                byKey[k] = new Dictionary<string, object>
                                {
                                    { "channelKey", k }, { "displayName", ch }, { "paidHowId", 0 }, { "paidHowName", "" },
                                    { "accountCode", "" }, { "linked", false }, { "buyerName", "" }, { "buyerTaxId", "" },
                                    { "buyerAddress", "" }, { "buyerBranch", "" }, { "isActive", true },
                                    { "hasContact", false }, { "saved", false }, { "bookings", 0 }
                                };
                                order.Add(k);
                            }
                            byKey[k]["bookings"] = Convert.ToInt32(byKey[k]["bookings"]) + cnt;
                        }
                }
                catch { /* คอลัมน์ OTA_Channel ยังไม่มี */ }

                var paidHows = new List<Dictionary<string, object>>();
                var ph = _code.DatabaseQuerySafe(ConnStr,
                    @"SELECT ID, Paid_How, ISNULL(Nexaacc_AccountCode, '') AS Code,
                             CASE WHEN Nexaacc_AccountId IS NULL THEN 0 ELSE 1 END AS Linked
                        FROM Account_Paid_How WHERE Status = 'True' ORDER BY ID", null);
                if (ph != null)
                    foreach (DataRow r in ph.Rows)
                        paidHows.Add(new Dictionary<string, object>
                        {
                            { "id", Convert.ToInt32(r["ID"]) }, { "name", r["Paid_How"]?.ToString() ?? "" },
                            { "code", r["Code"]?.ToString() ?? "" }, { "linked", Convert.ToInt32(r["Linked"]) == 1 }
                        });

                var items = new List<Dictionary<string, object>>();
                foreach (string k in order) items.Add(byKey[k]);
                var config = new Integration.AccountingConfig(ConnStr);
                return new Dictionary<string, object>
                {
                    { "success", true }, { "items", items }, { "paidHows", paidHows },
                    { "mode", config.OtaDocumentMode }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> SaveOtaChannelMap(Dictionary<string, object> data)
        {
            try
            {
                if (!TableExists("Accounting_Ota_Channel_Map"))
                    return new Dictionary<string, object> { { "success", false }, { "message", "ยังไม่มีตาราง Accounting_Ota_Channel_Map — รัน migration PHASE19_21 ก่อน" } };

                Func<string, string> s = k => data.ContainsKey(k) && data[k] != null ? data[k].ToString().Trim() : "";
                string key = Integration.AccountingSyncService.OtaChannelKey(s("channelKey"));
                if (string.IsNullOrEmpty(key))
                    return new Dictionary<string, object> { { "success", false }, { "message", "ไม่มีรหัสช่องทาง" } };
                int paidHowId; int.TryParse(s("paidHowId"), out paidHowId);
                string taxId = s("buyerTaxId");
                if (taxId.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(taxId, @"^\d{13}$"))
                    return new Dictionary<string, object> { { "success", false }, { "message", "เลขผู้เสียภาษีผู้ซื้อต้องเป็นตัวเลข 13 หลัก (หรือเว้นว่าง สำหรับ OTA ต่างประเทศ)" } };
                string branch = s("buyerBranch");
                if (branch.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(branch, @"^\d{5}$"))
                    return new Dictionary<string, object> { { "success", false }, { "message", "รหัสสาขาต้องเป็นตัวเลข 5 หลัก (00000 = สำนักงานใหญ่)" } };
                bool isActive = !data.ContainsKey("isActive") || Convert.ToBoolean(data["isActive"]);

                string warn = null;
                if (paidHowId > 0)
                {
                    bool coa = TableExists("Accounting_Nexaacc_Accounts");
                    var p = _code.DatabaseQuerySafe(ConnStr,
                        @"SELECT TOP 1 p.Paid_How, CAST(p.Nexaacc_AccountId AS NVARCHAR(50)) AS Acc, "
                        + (coa ? "a.Account_Type" : "CAST(NULL AS NVARCHAR(50)) AS Account_Type") + @"
                            FROM Account_Paid_How p "
                        + (coa ? "LEFT JOIN Accounting_Nexaacc_Accounts a ON a.Nexaacc_AccountId = p.Nexaacc_AccountId " : "") + @"
                           WHERE p.ID = @id AND p.Status = 'True'",
                        new Dictionary<string, object> { { "@id", paidHowId } });
                    if (p == null || p.Rows.Count == 0)
                        return new Dictionary<string, object> { { "success", false }, { "message", "ไม่พบแหล่งเงินที่เลือก (หรือถูกปิดใช้)" } };
                    if (p.Rows[0]["Acc"] == DBNull.Value)
                        warn = $"⚠ แหล่งเงิน \"{p.Rows[0]["Paid_How"]}\" ยังไม่ได้ผูกบัญชี NextAcc — ผูกที่ตาราง 'วิธีจ่ายเงิน → บัญชี NextAcc' ก่อน ไม่งั้นระบบใช้ OTA_RECEIVABLE แทน";
                    else if (p.Rows[0]["Account_Type"] != DBNull.Value
                             && !p.Rows[0]["Account_Type"].ToString().Equals("Asset", StringComparison.OrdinalIgnoreCase))
                        return new Dictionary<string, object> { { "success", false }, { "message", $"แหล่งเงิน \"{p.Rows[0]["Paid_How"]}\" ผูกกับบัญชีประเภท {p.Rows[0]["Account_Type"]} — เงินที่ OTA เก็บแทนต้องเป็นบัญชีสินทรัพย์ (พักเงิน/ลูกหนี้ OTA)" } };
                }

                string user = Session["UserName"]?.ToString() ?? "";
                _code.DatabaseInsertSafe(ConnStr, @"
                    IF EXISTS (SELECT 1 FROM Accounting_Ota_Channel_Map WHERE Channel_Key = @k)
                        UPDATE Accounting_Ota_Channel_Map
                           SET Display_Name = @dn, Paid_How_ID = @ph, Buyer_Name = @bn, Buyer_Tax_Id = @bt,
                               Buyer_Address = @ba, Buyer_Branch = @bb, Is_Active = @act,
                               Nexaacc_Contact_Id = NULL,   -- ข้อมูลผู้ซื้ออาจเปลี่ยน → upsert ผู้ติดต่อใหม่ตอนออกเอกสารถัดไป
                               Updated_Date = GETDATE(), Updated_By = @u
                         WHERE Channel_Key = @k
                    ELSE
                        INSERT INTO Accounting_Ota_Channel_Map
                            (Channel_Key, Display_Name, Paid_How_ID, Buyer_Name, Buyer_Tax_Id, Buyer_Address, Buyer_Branch, Is_Active, Updated_Date, Updated_By)
                        VALUES (@k, @dn, @ph, @bn, @bt, @ba, @bb, @act, GETDATE(), @u)",
                    new Dictionary<string, object>
                    {
                        { "@k", key },
                        { "@dn", s("displayName").Length > 0 ? (object)s("displayName") : key },
                        { "@ph", paidHowId > 0 ? (object)paidHowId : DBNull.Value },
                        { "@bn", s("buyerName").Length > 0 ? (object)s("buyerName") : DBNull.Value },
                        { "@bt", taxId.Length > 0 ? (object)taxId : DBNull.Value },
                        { "@ba", s("buyerAddress").Length > 0 ? (object)s("buyerAddress") : DBNull.Value },
                        { "@bb", branch.Length > 0 ? (object)branch : DBNull.Value },
                        { "@act", isActive },
                        { "@u", user }
                    });
                try
                {
                    _code.Logs(ConnStr, "AccountingConfig",
                        $"OTA {key}: แหล่งเงิน #{paidHowId} ผู้ซื้อ \"{s("buyerName")}\" {(isActive ? "เปิด" : "ปิด")} โดย {(user.Length > 0 ? user : "?")}",
                        user.Length > 0 ? user : "SYSTEM");
                }
                catch { }
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", $"บันทึกช่องทาง {key} แล้ว" + (warn != null ? "\n" + warn : "") }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        // ──────────────────────────────────────────────
        // Expense Category → Account Mapping (Account_Paid_Type)
        // ──────────────────────────────────────────────

        private Dictionary<string, object> GetPaidTypeMapping()
        {
            try
            {
                DataTable dt = _code.DatabaseQuerySafe(ConnStr,
                    @"SELECT ID, Paid_Type,
                             ISNULL(CAST(Nexaacc_AccountId AS NVARCHAR(50)), '') AS Nexaacc_AccountId,
                             ISNULL(Nexaacc_AccountCode, '') AS Nexaacc_AccountCode,
                             Status
                      FROM Account_Paid_Type WHERE Status = 'True' ORDER BY ID", null);

                var items = new List<Dictionary<string, object>>();
                if (dt?.Rows.Count > 0)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        items.Add(new Dictionary<string, object>
                        {
                            { "id", Convert.ToInt32(row["ID"]) },
                            { "name", row["Paid_Type"]?.ToString() ?? "" },
                            { "accountId", row["Nexaacc_AccountId"]?.ToString() ?? "" },
                            { "accountCode", row["Nexaacc_AccountCode"]?.ToString() ?? "" }
                        });
                    }
                }
                return new Dictionary<string, object> { { "success", true }, { "items", items } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> UpdatePaidTypeAccount()
        {
            try
            {
                int id = int.Parse(Request.QueryString["id"]);
                string accountId = Request.QueryString["accountId"] ?? "";
                string accountCode = Request.QueryString["accountCode"] ?? "";

                if (string.IsNullOrEmpty(accountId))
                    return new Dictionary<string, object> { { "success", false }, { "message", "ไม่ได้เลือกบัญชี" } };

                _code.DatabaseInsertSafe(ConnStr,
                    @"UPDATE Account_Paid_Type SET Nexaacc_AccountId = @accId, Nexaacc_AccountCode = @accCode WHERE ID = @id",
                    new Dictionary<string, object>
                    {
                        { "@accId", Guid.Parse(accountId) },
                        { "@accCode", accountCode },
                        { "@id", id }
                    });

                DataTable name = _code.DatabaseQuerySafe(ConnStr,
                    "SELECT Paid_Type FROM Account_Paid_Type WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", id } });
                string paidTypeName = name?.Rows.Count > 0 ? name.Rows[0]["Paid_Type"]?.ToString() : "";

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", $"ผูก \"{paidTypeName}\" กับบัญชี {accountCode} เรียบร้อย" }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        /// <summary>แปลงค่าเป็นตัวเลขแบบไม่โยน exception — คอลัมน์ในคิวอาจเป็น NULL หรือข้อความ</summary>
        private static int ToIntSafe(object v)
        {
            if (v == null || v == DBNull.Value) return 0;
            int n;
            return int.TryParse(v.ToString().Trim(), out n) ? n : 0;
        }

        private static long ToLongSafe(object v)
        {
            if (v == null || v == DBNull.Value) return 0;
            long n;
            return long.TryParse(v.ToString().Trim(), out n) ? n : 0;
        }

        private void WriteJson(Dictionary<string, object> data)
        {
            Response.ContentType = "application/json";
            string json;
            try
            {
                // ⚠️ JavaScriptSerializer จำกัดผลลัพธ์ที่ 2 MB by default — คิว 1 หน้าที่มีแถว FAILED
                // ซึ่ง Error_Message เป็น response ดิบของ API ยาว ๆ จะทะลุลิมิตแล้วโยน exception
                // ออกไปนอก try ของ handler → ASP.NET ตอบเป็นหน้า HTML error → ฝั่ง JS ทำ r.json()
                // ไม่ได้ → เข้า .catch ที่ log ลง console เฉย ๆ = ผู้ใช้เห็นแค่ "กดแล้วไม่ไปไหน"
                var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                json = ser.Serialize(data);
            }
            catch (Exception ex)
            {
                try { _code.Logs(ConnStr, "AccountingIntegration", $"WriteJson serialize failed: {ex.Message}", "SYSTEM"); }
                catch { }
                // ต้องตอบเป็น JSON เสมอ ไม่งั้นหน้าเว็บจะเงียบโดยไม่มีสาเหตุให้ดู
                var safe = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                json = safe.Serialize(new Dictionary<string, object>
                {
                    { "success", false },
                    { "message", "ส่งข้อมูลกลับไม่สำเร็จ (serialize): " + ex.Message }
                });
            }
            Response.Write(json);
            Response.End();
        }

        private static string BoolToFlag(object value)
        {
            string s = value?.ToString() ?? "";
            return (s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1") ? "1" : "0";
        }

        // ──────────────────────────────────────────────
        // Document Source Lookup — query vw_Receipt_Document_Source
        // ──────────────────────────────────────────────

        private Dictionary<string, object> LookupDocumentSource()
        {
            try
            {
                string q = (Request.QueryString["q"] ?? "").Trim();
                if (string.IsNullOrEmpty(q))
                    return new Dictionary<string, object> { { "success", false }, { "message", "กรุณาระบุเลขที่ใบเสร็จหรือ Reservation ID" } };

                string sql;
                var parameters = new Dictionary<string, object>();
                if (int.TryParse(q, out int resId))
                {
                    sql = @"SELECT TOP 50 * FROM vw_Receipt_Document_Source WHERE Reservation_ID = @resId ORDER BY Created_Date DESC";
                    parameters.Add("@resId", resId);
                }
                else
                {
                    sql = @"SELECT TOP 50 * FROM vw_Receipt_Document_Source WHERE Receipt_Number LIKE @num ORDER BY Created_Date DESC";
                    parameters.Add("@num", "%" + q + "%");
                }

                var dt = _code.DatabaseQuerySafe(ConnStr, sql, parameters);
                var config = new Integration.AccountingConfig(ConnStr);
                var items = new List<Dictionary<string, object>>();
                if (dt != null)
                {
                    foreach (System.Data.DataRow row in dt.Rows)
                    {
                        string nexaaccDocId = row["Nexaacc_Doc_Id"]?.ToString() ?? "";
                        string nexaaccUrl = "";
                        if (!string.IsNullOrEmpty(nexaaccDocId) && config.IsConfigured)
                        {
                            string basePath = config.RawBaseUrl.TrimEnd('/');
                            string cid = config.CompanyId.ToString();
                            nexaaccUrl = $"{basePath}/{cid}/documents/{nexaaccDocId}";
                        }
                        items.Add(new Dictionary<string, object>
                        {
                            { "receiptId", row["Receipt_ID"]?.ToString() },
                            { "receiptNumber", row["Receipt_Number"]?.ToString() },
                            { "reservationId", row["Reservation_ID"] != DBNull.Value ? Convert.ToInt32(row["Reservation_ID"]) : 0 },
                            { "total", row["Total"] != DBNull.Value ? Convert.ToDecimal(row["Total"]) : 0m },
                            { "isDeposit", row["IsDeposit"] != DBNull.Value && Convert.ToBoolean(row["IsDeposit"]) },
                            { "documentSource", row["Document_Source"]?.ToString() ?? "LOCAL" },
                            { "nexaaccDocId", nexaaccDocId },
                            { "nexaaccUrl", nexaaccUrl },
                            { "syncStatus", row["Sync_Status"]?.ToString() },
                            { "syncError", row["Sync_Error"]?.ToString() },
                            { "etaxStatus", row["Etax_Status"]?.ToString() },
                            { "etaxRefNumber", row["Etax_Ref_Number"]?.ToString() },
                            { "etaxPdfUrl", row["Etax_Pdf_Url"]?.ToString() },
                            { "etaxXmlUrl", row["Etax_Xml_Url"]?.ToString() },
                            { "etaxEmailSent", row["Etax_Email_Sent"] != DBNull.Value && Convert.ToBoolean(row["Etax_Email_Sent"]) },
                            { "etaxError", row["Etax_Error"]?.ToString() },
                            { "customerName", row["Customer_FullName"]?.ToString() },
                            { "customerEmail", row["Customer_Email"]?.ToString() },
                            { "customerTaxID", row["Customer_TaxID"]?.ToString() }
                        });
                    }
                }

                return new Dictionary<string, object> { { "success", true }, { "items", items }, { "count", items.Count } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        // ──────────────────────────────────────────────
        // E-Tax manual triggers
        // ──────────────────────────────────────────────

        private Dictionary<string, object> ManualEtaxGenerate(Dictionary<string, object> data)
        {
            try
            {
                string receiptNumber = data.ContainsKey("receiptNumber") ? data["receiptNumber"]?.ToString() : null;
                if (string.IsNullOrEmpty(receiptNumber))
                    return new Dictionary<string, object> { { "success", false }, { "message", "กรุณาระบุเลขที่ใบเสร็จ" } };

                var service = new Integration.AccountingSyncService(ConnStr);
                var (success, message, etaxRef) = System.Threading.Tasks.Task.Run(() => service.ManualGenerateEtaxAsync(receiptNumber)).Result;
                return new Dictionary<string, object>
                {
                    { "success", success },
                    { "message", message },
                    { "etaxRefNumber", etaxRef ?? "" }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        // ──────────────────────────────────────────────
        // Stock Adjustment / Write-off / Product Sync handlers
        // ──────────────────────────────────────────────

        private Dictionary<string, object> ManualStockAdjustment(Dictionary<string, object> data)
        {
            try
            {
                string adjustType = data.ContainsKey("adjustType") ? data["adjustType"]?.ToString() : "";
                string productIdStr = data.ContainsKey("productId") ? data["productId"]?.ToString() : "";
                string qtyStr = data.ContainsKey("quantity") ? data["quantity"]?.ToString() : "";
                string costStr = data.ContainsKey("costPerUnit") ? data["costPerUnit"]?.ToString() : "";
                string reason = data.ContainsKey("reason") ? data["reason"]?.ToString() : "";

                if (!int.TryParse(productIdStr, out int productId) || productId <= 0)
                    return new Dictionary<string, object> { { "success", false }, { "message", "Product ID ไม่ถูกต้อง" } };
                if (!decimal.TryParse(qtyStr, out decimal quantity))
                    return new Dictionary<string, object> { { "success", false }, { "message", "จำนวนไม่ถูกต้อง" } };
                if (!decimal.TryParse(costStr, out decimal costPerUnit) || costPerUnit <= 0)
                    return new Dictionary<string, object> { { "success", false }, { "message", "ต้นทุน/หน่วยไม่ถูกต้อง" } };

                // Insert Stock_Adjustment_Log first
                var dt = _code.DatabaseQuerySafe(ConnStr,
                    "SELECT Product_Name FROM Product WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", productId } });
                if (dt == null || dt.Rows.Count == 0)
                    return new Dictionary<string, object> { { "success", false }, { "message", $"ไม่พบ Product ID {productId}" } };
                string productName = dt.Rows[0]["Product_Name"]?.ToString() ?? "";

                bool isWriteoff = adjustType.Equals("WRITEOFF", StringComparison.OrdinalIgnoreCase);
                decimal absQty = Math.Abs(quantity);
                decimal totalCost = Math.Round(absQty * costPerUnit, 2);

                _code.DatabaseInsertSafe(ConnStr,
                    @"INSERT INTO Stock_Adjustment_Log
                      (Adjustment_Date, Adjustment_Type, Product_ID, Difference_Qty, Cost_PerUnit, Total_Cost, Reason, Created_Date, Sync_Status)
                      VALUES (GETDATE(), @type, @prodId, @diff, @cost, @total, @reason, GETDATE(), 'PENDING')",
                    new Dictionary<string, object>
                    {
                        { "@type", isWriteoff ? "WRITEOFF" : "COUNT_VARIANCE" },
                        { "@prodId", productId },
                        { "@diff", isWriteoff ? -absQty : quantity },
                        { "@cost", costPerUnit },
                        { "@total", totalCost },
                        { "@reason", reason ?? "" }
                    });

                var idDt = _code.DatabaseQuerySafe(ConnStr,
                    "SELECT TOP 1 ID FROM Stock_Adjustment_Log WHERE Product_ID = @id ORDER BY ID DESC",
                    new Dictionary<string, object> { { "@id", productId } });
                long logId = idDt?.Rows.Count > 0 ? Convert.ToInt64(idDt.Rows[0]["ID"]) : 0;

                // Insert Product_In/Product_Out for stock movement
                if (isWriteoff || quantity < 0)
                {
                    _code.DatabaseInsertSafe(ConnStr,
                        @"INSERT INTO Product_Out (DateTime_Out, Product_ID, Amount, PricePerUnit, OutType, Reason)
                          VALUES (GETDATE(), @id, @qty, @cost, @type, @reason)",
                        new Dictionary<string, object>
                        {
                            { "@id", productId }, { "@qty", absQty }, { "@cost", costPerUnit },
                            { "@type", isWriteoff ? "WRITEOFF" : "ADJUSTMENT_LOSS" }, { "@reason", reason ?? "" }
                        });
                }
                else if (quantity > 0)
                {
                    _code.DatabaseInsertSafe(ConnStr,
                        @"INSERT INTO Product_In (DateTime_In, Product_ID, Amount, PricePerUnit, InType)
                          VALUES (GETDATE(), @id, @qty, @cost, 'ADJUSTMENT_GAIN')",
                        new Dictionary<string, object>
                        {
                            { "@id", productId }, { "@qty", quantity }, { "@cost", costPerUnit }
                        });
                }

                // Enqueue accounting sync
                var sync = new Integration.AccountingSyncService(ConnStr);
                long queueId;
                if (isWriteoff)
                    queueId = sync.EnqueueStockWriteOff(logId, productId, productName, absQty, costPerUnit, DateTime.Now, reason);
                else
                    queueId = sync.EnqueueStockAdjustment(logId, productId, productName, quantity, costPerUnit, DateTime.Now, reason);

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", $"{(isWriteoff ? "Write-off" : "Adjustment")}: ส่งเข้าคิวเรียบร้อย — logId={logId}, queueId={queueId}" }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> ManualProductSync(Dictionary<string, object> data)
        {
            try
            {
                string productIdStr = data.ContainsKey("productId") ? data["productId"]?.ToString() : "";
                if (!int.TryParse(productIdStr, out int productId) || productId <= 0)
                    return new Dictionary<string, object> { { "success", false }, { "message", "Product ID ไม่ถูกต้อง" } };

                var sync = new Integration.AccountingSyncService(ConnStr);
                long queueId = sync.EnqueueProductSync(productId);
                if (queueId <= 0)
                    return new Dictionary<string, object> { { "success", false }, { "message", "ไม่สามารถ enqueue ได้ (อาจมีรายการเดิมแล้ว หรือ config ไม่พร้อม)" } };

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", $"Product sync: ส่งเข้าคิวเรียบร้อย (queueId={queueId})" }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        // ──────────────────────────────────────────────
        // Deposit Lifecycle handlers
        // ──────────────────────────────────────────────

        private Dictionary<string, object> LookupDepositStatus()
        {
            try
            {
                string q = (Request.QueryString["q"] ?? "").Trim();
                string statusFilter = (Request.QueryString["status"] ?? "").Trim();

                var sb = new System.Text.StringBuilder("SELECT TOP 100 * FROM vw_Reservation_Deposit_Status WHERE DepositPaid > 0");
                var parameters = new Dictionary<string, object>();

                if (!string.IsNullOrEmpty(q))
                {
                    if (int.TryParse(q, out int resId))
                    {
                        sb.Append(" AND Reservation_ID = @resId");
                        parameters.Add("@resId", resId);
                    }
                    else
                    {
                        sb.Append(" AND (Customer_Name LIKE @q OR Customer_MobilePhone LIKE @q)");
                        parameters.Add("@q", "%" + q + "%");
                    }
                }
                if (!string.IsNullOrEmpty(statusFilter))
                {
                    sb.Append(" AND Deposit_Status = @status");
                    parameters.Add("@status", statusFilter);
                }
                sb.Append(" ORDER BY CheckoutDate DESC, Reservation_ID DESC");

                var dt = _code.DatabaseQuerySafe(ConnStr, sb.ToString(), parameters);
                var items = new List<Dictionary<string, object>>();
                if (dt != null)
                {
                    foreach (System.Data.DataRow row in dt.Rows)
                    {
                        items.Add(new Dictionary<string, object>
                        {
                            { "reservationId", Convert.ToInt32(row["Reservation_ID"]) },
                            { "reservationStatus", row["Reservation_Status"]?.ToString() },
                            { "checkinDate", row["CheckinDate"] != DBNull.Value ? Convert.ToDateTime(row["CheckinDate"]).ToString("yyyy-MM-dd") : null },
                            { "checkoutDate", row["CheckoutDate"] != DBNull.Value ? Convert.ToDateTime(row["CheckoutDate"]).ToString("yyyy-MM-dd") : null },
                            { "customerMobilePhone", row["Customer_MobilePhone"]?.ToString() },
                            { "customerName", row["Customer_Name"]?.ToString() },
                            { "customerEmail", row["Customer_Email"]?.ToString() },
                            { "depositPaid", row["DepositPaid"] != DBNull.Value ? Convert.ToDecimal(row["DepositPaid"]) : 0m },
                            { "depositCleared", row["DepositCleared"] != DBNull.Value ? Convert.ToDecimal(row["DepositCleared"]) : 0m },
                            { "depositOutstanding", row["DepositOutstanding"] != DBNull.Value ? Convert.ToDecimal(row["DepositOutstanding"]) : 0m },
                            { "depositStatus", row["Deposit_Status"]?.ToString() ?? "OPEN" },
                            { "lastClearDate", row["LastClearDate"] != DBNull.Value ? Convert.ToDateTime(row["LastClearDate"]).ToString("yyyy-MM-ddTHH:mm:ss") : null },
                            { "lastClearAction", row["LastClearAction"]?.ToString() },
                            { "lastClearJournalId", row["LastClearJournalId"]?.ToString() }
                        });
                    }
                }
                return new Dictionary<string, object> { { "success", true }, { "items", items }, { "count", items.Count } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> ManualDepositOperation(Dictionary<string, object> data)
        {
            try
            {
                string operation = data.ContainsKey("operation") ? data["operation"]?.ToString() : "";
                string resIdStr = data.ContainsKey("reservationId") ? data["reservationId"]?.ToString() : "";
                string amountStr = data.ContainsKey("amount") ? data["amount"]?.ToString() : "";
                string reason = data.ContainsKey("reason") ? data["reason"]?.ToString() : "";

                if (!int.TryParse(resIdStr, out int resId) || resId <= 0)
                    return new Dictionary<string, object> { { "success", false }, { "message", "Reservation ID ไม่ถูกต้อง" } };
                if (!decimal.TryParse(amountStr, out decimal amount) || amount <= 0)
                    return new Dictionary<string, object> { { "success", false }, { "message", "จำนวนเงินไม่ถูกต้อง" } };

                // ดึงชื่อลูกค้า + paid type
                var dt = _code.DatabaseQuerySafe(ConnStr,
                    @"SELECT TOP 1 ISNULL(C.FullName, C.Name) AS Name,
                             (SELECT TOP 1 Paid_Type FROM Account_Receipt WHERE Reservation_ID = @id AND IsDeposit = 1 ORDER BY Created_Date DESC) AS PaidType
                      FROM Reservation R
                      LEFT JOIN Customer C ON C.MobilePhone = R.Customer_MobilePhone
                      WHERE R.ID = @id",
                    new Dictionary<string, object> { { "@id", resId } });
                string customerName = dt?.Rows.Count > 0 ? dt.Rows[0]["Name"]?.ToString() ?? "" : "";
                string paymentMethod = dt?.Rows.Count > 0 ? dt.Rows[0]["PaidType"]?.ToString() ?? "CASH" : "CASH";

                var sync = new Integration.AccountingSyncService(ConnStr);
                long queueId;
                string actionLabel;

                switch (operation.ToLower())
                {
                    case "checkout":
                        queueId = sync.EnqueueDepositClearingOnCheckout(resId, amount, customerName, DateTime.Now, 0);
                        actionLabel = "ตัดมัดจำ checkout";
                        break;
                    case "refund":
                        queueId = sync.EnqueueDepositRefund(resId, amount, paymentMethod, customerName, DateTime.Now);
                        actionLabel = "คืนเงินมัดจำ";
                        break;
                    case "forfeit":
                        queueId = sync.EnqueueDepositForfeit(resId, amount, customerName,
                            DateTime.Now, !string.IsNullOrEmpty(reason) ? reason : "manual forfeit");
                        actionLabel = "ริบมัดจำ";
                        break;
                    default:
                        return new Dictionary<string, object> { { "success", false }, { "message", "operation ไม่ถูกต้อง (checkout/refund/forfeit)" } };
                }

                if (queueId <= 0)
                    return new Dictionary<string, object> { { "success", false }, { "message", "ไม่สามารถ enqueue ได้ (อาจมีรายการเดิมแล้ว หรือ config ไม่พร้อม)" } };

                return new Dictionary<string, object> { { "success", true }, { "message", $"{actionLabel}: ส่งเข้าคิวเรียบร้อย (queueId={queueId}). กดปุ่ม Process Queue เพื่อดำเนินการ" } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> ManualEtaxSendEmail(Dictionary<string, object> data)
        {
            try
            {
                string receiptNumber = data.ContainsKey("receiptNumber") ? data["receiptNumber"]?.ToString() : null;
                string overrideEmail = data.ContainsKey("email") ? data["email"]?.ToString() : null;
                if (string.IsNullOrEmpty(receiptNumber))
                    return new Dictionary<string, object> { { "success", false }, { "message", "กรุณาระบุเลขที่ใบเสร็จ" } };

                var service = new Integration.AccountingSyncService(ConnStr);
                var (success, message) = System.Threading.Tasks.Task.Run(() => service.ManualSendEtaxEmailAsync(receiptNumber, overrideEmail)).Result;
                return new Dictionary<string, object> { { "success", success }, { "message", message } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }
    }
}
