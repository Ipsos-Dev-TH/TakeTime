<%@ Page Title="พิมพ์ QR ติดโต๊ะ" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="QrSheet.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Dining.QrSheetPage" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link rel="stylesheet" href="<%= ResolveUrl("~/Content/admin-settings.css") %>?v=<%= Take_Time_BangPhra.Admin.Settings.SettingsUi.AssetVersion %>" />
    <link rel="stylesheet" href="<%= ResolveUrl("~/Content/dining-admin.css") %>?v=<%= Take_Time_BangPhra.Admin.Settings.SettingsUi.AssetVersion %>" />
    <style>
        /* ── แถบเครื่องมือ (ซ่อนตอนพิมพ์) ── */
        .qs-tools { display: flex; flex-wrap: wrap; gap: 8px 14px; align-items: center; }
        .qs-tools .grp { display: flex; flex-wrap: wrap; gap: 4px; align-items: center; }
        .qs-tools .grp > span { font-size: 12.5px; color: #6b7c85; margin-right: 4px; }
        .qs-tools a.opt { padding: 6px 11px; border-radius: 18px; border: 1.5px solid #ffccbc; background: #fff; color: #bf360c; font-size: 13px; font-weight: 600; text-decoration: none; }
        .qs-tools a.opt.on { background: #e65100; border-color: #e65100; color: #fff; }

        /* ── แผ่นพิมพ์ ── */
        .qs-root { background: #e9ecef; padding: 12px; border-radius: 12px; overflow-x: auto; }
        .qs-sheet { background: #fff; width: 210mm; min-height: 297mm; margin: 0 auto 14px; padding: 8mm; box-sizing: border-box; box-shadow: 0 2px 10px rgba(0,0,0,.12);
                    display: grid; gap: 4mm; align-content: start; page-break-after: always; }
        .qs-sheet.grid8 { grid-template-columns: repeat(2, 1fr); grid-auto-rows: 66mm; }
        .qs-sheet.grid18 { grid-template-columns: repeat(3, 1fr); grid-auto-rows: 44mm; }
        .qs-sheet.tent { grid-template-columns: repeat(2, 100mm); grid-auto-rows: 150mm; justify-content: center; gap: 0; padding: 5mm; }
        .qs-card { border: 0.4mm dashed #b0bec5; border-radius: 3mm; padding: 3mm; box-sizing: border-box; display: flex; flex-direction: column; align-items: center; text-align: center; overflow: hidden;
                   font-family: 'Segoe UI', Tahoma, 'Noto Sans Thai', sans-serif; color: #263238; }
        .tent .qs-card { border-style: solid; border-color: #ffccbc; border-radius: 0; }
        .qs-logo { height: 7mm; max-width: 60%; object-fit: contain; margin-bottom: 0.5mm; }
        .grid18 .qs-logo { height: 5mm; }
        .tent .qs-logo { height: 16mm; margin-bottom: 2mm; }
        .qs-biz { font-size: 8pt; color: #546e7a; font-weight: 600; line-height: 1.2; }
        .grid18 .qs-biz { display: none; }
        .tent .qs-biz { font-size: 12pt; }
        .qs-title { font-size: 10pt; font-weight: 700; color: #bf360c; line-height: 1.2; margin: 0.5mm 0; }
        .qs-title small { display: block; font-size: 7.5pt; font-weight: 600; color: #e65100; }
        .grid18 .qs-title { font-size: 8pt; margin: 0.3mm 0; }
        .grid18 .qs-title small { font-size: 6pt; }
        .tent .qs-title { font-size: 17pt; margin: 2mm 0; }
        .tent .qs-title small { font-size: 11pt; }
        /* ความสูง QR กำหนดตายตัวต่อแบบ (SVG ยืดตามความสูง — ไม่พึ่ง flex ที่อาจยุบเป็น 0) */
        .qs-qr { height: 23mm; flex: none; display: flex; align-items: center; justify-content: center; width: 100%; margin: 0.5mm 0; }
        .grid18 .qs-qr { height: 16mm; }
        .tent .qs-qr { height: 64mm; margin: 2mm 0; }
        .qs-qr svg, .qs-qr img, .qs-qr canvas { height: 100%; width: auto; max-width: 100%; aspect-ratio: 1 / 1; display: block; }
        .qs-qr .qr-js { height: 100%; aspect-ratio: 1 / 1; }
        .qs-qr .qr-js img, .qs-qr .qr-js canvas { height: 100%; width: auto; }
        .qs-table { font-size: 20pt; font-weight: 800; line-height: 1.1; margin-top: 0.5mm; letter-spacing: .5px; }
        .grid18 .qs-table { font-size: 14pt; }
        .tent .qs-table { font-size: 40pt; margin-top: 3mm; }
        .qs-zone { font-size: 9pt; color: #546e7a; }
        .grid18 .qs-zone { font-size: 7pt; }
        .tent .qs-zone { font-size: 14pt; }
        .qs-note { font-size: 6pt; color: #78909c; line-height: 1.25; margin-top: 0.5mm; }
        .grid18 .qs-note { display: none; }
        .tent .qs-note { font-size: 9.5pt; margin-top: 2mm; }
        .qs-url { font-size: 6.5pt; color: #90a4ae; word-break: break-all; }
        .grid18 .qs-url { font-size: 5pt; }
        .tent .qs-url { font-size: 9pt; }
        .qs-contact { font-size: 7pt; color: #546e7a; margin-top: .3mm; }
        .grid18 .qs-contact { display: none; }
        .tent .qs-contact { font-size: 10pt; }
        .qs-nojs { font-size: 8pt; color: #a12626; }

        @media screen and (max-width: 860px) { .qs-sheet { transform: scale(.6); transform-origin: top left; margin-bottom: -110mm; } }

        @media print {
            @page { size: A4 portrait; margin: 0; }
            body * { visibility: hidden !important; }
            #qsPrintRoot, #qsPrintRoot * { visibility: visible !important; }
            #qsPrintRoot { position: absolute; left: 0; top: 0; width: 100%; background: #fff; padding: 0; border-radius: 0; overflow: visible; }
            .qs-sheet { box-shadow: none; margin: 0; width: 210mm; height: 297mm; min-height: 0; transform: none; }
            .qs-sheet:last-child { page-break-after: auto; }
            .qs-card { break-inside: avoid; }
        }
    </style>

    <div class="dn-wrap">
        <nav class="as-crumb" aria-label="เส้นทาง">
            <a href="<%= ResolveUrl("~/Admin/Settings/Index") %>"><i class="fas fa-sliders"></i> ศูนย์ตั้งค่า</a>
            <span class="sep">›</span><a href="<%= ResolveUrl("~/Admin/Dining/Zones") %>">QR สั่งอาหาร — โซน &amp; โต๊ะ</a>
            <span class="sep">›</span><span class="here">พิมพ์ QR ติดโต๊ะ</span>
            <a class="as-back" href="<%= ResolveUrl("~/Admin/Dining/Zones") %>">← กลับโซน &amp; โต๊ะ</a>
        </nav>

        <div class="dn-head">
            <h2><i class="fas fa-print"></i> พิมพ์ QR ติดโต๊ะ</h2>
            <p>QR สร้างในเครื่องนี้ (ไม่ส่งลิงก์ออกนอก) · เลือกแบบแผ่น A4 แล้วกดพิมพ์ — ตั้งเครื่องพิมพ์เป็น A4, ขอบ 0, ไม่ย่อหน้า (100%) · หมุน QR แล้วต้องพิมพ์ใหม่</p>
        </div>

        <asp:Literal ID="litMsg" runat="server" />

        <div class="dn-card">
            <div class="qs-tools">
                <asp:Literal ID="litTools" runat="server" />
                <button type="button" class="dn-btn" onclick="window.print()"><i class="fas fa-print"></i> พิมพ์</button>
            </div>
        </div>

        <div class="qs-root" id="qsPrintRoot">
            <asp:Literal ID="litSheets" runat="server" />
        </div>
    </div>

    <asp:Literal ID="litFallback" runat="server" />
</asp:Content>
