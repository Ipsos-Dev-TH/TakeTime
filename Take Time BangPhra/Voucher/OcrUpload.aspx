<%@ Page Title="OCR → ตรวจทาน → ส่ง NextAcc" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="OcrUpload.aspx.cs" Inherits="Take_Time_BangPhra.Voucher.OcrUpload" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .ocr-wrap { max-width: 1400px; margin: 16px auto 90px; padding: 0 12px; font-family: inherit; box-sizing: border-box; }
        .ocr-wrap *, .ocr-wrap *::before, .ocr-wrap *::after { box-sizing: border-box; }
        .ocr-card { background:#fff; border:1px solid #e3e3e3; border-radius:10px; padding:16px 18px; margin-bottom:14px; box-shadow:0 1px 3px rgba(0,0,0,.06); }
        .ocr-card h3 { margin:0 0 10px; font-size:17px; }
        .ocr-row { display:flex; gap:12px; margin-bottom:10px; flex-wrap:wrap; }
        .ocr-field { flex:1 1 220px; min-width:0; }
        .ocr-field label, .rv-f label { display:block; font-size:12px; color:#555; margin-bottom:3px; font-weight:600; }
        .ocr-field input, .ocr-field select { width:100%; padding:7px 9px; border:1px solid #ccc; border-radius:6px; }
        .ocr-btn { background:#2b6cb0; color:#fff; border:none; padding:9px 16px; border-radius:6px; cursor:pointer; font-size:14px; }
        .ocr-btn.green { background:#2f855a; } .ocr-btn.gray { background:#718096; } .ocr-btn.red { background:#c53030; }
        .ocr-btn.ghost { background:#fff; color:#2b6cb0; border:1px solid #2b6cb0; }
        .ocr-btn:disabled { background:#a0aec0; cursor:not-allowed; }
        .ocr-msg { padding:10px 12px; border-radius:6px; margin:8px 0; font-size:13px; line-height:1.5; }
        .ocr-msg.info { background:#ebf8ff; color:#2c5282; } .ocr-msg.warn { background:#fffaf0; color:#9c4221; }
        .ocr-msg.err { background:#fff5f5; color:#c53030; } .ocr-msg.ok { background:#f0fff4; color:#276749; }
        .ocr-hint { font-size:12px; color:#777; }
        .badge { display:inline-block; padding:2px 8px; border-radius:10px; font-size:12px; color:#fff; background:#718096; }
        .tbl { width:100%; border-collapse:collapse; font-size:13px; }
        .tbl th, .tbl td { padding:6px 8px; border-top:1px solid #eee; text-align:left; vertical-align:top; }
        .tbl th { background:#f7fafc; font-size:12px; color:#4a5568; }
        .num { text-align:right !important; font-variant-numeric: tabular-nums; }

        /* ── หน้าตรวจทาน: ภาพซ้าย / ฟอร์มขวา (มือถือเรียงบน-ล่าง) ── */
        .rv-head { display:flex; gap:10px; align-items:center; flex-wrap:wrap; justify-content:space-between; }
        .rv-layout { display:grid; grid-template-columns: minmax(0, 1fr); gap:14px; }
        .rv-layout > * { min-width:0; }
        @media (min-width: 1100px) { .rv-layout { grid-template-columns: minmax(0, 5fr) minmax(0, 7fr); } .rv-doc { position:sticky; top:70px; align-self:start; } }
        .rv-doc { background:#2d3748; border-radius:10px; padding:8px; min-width:0; }
        .rv-doc-bar { display:flex; gap:6px; align-items:center; flex-wrap:wrap; color:#e2e8f0; font-size:12px; margin-bottom:6px; }
        .rv-doc-bar button, .rv-doc-bar a { background:#4a5568; color:#fff; border:none; border-radius:5px; padding:4px 10px; cursor:pointer; font-size:13px; text-decoration:none; }
        .rv-doc-view { height:62vh; min-height:320px; overflow:auto; background:#1a202c; border-radius:6px; text-align:center; }
        @media (min-width: 1100px) { .rv-doc-view { height: calc(100vh - 160px); } }
        .rv-doc-view img { transform-origin: top left; max-width:none; display:block; }
        .rv-doc-view iframe { width:100%; height:100%; border:0; background:#fff; }
        .rv-sec { background:#fff; border:1px solid #e2e8f0; border-radius:10px; padding:12px 14px; margin-bottom:12px; }
        .rv-sec h4 { margin:0 0 10px; font-size:15px; color:#2d3748; }
        .rv-grid { display:grid; grid-template-columns: repeat(auto-fill, minmax(190px, 1fr)); gap:10px 12px; }
        .rv-f { min-width:0; }
        .rv-f.wide { grid-column: 1 / -1; }
        .rv-f input[type=text], .rv-f input[type=date], .rv-f input[type=number], .rv-f select, .rv-f textarea,
        .ln input, .ln select { width:100%; padding:7px 8px; border:1px solid #cbd5e0; border-radius:6px; font-size:14px; background:#fff; }
        .rv-f textarea { min-height:52px; resize:vertical; }
        .rv-f .hint { font-size:11px; color:#718096; margin-top:2px; }
        .lowconf input, .lowconf textarea, input.lowconf { background:#fffbea !important; border-color:#ecc94b !important; }
        .lowconf label::after { content:" ⚠ OCR ไม่มั่นใจ"; color:#b7791f; font-weight:400; }
        .bad { border-color:#e53e3e !important; background:#fff5f5 !important; }
        .okmark { color:#2f855a; font-size:12px; } .badmark { color:#c53030; font-size:12px; }
        .kind-grid { display:grid; grid-template-columns: repeat(auto-fill, minmax(150px, 1fr)); gap:8px; }
        .kind-opt { border:2px solid #e2e8f0; border-radius:8px; padding:8px 10px; cursor:pointer; display:block; }
        .kind-opt input { margin-right:6px; }
        .kind-opt small { display:block; color:#718096; font-size:11px; margin-top:2px; }
        .kind-opt.sel { border-color:#2b6cb0; background:#ebf8ff; }
        .seg { display:flex; flex-wrap:wrap; gap:6px; }
        .seg label { border:1px solid #cbd5e0; border-radius:16px; padding:4px 12px; cursor:pointer; font-size:13px; font-weight:400; color:#2d3748; }
        .seg label.sel { background:#2b6cb0; color:#fff; border-color:#2b6cb0; }
        .seg input { display:none; }

        /* ตารางรายการ — เดสก์ท็อปเป็นตาราง, มือถือเป็นการ์ด */
        .lines { width:100%; border-collapse:collapse; }
        .lines th { font-size:11px; color:#4a5568; background:#f7fafc; padding:6px 4px; text-align:left; white-space:nowrap; }
        .lines td { padding:4px; border-top:1px solid #edf2f7; vertical-align:top; }
        .lines .c-desc { min-width:160px; } .lines .c-n { width:82px; } .lines .c-acc { min-width:150px; } .lines .c-vt { width:86px; }
        .lines .c-amt { width:96px; text-align:right; font-variant-numeric: tabular-nums; padding-top:10px; }
        .lines .c-x { width:34px; }
        .ln-del { background:#fff5f5; color:#c53030; border:1px solid #feb2b2; border-radius:5px; cursor:pointer; padding:5px 8px; }
        .ln .mlabel { display:none; }
        .lines-wrap { overflow-x:auto; }
        /* การ์ดเมื่อพื้นที่ฟอร์มแคบ: มือถือ/แท็บเล็ต และจอ 1100-1499px ที่ฟอร์มอยู่คอลัมน์ขวา (~700px) */
        @media (max-width: 860px), (min-width: 1100px) and (max-width: 1499px) {
            .lines thead { display:none; }
            .lines, .lines tbody, .lines tr, .lines td { display:block; width:100%; }
            .lines tr { border:1px solid #e2e8f0; border-radius:8px; padding:8px; margin-bottom:10px; display:grid; grid-template-columns: 1fr 1fr; gap:6px 8px; }
            .lines td { border:0; padding:0; width:auto !important; }
            .lines td.c-desc, .lines td.c-acc { grid-column: 1 / -1; }
            .ln .mlabel { display:block; font-size:11px; color:#718096; }
            .lines .c-amt { text-align:left; padding-top:0; font-weight:700; }
        }
        .recon td, .recon th { padding:6px 8px; }
        .recon input { width:120px; padding:5px 7px; border:1px solid #cbd5e0; border-radius:5px; text-align:right; }
        @media (max-width: 480px) { .recon input { width:92px; } .recon td, .recon th { padding:5px 4px; font-size:12px; } }
        .recon tr.diff td { color:#c53030; font-weight:700; } .recon tr.same td.st { color:#2f855a; }
        .chk li { margin:2px 0; font-size:13px; } .chk { padding-left:18px; margin:4px 0; }
        .rv-bar { position:fixed; left:0; right:0; bottom:0; background:#fff; border-top:1px solid #e2e8f0; box-shadow:0 -2px 8px rgba(0,0,0,.08);
                  padding:10px 16px; display:flex; gap:12px; align-items:center; justify-content:flex-end; flex-wrap:wrap; z-index:900; }
        .rv-bar .sum { margin-right:auto; font-size:13px; color:#2d3748; }
        .rv-bar .sum b { font-size:16px; }
        .rv-modal { display:none; position:fixed; inset:0; background:rgba(0,0,0,.45); z-index:9998; align-items:center; justify-content:center; padding:12px; }
        .rv-modal .box { background:#fff; border-radius:10px; max-width:640px; width:100%; max-height:90vh; overflow:auto; padding:18px 20px; }
        .src-pick { cursor:pointer; }
        .src-pick:hover td { background:#f7fafc; }
        .src-sel td { background:#ebf8ff !important; }
        .pill { display:inline-block; padding:1px 8px; border-radius:10px; font-size:11px; background:#edf2f7; color:#2d3748; }
        /* ข้อที่ต้องแก้ + ปุ่มแก้ด่วน (ขั้น 5) — อยู่เหนือตารางยอด, เลื่อนมาได้จากแถบล่าง */
        #rvStep5 { scroll-margin-top: 70px; }
        .ocr-fix { background:#ebf8ff; border:1px solid #90cdf4; border-radius:8px; padding:10px 12px; margin:6px 0 10px; }
        .ocr-fix .ocr-btn { width:100%; text-align:left; white-space:normal; margin-top:6px; }
        .ocr-fix .ocr-btn:first-child { margin-top:0; }
        .fixhint { font-size:12px; color:#744210; background:#fffaf0; border-left:3px solid #ed8936; padding:3px 8px; margin:3px 0 6px; font-weight:400; }
        .src-lines summary { cursor:pointer; font-size:13px; font-weight:600; color:#2c5282; margin:6px 0; }
        @media (max-width: 600px) {
            .rv-bar { padding:8px 12px; gap:6px; }
            .rv-bar .sum { flex:1 1 100%; margin-right:0; }
            /* เว้นมุมขวาล่างให้ปุ่มแชทของเว็บไม่ทับปุ่มส่ง */
            .rv-bar #rvSubmitBtn { flex:1 1 auto; margin-right:72px; }
        }
        @keyframes ocrspin { to { transform: rotate(360deg); } }
    </style>

    <div class="ocr-wrap">
        <%-- ═══ 1) อัปโหลด & สแกน ═══ --%>
        <asp:Panel ID="pnlUpload" runat="server" CssClass="ocr-card">
            <h3><i class="fas fa-file-import"></i> สแกนเอกสารด้วย OCR → ตรวจทาน → ส่งสร้างเอกสารบน NextAcc</h3>
            <p class="ocr-hint">
                อัปโหลดใบกำกับภาษี / ใบเสร็จ / <b>ใบลดหนี้-ใบเพิ่มหนี้จากผู้ขาย</b> → NextAcc อ่านข้อมูลให้ → ตรวจและแก้ให้ตรงเอกสาร (ยอดต้องตรงกระดาษก่อนส่ง)
                → กด "ส่ง" จึงสร้างและอนุมัติเอกสารบน NextAcc. ระหว่างตรวจ ออกจากหน้าได้ — ระบบเก็บร่างไว้ให้ทำต่อ
            </p>
            <div class="ocr-row">
                <div class="ocr-field" style="flex:2 1 320px;">
                    <label>ไฟล์เอกสาร (PDF / รูปภาพ ไม่เกิน 10 MB)</label>
                    <asp:FileUpload ID="fuOcr" runat="server" accept=".pdf,image/*" />
                </div>
                <div class="ocr-field">
                    <label>ชนิดเอกสาร (เปลี่ยนได้ในหน้าตรวจทาน)</label>
                    <asp:DropDownList ID="ddlTargetType" runat="server">
                        <asp:ListItem Value="" Text="ให้ระบบเดาจากเอกสาร" />
                        <asp:ListItem Value="PV" Text="ใบสำคัญจ่าย (จ่ายแล้ว)" />
                        <asp:ListItem Value="EXP" Text="ค่าใช้จ่ายตั้งหนี้ (ยังไม่จ่าย)" />
                        <asp:ListItem Value="VCN" Text="ใบลดหนี้จากผู้ขาย" />
                        <asp:ListItem Value="VDN" Text="ใบเพิ่มหนี้จากผู้ขาย" />
                    </asp:DropDownList>
                </div>
            </div>
            <asp:Button ID="btnScan" runat="server" CssClass="ocr-btn" Text="อัปโหลด & สแกน OCR" OnClick="btnScan_Click"
                OnClientClick="if(this.dataset.busy){return false;} this.dataset.busy='1'; this.value='⏳ กำลังสแกน...'; OCR_overlay('กำลังสแกน OCR… อาจใช้เวลาสักครู่ (อย่าปิดหน้านี้)');" UseSubmitBehavior="false" />
            <asp:Literal ID="litDrafts" runat="server" />
        </asp:Panel>

        <asp:UpdatePanel ID="upOcr" runat="server" UpdateMode="Conditional">
        <ContentTemplate>
        <asp:Literal ID="litStatus" runat="server" />
        <asp:Panel ID="pnlScanning" runat="server" Visible="false" CssClass="ocr-card">
            <div style="display:flex; align-items:center; gap:12px;">
                <div style="width:28px;height:28px;border:4px solid #cbd5e0;border-top-color:#2b6cb0;border-radius:50%;animation:ocrspin .8s linear infinite;"></div>
                <span>กำลังประมวลผล OCR… เมื่อเสร็จระบบจะเปิดหน้าตรวจทานให้อัตโนมัติ (อย่าปิดหน้านี้)</span>
            </div>
        </asp:Panel>
        <%-- โพลล์สถานะ OCR แบบ async — เสร็จแล้วพาไปหน้าตรวจทาน (?scan=) --%>
        <asp:Timer ID="tmrPoll" runat="server" Interval="2500" Enabled="false" OnTick="tmrPoll_Tick" />
        </ContentTemplate>
        </asp:UpdatePanel>

        <asp:Literal ID="litResult" runat="server" />

        <%-- ═══ 2) หน้าตรวจทาน ═══ --%>
        <asp:Panel ID="pnlReview" runat="server" Visible="false">
            <div class="ocr-card rv-head">
                <div>
                    <h3 style="margin:0;"><i class="fas fa-clipboard-check"></i> ตรวจทานก่อนส่งไป NextAcc</h3>
                    <asp:Literal ID="litReviewHead" runat="server" />
                </div>
                <div style="display:flex; gap:8px; flex-wrap:wrap;">
                    <a class="ocr-btn ghost" href="OcrUpload.aspx" style="text-decoration:none;">＋ สแกนใบใหม่</a>
                    <asp:Button ID="btnSaveDraft" runat="server" CssClass="ocr-btn gray" Text="💾 บันทึกร่าง (ทำต่อภายหลัง)" OnClick="btnSaveDraft_Click"
                        OnClientClick="return OCR.beforePost('save');" />
                    <asp:Button ID="btnDiscard" runat="server" CssClass="ocr-btn red" Text="ทิ้งร่างนี้" OnClick="btnDiscard_Click"
                        OnClientClick="return confirm('ทิ้งร่างนี้? (ยังไม่มีอะไรถูกสร้างบน NextAcc)');" />
                </div>
            </div>

            <asp:Panel ID="pnlWarnings" runat="server" Visible="false" CssClass="ocr-card" style="border:2px solid #dd6b20;">
                <h3 style="color:#c05621;">⚠ NextAcc มีคำเตือนก่อนอนุมัติ</h3>
                <asp:Literal ID="litWarnings" runat="server" />
                <p class="ocr-hint">เอกสารยังเป็น<b>ร่าง</b>บน NextAcc. อ่านคำเตือนแล้วเลือก: แก้ข้อมูลด้านล่างแล้วกดส่งใหม่ หรือยืนยันอนุมัติทั้งที่มีคำเตือน</p>
                <asp:Button ID="btnSubmitAck" runat="server" CssClass="ocr-btn" Text="ยืนยันแม้มีคำเตือน แล้วอนุมัติ" OnClick="btnSubmitAck_Click"
                    OnClientClick="return OCR.beforePost('ack');" />
            </asp:Panel>

            <div class="rv-layout">
                <div class="rv-doc" id="rvDoc">
                    <div class="rv-doc-bar">
                        <span id="rvDocName" style="margin-right:auto; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; max-width:50%;"></span>
                        <button type="button" onclick="OCR.zoom(-0.2)" title="ย่อ">－</button>
                        <button type="button" onclick="OCR.zoom(0)" title="พอดีกรอบ">พอดี</button>
                        <button type="button" onclick="OCR.zoom(0.2)" title="ขยาย">＋</button>
                        <button type="button" onclick="OCR.rotate()" title="หมุน">⟳</button>
                        <a id="rvDocOpen" href="#" target="_blank" rel="noopener">เปิดแท็บใหม่</a>
                    </div>
                    <div class="rv-doc-view" id="rvDocView"></div>
                </div>
                <div id="rvForm"><div class="ocr-msg info">กำลังโหลดฟอร์ม… (ต้องเปิด JavaScript)</div></div>
            </div>

            <div class="rv-bar" id="rvBar">
                <div class="sum" id="rvBarSum"></div>
                <button type="button" class="ocr-btn green" id="rvSubmitBtn" onclick="OCR.openConfirm()" disabled>ส่งไปสร้างเอกสารบน NextAcc</button>
            </div>

            <div class="rv-modal" id="rvModal">
                <div class="box">
                    <h3 style="margin-top:0;">ยืนยันส่งไป NextAcc</h3>
                    <div id="rvModalBody"></div>
                    <div style="display:flex; gap:8px; justify-content:flex-end; margin-top:14px; flex-wrap:wrap;">
                        <button type="button" class="ocr-btn gray" onclick="OCR.closeConfirm()">กลับไปแก้</button>
                        <asp:Button ID="btnSubmit" runat="server" CssClass="ocr-btn green" Text="ยืนยัน สร้าง & อนุมัติบน NextAcc" OnClick="btnSubmit_Click"
                            OnClientClick="return OCR.beforePost('submit');" />
                    </div>
                </div>
            </div>

            <%-- ปุ่มซ่อน: ค้นใบต้นทาง (JS เรียกหลังเขียน state ลง hfReviewJson) --%>
            <asp:Button ID="btnCnSearch" runat="server" Text="ค้น" OnClick="btnCnSearch_Click" style="display:none;" />
            <%-- ปุ่มซ่อน: เลือกใบต้นทาง → เซิร์ฟเวอร์ดึงรายการ/ผังบัญชี/บัญชีที่จ่ายของใบเดิม (OcrReviewService.LoadSourceDetailAsync) --%>
            <asp:Button ID="btnCnPick" runat="server" Text="เลือก" OnClick="btnCnPick_Click" style="display:none;" />
            <asp:HiddenField ID="hfReviewJson" runat="server" />
            <asp:HiddenField ID="hfMetaJson" runat="server" />
        </asp:Panel>

        <asp:HiddenField ID="hfScanId" runat="server" />
        <asp:HiddenField ID="hfHintKind" runat="server" />
    </div>

    <div id="ocrOverlay" style="display:none; position:fixed; inset:0; background:rgba(0,0,0,.35); z-index:9999; align-items:center; justify-content:center;">
        <div style="background:#fff; padding:22px 30px; border-radius:10px; box-shadow:0 4px 20px rgba(0,0,0,.25); font-size:15px; text-align:center; max-width:90vw;">
            <div style="width:38px; height:38px; margin:0 auto 12px; border:4px solid #cbd5e0; border-top-color:#2b6cb0; border-radius:50%; animation:ocrspin 0.8s linear infinite;"></div>
            <span id="ocrOverlayMsg">กำลังประมวลผล… กรุณารอสักครู่ (อย่าปิดหน้านี้)</span>
        </div>
    </div>

    <script>
    function OCR_overlay(msg) {
        var o = document.getElementById('ocrOverlay');
        if (!o) return;
        document.getElementById('ocrOverlayMsg').innerText = msg || 'กำลังประมวลผล…';
        o.style.display = 'flex';
    }

    /* ── สูตรคำนวณ: สำเนาของ OcrReviewMath.cs (เซิร์ฟเวอร์เป็นตัวตัดสินก่อนส่ง — ที่นี่แสดงผลสดเท่านั้น) ── */
    var OcrMath = (function () {
        var TOL = 0.01;
        function r2(v) { v = +v || 0; var s = v < 0 ? -1 : 1; return s * Math.round((Math.abs(v) + 1e-9) * 100) / 100; }
        function digits(raw) {
            var s = String(raw || ''), out = '', i, c;
            for (i = 0; i < s.length; i++) { c = s.charCodeAt(i); if (c >= 48 && c <= 57) out += s[i]; }
            if (!out) for (i = 0; i < s.length; i++) { c = s.charCodeAt(i); if (c >= 0x0E50 && c <= 0x0E59) out += String.fromCharCode(48 + c - 0x0E50); }
            return out;
        }
        function isValidTaxId(raw) {
            if (!raw || !String(raw).trim()) return false;
            if (/[^0-9 \-\t]/.test(String(raw))) return false;
            var d = digits(raw); if (d.length !== 13) return false;
            var sum = 0; for (var i = 0; i < 12; i++) sum += (+d[i]) * (13 - i);
            return ((11 - sum % 11) % 10) === +d[12];
        }
        function isJuristic(t) { var d = digits(t); return d.length === 13 && d[0] === '0'; }
        function normBranch(raw) {
            if (raw === null || raw === undefined || !String(raw).trim()) return null;
            var t = String(raw).trim(), low = t.toLowerCase();
            if (low.indexOf('สำนักงานใหญ่') >= 0 || low.indexOf('สนญ') >= 0 || low === 'hq' || low.indexOf('head office') >= 0 || low.indexOf('headoffice') >= 0) return '00000';
            var d = digits(t); if (!d) return t; if (d.length <= 5) return ('00000' + d).slice(-5); return t;
        }
        function isValidBranch(n) { return !!n && n.length === 5 && digits(n) === n; }
        function vatRate(t) { return t === 'EX' ? -1 : (t === '0' ? 0 : 7); }
        function hasOv(l) { return l.VatOverride !== null && l.VatOverride !== undefined && l.VatOverride !== ''; }
        /* บรรทัดเดียว — OcrReviewMath.CalcLine(l, incl) (NextAcc ComputeLineAmounts) */
        function calcLine(l, incl) {
            var gross = r2((+l.Qty || 0) * (+l.UnitPrice || 0)), amount = r2(gross - (+l.Discount || 0)), rate = vatRate(l.VatType), vat = 0, net = amount;
            if (rate > 0) {
                if (hasOv(l)) { vat = r2(+l.VatOverride); net = incl ? r2(amount - vat) : amount; }
                else if (incl) { net = r2(amount * 100 / (100 + rate)); vat = r2(amount - net); }
                else vat = r2(net * rate / 100);
            }
            var wht = (+l.WhtRate || 0) > 0 ? r2(net * (+l.WhtRate) / 100) : 0;
            return { gross: gross, amount: amount, net: net, vat: vat, wht: wht, rate: rate };
        }
        function largest(g, cs) { var b = -1; g.forEach(function (i) { if (b < 0 || Math.abs(cs[i].net) > Math.abs(cs[b].net)) b = i; }); return b; }
        /* ทั้งใบ — OcrReviewMath.CalcLines (กระทบเศษ VAT/WHT ต่ออัตรา; รวม VAT: VAT = Σยอด − round(Σยอด×100/107)) */
        function calcLines(lines, incl) {
            lines = lines || [];
            var cs = lines.map(function (l) { return calcLine(l, incl); }), groups = {}, order = [];
            lines.forEach(function (l, i) { if (!hasOv(l) && cs[i].rate > 0 && cs[i].net !== 0) { var k = String(cs[i].rate); if (!groups[k]) { groups[k] = []; order.push(k); } groups[k].push(i); } });
            order.forEach(function (k) {
                var g = groups[k], rate = +k, target, sv = 0;
                if (incl) { var amt = 0; g.forEach(function (i) { amt += cs[i].amount; }); amt = r2(amt); target = r2(amt - r2(amt * 100 / (100 + rate))); }
                else { var sn = 0; g.forEach(function (i) { sn += cs[i].net; }); target = r2(r2(sn) * rate / 100); }
                g.forEach(function (i) { sv += cs[i].vat; });
                var diff = r2(target - sv);
                if (diff === 0 || Math.abs(diff) > 1) return;
                var j = largest(g, cs); cs[j].vat = r2(cs[j].vat + diff); if (incl) cs[j].net = r2(cs[j].net - diff);
            });
            var wg = {}, wo = [];
            lines.forEach(function (l, i) { if ((+l.WhtRate || 0) > 0 && cs[i].net !== 0) { var k = String(+l.WhtRate); if (!wg[k]) { wg[k] = []; wo.push(k); } wg[k].push(i); } });
            wo.forEach(function (k) {
                var g = wg[k], sn = 0, sw = 0; g.forEach(function (i) { sn += cs[i].net; sw += cs[i].wht; });
                var diff = r2(r2(r2(sn) * (+k) / 100) - sw);
                if (diff === 0 || Math.abs(diff) > 1) return;
                var j = largest(g, cs); cs[j].wht = r2(cs[j].wht + diff);
            });
            return cs;
        }
        function calcTotals(lines, incl) {
            var t = { sub: 0, vat: 0, vatClaim: 0, vatNoClaim: 0, wht: 0 }, cs = calcLines(lines, !!incl);
            (lines || []).forEach(function (l, i) { var c = cs[i]; t.sub += c.net; t.vat += c.vat; if (l.VatClaimable) t.vatClaim += c.vat; else t.vatNoClaim += c.vat; t.wht += c.wht; });
            t.sub = r2(t.sub); t.vat = r2(t.vat); t.vatClaim = r2(t.vatClaim); t.vatNoClaim = r2(t.vatNoClaim); t.wht = r2(t.wht);
            t.total = r2(t.sub + t.vat); t.net = r2(t.total - t.wht);
            return t;
        }
        function noOv(lines) { return (lines || []).map(function (l) { var c = {}; for (var k in l) c[k] = l[k]; c.VatOverride = null; return c; }); }
        function has(v) { return v !== null && v !== undefined && v !== ''; }
        function score(t, s) { var n = 0; if (has(s.PrintedSubTotal) && Math.abs(t.sub - (+s.PrintedSubTotal)) <= TOL) n++; if (has(s.PrintedVat) && Math.abs(t.vat - (+s.PrintedVat)) <= TOL) n++; if (has(s.PrintedTotal) && Math.abs(t.total - (+s.PrintedTotal)) <= TOL) n++; return n; }
        /* OcrReviewMath.DetectPricesIncludeVat — true/false/null */
        function detectIncl(s) {
            var raw = noOv(s.Lines);
            if (!raw.length || !raw.some(function (l) { return vatRate(l.VatType) > 0; })) return null;
            if (!has(s.PrintedSubTotal) && !has(s.PrintedTotal)) return null;
            if (has(s.PrintedVat) && +s.PrintedVat === 0) return null;
            var se = score(calcTotals(raw, false), s), si = score(calcTotals(raw, true), s);
            if (si > se) return true; if (se > si) return false;
            var sum = 0; raw.forEach(function (l) { sum += calcLine(l, false).amount; }); sum = r2(sum);
            var nt = has(s.PrintedTotal) && Math.abs(sum - (+s.PrintedTotal)) <= 1, ns = has(s.PrintedSubTotal) && Math.abs(sum - (+s.PrintedSubTotal)) <= 1;
            if (nt && !ns) return true; if (ns && !nt) return false;
            return null;
        }
        /* OcrReviewMath.AutoPriceBasis — ผู้ใช้ยังไม่เลือกเอง ⇒ ตามยอดบนกระดาษ (สลับ + ล้าง override + ปรับเศษ VAT) */
        function autoBasis(s) {
            if (!s || !(s.Lines || []).length || s.PriceBasisSource === 'USER') return false;
            var det = detectIncl(s);
            if (det === null || det === !!s.PricesIncludeVat) return false;
            s.PricesIncludeVat = det; s.PriceBasisSource = 'AUTO';
            s.Lines.forEach(function (l) { if (l) l.VatOverride = null; });
            if (suggestFixes(s).some(function (f) { return f.code === 'FIT_VAT'; })) applyFix(s, 'FIT_VAT');
            return true;
        }
        function fitLine(cs) { var b = -1; cs.forEach(function (c, i) { if (c.rate > 0 && (b < 0 || Math.abs(c.net) > Math.abs(cs[b].net))) b = i; }); return b; }
        /* OcrReviewMath.SuggestFixes — ปุ่มแก้ด่วน */
        function suggestFixes(s) {
            var f = [], lines = s.Lines || [];
            if (!lines.length) return f;
            var incl = !!s.PricesIncludeVat, t = calcTotals(lines, incl);
            if (reconcile(t, s).slice(0, 3).every(function (r) { return r.ok; })) return f;
            var missing = (has(s.PrintedSubTotal) ? 0 : 1) + (has(s.PrintedVat) ? 0 : 1) + (has(s.PrintedTotal) ? 0 : 1);
            if (missing === 1) { f.push({ code: 'FILL_MISSING', label: 'เติมช่องที่ว่างจากอีกสองช่องตามเอกสาร', hint: 'ยอดรวม = ก่อน VAT + VAT' }); return f; }
            if (missing > 0) return f;
            if (reconcile(calcTotals(noOv(lines), !incl), s).slice(0, 3).every(function (r) { return r.ok; })) {
                f.push({ code: 'SWITCH_BASIS', label: incl ? 'สลับเป็น "ราคายังไม่รวม VAT"' : 'สลับเป็น "ราคาในรายการรวม VAT แล้ว"',
                         hint: incl ? 'ผลรวมรายการเท่ากับยอดก่อน VAT บนเอกสาร' : 'ผลรวมรายการเท่ากับยอดรวม (รวม VAT) บนเอกสาร — เช่นใบของ Makro' });
                return f;
            }
            var cs = calcLines(lines, incl), amt = 0; cs.forEach(function (c) { amt += c.amount; }); amt = r2(amt);
            var baseOk = incl ? Math.abs(amt - (+s.PrintedTotal)) <= TOL : Math.abs(t.sub - (+s.PrintedSubTotal)) <= TOL;
            var vd = r2((+s.PrintedVat) - t.vat), j = fitLine(cs);
            if (baseOk && vd !== 0 && Math.abs(vd) <= 1 && j >= 0) {
                f.push({ code: 'FIT_VAT', label: 'ใช้ยอดตามเอกสาร (ปรับ VAT ' + money(vd) + ' ที่รายการที่ ' + (j + 1) + ')', hint: 'ต่างแค่เศษสตางค์จากการปัด — ตั้ง VAT ของรายการใหญ่สุดให้ตรงกระดาษ' });
                return f;
            }
            var gap = incl ? r2((+s.PrintedTotal) - amt) : r2((+s.PrintedSubTotal) - t.sub);
            if (gap > TOL) f.push({ code: 'ADD_GAP', label: 'เพิ่มรายการส่วนต่าง ' + money(gap), hint: 'OCR อ่านรายการไม่ครบ — ตรวจกับภาพแล้วแก้รายละเอียด/บัญชีของบรรทัดที่เพิ่ม' });
            return f;
        }
        /* OcrReviewMath.ApplyFix */
        function applyFix(s, code) {
            var lines = s.Lines || [], incl = !!s.PricesIncludeVat;
            if (code === 'FILL_MISSING') {
                if (!has(s.PrintedSubTotal) && has(s.PrintedVat) && has(s.PrintedTotal)) s.PrintedSubTotal = r2(+s.PrintedTotal - (+s.PrintedVat));
                else if (!has(s.PrintedVat) && has(s.PrintedSubTotal) && has(s.PrintedTotal)) s.PrintedVat = r2(+s.PrintedTotal - (+s.PrintedSubTotal));
                else if (!has(s.PrintedTotal) && has(s.PrintedSubTotal) && has(s.PrintedVat)) s.PrintedTotal = r2(+s.PrintedSubTotal + (+s.PrintedVat));
                else return false;
                return true;
            }
            if (code === 'SWITCH_BASIS') { s.PricesIncludeVat = !incl; s.PriceBasisSource = 'USER'; lines.forEach(function (l) { l.VatOverride = null; }); return true; }
            if (code === 'FIT_VAT') {
                if (!has(s.PrintedVat)) return false;
                var cs = calcLines(lines, incl), j = fitLine(cs); if (j < 0) return false;
                lines[j].VatOverride = r2(cs[j].vat + r2((+s.PrintedVat) - calcTotals(lines, incl).vat));
                return true;
            }
            if (code === 'ADD_GAP') {
                var cs2 = calcLines(lines, incl), a = 0; cs2.forEach(function (c) { a += c.amount; });
                var gap = incl ? (has(s.PrintedTotal) ? r2((+s.PrintedTotal) - r2(a)) : 0) : (has(s.PrintedSubTotal) ? r2((+s.PrintedSubTotal) - calcTotals(lines, false).sub) : 0);
                if (gap <= TOL) return false;
                var b = lines[0] || {};
                lines.push({ Description: 'รายการเพิ่มเติมตามเอกสาร', Qty: 1, UnitPrice: gap, Discount: 0, AccountCode: b.AccountCode || '', VatType: b.VatType || '7', VatClaimable: b.VatClaimable !== false, VatOverride: null, WhtRate: b.WhtRate || 0, IncomeType: '', OcrAccountCode: '' });
                return true;
            }
            return false;
        }
        var REASONS = { VCN: [], VDN: [] };
        function setReasons(r) { if (r) REASONS = r; }
        function findReason(kind, code) { return (REASONS[kind === 'VDN' ? 'VDN' : 'VCN'] || []).filter(function (x) { return x.c === code; })[0] || null; }
        function normDocNo(v) { return String(v || '').toUpperCase().replace(/[^0-9A-Z฀-๿]/g, ''); }
        function item(key, label, computed, printed, required) {
            var has = printed !== null && printed !== undefined && printed !== '';
            var diff = has ? r2(computed - (+printed)) : 0;
            return { key: key, label: label, computed: computed, printed: has ? +printed : null, diff: diff, ok: has ? Math.abs(diff) <= TOL : !required };
        }
        function reconcile(t, s) {
            var a = [item('PrintedSubTotal', 'ยอดก่อน VAT', t.sub, s.PrintedSubTotal, true),
                     item('PrintedVat', 'ภาษีมูลค่าเพิ่ม', t.vat, s.PrintedVat, true),
                     item('PrintedTotal', 'ยอดรวมตามเอกสาร', t.total, s.PrintedTotal, true)];
            if (s.PrintedWht !== null && s.PrintedWht !== undefined && s.PrintedWht !== '') a.push(item('PrintedWht', 'หัก ณ ที่จ่าย', t.wht, s.PrintedWht, false));
            return a;
        }
        function capCheck(net, srcTotal, credited, balance) {
            if (net <= 0) return 'ยอดใบลดหนี้ต้องมากกว่า 0';
            var rem = r2(srcTotal - credited);
            if (net > rem + TOL) return 'ยอดใบลดหนี้ ' + money(net) + ' เกินยอดที่ลดหนี้ได้ของใบต้นทาง ' + money(rem) + ' (ยอดใบ ' + money(srcTotal) + ' − ลดหนี้แล้ว ' + money(credited) + ') — §86/10';
            if (balance > TOL && net > balance + TOL) return 'ยอดใบลดหนี้ ' + money(net) + ' มากกว่ายอดค้างจ่ายของใบต้นทาง ' + money(balance) + ' — ต้องแยกใบ';
            return null;
        }
        function isDate(s) { return /^\d{4}-\d{2}-\d{2}$/.test(String(s || '')); }
        function missing8604(s) {
            var m = [];
            if (!String(s.SupplierName || '').trim()) m.push('ชื่อผู้ขาย');
            if (!isValidTaxId(s.SupplierTaxId)) m.push('เลขผู้เสียภาษีผู้ขาย (13 หลัก + mod-11)');
            if (!String(s.SupplierAddress || '').trim()) m.push('ที่อยู่ผู้ขาย');
            if (!isValidBranch(normBranch(s.SupplierBranch))) m.push('รหัสสาขาผู้ขาย');
            if (!String(s.SupplierDocNo || '').trim()) m.push('เลขที่ใบกำกับภาษีจากผู้ขาย');
            if (!isDate(s.TaxInvoiceDate)) m.push('วันที่ใบกำกับภาษีจากผู้ขาย');
            return m;
        }
        function money(v) { return (+v || 0).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }); }
        function validate(s) {
            var v = { errors: [], warnings: [], missing: [] }, isNote = s.DocKind === 'VCN' || s.DocKind === 'VDN';
            var lines = s.Lines || [], incl = !!s.PricesIncludeVat, cs = calcLines(lines, incl);
            v.totals = calcTotals(lines, incl); v.recon = reconcile(v.totals, s); v.calcs = cs;
            if (!String(s.SupplierName || '').trim() && !(+s.VendorId > 0) && !(isNote && s.SourceMode === 'NEXTACC')) v.errors.push('ระบุชื่อผู้ขาย');
            if (String(s.SupplierTaxId || '').trim() && !isValidTaxId(s.SupplierTaxId)) v.errors.push('เลขผู้เสียภาษีผู้ขายไม่ถูกต้อง (13 หลัก + mod-11)');
            var br = normBranch(s.SupplierBranch); if (br !== null && !isValidBranch(br)) v.errors.push('รหัสสาขาต้องเป็นตัวเลข 5 หลัก (00000 = สำนักงานใหญ่)');
            if (!isDate(s.DocDate)) v.errors.push('ระบุวันที่ลงบัญชี');
            if (s.SupplierDocDate && !isDate(s.SupplierDocDate)) v.errors.push('วันที่เอกสารผู้ขายไม่ถูกรูปแบบ');
            if (s.TaxInvoiceDate && !isDate(s.TaxInvoiceDate)) v.errors.push('วันที่ใบกำกับภาษีไม่ถูกรูปแบบ');
            if (!String(s.SupplierDocNo || '').trim()) v.warnings.push('ไม่มีเลขที่เอกสารผู้ขาย — กันซ้ำไม่ได้และเคลมภาษีซื้อไม่ได้');
            if (!lines.length) v.errors.push('ต้องมีอย่างน้อย 1 รายการ');
            lines.forEach(function (l, i) {
                var c = cs[i], n = 'รายการที่ ' + (i + 1);
                if (!String(l.Description || '').trim()) v.errors.push(n + ': ระบุรายละเอียด');
                if (!(+l.Qty > 0)) v.errors.push(n + ': จำนวนต้องมากกว่า 0');
                if (+l.UnitPrice < 0 || +l.Discount < 0) v.errors.push(n + ': ราคา/ส่วนลดติดลบไม่ได้');
                if (!(c.net > 0)) v.errors.push(n + ': ยอดหลังส่วนลดต้องมากกว่า 0');
                if (!String(l.AccountCode || '').trim()) v.errors.push(n + ': เลือกบัญชี');
                if (+l.WhtRate < 0 || +l.WhtRate > 15) v.errors.push(n + ': อัตราหัก ณ ที่จ่าย 0-15%');
                if (c.rate > 0 && hasOv(l)) {
                    var std = incl ? r2(c.amount - r2(c.amount * 100 / 107)) : r2(c.net * 0.07);
                    if (Math.abs(+l.VatOverride - std) > 1) v.errors.push(n + ': VAT ที่แก้ต่างจาก 7% เกิน 1 บาท — แยกบรรทัด VAT 0%/ยกเว้น' + (incl ? '' : ' · ถ้าราคาบนกระดาษรวม VAT ให้สลับ "ราคาในรายการรวม VAT แล้ว"'));
                }
            });
            v.recon.forEach(function (r) {
                if (r.ok) return;
                v.errors.push(r.printed === null ? ('กรอก' + r.label + 'ตามเอกสาร') : (r.label + ': คำนวณ ' + money(r.computed) + ' ≠ เอกสาร ' + money(r.printed) + ' (ต่าง ' + money(r.diff) + ')'));
            });
            v.fixes = suggestFixes(s);
            var claims = lines.some(function (l, i) { return l.VatClaimable && cs[i].vat > 0; });
            if (claims) {
                v.missing = missing8604(s);
                if (!s.FullTaxInvoice) v.errors.push('มีบรรทัดที่เคลมภาษีซื้อ แต่ไม่ได้ติ๊ก "เป็นใบกำกับภาษีเต็มรูป" — ใบเสร็จ/อย่างย่อเคลมไม่ได้ ให้เลือก "ไม่เคลม"');
                else if (v.missing.length) v.warnings.push('§86/4 ยังไม่ครบ — NextAcc จะพักภาษีซื้อที่ 11640');
            }
            if (s.DocKind === 'PV' && !s.PaidHowAccountId) v.warnings.push('ยังไม่เลือกแหล่งจ่ายเงิน — NextAcc จะเลือกบัญชีเงินสด/ธนาคารเอง');
            if (isNote) {
                var rs = findReason(s.DocKind, s.NoteReasonCode);
                if (!rs && !(+s.NoteReason >= 1 && +s.NoteReason <= 4)) v.errors.push('เลือกเหตุผล (§86/9-10)');
                if (rs && rs.txt && !String(s.NoteReasonText || '').trim()) v.errors.push('เหตุผล "อื่น ๆ" — พิมพ์รายละเอียดเหตุผล (§86/9-10)');
                var phys = rs ? !!rs.phys : +s.NoteReason === 1;
                if (s.DocKind === 'VCN' && String(s.ReturnRef || '').trim() && !phys)
                    v.errors.push('ผูกการคืนสินค้าได้เฉพาะเหตุผล "คืนสินค้า" / "ชำรุด-ผิดสเปก (ส่งคืน)"' + (rs && rs.c === 'SHORT' ? ' — ส่งไม่ครบไม่มีของส่งกลับ เลือก "ไม่ใช่การคืนสินค้า"' : ''));
                if (has(s.OrigAmount) && has(s.CorrectAmount) && has(s.DiffAmount) && Math.abs(r2(+s.OrigAmount - (+s.CorrectAmount)) - (+s.DiffAmount)) > TOL)
                    v.warnings.push('บนกระดาษ: มูลค่าเดิม − มูลค่าที่ถูกต้อง ≠ ผลต่าง — ตรวจตัวเลขที่ OCR อ่านกับภาพ');
                if (has(s.DiffAmount) && Math.abs(+s.DiffAmount - v.totals.total) > TOL && Math.abs(+s.DiffAmount - v.totals.sub) > TOL)
                    v.warnings.push('ผลต่างบนกระดาษ ' + money(+s.DiffAmount) + ' ≠ ยอดของใบนี้ (รวม ' + money(v.totals.total) + ' / ก่อน VAT ' + money(v.totals.sub) + ')');
                if (s.SourceMode === 'NEXTACC') {
                    if (!s.SourceDocId) v.errors.push('เลือกเอกสารต้นทางใน NextAcc');
                    else {
                        if (s.DocKind === 'VCN') { var cap = capCheck(v.totals.net, +s.SourceTotal || 0, +s.SourceCredited || 0, +s.SourceBalanceDue || 0); if (cap) v.errors.push(cap); }
                        var a = normDocNo(s.OrigTaxInvoiceNo), b = normDocNo(s.SourceSupplierDocNo);
                        if (a && b && a !== b) v.warnings.push('ใบลด/เพิ่มหนี้อ้างใบกำกับเดิมเลข ' + s.OrigTaxInvoiceNo + ' แต่ใบต้นทางที่เลือกบันทึกเลขใบผู้ขาย ' + s.SourceSupplierDocNo + ' — ตรวจว่าเลือกใบถูก');
                        if (has(s.OrigAmount) && (+s.SourceTotal || 0) > 0 && Math.abs(+s.OrigAmount - (+s.SourceTotal)) > 1 && Math.abs(+s.OrigAmount - (+s.SourceSubTotal || 0)) > 1)
                            v.warnings.push('มูลค่าตามใบกำกับเดิมบนกระดาษ ' + money(+s.OrigAmount) + ' ≠ ยอดใบต้นทางใน NextAcc ' + money(+s.SourceTotal) + ' — ตรวจว่าเลือกใบถูก');
                    }
                } else if (s.SourceMode === 'OUTSIDE') {
                    if (!s.OutsideCapConfirmed) v.errors.push('ติ๊กยืนยันว่ายอดไม่เกินยอดใบเดิม (ใบเดิมอยู่นอกระบบ)');
                    if (!String(s.OrigTaxInvoiceNo || '').trim()) v.errors.push('ใบเดิมอยู่นอกระบบ — กรอก "เลขที่ใบกำกับภาษีเดิม" (§86/9-10)');
                }
                else if (s.SourceMode === 'PRT') {
                    if (s.DocKind !== 'VCN') v.errors.push('อ้างการคืนสินค้า (PRT-) ได้เฉพาะใบลดหนี้');
                    if (!/^PRT-/i.test(String(s.ReturnRef || '').trim())) v.errors.push('ระบุเลขอ้างอิงการคืนสินค้า (PRT-)');
                }
                if (s.Settlement === 'BANK' && !s.RefundAccountId) v.errors.push('เลือกบัญชีที่รับเงินคืน/จ่ายเงิน');
                if (s.SourceMode !== 'PRT' && String(s.ReturnRef || '').trim() && s.DocKind !== 'VCN') v.errors.push('ผูกการคืนสินค้าได้เฉพาะใบลดหนี้');
            }
            return v;
        }
        return { r2: r2, digits: digits, isValidTaxId: isValidTaxId, isJuristic: isJuristic, normBranch: normBranch, isValidBranch: isValidBranch,
                 vatRate: vatRate, calcLine: calcLine, calcLines: calcLines, calcTotals: calcTotals, reconcile: reconcile, capCheck: capCheck, missing8604: missing8604,
                 detectIncl: detectIncl, autoBasis: autoBasis, suggestFixes: suggestFixes, applyFix: applyFix, setReasons: setReasons, findReason: findReason,
                 validate: validate, money: money };
    })();

    var OCR = (function () {
        var S = null, M = null, scale = 1, rot = 0;
        var hfState = '<%= hfReviewJson.ClientID %>', hfMeta = '<%= hfMetaJson.ClientID %>';
        var btnSearchId = '<%= btnCnSearch.ClientID %>', btnPickId = '<%= btnCnPick.ClientID %>';
        function $(id) { return document.getElementById(id); }
        function esc(s) { return String(s === null || s === undefined ? '' : s).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); }
        function money(v) { return OcrMath.money(v); }
        function numOrNull(v) { if (v === null || v === undefined || String(v).trim() === '') return null; var n = parseFloat(String(v).replace(/,/g, '')); return isNaN(n) ? null : n; }
        var LOWMAP = { SellerName: 'SupplierName', SellerTaxId: 'SupplierTaxId', SellerBranchCode: 'SupplierBranch', SellerAddress: 'SupplierAddress',
                       DocumentNumber: 'SupplierDocNo', DocumentDate: 'SupplierDocDate', SubTotal: 'PrintedSubTotal', VatAmount: 'PrintedVat', TotalAmount: 'PrintedTotal' };
        function low(f) { return (S.LowConfidence || []).some(function (k) { return LOWMAP[k] === f || k === f; }); }
        var KINDS = [
            { v: 'PV', t: 'ใบสำคัญจ่าย', d: 'จ่ายเงินแล้ว — Cr เงินสด/ธนาคาร' },
            { v: 'EXP', t: 'ค่าใช้จ่ายตั้งหนี้', d: 'ยังไม่จ่าย — Cr เจ้าหนี้' },
            { v: 'VCN', t: 'ใบลดหนี้จากผู้ขาย', d: 'ลดเจ้าหนี้/รับเงินคืน · ลดภาษีซื้อ' },
            { v: 'VDN', t: 'ใบเพิ่มหนี้จากผู้ขาย', d: 'เพิ่มเจ้าหนี้ · เพิ่มภาษีซื้อ' }];
        /* เหตุผลใบลด/เพิ่มหนี้มาจากเซิร์ฟเวอร์ (OcrNoteReasons — M.reasons) ไม่มีสำเนาในหน้า */
        function reasonList() { return ((M.reasons || {})[S.DocKind === 'VDN' ? 'VDN' : 'VCN']) || []; }
        function isNote() { return S.DocKind === 'VCN' || S.DocKind === 'VDN'; }

        function field(f, label, type, opts) {
            opts = opts || {};
            var v = S[f]; if (v === null || v === undefined) v = '';
            var cls = 'rv-f' + (opts.wide ? ' wide' : '') + (low(f) ? ' lowconf' : '');
            var input = type === 'textarea'
                ? '<textarea data-f="' + f + '" id="f_' + f + '">' + esc(v) + '</textarea>'
                : '<input type="' + (type || 'text') + '" data-f="' + f + '" id="f_' + f + '" value="' + esc(v) + '"' + (opts.ph ? ' placeholder="' + esc(opts.ph) + '"' : '') + (opts.num ? ' inputmode="decimal" data-num="1"' : '') + ' />';
            return '<div class="' + cls + '"><label for="f_' + f + '">' + esc(label) + '</label>' + input + (opts.hint ? '<div class="hint" id="h_' + f + '">' + opts.hint + '</div>' : '') + '</div>';
        }
        function moneySelect(f, label, emptyText) {
            var h = '<div class="rv-f"><label>' + esc(label) + '</label><select data-f="' + f + '"><option value="">' + esc(emptyText) + '</option>';
            (M.money || []).forEach(function (a) { h += '<option value="' + esc(a.id) + '"' + (S[f] === a.id ? ' selected' : '') + '>' + esc((a.c ? a.c + ' ' : '') + a.n) + '</option>'; });
            return h + '</select></div>';
        }
        function accountOptions(sel, ocrCode, srcCode) {
            var h = '<option value="">— เลือกบัญชี —</option>', groups = {}, order = [];
            (M.accounts || []).forEach(function (a) { if (!groups[a.g]) { groups[a.g] = []; order.push(a.g); } groups[a.g].push(a); });
            var found = false, srcFound = false;
            order.forEach(function (g) {
                h += '<optgroup label="' + esc(g) + '">';
                groups[g].forEach(function (a) {
                    var s = a.c === sel; if (s) found = true; if (a.c === srcCode) srcFound = true;
                    h += '<option value="' + esc(a.c) + '"' + (s ? ' selected' : '') + '>' + esc(a.c + ' ' + a.n + (a.c === srcCode ? ' ★ใบเดิม' : '') + (a.c === ocrCode ? ' ★OCR' : '')) + '</option>';
                });
                h += '</optgroup>';
            });
            // บัญชีของใบต้นทางที่ไม่อยู่ในกลุ่มบัญชีฝั่งเดบิต (เช่น GRNI/บัญชีอื่น) — ยังเลือกได้
            if (srcCode && !srcFound && srcCode !== sel) h += '<option value="' + esc(srcCode) + '">' + esc(srcCode + ' ★ใบเดิม') + '</option>';
            if (sel && !found) h += '<option value="' + esc(sel) + '" selected>' + esc(sel) + (sel === srcCode ? ' ★ใบเดิม' : ' (ไม่พบในผังที่ Sync — ตรวจ)') + '</option>';
            return h;
        }
        function lineAmt(c, incl) {
            return money(c.amount) + (incl && c.rate > 0 ? '<div class="ocr-hint" style="font-weight:400;">ก่อน VAT ' + money(c.net) + ' · VAT ' + money(c.vat) + '</div>' : '');
        }
        function srcBadge(l) {
            var same = String(l.AccountCode || '') === String(l.SourceAccountCode || '');
            return (same ? '<span class="okmark">✓ ' : '<span class="badmark">≠ ') + 'ใบเดิม: ' + esc(l.SourceAccountCode) + '</span>' + (l.SourceMatch ? ' <small>(' + esc(l.SourceMatch) + ')</small>' : '');
        }

        function render() {
            var o = M.ocr || {}, h = '';
            // ── คำเตือนจากเซิร์ฟเวอร์ ──
            (M.serverErrors || []).forEach(function (e) { h += '<div class="ocr-msg err">' + esc(e) + '</div>'; });
            (M.serverNotes || []).forEach(function (e) { h += '<div class="ocr-msg info">' + esc(e) + '</div>'; });
            if (M.duplicate) h += '<div class="ocr-msg err">' + esc(M.duplicate.text) + (M.duplicate.pdf ? ' <a href="' + esc(M.duplicate.pdf) + '" target="_blank">เปิด PDF</a>' : '') + '</div>';
            // ── ข้อมูล OCR ──
            var meta = '';
            if (o.quality) meta += '<span class="badge" style="background:' + esc(o.quality.c || '#888') + '">คุณภาพ ' + esc(o.quality.l) + ' (' + esc(o.quality.s) + ')</span> ';
            if (o.target) meta += '<span class="pill">OCR แนะนำ: ' + esc(o.target) + (o.role ? ' · เราเป็น' + (o.role === 'Buyer' ? 'ผู้ซื้อ' : 'ผู้ขาย') : '') + '</span> ';
            if (o.engine) meta += '<span class="pill">' + esc(o.engine) + '</span> ';
            if (o.handwriting) meta += '<div class="ocr-msg warn">✋ พบลายมือเขียน — ตรวจยอดเงินกับภาพให้ละเอียด</div>';
            if (o.asset) meta += '<div class="ocr-msg warn">มีรายการที่อาจเป็นสินทรัพย์ถาวร — ควรเลือกบัญชีสินทรัพย์ (12x)</div>';
            if (o.duplicate) meta += '<div class="ocr-msg warn">NextAcc ตรวจว่าเอกสารนี้อาจซ้ำกับที่เคยสแกน</div>';
            (o.compliance || []).forEach(function (c) { meta += '<div class="ocr-msg ' + (c.s === 'error' ? 'err' : 'warn') + '">สรรพากร: ' + esc(c.m) + '</div>'; });
            if (o.role === 'Seller') meta += '<div class="ocr-msg err">OCR อ่านว่าเราเป็น<b>ผู้ขาย</b>บนเอกสารนี้ — หน้านี้รองรับเอกสารฝั่งซื้อเท่านั้น ตรวจว่าสแกนถูกใบ</div>';
            if (!M.chartSynced) meta += '<div class="ocr-msg warn">ยังไม่ได้ Sync ผังบัญชีจาก NextAcc — รายการบัญชีใช้ mapping เดิม. แนะนำ Admin → Accounting Integration → "Sync บัญชี"</div>';
            if (meta) h += '<div class="rv-sec">' + meta + '</div>';

            // ── 1) ชนิดเอกสาร ──
            h += '<div class="rv-sec"><h4>1. ชนิดเอกสารที่จะสร้าง</h4><div class="kind-grid">';
            KINDS.forEach(function (k) { h += '<label class="kind-opt' + (S.DocKind === k.v ? ' sel' : '') + '"><input type="radio" name="rvKind" value="' + k.v + '" data-act="kind"' + (S.DocKind === k.v ? ' checked' : '') + ' /><b>' + k.t + '</b><small>' + k.d + '</small></label>'; });
            h += '</div><div class="hint" style="margin-top:6px;">ใบรับรองแทนใบเสร็จ (ผู้รับเงินออกใบเสร็จไม่ได้) ทำที่หน้า <a href="../Account/PaymentVoucher.aspx">ใบสำคัญจ่าย</a> — ไม่มีกระดาษให้สแกน</div></div>';

            // ── 2) ผู้ขาย ──
            h += '<div class="rv-sec"><h4>2. ผู้ขาย</h4><div class="rv-grid">';
            h += field('SupplierName', 'ชื่อผู้ขาย', 'text', { wide: true });
            h += field('SupplierTaxId', 'เลขประจำตัวผู้เสียภาษี (13 หลัก)', 'text', { ph: '0105xxxxxxxxx', hint: '<span id="taxChk"></span>' });
            h += field('SupplierBranch', 'สาขา (00000 = สำนักงานใหญ่)', 'text', { ph: '00000', hint: '<span id="brChk"></span>' });
            h += field('SupplierAddress', 'ที่อยู่ผู้ขาย (ตามใบกำกับ)', 'textarea', { wide: true });
            if (!(isNote() && S.SourceMode === 'NEXTACC') && (M.vendors || []).length) {
                h += '<div class="rv-f wide"><label>หรือผูกผู้ขายในระบบ TakeTime</label><select data-f="VendorId" data-int="1"><option value="0">— ใช้ผู้ขายจาก OCR —</option>';
                M.vendors.forEach(function (x) { h += '<option value="' + x.id + '"' + (+S.VendorId === +x.id ? ' selected' : '') + '>' + esc(x.n) + '</option>'; });
                h += '</select><div class="hint">เลือกแล้วระบบผูกผู้ติดต่อของผู้ขายนี้ใน NextAcc แทนผู้ติดต่อที่สร้างจาก OCR</div></div>';
            }
            h += '</div></div>';

            // ── 3) เอกสาร ──
            h += '<div class="rv-sec"><h4>3. ข้อมูลเอกสาร</h4><div class="rv-grid">';
            h += field('SupplierDocNo', isNote() ? 'เลขที่ใบลด/เพิ่มหนี้ของผู้ขาย' : 'เลขที่เอกสาร/ใบกำกับของผู้ขาย', 'text');
            h += field('SupplierDocDate', 'วันที่บนเอกสาร', 'date');
            h += field('TaxInvoiceDate', 'วันที่ใบกำกับภาษี (คุมงวดเคลม)', 'date');
            h += field('DocDate', 'วันที่ลงบัญชี', 'date');
            h += '<div class="rv-f wide"><label><input type="checkbox" data-f="FullTaxInvoice"' + (S.FullTaxInvoice ? ' checked' : '') + ' /> เป็นใบกำกับภาษีเต็มรูป (§86/4) — มีชื่อ/เลขผู้เสียภาษี/ที่อยู่/สาขาของผู้ขายและผู้ซื้อ</label><div class="hint">ใบเสร็จรับเงินธรรมดา / ใบกำกับอย่างย่อ ⇒ <b>เคลมภาษีซื้อไม่ได้</b> (§82/5(3)) ให้ตั้งบรรทัดเป็น "ไม่เคลม"</div></div>';
            if (S.DocKind === 'PV') h += moneySelect('PaidHowAccountId', 'แหล่งจ่ายเงิน (บังคับ Cr บัญชีนี้)', '— ให้ NextAcc เลือกเอง —');
            h += field('Notes', 'หมายเหตุ / เหตุผลทางธุรกิจ (§65 ตรี)', 'textarea', { wide: true });
            h += '</div></div>';

            // ── ใบลด/เพิ่มหนี้: ใบต้นทาง ──
            if (isNote()) h += renderNote();

            // ── 4) รายการ ──
            // รวม/ไม่รวม VAT: ผู้ใช้ยังไม่เลือกเอง ⇒ ระบบเลือกตามยอดบนกระดาษทุกครั้งที่คำนวณใหม่ (แก้ยอดตามเอกสาร/รายการแล้วสลับให้เอง)
            OcrMath.autoBasis(S);
            var incl = !!S.PricesIncludeVat, cs = OcrMath.calcLines(S.Lines, incl), hasSrcAcc = (S.Lines || []).some(function (l) { return !!l.SourceAccountCode; });
            h += '<div class="rv-sec"><h4>4. รายการ</h4>';
            // ราคาในรายการรวม VAT หรือยัง — ระบบตรวจจากยอดบนกระดาษ (OcrReviewMath.DetectPricesIncludeVat) ผู้ใช้สลับได้
            var det = OcrMath.detectIncl(S);
            h += '<div class="rv-f" style="margin-bottom:8px;"><label>ราคาในรายการ (ตามกระดาษ)</label><div class="seg">' +
                 '<label class="' + (!incl ? 'sel' : '') + '"><input type="radio" name="rvBasis" value="0" data-act="basis"' + (!incl ? ' checked' : '') + ' />ยังไม่รวม VAT</label>' +
                 '<label class="' + (incl ? 'sel' : '') + '"><input type="radio" name="rvBasis" value="1" data-act="basis"' + (incl ? ' checked' : '') + ' />รวม VAT แล้ว</label></div>' +
                 '<div class="hint">' + (S.PriceBasisSource === 'USER' ? 'ผู้ใช้เลือกเอง' : (S.PriceBasisSource === 'OCR' ? 'OCR อ่านว่าราคารวม VAT' : (S.PriceBasisSource === 'AUTO' ? 'ระบบตรวจจากยอดบนกระดาษ' : ''))) +
                 (det !== null && det !== incl ? ' <span class="badmark">· ยอดบนกระดาษบอกว่าควรเป็น "' + (det ? 'รวม VAT แล้ว' : 'ยังไม่รวม VAT') + '"</span>' : (det === incl ? ' <span class="okmark">✓ ตรงกับยอดบนกระดาษ</span>' : '')) +
                 (incl ? ' · VAT ทั้งใบ = ยอดรวม − ยอดรวม×100/107 (ปัดทั้งใบ ไม่ปัดรายบรรทัด)' : '') + '</div></div>';
            h += '<div class="lines-wrap"><table class="lines"><thead><tr>' +
                 '<th>#</th><th class="c-desc">รายละเอียด</th><th class="c-n">จำนวน</th><th class="c-n">ราคา/หน่วย' + (incl ? ' (รวม VAT)' : '') + '</th><th class="c-n">ส่วนลด ฿</th>' +
                 '<th class="c-amt">ยอด' + (incl ? ' (รวม VAT)' : '') + '</th><th class="c-acc">บัญชี</th><th class="c-vt">VAT</th><th>เคลม</th><th class="c-n">VAT ฿</th><th class="c-n">WHT %</th><th class="c-x"></th></tr></thead><tbody>';
            (S.Lines || []).forEach(function (l, i) {
                var c = cs[i];
                h += '<tr class="ln" data-i="' + i + '">' +
                    '<td class="c-x" style="padding-top:10px;">' + (i + 1) + '</td>' +
                    '<td class="c-desc"><span class="mlabel">รายละเอียด</span><input type="text" data-l="' + i + '" data-k="Description" value="' + esc(l.Description) + '" /></td>' +
                    '<td class="c-n"><span class="mlabel">จำนวน</span><input type="text" inputmode="decimal" data-l="' + i + '" data-k="Qty" value="' + esc(l.Qty) + '" /></td>' +
                    '<td class="c-n"><span class="mlabel">ราคา/หน่วย' + (incl ? ' (รวม VAT)' : '') + '</span><input type="text" inputmode="decimal" data-l="' + i + '" data-k="UnitPrice" value="' + esc(l.UnitPrice) + '" /></td>' +
                    '<td class="c-n"><span class="mlabel">ส่วนลด ฿</span><input type="text" inputmode="decimal" data-l="' + i + '" data-k="Discount" value="' + esc(l.Discount || '') + '" placeholder="0" /></td>' +
                    '<td class="c-amt" id="la_' + i + '">' + lineAmt(c, incl) + '</td>' +
                    '<td class="c-acc"><span class="mlabel">บัญชี</span><select data-l="' + i + '" data-k="AccountCode">' + accountOptions(l.AccountCode, l.OcrAccountCode, l.SourceAccountCode) + '</select>' +
                    (l.SourceAccountCode ? '<div class="hint" id="ls_' + i + '">' + srcBadge(l) + '</div>' : '') + '</td>' +
                    '<td class="c-vt"><span class="mlabel">VAT</span><select data-l="' + i + '" data-k="VatType"><option value="7"' + (l.VatType === '7' ? ' selected' : '') + '>7%</option><option value="0"' + (l.VatType === '0' ? ' selected' : '') + '>0%</option><option value="EX"' + (l.VatType === 'EX' ? ' selected' : '') + '>ยกเว้น</option></select></td>' +
                    '<td style="padding-top:10px;"><span class="mlabel">เคลมภาษีซื้อ</span><input type="checkbox" data-l="' + i + '" data-k="VatClaimable"' + (l.VatClaimable ? ' checked' : '') + (c.rate > 0 ? '' : ' disabled') + ' /></td>' +
                    '<td class="c-n"><span class="mlabel">VAT ฿</span><input type="text" inputmode="decimal" data-l="' + i + '" data-k="VatOverride" value="' + esc(l.VatOverride === null || l.VatOverride === undefined ? '' : l.VatOverride) + '" placeholder="' + (c.rate > 0 ? money(c.vat) : '-') + '"' + (c.rate > 0 ? '' : ' disabled') + ' /></td>' +
                    '<td class="c-n"><span class="mlabel">WHT %</span><input type="text" inputmode="decimal" data-l="' + i + '" data-k="WhtRate" value="' + esc(l.WhtRate || '') + '" placeholder="0" /></td>' +
                    '<td class="c-x"><button type="button" class="ln-del" data-act="del" data-i="' + i + '" title="ลบรายการ">✕</button></td></tr>';
            });
            h += '</tbody></table></div><div style="margin-top:8px; display:flex; gap:8px; flex-wrap:wrap;"><button type="button" class="ocr-btn ghost" data-act="add">＋ เพิ่มรายการ</button>' +
                 '<button type="button" class="ocr-btn ghost" data-act="fit">ใส่ส่วนต่างเป็นรายการใหม่</button>' +
                 (hasSrcAcc ? '<button type="button" class="ocr-btn ghost" data-act="srcacc">ใช้บัญชีตามใบเดิมทุกบรรทัด</button>' : '') + '</div>' +
                 '<div class="hint" style="margin-top:6px;">VAT ฿ = แก้เศษสตางค์ให้ตรงกระดาษ (ว่าง = คำนวณอัตโนมัติ) · ใบกำกับของสินค้าที่ <b>รับเข้าสต๊อกแล้ว</b> เลือกบัญชี "ล้างรับของ" ไม่ใช่ค่าใช้จ่าย (กันต้นทุนซ้ำ) · WHT คิดจากยอดก่อน VAT</div></div>';

            // ── 5) ตรวจยอด ── (ข้อที่ต้องแก้ + ปุ่มแก้ด่วนอยู่บนตาราง — มือถือไม่ถูกแถบล่างบัง)
            h += '<div class="rv-sec" id="rvStep5"><h4>5. ตรวจยอดกับเอกสาร</h4><div id="rvChecks"></div><div id="rvRecon"></div></div>';
            $('rvForm').innerHTML = h;
            updateDynamic();
        }

        /* PHASE19_39: ใบต้นทางจ่ายครบ ⇒ NextAcc ลงขาเงิน 111 (DocumentService.cs:17899,:17922) — ไม่มีเงินเข้า/ออกจริง ⇒ "ตาม NextAcc" = เครดิตผู้ขาย/ตั้งเจ้าหนี้ */
        function sourcePaid() { return S.SourceMode === 'NEXTACC' && !!S.SourceDocId && (+S.SourceBalanceDue || 0) <= 0.01; }
        function pickedReturn() { var rr = String(S.ReturnRef || '').trim(); return rr && S.SourceMode !== 'PRT' ? (M.pendingReturns || []).filter(function (x) { return x.ref === rr; })[0] || null : null; }
        function effSettlement() {
            var s = S.Settlement || 'AUTO';
            if (!M.cnPrimary) return s;
            if (!sourcePaid()) return 'AUTO';
            if (s !== 'AUTO') return s;
            var r = pickedReturn(); return r && r.refund ? 'BANK' : 'VENDOR';
        }
        function renderNote() {
            if (M.cnPrimary && S.SourceMode !== 'PRT') S.Settlement = effSettlement();   // ค่าที่จะทำจริง (ฝั่งเซิร์ฟเวอร์ใช้กติกาเดียวกัน — VendorCnPolicy.EffectiveSettlement)
            var cn = S.DocKind === 'VCN', h = '<div class="rv-sec"><h4>ใบต้นทาง &amp; ' + (cn ? 'ใบลดหนี้' : 'ใบเพิ่มหนี้') + '</h4>';
            // ── §86/9-10 บนกระดาษ: อ้างใบกำกับเดิม + มูลค่าเดิม/ที่ถูกต้อง/ผลต่าง (อ่านจาก OCR แก้ได้) ──
            var od = has(S.OrigAmount) && has(S.CorrectAmount) && has(S.DiffAmount) ? OcrMath.r2(+S.OrigAmount - (+S.CorrectAmount)) : null;
            h += '<div class="rv-grid" style="margin-bottom:10px;">' +
                 field('OrigTaxInvoiceNo', 'อ้างอิงใบกำกับภาษีเดิม เลขที่', 'text', { hint: 'เลขที่ใบกำกับของผู้ขายที่ใบนี้' + (cn ? 'ลด' : 'เพิ่ม') + 'หนี้ — ใช้ค้นใบต้นทาง' }) +
                 field('OrigTaxInvoiceDate', 'ลงวันที่', 'date') +
                 field('OrigAmount', 'มูลค่าตามใบกำกับเดิม', 'text', { num: true }) +
                 field('CorrectAmount', 'มูลค่าที่ถูกต้อง', 'text', { num: true }) +
                 field('DiffAmount', 'ผลต่าง (' + (cn ? 'ลดหนี้' : 'เพิ่มหนี้') + ')', 'text', { num: true, hint: '<span id="paperChk">' + (od === null ? '' : (Math.abs(od - (+S.DiffAmount)) <= 0.01 ? '<span class="okmark">✓ เดิม − ที่ถูกต้อง = ผลต่าง</span>' : '<span class="badmark">✗ เดิม − ที่ถูกต้อง = ' + money(od) + '</span>')) + '</span>' }) +
                 '</div>';
            if (S.PaperPaymentType) h += '<div class="hint" style="margin:-4px 0 8px;">วิธีชำระบนใบ: <b>' + esc(S.PaperPaymentType) + '</b> — ใช้เลือก "รับเงินคืนเข้า" ด้านล่าง</div>';
            h += '<div class="seg" style="margin-bottom:10px;">';
            [['NEXTACC', 'เลือกเอกสารใน NextAcc'], ['OUTSIDE', 'ใบเดิมอยู่นอกระบบ']].concat(cn && !M.cnPrimary ? [['PRT', 'เป็นการคืนสินค้าที่บันทึกแล้ว (PRT-)']] : []).forEach(function (m) {
                h += '<label class="' + (S.SourceMode === m[0] ? 'sel' : '') + '"><input type="radio" name="rvSrc" value="' + m[0] + '" data-act="src"' + (S.SourceMode === m[0] ? ' checked' : '') + ' />' + m[1] + '</label>';
            });
            h += '</div>';
            if (S.SourceMode === 'NEXTACC') {
                h += '<div class="rv-grid"><div class="rv-f wide"><label>ค้นเอกสารต้นทาง (เลขใบกำกับเดิม / เลขที่เอกสาร NextAcc / ชื่อผู้ขาย)</label><div style="display:flex; gap:6px;">' +
                     '<input type="text" data-f="SearchText" id="f_SearchText" value="' + esc(S.SearchText || '') + '" /><button type="button" class="ocr-btn" data-act="search">ค้นหา</button></div>' +
                     '<div class="hint">ใบลด/เพิ่มหนี้ฝั่งซื้ออ้างได้: ใบแจ้งหนี้ซื้อ · ค่าใช้จ่าย · ใบสำคัญจ่าย (NextAcc ValidConversions) · เลือกแล้วระบบดึงรายการ/บัญชี/การจ่ายของใบเดิมมาให้</div></div></div>';
                (M.cnNotes || []).forEach(function (n) { h += '<div class="ocr-msg info">' + esc(n) + '</div>'; });
                var rs = M.cnResults || [];
                if (rs.length) {
                    h += '<div style="overflow-x:auto;"><table class="tbl"><thead><tr><th>เลขที่</th><th>ชนิด</th><th>วันที่</th><th>ผู้ขาย</th><th class="num">ยอด</th><th class="num">ค้างจ่าย</th><th class="num">ลดหนี้แล้ว</th><th class="num">ลดได้อีก</th><th></th></tr></thead><tbody>';
                    rs.forEach(function (r, i) {
                        h += '<tr class="src-pick' + (S.SourceDocId === r.Id ? ' src-sel' : '') + '"><td>' + esc(r.No) + (r.SupplierDocNo ? '<br/><small>ใบผู้ขาย ' + esc(r.SupplierDocNo) + '</small>' : '') + (r.Reason ? '<br/><small style="color:' + (r.Exact ? '#2f855a;font-weight:700' : '#2b6cb0') + ';">' + (r.Exact ? '✓ ' : '') + esc(r.Reason) + '</small>' : '') + '</td><td>' + esc(r.TypeLabel) + '</td><td>' + esc(r.Date) + '</td><td>' + esc(r.ContactName || '') + '</td>' +
                             '<td class="num">' + money(r.Total) + '</td><td class="num">' + money(r.BalanceDue) + '</td><td class="num">' + money(r.Credited) + '</td><td class="num"><b>' + money(r.Remaining) + '</b></td>' +
                             '<td><button type="button" class="ocr-btn" style="padding:4px 10px;" data-act="pick" data-i="' + i + '">' + (S.SourceDocId === r.Id ? '✓ เลือกแล้ว' : 'เลือก') + '</button></td></tr>';
                    });
                    h += '</tbody></table></div>';
                }
                if (S.SourceDocId) {
                    var rem = OcrMath.r2((+S.SourceTotal || 0) - (+S.SourceCredited || 0));
                    h += '<div class="ocr-msg info"><b>ใบต้นทาง: ' + esc(S.SourceDocNo || S.SourceDocId) + '</b>' + (S.SourceContactName ? ' · ' + esc(S.SourceContactName) : '') + (S.SourceSupplierDocNo ? ' · ใบผู้ขาย ' + esc(S.SourceSupplierDocNo) : '') +
                         '<br/>ยอดใบ ' + money(S.SourceTotal) + ' − ลดหนี้แล้ว ' + money(S.SourceCredited) + ' = <b>ลดได้อีก ' + money(rem) + '</b> · ค้างจ่าย ' + money(S.SourceBalanceDue) +
                         '<br/><span id="capChk"></span>' +
                         '<br/><small>' + ((+S.SourceBalanceDue || 0) > 0.01 ? (cn ? 'ใบต้นทางยังค้างจ่าย ⇒ NextAcc ลง <b>Dr เจ้าหนี้</b> (ลดหนี้ที่ค้าง)' : 'NextAcc ลง Cr เจ้าหนี้') : (cn ? 'ใบต้นทางจ่ายครบแล้ว ⇒ NextAcc ลง <b>Dr เงินสด 111</b> — เลือกด้านล่างว่าเงินคืนเข้าบัญชีไหน หรือเก็บเป็นเครดิตกับผู้ขาย' : 'ใบต้นทางจ่ายครบ ⇒ NextAcc ลง Cr เงินสด 111 — เลือกด้านล่างว่าตั้งเจ้าหนี้รอจ่าย หรือจ่ายทันที')) + '</small></div>';
                    var sl = S.SourceLines || [];
                    if (sl.length) {
                        h += '<details class="src-lines" open><summary>รายการ + ผังบัญชีของใบเดิม (' + sl.length + ')</summary><div style="overflow-x:auto;"><table class="tbl"><thead><tr><th>รายการ</th><th class="num">จำนวน</th><th class="num">ราคา/หน่วย</th><th class="num">ยอดก่อน VAT</th><th>บัญชี</th></tr></thead><tbody>';
                        sl.forEach(function (x) { h += '<tr><td>' + esc((x.ProductCode ? x.ProductCode + ' · ' : '') + (x.Description || '')) + '</td><td class="num">' + esc(x.Qty) + '</td><td class="num">' + money(x.UnitPrice) + '</td><td class="num">' + money(x.Amount) + '</td><td><b>' + esc(x.AccountCode || '-') + '</b> ' + esc(x.AccountName || '') + '</td></tr>'; });
                        h += '</tbody></table></div><div class="hint">บัญชีของแต่ละรายการใบลดหนี้ตั้งตามรายการที่ตรงกันในใบเดิม (รหัสสินค้า → รายละเอียด → ราคา → บัญชีหลักของใบเดิม) — เปลี่ยนได้ที่ช่องบัญชี (★ใบเดิม)</div></details>';
                    } else if (S.SourceDocNo) h += '<button type="button" class="ocr-btn ghost" style="padding:4px 10px;" data-act="reload">ดึงรายการ/บัญชี/การจ่ายของใบเดิม</button>';
                }
            } else if (S.SourceMode === 'OUTSIDE') {
                h += '<div class="ocr-msg warn">ไม่ได้อ้างเอกสารใน NextAcc ⇒ ตรวจเพดานยอด §86/10 อัตโนมัติไม่ได้ และ NextAcc จะลง <b>เจ้าหนี้</b> เป็นคู่บัญชี (ไม่มีใบต้นทางให้ดูสถานะการจ่าย) · NextAcc พิมพ์ "อ้างอิงใบกำกับเดิม" จากเลขที่ด้านบน</div>' +
                     '<label style="font-size:13px;"><input type="checkbox" data-f="OutsideCapConfirmed"' + (S.OutsideCapConfirmed ? ' checked' : '') + ' /> ตรวจแล้ว ยอดลด/เพิ่มหนี้รวมไม่เกินยอดใบเดิม และผู้ขายรายเดียวกับใบเดิม</label>';
            } else {
                h += '<div class="ocr-msg warn">การคืนสินค้าที่บันทึกในหน้า "คืนสินค้าให้ผู้ขาย" โพสต์บัญชีไว้ครบแล้ว (เจ้าหนี้/เงินคืน · สินค้าคงเหลือ · ภาษีซื้อ) ⇒ ระบบจะ <b>เก็บใบลดหนี้นี้เป็นหลักฐาน</b> ไม่สร้างใบลดหนี้ใน NextAcc ซ้ำ (Vendor_Cn_Primary=0)</div>' +
                     '<div class="rv-grid">' + field('ReturnRef', 'เลขอ้างอิงการคืนสินค้า (PRT-…)', 'text', { ph: 'PRT-20261026-101500-123' }) + '</div>';
            }
            if (S.SourceMode === 'PRT') return h + '</div>';

            // ── เหตุผล (OcrNoteReasons — map เป็น CreditNoteReason/DebitNoteReason ของ NextAcc; ข้อความเต็มไปที่หมายเหตุเอกสาร) ──
            var rl = reasonList(), cur = OcrMath.findReason(S.DocKind, S.NoteReasonCode);
            h += '<div class="rv-grid" style="margin-top:10px;"><div class="rv-f"><label>เหตุผล (§86/' + (cn ? '10' : '9') + ')</label><select data-f="NoteReasonCode"><option value="">— เลือก —</option>';
            rl.forEach(function (r) { h += '<option value="' + esc(r.c) + '"' + (S.NoteReasonCode === r.c ? ' selected' : '') + '>' + esc(r.t) + '</option>'; });
            h += '</select>' + (cur && cur.h ? '<div class="hint">' + esc(cur.h) + '</div>' : '') + '</div>' +
                 field('NoteReasonText', cur && cur.txt ? 'รายละเอียดเหตุผล (ต้องกรอก)' : 'รายละเอียดเพิ่มเติม (พิมพ์ในหมายเหตุเอกสาร)', 'text') + '</div>';

            // ── การคืนสินค้าที่เกี่ยวข้อง (PHASE19_39: ใบลดหนี้ = ตัวจริง) — เฉพาะเหตุผลที่มีของส่งกลับ ──
            if (cn && M.cnPrimary && (!cur || cur.phys || S.ReturnRef)) {
                var prs = M.pendingReturns || [], pr = pickedReturn();
                h += '<div class="rv-f wide" style="margin-top:10px;"><label>ใบลดหนี้นี้เป็นของการคืนสินค้า (หน้า "คืนสินค้าให้ผู้ขาย") หรือไม่</label><select data-f="ReturnRef">' +
                     '<option value="">ไม่ใช่การคืนสินค้า — ส่วนลด/ปรับราคา/ส่งไม่ครบ (ไม่แตะสต๊อก)</option>';
                prs.forEach(function (r) { h += '<option value="' + esc(r.ref) + '"' + (pr && pr.ref === r.ref ? ' selected' : '') + '>' + esc(r.ref + ' · ' + (r.supplier || '') + ' · ' + money(r.net) + ' + VAT ' + money(r.vat) + ' · ' + r.date + (r.refund ? ' · ผู้ขายคืนเงินแล้ว' : '')) + '</option>'; });
                if (S.ReturnRef && !pr) h += '<option value="' + esc(S.ReturnRef) + '" selected>' + esc(S.ReturnRef) + ' (ไม่อยู่ในรายการรอใบลดหนี้)</option>';
                h += '</select><div class="hint">' + (prs.length ? 'แสดงเฉพาะการคืนที่วางบิลแล้วและยังรอใบลดหนี้' : 'ไม่มีการคืนสินค้าที่รอใบลดหนี้') + '</div></div>';
                if (pr) {
                    var invNet = 0, pcs = OcrMath.calcLines(S.Lines, !!S.PricesIncludeVat);
                    (S.Lines || []).forEach(function (l, i) { if (M.inventoryCode && String(l.AccountCode || '') === M.inventoryCode) invNet += pcs[i].net; });
                    var dif = OcrMath.r2(OcrMath.r2(invNet) - (+pr.carry || 0));
                    h += '<div class="ocr-msg info"><b>ใบลดหนี้นี้เป็นรายการบัญชี/ภาษีตัวจริงของการคืน ' + esc(pr.ref) + '</b>' +
                         '<br/>ราคาซื้อที่คืน ' + money(pr.net) + ' · VAT ' + money(pr.vat) + ' · ต้นทุนเฉลี่ยที่ตัดสต๊อก ' + money(pr.carry) +
                         '<br/>หลังอนุมัติ: กลับ JE ที่ลงระหว่างรอ (<b>' + esc(pr.ref) + '-REV</b>) + ปรับสินค้าคงเหลือ ' + money(dif) + ' (<b>' + esc(pr.ref) + '-DIF</b> ราคาซื้อ ↔ ต้นทุนเฉลี่ย) — ยอดไม่ซ้อน' +
                         (M.inventoryCode ? '<br/><button type="button" class="ocr-btn ghost" style="padding:4px 10px;margin-top:6px;" data-act="invlines">ตั้งบัญชีทุกบรรทัด = สินค้าคงเหลือ ' + esc(M.inventoryCode) + '</button>' : '<br/><span class="badmark">ยังไม่ผูก mapping INVENTORY</span>') + '</div>';
                    if (M.inventoryCode && invNet <= 0) h += '<div class="ocr-msg warn">ยังไม่มีบรรทัดที่ลงบัญชีสินค้าคงเหลือ (' + esc(M.inventoryCode) + ') — สต๊อกใน GL จะถูกตัดที่ต้นทุนเฉลี่ยผ่าน ' + esc(pr.ref) + '-DIF ส่วนบรรทัดใบลดหนี้ลงบัญชีที่เลือก</div>';
                    if (Math.abs(OcrMath.calcTotals(S.Lines, !!S.PricesIncludeVat).sub - (+pr.net || 0)) > 1) h += '<div class="ocr-msg warn">ยอดก่อน VAT ของใบลดหนี้ ≠ ราคาซื้อที่คืน ' + money(pr.net) + ' — ตรวจว่าผู้ขายลดหนี้เต็มจำนวนหรือไม่ (เจ้าหนี้จะลดตามใบลดหนี้)</div>';
                } else if (M.inventoryCode && (S.Lines || []).some(function (l) { return String(l.AccountCode || '') === M.inventoryCode; }) && (!cur || cur.phys)) {
                    h += '<div class="ocr-msg warn">ใบลดหนี้ไม่ผูกการคืนสินค้า แต่มีบรรทัดลงบัญชีสินค้าคงเหลือ — ถ้าจะส่งของคืนตามมา: บันทึกในหน้า "คืนสินค้าให้ผู้ขาย" เลือก "มีใบลดหนี้ที่บันทึกแล้ว" (ตัดจำนวน + ปรับต้นทุน) · ถ้าเป็นส่วนลด/ปรับราคา แนะนำบัญชีส่งคืน/ส่วนลดรับ (ต้นทุนเฉลี่ยใน TakeTime ไม่ลดตาม)</div>';
                }
            }
            if (cn && cur && cur.c === 'SHORT') h += '<div class="ocr-msg info" style="margin-top:8px;"><b>ผู้ขายส่งไม่ครบ / ไม่มีสินค้า:</b> ไม่มีของส่งกลับ ⇒ ไม่ผูกการคืนสินค้า · ถ้าบันทึกรับเข้าสต๊อก (Product/In) เต็มจำนวนไปแล้ว ให้แก้จำนวนรับเข้าให้ตรงของที่ได้จริง · บรรทัดใบลดหนี้ใช้บัญชีเดียวกับใบเดิม (สินค้าคงเหลือ/GRNI/ค่าใช้จ่าย) เพื่อลดต้นทุนของที่ไม่ได้รับ — ผู้ทำบัญชีเปลี่ยนได้</div>';

            // ── ขาเงิน: เลือกจากแหล่งเงิน (Account_Paid_How) — ค่าเริ่มต้น = บัญชีที่ใบเดิมจ่ายออก (หักเงินกลับ) ──
            h += renderSettlement(cn);
            return h + '</div>';
        }

        function settleValue() {
            var e = M.cnPrimary ? effSettlement() : (S.Settlement || 'AUTO');
            return e === 'BANK' ? 'BANK|' + (S.RefundAccountId || '') : e;
        }
        function renderSettlement(cn) {
            var h = '<div class="rv-f wide" style="margin-top:12px;"><label>' + (cn ? 'รับเงินคืน / หักกลับเข้า' : 'จ่ายเพิ่มจาก / ตั้งหนี้') + '</label>';
            var srcSel = S.SourceMode === 'NEXTACC' && !!S.SourceDocId;
            if (srcSel && !sourcePaid()) {
                return h + '<select disabled><option>' + (cn ? 'ลดเจ้าหนี้ (ใบเดิมยังไม่จ่าย)' : 'ตั้งเจ้าหนี้เพิ่ม (ใบเดิมยังไม่จ่าย)') + '</option></select>' +
                       '<div class="hint">ใบต้นทางยังค้างจ่าย ' + money(S.SourceBalanceDue) + ' ⇒ NextAcc ' + (cn ? 'ลด' : 'เพิ่ม') + 'ยอดเจ้าหนี้ของใบเดิม (DocumentService.cs:17933-17936) — ไม่มีเงินเข้า/ออก</div></div>';
            }
            if (!srcSel) {
                return h + '<select disabled><option>' + (cn ? 'ลดเจ้าหนี้' : 'ตั้งเจ้าหนี้') + '</option></select><div class="hint">' +
                       (S.SourceMode === 'NEXTACC' ? 'เลือกใบต้นทางก่อน — ระบบจะเลือกบัญชีที่ใบเดิมจ่ายให้อัตโนมัติ' : 'ใบเดิมอยู่นอกระบบ ⇒ NextAcc ลงเจ้าหนี้') + '</div></div>';
            }
            var val = settleValue(), list = (M.paidHow && M.paidHow.length) ? M.paidHow : (M.money || []).map(function (a) { return { id: a.id, n: (a.c ? a.c + ' ' : '') + a.n }; });
            var found = false;
            h += '<select data-act="settle" id="f_Settle">';
            h += '<optgroup label="' + (cn ? 'รับเงินคืนเข้าแหล่งเงิน' : 'จ่ายจากแหล่งเงิน') + '">';
            list.forEach(function (a) {
                var v = 'BANK|' + a.id, s = v === val; if (s) found = true;
                h += '<option value="' + esc(v) + '"' + (s ? ' selected' : '') + '>' + esc(a.n + (a.id === S.SourcePaidAccountId ? ' ★ใบเดิมจ่ายจากบัญชีนี้' : '')) + '</option>';
            });
            if (S.SourcePaidAccountId && !list.some(function (a) { return a.id === S.SourcePaidAccountId; })) {
                var v2 = 'BANK|' + S.SourcePaidAccountId; if (v2 === val) found = true;
                h += '<option value="' + esc(v2) + '"' + (v2 === val ? ' selected' : '') + '>' + esc((S.SourcePaidAccountLabel || 'บัญชีที่ใบเดิมจ่าย') + ' ★ใบเดิม') + '</option>';
            }
            if (/^BANK\|/.test(val) && !found) h += '<option value="' + esc(val) + '" selected>' + esc(S.RefundAccountId ? 'บัญชีที่เลือกไว้' : '— เลือกบัญชี —') + '</option>';
            h += '</optgroup><optgroup label="ไม่มีเงินเข้า-ออก">';
            h += '<option value="VENDOR"' + (val === 'VENDOR' ? ' selected' : '') + '>' + esc(cn ? 'เก็บเป็นเครดิตกับผู้ขาย (หักตอนจ่ายครั้งหน้า) — ' + (M.vendorCreditLabel || 'เจ้าหนี้การค้า') : 'ตั้งเจ้าหนี้รอจ่าย') + '</option>';
            if (!M.cnPrimary) h += '<option value="AUTO"' + (val === 'AUTO' ? ' selected' : '') + '>ตาม NextAcc (เงินสด 111)</option>';
            h += '</optgroup></select>';
            h += '<div class="hint">' + (S.SourcePaidAccountId ? 'ใบเดิมจ่ายจาก <b>' + esc(S.SourcePaidAccountLabel || '') + '</b> ⇒ ค่าเริ่มต้นหักเงินกลับเข้าบัญชีเดียวกัน · ' : 'ไม่พบบัญชีที่ใบเดิมจ่าย — เลือกเอง · ') +
                 'NextAcc ลงขาเงินที่เงินสด 111 เสมอ (DocumentService.cs:17899, :17920-17924) ⇒ TakeTime โพสต์ JE ย้ายขาไปบัญชีที่เลือก' + (S.SettlementUserSet ? ' · <i>ผู้ใช้เลือกเอง</i>' : '') + '</div></div>';
            return h;
        }

        /* แก้อย่างไร — คำแนะนำต่อข้อผิดพลาด (ข้อความจาก OcrMath.validate) */
        var HINTS = [
            [/^(ยอดก่อน VAT|ภาษีมูลค่าเพิ่ม|ยอดรวมตามเอกสาร):/, 'ดูว่าราคาบนกระดาษ "รวม VAT แล้ว" หรือยัง (สลับที่ขั้น 4) · เทียบจำนวน/ราคาแต่ละรายการกับภาพ · ต่างแค่เศษ VAT ใช้ปุ่ม "ใช้ยอดตามเอกสาร"'],
            [/^กรอก/, 'พิมพ์ตัวเลขตามที่พิมพ์บนเอกสาร (ตาราง "ตามเอกสาร" ด้านล่าง)'],
            [/เลือกบัญชี$/, 'เลือกบัญชีที่ช่อง "บัญชี" ของรายการ' ],
            [/เพดาน|ลดได้อีก|ลดหนี้ได้ของใบต้นทาง|ยอดค้างจ่ายของใบต้นทาง/, 'ตรวจว่าเลือกใบต้นทางถูก (เทียบ "มูลค่าตามใบกำกับเดิม") · ถ้าราคาในรายการรวม VAT ให้สลับที่ขั้น 4 · ส่วนที่จ่ายแล้วต้องแยกใบ'],
            [/เหตุผล/, 'เลือก/กรอกเหตุผลในส่วน "ใบต้นทาง & ใบลดหนี้"'],
            [/เอกสารต้นทาง/, 'พิมพ์เลขใบกำกับเดิมแล้วกด "ค้นหา" แล้วกด "เลือก"'],
            [/ใบกำกับภาษีเต็มรูป/, 'ติ๊ก "เป็นใบกำกับภาษีเต็มรูป" ที่ขั้น 3 หรือเปลี่ยนบรรทัดเป็น "ไม่เคลม"'],
            [/mod-11|13 หลัก/, 'พิมพ์เลขผู้เสียภาษีให้ตรงเอกสาร (13 หลัก)'],
            [/ผูกการคืนสินค้า/, 'เลือก "ไม่ใช่การคืนสินค้า" หรือเปลี่ยนเหตุผลเป็น "คืนสินค้า"'],
            [/บัญชีที่รับเงินคืน/, 'เลือกบัญชีที่ช่อง "รับเงินคืน / หักกลับเข้า"']
        ];
        function hintFor(m) { for (var i = 0; i < HINTS.length; i++) if (HINTS[i][0].test(m)) return HINTS[i][1]; return ''; }

        function updateDynamic() {
            if (!S) return;
            var v = OcrMath.validate(S), t = v.totals, incl = !!S.PricesIncludeVat;
            (S.Lines || []).forEach(function (l, i) {
                var e = $('la_' + i); if (e) e.innerHTML = lineAmt(v.calcs[i], incl);
                var b = $('ls_' + i); if (b && l.SourceAccountCode) b.innerHTML = srcBadge(l);
            });
            // tax id / branch marks
            var tc = $('taxChk'); if (tc) { var tx = S.SupplierTaxId; tc.innerHTML = !tx ? '<span class="ocr-hint">ว่าง = เอกสารไม่มีเลข (เคลม VAT ไม่ได้)</span>' : (OcrMath.isValidTaxId(tx) ? '<span class="okmark">✓ ถูกต้อง · ' + (OcrMath.isJuristic(tx) ? 'นิติบุคคล (หัก ณ ที่จ่าย ภ.ง.ด.53)' : 'บุคคลธรรมดา (ภ.ง.ด.3)') + '</span>' : '<span class="badmark">✗ ไม่ผ่าน mod-11 / ไม่ครบ 13 หลัก</span>'); var ti = $('f_SupplierTaxId'); if (ti) ti.classList.toggle('bad', !!tx && !OcrMath.isValidTaxId(tx)); }
            var bc = $('brChk'); if (bc) { var nb = OcrMath.normBranch(S.SupplierBranch); bc.innerHTML = nb === null ? '<span class="ocr-hint">ว่าง</span>' : (OcrMath.isValidBranch(nb) ? '<span class="okmark">✓ ' + nb + (nb === '00000' ? ' สำนักงานใหญ่' : '') + '</span>' : '<span class="badmark">✗ ต้องเป็นตัวเลข 5 หลัก</span>'); }
            var cc = $('capChk'); if (cc && S.DocKind === 'VCN') { var cap = OcrMath.capCheck(t.net, +S.SourceTotal || 0, +S.SourceCredited || 0, +S.SourceBalanceDue || 0); cc.innerHTML = cap ? '<span class="badmark">✗ ' + esc(cap) + '</span>' : '<span class="okmark">✓ ใบนี้ ' + money(t.net) + ' ≤ ลดได้อีก — อยู่ในเพดาน §86/10</span>'; }
            var pc = $('paperChk'); if (pc) { pc.innerHTML = has(S.OrigAmount) && has(S.CorrectAmount) && has(S.DiffAmount) ? (Math.abs(OcrMath.r2(+S.OrigAmount - (+S.CorrectAmount)) - (+S.DiffAmount)) <= 0.01 ? '<span class="okmark">✓ เดิม − ที่ถูกต้อง = ผลต่าง</span>' : '<span class="badmark">✗ เดิม − ที่ถูกต้อง = ' + money(OcrMath.r2(+S.OrigAmount - (+S.CorrectAmount))) + '</span>') : ''; }
            // recon
            var r = '<div style="overflow-x:auto;"><table class="tbl recon"><thead><tr><th>ช่อง</th><th class="num">ตามเอกสาร (แก้ได้)</th><th class="num">คำนวณจากรายการ</th><th class="num">ต่าง</th><th></th></tr></thead><tbody>';
            var rows = [['PrintedSubTotal', 'ยอดก่อน VAT', t.sub], ['PrintedVat', 'ภาษีมูลค่าเพิ่ม', t.vat], ['PrintedTotal', 'ยอดรวมตามเอกสาร', t.total], ['PrintedWht', 'หัก ณ ที่จ่าย (ถ้าเอกสารระบุ)', t.wht]];
            rows.forEach(function (row) {
                var it = null; v.recon.forEach(function (x) { if (x.key === row[0]) it = x; });
                var pv = S[row[0]]; var lc = low(row[0]) ? ' lowconf' : '';
                var cls = it ? (it.ok ? 'same' : 'diff') : '';
                r += '<tr class="' + cls + '"><td>' + row[1] + '</td><td class="num"><input type="text" inputmode="decimal" class="' + lc + '" data-f="' + row[0] + '" data-num="1" value="' + esc(pv === null || pv === undefined ? '' : pv) + '" /></td>' +
                     '<td class="num">' + money(row[2]) + '</td><td class="num">' + (it && it.printed !== null ? money(it.diff) : '-') + '</td><td class="st">' + (it ? (it.ok ? '✓' : (it.printed === null ? 'กรอก' : '✗')) : '') + '</td></tr>';
            });
            r += '</tbody></table></div><div class="ocr-hint" style="margin-top:6px;">' + (incl ? 'ราคาในรายการรวม VAT แล้ว · ' : '') + 'ภาษีซื้อเคลมได้ ' + money(t.vatClaim) + ' · ไม่เคลม (รวมเข้าค่าใช้จ่าย) ' + money(t.vatNoClaim) +
                 ' · หัก ณ ที่จ่าย ' + money(t.wht) + (t.wht > 0 ? ' (' + (OcrMath.isJuristic(S.SupplierTaxId) ? 'ภ.ง.ด.53' : 'ภ.ง.ด.3') + ')' : '') + ' · <b>ยอด' + (OcrDocIsNote() ? 'ใบนี้' : 'จ่าย') + 'สุทธิ ' + money(t.net) + '</b></div>';
            var rc = $('rvRecon');
            if (rc) {
                var act = document.activeElement, actF = act && act.getAttribute ? act.getAttribute('data-f') : null, selS = act && act.selectionStart, selE = act && act.selectionEnd;
                rc.innerHTML = r;
                if (actF && /^Printed/.test(actF)) { var ne = rc.querySelector('[data-f="' + actF + '"]'); if (ne) { ne.focus(); try { ne.setSelectionRange(selS, selE); } catch (e) { } } }
            }
            // checks — ข้อที่ต้องแก้ + "แก้อย่างไร" + ปุ่มแก้ด่วน 1 แตะ (OcrMath.suggestFixes ≡ OcrReviewMath.SuggestFixes)
            var c = '', seenHint = {};
            if ((v.fixes || []).length) c += '<div class="ocr-fix">' + v.fixes.map(function (f) { return '<button type="button" class="ocr-btn" data-act="fix" data-code="' + esc(f.code) + '">⚡ ' + esc(f.label) + '</button><div class="hint">' + esc(f.hint || '') + '</div>'; }).join('') + '</div>';
            if (v.errors.length) c += '<div class="ocr-msg err" id="rvErrList"><b>ต้องแก้ก่อนส่ง (' + v.errors.length + ')</b><ul class="chk">' + v.errors.map(function (m) { var hn = hintFor(m); if (hn && seenHint[hn]) hn = ''; else if (hn) seenHint[hn] = 1; return '<li>' + esc(m) + (hn ? '<div class="fixhint">แก้อย่างไร: ' + esc(hn) + '</div>' : '') + '</li>'; }).join('') + '</ul></div>';
            if (v.missing.length) c += '<div class="ocr-msg warn"><b>ใบกำกับ §86/4 ยังขาด:</b><ul class="chk">' + v.missing.map(function (m) { return '<li>✗ ' + esc(m) + '</li>'; }).join('') + '</ul>ขาด = NextAcc ลงภาษีซื้อพักไว้ที่ 11640 จนกว่าจะเติมครบ</div>';
            if (v.warnings.length) c += '<div class="ocr-msg warn"><ul class="chk">' + v.warnings.map(function (m) { return '<li>' + esc(m) + '</li>'; }).join('') + '</ul></div>';
            if (M.nextaccDupWarn) c += '<label class="ocr-msg warn" style="display:block;"><input type="checkbox" data-f="AllowDuplicate"' + (S.AllowDuplicate ? ' checked' : '') + ' /> NextAcc เตือนว่าอาจเป็นใบซ้ำ — ตรวจแล้ว <b>ไม่ใช่ใบซ้ำ</b> ให้สร้างได้</label>';
            if (!v.errors.length) c += '<div class="ocr-msg ok">✓ ยอดตรงเอกสารและข้อมูลครบ — กด "ส่งไปสร้างเอกสารบน NextAcc" ด้านล่าง</div>';
            var ce = $('rvChecks'); if (ce) ce.innerHTML = c;
            // bar — แตะ "ต้องแก้ n ข้อ" เลื่อนไปที่รายการ; เผื่อที่ให้แถบล่างไม่บังเนื้อหา
            $('rvBarSum').innerHTML = esc(kindLabel(S.DocKind)) + ' · รวม <b>' + money(t.total) + '</b>' + (t.wht > 0 ? ' · สุทธิ ' + money(t.net) : '') +
                (v.errors.length ? ' · <a href="#rvStep5" class="badmark" data-act="goerr">ต้องแก้ ' + v.errors.length + ' ข้อ ↑</a>' : ' · <span class="okmark">พร้อมส่ง</span>');
            $('rvSubmitBtn').disabled = v.errors.length > 0 || !!M.locked;
            padForBar();
            return v;
        }
        function OcrDocIsNote() { return S.DocKind === 'VCN' || S.DocKind === 'VDN'; }
        function has(v) { return v !== null && v !== undefined && v !== ''; }
        function padForBar() { var b = $('rvBar'); if (b && b.offsetHeight && document.body && document.body.style) document.body.style.paddingBottom = (b.offsetHeight + 24) + 'px'; }
        function kindLabel(k) { var x = KINDS.filter(function (z) { return z.v === k; })[0]; return x ? x.t : k; }

        function onInput(e) {
            var el = e.target, f = el.getAttribute('data-f'), li = el.getAttribute('data-l');
            if (el.getAttribute('data-act') === 'settle') {
                if (e.type !== 'change') return;
                var sv = el.value || '';
                if (sv.indexOf('BANK|') === 0) { S.Settlement = 'BANK'; S.RefundAccountId = sv.substring(5); }
                else { S.Settlement = sv; }
                S.SettlementUserSet = true;
                render(); return;
            }
            if (f) {
                if (el.type === 'checkbox') S[f] = el.checked;
                else if (el.getAttribute('data-num')) S[f] = numOrNull(el.value);
                else if (el.getAttribute('data-int')) S[f] = parseInt(el.value, 10) || 0;
                else S[f] = el.value;
                if (f === 'FullTaxInvoice' && el.checked && !S.TaxInvoiceDate) { S.TaxInvoiceDate = S.SupplierDocDate || ''; var td = $('f_TaxInvoiceDate'); if (td) td.value = S.TaxInvoiceDate; }
                if (f === 'FullTaxInvoice' && !el.checked) { (S.Lines || []).forEach(function (l) { if (OcrMath.vatRate(l.VatType) > 0) l.VatClaimable = false; }); if (e.type === 'change') { render(); return; } }
                if (f === 'VendorId' || f === 'NoteReason' || f === 'RefundAccountId' || f === 'PaidHowAccountId') { updateDynamic(); return; }
                if (f === 'NoteReasonCode') {
                    var rr = OcrMath.findReason(S.DocKind, S.NoteReasonCode); S.NoteReason = rr ? rr.n : 0;
                    if (rr && !rr.phys && S.ReturnRef && S.SourceMode !== 'PRT') S.ReturnRef = '';   // ส่งไม่ครบ/ส่วนลด ไม่มีของคืน
                    if (e.type === 'change') { render(); return; }
                }
                if (f === 'OrigTaxInvoiceNo' && e.type === 'change' && !String(S.SearchText || '').trim()) { S.SearchText = S.OrigTaxInvoiceNo; var st = $('f_SearchText'); if (st) st.value = S.SearchText; }
                if (f === 'ReturnRef' && el.tagName === 'SELECT') {
                    // การคืนที่ผู้ขายคืนเงินแล้ว ⇒ ขาเงินเริ่มต้น = บัญชีที่ PRT รับเงินคืน
                    var pr = pickedReturn();
                    if (pr && pr.refund) { S.Settlement = 'BANK'; if (pr.accId) S.RefundAccountId = pr.accId; }
                    else if (S.Settlement === 'BANK' && !S.RefundAccountId) S.Settlement = 'AUTO';
                    if (e.type === 'change') { render(); return; }
                }
            } else if (li !== null) {
                var l = S.Lines[+li], k = el.getAttribute('data-k');
                if (!l) return;
                if (k === 'VatClaimable') l.VatClaimable = el.checked;
                else if (k === 'Description' || k === 'AccountCode') l[k] = el.value;
                else if (k === 'VatType') { l.VatType = el.value; if (el.value !== '7') { l.VatOverride = null; l.VatClaimable = true; } else l.VatClaimable = !!S.FullTaxInvoice; if (e.type === 'change') { render(); return; } }
                else if (k === 'VatOverride') l.VatOverride = numOrNull(el.value);
                else l[k] = numOrNull(el.value) || 0;
            }
            updateDynamic();
        }
        function onClick(e) {
            var el = e.target.closest ? e.target.closest('[data-act]') : null;
            if (!el) return;
            var act = el.getAttribute('data-act');
            if (act === 'kind') { S.DocKind = el.value; if (isNote() && !S.SourceMode) S.SourceMode = 'NEXTACC';
                if (isNote() && S.NoteReasonCode && !OcrMath.findReason(S.DocKind, S.NoteReasonCode)) { S.NoteReasonCode = null; S.NoteReason = 0; } if (S.DocKind !== 'VCN' && S.SourceMode === 'PRT') S.SourceMode = 'NEXTACC'; if (S.DocKind !== 'VCN' && S.SourceMode !== 'PRT') S.ReturnRef = ''; render(); }
            else if (act === 'invlines') { if (M.inventoryCode) (S.Lines || []).forEach(function (l) { l.AccountCode = M.inventoryCode; }); render(); }
            else if (act === 'src') { S.SourceMode = el.value; render(); }
            else if (act === 'set') { S.Settlement = el.value; render(); }
            else if (act === 'add') {
                var last = S.Lines[S.Lines.length - 1] || {};
                S.Lines.push({ Description: '', Qty: 1, UnitPrice: 0, Discount: 0, AccountCode: last.AccountCode || '', VatType: last.VatType || '7', VatClaimable: last.VatClaimable !== false, VatOverride: null, WhtRate: last.WhtRate || 0, IncomeType: '', OcrAccountCode: '' });
                render();
            }
            else if (act === 'fit') {
                var incl = !!S.PricesIncludeVat, want = numOrNull(incl ? S.PrintedTotal : S.PrintedSubTotal);
                if (want === null) { alert('กรอก "' + (incl ? 'ยอดรวมตามเอกสาร' : 'ยอดก่อน VAT ตามเอกสาร') + '" ก่อน'); return; }
                if (!OcrMath.applyFix(S, 'ADD_GAP')) { alert('ยอดรายการไม่น้อยกว่าเอกสาร — แก้/ลดรายการแทน (NextAcc ไม่รับบรรทัดติดลบ)'); return; }
                render();
            }
            else if (act === 'basis') { var nv = el.value === '1'; if (nv !== !!S.PricesIncludeVat) { S.PricesIncludeVat = nv; S.PriceBasisSource = 'USER'; (S.Lines || []).forEach(function (l) { l.VatOverride = null; }); } render(); }
            else if (act === 'fix') { if (OcrMath.applyFix(S, el.getAttribute('data-code'))) render(); }
            else if (act === 'srcacc') { (S.Lines || []).forEach(function (l) { if (l.SourceAccountCode) l.AccountCode = l.SourceAccountCode; }); render(); }
            else if (act === 'reload') { pickPost(); }
            else if (act === 'del') { if (S.Lines.length <= 1) { alert('ต้องมีอย่างน้อย 1 รายการ'); return; } S.Lines.splice(+el.getAttribute('data-i'), 1); render(); }
            else if (act === 'pick') {
                var r = (M.cnResults || [])[+el.getAttribute('data-i')]; if (!r) return;
                S.SourceDocId = r.Id; S.SourceDocNo = r.No; S.SourceDocType = r.Type; S.SourceTotal = r.Total; S.SourceBalanceDue = r.BalanceDue;
                S.SourceCredited = r.Credited; S.SourceContactId = r.ContactId; S.SourceContactName = r.ContactName;
                S.SourceSubTotal = r.SubTotal || 0; S.SourceSupplierDocNo = r.SupplierDocNo || null; S.SourceLines = [];
                S.SourcePaidAccountId = null; S.SourcePaidAccountLabel = null; S.SettlementUserSet = false;
                if (!S.SupplierName && r.ContactName) S.SupplierName = r.ContactName;
                pickPost();   // เซิร์ฟเวอร์ดึงรายการ/บัญชี/การจ่ายของใบเดิม → ตั้งบัญชีบรรทัด + ขาเงินเริ่มต้น
            }
            else if (act === 'search') { search(); }
        }
        function search() {
            writeState();
            OCR_overlay('กำลังค้นเอกสารต้นทางใน NextAcc…');
            $(btnSearchId).click();
        }
        function pickPost() {
            writeState();
            OCR_overlay('กำลังดึงรายการและผังบัญชีของใบต้นทาง…');
            $(btnPickId).click();
        }
        /* \u003c / \u0026 — กัน ASP.NET request validation ("<x" / "&#") เมื่อผู้ใช้พิมพ์อักขระพิเศษ (JSON ถอดกลับเองฝั่งเซิร์ฟเวอร์) */
        function writeState() { $(hfState).value = JSON.stringify(S).replace(/</g, '\\u003c').replace(/>/g, '\\u003e').replace(/&/g, '\\u0026'); }

        function openConfirm() {
            var v = updateDynamic();
            if (v.errors.length) { alert('ยังมีข้อที่ต้องแก้ ' + v.errors.length + ' ข้อ'); return; }
            var t = v.totals, h = '<table class="tbl">';
            function row(a, b) { h += '<tr><th style="width:38%;">' + a + '</th><td>' + b + '</td></tr>'; }
            row('ชนิดเอกสาร', '<b>' + esc(kindLabel(S.DocKind)) + '</b>');
            row('ผู้ขาย', esc(S.SupplierName || S.SourceContactName || '-') + (S.SupplierTaxId ? '<br/><small>' + esc(S.SupplierTaxId) + ' สาขา ' + esc(OcrMath.normBranch(S.SupplierBranch) || '-') + '</small>' : ''));
            row('เลขที่ / วันที่เอกสาร', esc(S.SupplierDocNo || '-') + ' · ' + esc(S.SupplierDocDate || '-'));
            row('วันที่ลงบัญชี', esc(S.DocDate));
            row('จำนวนรายการ', (S.Lines || []).length + ' รายการ · บัญชี ' + esc(Array.from(new Set((S.Lines || []).map(function (l) { return l.AccountCode; }))).join(', ')));
            row('ยอดก่อน VAT / VAT / รวม', money(t.sub) + ' / ' + money(t.vat) + ' / <b>' + money(t.total) + '</b>' + (S.PricesIncludeVat ? '<br/><small>ราคาในรายการรวม VAT แล้ว (ส่ง NextAcc แบบราคารวม VAT)</small>' : ''));
            row('ภาษีซื้อ', 'เคลม ' + money(t.vatClaim) + (t.vatNoClaim ? ' · ไม่เคลม ' + money(t.vatNoClaim) : '') + (S.FullTaxInvoice ? '' : ' · <i>ไม่ใช่ใบกำกับเต็มรูป</i>'));
            if (t.wht > 0) row('หัก ณ ที่จ่าย / จ่ายสุทธิ', money(t.wht) + ' / <b>' + money(t.net) + '</b>');
            if (S.DocKind === 'PV') row('แหล่งจ่ายเงิน', esc(optText('PaidHowAccountId') || 'ให้ NextAcc เลือกเอง'));
            if (isNote()) {
                row('ใบต้นทาง', (S.SourceMode === 'NEXTACC' ? esc(S.SourceDocNo || '-') : (S.SourceMode === 'OUTSIDE' ? 'อยู่นอกระบบ' : 'คืนสินค้า ' + esc(S.ReturnRef || ''))) + (S.OrigTaxInvoiceNo ? '<br/><small>อ้างใบกำกับเดิม ' + esc(S.OrigTaxInvoiceNo) + '</small>' : ''));
                var rsn = OcrMath.findReason(S.DocKind, S.NoteReasonCode);
                row('เหตุผล', esc(rsn ? rsn.t : '-') + (S.NoteReasonText ? ' — ' + esc(S.NoteReasonText) : ''));
                var es = M.cnPrimary ? effSettlement() : (S.Settlement || 'AUTO'), srcUnpaid = S.SourceMode !== 'NEXTACC' || !sourcePaid();
                if (S.SourceMode !== 'PRT') row('ขาเงิน', srcUnpaid ? (S.DocKind === 'VCN' ? 'ลดเจ้าหนี้' : 'ตั้งเจ้าหนี้') : (es === 'BANK' ? (S.DocKind === 'VCN' ? 'รับเงินคืนเข้า ' : 'จ่ายจาก ') + esc(optText('RefundAccountId') || '-') : (es === 'VENDOR' ? (S.DocKind === 'VCN' ? 'เก็บเป็นเครดิตกับผู้ขาย — ' + esc(M.vendorCreditLabel || 'เจ้าหนี้การค้า') : 'ตั้งเจ้าหนี้รอจ่าย') : 'ตาม NextAcc (เงินสด 111)')));
                if (pickedReturn()) row('การคืนสินค้า', '<b>' + esc(S.ReturnRef) + '</b> — ใบลดหนี้นี้เป็นรายการจริง: กลับ ' + esc(S.ReturnRef) + '-REV + ปรับต้นทุน ' + esc(S.ReturnRef) + '-DIF');
            }
            h += '</table>';
            h += S.SourceMode === 'PRT' && isNote()
                ? '<div class="ocr-msg info">จะบันทึกเป็นหลักฐานของการคืนสินค้า — ไม่สร้างเอกสารบน NextAcc</div>'
                : '<div class="ocr-msg warn">กดยืนยัน = สร้างเอกสารบน NextAcc และ <b>อนุมัติ (ลงบัญชี)</b> ทันที · ถ้า NextAcc มีคำเตือน ระบบจะหยุดให้อ่านก่อน</div>';
            $('rvModalBody').innerHTML = h;
            $('rvModal').style.display = 'flex';
        }
        function optText(f) {
            var p = (M.paidHow || []).filter(function (a) { return a.id === S[f]; })[0]; if (p) return p.n;
            var s = (M.money || []).filter(function (a) { return a.id === S[f]; })[0]; if (s) return (s.c ? s.c + ' ' : '') + s.n;
            return S[f] && S[f] === S.SourcePaidAccountId ? (S.SourcePaidAccountLabel || '') : '';
        }
        function closeConfirm() { $('rvModal').style.display = 'none'; }
        function beforePost(action) {
            if (!S) return true;
            writeState();
            if (action === 'submit' || action === 'ack') {
                var v = OcrMath.validate(S);
                if (v.errors.length) { alert('ยังมีข้อที่ต้องแก้ ' + v.errors.length + ' ข้อ'); closeConfirm(); return false; }
                closeConfirm();
                OCR_overlay('กำลังสร้าง & อนุมัติเอกสารบน NextAcc… (อย่าปิดหน้านี้)');
            } else OCR_overlay('กำลังบันทึก…');
            return true;
        }
        function zoom(d) {
            var img = $('rvDocView').querySelector('img'); if (!img) return;
            scale = d === 0 ? fitScale(img) : Math.max(0.2, Math.min(5, scale + d));
            applyT(img);
        }
        function fitScale(img) { var w = $('rvDocView').clientWidth - 4, nw = (rot % 180 === 0 ? img.naturalWidth : img.naturalHeight) || w; return Math.min(1.5, w / nw); }
        function rotate() { var img = $('rvDocView').querySelector('img'); if (!img) return; rot = (rot + 90) % 360; scale = fitScale(img); applyT(img); }
        function applyT(img) {
            var w = img.naturalWidth * scale, hh = img.naturalHeight * scale, tx = 0, ty = 0;
            if (rot === 90) tx = hh; else if (rot === 180) { tx = w; ty = hh; } else if (rot === 270) ty = w;
            img.style.width = w + 'px'; img.style.height = hh + 'px'; img.style.transform = 'translate(' + tx + 'px,' + ty + 'px) rotate(' + rot + 'deg)';
            img.style.marginBottom = (rot % 180 === 0 ? 0 : (w - hh)) + 'px';
        }
        function showDoc() {
            var v = $('rvDocView'); if (!v || !M.previewUrl) return;
            $('rvDocName').textContent = M.fileName || '';
            $('rvDocOpen').href = M.previewUrl;
            if (M.previewIsPdf) { v.innerHTML = '<iframe src="' + esc(M.previewUrl) + '#view=FitH" title="เอกสาร"></iframe>'; return; }
            var img = new Image();
            img.alt = 'เอกสารที่สแกน';
            img.onload = function () { scale = fitScale(img); applyT(img); };
            img.onerror = function () { v.innerHTML = '<iframe src="' + esc(M.previewUrl) + '" title="เอกสาร"></iframe>'; };
            img.src = M.previewUrl;
            v.innerHTML = ''; v.appendChild(img);
        }
        function init() {
            var hs = $(hfState), hm = $(hfMeta);
            if (!hs || !hm || !hs.value) return;
            try { S = JSON.parse(hs.value); M = JSON.parse(hm.value || '{}'); } catch (e) { $('rvForm').innerHTML = '<div class="ocr-msg err">อ่านข้อมูลตรวจทานไม่ได้: ' + esc(e.message) + '</div>'; return; }
            S.Lines = S.Lines || []; S.LowConfidence = S.LowConfidence || []; S.SourceLines = S.SourceLines || [];
            OcrMath.setReasons(M.reasons);
            window.addEventListener('resize', padForBar);
            if (M.cnPrimary && S.SourceMode === 'PRT') S.SourceMode = 'NEXTACC';   // ร่างเก่า (หลักฐาน PRT) → ใบลดหนี้จริงที่ผูกการคืน
            var form = $('rvForm');
            form.addEventListener('input', onInput);
            form.addEventListener('change', onInput);
            form.addEventListener('click', onClick);
            form.addEventListener('keydown', function (e) { if (e.key === 'Enter' && e.target.tagName !== 'TEXTAREA') { e.preventDefault(); if (e.target.getAttribute('data-f') === 'SearchText') search(); } });
            render();
            showDoc();
        }
        document.addEventListener('DOMContentLoaded', init);
        return { beforePost: beforePost, openConfirm: openConfirm, closeConfirm: closeConfirm, zoom: zoom, rotate: rotate, _state: function () { return S; } };
    })();
    </script>
</asp:Content>
