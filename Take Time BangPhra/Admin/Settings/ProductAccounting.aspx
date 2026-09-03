<%@ Page Title="ตั้งค่าลงบัญชีรายสินค้า" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ProductAccounting.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Settings.ProductAccounting" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .pa-wrap { max-width: 980px; margin: 0 auto; padding: 18px 12px 60px; }
        .pa-head { background: linear-gradient(135deg,#00695c,#00897b); color:#fff; border-radius:14px;
                   padding:22px 26px; margin-bottom:18px; }
        .pa-head h2 { margin:0 0 6px; font-weight:700; font-size:1.5em; }
        .pa-head p { margin:0; opacity:.93; font-size:14px; }
        .pa-note { background:#e0f2f1; border-left:4px solid #00897b; padding:12px 15px; border-radius:8px;
                   font-size:13px; color:#004d40; margin-bottom:16px; line-height:1.6; }
        .pa-card { background:#fff; border-radius:11px; box-shadow:0 2px 10px rgba(0,0,0,.08);
                   padding:18px 20px; margin-bottom:16px; }
        .pa-msg { padding:11px 15px; border-radius:8px; margin-bottom:14px; font-size:14px; }
        .pa-ok { background:#e8f5e9; color:#1e7e42; } .pa-err { background:#ffebee; color:#c62828; }
        .pa-tools { display:flex; gap:8px; flex-wrap:wrap; align-items:center; margin-bottom:14px; }
        .pa-tools input[type=text] { padding:9px 12px; border:1.5px solid #dbe2e7; border-radius:8px; min-width:240px; }
        .pa-mini { font-size:12.5px; padding:6px 12px; border:1px solid #cfd8dc; background:#f7f9fa;
                   border-radius:8px; cursor:pointer; }
        table.pa-t { width:100%; border-collapse:collapse; font-size:13.5px; }
        table.pa-t th { background:#f5f7f9; text-align:left; padding:9px 10px; font-weight:650; color:#37474f;
                        border-bottom:2px solid #e3e9ed; position:sticky; top:0; }
        table.pa-t td { padding:8px 10px; border-bottom:1px solid #f0f3f5; }
        table.pa-t tr:hover td { background:#fafcfd; }
        .pa-name { font-weight:600; color:#263238; }
        .pa-sub { font-size:11.5px; color:#90a4ae; }
        .pa-chk { text-align:center; width:150px; }
        .pa-off td { background:#fff8f0 !important; }
        .pa-count { font-size:13px; color:#78909c; margin-left:auto; }
    </style>

    <div class="pa-wrap">
        <div class="pa-head">
            <h2><i class="fas fa-cash-register"></i> ตั้งค่าลงบัญชีรายสินค้า</h2>
            <p>เลือกได้รายสินค้า ว่าการขายจะถูกรวมเข้า <b>ใบสรุปรายได้รายวัน</b> หรือไม่</p>
        </div>

        <div class="pa-note">
            <b>✔ ติ๊ก = รวมใบสรุปรายวัน (ค่าเริ่มต้น — เหมือนเดิม)</b> ·
            <b>เว้นว่าง = ไม่รวม</b>: การขายสินค้านั้นจะไม่ถูกลงรายได้/ต้นทุนใน
            ใบรับเงินสดสรุปรายวันของขายหน้าร้าน และใบสรุปรายวันของรูมเซอร์วิส
            (แถวขายถูกทำเครื่องหมาย EXCLUDED ตรวจย้อนหลังได้)<br />
            ตัวอย่าง: <b>หมูกระทะ</b> ที่ตกลงให้รายได้ไปรวมกับค่าห้อง — ชาร์จเข้าห้องจะไปกับใบเสร็จ
            เช็คเอาท์อยู่แล้ว จึงปิดตัวนี้เพื่อไม่ให้เข้าใบสรุปรายวันซ้ำอีกทาง<br />
            <b>ไม่กระทบ:</b> ขายที่ออกใบกำกับในระบบ (ยังออกใบจริงรายใบ) · ชาร์จเข้าห้อง (ไปกับใบเสร็จ
            เช็คเอาท์) · การตัดจำนวนสต๊อก — สวิตช์นี้คุมเฉพาะการลงบัญชีในใบสรุปรายวันเท่านั้น
        </div>

        <asp:Panel ID="pnlMsg" runat="server" Visible="false">
            <div id="divMsg" runat="server" class="pa-msg pa-ok"><asp:Literal ID="litMsg" runat="server" /></div>
        </asp:Panel>

        <div class="pa-card">
            <div class="pa-tools">
                <asp:TextBox ID="txtSearch" runat="server" placeholder="ค้นหาชื่อสินค้า / บาร์โค้ด…" />
                <asp:Button ID="btnSearch" runat="server" Text="🔍 ค้นหา" CssClass="btn btn-default" OnClick="btnSearch_Click" />
                <button type="button" class="pa-mini" onclick="paSetAll(true); return false;">ติ๊กทั้งหมดที่แสดง</button>
                <button type="button" class="pa-mini" onclick="paSetAll(false); return false;">เอาออกทั้งหมดที่แสดง</button>
                <span class="pa-count"><asp:Literal ID="litCount" runat="server" /></span>
            </div>

            <div style="max-height:60vh; overflow-y:auto; border:1px solid #eef1f3; border-radius:8px;">
                <table class="pa-t">
                    <tr>
                        <th>สินค้า</th>
                        <th style="width:110px; text-align:right;">ราคาขาย</th>
                        <th class="pa-chk">รวมใบสรุปรายวัน</th>
                    </tr>
                    <asp:Literal ID="litRows" runat="server" />
                </table>
            </div>

            <div style="margin-top:14px; text-align:right;">
                <asp:Button ID="btnSave" runat="server" Text="💾 บันทึกการตั้งค่า" CssClass="btn btn-success btn-lg"
                    OnClick="btnSave_Click" />
            </div>
        </div>
    </div>

    <script>
        function paSetAll(on) {
            var boxes = document.querySelectorAll("input[name^='inc_']");
            for (var i = 0; i < boxes.length; i++) boxes[i].checked = on;
        }
    </script>
</asp:Content>
