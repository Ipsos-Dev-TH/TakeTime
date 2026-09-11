<%@ Page Title="ผลการชำระเงิน" Language="C#" AutoEventWireup="true" CodeBehind="PayResult.aspx.cs" Inherits="Take_Time_BangPhra.Payment.PayResult" %>

<!DOCTYPE html>
<html lang="th">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>ผลการชำระเงิน — Take Time BangPhra</title>
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css" />
    <style>
        * { box-sizing: border-box; }
        body { margin: 0; background: #f2f5f3; font-family: 'Segoe UI', 'Sarabun', Tahoma, sans-serif; color: #21302a; }
        .wrap { max-width: 480px; margin: 0 auto; padding: 40px 14px; }
        .card { background: #fff; border-radius: 16px; padding: 26px 20px; text-align: center;
                box-shadow: 0 2px 14px rgba(0,0,0,.06); }
        .icon { font-size: 54px; margin-bottom: 14px; }
        .ok .icon { color: #1b7a4b; }
        .wait .icon { color: #d68910; }
        .bad .icon { color: #c0392b; }
        h1 { font-size: 21px; margin: 0 0 10px; }
        p { color: #56685f; line-height: 1.7; margin: 0 0 8px; font-size: 15px; }
        .ref { font-family: monospace; font-size: 13px; color: #7b8a83; margin-top: 14px; word-break: break-all; }
        .btn { display: block; width: 100%; padding: 13px; margin-top: 18px; border: 0; border-radius: 12px;
               background: #1b7a4b; color: #fff; font-size: 15.5px; font-weight: 600; cursor: pointer;
               text-decoration: none; text-align: center; }
        .btn.ghost { background: #fff; color: #46584f; border: 2px solid #dfe6e2; }
    </style>
</head>
<body>
<form id="form1" runat="server">
    <div class="wrap">
        <asp:Panel ID="pnlBox" runat="server" CssClass="card">
            <div class="icon"><asp:Literal ID="litIcon" runat="server" /></div>
            <h1><asp:Literal ID="litTitle" runat="server" /></h1>
            <p><asp:Literal ID="litDetail" runat="server" /></p>
            <div class="ref"><asp:Literal ID="litRef" runat="server" /></div>

            <asp:Button ID="btnRecheck" runat="server" CssClass="btn ghost" Visible="false"
                Text="🔄 ตรวจสอบอีกครั้ง" OnClick="btnRecheck_Click" />
            <%-- หมดอายุ/ไม่สำเร็จ/ยกเลิก = ต้องมีทางไปต่อ ไม่ใช่ทางตัน
                 ลิงก์นี้ผูกกับรายการต้นทาง (ใบจอง/กิจกรรม) ระบบสร้างรายการจ่ายใหม่ให้เอง --%>
            <asp:HyperLink ID="lnkRetry" runat="server" CssClass="btn" Visible="false">↻ เริ่มรายการชำระเงินใหม่</asp:HyperLink>
            <asp:HyperLink ID="lnkHome" runat="server" CssClass="btn" NavigateUrl="~/">กลับหน้าแรก</asp:HyperLink>
        </asp:Panel>
    </div>
</form>
</body>
</html>
