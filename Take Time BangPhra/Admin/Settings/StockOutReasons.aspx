<%@ Page Title="เหตุผลการเบิกสินค้า" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="StockOutReasons.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Settings.StockOutReasons" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .sr-wrap { max-width: 1180px; margin: 0 auto; padding: 18px 12px 60px; }
        .sr-head { background: linear-gradient(135deg,#4e342e,#795548); color:#fff; border-radius:14px; padding:22px 26px; margin-bottom:18px; }
        .sr-head h2 { margin:0 0 6px; font-weight:700; font-size:1.5em; }
        .sr-head p { margin:0; opacity:.93; font-size:14px; }
        .sr-note { background:#efebe9; border-left:4px solid #795548; padding:12px 15px; border-radius:8px; font-size:13px; color:#3e2723; margin-bottom:16px; line-height:1.65; }
        .sr-note ul { margin:6px 0 0 18px; padding:0; }
        .sr-card { background:#fff; border-radius:11px; box-shadow:0 2px 10px rgba(0,0,0,.08); padding:16px 18px; margin-bottom:16px; }
        .sr-msg { padding:11px 15px; border-radius:8px; margin-bottom:14px; font-size:14px; }
        .sr-ok { background:#e8f5e9; color:#1e7e42; } .sr-err { background:#ffebee; color:#c62828; }
        table.sr-t { width:100%; border-collapse:collapse; font-size:13px; }
        table.sr-t th { background:#f5f7f9; text-align:left; padding:8px; font-weight:650; color:#37474f; border-bottom:2px solid #e3e9ed; }
        table.sr-t td { padding:7px 8px; border-bottom:1px solid #f0f3f5; vertical-align:top; }
        table.sr-t input[type=text], table.sr-t select, table.sr-t textarea { width:100%; padding:5px 7px; border:1px solid #d5dce1; border-radius:6px; font-size:13px; box-sizing:border-box; }
        table.sr-t textarea { min-height:58px; font-size:12px; }
        .sr-c { text-align:center; }
        .sr-code { font-family:Consolas,monospace; font-weight:600; color:#4e342e; }
        .sr-sub { font-size:11.5px; color:#90a4ae; }
        .sr-off td { background:#fafafa; color:#999; }
        .sr-scroll { overflow-x:auto; }
    </style>

    <div class="sr-wrap">
        <div class="sr-head">
            <h2>🏷 เหตุผลการเบิกสินค้า (ไม่ใช่การขาย)</h2>
            <p>กำหนดบัญชี ภาษีขายเสมือนขาย การอนุมัติ และหลักฐาน ของแต่ละเหตุผลที่ใช้ในหน้า สต็อก → บันทึกเบิกสินค้า</p>
        </div>

        <div class="sr-note">
            <b>⚠ ค่าเริ่มต้นมาจากงานค้นคว้า — ให้ผู้ทำบัญชียืนยันก่อนใช้จริง</b>
            <ul>
                <li><b>ถือเป็นการขาย (ภาษีขาย)</b>: ม.77/1(8) "นำสินค้าไปใช้ไม่ว่าประการใด ๆ" = ขาย — ภาษีขาย 7% จาก<b>ราคาตลาด</b> (ม.79/3)
                    ระบบลง JE: Dr บัญชีของเหตุผล / Cr ภาษีขาย 21911 → NextAcc นับเข้า ภ.พ.30 โดยไม่ออกใบกำกับ.
                    สินค้าขาดจากรายงาน/ถูกขโมย = ขาย ม.77/1(8)(จ) (ข้อหารือ กค 0706/พ./6444).</li>
                <li><b>ข้อยกเว้น (ไม่รวมเป็นฐานภาษี)</b> ประกาศอธิบดีฯ VAT ฉบับที่ 40: 2(1) ของแถมพร้อมการขาย (ระบุในใบกำกับ),
                    2(6) ของขวัญตามประเพณีราคาไม่เกินสมควร, 2(7) ตัวอย่าง/ของแจกส่งเสริมการขาย, 2(8) อาหารพนักงานระหว่างเวลางาน.
                    ใช้ในการให้บริการของตนเองโดยตรง (ของใช้ในห้องพัก) ไม่ถือเป็นการขาย.</li>
                <li><b>เสียหาย/หมดอายุ/ทำลาย</b>: ไม่ใช่การขายเมื่อมีหลักฐาน — ทำรายงานสินค้าเสียหาย + ภาพถ่าย + พยาน/ผู้สอบบัญชี ตามแนวคำสั่ง ป.79/2541 (แก้ ป.84/2542)
                    (พิมพ์จาก <a href="/Product/StockOutReport" target="_blank">รายงานสินค้าเสียหาย/ทำลาย</a>).</li>
                <li><b>หุ้นส่วนใช้ส่วนตัว</b>: หักรายจ่ายไม่ได้ ม.65 ตรี(3) → ผูก <code>OWNER_DRAWINGS</code> กับบัญชีถอนใช้ส่วนตัว (ผังห้างหุ้นส่วน NextAcc 31030).
                    <b>ค่ารับรอง</b> (ให้บุคคลเจาะจง) จำกัดตาม ม.65 ตรี(4) + กฎกระทรวง 143.</li>
                <li><b>บัญชี</b> = คีย์ใน Accounting_Account_Mapping (ผูกรหัสผังบัญชี NextAcc ที่หน้า Accounting Integration).
                    คีย์ที่ยังไม่ผูก → ลงบัญชี INVENTORY_LOSS / STOCK_LOSS_EXPENSE แทนและเขียน log เตือน.
                    บัญชีของเหตุผลที่มีภาษีขาย <b>ห้าม</b>เป็นบัญชีชื่อ "ภาษีขาย/ภาษีซื้อ" (จะหักล้างใน ภ.พ.30).</li>
                <li><b>ต้องอนุมัติ</b>: Admin บันทึก → รอ Owner/Admin อื่นอนุมัติ (Owner บันทึกเอง = อนุมัติแล้ว). ลงบัญชีเฉพาะรายการที่อนุมัติ.</li>
            </ul>
        </div>

        <asp:Panel ID="pnlMsg" runat="server" Visible="false">
            <div id="divMsg" runat="server" class="sr-msg sr-ok"><asp:Literal ID="litMsg" runat="server" /></div>
        </asp:Panel>

        <div class="sr-card sr-scroll">
            <table class="sr-t">
                <tr>
                    <th style="width:130px;">รหัส</th>
                    <th style="width:190px;">ชื่อ (ไทย)</th>
                    <th style="width:200px;">บัญชี (mapping key)</th>
                    <th class="sr-c" style="width:80px;">ถือเป็นการขาย<br/>(ภาษีขาย)</th>
                    <th style="width:95px;">ฐานภาษี</th>
                    <th class="sr-c" style="width:65px;">ต้อง<br/>อนุมัติ</th>
                    <th class="sr-c" style="width:65px;">ต้องมี<br/>หลักฐาน</th>
                    <th class="sr-c" style="width:55px;">ใช้งาน</th>
                    <th style="width:60px;">ลำดับ</th>
                    <th>คำอธิบาย/หลักกฎหมาย</th>
                </tr>
                <asp:Literal ID="litRows" runat="server" />
            </table>
            <div style="margin-top:14px; text-align:right;">
                <asp:Button ID="btnSave" runat="server" Text="💾 บันทึกการตั้งค่า" CssClass="btn btn-success btn-lg" OnClick="btnSave_Click" />
            </div>
        </div>

        <div class="sr-card">
            <h4 style="margin-top:0;">➕ เพิ่มเหตุผลใหม่</h4>
            <div style="display:flex; gap:10px; flex-wrap:wrap; align-items:flex-end;">
                <div><label class="sr-sub">รหัส (A-Z, 0-9, _)</label><br/><asp:TextBox ID="txtNewCode" runat="server" MaxLength="30" placeholder="เช่น KITCHEN_USE" /></div>
                <div style="flex:1; min-width:220px;"><label class="sr-sub">ชื่อ (ไทย)</label><br/><asp:TextBox ID="txtNewName" runat="server" MaxLength="100" Width="100%" /></div>
                <div><label class="sr-sub">บัญชี (mapping key)</label><br/><asp:DropDownList ID="ddlNewGl" runat="server" /></div>
                <div><asp:CheckBox ID="chkNewVat" runat="server" Text=" ถือเป็นการขาย" /></div>
                <div><asp:CheckBox ID="chkNewApproval" runat="server" Text=" ต้องอนุมัติ" Checked="true" /></div>
                <div><asp:CheckBox ID="chkNewEvidence" runat="server" Text=" ต้องมีหลักฐาน" /></div>
                <asp:Button ID="btnAdd" runat="server" Text="เพิ่ม" CssClass="btn btn-primary" OnClick="btnAdd_Click" />
            </div>
        </div>
    </div>
</asp:Content>
