<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="LineLinkCallback.aspx.cs" Inherits="Take_Time_BangPhra.Admin.LineLinkCallback" %>
<!DOCTYPE html>
<html>
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>ผูกบัญชี LINE</title>
    <link href="https://fonts.googleapis.com/css2?family=Prompt:wght@400;600&display=swap" rel="stylesheet" />
    <style>
        body { font-family: 'Prompt', sans-serif; background: #f4f7f5; display: flex;
               align-items: center; justify-content: center; height: 100vh; margin: 0; }
        .box { background: #fff; border-radius: 16px; padding: 34px 30px; max-width: 420px;
               text-align: center; box-shadow: 0 4px 20px rgba(0,0,0,.1); }
        .ico { font-size: 52px; margin-bottom: 12px; }
        h2 { margin: 0 0 10px; font-size: 1.25em; }
        p { color: #667; font-size: 14.5px; line-height: 1.6; margin: 0 0 20px; }
        a.btn { display: inline-block; background: #4a7c59; color: #fff; text-decoration: none;
                padding: 12px 26px; border-radius: 10px; font-weight: 600; }
        .ok { color: #1e7e42; } .err { color: #c0392b; }
    </style>
</head>
<body>
    <form id="form1" runat="server">
        <div class="box">
            <div class="ico"><asp:Literal ID="litIcon" runat="server" /></div>
            <h2><asp:Literal ID="litTitle" runat="server" /></h2>
            <p><asp:Literal ID="litDetail" runat="server" /></p>
            <a class="btn" href="/Admin/Settings/LineAccount">กลับไปหน้าตั้งค่า</a>
        </div>
    </form>
</body>
</html>
