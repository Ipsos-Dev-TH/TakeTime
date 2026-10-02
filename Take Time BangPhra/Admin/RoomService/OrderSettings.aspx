<%@ Page Title="ตั้งค่าเปิด-ปิดระบบสั่งของ" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeFile="OrderSettings.aspx.cs" Inherits="Take_Time_BangPhra.Admin.RoomService.OrderSettings" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .rs-settings { max-width: 720px; margin: 0 auto; padding: 20px; }
        .rs-settings h2 { color: #2c3e50; margin-bottom: 6px; }
        .rs-sub { color: #7f8c8d; margin-bottom: 22px; }
        .rs-card { background: #fff; border: 1px solid #e6e8eb; border-radius: 14px; padding: 22px 24px; box-shadow: 0 2px 10px rgba(0,0,0,.05); }
        .rs-row { margin-bottom: 20px; }
        .rs-row label.rs-label { display: block; font-weight: 600; color: #34495e; margin-bottom: 8px; }
        .rs-input, .rs-select, .rs-textarea {
            width: 100%; padding: 10px 12px; border: 1px solid #d6dadf; border-radius: 8px; font-size: 15px; box-sizing: border-box;
        }
        .rs-textarea { min-height: 80px; resize: vertical; }
        .rs-inline { display: flex; gap: 16px; }
        .rs-inline > div { flex: 1; }
        .rs-status { display: inline-block; padding: 6px 14px; border-radius: 20px; font-weight: 700; font-size: 14px; }
        .rs-open { background: #d4edda; color: #155724; }
        .rs-closed { background: #f8d7da; color: #721c24; }
        .rs-check { display: flex; align-items: center; gap: 10px; }
        .rs-check input { width: 20px; height: 20px; }
        .rs-hint { color: #95a5a6; font-size: 13px; margin-top: 6px; }
        .btn-save {
            background: linear-gradient(135deg,#27ae60,#1e8449); color: #fff; border: none; border-radius: 8px;
            padding: 12px 28px; font-size: 16px; font-weight: 700; cursor: pointer;
        }
        .btn-save:hover { filter: brightness(1.05); }
        .rs-saved { color: #1e8449; font-weight: 700; margin-left: 14px; }
        .rs-modehint { background:#f4f6f8; border-radius:8px; padding:10px 12px; font-size:13px; color:#5a6b7b; margin-top:8px; }
    </style>

    <div class="rs-settings">
        <h2><i class="fas fa-store"></i> ตั้งค่าเปิด-ปิด ระบบให้ลูกค้าสั่งของ</h2>
        <div class="rs-sub">ควบคุมเวลาเปิด-ปิดรับออเดอร์ Room Service (ตั้งเวลาอัตโนมัติ หรือบังคับเปิด/ปิดด้วยมือ)</div>

        <div class="rs-card">
            <div class="rs-row">
                สถานะตอนนี้:
                <asp:Label ID="lblCurrentStatus" runat="server" CssClass="rs-status rs-open" Text="เปิดรับออเดอร์"></asp:Label>
            </div>

            <div class="rs-row">
                <div class="rs-check">
                    <asp:CheckBox ID="chkEnabled" runat="server" />
                    <label class="rs-label" style="margin:0;">เปิดใช้งานระบบสั่งของ</label>
                </div>
                <div class="rs-hint">ถ้าปิด ลูกค้าจะสั่งของไม่ได้เลยทั้งระบบ</div>
            </div>

            <div class="rs-row">
                <label class="rs-label">โหมดควบคุม</label>
                <asp:DropDownList ID="ddlMode" runat="server" CssClass="rs-select">
                    <asp:ListItem Value="AUTO" Text="อัตโนมัติตามเวลา (เปิด-ปิดตามเวลาที่ตั้ง)"></asp:ListItem>
                    <asp:ListItem Value="OPEN" Text="บังคับเปิด (เปิดตลอด ไม่สนเวลา)"></asp:ListItem>
                    <asp:ListItem Value="CLOSED" Text="บังคับปิด (ปิดทันที ไม่สนเวลา)"></asp:ListItem>
                </asp:DropDownList>
                <div class="rs-modehint">
                    💡 <b>บังคับเปิด/ปิด</b> จะมีผลทันทีและสำคัญกว่าเวลาที่ตั้งไว้ — ใช้เวลาด้านล่างเฉพาะโหมด "อัตโนมัติตามเวลา"
                </div>
            </div>

            <div class="rs-row rs-inline">
                <div>
                    <label class="rs-label">เวลาเปิด</label>
                    <asp:TextBox ID="txtOpenTime" runat="server" CssClass="rs-input" TextMode="Time"></asp:TextBox>
                </div>
                <div>
                    <label class="rs-label">เวลาปิด</label>
                    <asp:TextBox ID="txtCloseTime" runat="server" CssClass="rs-input" TextMode="Time"></asp:TextBox>
                </div>
            </div>
            <div class="rs-hint" style="margin-top:-10px;">ตั้งเวลาปิดน้อยกว่าเวลาเปิดได้ (เช่น 18:00–02:00) ระบบรองรับช่วงข้ามเที่ยงคืน</div>

            <div class="rs-row" style="margin-top:20px;">
                <label class="rs-label">ข้อความแจ้งลูกค้าตอนปิด</label>
                <asp:TextBox ID="txtClosedMessage" runat="server" CssClass="rs-textarea" TextMode="MultiLine"></asp:TextBox>
            </div>

            <hr style="border:none; border-top:1px solid #e6eaee; margin:26px 0 20px;" />

            <div class="rs-row">
                <label class="rs-label">💰 ค่าบริการ (Service Charge)</label>
                <asp:DropDownList ID="ddlServiceChargeMode" runat="server" CssClass="rs-input">
                    <asp:ListItem Value="NONE" Text="ไม่คิดค่าบริการ" />
                    <asp:ListItem Value="PERCENT" Text="คิดเป็น % ของยอดสินค้า" />
                    <asp:ListItem Value="PER_ITEM" Text="คิดเป็นบาท ต่อชิ้น" />
                    <asp:ListItem Value="PER_ORDER" Text="คิดเป็นบาท ต่อครั้ง (ต่อออเดอร์)" />
                </asp:DropDownList>
            </div>

            <div class="rs-row rs-inline">
                <div>
                    <label class="rs-label">จำนวน (% หรือ บาท ตามโหมด)</label>
                    <asp:TextBox ID="txtServiceChargeValue" runat="server" CssClass="rs-input"
                        TextMode="Number" step="0.01" min="0" placeholder="เช่น 10"></asp:TextBox>
                </div>
                <div>
                    <label class="rs-label">เพดานสูงสุด (บาท) — 0 = ไม่จำกัด</label>
                    <asp:TextBox ID="txtServiceChargeMax" runat="server" CssClass="rs-input"
                        TextMode="Number" step="0.01" min="0" placeholder="0"></asp:TextBox>
                </div>
            </div>

            <div class="rs-row">
                <label class="rs-label">ชื่อที่แสดงให้ลูกค้า</label>
                <asp:TextBox ID="txtServiceChargeLabel" runat="server" CssClass="rs-input"
                    placeholder="ค่าบริการ"></asp:TextBox>
            </div>

            <div class="rs-hint" style="margin-top:-6px;">
                ตัวอย่าง: <b>%</b> → ตั้ง 10 แล้วสั่ง ฿500 บวก ฿50 ·
                <b>ต่อชิ้น</b> → ตั้ง 5 แล้วสั่ง 3 ชิ้น บวก ฿15 ·
                <b>ต่อครั้ง</b> → ตั้ง 20 บวก ฿20 ไม่ว่าสั่งกี่ชิ้น<br />
                ค่าบริการจะโชว์แยกบรรทัดในตะกร้าของลูกค้า และรวมอยู่ในยอดที่ลงบิลห้อง/ใบเสร็จอัตโนมัติ ·
                <b>ยอดสั่งขั้นต่ำนับเฉพาะค่าสินค้า</b> ไม่รวมค่าบริการ
            </div>

            <div class="rs-row" style="margin-bottom:0;">
                <asp:Button ID="btnSave" runat="server" CssClass="btn-save" Text="💾 บันทึกการตั้งค่า" OnClick="btnSave_Click" />
                <asp:Label ID="lblSaved" runat="server" CssClass="rs-saved" Text="" Visible="false"></asp:Label>
            </div>
        </div>
    </div>
</asp:Content>
