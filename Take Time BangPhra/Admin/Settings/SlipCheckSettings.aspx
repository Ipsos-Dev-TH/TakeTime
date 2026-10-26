<%@ Page Title="ตรวจสลิปโอน" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="SlipCheckSettings.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Settings.SlipCheckSettings" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link rel="stylesheet" href="<%= ResolveUrl("~/Content/admin-settings.css") %>?v=<%= Take_Time_BangPhra.Admin.Settings.SettingsUi.AssetVersion %>" />
    <script src="<%= ResolveUrl("~/Scripts/admin-settings.js") %>?v=<%= Take_Time_BangPhra.Admin.Settings.SettingsUi.AssetVersion %>"></script>
    <style>
        .sc-wrap { max-width: 1100px; margin: 0 auto; padding: 12px 12px 30px; }
        .sc-head { background: linear-gradient(135deg,#1565c0,#0d47a1); color:#fff; border-radius:14px; padding:20px 22px; margin-bottom:16px; }
        .sc-head h2 { margin:0 0 6px; font-size:21px; }
        .sc-head p { margin:0; opacity:.93; font-size:14px; line-height:1.7; }
        .sc-card { background:#fff; border-radius:14px; padding:18px 20px; margin-bottom:16px; box-shadow:0 2px 10px rgba(0,0,0,.05); }
        .sc-card h3 { margin:0 0 4px; font-size:16.5px; color:#0d47a1; }
        .sc-card .sub { color:#6b7c85; font-size:13px; margin-bottom:12px; line-height:1.65; }
        .sc-alert { padding:12px 15px; border-radius:10px; margin-bottom:14px; font-size:14px; line-height:1.7; }
        .sc-alert.ok { background:#e8f6ee; color:#16653e; } .sc-alert.err { background:#fdecec; color:#a12626; }
        .sc-row { display:grid; grid-template-columns: 260px 1fr; gap:10px; align-items:center; padding:8px 0; border-bottom:1px solid #f0f3f5; font-size:14px; }
        .sc-row:last-child { border-bottom:0; }
        .sc-row small { display:block; color:#90a4ae; font-size:12px; line-height:1.5; }
        .sc-in { width:100%; max-width:420px; padding:7px 9px; border:1.5px solid #dbe2e7; border-radius:8px; font-size:14px; box-sizing:border-box; }
        .sc-status { display:flex; flex-wrap:wrap; gap:6px; }
        .sc-btn { padding:10px 20px; border:0; border-radius:10px; background:#1976d2; color:#fff; font-size:14px; font-weight:600; cursor:pointer; margin:4px 6px 4px 0; }
        .sc-btn.sec { background:#eceff1; color:#37474f; }
        .sc-scroll { overflow-x:auto; }
        table.sc-t { width:100%; border-collapse:collapse; font-size:13px; min-width:640px; }
        table.sc-t th { background:#f5f7f9; padding:8px; text-align:left; border-bottom:2px solid #e3e9ed; }
        table.sc-t td { padding:6px 8px; border-bottom:1px solid #f0f3f5; vertical-align:middle; }
        table.sc-t input { width:100%; padding:5px 7px; border:1.5px solid #dbe2e7; border-radius:7px; font-size:13px; box-sizing:border-box; }
        pre.sc-raw { background:#f7f9fa; padding:10px; border-radius:8px; font-size:12px; white-space:pre-wrap; word-break:break-all; max-height:240px; overflow:auto; }
        @media (max-width: 640px) {
            .sc-wrap { padding: 8px 4px 24px; }
            .sc-row { grid-template-columns: 1fr; }
        }
    </style>

    <div class="sc-wrap" data-as-dirty="1">
        <nav class="as-crumb" aria-label="เส้นทาง">
            <a href="<%= ResolveUrl("~/Admin/Settings/Index") %>"><i class="fas fa-sliders"></i> ศูนย์ตั้งค่า</a>
            <span class="sep">›</span><span>การชำระเงิน</span>
            <span class="sep">›</span><span class="here">ตรวจสลิปโอน</span>
            <a class="as-back" href="<%= ResolveUrl("~/Admin/Settings/Index") %>">← กลับศูนย์ตั้งค่า</a>
        </nav>

        <div class="sc-head">
            <h2><i class="fas fa-magnifying-glass-dollar"></i> ตรวจสลิปโอนก่อนออกเอกสาร</h2>
            <p>
                ทุกครั้งที่รับเงินด้วยการโอน/QR ระบบตรวจสลิปก่อนออกใบเสร็จและส่ง NextAcc:
                <b>① SlipOK</b> (ธนาคารยืนยันยอด — แพ็กเกจฟรี 100 สลิป/เดือน) →
                <b>② อ่านตัวเลขยอดโอนจากรูปเอง</b> (Tesseract ในเครื่อง เมื่อโควต้าหมด) →
                <b>③ ตรวจไม่ได้ = ข้ามการตรวจ</b> ใช้ยอดที่พนักงานลง แต่ติดธง "ไม่ได้ตรวจ" ไว้ให้ตรวจภายหลัง ·
                อ่าน QR ในสลิปทุกครั้ง (ฟรี) เพื่อกันสลิปซ้ำ ·
                ยอดไม่ตรง / สลิปซ้ำ / โอนเข้าบัญชีอื่น = หยุดก่อนออกเอกสาร (Owner/Admin ยืนยันพร้อมเหตุผลได้ ยกเว้นสลิปซ้ำ)
            </p>
        </div>

        <asp:Literal ID="litMsg" runat="server" />

        <div class="sc-card">
            <h3>สถานะ</h3>
            <asp:Literal ID="litStatus" runat="server" />
        </div>

        <div class="sc-card">
            <h3>ตั้งค่า</h3>
            <div class="sub">API key เก็บแบบเข้ารหัส (เหมือนคีย์ NextAcc) — เว้นว่าง = คงเดิม, ใส่ "-" = ล้าง · ระบบไม่บันทึกคีย์ลง log</div>
            <div class="sc-row"><div>เปิดตรวจสลิป<small>ปิด = ทุกหน้าทำงานแบบเดิม</small></div>
                <div><asp:CheckBox ID="chkEnabled" runat="server" Text=" เปิดใช้งาน" /></div></div>
            <div class="sc-row"><div>SlipOK Branch ID<small>เมนู API ใน LINE SlipOK</small></div>
                <div><asp:TextBox ID="txtBranchId" runat="server" CssClass="sc-in" MaxLength="50" /></div></div>
            <div class="sc-row"><div>SlipOK API key<small>header x-authorization</small></div>
                <div><asp:TextBox ID="txtApiKey" runat="server" CssClass="sc-in" TextMode="Password" autocomplete="new-password" MaxLength="200" /></div></div>
            <div class="sc-row"><div>โควต้าต่อเดือน<small>แพ็กเกจฟรี = 100 · ครบแล้วใช้การอ่านยอดจากรูปแทน</small></div>
                <div><asp:TextBox ID="txtQuota" runat="server" CssClass="sc-in" TextMode="Number" /></div></div>
            <div class="sc-row"><div>ยอมให้ยอดต่างได้ (บาท)<small>ต่างไม่เกินนี้ = ตรง และใช้ยอดจากธนาคาร</small></div>
                <div><asp:TextBox ID="txtTolerance" runat="server" CssClass="sc-in" /></div></div>
            <div class="sc-row"><div>ตรวจบัญชีผู้รับ<small>เทียบเลขบัญชีที่ปิดบังในสลิปกับตารางด้านล่าง</small></div>
                <div><asp:CheckBox ID="chkReceiver" runat="server" Text=" ตรวจ" /></div></div>
            <div class="sc-row"><div>อ่านยอดจากรูปเอง (ระดับ ②)<small>ต้องติดตั้ง Tesseract บนเซิร์ฟเวอร์</small></div>
                <div><asp:CheckBox ID="chkLocalOcr" runat="server" Text=" ใช้" /></div></div>
            <div class="sc-row"><div>ที่อยู่ tesseract.exe<small>ว่าง = C:\Program Files\Tesseract-OCR\tesseract.exe</small></div>
                <div><asp:TextBox ID="txtTessPath" runat="server" CssClass="sc-in" MaxLength="300" /></div></div>
            <div class="sc-row"><div>โฟลเดอร์ tessdata<small>ว่าง = ~/App_Data/tessdata (ถ้ามี) — ต้องมี tha + eng.traineddata</small></div>
                <div><asp:TextBox ID="txtTessdata" runat="server" CssClass="sc-in" MaxLength="300" /></div></div>
        </div>

        <div class="sc-card">
            <h3>บัญชีรับเงินของร้าน (ตรวจผู้รับ)</h3>
            <div class="sub">
                ใส่รหัสธนาคาร 3 หลัก (004 กสิกร · 014 ไทยพาณิชย์ · 006 กรุงไทย · 002 กรุงเทพ · 025 กรุงศรี · 011 ทีทีบี · 030 ออมสิน)
                และเลขบัญชี/พร้อมเพย์ของแหล่งเงินที่รับโอน — เว้นเลขบัญชีว่าง = ใช้เลขบัญชีจาก NextAcc ที่ผูกไว้ (ถ้ามี) ·
                ยังไม่ตั้งเลยสักบัญชี = ข้ามการตรวจผู้รับ
            </div>
            <div class="sc-scroll"><asp:Literal ID="litReceivers" runat="server" /></div>
        </div>

        <asp:Button ID="btnSave" runat="server" Text="💾 บันทึก" CssClass="sc-btn" OnClick="btnSave_Click" />
        <asp:Button ID="btnResetQuota" runat="server" Text="ล้างธงโควต้าหมด (หลังต่อแพ็กเกจ)" CssClass="sc-btn sec" OnClick="btnResetQuota_Click" CausesValidation="false" />
        <asp:Button ID="btnQuota" runat="server" Text="ถามโควต้าจาก SlipOK" CssClass="sc-btn sec" OnClick="btnQuota_Click" CausesValidation="false" />

        <div class="sc-card" style="margin-top:16px;">
            <h3>ทดสอบด้วยสลิป</h3>
            <div class="sub">อัปโหลดรูปสลิป + ยอดที่คาดไว้ → ดูผลทุกขั้น (QR / SlipOK / อ่านจากรูป) · ทดสอบไม่บันทึกลงสลิปใด ๆ แต่ใช้โควต้า SlipOK 1 ครั้ง (รูปเดิมซ้ำใน 1 ชม. ไม่เสียซ้ำ)</div>
            <asp:FileUpload ID="fuTest" runat="server" accept="image/*" />
            <asp:TextBox ID="txtTestAmount" runat="server" CssClass="sc-in" placeholder="ยอดที่คาดไว้ เช่น 1000" style="max-width:200px; display:inline-block;" />
            <asp:Button ID="btnTest" runat="server" Text="🔎 ตรวจ" CssClass="sc-btn" OnClick="btnTest_Click" CausesValidation="false" />
            <asp:Literal ID="litTest" runat="server" />
        </div>
    </div>
</asp:Content>
