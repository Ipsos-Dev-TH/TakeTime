<%@ Page Title="" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Login.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Login" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .lg-wrap { max-width: 420px; margin: 34px auto 60px; padding: 0 14px; }
        .lg-card { background: #fff; border-radius: 16px; box-shadow: 0 4px 20px rgba(0,0,0,.09); padding: 28px 26px; }
        .lg-card h2 { margin: 0 0 4px; font-size: 1.28em; font-weight: 700; color: #4a3226; text-align: center; }
        .lg-card .sub { text-align: center; color: #8b7f77; font-size: 13.5px; margin-bottom: 22px; }

        .lg-field { margin-bottom: 15px; }
        .lg-field label { display: block; font-weight: 600; font-size: 13.5px; margin-bottom: 6px; color: #4a3226; }
        .lg-field input[type=text], .lg-field input[type=password] {
            width: 100%; padding: 13px 14px; border: 2px solid #e2dcd7; border-radius: 11px;
            font-size: 16px; font-family: inherit;
        }
        .lg-field input:focus { outline: none; border-color: #6d4c41; }

        .lg-btn { width: 100%; padding: 14px; border: none; border-radius: 11px;
                  font-size: 16px; font-weight: 600; cursor: pointer; font-family: inherit; }
        .lg-primary { background: #6d4c41; color: #fff; }
        .lg-line { background: #06C755; color: #fff; display: block; text-align: center;
                   text-decoration: none; line-height: 1.4; }
        .lg-line:hover { background: #05b34c; color: #fff; text-decoration: none; }
        .lg-line i { margin-right: 6px; }

        .lg-or { display: flex; align-items: center; gap: 12px; margin: 20px 0 16px; color: #b0a49c; font-size: 13px; }
        .lg-or::before, .lg-or::after { content: ""; flex: 1; height: 1px; background: #e8e2dd; }

        .lg-note { font-size: 12.5px; color: #8b7f77; text-align: center; margin-top: 10px; line-height: 1.6; }
    </style>

    <div class="lg-wrap">
        <div class="lg-card">
            <h2><asp:Literal ID="litTitle" runat="server" Text="เข้าสู่ระบบผู้ดูแล" /></h2>
            <div class="sub"><asp:Literal ID="litSub" runat="server" Text="Take Time Nature Resort" /></div>

            <%-- เข้าด้วย LINE — แสดงเฉพาะเมื่อผู้ดูแลตั้งค่า LINE Login แล้ว --%>
            <asp:Panel ID="pnlLineLogin" runat="server" Visible="false">
                <asp:LinkButton ID="btnLineLogin" runat="server" CssClass="lg-btn lg-line" OnClick="btnLineLogin_Click">
                    <i class="fab fa-line"></i> เข้าสู่ระบบด้วย LINE
                </asp:LinkButton>
                <div class="lg-note">เร็วที่สุด — ไม่ต้องจำรหัสผ่าน (ครั้งแรกจะให้เลือกชื่อของคุณ)</div>
                <div class="lg-or">หรือเข้าด้วยรหัสผ่าน</div>
            </asp:Panel>

            <div class="lg-field">
                <label><asp:Label ID="Label9" runat="server" Text="User"></asp:Label></label>
                <asp:TextBox ID="TextBox1" runat="server" autocomplete="username" />
            </div>
            <div class="lg-field">
                <label><asp:Label ID="Label10" runat="server" Text="Password"></asp:Label></label>
                <asp:TextBox ID="TextBox2" runat="server" TextMode="Password" autocomplete="current-password" />
            </div>

            <asp:Button ID="Button1" runat="server" Text="เข้าสู่ระบบ" CssClass="lg-btn lg-primary" OnClick="Button1_Click" />
            <asp:Button ID="Button2" runat="server" Text="เปลี่ยนรหัสผ่าน" CssClass="lg-btn lg-primary"
                Visible="false" OnClick="Button2_Click" />
        </div>
    </div>
</asp:Content>
