<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="DailyBoard.aspx.cs" Inherits="Take_Time_BangPhra.DailyBoard" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>ตารางการจองรายวัน</title>
    <%-- CSS เขียนแบบพื้นฐานล้วน (ไม่มี flex/grid/shadow) เพราะหน้านี้ถูก render เป็นรูปด้วย
         HtmlRenderer ซึ่งรองรับ CSS ชุดจำกัด — ใช้ table + สีทึบ + ขนาดตัวอักษรใหญ่ อ่านง่ายบนมือถือ --%>
    <style type="text/css">
        body { font-family: Tahoma, 'Segoe UI', sans-serif; margin: 0; padding: 18px; background: #ffffff; color: #1f2d24; }
        .hd { background: #2e5d3a; color: #ffffff; padding: 14px 18px; }
        .hd .t1 { font-size: 24px; font-weight: bold; }
        .hd .t2 { font-size: 15px; color: #cfe3d6; }

        .kpi { margin-top: 10px; width: 100%; border-collapse: collapse; }
        .kpi td { width: 20%; background: #f1f7f3; border: 2px solid #ffffff; padding: 10px 12px; text-align: center; }
        .kpi .n { font-size: 25px; font-weight: bold; color: #2e5d3a; }
        .kpi .l { font-size: 14px; color: #5b7266; }
        .kpi .warn .n { color: #c0392b; }

        table.main { width: 100%; border-collapse: collapse; margin-top: 14px; }
        table.main th {
            background: #43705180; background: #437051; color: #ffffff; font-size: 15px;
            padding: 10px 8px; text-align: left; border: 1px solid #355a41;
        }
        table.main td { font-size: 15px; padding: 9px 8px; border: 1px solid #d8e4dc; vertical-align: top; }
        table.main tr.alt td { background: #f7faf8; }

        .room { font-weight: bold; font-size: 16px; color: #1f4d2c; }
        .guest { font-weight: bold; }
        .sub { font-size: 13px; color: #6b7f73; }
        .num { text-align: right; }
        .due { color: #c0392b; font-weight: bold; }
        .paid { color: #27ae60; font-weight: bold; }

        .tag { font-size: 13px; font-weight: bold; padding: 3px 9px; color: #ffffff; }
        .t-in { background: #2980b9; }
        .t-out { background: #e67e22; }
        .t-stay { background: #7f8c8d; }
        .t-vip { background: #8e44ad; }
        .t-new { background: #95a5a6; }

        .foot { margin-top: 12px; font-size: 13px; color: #8a9a90; }
        .empty { padding: 40px; text-align: center; font-size: 18px; color: #8a9a90; }
    </style>
</head>
<body>
    <form id="form1" runat="server">
        <asp:Literal ID="litBoard" runat="server" />
    </form>
</body>
</html>
