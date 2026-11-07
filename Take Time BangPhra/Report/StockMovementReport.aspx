<%@ Page Title="รายงานสินค้าและวัตถุดิบ" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="StockMovementReport.aspx.cs" Inherits="Take_Time_BangPhra.TaxReports.StockMovementReport" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <asp:Literal ID="litCss" runat="server" />

    <div class="tr-wrap">
        <div class="tr-head">
            <h2><i class="fas fa-boxes"></i> รายงานสินค้าและวัตถุดิบ</h2>
            <p>ยกมา · รับเข้า · ขาย · จ่ายออกอื่นแยกตามเหตุผล · คงเหลือ (จำนวนและมูลค่าตามราคาทุน) — ประกอบ ม.87(3) แห่งประมวลรัษฎากร</p>
        </div>

        <div class="tr-note tr-noprint">
            <b>ใช้อย่างไร:</b> ผู้ประกอบการจด VAT ที่ขายสินค้าต้องจัดทำรายงานสินค้าและวัตถุดิบ (ม.87(3)) — หน้านี้รวบรวมจากการรับเข้า (Product_In)
            และจ่ายออก (Product_Out) ใน TakeTime. เลือกสินค้ารายตัวเพื่อดู <b>บัญชีคุมรายวัน</b> (วันที่ · เอกสาร · รับ · จ่าย · คงเหลือ) ตามรูปแบบรายงาน.
            สินค้าที่ขาดจากรายงานโดยไม่มีหลักฐาน (สูญหาย/ถูกขโมย/ขาดจากการนับ) อาจถือเป็นการขายตาม ม.77/1(8)(จ) ต้องเสีย VAT จากราคาตลาด —
            บันทึกจ่ายออกพร้อมเหตุผลและหลักฐานทุกครั้ง. <b>มูลค่า</b> คิดจากราคาทุนต่อหน่วยปัจจุบันของสินค้า (ระบบใช้ต้นทุนมาตรฐาน ไม่ใช่ FIFO)
            — ผู้ทำบัญชีควรตรวจทานรูปแบบรายงานตามประกาศอธิบดีฯ ก่อนใช้ยื่น/เก็บเป็นหลักฐาน
        </div>

        <div class="tr-card tr-noprint">
            <div class="tr-tools">
                <div><label>ตั้งแต่วันที่</label><asp:TextBox ID="txtFrom" runat="server" TextMode="Date" /></div>
                <div><label>ถึงวันที่</label><asp:TextBox ID="txtTo" runat="server" TextMode="Date" /></div>
                <div><label>สินค้า</label><asp:DropDownList ID="ddlProduct" runat="server" /></div>
                <div><label>&nbsp;</label><asp:CheckBox ID="chkAll" runat="server" Text=" แสดงสินค้าที่ไม่มีความเคลื่อนไหวด้วย" /></div>
                <div>
                    <asp:Button ID="btnShow" runat="server" Text="แสดงรายงาน" CssClass="btn btn-primary" OnClick="btnShow_Click" />
                    <asp:Button ID="btnCsv" runat="server" Text="ดาวน์โหลด CSV" CssClass="btn btn-default" OnClick="btnCsv_Click" />
                    <button type="button" class="btn btn-default" onclick="window.print(); return false;">พิมพ์</button>
                </div>
            </div>
        </div>

        <asp:Literal ID="litMsg" runat="server" />

        <div class="tr-card">
            <asp:Literal ID="litHeader" runat="server" />
            <div class="tr-scroll">
                <asp:Literal ID="litSummary" runat="server" />
            </div>
        </div>

        <asp:Panel ID="pnlLedger" runat="server" Visible="false" CssClass="tr-card">
            <h3>บัญชีคุมสินค้ารายวัน</h3>
            <div class="tr-scroll">
                <asp:Literal ID="litLedger" runat="server" />
            </div>
        </asp:Panel>

        <asp:Literal ID="litNotes" runat="server" />
    </div>
</asp:Content>
