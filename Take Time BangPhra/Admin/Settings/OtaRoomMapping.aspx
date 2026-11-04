<%@ Page Title="Mapping ห้อง OTA" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="OtaRoomMapping.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Settings.OtaRoomMapping" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .om-wrap { max-width: 1180px; margin: 0 auto; padding: 12px 12px 30px; }
        .om-head { background: linear-gradient(135deg,#5d4037,#3e2723); color:#fff; border-radius:14px; padding:20px 22px; margin-bottom:16px; }
        .om-head h2 { margin:0 0 6px; font-size:21px; }
        .om-head p { margin:0; opacity:.92; font-size:14px; line-height:1.7; }
        .om-card { background:#fff; border-radius:14px; padding:18px 20px; margin-bottom:16px; box-shadow:0 2px 10px rgba(0,0,0,.05); }
        .om-card h3 { margin:0 0 4px; font-size:16.5px; color:#3e2723; }
        .om-card .sub { color:#7b8a93; font-size:13px; margin-bottom:12px; line-height:1.65; }
        .om-btn { padding:10px 18px; border:0; border-radius:10px; background:#5d4037; color:#fff; font-size:14px; font-weight:600; cursor:pointer; }
        .om-btn:hover { background:#4e342e; }
        .om-btn.ghost { background:#fff; color:#5d4037; border:1.5px solid #d7ccc8; }
        .om-btn.sm { padding:6px 12px; font-size:13px; border-radius:8px; }
        .om-btn.danger { background:#fff; color:#b71c1c; border:1.5px solid #ef9a9a; }
        .om-alert { padding:12px 15px; border-radius:10px; margin-bottom:14px; font-size:14px; line-height:1.7; }
        .om-alert.ok { background:#e8f6ee; color:#16653e; }
        .om-alert.err { background:#fdecec; color:#a12626; }
        .om-alert.warn { background:#fff6e5; color:#8a5a00; }
        .om-alert.info { background:#eef4fb; color:#1d4e79; }
        .om-chip { display:inline-block; padding:2px 9px; border-radius:11px; font-size:12px; font-weight:700; }
        .om-chip.ok { background:#e8f5e9; color:#1e7e42; }
        .om-chip.warn { background:#fff3e0; color:#a35400; }
        .om-chip.err { background:#fdecec; color:#a12626; }
        .om-chip.off { background:#eceff1; color:#607d8b; }
        .om-scroll { overflow-x:auto; }
        .om-table { width:100%; border-collapse:collapse; font-size:13.5px; }
        .om-table th { text-align:left; background:#f5f0ee; color:#4e342e; padding:8px; font-weight:700; }
        .om-table td { padding:8px; border-top:1px solid #eee; vertical-align:top; }
        .om-rt { word-break:break-word; max-width:340px; }
        .om-room { display:inline-block; background:#efebe9; color:#4e342e; border-radius:6px; padding:2px 7px; margin:2px 2px 2px 0; font-size:12.5px; }
        .om-room.bad { background:#fdecec; color:#a12626; text-decoration:line-through; }
        .om-warn { color:#a35400; font-size:12.5px; margin-top:3px; }
        .om-note { color:#6b625c; font-size:12.5px; margin-top:3px; }
        .om-act { white-space:nowrap; text-align:right; }
        .om-empty { text-align:center; color:#90a4ae; padding:18px; }
        .om-field { margin-bottom:12px; }
        .om-field label.l { display:block; font-weight:700; color:#4e342e; margin-bottom:4px; font-size:14px; }
        .om-field .h { color:#7b8a93; font-size:12.5px; margin-top:3px; line-height:1.6; }
        .om-field input[type=text], .om-field textarea, .om-field input[type=date] { width:100%; max-width:100%; box-sizing:border-box; padding:9px 11px; border:1.5px solid #d7ccc8; border-radius:9px; font-size:14px; }
        .om-rooms { max-height:340px; overflow:auto; border:1.5px solid #eee; border-radius:9px; padding:8px 10px; }
        .om-rooms table td { padding:3px 2px; }
        .om-rooms label { font-size:13.5px; margin-left:4px; }
        .om-radio td { padding-right:16px; }
        .om-radio label { margin-left:4px; font-size:14px; }
        .om-row { display:flex; flex-wrap:wrap; gap:10px; align-items:flex-end; }
        .om-row > * { flex:1 1 220px; }
        @media (max-width: 720px) {
            .om-table thead { display:none; }
            .om-table tr { display:block; border-top:1px solid #eee; padding:6px 0; }
            .om-table td { display:block; border:0; padding:3px 4px; }
            .om-table td[data-l]:not([data-l=""]):before { content:attr(data-l); display:block; color:#90a4ae; font-size:11.5px; }
            .om-act { text-align:left; }
            .om-rt { max-width:none; }
        }
    </style>

    <div class="om-wrap">
        <div class="om-head">
            <h2><i class="fas fa-exchange-alt"></i> Mapping ห้อง OTA</h2>
            <p>ผูก "ชื่อห้องที่ OTA ส่งมา" (เช่น <i>Nordic Tent - RNB - Nordic Tent - Double - Room no breakfast</i>) กับห้องจริงที่จะจัดให้ และติ๊กว่าแผนนี้รวมอาหารเช้าไหม.
               ระบบ<b>ไม่เชื่อชื่อห้องจาก OTA</b> — ใช้หาแถวที่ตรงเท่านั้น ห้องที่ได้คือห้องที่ติ๊กไว้ที่นี่</p>
        </div>

        <asp:Literal ID="litAlert" runat="server" />
        <asp:Literal ID="litSchema" runat="server" />

        <div class="om-card">
            <h3>วิธีจับคู่</h3>
            <div class="sub"><asp:Literal ID="litStrict" runat="server" /></div>
            <asp:Button ID="btnStrict" runat="server" CssClass="om-btn ghost sm" OnClick="btnStrict_Click" />
            <div class="sub" style="margin-top:10px;"><asp:Literal ID="litPriority" runat="server" /></div>
        </div>

        <asp:Panel ID="pnlUnmapped" runat="server" CssClass="om-card" Visible="false">
            <h3>ชื่อห้องจาก OTA ที่ยังไม่ได้ map (30 วันล่าสุด)</h3>
            <div class="sub">อีเมลการจองที่ลงอัตโนมัติไม่ได้เพราะไม่มีแถวที่ตรง — กด "ตั้ง mapping" แล้วติ๊กห้อง ระบบลองอีเมลที่ค้างใหม่เองในรอบถัดไป</div>
            <div class="om-scroll"><asp:Literal ID="litUnmapped" runat="server" /></div>
        </asp:Panel>

        <div class="om-card" id="omForm">
            <h3><asp:Literal ID="litFormTitle" runat="server" Text="ตั้ง mapping ใหม่" /></h3>
            <div class="sub">ชื่อห้องต้องเป็น<b>ชื่อเต็มตามที่ OTA/STAAH ส่งมา</b> (รวมแผนราคา) — คนละแผน (มี/ไม่มีอาหารเช้า) = คนละแถว</div>
            <asp:HiddenField ID="hidOrig" runat="server" />
            <div class="om-row">
                <div class="om-field" style="flex:0 1 240px;">
                    <label class="l" for="<%= txtAgency.ClientID %>">ช่องทาง (Agency)</label>
                    <asp:TextBox ID="txtAgency" runat="server" MaxLength="100" list="omAgencies" placeholder="Agoda" />
                    <datalist id="omAgencies"><asp:Literal ID="litAgencies" runat="server" /></datalist>
                </div>
                <div class="om-field" style="flex:2 1 360px;">
                    <label class="l" for="<%= txtRoomType.ClientID %>">ชื่อห้องที่ OTA ส่งมา (ROOM_TYPE)</label>
                    <asp:TextBox ID="txtRoomType" runat="server" MaxLength="400" placeholder="Nordic Tent - RNB - Nordic Tent - Double - Room no breakfast" />
                </div>
            </div>
            <div class="om-field">
                <label class="l">ห้องที่จะจัดให้ (ติ๊กเฉพาะห้องที่ขายภายใต้แผนนี้จริง)</label>
                <div class="om-rooms"><asp:CheckBoxList ID="cblRooms" runat="server" RepeatLayout="Table" RepeatColumns="1" /></div>
                <div class="h">แสดง ชื่อห้อง · ประเภท [#เลขห้อง] — ติ๊กห้องต่างประเภทในแผนเดียวกัน ระบบจะเตือน</div>
            </div>
            <div class="om-field">
                <label class="l">อาหารเช้า</label>
                <asp:RadioButtonList ID="rblBreakfast" runat="server" RepeatDirection="Horizontal" CssClass="om-radio">
                    <asp:ListItem Value="EXCLUDED">ไม่รวมอาหารเช้า</asp:ListItem>
                    <asp:ListItem Value="INCLUDED">รวมอาหารเช้า</asp:ListItem>
                    <asp:ListItem Value="" Selected="True">ไม่ระบุ</asp:ListItem>
                </asp:RadioButtonList>
                <div class="h">ขึ้นเป็นป้ายในหมายเหตุการจอง ("ไม่รวมอาหารเช้า" ส้ม / "รวมอาหารเช้า" เขียว) — ระบบไม่เดาจากชื่อแผนของ OTA</div>
            </div>
            <div class="om-field">
                <label class="l" for="<%= txtNote.ClientID %>">หมายเหตุอื่น ๆ (ไม่บังคับ)</label>
                <asp:TextBox ID="txtNote" runat="server" MaxLength="400" placeholder="เช่น ห้ามสัตว์เลี้ยง / เตียงเสริมรวมแล้ว (ไม่ต้องพิมพ์เรื่องอาหารเช้า)" />
            </div>
            <asp:Button ID="btnSave" runat="server" Text="บันทึก mapping" CssClass="om-btn" OnClick="btnSave_Click" />
            <asp:Button ID="btnCancel" runat="server" Text="ล้างฟอร์ม" CssClass="om-btn ghost" OnClick="btnCancel_Click" CausesValidation="false" />
        </div>

        <div class="om-card">
            <h3>Mapping ทั้งหมด</h3>
            <div class="sub"><asp:Literal ID="litGroupSummary" runat="server" /></div>
            <div class="om-row" style="margin-bottom:10px;">
                <asp:TextBox ID="txtFilter" runat="server" placeholder="ค้นหา ช่องทาง / ชื่อห้อง / ห้องที่ผูก" />
                <div style="flex:0 1 auto;"><asp:CheckBox ID="chkOnlyWarn" runat="server" Text=" เฉพาะที่ต้องตรวจ" /></div>
                <div style="flex:0 1 auto;">
                    <asp:Button ID="btnFilter" runat="server" Text="กรอง" CssClass="om-btn ghost sm" OnClick="btnFilter_Click" />
                    <asp:Button ID="btnNew" runat="server" Text="+ เพิ่ม mapping" CssClass="om-btn sm" OnClick="btnNew_Click" />
                </div>
            </div>
            <div class="om-scroll">
                <table class="om-table">
                    <thead><tr><th>ช่องทาง</th><th>ชื่อห้องจาก OTA</th><th>อาหารเช้า</th><th>ห้องที่จัดให้</th><th></th></tr></thead>
                    <tbody><asp:Literal ID="litGroups" runat="server" /></tbody>
                </table>
            </div>
        </div>

        <div class="om-card" id="omTest">
            <h3>ทดสอบการจับคู่</h3>
            <div class="sub">วางชื่อห้องจากอีเมล OTA แล้วดูว่าระบบจะจัดห้องไหน (ไม่บันทึกอะไร)</div>
            <div class="om-row">
                <div class="om-field" style="flex:0 1 200px;"><label class="l">ช่องทาง</label><asp:TextBox ID="txtTestAgency" runat="server" list="omAgencies" placeholder="Agoda" /></div>
                <div class="om-field" style="flex:2 1 340px;"><label class="l">ชื่อห้องจากอีเมล</label><asp:TextBox ID="txtTestRoomType" runat="server" /></div>
                <div class="om-field" style="flex:0 1 170px;"><label class="l">วันเข้าพัก</label><asp:TextBox ID="txtTestIn" runat="server" TextMode="Date" /></div>
            </div>
            <asp:Button ID="btnTest" runat="server" Text="ทดสอบ" CssClass="om-btn ghost" OnClick="btnTest_Click" />
            <div style="margin-top:10px;"><asp:Literal ID="litTest" runat="server" /></div>
        </div>

        <asp:HiddenField ID="hidAct" runat="server" />
        <asp:HiddenField ID="hidKey" runat="server" />
        <asp:Button ID="btnAct" runat="server" OnClick="btnAct_Click" style="display:none" />
    </div>

    <script>
        function omAct(act, key, msg) {
            if (msg && !confirm(msg)) return;
            document.getElementById('<%= hidAct.ClientID %>').value = act;
            document.getElementById('<%= hidKey.ClientID %>').value = key;
            document.getElementById('<%= btnAct.ClientID %>').click();
        }
    </script>
</asp:Content>
