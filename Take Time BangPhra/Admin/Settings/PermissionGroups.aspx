<%@ Page Title="กลุ่มสิทธิ์" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="PermissionGroups.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Settings.PermissionGroups" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .pg-wrap { max-width: 1180px; margin: 0 auto; padding: 18px 12px 60px; }
        .pg-head { background: linear-gradient(135deg,#37474f,#546e7a); color:#fff; border-radius:14px;
                   padding:22px 26px; margin-bottom:18px; }
        .pg-head h2 { margin:0 0 6px; font-weight:700; font-size:1.5em; }
        .pg-head p { margin:0; opacity:.92; font-size:14px; }
        .pg-card { background:#fff; border-radius:11px; box-shadow:0 2px 10px rgba(0,0,0,.08);
                   padding:20px; margin-bottom:18px; }
        .pg-card h3 { margin:0 0 14px; font-size:1.05em; color:#37474f; font-weight:700; }
        .pg-msg { padding:11px 15px; border-radius:8px; margin-bottom:14px; font-size:14px; }
        .pg-ok { background:#e8f5e9; color:#1e7e42; }
        .pg-err { background:#ffebee; color:#c62828; }

        .pg-groups { display:grid; grid-template-columns:repeat(auto-fill,minmax(240px,1fr)); gap:12px; }
        .pg-g { border:1.5px solid #e3e9ed; border-radius:10px; padding:13px 15px; cursor:pointer;
                text-decoration:none; color:inherit; display:block; transition:all .12s; }
        .pg-g:hover { border-color:#546e7a; text-decoration:none; color:inherit; box-shadow:0 3px 10px rgba(0,0,0,.08); }
        .pg-g.active { border-color:#546e7a; background:#eef4f8; }
        .pg-g .n { font-weight:650; color:#263238; margin-bottom:3px; }
        .pg-g .d { font-size:12.5px; color:#78909c; line-height:1.45; }
        .pg-g .m { font-size:11.5px; color:#90a4ae; margin-top:6px; }
        .pg-sys { font-size:10.5px; background:#fff8e1; color:#f57f17; padding:2px 7px; border-radius:10px; }

        table.pg-matrix { width:100%; border-collapse:collapse; font-size:13.5px; }
        table.pg-matrix th { background:#f5f7f9; text-align:left; padding:9px 10px; font-weight:650;
                             color:#37474f; border-bottom:2px solid #e3e9ed; }
        table.pg-matrix td { padding:8px 10px; border-bottom:1px solid #f0f3f5; vertical-align:top; }
        table.pg-matrix tr.cat td { background:#fafbfc; font-weight:700; color:#546e7a; font-size:12.5px; }
        table.pg-matrix .chk { text-align:center; width:92px; }
        .pg-mod-name { font-weight:600; color:#263238; }
        .pg-mod-note { font-size:12px; color:#90a4ae; margin-top:2px; }

        .pg-field { margin-bottom:12px; }
        .pg-field label { display:block; font-weight:600; font-size:13.5px; margin-bottom:5px; color:#37474f; }
        .pg-field input[type=text], .pg-field select { width:100%; max-width:420px; padding:9px 12px;
            border:1.5px solid #dbe2e7; border-radius:8px; font-size:14px; }
        .pg-members { display:grid; grid-template-columns:repeat(auto-fill,minmax(230px,1fr)); gap:8px; }
        .pg-mem { border:1px solid #e3e9ed; border-radius:8px; padding:9px 11px; font-size:13.5px; }
        .pg-mem small { color:#90a4ae; display:block; font-size:11.5px; }
        .pg-note { background:#eef4f8; border-left:4px solid #546e7a; padding:12px 15px; border-radius:8px;
                   font-size:13px; color:#37474f; margin-bottom:16px; }
    </style>

    <div class="pg-wrap">
        <div class="pg-head">
            <h2><i class="fas fa-users-gear"></i> กลุ่มสิทธิ์</h2>
            <p>สร้างกลุ่มเอง แล้วกำหนดว่าแต่ละกลุ่ม <b>มองเห็น</b> และ <b>เข้าใช้งาน</b> ส่วนไหนได้บ้าง</p>
        </div>

        <asp:Panel ID="pnlMsg" runat="server" Visible="false">
            <div id="divMsg" runat="server" class="pg-msg pg-ok"><asp:Literal ID="litMsg" runat="server" /></div>
        </asp:Panel>

        <div class="pg-note">
            <b>พนักงานที่ยังไม่ได้กำหนดกลุ่ม จะใช้สิทธิ์ตามตำแหน่งเดิม (Owner / Admin / Staff) เหมือนก่อนทุกประการ</b> —
            ระบบจะเริ่มใช้กลุ่มก็ต่อเมื่อคุณกำหนดกลุ่มให้คนนั้นแล้ว<br />
            ผู้ใช้ตำแหน่ง <b>Owner เข้าถึงได้ทุกส่วนเสมอ</b> ไม่ว่ากลุ่มจะตั้งไว้อย่างไร (กันตั้งค่าพลาดแล้วเข้าหน้าตั้งค่าไม่ได้)
        </div>

        <div class="pg-card">
            <h3><i class="fas fa-layer-group"></i> เลือกกลุ่มที่ต้องการแก้ไข</h3>
            <div class="pg-groups"><asp:Literal ID="litGroups" runat="server" /></div>

            <div style="margin-top:16px; padding-top:14px; border-top:1px dashed #e0e6ea;">
                <asp:TextBox ID="txtNewGroup" runat="server" placeholder="ชื่อกลุ่มใหม่ เช่น แม่บ้าน, บัญชี"
                    style="padding:9px 12px; border:1.5px solid #dbe2e7; border-radius:8px; min-width:260px;" />
                <asp:DropDownList ID="ddlNewBaseRole" runat="server"
                    style="padding:9px 12px; border:1.5px solid #dbe2e7; border-radius:8px; margin-left:6px;">
                    <asp:ListItem Value="Staff" Text="ฐานตำแหน่ง: Staff" />
                    <asp:ListItem Value="Admin" Text="ฐานตำแหน่ง: Admin" />
                </asp:DropDownList>
                <asp:Button ID="btnAddGroup" runat="server" Text="➕ สร้างกลุ่ม" CssClass="btn btn-primary"
                    OnClick="btnAddGroup_Click" />
                <div style="font-size:12.5px; color:#90a4ae; margin-top:7px;">
                    "ฐานตำแหน่ง" ใช้กับหน้าเก่าที่ยังเช็คตำแหน่งโดยตรง — เลือก Admin ถ้ากลุ่มนี้ควรทำงานระดับผู้ดูแลได้
                </div>
            </div>
        </div>

        <asp:Panel ID="pnlEdit" runat="server" Visible="false">
            <div class="pg-card">
                <h3><i class="fas fa-sliders"></i> สิทธิ์ของกลุ่ม: <asp:Literal ID="litGroupName" runat="server" /></h3>

                <div class="pg-field">
                    <label>ชื่อกลุ่ม</label>
                    <asp:TextBox ID="txtGroupName" runat="server" />
                </div>
                <div class="pg-field">
                    <label>คำอธิบาย</label>
                    <asp:TextBox ID="txtGroupDesc" runat="server" />
                </div>

                <table class="pg-matrix">
                    <tr>
                        <th>ส่วนงาน</th>
                        <th class="chk">👁 มองเห็น</th>
                        <th class="chk">🔓 เข้าใช้งาน</th>
                    </tr>
                    <asp:Literal ID="litMatrix" runat="server" />
                </table>

                <div style="margin-top:16px; text-align:right;">
                    <asp:Button ID="btnDeleteGroup" runat="server" Text="🗑 ลบกลุ่มนี้" CssClass="btn btn-danger"
                        OnClick="btnDeleteGroup_Click"
                        OnClientClick="return confirm('ลบกลุ่มนี้? พนักงานในกลุ่มจะกลับไปใช้สิทธิ์ตามตำแหน่งเดิม');" />
                    <asp:Button ID="btnSavePerm" runat="server" Text="💾 บันทึกสิทธิ์" CssClass="btn btn-success btn-lg"
                        OnClick="btnSavePerm_Click" />
                </div>
            </div>

            <div class="pg-card">
                <h3><i class="fas fa-user-group"></i> พนักงานในกลุ่มนี้</h3>
                <div class="pg-members"><asp:Literal ID="litMembers" runat="server" /></div>

                <div style="margin-top:16px; padding-top:14px; border-top:1px dashed #e0e6ea;">
                    <label style="font-weight:600; font-size:13.5px; display:block; margin-bottom:6px;">เพิ่มพนักงานเข้ากลุ่ม</label>
                    <asp:DropDownList ID="ddlAddMember" runat="server"
                        style="padding:9px 12px; border:1.5px solid #dbe2e7; border-radius:8px; min-width:300px;" />
                    <asp:Button ID="btnAddMember" runat="server" Text="➕ เพิ่ม" CssClass="btn btn-primary"
                        OnClick="btnAddMember_Click" />
                </div>
            </div>
        </asp:Panel>
    </div>
</asp:Content>
