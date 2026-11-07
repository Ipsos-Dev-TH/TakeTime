<%@ Page Title="กระทบยอด TakeTime กับ NextAcc" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="NextAccReconciliation.aspx.cs" Inherits="Take_Time_BangPhra.TaxReports.NextAccReconciliation" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <asp:Literal ID="litCss" runat="server" />

    <div class="tr-wrap">
        <div class="tr-head">
            <h2><i class="fas fa-balance-scale"></i> กระทบยอด TakeTime กับ NextAcc</h2>
            <p>เทียบยอดรายวันของ TakeTime กับเอกสาร/สมุดรายวันที่เข้าคิวส่ง NextAcc: รายได้ · ภาษีขาย · ต้นทุนขาย · ตัดจำหน่ายสต๊อก — พร้อมรายการคิวที่ล้มเหลว/ค้าง และการขายที่ยังไม่มีเอกสาร</p>
        </div>

        <div class="tr-card tr-noprint">
            <div class="tr-tools">
                <div><label>ตั้งแต่วันที่</label><asp:TextBox ID="txtFrom" runat="server" TextMode="Date" /></div>
                <div><label>ถึงวันที่</label><asp:TextBox ID="txtTo" runat="server" TextMode="Date" /></div>
                <div><label>ยอมให้ต่าง (บาท)</label><asp:TextBox ID="txtTol" runat="server" Text="1.00" Width="90px" /></div>
                <div><label>&nbsp;</label><asp:CheckBox ID="chkMismatchOnly" runat="server" Text=" เฉพาะวันที่ไม่ตรง" /></div>
                <div>
                    <asp:Button ID="btnShow" runat="server" Text="กระทบยอด" CssClass="btn btn-primary" OnClick="btnShow_Click" />
                    <asp:Button ID="btnCsv" runat="server" Text="ดาวน์โหลด CSV" CssClass="btn btn-default" OnClick="btnCsv_Click" />
                    <button type="button" class="btn btn-default" onclick="window.print(); return false;">พิมพ์</button>
                </div>
            </div>
        </div>

        <asp:Literal ID="litMsg" runat="server" />
        <asp:Literal ID="litStatus" runat="server" />
        <asp:Literal ID="litKpis" runat="server" />

        <div class="tr-card">
            <h3>ยอดรายวัน</h3>
            <div class="tr-scroll"><asp:Literal ID="litDays" runat="server" /></div>
        </div>

        <div class="tr-card">
            <h3>คิวที่ล้มเหลว / ค้าง</h3>
            <div class="tr-scroll"><asp:Literal ID="litQueue" runat="server" /></div>
        </div>

        <div class="tr-card">
            <h3>การขายที่ยังไม่มีเอกสาร NextAcc</h3>
            <div class="tr-scroll"><asp:Literal ID="litMissing" runat="server" /></div>
        </div>

        <asp:Literal ID="litNotes" runat="server" />
    </div>
</asp:Content>
