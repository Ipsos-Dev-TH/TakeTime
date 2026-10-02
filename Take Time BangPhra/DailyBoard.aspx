<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="DailyBoard.aspx.cs" Inherits="Take_Time_BangPhra.DailyBoard" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>ตารางการจองรายวัน</title>
    <%-- หน้านี้ถูก render เป็นรูปด้วย HtmlRenderer (รองรับ CSS ชุดจำกัด) —
         สีพื้น/เส้นขอบทั้งหมดสั่งผ่าน bgcolor attribute + inline style ใน code-behind
         ไม่ใช้ CSS class/descendant selector เพราะบางตัวไม่ถูกวาด --%>
    <style type="text/css">
        body { font-family: Tahoma, sans-serif; margin: 0; padding: 16px; color: #111111; }
    </style>
</head>
<body>
    <form id="form1" runat="server">
        <asp:Literal ID="litBoard" runat="server" />
    </form>
</body>
</html>
