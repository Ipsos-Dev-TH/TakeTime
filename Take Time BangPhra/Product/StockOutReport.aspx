<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="StockOutReport.aspx.cs" Inherits="Take_Time_BangPhra.Product.StockOutReport" %>
<!DOCTYPE html>
<html lang="th">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>รายงานสินค้าเสียหาย/ทำลาย</title>
    <style>
        body { font-family: 'Sarabun', 'TH Sarabun New', Tahoma, sans-serif; color:#222; margin:0; background:#f4f5f7; font-size:14px; }
        .sheet { background:#fff; max-width:1050px; margin:16px auto; padding:26px 30px 34px; box-shadow:0 2px 10px rgba(0,0,0,.08); }
        .tools { max-width:1050px; margin:16px auto 0; padding:0 16px; display:flex; gap:10px; flex-wrap:wrap; align-items:flex-end; }
        .tools label { font-size:12.5px; color:#555; display:block; }
        .tools input, .tools select { padding:6px 8px; border:1px solid #c9d1d8; border-radius:6px; }
        .tools button { padding:8px 14px; border:0; border-radius:6px; background:#00695c; color:#fff; cursor:pointer; }
        .tools .alt { background:#546e7a; }
        h1 { font-size:20px; text-align:center; margin:4px 0 2px; }
        .co { text-align:center; font-size:13.5px; color:#444; }
        .meta { display:flex; justify-content:space-between; font-size:13px; margin:12px 0 8px; flex-wrap:wrap; gap:6px; }
        table.r { width:100%; border-collapse:collapse; font-size:12.5px; }
        table.r th, table.r td { border:1px solid #9aa5ae; padding:5px 6px; vertical-align:top; }
        table.r th { background:#eef2f4; }
        td.n { text-align:right; white-space:nowrap; }
        tr.tot td { font-weight:700; background:#fafbfc; }
        .sec { margin-top:16px; font-size:13px; }
        .line { display:inline-block; min-width:220px; border-bottom:1px dotted #777; padding:0 4px; }
        .sig { display:grid; grid-template-columns:repeat(auto-fill, minmax(190px, 1fr)); gap:22px 18px; margin-top:22px; }
        .sig div { text-align:center; font-size:12.5px; }
        .sig .s { border-bottom:1px solid #333; height:42px; margin-bottom:4px; }
        .sig input { width:92%; border:0; border-bottom:1px dotted #777; text-align:center; font:inherit; }
        .note { font-size:11.5px; color:#555; margin-top:18px; line-height:1.55; border-top:1px solid #ddd; padding-top:8px; }
        .empty { padding:30px; text-align:center; color:#888; }
        @media print {
            body { background:#fff; }
            .tools { display:none; }
            .sheet { box-shadow:none; margin:0; max-width:none; padding:0; }
            .sig input { border:0; }
            @page { size: A4 landscape; margin: 12mm; }
        }
        @media (max-width:640px) { .sheet { padding:16px; margin:8px; } }
    </style>
</head>
<body>
    <asp:Literal ID="litBody" runat="server" />
</body>
</html>
