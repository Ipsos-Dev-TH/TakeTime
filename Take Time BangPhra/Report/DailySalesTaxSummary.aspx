<%@ Page Title="สรุปยอดขายและภาษีขายรายวัน" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="DailySalesTaxSummary.aspx.cs" Inherits="Take_Time_BangPhra.TaxReports.DailySalesTaxSummary" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <asp:Literal ID="litCss" runat="server" />

    <div class="tr-wrap">
        <div class="tr-head">
            <h2><i class="fas fa-file-invoice-dollar"></i> สรุปยอดขายและภาษีขายรายวัน</h2>
            <p>รายวัน × ช่องทางขาย × แหล่งเงิน: จำนวนการขาย ยอดรวม ภาษีขาย และเอกสาร NextAcc ที่ครอบ (รายใบ / ใบสรุปรายวัน) พร้อมธงรายการที่ยังไม่มีเอกสาร</p>
        </div>

        <div class="tr-note">
            <b>ข้อควรทราบทางภาษี:</b> ใบสรุปรายวันที่ส่งไป NextAcc (เช่น <code>POSDAY-…</code>) เป็น <b>เอกสารประกอบการลงบัญชีเท่านั้น ไม่ใช่ใบกำกับภาษีของลูกค้า</b> —
            การขายเงินสดทุกครั้งยังต้องออก <b>ใบกำกับภาษีอย่างย่อรายใบ</b> ให้ลูกค้าที่หน้าร้าน (ม.86, ม.86/6; สลิปจากเครื่องบันทึกการเก็บเงินต้องได้รับอนุมัติ ภ.พ.06)
            แล้วจึงลงรายงานภาษีขายเป็นยอดรวมต่อวันโดยอ้างเลขที่ใบอย่างย่อแรก–สุดท้ายได้. หน้านี้ใช้เป็น <b>บันทึกสรุปยอดขายเงินสดรายวัน</b> ที่รองรับใบสรุปนั้น
            (พิมพ์/ดาวน์โหลดเก็บคู่กับสำเนาใบอย่างย่อ). ขายหลายลูกค้ารวมเป็นใบกำกับภาษีใบเดียวไม่ได้ — ให้ผู้ทำบัญชียืนยันกับสรรพากรพื้นที่
        </div>

        <div class="tr-card tr-noprint">
            <div class="tr-tools">
                <div><label>ตั้งแต่วันที่</label><asp:TextBox ID="txtFrom" runat="server" TextMode="Date" /></div>
                <div><label>ถึงวันที่</label><asp:TextBox ID="txtTo" runat="server" TextMode="Date" /></div>
                <div><label>ช่องทางขาย</label>
                    <asp:DropDownList ID="ddlChannel" runat="server">
                        <asp:ListItem Value="" Text="— ทุกช่องทาง —" />
                        <asp:ListItem Value="POS" Text="ขายหน้าร้าน (POS)" />
                        <asp:ListItem Value="ROOM_SERVICE" Text="รูมเซอร์วิส" />
                        <asp:ListItem Value="RESERVATION" Text="ห้องพัก/การจอง" />
                        <asp:ListItem Value="OTA" Text="OTA" />
                        <asp:ListItem Value="OTHER" Text="อื่น ๆ" />
                    </asp:DropDownList>
                </div>
                <div><label>&nbsp;</label><asp:CheckBox ID="chkProblems" runat="server" Text=" เฉพาะรายการที่มีปัญหา" /></div>
                <div>
                    <asp:Button ID="btnShow" runat="server" Text="แสดงรายงาน" CssClass="btn btn-primary" OnClick="btnShow_Click" />
                    <asp:Button ID="btnCsv" runat="server" Text="ดาวน์โหลด CSV" CssClass="btn btn-default" OnClick="btnCsv_Click" />
                    <button type="button" class="btn btn-default" onclick="window.print(); return false;">พิมพ์</button>
                </div>
            </div>
        </div>

        <asp:Literal ID="litMsg" runat="server" />
        <asp:Literal ID="litStatus" runat="server" />
        <asp:Literal ID="litKpis" runat="server" />

        <div class="tr-card">
            <asp:Literal ID="litHeader" runat="server" />
            <div class="tr-scroll">
                <asp:Literal ID="litTable" runat="server" />
            </div>
        </div>

        <asp:Literal ID="litNotes" runat="server" />
    </div>
</asp:Content>
