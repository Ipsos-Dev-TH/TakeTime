<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="AddFriend.aspx.cs" Inherits="Take_Time_BangPhra.Mobile.AddFriend" %>

<!DOCTYPE html>
<html lang="th">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no" />
    <title>เพิ่มเพื่อน LINE</title>
    <link href="https://fonts.googleapis.com/css2?family=Prompt:wght@300;400;500;600;700&display=swap" rel="stylesheet" />
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css" />
    <style>
        * { box-sizing: border-box; -webkit-tap-highlight-color: transparent; }
        body { margin: 0; font-family: 'Prompt', sans-serif; background: #f2f5f8; color: #24303a; }
        .hd { background: linear-gradient(135deg, #06C755, #04a144); color: #fff; padding: 26px 18px; text-align: center; }
        .hd i { font-size: 44px; }
        .hd h1 { margin: 10px 0 4px; font-size: 1.3em; font-weight: 600; }
        .hd p { margin: 0; font-size: 13.5px; opacity: .92; }
        .wrap { max-width: 520px; margin: 0 auto; padding: 16px; }
        .card { background: #fff; border-radius: 14px; box-shadow: 0 2px 10px rgba(0,0,0,.06);
                padding: 20px; margin-bottom: 14px; text-align: center; }
        .card h2 { margin: 0 0 8px; font-size: 1.05em; color: #1b5e3a; font-weight: 600; }
        .card p { margin: 0 0 16px; font-size: 14px; color: #667; line-height: 1.65; }
        .qr { width: 190px; height: 190px; border: 1px solid #e3eae6; border-radius: 12px; margin: 0 auto 16px; display: block; }
        .btn { display: block; width: 100%; padding: 15px; border: none; border-radius: 12px;
               font-family: inherit; font-size: 16px; font-weight: 600; cursor: pointer;
               margin-bottom: 10px; text-decoration: none; text-align: center; }
        .btn-line { background: #06C755; color: #fff; }
        .btn-check { background: #fff; color: #2c5c8a; border: 2px solid #2c5c8a; }
        .steps { text-align: left; background: #f6f9f7; border-radius: 10px; padding: 14px 16px;
                 font-size: 13.5px; color: #4a5b52; line-height: 1.9; }
        .steps b { color: #1b5e3a; }
        .msg { padding: 14px 16px; border-radius: 12px; margin-bottom: 14px; font-size: 14.5px; }
        .msg.err { background: #fdecea; color: #a5342a; border: 1px solid #f5c2bc; }
        .msg.ok { background: #e8f6ed; color: #1e7e42; border: 1px solid #b6e0c4; }
    </style>
</head>
<body>
    <form id="form1" runat="server">
        <div class="hd">
            <i class="fab fa-line"></i>
            <h1>เพิ่มเพื่อนก่อนใช้งาน</h1>
            <p>ระบบต้องส่งแจ้งเตือนหาคุณผ่าน LINE นี้</p>
        </div>

        <div class="wrap">
            <asp:Panel ID="pnlMsg" runat="server" Visible="false">
                <div runat="server" id="divMsg" class="msg"><asp:Literal ID="litMsg" runat="server" /></div>
            </asp:Panel>

            <div class="card">
                <h2><i class="fas fa-user-plus"></i> เพิ่ม <asp:Literal ID="litOaName" runat="server" /> เป็นเพื่อน</h2>
                <p>LINE จะส่งข้อความหาคุณได้ก็ต่อเมื่อคุณเพิ่มบัญชีทางการของที่พักเป็นเพื่อนแล้ว</p>

                <asp:Literal ID="litQr" runat="server" />

                <asp:HyperLink ID="lnkAdd" runat="server" CssClass="btn btn-line" Target="_blank">
                    <i class="fab fa-line"></i> แตะเพื่อเพิ่มเพื่อนใน LINE
                </asp:HyperLink>

                <asp:Button ID="btnRecheck" runat="server" Text="✓ เพิ่มแล้ว — ตรวจสอบอีกครั้ง"
                    CssClass="btn btn-check" OnClick="btnRecheck_Click" />

                <div class="steps">
                    <b>ทำตามนี้</b><br />
                    1. แตะปุ่มเขียวด้านบน (หรือสแกน QR ถ้าเปิดจากคอม)<br />
                    2. ในแอป LINE กด <b>“เพิ่มเพื่อน”</b><br />
                    3. กลับมาหน้านี้แล้วกด <b>“ตรวจสอบอีกครั้ง”</b>
                </div>
            </div>
        </div>
    </form>
</body>
</html>
