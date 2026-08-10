<%@ Page Title="บัญชี LINE" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="LineAccount.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Settings.LineAccount" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .la-wrap { max-width: 1000px; margin: 0 auto; padding: 18px 12px 50px; }
        .la-head { background: linear-gradient(135deg, #06C755, #04a144); color: #fff;
                   border-radius: 12px; padding: 22px 26px; margin-bottom: 20px; }
        .la-head h2 { margin: 0 0 6px; font-weight: 700; }
        .la-head p { margin: 0; opacity: .92; font-size: 14px; }
        .la-card { background: #fff; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,.08);
                   padding: 20px; margin-bottom: 18px; }
        .la-card h3 { margin: 0 0 14px; font-size: 1.12em; color: #1b5e3a; font-weight: 700; }
        .me { display: flex; align-items: center; gap: 16px; flex-wrap: wrap; }
        .avatar { width: 66px; height: 66px; border-radius: 50%; background: #e8f6ed;
                  display: flex; align-items: center; justify-content: center; font-size: 26px; color: #06C755; }
        .avatar img { width: 66px; height: 66px; border-radius: 50%; object-fit: cover; }
        .pill { display: inline-block; padding: 3px 11px; border-radius: 12px; font-size: 12px; font-weight: 700; color: #fff; }
        .p-on { background: #06C755; } .p-off { background: #95a5a6; }
        .form-row { margin-bottom: 12px; }
        .form-row label { display: block; font-weight: 600; margin-bottom: 5px; font-size: 13.5px; }
        .grid2 { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 14px; }
        .tbl { width: 100%; border-collapse: collapse; font-size: 13.5px; }
        .tbl th { background: #f1f8f4; padding: 10px; text-align: left; border-bottom: 2px solid #d9ebe0; }
        .tbl td { padding: 10px; border-bottom: 1px solid #eef4f0; vertical-align: middle; }
        .hint { background: #fffaf0; border-left: 4px solid #f0ad4e; padding: 12px 14px;
                border-radius: 8px; font-size: 13px; color: #7a6027; margin-bottom: 16px; }
    </style>

    <div class="la-wrap">
        <div class="la-head">
            <h2><i class="fab fa-line"></i> บัญชี LINE ของผู้ใช้</h2>
            <p>ผูกบัญชี LINE ส่วนตัวเพื่อรับแจ้งเตือนจากระบบเข้าไลน์โดยตรง (ไม่ต้องผ่านกลุ่ม)</p>
        </div>

        <asp:Panel ID="pnlMsg" runat="server" Visible="false" CssClass="la-card" style="padding:14px 18px;">
            <asp:Literal ID="litMsg" runat="server" />
        </asp:Panel>

        <!-- บัญชีของฉัน -->
        <div class="la-card">
            <h3><i class="fas fa-user"></i> บัญชีของฉัน</h3>
            <asp:Panel ID="pnlNotConfigured" runat="server" Visible="false" CssClass="hint">
                <b>ยังตั้งค่า LINE Login ไม่ครบ</b> — เจ้าของระบบต้องกรอก Channel ID / Secret / Callback URL
                ในส่วน "ตั้งค่า LINE Login" ด้านล่างก่อน จึงจะผูกบัญชีได้
            </asp:Panel>

            <div class="me">
                <div class="avatar"><asp:Literal ID="litAvatar" runat="server" /></div>
                <div style="flex:1; min-width:220px;">
                    <div style="font-size:16px; font-weight:600;"><asp:Literal ID="litMyName" runat="server" /></div>
                    <div style="font-size:13px; color:#7a8a80; margin-top:3px;"><asp:Literal ID="litMyStatus" runat="server" /></div>
                </div>
                <div>
                    <asp:Button ID="btnLink" runat="server" Text="🔗 ผูกบัญชี LINE"
                        CssClass="btn btn-success btn-lg" OnClick="btnLink_Click" />
                    <asp:Button ID="btnTestMe" runat="server" Text="📨 ทดสอบส่งหาฉัน"
                        CssClass="btn btn-default btn-lg" OnClick="btnTestMe_Click" Visible="false" />
                    <asp:Button ID="btnUnlink" runat="server" Text="ยกเลิกการผูก"
                        CssClass="btn btn-danger btn-lg" OnClick="btnUnlink_Click" Visible="false"
                        OnClientClick="return confirm('ยกเลิกการผูกบัญชี LINE?\nจะไม่ได้รับแจ้งเตือนส่วนตัวอีก');" />
                </div>
            </div>

            <div style="margin-top:14px; padding-top:14px; border-top:1px dashed #e3ece7;">
                <label style="font-weight:600;">
                    <asp:CheckBox ID="chkNotify" runat="server" AutoPostBack="true" OnCheckedChanged="chkNotify_Changed" />
                    รับแจ้งเตือนจากระบบทาง LINE
                </label>
            </div>
        </div>

        <!-- คำขอผูกบัญชีรออนุมัติ (เฉพาะ Owner) -->
        <asp:Panel ID="pnlRequests" runat="server" CssClass="la-card" Visible="false">
            <h3><i class="fas fa-user-clock"></i> คำขอผูกบัญชี LINE รออนุมัติ
                <asp:Literal ID="litReqCount" runat="server" /></h3>
            <div class="hint">
                พนักงานที่จำรหัสผ่านไม่ได้ จะเลือกชื่อตัวเองแล้วส่งคำขอมาที่นี่ —
                <b>กรุณาตรวจสอบให้แน่ใจว่าเป็นคนคนเดียวกันจริงก่อนอนุมัติ</b>
                (อนุมัติผิดคน = คนนั้นจะเข้าระบบในชื่อผู้ใช้นั้นได้)
            </div>
            <div style="overflow-x:auto;">
                <asp:GridView ID="gvRequests" runat="server" AutoGenerateColumns="false" CssClass="tbl"
                    GridLines="None" DataKeyNames="ID" OnRowCommand="gvRequests_RowCommand"
                    EmptyDataText="ไม่มีคำขอรออนุมัติ">
                    <Columns>
                        <asp:TemplateField HeaderText="บัญชี LINE ที่ขอผูก">
                            <ItemTemplate><%# ReqLineCell(Container.DataItem) %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="อ้างว่าเป็น">
                            <ItemTemplate>
                                <b><%# Eval("Username") %></b>
                                <div style="font-size:12px;color:#8a9a90;"><%# Eval("Role") %></div>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="ขอเมื่อ">
                            <ItemTemplate><%# Eval("RequestedDate", "{0:dd/MM/yyyy HH:mm}") %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="จัดการ">
                            <ItemTemplate>
                                <asp:LinkButton runat="server" CssClass="btn btn-success btn-xs" CommandName="ApproveReq"
                                    CommandArgument='<%# Eval("ID") %>'
                                    OnClientClick="return confirm('ยืนยันว่าเป็นคนคนเดียวกันจริง?\nอนุมัติแล้วผู้ใช้นี้จะเข้าระบบด้วย LINE ได้ทันที');">
                                    <i class="fas fa-check"></i> อนุมัติ</asp:LinkButton>
                                <asp:LinkButton runat="server" CssClass="btn btn-danger btn-xs" CommandName="RejectReq"
                                    CommandArgument='<%# Eval("ID") %>'
                                    OnClientClick="return confirm('ปฏิเสธคำขอนี้?');">
                                    <i class="fas fa-xmark"></i></asp:LinkButton>
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>
        </asp:Panel>

        <!-- รายชื่อผู้ใช้ (เฉพาะ Owner) -->
        <asp:Panel ID="pnlTeam" runat="server" CssClass="la-card" Visible="false">
            <h3><i class="fas fa-users"></i> สถานะการผูกบัญชีของทีม</h3>
            <div style="overflow-x:auto;">
                <asp:GridView ID="gvTeam" runat="server" AutoGenerateColumns="false" CssClass="tbl"
                    GridLines="None" DataKeyNames="ID" OnRowCommand="gvTeam_RowCommand"
                    EmptyDataText="ไม่พบผู้ใช้">
                    <Columns>
                        <asp:BoundField DataField="Username" HeaderText="ผู้ใช้" />
                        <asp:BoundField DataField="Role" HeaderText="บทบาท" />
                        <asp:TemplateField HeaderText="บัญชี LINE">
                            <ItemTemplate><%# LineCell(Container.DataItem) %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="แจ้งเตือน">
                            <ItemTemplate><%# NotifyCell(Container.DataItem) %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="จัดการ">
                            <ItemTemplate>
                                <asp:LinkButton runat="server" CssClass="btn btn-default btn-xs" CommandName="TestSend"
                                    CommandArgument='<%# Eval("ID") %>' Visible='<%# HasLine(Container.DataItem) %>'>
                                    <i class="fas fa-paper-plane"></i> ทดสอบส่ง</asp:LinkButton>
                                <asp:LinkButton runat="server" CssClass="btn btn-danger btn-xs" CommandName="UnlinkUser"
                                    CommandArgument='<%# Eval("ID") %>' Visible='<%# HasLine(Container.DataItem) %>'
                                    OnClientClick="return confirm('ยกเลิกการผูกบัญชีของผู้ใช้นี้?');">
                                    <i class="fas fa-unlink"></i></asp:LinkButton>
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>
            <div style="margin-top:14px;">
                <asp:TextBox ID="txtBroadcast" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="2"
                    placeholder="ข้อความทดสอบส่งหาทุกคนที่ผูกบัญชีแล้ว..." />
                <asp:Button ID="btnBroadcast" runat="server" Text="📢 ส่งหาทุกคนที่ผูกแล้ว"
                    CssClass="btn btn-primary" OnClick="btnBroadcast_Click" style="margin-top:8px;" />
            </div>
        </asp:Panel>

        <!-- ตั้งค่า (เฉพาะ Owner) -->
        <asp:Panel ID="pnlConfig" runat="server" CssClass="la-card" Visible="false">
            <h3><i class="fas fa-gear"></i> ตั้งค่า LINE Login</h3>
            <div class="hint">
                <b>สำคัญ:</b> LINE Login channel ต้องอยู่ <b>provider เดียวกัน</b> กับ Messaging API channel
                ที่ใช้ส่งข้อความ — ไม่งั้น userId ที่ได้จะเป็นคนละตัวและส่งข้อความไม่ถึง<br />
                และผู้ใช้ต้อง <b>เพิ่ม LINE OA ของที่พักเป็นเพื่อน</b> ก่อน ระบบจึงจะส่งหาได้<br />
                ตั้ง Callback URL ใน LINE Developers Console ให้ตรงกับค่าด้านล่างทุกตัวอักษร
            </div>

            <div class="grid2">
                <div class="form-row">
                    <label>สถานะ</label>
                    <asp:DropDownList ID="ddlEnabled" runat="server" CssClass="form-control">
                        <asp:ListItem Value="0" Text="ปิด" />
                        <asp:ListItem Value="1" Text="เปิดให้ผูกบัญชี" />
                    </asp:DropDownList>
                </div>
                <div class="form-row">
                    <label>LINE Login Channel ID</label>
                    <asp:TextBox ID="txtChannelId" runat="server" CssClass="form-control" placeholder="2001234567" />
                </div>
                <div class="form-row">
                    <label>Channel Secret <asp:Literal ID="litSecretStatus" runat="server" /></label>
                    <asp:TextBox ID="txtChannelSecret" runat="server" CssClass="form-control" TextMode="Password"
                        autocomplete="new-password" placeholder="เว้นว่าง = คงค่าเดิม" />
                </div>
            </div>
            <div class="form-row">
                <label>Callback URL (คัดลอกไปใส่ใน LINE Developers Console)</label>
                <asp:TextBox ID="txtCallback" runat="server" CssClass="form-control"
                    placeholder="https://taketimebangphra.com/Admin/LineLinkCallback" />
            </div>
            <asp:Button ID="btnSaveConfig" runat="server" Text="💾 บันทึกการตั้งค่า"
                CssClass="btn btn-success" OnClick="btnSaveConfig_Click" />
        </asp:Panel>
    </div>
</asp:Content>
