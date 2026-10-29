using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Integration;
using Take_Time_BangPhra.Integration.OcrReview;

namespace Take_Time_BangPhra.Voucher
{
    /// <summary>
    /// OCR → ตรวจทาน → ส่ง NextAcc (CLAUDE.md ข้อ 3 + "หน้าตรวจทานก่อนส่ง + ใบลดหนี้ผู้ขาย"):
    ///   อัปโหลด → ocr/upload (autoCreate=false) → โพลล์ → หน้าตรวจทาน <c>?scan={scanId}</c> (ภาพเอกสารคู่ฟอร์ม, บรรทัดแก้ได้,
    ///   เทียบยอดกับกระดาษ, §86/4, ใบต้นทางของใบลด/เพิ่มหนี้) → ผู้ใช้กด "ส่ง" → <see cref="OcrReviewService.SubmitAsync"/>
    ///   (create → PUT → approve(422 ให้ผู้ใช้ยืนยันเอง) → แนบไฟล์ → 50 ทวิ → ย้ายขาเงิน). Draft เก็บใน Ocr_Document_Log (PHASE19_38)
    ///   หรือ Session เมื่อยังไม่รัน migration. ใช้ company endpoints — gate <c>CanUseCompanyEndpoints</c>.
    /// </summary>
    public partial class OcrUpload : Page
    {
        private string Conn => ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        // รันงาน async บน threadpool เพื่อเลี่ยง deadlock กับ ASP.NET sync context
        private static T RunSync<T>(Func<Task<T>> f) => Task.Run(f).GetAwaiter().GetResult();

        private bool _served;   // ส่งไฟล์ (preview/pdf) แล้ว — ไม่ render หน้า
        private readonly List<string> _serverErrors = new List<string>();
        private readonly List<string> _serverNotes = new List<string>();
        private Dictionary<string, object> _duplicate;
        private bool _nextaccDupWarn;

        private OcrReviewService Svc => new OcrReviewService(Conn);
        private string UserName => Session["username"]?.ToString();

        private Guid? QueryScanId
        {
            get
            {
                Guid g;
                return Guid.TryParse(Request.QueryString["scan"] ?? "", out g) && g != Guid.Empty ? g : (Guid?)null;
            }
        }

        private static string FileKey(Guid scanId) => "OcrScanFile_" + scanId.ToString("N");
        private static string NameKey(Guid scanId) => "OcrScanName_" + scanId.ToString("N");
        private static string DraftKey(Guid scanId) => "OcrReview_" + scanId.ToString("N");
        private static string RawKey(Guid scanId) => "OcrReviewRaw_" + scanId.ToString("N");
        private const string DocsKey = "OcrReviewDocs";

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.FinVoucher)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("~/Admin/Login");
                return;
            }

            if (!string.IsNullOrEmpty(Request.QueryString["preview"])) { ServePreview(); return; }
            if (!string.IsNullOrEmpty(Request.QueryString["pdf"])) { ServePdf(); return; }

            if (!IsPostBack)
            {
                bool ready = CheckConfig();
                var scan = QueryScanId;
                if (scan.HasValue && ready) ShowReview(scan.Value, Request.QueryString["kind"]);
                else
                {
                    if (Request.QueryString["discarded"] == "1") Msg(litStatus, "ok", "ทิ้งงานแล้ว (ถ้ามีร่างบน NextAcc ระบบลบให้แล้ว)");
                    RenderDrafts();
                }
            }
        }

        protected override void Render(HtmlTextWriter writer)
        {
            if (_served) return;
            base.Render(writer);
        }

        private bool CheckConfig()
        {
            var cfg = new AccountingConfig(Conn);
            if (!cfg.IsConfigured || !cfg.Enabled)
            {
                Msg(litStatus, "warn", "ยังไม่ได้ตั้งค่า/เปิดใช้งาน Accounting Integration — ไปที่ Admin → Settings → Accounting Integration ก่อน");
                btnScan.Enabled = false;
                return false;
            }
            if (!cfg.CanUseCompanyEndpoints)
            {
                Msg(litStatus, "warn", "OCR ใช้ company endpoints (/api/companies/*) — ต้องตั้ง Company ID และเปิด Nexaacc_Company_Endpoints. แนะนำตั้ง acc_ key แยกในหน้า Accounting Integration (ไม่งั้นจะใช้ int_ ผ่าน X-Api-Key fallback)");
                btnScan.Enabled = false;
                return false;
            }
            return true;
        }

        private AccountingApiClient NewClient()
        {
            var client = new AccountingApiClient(new AccountingConfig(Conn), Conn);
            client.ActingUser = UserName;   // creator จริง → ลายเซ็น/audit ใน NextAcc
            return client;
        }

        // ══════════════════════════════════════════════════════════════
        // 1) อัปโหลด + โพลล์
        // ══════════════════════════════════════════════════════════════

        protected void btnScan_Click(object sender, EventArgs e)
        {
            // OCR ประมวลผล + poll นานได้ → ยืด request timeout ของ ASP.NET (default 110s) กัน "A task was canceled"
            Server.ScriptTimeout = 600;
            litResult.Text = "";
            RenderDrafts();
            if (!CheckConfig()) return;
            if (!fuOcr.HasFile)
            {
                Msg(litStatus, "err", "กรุณาเลือกไฟล์ก่อน");
                return;
            }
            // NextAcc จำกัดขนาดไฟล์ — กันส่งไปแล้วโดน reject
            if (fuOcr.PostedFile != null && fuOcr.PostedFile.ContentLength > 10 * 1024 * 1024)
            {
                Msg(litStatus, "err", "ไฟล์ใหญ่เกิน 10 MB — กรุณาบีบ/ลดขนาดไฟล์ก่อน");
                return;
            }

            string tempPath = null;
            bool keepFile = false;   // เก็บไฟล์ไว้แสดงข้างฟอร์ม + แนบเข้าเอกสาร NextAcc (ไม่ลบใน finally)
            try
            {
                var client = NewClient();

                // บันทึกไฟล์ชั่วคราว — คงชื่อไฟล์เดิม (ในโฟลเดอร์ย่อย) เป็นชื่อไฟล์แนบที่อ่านง่าย (ไม่งั้นได้ ocr_<guid>)
                string origName = Path.GetFileName(fuOcr.FileName ?? "");
                if (string.IsNullOrWhiteSpace(origName)) origName = "scan" + Path.GetExtension(fuOcr.FileName);
                foreach (char ch in Path.GetInvalidFileNameChars()) origName = origName.Replace(ch, '_');
                string tempDir = Path.Combine(Path.GetTempPath(), "ocrupload_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);
                tempPath = Path.Combine(tempDir, origName);
                fuOcr.SaveAs(tempPath);

                // อัปโหลดเข้า OCR inbox (ไม่สร้างเอกสารอัตโนมัติ — ผู้ใช้ตรวจทานก่อน)
                var up = RunSync(() => client.UploadOcrAsync(tempPath, autoCreate: false));
                if (up == null || up.data == null)
                {
                    Msg(litStatus, "err", "อัปโหลด OCR ไม่สำเร็จ: " + (up?.message ?? "ไม่มีข้อมูลตอบกลับ"));
                    return;
                }

                var result = up.data;
                Guid scanId = result.Id;
                hfScanId.Value = scanId.ToString();
                hfHintKind.Value = ddlTargetType.SelectedValue ?? "";
                Session[FileKey(scanId)] = tempPath;
                Session[NameKey(scanId)] = origName;
                keepFile = true;
                ViewState["ocrTicks"] = 0;

                if (IsTerminal(result.ScanStatus))
                {
                    if (string.Equals(result.ScanStatus, "Failed", StringComparison.OrdinalIgnoreCase))
                        Msg(litStatus, "err", "OCR ประมวลผลไม่สำเร็จ: " + (result.ProcessingNotes ?? "ไม่ทราบสาเหตุ"));
                    else
                    {
                        Response.Redirect(ReviewUrl(scanId, hfHintKind.Value), false);
                        Context.ApplicationInstance.CompleteRequest();
                    }
                }
                else
                {
                    pnlScanning.Visible = true;
                    tmrPoll.Enabled = true;
                    Msg(litStatus, "info", "อัปโหลดแล้ว — กำลังประมวลผล OCR (จะเปิดหน้าตรวจทานให้อัตโนมัติ)");
                }
            }
            catch (AuthenticationFailedException ax)
            {
                Msg(litStatus, "err", "ยืนยันตัวตน API ล้มเหลว: " + ax.Message);
            }
            catch (AccountingApiException ae)
            {
                Msg(litStatus, "err", "OCR ผิดพลาด (" + ae.StatusCode + "): " + OcrReviewService.ApiMessage(ae));
            }
            catch (Exception ex)
            {
                Msg(litStatus, "err", "เกิดข้อผิดพลาด: " + ex.Message);
            }
            finally
            {
                if (!keepFile && tempPath != null) TryDeleteScanFile(tempPath);
            }
        }

        private static string ReviewUrl(Guid scanId, string kind) =>
            "OcrUpload.aspx?scan=" + scanId + (OcrDocKind.IsKnown(kind) ? "&kind=" + kind : "");

        /// <summary>โพลล์สถานะ OCR แบบ async (UpdatePanel + Timer) — เสร็จแล้วพาไปหน้าตรวจทาน; cap ~30 รอบ (~75 วิ)</summary>
        protected void tmrPoll_Tick(object sender, EventArgs e)
        {
            Server.ScriptTimeout = 600;
            Guid scanId;
            if (!Guid.TryParse(hfScanId.Value, out scanId))
            {
                tmrPoll.Enabled = false;
                pnlScanning.Visible = false;
                return;
            }
            int ticks = 0;
            int.TryParse(ViewState["ocrTicks"]?.ToString(), out ticks);
            ticks++;
            ViewState["ocrTicks"] = ticks;
            try
            {
                var result = RunSync(() => NewClient().GetOcrResultAsync(scanId))?.data;
                if (result != null && IsTerminal(result.ScanStatus))
                {
                    tmrPoll.Enabled = false;
                    pnlScanning.Visible = false;
                    if (string.Equals(result.ScanStatus, "Failed", StringComparison.OrdinalIgnoreCase))
                        Msg(litStatus, "err", "OCR ประมวลผลไม่สำเร็จ: " + (result.ProcessingNotes ?? "ไม่ทราบสาเหตุ"));
                    else
                    {
                        Msg(litStatus, "ok", "สแกนเสร็จแล้ว — กำลังเปิดหน้าตรวจทาน…");
                        ScriptManager.RegisterStartupScript(upOcr, GetType(), "ocrGoReview",
                            "location.href=" + JsString(ReviewUrl(scanId, hfHintKind.Value)) + ";", true);
                    }
                    return;
                }
                if (ticks >= 30)
                {
                    tmrPoll.Enabled = false;
                    pnlScanning.Visible = false;
                    Msg(litStatus, "warn", "OCR ยังประมวลผลไม่เสร็จ — เปิดหน้าตรวจทานภายหลังจากรายการ \"งานที่ยังไม่ส่ง\" หรือ "
                        + "ลิงก์นี้: OcrUpload.aspx?scan=" + scanId);
                    return;
                }
                Msg(litStatus, "info", "กำลังประมวลผล OCR… (รอบ " + ticks + ")");
            }
            catch (Exception ex)
            {
                if (ticks >= 30)
                {
                    tmrPoll.Enabled = false;
                    pnlScanning.Visible = false;
                    Msg(litStatus, "err", "ตรวจผล OCR ล้มเหลว: " + ex.Message);
                }
            }
        }

        // ══════════════════════════════════════════════════════════════
        // 2) หน้าตรวจทาน
        // ══════════════════════════════════════════════════════════════

        /// <summary>เปิดหน้าตรวจทาน: draft (DB → Session) หรือสร้างจากผล OCR ครั้งแรก</summary>
        private void ShowReview(Guid scanId, string hintKind)
        {
            var row = Svc.Load(scanId);
            if (row != null && (row.Status == OcrReviewService.StDone || row.Status == OcrReviewService.StEvidence))
            {
                ShowFinished(row);
                return;
            }

            var state = OcrReviewService.ParseState(row?.ReviewJson) ?? OcrReviewService.ParseState(Session[DraftKey(scanId)] as string);
            OcrResultResponse ocr = null;
            try { ocr = RunSync(() => NewClient().GetOcrResultAsync(scanId))?.data; }
            catch (Exception ex)
            {
                if (state == null)
                {
                    Msg(litStatus, "err", "อ่านผลสแกนจาก NextAcc ไม่สำเร็จ: " + OcrReviewService.ApiMessage(ex));
                    RenderDrafts();
                    return;
                }
                _serverNotes.Add("อ่านผลสแกนล่าสุดจาก NextAcc ไม่สำเร็จ — แสดงร่างที่บันทึกไว้ (" + OcrReviewService.ShortError(ex) + ")");
            }

            if (state == null)
            {
                if (ocr == null) { Msg(litStatus, "err", "ไม่พบผลสแกนนี้ใน NextAcc"); RenderDrafts(); return; }
                if (!IsTerminal(ocr.ScanStatus))
                {
                    hfScanId.Value = scanId.ToString();
                    hfHintKind.Value = hintKind ?? "";
                    pnlScanning.Visible = true;
                    tmrPoll.Enabled = true;
                    Msg(litStatus, "info", "OCR ยังประมวลผลอยู่ — จะเปิดหน้าตรวจทานให้อัตโนมัติ");
                    return;
                }
                if (string.Equals(ocr.ScanStatus, "Failed", StringComparison.OrdinalIgnoreCase))
                {
                    Msg(litStatus, "err", "OCR ประมวลผลไม่สำเร็จ: " + (ocr.ProcessingNotes ?? "ไม่ทราบสาเหตุ") + " — สแกนใหม่อีกครั้ง");
                    RenderDrafts();
                    return;
                }
                state = OcrReviewService.BuildInitialState(ocr, hintKind);
                string raw = OcrReviewService.ToJson(state);
                Session[RawKey(scanId)] = raw;
                Session[DraftKey(scanId)] = raw;
                // ใบลด/เพิ่มหนี้: ค้นใบต้นทางจาก "อ้างอิงใบกำกับภาษีเลขที่" ให้เลย — ตรงใบเดียว = เลือก + ตั้งบัญชี/ขาเงินตามใบเดิม
                if (OcrDocKind.IsNote(state.DocKind)) { AutoFindSource(state); Session[DraftKey(scanId)] = OcrReviewService.ToJson(state); }
                Svc.SaveDraft(state, raw, ocr.OriginalFileName ?? Session[NameKey(scanId)] as string, UserName);
                if (ocr.CreatedDocumentId.HasValue)
                    _serverNotes.Add("สแกนนี้มีเอกสารบน NextAcc แล้ว — กดส่งจะใช้เอกสารเดิม (แก้ร่าง/อนุมัติต่อ) ไม่สร้างใหม่");
            }
            if (ocr != null)
            {
                ViewState["ocrMeta"] = OcrReviewService.ToJson(OcrMeta(ocr));
                ViewState["fileName"] = ocr.OriginalFileName;
            }
            if (row != null && row.NexaaccDocId.HasValue)
                _serverNotes.Add("มีเอกสารบน NextAcc แล้ว (" + (row.NexaaccDocNo ?? row.NexaaccDocId.ToString()) + ", สถานะ " + row.Status
                    + ") — กดส่งจะทำต่อจากขั้นที่ค้าง ไม่สร้างใหม่" + (string.IsNullOrEmpty(row.LastError) ? "" : " · ล่าสุด: " + row.LastError));
            RenderReview(state);
        }

        private void RenderReview(OcrReviewState s)
        {
            Guid scanId = Guid.Parse(s.ScanId);
            pnlUpload.Visible = false;
            pnlReview.Visible = true;
            hfScanId.Value = s.ScanId;
            var row = Svc.Load(scanId);
            // ทิ้งได้จนกว่าจะอนุมัติ (มีร่างบน NextAcc = ลบร่างด้วย — DiscardAsync)
            btnDiscard.Visible = row == null || (row.Status != OcrReviewService.StApproved && row.Status != OcrReviewService.StDone && row.Status != OcrReviewService.StEvidence);

            string fileName = ViewState["fileName"] as string ?? row?.FileName ?? Session[NameKey(scanId)] as string ?? "เอกสาร";
            var head = new StringBuilder();
            head.Append("<div class=\"ocr-hint\">ไฟล์ <b>").Append(Server.HtmlEncode(fileName)).Append("</b> · สแกน ")
                .Append(scanId.ToString("N").Substring(0, 8));
            if (row != null) head.Append(" · สถานะ ").Append(Server.HtmlEncode(row.Status));
            if (!Svc.HasLogTable()) head.Append(" · <span style=\"color:#c05621;\">ร่างเก็บใน session (ยังไม่รัน PHASE19_38)</span>");
            head.Append("</div>");
            litReviewHead.Text = head.ToString();

            hfReviewJson.Value = OcrReviewService.ToJson(s);
            hfMetaJson.Value = OcrReviewService.ToJson(BuildMeta(s, row, fileName));
        }

        private Dictionary<string, object> BuildMeta(OcrReviewState s, OcrLogRow row, string fileName)
        {
            var m = new Dictionary<string, object>();
            m["scanId"] = s.ScanId;
            m["fileName"] = fileName;
            m["previewUrl"] = "OcrUpload.aspx?preview=" + s.ScanId;
            m["previewIsPdf"] = (Path.GetExtension(fileName ?? "") ?? "").Equals(".pdf", StringComparison.OrdinalIgnoreCase);
            bool synced;
            m["accounts"] = LoadAccountOptions(out synced);
            m["chartSynced"] = synced;
            m["money"] = LoadMoneyOptions();
            m["paidHow"] = LoadPaidHowAccounts().Select(x => (object)new Dictionary<string, object> { { "id", x.Item1.ToString() }, { "n", x.Item2 } }).ToList();
            m["reasons"] = new Dictionary<string, object>
            {
                { "VCN", OcrNoteReasons.Credit.Select(ReasonMeta).ToList() },
                { "VDN", OcrNoteReasons.Debit.Select(ReasonMeta).ToList() }
            };
            m["vendors"] = LoadVendorOptions();
            var ocrJson = ViewState["ocrMeta"] as string;
            m["ocr"] = string.IsNullOrEmpty(ocrJson) ? new Dictionary<string, object>() : OcrReviewService.Json().DeserializeObject(ocrJson);
            var cn = ViewState["cnResults"] as string;
            m["cnResults"] = string.IsNullOrEmpty(cn) ? null : OcrReviewService.Json().DeserializeObject(cn);
            var cnNotes = ViewState["cnNotes"] as string;
            m["cnNotes"] = string.IsNullOrEmpty(cnNotes) ? null : OcrReviewService.Json().DeserializeObject(cnNotes);
            AddVendorCnMeta(m, s);
            m["serverErrors"] = _serverErrors;
            m["serverNotes"] = _serverNotes;
            m["duplicate"] = _duplicate;
            m["nextaccDupWarn"] = _nextaccDupWarn || s.AllowDuplicate;
            m["locked"] = false;
            return m;
        }

        /// <summary>
        /// PHASE19_39 — ใบลดหนี้ผู้ขาย = ตัวจริงของการคืนสินค้า: รายการคืนที่รอใบลดหนี้ (ผู้ขายเดียวกันก่อน), รหัสบัญชีสินค้าคงเหลือ,
        /// ป้ายบัญชีเครดิตผู้ขาย. ไม่มีตาราง/ปิด Vendor_Cn_Primary → cnPrimary=false (หน้าทำงานแบบเดิม: ตัวเลือก "หลักฐาน PRT")
        /// </summary>
        private void AddVendorCnMeta(Dictionary<string, object> m, OcrReviewState s)
        {
            bool primary = false;
            try
            {
                var vcs = new Take_Time_BangPhra.Purchasing.VendorCnService(Conn);
                primary = vcs.CnPrimary();
                if (!primary) return;
                m["inventoryCode"] = vcs.MappedCode("INVENTORY");
                string vcr = vcs.MappedCode(Take_Time_BangPhra.Purchasing.VendorCnService.MapVendorCredit);
                string ap = vcs.MappedCode("ACCOUNTS_PAYABLE");
                m["vendorCreditLabel"] = string.IsNullOrEmpty(vcr)
                    ? "เจ้าหนี้การค้า " + ap + " (ยังไม่ผูกบัญชี VENDOR_CREDIT_RECEIVABLE)"
                    : "บัญชี " + vcr + " ลูกหนี้ผู้ขาย/เครดิตจากผู้ขาย";
                var list = vcs.ListPending(80);
                Guid scanId;
                if (Guid.TryParse(s.ScanId ?? "", out scanId))
                {
                    var mine = vcs.GetByScan(scanId);
                    if (mine != null && !list.Any(x => x.ReturnRef == mine.ReturnRef)) list.Insert(0, mine);
                }
                string tax = OcrReviewMath.Digits(s.SupplierTaxId);
                m["pendingReturns"] = list
                    .OrderByDescending(r => tax.Length == 13 && Take_Time_BangPhra.Purchasing.VendorCnService.Digits(r.SupplierTaxId) == tax)
                    .ThenByDescending(r => r.CreatedAt)
                    .Select(r => (object)new Dictionary<string, object>
                    {
                        { "ref", r.ReturnRef }, { "supplier", r.SupplierName ?? "" }, { "taxId", r.SupplierTaxId ?? "" },
                        { "net", r.PurchaseNet }, { "vat", r.VatAmount }, { "carry", r.CarryingCost },
                        { "date", r.CreatedAt.ToString("dd/MM/yy", CultureInfo.InvariantCulture) }, { "refund", r.IsRefund },
                        { "accId", r.PaidHowAccountId ?? "" }
                    }).ToList();
            }
            catch { }
            finally { m["cnPrimary"] = primary; }
        }

        private static Dictionary<string, object> OcrMeta(OcrResultResponse r)
        {
            var o = new Dictionary<string, object>();
            if (r.Quality != null) o["quality"] = new Dictionary<string, object> { { "l", r.Quality.Letter }, { "s", r.Quality.Score }, { "c", r.Quality.Color } };
            o["engine"] = r.OcrEngine;
            o["target"] = r.TargetDocumentType;
            o["role"] = r.OurRole;
            o["scanned"] = r.ScannedDocumentType ?? r.DocumentType;
            o["handwriting"] = r.HasHandwriting;
            o["asset"] = r.HasPotentialFixedAsset;
            o["duplicate"] = r.IsDuplicate;
            o["compliance"] = (r.ComplianceIssues ?? new List<OcrScanIssueDto>())
                .Select(c => (object)new Dictionary<string, object> { { "s", c.Severity }, { "m", c.Message } }).ToList();
            return o;
        }

        /// <summary>สถานะที่หน้าเว็บส่งกลับมา — ScanId มาจาก query string เสมอ (ไม่เชื่อค่าในฟอร์ม)</summary>
        private OcrReviewState ReadPostedState()
        {
            var scan = QueryScanId;
            if (!scan.HasValue) return null;
            var s = OcrReviewService.ParseState(hfReviewJson.Value)
                    ?? OcrReviewService.ParseState(Svc.Load(scan.Value)?.ReviewJson)
                    ?? OcrReviewService.ParseState(Session[DraftKey(scan.Value)] as string);
            if (s == null) return null;
            s.ScanId = scan.Value.ToString();
            if (!OcrDocKind.IsKnown(s.DocKind)) s.DocKind = OcrDocKind.PaymentVoucher;
            if (s.Lines == null) s.Lines = new List<OcrReviewLine>();
            if (s.LowConfidence == null) s.LowConfidence = new List<string>();
            if (OcrReviewMath.IsValidThaiTaxId(s.SupplierTaxId)) s.SupplierTaxId = OcrReviewMath.Digits(s.SupplierTaxId);
            string br = OcrReviewMath.NormalizeBranch(s.SupplierBranch);
            if (OcrReviewMath.IsValidBranch(br)) s.SupplierBranch = br;
            s.Lines.RemoveAll(l => l == null);
            if (s.SourceLines == null) s.SourceLines = new List<OcrSourceLine>();
            s.NoteReasonCode = string.IsNullOrWhiteSpace(s.NoteReasonCode) ? null : s.NoteReasonCode.Trim().ToUpperInvariant();
            if (OcrDocKind.IsNote(s.DocKind))
            {
                if (s.NoteReasonCode == null && s.NoteReason >= 1 && s.NoteReason <= 4) s.NoteReasonCode = OcrNoteReasons.FromLegacy(s.DocKind, s.NoteReason);   // ร่างเก่า
                var rs = OcrNoteReasons.Find(s.DocKind, s.NoteReasonCode);
                s.NoteReason = rs != null ? rs.NextAcc : 0;
            }
            foreach (var l in s.Lines)
            {
                l.Description = (l.Description ?? "").Trim();
                l.AccountCode = (l.AccountCode ?? "").Trim();
                if (l.VatType != "0" && l.VatType != "EX") l.VatType = "7";
                if (l.VatType != "7") { l.VatOverride = null; l.VatClaimable = true; }
            }
            Session[DraftKey(scan.Value)] = OcrReviewService.ToJson(s);
            return s;
        }

        private bool ReviewLost()
        {
            Msg(litResult, "err", "ไม่พบข้อมูลตรวจทาน (session หมดอายุ?) — เปิดลิงก์สแกนนี้ใหม่หรือสแกนอีกครั้ง");
            pnlReview.Visible = false;
            pnlUpload.Visible = true;
            RenderDrafts();
            return false;
        }

        protected void btnSaveDraft_Click(object sender, EventArgs e)
        {
            var s = ReadPostedState();
            if (s == null) { ReviewLost(); return; }
            bool db = Svc.SaveDraft(s, Session[RawKey(Guid.Parse(s.ScanId))] as string, Session[NameKey(Guid.Parse(s.ScanId))] as string, UserName);
            _serverNotes.Add(db ? "บันทึกร่างแล้ว — กลับมาทำต่อได้จากหน้าสแกน (รายการ \"งานที่ยังไม่ส่ง\") หรือลิงก์หน้านี้"
                                : "บันทึกร่างไว้ใน session แล้ว (ยังไม่รัน PHASE19_38 — ปิดเบราว์เซอร์/หมด session = หาย)");
            RenderReview(s);
        }

        protected void btnCnSearch_Click(object sender, EventArgs e)
        {
            Server.ScriptTimeout = 300;
            var s = ReadPostedState();
            if (s == null) { ReviewLost(); return; }
            var notes = new List<string>();
            try
            {
                var results = RunSync(() => OcrReviewService.SearchSourceDocsAsync(NewClient(), s, notes));
                ViewState["cnResults"] = OcrReviewService.ToJson(results);
                // ตรงเลขใบกำกับเดิมใบเดียว และยังไม่ได้เลือก ⇒ เลือกให้เลย
                var exact = results.Where(x => x.Exact).ToList();
                if (string.IsNullOrWhiteSpace(s.SourceDocId) && exact.Count == 1)
                {
                    s.SourceDocId = exact[0].Id;
                    LoadSource(s, notes);
                    notes.Insert(0, "เลือกใบต้นทาง " + exact[0].No + " ให้แล้ว (ตรงเลขใบกำกับเดิม " + s.OrigTaxInvoiceNo + ") — เปลี่ยนได้");
                }
            }
            catch (Exception ex)
            {
                notes.Add("ค้นเอกสารต้นทางไม่สำเร็จ: " + OcrReviewService.ApiMessage(ex));
                ViewState["cnResults"] = null;
            }
            ViewState["cnNotes"] = OcrReviewService.ToJson(notes);
            Svc.SaveDraft(s, null, null, UserName);
            RenderReview(s);
        }

        /// <summary>เลือกใบต้นทางจากผลค้น (JS ใส่ SourceDocId แล้วกดปุ่มซ่อน) → โหลดบรรทัด/บัญชี/การจ่ายของใบเดิม</summary>
        protected void btnCnPick_Click(object sender, EventArgs e)
        {
            Server.ScriptTimeout = 300;
            var s = ReadPostedState();
            if (s == null) { ReviewLost(); return; }
            var notes = new List<string>();
            LoadSource(s, notes);
            ViewState["cnNotes"] = OcrReviewService.ToJson(notes);
            Svc.SaveDraft(s, null, null, UserName);
            RenderReview(s);
        }

        private void LoadSource(OcrReviewState s, List<string> notes)
        {
            try
            {
                bool cnPrimary = false;
                try { cnPrimary = new Take_Time_BangPhra.Purchasing.VendorCnService(Conn).CnPrimary(); } catch { }
                var money = LoadPaidHowAccounts();
                Guid cg = new AccountingConfig(Conn).CompanyId;
                string cid = cg == Guid.Empty ? null : cg.ToString();
                RunSync(() => OcrReviewService.LoadSourceDetailAsync(NewClient(), s, money.ToDictionary(x => x.Item1, x => x.Item2), cnPrimary, cid, notes));
            }
            catch (Exception ex) { notes.Add("โหลดใบต้นทางไม่สำเร็จ: " + OcrReviewService.ApiMessage(ex)); }
        }

        /// <summary>ครั้งแรกที่เปิดใบลด/เพิ่มหนี้: ค้นด้วยเลขใบกำกับเดิม (best-effort ไม่บล็อกหน้า)</summary>
        private void AutoFindSource(OcrReviewState s)
        {
            if (string.IsNullOrWhiteSpace(s.OrigTaxInvoiceNo) || !string.IsNullOrWhiteSpace(s.SourceDocId)) return;
            var notes = new List<string>();
            try
            {
                var results = RunSync(() => OcrReviewService.SearchSourceDocsAsync(NewClient(), s, notes));
                ViewState["cnResults"] = OcrReviewService.ToJson(results);
                var exact = results.Where(x => x.Exact).ToList();
                if (exact.Count == 1)
                {
                    s.SourceDocId = exact[0].Id;
                    LoadSource(s, notes);
                    notes.Insert(0, "พบใบต้นทาง " + exact[0].No + " จากเลขใบกำกับเดิม " + s.OrigTaxInvoiceNo + " — เลือกให้แล้ว (เปลี่ยนได้)");
                }
                else if (exact.Count > 1) notes.Insert(0, "พบใบต้นทางที่อ้างเลข " + s.OrigTaxInvoiceNo + " หลายใบ — เลือกใบที่ถูกจากตาราง");
            }
            catch (Exception ex) { notes.Add("ค้นใบต้นทางอัตโนมัติไม่สำเร็จ: " + OcrReviewService.ShortError(ex)); }
            ViewState["cnNotes"] = OcrReviewService.ToJson(notes);
        }

        protected void btnSubmit_Click(object sender, EventArgs e) => DoSubmit(false);

        protected void btnSubmitAck_Click(object sender, EventArgs e) => DoSubmit(true);

        private void DoSubmit(bool acknowledge)
        {
            Server.ScriptTimeout = 600;   // create + update + approve + แนบไฟล์ หลาย call
            pnlWarnings.Visible = false;
            var s = ReadPostedState();
            if (s == null) { ReviewLost(); return; }
            Guid scanId = Guid.Parse(s.ScanId);
            string scanFile = Session[FileKey(scanId)] as string;
            string prepName, prepSig;
            ResolvePreparer(out prepName, out prepSig);
            var ctx = new OcrSubmitContext
            {
                UserName = UserName,
                PreparerName = prepName,
                PreparerSignature = prepSig,
                ScanFilePath = scanFile,
                AcknowledgeWarnings = acknowledge
            };

            OcrSubmitResult r;
            try
            {
                r = RunSync(() => Svc.SubmitAsync(NewClient(), s, ctx));
            }
            catch (AuthenticationFailedException ax)
            {
                r = new OcrSubmitResult();
                r.Errors.Add("ยืนยันตัวตน API ล้มเหลว: " + ax.Message);
            }
            catch (Exception ex)
            {
                r = new OcrSubmitResult();
                r.Errors.Add("เกิดข้อผิดพลาด: " + OcrReviewService.ApiMessage(ex));
            }

            if (r.Ok)
            {
                if (r.DocId.HasValue)
                {
                    var docs = Session[DocsKey] as HashSet<Guid> ?? new HashSet<Guid>();
                    docs.Add(r.DocId.Value);
                    Session[DocsKey] = docs;
                }
                if (!string.IsNullOrEmpty(scanFile)) TryDeleteScanFile(scanFile);
                Session.Remove(FileKey(scanId));
                Session.Remove(DraftKey(scanId));
                Session.Remove(RawKey(scanId));
                ShowSuccess(r);
                return;
            }

            _serverNotes.AddRange(r.Notes);
            if (r.NeedsAck)
            {
                pnlWarnings.Visible = true;
                litWarnings.Text = "<ul>" + string.Join("", r.ApproveWarnings.Select(w => "<li>" + Server.HtmlEncode(w) + "</li>")) + "</ul>";
                _serverNotes.Insert(0, r.Message);
            }
            else
            {
                _serverErrors.AddRange(r.Errors);
                if (r.Errors.Any(x => x.Contains("ไม่ใช่ใบซ้ำ"))) _nextaccDupWarn = true;
                if (r.DuplicateDocId.HasValue || !string.IsNullOrEmpty(r.DuplicateDocNo))
                {
                    var d = new Dictionary<string, object> { { "text", "เอกสารที่ซ้ำ: " + (r.DuplicateDocNo ?? r.DuplicateDocId.ToString()) } };
                    if (r.DuplicateDocId.HasValue && Svc.IsKnownDocument(r.DuplicateDocId.Value)) d["pdf"] = "OcrUpload.aspx?pdf=" + r.DuplicateDocId.Value;
                    _duplicate = d;
                }
            }
            RenderReview(s);
        }

        protected void btnDiscard_Click(object sender, EventArgs e)
        {
            var scan = QueryScanId;
            if (!scan.HasValue) { Response.Redirect("OcrUpload.aspx", false); return; }
            string err = DiscardScan(scan.Value);
            if (err != null)
            {
                var s = ReadPostedState();
                _serverErrors.Add(err);
                if (s != null) RenderReview(s);
                return;
            }
            Response.Redirect("OcrUpload.aspx?discarded=1", false);
        }

        /// <summary>ปุ่ม "ทิ้ง" ในรายการงานที่ยังไม่ส่ง</summary>
        protected void btnDiscardRow_Click(object sender, EventArgs e)
        {
            Guid scanId;
            if (!Guid.TryParse(hfDiscardScan.Value ?? "", out scanId)) { RenderDrafts(); return; }
            hfDiscardScan.Value = "";
            string err = DiscardScan(scanId);
            if (err != null) { Msg(litStatus, "err", err); RenderDrafts(); return; }
            Response.Redirect("OcrUpload.aspx?discarded=1", false);
        }

        /// <summary>ทิ้งงาน (ลบร่างบน NextAcc เมื่อมี) + ล้างไฟล์/ร่างใน session — null = สำเร็จ</summary>
        private string DiscardScan(Guid scanId)
        {
            string err;
            try
            {
                AccountingApiClient client = null;
                try { client = NewClient(); } catch { client = null; }
                err = RunSync(() => Svc.DiscardAsync(client, scanId, UserName));
            }
            catch (Exception ex) { err = "ทิ้งงานไม่สำเร็จ: " + ex.Message; }
            if (err != null) return err;
            string f = Session[FileKey(scanId)] as string;
            if (!string.IsNullOrEmpty(f)) TryDeleteScanFile(f);
            Session.Remove(FileKey(scanId));
            Session.Remove(DraftKey(scanId));
            Session.Remove(RawKey(scanId));
            return null;
        }

        private void ShowSuccess(OcrSubmitResult r)
        {
            pnlReview.Visible = false;
            pnlUpload.Visible = true;
            RenderDrafts();
            var h = new StringBuilder();
            h.Append("<div class=\"ocr-card\" style=\"border:2px solid #38a169;\"><h3 style=\"color:#276749;\">✓ ").Append(Server.HtmlEncode(r.Message)).Append("</h3>");
            if (!string.IsNullOrEmpty(r.LinkedReturnRef))
                h.Append("<p><b>↩️ ผูกกับการคืนสินค้า ").Append(Server.HtmlEncode(r.LinkedReturnRef))
                 .Append("</b> — ใบลดหนี้นี้เป็นรายการบัญชี/ภาษีตัวจริง (ดูสถานะที่ <a href=\"../Product/PurchaseReturn\">หน้าคืนสินค้าให้ผู้ขาย</a>)</p>");
            if (!string.IsNullOrEmpty(r.ContactFilled))
                h.Append("<p><b>เติมข้อมูลผู้ขายใน NextAcc:</b> ").Append(Server.HtmlEncode(r.ContactFilled)).Append(" <small>(เฉพาะช่องที่ว่าง — ไม่ทับข้อมูลเดิม)</small></p>");
            if (r.DocId.HasValue && !r.Evidence)
                h.Append("<p><a class=\"ocr-btn\" style=\"text-decoration:none;\" target=\"_blank\" href=\"OcrUpload.aspx?pdf=").Append(r.DocId.Value)
                 .Append("\">📄 เปิด PDF เอกสาร NextAcc</a></p>");
            var notes = r.Notes.Where(n => string.IsNullOrEmpty(r.ContactFilled) || !n.StartsWith("เติมข้อมูลผู้ขายใน NextAcc:", StringComparison.Ordinal)).ToList();
            if (notes.Count > 0)
                h.Append("<ul class=\"ocr-hint\">").Append(string.Join("", notes.Select(n => "<li>" + Server.HtmlEncode(n) + "</li>"))).Append("</ul>");
            h.Append("<p class=\"ocr-hint\">สแกนใบถัดไปได้จากฟอร์มด้านบน</p></div>");
            litResult.Text = h.ToString();
        }

        private void ShowFinished(OcrLogRow row)
        {
            pnlReview.Visible = false;
            pnlUpload.Visible = true;
            RenderDrafts();
            var h = new StringBuilder();
            h.Append("<div class=\"ocr-card\" style=\"border:2px solid #38a169;\"><h3 style=\"color:#276749;\">สแกนนี้ส่งเรียบร้อยแล้ว</h3><p>")
             .Append(Server.HtmlEncode(OcrDocKind.Label(row.DocKind))).Append(" · ").Append(Server.HtmlEncode(row.SupplierName ?? ""))
             .Append(" เลขที่ ").Append(Server.HtmlEncode(row.SupplierDocNo ?? "-"));
            if (row.Status == OcrReviewService.StEvidence) h.Append(" — เก็บเป็นหลักฐานการคืนสินค้า (ไม่มีเอกสารบน NextAcc)");
            else h.Append(" → NextAcc ").Append(Server.HtmlEncode(row.NexaaccDocNo ?? row.NexaaccDocId?.ToString() ?? ""));
            h.Append("</p>");
            try
            {
                var lk = new Take_Time_BangPhra.Purchasing.VendorCnService(Conn).GetByScan(row.ScanId);
                if (lk != null)
                    h.Append("<p>↩️ ผูกกับการคืนสินค้า <b>").Append(Server.HtmlEncode(lk.ReturnRef)).Append("</b> · ")
                     .Append(Server.HtmlEncode((lk.RevRef != null ? "กลับ " + lk.RevRef + " · " : "") + (lk.DifRef != null ? "ปรับต้นทุน " + (lk.DifAmount ?? 0m).ToString("#,##0.00", CultureInfo.InvariantCulture) + " (" + lk.DifRef + ")" : "ต้นทุนตรง")))
                     .Append("</p>");
            }
            catch { }
            if (row.NexaaccDocId.HasValue)
                h.Append("<a class=\"ocr-btn\" style=\"text-decoration:none;\" target=\"_blank\" href=\"OcrUpload.aspx?pdf=").Append(row.NexaaccDocId.Value).Append("\">📄 เปิด PDF</a>");
            h.Append("</div>");
            litResult.Text = h.ToString();
        }

        /// <summary>งานที่ยังไม่ส่ง (draft / ร่างบน NextAcc / ล้ม) — ลิงก์กลับไปทำต่อ</summary>
        private void RenderDrafts()
        {
            var rows = Svc.ListOpen();
            if (rows.Count == 0) { litDrafts.Text = ""; return; }
            var h = new StringBuilder();
            h.Append("<h4 style=\"margin:16px 0 6px;\">งานที่ยังไม่ส่ง (ทำต่อได้)</h4><div style=\"overflow-x:auto;\"><table class=\"tbl\"><thead><tr>")
             .Append("<th>ไฟล์</th><th>ชนิด</th><th>ผู้ขาย</th><th>เลขที่</th><th class=\"num\">ยอด</th><th>สถานะ</th><th>แก้ล่าสุด</th><th></th></tr></thead><tbody>");
            foreach (var r in rows)
            {
                h.Append("<tr><td>").Append(Server.HtmlEncode(r.FileName ?? "-")).Append("</td><td>").Append(Server.HtmlEncode(OcrDocKind.Label(r.DocKind)))
                 .Append("</td><td>").Append(Server.HtmlEncode(r.SupplierName ?? "")).Append("</td><td>").Append(Server.HtmlEncode(r.SupplierDocNo ?? ""))
                 .Append("</td><td class=\"num\">").Append(r.Total.HasValue ? r.Total.Value.ToString("#,##0.00", CultureInfo.InvariantCulture) : "")
                 .Append("</td><td>").Append(Server.HtmlEncode(r.Status)).Append(string.IsNullOrEmpty(r.NexaaccDocNo) ? "" : "<br/><small>" + Server.HtmlEncode(r.NexaaccDocNo) + "</small>")
                 .Append(string.IsNullOrEmpty(r.LastError) ? "" : "<br/><small style=\"color:#c53030;\">" + Server.HtmlEncode(r.LastError.Length > 120 ? r.LastError.Substring(0, 120) + "…" : r.LastError) + "</small>")
                 .Append("</td><td>").Append(r.UpdatedAt.HasValue ? r.UpdatedAt.Value.ToString("dd/MM HH:mm", CultureInfo.InvariantCulture) : "")
                 .Append("</td><td style=\"white-space:nowrap;\"><a href=\"OcrUpload.aspx?scan=").Append(r.ScanId).Append("\">ทำต่อ →</a>");
                // ทิ้ง: ส่งแล้ว (APPROVED) ทิ้งไม่ได้ — ซ่อนปุ่ม
                if (r.Status != OcrReviewService.StApproved)
                    h.Append(" <button type=\"button\" class=\"ocr-del\" onclick=\"return OCR_discardRow(this);\" data-scan=\"").Append(r.ScanId)
                     .Append("\" data-name=\"").Append(Server.HtmlEncode(r.FileName ?? r.SupplierDocNo ?? ""))
                     .Append("\" data-doc=\"").Append(r.NexaaccDocId.HasValue ? Server.HtmlEncode(r.NexaaccDocNo ?? "เอกสาร") : "")
                     .Append("\" title=\"ทิ้งงานนี้").Append(r.NexaaccDocId.HasValue ? " (ลบร่างบน NextAcc ด้วย)" : "").Append("\">🗑 ทิ้ง</button>");
                h.Append("</td></tr>");
            }
            h.Append("</tbody></table></div>");
            litDrafts.Text = h.ToString();
        }

        // ══════════════════════════════════════════════════════════════
        // ไฟล์: ภาพเอกสาร (preview) / PDF ของเอกสาร NextAcc
        // ══════════════════════════════════════════════════════════════

        private void ServePreview()
        {
            Guid scanId;
            if (!Guid.TryParse(Request.QueryString["preview"], out scanId)) { EndWith(404, "ไม่พบไฟล์"); return; }
            try
            {
                string path = Session[FileKey(scanId)] as string;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    SendBytes(File.ReadAllBytes(path), ContentTypeOf(path), Path.GetFileName(path));
                    return;
                }
                // ไฟล์ชั่วคราวหมดแล้ว (กลับมาทำ draft ต่อ) → ดึงต้นฉบับจาก NextAcc — เฉพาะสแกนที่หน้านี้รู้จัก
                if (Svc.Load(scanId) == null && Session[DraftKey(scanId)] == null) { EndWith(403, "ไม่มีสิทธิ์ดูไฟล์นี้"); return; }
                var img = RunSync(() => NewClient().GetOcrImageAsync(scanId));
                if (img == null || img.Item1 == null || img.Item1.Length == 0) { EndWith(404, "ไม่พบไฟล์ต้นฉบับใน NextAcc"); return; }
                SendBytes(img.Item1, img.Item2, "scan");
            }
            catch (Exception ex) { EndWith(500, "เปิดไฟล์ไม่สำเร็จ: " + ex.Message); }
        }

        private void ServePdf()
        {
            Guid docId;
            if (!Guid.TryParse(Request.QueryString["pdf"], out docId)) { EndWith(404, "ไม่พบเอกสาร"); return; }
            var docs = Session[DocsKey] as HashSet<Guid>;
            if (!(docs != null && docs.Contains(docId)) && !Svc.IsKnownDocument(docId)) { EndWith(403, "เปิดได้เฉพาะเอกสารที่สร้างจากหน้าตรวจทาน OCR"); return; }
            try
            {
                var pdf = RunSync(() => NewClient().GenerateDocumentPdfAsync(docId));
                if (pdf == null || pdf.Length == 0) { EndWith(404, "NextAcc ยังไม่มี PDF ของเอกสารนี้ (ไม่มีแม่แบบ/ยังไม่อนุมัติ)"); return; }
                SendBytes(pdf, "application/pdf", docId.ToString("N").Substring(0, 8) + ".pdf");
            }
            catch (Exception ex) { EndWith(500, "ดึง PDF จาก NextAcc ไม่สำเร็จ: " + ex.Message); }
        }

        private void SendBytes(byte[] bytes, string contentType, string fileName)
        {
            Response.Clear();
            Response.ContentType = contentType ?? "application/octet-stream";
            Response.AddHeader("Content-Disposition", "inline; filename=\"" + Uri.EscapeDataString(fileName ?? "file") + "\"");
            Response.AddHeader("X-Content-Type-Options", "nosniff");
            Response.Cache.SetCacheability(System.Web.HttpCacheability.Private);
            Response.BinaryWrite(bytes);
            Response.Flush();
            _served = true;
            Context.ApplicationInstance.CompleteRequest();
        }

        private void EndWith(int status, string text)
        {
            Response.Clear();
            Response.StatusCode = status;
            Response.ContentType = "text/plain; charset=utf-8";
            Response.Write(text);
            _served = true;
            Context.ApplicationInstance.CompleteRequest();
        }

        private static string ContentTypeOf(string path)
        {
            switch ((Path.GetExtension(path) ?? "").ToLowerInvariant())
            {
                case ".pdf": return "application/pdf";
                case ".png": return "image/png";
                case ".gif": return "image/gif";
                case ".webp": return "image/webp";
                case ".bmp": return "image/bmp";
                case ".tif": case ".tiff": return "image/tiff";
                case ".heic": return "image/heic";
                default: return "image/jpeg";
            }
        }

        // ══════════════════════════════════════════════════════════════
        // ตัวเลือก (ผังบัญชี NextAcc / แหล่งเงิน / ผู้ขาย)
        // ══════════════════════════════════════════════════════════════

        /// <summary>ผังบัญชีจาก cache NextAcc (Accounting_Nexaacc_Accounts) — codeFilter เป็นค่าคงที่ในโค้ด (ไม่ใช่ user input)</summary>
        private DataTable LoadNexaaccAccounts(string codeFilter)
        {
            try
            {
                return new Take_Time_BangPhra.code().DatabaseQuerySafe(Conn,
                    "SELECT Nexaacc_AccountId, Account_Code, Account_Name FROM Accounting_Nexaacc_Accounts " +
                    "WHERE ISNULL(Is_Active, 1) = 1 AND (" + codeFilter + ") ORDER BY Account_Code", null);
            }
            catch { return null; }
        }

        /// <summary>บัญชีฝั่งเดบิตของบรรทัด: ล้างรับของ (GR/IR) · ค่าใช้จ่าย 5x · สินทรัพย์ถาวร 12x · สินทรัพย์หมุนเวียน 11x (ไม่รวมเงินสด 111) ·
        /// เงินทดรองกรรมการ — ถ้ายังไม่ Sync ผังบัญชี → mapping เดิม (Account_Paid_Type)</summary>
        private List<object> LoadAccountOptions(out bool synced)
        {
            var list = new List<object>();
            synced = false;
            // GR/IR: ใบกำกับของสินค้าที่รับเข้าสต๊อกแล้ว → เดบิตบัญชีล้างรับของ (PurchaseService.ClearingAccountKey — PHASE19_31) ไม่ใช่ค่าใช้จ่าย
            string clearKey = "GRNI";
            try { clearKey = Take_Time_BangPhra.Purchasing.PurchaseService.ClearingAccountKey(new AccountingConfig(Conn)); } catch { }
            string grniCode = LookupMappedAccountCode(clearKey);
            if (!string.IsNullOrEmpty(grniCode))
                list.Add(Acc(grniCode, "🔄 ล้างรับของ " + (clearKey == "GRNI" ? "GRNI" : Take_Time_BangPhra.Purchasing.PurchaseService.ClearingAccountLabel(clearKey)),
                    "ล้างรับของ (สินค้าที่รับเข้าสต๊อกแล้ว)"));

            var na = LoadNexaaccAccounts("Account_Code LIKE '5%' OR Account_Code LIKE '12%' OR (Account_Code LIKE '11%' AND Account_Code NOT LIKE '111%')"
                + " OR (Account_Code LIKE '2%' AND Account_Name LIKE N'%กรรมการ%')");
            if (na != null && na.Rows.Count > 0)
            {
                synced = true;
                foreach (DataRow row in na.Rows)
                {
                    string code = row["Account_Code"]?.ToString() ?? "";
                    string g = code.StartsWith("5") ? "ค่าใช้จ่าย (5x)" : code.StartsWith("12") ? "สินทรัพย์ถาวร (12x)"
                             : code.StartsWith("11") ? "สินทรัพย์หมุนเวียน/สินค้า (11x)" : "หนี้สิน — เงินทดรองกรรมการ";
                    list.Add(Acc(code, row["Account_Name"]?.ToString() ?? "", g));
                }
                return list;
            }
            try
            {
                var dt = new Take_Time_BangPhra.code().DatabaseQuerySafe(Conn,
                    @"SELECT Paid_Type, Nexaacc_AccountCode FROM Account_Paid_Type
                      WHERE Status = 'True' AND Nexaacc_AccountCode IS NOT NULL AND LTRIM(RTRIM(Nexaacc_AccountCode)) <> ''
                      ORDER BY Paid_Type", null);
                if (dt != null)
                    foreach (DataRow row in dt.Rows)
                        list.Add(Acc(row["Nexaacc_AccountCode"]?.ToString() ?? "", row["Paid_Type"]?.ToString() ?? "", "หมวดค่าใช้จ่าย (mapping เดิม)"));
            }
            catch { }
            return list;
        }

        private static Dictionary<string, object> Acc(string c, string n, string g) =>
            new Dictionary<string, object> { { "c", c }, { "n", n }, { "g", g } };

        /// <summary>แหล่งเงิน (เงินสด/ธนาคาร 11x · หนี้สิน 21x เช่นเจ้าหนี้กรรมการ) value = Nexaacc_AccountId → fallback Account_Paid_How</summary>
        private List<object> LoadMoneyOptions()
        {
            var list = new List<object>();
            var na = LoadNexaaccAccounts("Account_Code LIKE '11%' OR Account_Code LIKE '21%'");
            if (na != null && na.Rows.Count > 0)
            {
                foreach (DataRow row in na.Rows)
                    list.Add(new Dictionary<string, object> { { "id", row["Nexaacc_AccountId"]?.ToString() ?? "" }, { "c", row["Account_Code"]?.ToString() ?? "" }, { "n", row["Account_Name"]?.ToString() ?? "" } });
                return list;
            }
            try
            {
                var dt = new Take_Time_BangPhra.code().DatabaseQuerySafe(Conn,
                    @"SELECT Paid_How, Nexaacc_AccountId FROM Account_Paid_How
                      WHERE Status = 'True' AND Nexaacc_AccountId IS NOT NULL AND LTRIM(RTRIM(Nexaacc_AccountId)) <> ''
                      ORDER BY Paid_How", null);
                if (dt != null)
                    foreach (DataRow row in dt.Rows)
                        list.Add(new Dictionary<string, object> { { "id", row["Nexaacc_AccountId"]?.ToString() ?? "" }, { "c", "" }, { "n", row["Paid_How"]?.ToString() ?? "" } });
            }
            catch { }
            return list;
        }

        private static object ReasonMeta(OcrNoteReasons.Reason r) => new Dictionary<string, object>
        {
            { "c", r.Code }, { "t", r.Label }, { "n", r.NextAcc }, { "phys", r.PhysicalReturn }, { "txt", r.NeedsText }, { "h", r.Hint ?? "" }
        };

        /// <summary>แหล่งเงิน (Account_Paid_How ที่ผูกบัญชี NextAcc) — (บัญชี NextAcc, "ชื่อแหล่งเงิน · รหัส ชื่อบัญชี") ใช้เป็นตัวเลือก
        /// "รับเงินคืนเข้า" ของใบลดหนี้ และใช้จับบัญชีที่ใบต้นทางจ่ายออก</summary>
        private List<Tuple<Guid, string>> LoadPaidHowAccounts()
        {
            var list = new List<Tuple<Guid, string>>();
            try
            {
                var dt = new Take_Time_BangPhra.code().DatabaseQuerySafe(Conn,
                    @"SELECT ph.Paid_How, ph.Nexaacc_AccountId, a.Account_Code, a.Account_Name
                        FROM Account_Paid_How ph
                        LEFT JOIN Accounting_Nexaacc_Accounts a ON CONVERT(NVARCHAR(64), a.Nexaacc_AccountId) = LTRIM(RTRIM(ph.Nexaacc_AccountId))
                       WHERE ph.Status = 'True' AND ph.Nexaacc_AccountId IS NOT NULL AND LTRIM(RTRIM(ph.Nexaacc_AccountId)) <> ''
                       ORDER BY ph.Paid_How", null);
                var seen = new HashSet<Guid>();
                if (dt != null)
                    foreach (DataRow row in dt.Rows)
                    {
                        Guid g;
                        if (!Guid.TryParse(row["Nexaacc_AccountId"]?.ToString() ?? "", out g) || g == Guid.Empty || !seen.Add(g)) continue;
                        string code = row["Account_Code"] == DBNull.Value ? "" : row["Account_Code"].ToString();
                        string an = row["Account_Name"] == DBNull.Value ? "" : row["Account_Name"].ToString();
                        list.Add(Tuple.Create(g, (row["Paid_How"]?.ToString() ?? "") + (code.Length > 0 ? " · " + code + " " + an : "")));
                    }
            }
            catch { }
            return list;
        }

        private List<object> LoadVendorOptions()
        {
            var list = new List<object>();
            try
            {
                var dt = new Take_Time_BangPhra.code().DatabaseQuerySafe(Conn,
                    "SELECT ID, Name FROM Vendor WHERE (Status = 'True' OR Status IS NULL) ORDER BY Name", null);
                if (dt != null)
                    foreach (DataRow row in dt.Rows)
                    {
                        int id;
                        if (int.TryParse(row["ID"]?.ToString(), out id))
                            list.Add(new Dictionary<string, object> { { "id", id }, { "n", row["Name"]?.ToString() ?? "" } });
                    }
            }
            catch { }
            return list;
        }

        /// <summary>หา Nexaacc_AccountCode จาก TakeTime_Code ใน Accounting_Account_Mapping ("" = ยังไม่ map/ปิด)</summary>
        private string LookupMappedAccountCode(string takeTimeCode)
        {
            try
            {
                var dt = new Take_Time_BangPhra.code().DatabaseQuerySafe(Conn,
                    @"SELECT TOP 1 Nexaacc_AccountCode FROM Accounting_Account_Mapping
                      WHERE TakeTime_Code = @code AND Is_Active = 1
                        AND Nexaacc_AccountCode IS NOT NULL AND LTRIM(RTRIM(Nexaacc_AccountCode)) <> ''",
                    new Dictionary<string, object> { { "@code", takeTimeCode } });
                if (dt != null && dt.Rows.Count > 0) return dt.Rows[0]["Nexaacc_AccountCode"]?.ToString() ?? "";
            }
            catch { }
            return "";
        }

        // ══════════════════════════════════════════════════════════════
        // ผู้จัดทำ (พนักงานที่ล็อกอิน) — ชื่อ + ลายเซ็น ส่งใน PUT/POST เอกสาร
        // ══════════════════════════════════════════════════════════════

        /// <summary>ผู้จัดทำ = พนักงาน TakeTime (Session["UserID"] → Admin) — NextAcc ให้ priority เหนือ CreatedBy
        /// (UpdateDocumentRequest.PreparerName/Signature DocumentDtos.cs:338-343). best-effort</summary>
        private void ResolvePreparer(out string name, out string signature)
        {
            name = null;
            signature = null;
            try
            {
                short adminId;
                if (short.TryParse(Session["UserID"]?.ToString() ?? "0", out adminId) && adminId > 0)
                {
                    var dt = new Take_Time_BangPhra.code().DatabaseQuerySafe(Conn, "SELECT FirstName, LastName FROM Admin WHERE ID = @id",
                        new Dictionary<string, object> { { "@id", adminId } });
                    if (dt != null && dt.Rows.Count > 0)
                    {
                        string first = dt.Rows[0]["FirstName"] == DBNull.Value ? "" : dt.Rows[0]["FirstName"].ToString();
                        string last = dt.Rows[0]["LastName"] == DBNull.Value ? "" : dt.Rows[0]["LastName"].ToString();
                        name = (first + " " + last).Trim();
                    }
                    string dataUri = new SignatureService().GetSignatureUrl(adminId);
                    if (!string.IsNullOrEmpty(dataUri) && dataUri.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && dataUri.Length <= 256 * 1024)
                        signature = dataUri;   // cap เท่ากับฝั่ง sync
                }
                if (string.IsNullOrEmpty(name)) name = UserName;
            }
            catch { }
        }

        // ══════════════════════════════════════════════════════════════
        // helpers
        // ══════════════════════════════════════════════════════════════

        private static bool IsTerminal(string status)
        {
            if (string.IsNullOrEmpty(status)) return false;
            return status.Equals("Completed", StringComparison.OrdinalIgnoreCase)
                || status.Equals("Failed", StringComparison.OrdinalIgnoreCase)
                || status.Equals("Cached", StringComparison.OrdinalIgnoreCase);
        }

        private void Msg(Literal lit, string kind, string text)
        {
            lit.Text = "<div class=\"ocr-msg " + kind + "\">" + Server.HtmlEncode(text) + "</div>";
        }

        private static string JsString(string s) =>
            "'" + (s ?? "").Replace("\\", "\\\\").Replace("'", "\\'").Replace("<", "\\x3c").Replace("\r", "").Replace("\n", " ") + "'";

        /// <summary>ลบไฟล์สแกนชั่วคราว + โฟลเดอร์ย่อย (ocrupload_*) — ละเลยข้อผิดพลาด</summary>
        private static void TryDeleteScanFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;
            try
            {
                if (File.Exists(filePath)) File.Delete(filePath);
                string dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir)
                    && Path.GetFileName(dir).StartsWith("ocrupload_", StringComparison.OrdinalIgnoreCase)
                    && Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }
            }
            catch { /* ไฟล์ชั่วคราว — ละเลยถ้าลบไม่ได้ */ }
        }
    }
}
