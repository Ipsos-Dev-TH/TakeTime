<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="LineLink.aspx.cs" Inherits="Take_Time_BangPhra.Mobile.LineLink" EnableEventValidation="false" %>

<!DOCTYPE html>
<html lang="th">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no" />
    <title>ผูกบัญชี LINE</title>
    <link href="https://fonts.googleapis.com/css2?family=Prompt:wght@300;400;500;600;700&display=swap" rel="stylesheet" />
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css" />
    <style>
        * { box-sizing: border-box; -webkit-tap-highlight-color: transparent; }
        body { margin: 0; font-family: 'Prompt', sans-serif; background: #f2f5f8; color: #24303a; padding-bottom: 34px; }
        .hd { background: linear-gradient(135deg, #06C755, #04a144); color: #fff; padding: 22px 18px; text-align: center; }
        .hd img { width: 72px; height: 72px; border-radius: 50%; border: 3px solid rgba(255,255,255,.6); object-fit: cover; }
        .hd .nm { font-size: 1.2em; font-weight: 600; margin-top: 9px; }
        .hd .sub { font-size: 13px; opacity: .9; margin-top: 3px; }
        .wrap { max-width: 620px; margin: 0 auto; padding: 14px; }
        .card { background: #fff; border-radius: 14px; box-shadow: 0 2px 10px rgba(0,0,0,.06); padding: 18px; margin-bottom: 14px; }
        .card h2 { margin: 0 0 6px; font-size: 1.04em; color: #1b5e3a; font-weight: 600; }
        .card p.lead { margin: 0 0 14px; font-size: 13.5px; color: #6b7f8f; }

        .search { width: 100%; padding: 13px 14px; border: 2px solid #dde5ec; border-radius: 11px;
                  font-family: inherit; font-size: 16px; margin-bottom: 12px; }
        .search:focus { outline: none; border-color: #06C755; }

        .who { display: block; width: 100%; text-align: left; border: 2px solid #dde5ec; background: #fff;
               border-radius: 12px; padding: 14px 16px; margin-bottom: 9px; cursor: pointer; font-family: inherit; }
        .who:active, .who.sel { border-color: #06C755; background: #f0fbf4; }
        .who b { display: block; font-size: 15.5px; color: #24303a; }
        .who small { color: #7d8f9c; font-size: 12.5px; }

        .field { margin-bottom: 13px; }
        .field label { display: block; font-weight: 600; font-size: 14px; margin-bottom: 7px; }
        .field input { width: 100%; padding: 13px 14px; border: 2px solid #dde5ec; border-radius: 11px;
                       font-family: inherit; font-size: 16px; }
        .field input:focus { outline: none; border-color: #06C755; }

        .btn { width: 100%; padding: 15px; border: none; border-radius: 12px; font-family: inherit;
               font-size: 16px; font-weight: 600; cursor: pointer; margin-bottom: 9px; }
        .btn-go { background: #06C755; color: #fff; }
        .btn-alt { background: #fff; color: #2c5c8a; border: 2px solid #2c5c8a; }
        .btn-gh { background: #fff; color: #6b7f8f; border: 2px solid #d6dee5; }

        .msg { padding: 14px 16px; border-radius: 12px; margin-bottom: 14px; font-size: 14.5px; }
        .msg.ok { background: #e8f6ed; color: #1e7e42; border: 1px solid #b6e0c4; }
        .msg.err { background: #fdecea; color: #a5342a; border: 1px solid #f5c2bc; }
        .msg.info { background: #eaf3fb; color: #1c5580; border: 1px solid #bcd9f0; }
        .empty { text-align: center; padding: 24px; color: #93a3af; font-size: 14px; }
        .picked { background: #f0fbf4; border: 2px solid #06C755; border-radius: 12px; padding: 13px 16px; margin-bottom: 14px; }
    </style>
</head>
<body>
    <form id="form1" runat="server">
        <div class="hd">
            <asp:Literal ID="litAvatar" runat="server" />
            <div class="nm"><asp:Literal ID="litLineName" runat="server" /></div>
            <div class="sub">เข้าสู่ระบบด้วย LINE สำเร็จ</div>
        </div>

        <div class="wrap">
            <asp:Panel ID="pnlMsg" runat="server" Visible="false">
                <div runat="server" id="divMsg" class="msg"><asp:Literal ID="litMsg" runat="server" /></div>
            </asp:Panel>

            <!-- ยืนยันตัวตนด้วยบัญชีระบบ (ครั้งแรกเท่านั้น) -->
            <asp:Panel ID="pnlVerify" runat="server" CssClass="card">
                <h2><i class="fas fa-shield-halved"></i> ยืนยันตัวตนก่อนผูกบัญชี</h2>
                <p class="lead">
                    นี่เป็นการผูกบัญชี LINE ครั้งแรก — กรุณากรอกชื่อผู้ใช้และรหัสผ่านของคุณ
                    เพื่อยืนยันว่าเป็นเจ้าของบัญชีจริง (ทำครั้งเดียว ครั้งต่อไปกดเข้าด้วย LINE ได้เลย)
                </p>

                <div class="field">
                    <label>ชื่อผู้ใช้</label>
                    <asp:TextBox ID="txtUsername" runat="server" autocomplete="username"
                        placeholder="ชื่อผู้ใช้ที่ใช้เข้าระบบ" />
                </div>
                <div class="field">
                    <label>รหัสผ่าน</label>
                    <asp:TextBox ID="txtPassword" runat="server" TextMode="Password"
                        autocomplete="current-password" placeholder="รหัสผ่าน" />
                </div>

                <asp:Button ID="btnLinkNow" runat="server" Text="🔗 ยืนยันและผูกบัญชี"
                    CssClass="btn btn-go" OnClick="btnLinkNow_Click" />

                <div style="font-size:12.5px; color:#93a3af; text-align:center; line-height:1.7;">
                    จำรหัสผ่านไม่ได้? ติดต่อผู้ดูแลระบบเพื่อรีเซ็ตรหัสผ่านก่อน<br />
                    แล้วจึงกลับมาผูกบัญชีอีกครั้ง
                </div>
            </asp:Panel>

            <!-- เสร็จแล้ว -->
            <asp:Panel ID="pnlDone" runat="server" CssClass="card" Visible="false">
                <h2><i class="fas fa-circle-check"></i> <asp:Literal ID="litDoneTitle" runat="server" /></h2>
                <p class="lead"><asp:Literal ID="litDoneText" runat="server" /></p>
                <a class="btn btn-go" style="display:block; text-align:center; text-decoration:none; line-height:1.4;"
                   href="/Mobile/Leave">ไปหน้ายื่นใบลา</a>
            </asp:Panel>
        </div>


    </form>
</body>
</html>
