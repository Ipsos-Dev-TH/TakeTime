<%@ Page Title="ตรวจสอบ Voucher กับใบกำกับภาษี" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="MatchTaxInvoice.aspx.cs" Inherits="Take_Time_BangPhra.Voucher.MatchTaxInvoice" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .match-page { padding: 20px; }
        .match-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; flex-wrap: wrap; gap: 10px; }
        .match-stats { display: flex; gap: 12px; flex-wrap: wrap; }
        .stat-card { background: #fff; border: 1px solid #e1e4e8; border-radius: 8px; padding: 10px 16px; min-width: 120px; }
        .stat-card .num { font-size: 22px; font-weight: bold; }
        .stat-card .label { font-size: 12px; color: #6a737d; }
        .stat-card.missing { border-left: 4px solid #c0392b; }
        .stat-card.matched { border-left: 4px solid #27ae60; }
        .stat-card.total { border-left: 4px solid #3498db; }
        .filter-row { background: #f6f8fa; padding: 12px; border-radius: 6px; margin-bottom: 12px; display: flex; gap: 10px; align-items: center; flex-wrap: wrap; }
        .filter-row label { font-weight: 600; }
        .badge-status { display: inline-block; padding: 2px 8px; border-radius: 12px; font-size: 11px; font-weight: bold; }
        .badge-status.matched { background: #d4edda; color: #155724; }
        .badge-status.missing { background: #f8d7da; color: #721c24; }
        .gv-vouchers { width: 100%; border-collapse: collapse; background: #fff; }
        .gv-vouchers th { background: #2c3e50; color: #fff; padding: 8px; text-align: left; }
        .gv-vouchers td { padding: 6px 8px; border-bottom: 1px solid #e1e4e8; }
        .gv-vouchers tr:hover { background: #f6f8fa; }
        .btn-create-tax { background: #27ae60; color: #fff; border: none; padding: 4px 10px; border-radius: 4px; cursor: pointer; font-size: 12px; }
        .btn-create-tax:hover { background: #219a52; }
        .btn-create-tax:disabled { background: #95a5a6; cursor: not-allowed; }
        .modal-overlay { display: none; position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,0.5); z-index: 1000; }
        .modal-overlay.active { display: flex; align-items: center; justify-content: center; }
        .modal-box { background: #fff; padding: 24px; border-radius: 8px; max-width: 500px; width: 90%; }
        .modal-box h3 { margin-top: 0; color: #2c3e50; }
        .modal-row { margin-bottom: 12px; }
        .modal-row label { display: block; font-weight: 600; margin-bottom: 4px; }
        .modal-row input, .modal-row select { width: 100%; padding: 6px; border: 1px solid #d1d5da; border-radius: 4px; }
        .modal-actions { display: flex; gap: 8px; justify-content: flex-end; margin-top: 16px; }
        .btn { padding: 8px 16px; border-radius: 4px; cursor: pointer; border: none; }
        .btn-primary { background: #2c3e50; color: #fff; }
        .btn-secondary { background: #95a5a6; color: #fff; }
    </style>

    <div class="match-page">
        <div class="match-header">
            <h2>📋 ตรวจสอบ Voucher กับใบกำกับภาษี</h2>
            <div>
                <a href="/Voucher/VoucherList" class="btn btn-secondary" style="text-decoration:none;">← กลับ Voucher List</a>
            </div>
        </div>

        <div class="match-stats">
            <div class="stat-card total">
                <div class="num"><asp:Literal ID="litTotal" runat="server" Text="0" /></div>
                <div class="label">Voucher ทั้งหมด</div>
            </div>
            <div class="stat-card matched">
                <div class="num"><asp:Literal ID="litMatched" runat="server" Text="0" /></div>
                <div class="label">มีใบกำกับภาษี</div>
            </div>
            <div class="stat-card missing">
                <div class="num"><asp:Literal ID="litMissing" runat="server" Text="0" /></div>
                <div class="label">ขาดใบกำกับภาษี</div>
            </div>
        </div>

        <div class="filter-row">
            <label>แสดง:</label>
            <asp:DropDownList ID="ddlFilter" runat="server" AutoPostBack="True" OnSelectedIndexChanged="ddlFilter_Changed">
                <asp:ListItem Value="missing" Text="ขาดใบกำกับภาษีเท่านั้น" Selected="True" />
                <asp:ListItem Value="matched" Text="มีใบกำกับภาษีแล้ว" />
                <asp:ListItem Value="all" Text="ทั้งหมด" />
            </asp:DropDownList>

            <label>ตั้งแต่:</label>
            <asp:TextBox ID="txtStartDate" runat="server" TextMode="Date" />
            <label>ถึง:</label>
            <asp:TextBox ID="txtEndDate" runat="server" TextMode="Date" />
            <asp:Button ID="btnApply" runat="server" Text="ค้นหา" CssClass="btn btn-primary" OnClick="btnApply_Click" />

            <asp:TextBox ID="txtSearch" runat="server" placeholder="ค้นหาเลข Voucher / ลูกค้า..." Width="220" />
        </div>

        <asp:Label ID="lblMessage" runat="server" Visible="false" Style="display:block; padding:10px; margin-bottom:10px; border-radius:4px;" />

        <asp:GridView ID="gvVouchers" runat="server" AutoGenerateColumns="false" CssClass="gv-vouchers"
            OnRowCommand="gvVouchers_RowCommand" OnRowDataBound="gvVouchers_RowDataBound"
            DataKeyNames="Voucher_Number">
            <Columns>
                <asp:BoundField DataField="Voucher_Number" HeaderText="เลข Voucher" />
                <asp:BoundField DataField="Created_Date" HeaderText="วันที่ขาย" DataFormatString="{0:yyyy-MM-dd}" />
                <asp:BoundField DataField="Customer_Name" HeaderText="ลูกค้า" />
                <asp:BoundField DataField="Sell_Price" HeaderText="ราคาขาย" DataFormatString="{0:#,##0.00}" ItemStyle-HorizontalAlign="Right" />
                <asp:BoundField DataField="Receipt_ID" HeaderText="เลขใบกำกับภาษี" />
                <asp:TemplateField HeaderText="สถานะ">
                    <ItemTemplate>
                        <asp:Label ID="lblStatus" runat="server" />
                    </ItemTemplate>
                </asp:TemplateField>
                <asp:TemplateField HeaderText="การดำเนินการ">
                    <ItemTemplate>
                        <asp:Button ID="btnCreateTax" runat="server" Text="📝 สร้างใบกำกับภาษี"
                            CommandName="createTax"
                            CommandArgument='<%# Eval("Voucher_Number") %>'
                            CssClass="btn-create-tax"
                            OnClientClick="if(!confirm('สร้างใบกำกับภาษีสำหรับ Voucher นี้?\\nระบบจะใช้ข้อมูลจาก Voucher และวันที่ปัจจุบัน')) return false; this.disabled=true; this.value='⏳ กำลังสร้าง...';"
                            UseSubmitBehavior="false" />
                    </ItemTemplate>
                </asp:TemplateField>
            </Columns>
            <EmptyDataTemplate>
                <div style="padding:30px; text-align:center; color:#6a737d;">ไม่พบ Voucher ตามเงื่อนไขที่ระบุ</div>
            </EmptyDataTemplate>
        </asp:GridView>
    </div>
</asp:Content>
