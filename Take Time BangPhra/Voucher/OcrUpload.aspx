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
                ต้องตั้งค่า API key แบบ acc_ และเปิดใช้งาน Accounting Integration ก่อน
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
            <asp:Button ID="btnScan" runat="server" CssClass="ocr-btn" Text="อัปโหลด & สแกน OCR" OnClick="btnScan_Click" />
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
                    <span class="ocr-hint">เลือกแหล่งเงินที่จ่ายจริง → ระบบจะบังคับให้ NextAcc เครดิตบัญชีนั้น (ถ้าไม่เลือก = ปล่อยให้ NextAcc เลือกเอง)</span>
                </div>
            </div>
            <div class="ocr-row">
                <div class="ocr-field">
                    <label>ผังบัญชีที่จะชาร์จ (บัญชีค่าใช้จ่าย / ฝั่งเดบิต)</label>
                    <asp:DropDownList ID="ddlChargeAccount" runat="server" CssClass="form-control" />
                    <span class="ocr-hint">เลือกบัญชีค่าใช้จ่ายที่จะลง (ถ้าไม่เลือก = ใช้บัญชีที่ OCR แนะนำ)</span>
                </div>
                <div class="ocr-field">
                    <label>ภาษีซื้อ (VAT)</label>
                    <asp:DropDownList ID="ddlVatClaim" runat="server" CssClass="form-control">
                        <asp:ListItem Value="1" Text="เคลมภาษีซื้อ (Dr ภาษีซื้อ)" Selected="True" />
                        <asp:ListItem Value="0" Text="ไม่เคลม — รวม VAT เข้าค่าใช้จ่าย (§82/5)" />
                    </asp:DropDownList>
                    <span class="ocr-hint">ไม่เคลม → NextAcc รวม VAT เข้าบัญชีค่าใช้จ่ายที่เลือก (ไม่แยกภาษีซื้อ)</span>
                </div>
            </div>
            <asp:Literal ID="litSuggested" runat="server" />
            <div style="margin-top:12px;">
                <asp:Button ID="btnCreate" runat="server" CssClass="ocr-btn green" Text="สร้างใบสำคัญจ่าย & อนุมัติ" OnClick="btnCreate_Click"
                    OnClientClick="if(this.dataset.busy){return false;} this.dataset.busy='1'; this.value='กำลังสร้าง...';" UseSubmitBehavior="false" />
                <span class="ocr-hint">การกดนี้จะสร้างเอกสารใน NextAcc และอนุมัติ (auto-post GL)</span>
            </div>
        </asp:Panel>

        <asp:Literal ID="litResult" runat="server" />
        <asp:HiddenField ID="hfScanId" runat="server" />
        <asp:HiddenField ID="hfDebitAcc" runat="server" />
        <asp:HiddenField ID="hfHasWht" runat="server" />
    </div>
</asp:Content>
