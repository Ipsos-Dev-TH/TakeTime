<%@ Page Title="สัตว์เลี้ยงเข้าพัก" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="PetStay.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Settings.PetStaySettings" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .ps-wrap { max-width: 1000px; margin: 0 auto; padding: 12px 12px 60px; }
        .ps-head { background: linear-gradient(135deg,#6d4c41,#3e2723); color:#fff;
                   border-radius:14px; padding:20px 22px; margin-bottom:16px; }
        .ps-head h2 { margin:0 0 6px; font-size:21px; }
        .ps-head p { margin:0; opacity:.92; font-size:14px; line-height:1.7; }

        .ps-card { background:#fff; border-radius:14px; padding:18px 20px; margin-bottom:16px;
                   box-shadow:0 2px 10px rgba(0,0,0,.05); }
        .ps-card h3 { margin:0 0 4px; font-size:16.5px; color:#3e2723; }
        .ps-card .sub { color:#7b8a93; font-size:13px; margin-bottom:14px; line-height:1.65; }

        .ps-row { display:flex; gap:16px; padding:11px 0; border-bottom:1px solid #eff2f5; align-items:flex-start; }
        .ps-row:last-child { border-bottom:0; }
        .ps-lbl { flex:0 0 280px; }
        .ps-lbl b { display:block; font-size:14.5px; color:#2c3742; }
        .ps-lbl small { display:block; color:#8b959e; font-size:12.5px; line-height:1.6; margin-top:3px; }
        .ps-in { flex:1; min-width:0; }
        .ps-in select { width:100%; max-width:340px; padding:9px 11px; border:1px solid #dbe1e7; border-radius:9px; font-size:14px; }
        .ps-chk { display:flex; align-items:center; gap:9px; font-size:14px; }
        .ps-chk input { width:18px; height:18px; accent-color:#6d4c41; }
        .ps-card textarea { width:100%; min-height:240px; padding:10px 12px; border:1px solid #dbe1e7;
                            border-radius:9px; font-size:14px; line-height:1.7; font-family:inherit;
                            box-sizing:border-box; resize:vertical; }

        .ps-btn { padding:11px 20px; border:0; border-radius:10px; background:#6d4c41; color:#fff;
                  font-size:14.5px; font-weight:600; cursor:pointer; }
        .ps-btn:hover { background:#4e342e; }
        .ps-btn.ghost { background:#fff; color:#46545f; border:1.5px solid #dbe1e7; text-decoration:none; display:inline-block; }
        .ps-actions { display:flex; gap:10px; flex-wrap:wrap; align-items:center; }

        .ps-alert { padding:12px 15px; border-radius:10px; margin-bottom:14px; font-size:14px; line-height:1.7; }
        .ps-alert.ok { background:#e8f6ee; color:#16653e; }
        .ps-alert.err { background:#fdecec; color:#a12626; }
        .ps-alert.warn { background:#fff6e5; color:#8a5a00; }
        .ps-alert.info { background:#eef4fb; color:#1d4e79; }

        .ps-rooms { width:100%; border-collapse:collapse; font-size:14px; }
        .ps-rooms th { background:#f4f7f9; text-align:left; padding:10px; color:#46545f; font-weight:600; font-size:13px; }
        .ps-rooms td { padding:9px 10px; border-top:1px solid #eff2f5; vertical-align:middle; }
        .ps-rooms input[type=text] { width:110px; padding:8px 10px; border:1px solid #dbe1e7;
                                     border-radius:8px; font-size:14px; text-align:right; }
        .ps-rooms input[type=checkbox] { width:18px; height:18px; accent-color:#6d4c41; }

        .ps-prev { background:#faf8f7; border:1px dashed #d7ccc8; border-radius:10px; padding:12px 14px;
                   font-size:13.5px; line-height:1.75; color:#4e342e; margin-top:10px; }

        @media (max-width: 760px) {
            .ps-row { flex-direction:column; gap:7px; }
            .ps-lbl { flex:none; }
            .ps-rooms thead { display:none; }
            .ps-rooms tr { display:block; border-top:1px solid #eff2f5; padding:8px 0; }
            .ps-rooms td { display:flex; justify-content:space-between; align-items:center; border:0; padding:5px 2px; }
            .ps-rooms td:before { content:attr(data-th); color:#8b959e; font-size:12.5px; }
        }
    </style>

    <div class="ps-wrap">
        <div class="ps-head">
            <h2><i class="fas fa-paw"></i> สัตว์เลี้ยงเข้าพัก</h2>
            <p>
                เปิดแล้วหน้าจองจะมีช่อง <b>🐾 มีสัตว์เลี้ยงเข้าพัก</b> เฉพาะเมื่อเลือกห้องที่รับสัตว์เลี้ยง ·
                ลูกค้าจองเองต้องติ๊กยอมรับนโยบายสัตว์เลี้ยงก่อนยืนยัน (บันทึกเวลา + ฉบับ)<br />
                ค่าบริการถูกบวกเข้ายอดการจองเป็นรายการ <b>"ค่าบริการสัตว์เลี้ยง"</b> แยกจากค่าห้อง
                (อยู่ในยอดรวม ยอดค้างชำระ ใบเสร็จ และส่ง NextAcc ตามรายการค่าใช้จ่ายในห้อง)
            </p>
        </div>

        <asp:Literal ID="litMsg" runat="server" />
        <asp:Literal ID="litInfo" runat="server" />

        <div class="ps-card">
            <h3>ค่ากลาง</h3>
            <div class="ps-row">
                <div class="ps-lbl"><b>เปิดใช้ฟีเจอร์สัตว์เลี้ยงเข้าพัก</b>
                    <small>ปิด = หน้าจองไม่แสดงอะไรเกี่ยวกับสัตว์เลี้ยงเลย (ใบจองเดิมที่มีสัตว์เลี้ยงยังแสดงข้อมูลตามเดิม)</small></div>
                <div class="ps-in"><label class="ps-chk"><asp:CheckBox ID="chkEnabled" runat="server" /> เปิดใช้งาน</label></div>
            </div>
            <div class="ps-row">
                <div class="ps-lbl"><b>หน่วยคิดค่าบริการ</b>
                    <small>ต่อตัวต่อคืน = จำนวนตัว × ค่าบริการ × จำนวนคืน · ต่อการเข้าพัก = จำนวนตัว × ค่าบริการ (ไม่ขึ้นกับจำนวนคืน)</small></div>
                <div class="ps-in">
                    <asp:DropDownList ID="ddlFeeUnit" runat="server">
                        <asp:ListItem Value="NIGHT" Text="ต่อตัว ต่อคืน (ค่าเริ่มต้น)" />
                        <asp:ListItem Value="STAY" Text="ต่อตัว ต่อการเข้าพัก" />
                    </asp:DropDownList>
                </div>
            </div>
        </div>

        <div class="ps-card">
            <h3>ห้องที่รับสัตว์เลี้ยง</h3>
            <div class="sub">
                ติ๊ก "รับ" เฉพาะห้องที่อนุญาต · จำนวนสูงสุดต่อห้องต้องอย่างน้อย 1 ตัว · ค่าบริการต่อ 1 ตัว (บาท) ใส่ 0 = ไม่คิดเงิน<br />
                ห้องที่ไม่ติ๊ก ลูกค้าจะเห็นข้อความ "ห้องนี้ไม่รับสัตว์เลี้ยง" และระบุสัตว์เลี้ยงไม่ได้
            </div>
            <asp:PlaceHolder ID="phRooms" runat="server" />
        </div>

        <div class="ps-card">
            <h3>🐾 นโยบายการนำสัตว์เลี้ยงเข้าพัก (Pet Policy)</h3>
            <div class="sub">
                แสดงบนหน้าจองเมื่อลูกค้าติ๊กว่ามีสัตว์เลี้ยง (ต้องติ๊กยอมรับก่อนจอง) และบนหน้ายืนยันการจอง ·
                พิมพ์ข้อความธรรมดา ขึ้นบรรทัดได้ · ตัวหนาด้วย <code>**ข้อความ**</code> ·
                บันทึกแล้วข้อความเปลี่ยน = ขึ้นฉบับใหม่ (แยกเลขฉบับจากนโยบายการจองหลัก)
            </div>
            <asp:TextBox ID="txtPetPolicy" runat="server" TextMode="MultiLine" ValidateRequestMode="Disabled" />
        </div>

        <div class="ps-card">
            <div class="ps-actions">
                <asp:Button ID="btnSave" runat="server" CssClass="ps-btn" Text="💾 บันทึกทั้งหมด" OnClick="btnSave_Click" />
                <a class="ps-btn ghost" href="<%= ResolveUrl("~/Admin/Settings/BookingPolicies") %>">นโยบายการจองหลัก</a>
                <a class="ps-btn ghost" href="<%= ResolveUrl("~/Admin/Settings/Index") %>">กลับศูนย์ตั้งค่า</a>
            </div>
        </div>

        <div class="ps-card">
            <h3>ตัวอย่างนโยบายที่ลูกค้าเห็น (ฉบับที่บันทึกแล้ว)</h3>
            <asp:Literal ID="litPreview" runat="server" />
        </div>
    </div>
</asp:Content>
