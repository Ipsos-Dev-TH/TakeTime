<%@ Page Title="OCR ใบสำคัญจ่าย" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="OcrUpload.aspx.cs" Inherits="Take_Time_BangPhra.Voucher.OcrUpload" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .ocr-wrap { max-width: 860px; margin: 20px auto; font-family: inherit; }
        .ocr-card { background:#fff; border:1px solid #e3e3e3; border-radius:8px; padding:18px 20px; margin-bottom:16px; box-shadow:0 1px 3px rgba(0,0,0,.06); }
        .ocr-card h3 { margin:0 0 12px; font-size:16px; }
        .ocr-row { display:flex; gap:12px; margin-bottom:10px; flex-wrap:wrap; }
        .ocr-field { flex:1 1 240px; }
        .ocr-field label { display:block; font-size:12px; color:#666; margin-bottom:3px; }
        .ocr-field input { width:100%; padding:7px 9px; border:1px solid #ccc; border-radius:4px; box-sizing:border-box; }
        .ocr-btn { background:#2b6cb0; color:#fff; border:none; padding:9px 18px; border-radius:5px; cursor:pointer; font-size:14px; }
        .ocr-btn.green { background:#2f855a; }
        .ocr-msg { padding:10px 12px; border-radius:5px; margin:8px 0; font-size:13px; }
        .ocr-msg.info { background:#ebf8ff; color:#2c5282; }
        .ocr-msg.warn { background:#fffaf0; color:#9c4221; }
        .ocr-msg.err { background:#fff5f5; color:#c53030; }
        .ocr-msg.ok { background:#f0fff4; color:#276749; }
        .ocr-hint { font-size:12px; color:#777; }
        .badge { display:inline-block; padding:2px 8px; border-radius:10px; font-size:12px; color:#fff; }
    </style>

    <div class="ocr-wrap">
        <div class="ocr-card">
            <h3><i class="fas fa-file-import"></i> สแกนเอกสารด้วย OCR แล้วสร้างใบสำคัญจ่าย</h3>
            <p class="ocr-hint">
                อัปโหลดใบกำกับภาษี/ใบเสร็จ → ระบบ OCR ดึงข้อมูลให้ตรวจสอบ/แก้ไข → กดสร้างใบสำคัญจ่าย (Draft) และอนุมัติใน NextAcc.
                ต้องเปิดใช้งาน Accounting Integration + ตั้ง Company ID (ใช้ได้ทั้ง key แบบ int_ และ acc_)
            </p>
            <div class="ocr-row">
                <div class="ocr-field" style="flex:2 1 380px;">
                    <label>ไฟล์เอกสาร (PDF / รูปภาพ)</label>
                    <asp:FileUpload ID="fuOcr" runat="server" />
                </div>
                <div class="ocr-field">
                    <label>ประเภทเอกสารที่จะสร้าง</label>
                    <asp:DropDownList ID="ddlTargetType" runat="server" CssClass="form-control">
                        <asp:ListItem Value="PaymentVoucher" Text="ใบสำคัญจ่าย (PaymentVoucher)" />
                        <asp:ListItem Value="Expense" Text="ค่าใช้จ่าย (Expense)" />
                    </asp:DropDownList>
                </div>
            </div>
            <asp:Button ID="btnScan" runat="server" CssClass="ocr-btn" Text="อัปโหลด & สแกน OCR" OnClick="btnScan_Click"
                OnClientClick="if(this.dataset.busy){return false;} this.dataset.busy='1'; this.value='⏳ กำลังสแกน...'; var o=document.getElementById('ocrOverlay'); if(o){document.getElementById('ocrOverlayMsg').innerText='กำลังสแกน OCR… อาจใช้เวลาสักครู่ (อย่าปิดหน้านี้)'; o.style.display='flex';}" UseSubmitBehavior="false" />
            <asp:Literal ID="litStatus" runat="server" />
        </div>

        <asp:Panel ID="pnlReview" runat="server" Visible="false" CssClass="ocr-card">
            <h3><i class="fas fa-clipboard-check"></i> ตรวจสอบ & แก้ไขข้อมูลก่อนสร้างเอกสาร</h3>
            <asp:Literal ID="litMeta" runat="server" />
            <div class="ocr-row">
                <div class="ocr-field"><label>ชื่อผู้ขาย</label><asp:TextBox ID="txtVendorName" runat="server" /></div>
                <div class="ocr-field"><label>เลขผู้เสียภาษี</label><asp:TextBox ID="txtVendorTaxId" runat="server" /></div>
            </div>
            <div class="ocr-row">
                <div class="ocr-field">
                    <label>หรือเลือกผู้ขายจากระบบ (กรณี OCR ไม่เจอชื่อ)</label>
                    <asp:DropDownList ID="ddlVendor" runat="server" CssClass="form-control" />
                    <span class="ocr-hint">เลือกแล้วระบบจะผูกผู้ติดต่อ (contact) ของผู้ขายนี้ให้เอกสารใน NextAcc (แทนชื่อที่ OCR ไม่เจอ)</span>
                </div>
            </div>
            <div class="ocr-row">
                <div class="ocr-field"><label>เลขที่เอกสาร</label><asp:TextBox ID="txtDocNumber" runat="server" /></div>
                <div class="ocr-field"><label>วันที่ (yyyy-MM-dd)</label><asp:TextBox ID="txtDocDate" runat="server" /></div>
            </div>
            <div class="ocr-row">
                <div class="ocr-field"><label>ยอดก่อน VAT</label><asp:TextBox ID="txtSubTotal" runat="server" /></div>
                <div class="ocr-field"><label>VAT</label><asp:TextBox ID="txtVat" runat="server" /></div>
                <div class="ocr-field"><label>ยอดรวม</label><asp:TextBox ID="txtTotal" runat="server" /></div>
            </div>
            <div class="ocr-row">
                <div class="ocr-field">
                    <label>แหล่งจ่ายเงิน (บังคับบัญชีเงินสด/ธนาคารฝั่งเครดิต)</label>
                    <asp:DropDownList ID="ddlPaidHow" runat="server" CssClass="form-control" />
                    <span class="ocr-hint">รายการดึงจาก<b>ผังบัญชีจริงของ NextAcc</b> (เงินสด/ธนาคาร/เจ้าหนี้กรรมการ) → เลือกแล้วบังคับ Cr บัญชีนั้นตรง ๆ ไม่ผ่าน mapping. ถ้าว่าง กด "Sync บัญชี" ในหน้า Admin ก่อน</span>
                </div>
            </div>
            <div class="ocr-row">
                <div class="ocr-field">
                    <label>ผังบัญชีที่จะชาร์จ (บัญชีค่าใช้จ่าย / ฝั่งเดบิต)</label>
                    <asp:DropDownList ID="ddlChargeAccount" runat="server" CssClass="form-control" />
                    <span class="ocr-hint">รายการดึงจาก<b>ผังบัญชีค่าใช้จ่ายของ NextAcc</b> (+ เจ้าหนี้/เงินทดรองกรรมการ สำหรับเคส "คืนเงินทดรองกรรมการ" = เดบิตเจ้าหนี้กรรมการ / เครดิตธนาคาร). ไม่เลือก = คงหลายรายการตามที่ OCR แยก; เลือก = ยุบทุกรายการเป็นบัญชีเดียวนี้</span>
                </div>
                <div class="ocr-field">
                    <label>ภาษีซื้อ (VAT)</label>
                    <asp:DropDownList ID="ddlVatClaim" runat="server" CssClass="form-control">
                        <asp:ListItem Value="1" Text="เคลมภาษีซื้อ (Dr ภาษีซื้อ)" Selected="True" />
                        <asp:ListItem Value="0" Text="ไม่เคลม — รวม VAT เข้าค่าใช้จ่าย (§82/5)" />
                    </asp:DropDownList>
                    <span class="ocr-hint">ไม่เคลม → NextAcc รวม VAT เข้าบัญชีค่าใช้จ่ายที่เลือก (ไม่แยกภาษีซื้อ)</span>
                </div>
                <div class="ocr-field">
                    <label>หัก ณ ที่จ่าย (%)</label>
                    <asp:TextBox ID="txtWhtRate" runat="server" placeholder="0" />
                    <span class="ocr-hint">เช่น 3 = 3% (0 = ไม่หัก). คิดบนฐานยอดก่อน VAT</span>
                </div>
            </div>
            <asp:Literal ID="litSuggested" runat="server" />
            <div style="margin-top:12px;">
                <asp:Button ID="btnCreate" runat="server" CssClass="ocr-btn green" Text="สร้างใบสำคัญจ่าย & อนุมัติ" OnClick="btnCreate_Click"
                    OnClientClick="if(this.dataset.busy){return false;} this.dataset.busy='1'; this.value='กำลังสร้าง...'; var o=document.getElementById('ocrOverlay'); if(o){document.getElementById('ocrOverlayMsg').innerText='กำลังสร้าง & อนุมัติเอกสารใน NextAcc… (อย่าปิดหน้านี้)'; o.style.display='flex';}" UseSubmitBehavior="false" />
                <span class="ocr-hint">การกดนี้จะสร้างเอกสารใน NextAcc และอนุมัติ (auto-post GL)</span>
            </div>
        </asp:Panel>

        <asp:Literal ID="litResult" runat="server" />
        <asp:HiddenField ID="hfScanId" runat="server" />
        <asp:HiddenField ID="hfDebitAcc" runat="server" />
        <asp:HiddenField ID="hfHasWht" runat="server" />
        <asp:HiddenField ID="hfCreatedDocId" runat="server" />
    </div>

    <div id="ocrOverlay" style="display:none; position:fixed; inset:0; background:rgba(0,0,0,.35); z-index:9999; align-items:center; justify-content:center;">
        <div style="background:#fff; padding:22px 30px; border-radius:10px; box-shadow:0 4px 20px rgba(0,0,0,.25); font-size:15px; text-align:center;">
            <div style="width:38px; height:38px; margin:0 auto 12px; border:4px solid #cbd5e0; border-top-color:#2b6cb0; border-radius:50%; animation:ocrspin 0.8s linear infinite;"></div>
            <span id="ocrOverlayMsg">กำลังประมวลผล… กรุณารอสักครู่ (อย่าปิดหน้านี้)</span>
        </div>
    </div>
    <style>@keyframes ocrspin { to { transform: rotate(360deg); } }</style>
</asp:Content>
