<%@ Page Title="นโยบายการจอง" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="BookingPolicies.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Settings.BookingPolicies" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .bp-wrap { max-width: 1000px; margin: 0 auto; padding: 12px 12px 60px; }
        .bp-head { background: linear-gradient(135deg,#5d4037,#3e2723); color:#fff;
                   border-radius:14px; padding:20px 22px; margin-bottom:16px; }
        .bp-head h2 { margin:0 0 6px; font-size:21px; }
        .bp-head p { margin:0; opacity:.92; font-size:14px; line-height:1.7; }

        .bp-card { background:#fff; border-radius:14px; padding:18px 20px; margin-bottom:16px;
                   box-shadow:0 2px 10px rgba(0,0,0,.05); }
        .bp-card h3 { margin:0 0 4px; font-size:16.5px; color:#3e2723; }
        .bp-card .sub { color:#7b8a93; font-size:13px; margin-bottom:12px; line-height:1.65; }
        .bp-card textarea { width:100%; min-height:220px; padding:10px 12px; border:1px solid #dbe1e7;
                            border-radius:9px; font-size:14px; line-height:1.7; font-family:inherit;
                            box-sizing:border-box; resize:vertical; }
        .bp-card textarea:focus { outline:0; border-color:#5d4037; box-shadow:0 0 0 3px rgba(93,64,55,.12); }

        .bp-btn { padding:11px 20px; border:0; border-radius:10px; background:#5d4037; color:#fff;
                  font-size:14.5px; font-weight:600; cursor:pointer; }
        .bp-btn:hover { background:#4e342e; }
        .bp-btn.ghost { background:#fff; color:#46545f; border:1.5px solid #dbe1e7; text-decoration:none;
                        display:inline-block; }
        .bp-actions { display:flex; gap:10px; flex-wrap:wrap; align-items:center; }

        .bp-alert { padding:12px 15px; border-radius:10px; margin-bottom:14px; font-size:14px; line-height:1.7; }
        .bp-alert.ok { background:#e8f6ee; color:#16653e; }
        .bp-alert.err { background:#fdecec; color:#a12626; }
        .bp-alert.info { background:#eef4fb; color:#1d4e79; }
        .bp-alert.warn { background:#fff6e5; color:#8a5a00; }

        .bp-prev { background:#faf8f7; border:1px dashed #d7ccc8; border-radius:10px; padding:12px 14px;
                   font-size:13.5px; line-height:1.75; color:#4e342e; margin-top:10px; }
        .bp-prev summary { cursor:pointer; font-weight:600; color:#6d4c41; }
    </style>

    <div class="bp-wrap">
        <div class="bp-head">
            <h2><i class="fas fa-file-contract"></i> นโยบายการจอง</h2>
            <p>
                ข้อความที่ลูกค้าเห็นบน<b>หน้าจองห้องพัก</b> (ต้องติ๊กยอมรับก่อนกดยืนยัน) และบน<b>หน้ายืนยันการจอง</b><br />
                นโยบายการยกเลิกแสดงคู่กับทุกช่องทางการชำระเงิน · บันทึกแล้วข้อความเปลี่ยน = ขึ้นฉบับใหม่อัตโนมัติ
                (ใบจองเก็บเลขฉบับที่ลูกค้ายอมรับไว้)
            </p>
        </div>

        <asp:Literal ID="litMsg" runat="server" />

        <div class="bp-card">
            <h3>สถานะ</h3>
            <asp:Literal ID="litInfo" runat="server" />
            <div class="sub" style="margin:8px 0 0;">
                พิมพ์เป็นข้อความธรรมดา ขึ้นบรรทัดได้ตามปกติ · ทำตัวหนาด้วย <code>**ข้อความ**</code>
                · ระบบไม่รับแท็ก HTML (แสดงเป็นตัวอักษรตามที่พิมพ์ เพื่อความปลอดภัย)
            </div>
        </div>

        <div class="bp-card">
            <h3>📜 ข้อกำหนดและเงื่อนไข (Terms &amp; Conditions)</h3>
            <div class="sub">เงื่อนไขการจอง การเข้าพัก เวลาเช็คอิน/เอาท์ กฎระเบียบ</div>
            <asp:TextBox ID="txtTerms" runat="server" TextMode="MultiLine" ValidateRequestMode="Disabled" />
        </div>

        <div class="bp-card">
            <h3>🔒 นโยบายความเป็นส่วนตัว (Privacy Policy)</h3>
            <div class="sub">ข้อมูลส่วนบุคคลที่เก็บ วัตถุประสงค์ ระยะเวลา และช่องทางติดต่อ (PDPA)</div>
            <asp:TextBox ID="txtPrivacy" runat="server" TextMode="MultiLine" ValidateRequestMode="Disabled" />
        </div>

        <div class="bp-card">
            <h3>💸 นโยบายการคืนเงิน (Refund Policy)</h3>
            <div class="sub">ช่องทางและระยะเวลาคืนเงิน แยกตามวิธีชำระ (โอน / บัตร / ออนไลน์)</div>
            <asp:TextBox ID="txtRefund" runat="server" TextMode="MultiLine" ValidateRequestMode="Disabled" />
        </div>

        <div class="bp-card">
            <h3>📅 นโยบายการยกเลิกการจอง (Cancellation Policy — นโยบายหลัก)</h3>
            <div class="sub">แสดงคู่กับ<b>ทุกช่องทางการชำระเงิน</b>บนหน้าจอง และบนหน้ายืนยันการจอง</div>
            <asp:TextBox ID="txtCancel" runat="server" TextMode="MultiLine" ValidateRequestMode="Disabled" />
        </div>

        <div class="bp-card">
            <div class="bp-actions">
                <asp:Button ID="btnSave" runat="server" CssClass="bp-btn" Text="💾 บันทึกนโยบาย" OnClick="btnSave_Click" />
                <a class="bp-btn ghost" href="<%= ResolveUrl("~/Admin/Settings/Index") %>">กลับศูนย์ตั้งค่า</a>
            </div>
        </div>

        <div class="bp-card">
            <h3>ตัวอย่างที่ลูกค้าเห็น (ฉบับที่บันทึกแล้ว)</h3>
            <asp:Literal ID="litPreview" runat="server" />
        </div>
    </div>
</asp:Content>
