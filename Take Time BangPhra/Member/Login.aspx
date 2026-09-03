<%@ Page Title="เข้าสู่ระบบสมาชิก" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Login.aspx.cs" Inherits="Take_Time_BangPhra.Member.MemberLogin" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .ml-wrap { max-width: 420px; margin: 40px auto 70px; padding: 0 14px; }
        .ml-card { background: #fff; border-radius: 18px; box-shadow: 0 8px 30px rgba(0,0,0,.12); overflow: hidden; }
        .ml-top { background: linear-gradient(135deg, #5D4037, #8D6E63); color: #fff; padding: 26px 24px; text-align: center; }
        .ml-top i { font-size: 34px; margin-bottom: 8px; display: block; }
        .ml-top h2 { margin: 0; font-size: 1.3em; font-weight: 700; }
        .ml-top p { margin: 6px 0 0; font-size: 13px; opacity: .9; }
        .ml-body { padding: 24px; }
        .ml-field { margin-bottom: 15px; }
        .ml-field label { display: block; font-weight: 600; font-size: 13.5px; margin-bottom: 6px; color: #37474f; }
        .ml-field input { width: 100%; padding: 12px 14px; border: 1.5px solid #dbe2e7; border-radius: 10px;
                          font-size: 16px; letter-spacing: 1px; }
        .ml-field input:focus { outline: none; border-color: #8D6E63; }
        .ml-btn { width: 100%; padding: 13px; background: linear-gradient(135deg, #5D4037, #6D4C41); color: #fff;
                  border: none; border-radius: 10px; font-size: 15.5px; font-weight: 700; cursor: pointer; }
        .ml-hint { font-size: 12.5px; color: #90a4ae; margin-top: 14px; line-height: 1.6; text-align: center; }
        .ml-err { background: #ffebee; color: #c62828; padding: 11px 14px; border-radius: 9px;
                  font-size: 13.5px; margin-bottom: 14px; }
    </style>

    <div class="ml-wrap">
        <div class="ml-card">
            <div class="ml-top">
                <i class="fas fa-id-card"></i>
                <h2>บัตรสมาชิก Take Time</h2>
                <p>ดูสิทธิ์ ส่วนลด และคูปองของคุณ</p>
            </div>
            <div class="ml-body">
                <asp:Panel ID="pnlErr" runat="server" Visible="false" CssClass="ml-err">
                    <asp:Literal ID="litErr" runat="server" />
                </asp:Panel>

                <asp:Panel ID="pnlLogin" runat="server" DefaultButton="btnLogin">
                    <div class="ml-field">
                        <label>เบอร์โทรศัพท์</label>
                        <asp:TextBox ID="txtPhone" runat="server" TextMode="Phone" MaxLength="15"
                            placeholder="08xxxxxxxx" autocomplete="tel" />
                    </div>
                    <div class="ml-field">
                        <label>รหัส PIN</label>
                        <asp:TextBox ID="txtPin" runat="server" TextMode="Password" MaxLength="8"
                            placeholder="••••••" autocomplete="current-password" />
                    </div>
                    <asp:Button ID="btnLogin" runat="server" Text="เข้าสู่ระบบ" CssClass="ml-btn" OnClick="btnLogin_Click" />
                    <div class="ml-hint">
                        เข้าครั้งแรก? ใช้ <b>เลขท้ายเบอร์โทร 4 ตัว</b> เป็นรหัส PIN<br />
                        ลืมรหัส PIN ติดต่อเคาน์เตอร์เพื่อรีเซ็ตได้เลยค่ะ
                    </div>
                </asp:Panel>

                <asp:Panel ID="pnlSetPin" runat="server" Visible="false" DefaultButton="btnSetPin">
                    <div class="ml-hint" style="text-align:left; margin: 0 0 14px; color:#37474f;">
                        🔐 เพื่อความปลอดภัย กรุณาตั้งรหัส PIN ใหม่ (ตัวเลข 4-8 หลัก ห้ามใช้เลขท้ายเบอร์)
                    </div>
                    <div class="ml-field">
                        <label>รหัส PIN ใหม่</label>
                        <asp:TextBox ID="txtNewPin" runat="server" TextMode="Password" MaxLength="8" />
                    </div>
                    <div class="ml-field">
                        <label>ยืนยันรหัส PIN</label>
                        <asp:TextBox ID="txtNewPin2" runat="server" TextMode="Password" MaxLength="8" />
                    </div>
                    <asp:Button ID="btnSetPin" runat="server" Text="บันทึกรหัส PIN" CssClass="ml-btn" OnClick="btnSetPin_Click" />
                </asp:Panel>
            </div>
        </div>
    </div>
</asp:Content>
