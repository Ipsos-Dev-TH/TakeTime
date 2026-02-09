<%@ Page Title="" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="DisplayReserve.aspx.cs" Inherits="Take_Time_BangPhra.DisplayReserve" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link rel="stylesheet" href="/Content/jquery-ui.css">
    <link rel="stylesheet" href="/Content/style.css">
    <link rel="stylesheet" type="text/css" href="/Content/GridView2.css">
    <style>
        .header-center {
            text-align: center;
        }
        .header-right {
            text-align: right;
        }
        .wrap {
            white-space: normal;
            width: 100px;
        }
        th, td {
            padding: 8px;
            vertical-align: top;
            border: 1px solid #dee2e6;
        }
        .reservation-grid {
            font-size: 14px;
            border-collapse: collapse;
            width: 100%;
            margin-top: 20px;
        }
        .reservation-grid th {
            background-color: #343a40;
            color: white;
            position: sticky;
            top: 0;
            z-index: 10;
            font-weight: bold;
            text-align: center;
        }
        .reservation-grid td {
            border: 1px solid #ddd;
            vertical-align: top;
        }
        .date-column {
            width: 150px;
            font-weight: bold;
            background-color: #f8f9fa;
            text-align: center;
        }
        .accommodation-cell {
            min-height: 80px;
            min-width: 250px;
            max-width: 300px;
            background-color: white;
        }
        .customer-info {
            background-color: #e8f4fd;
            border-radius: 4px;
            padding: 8px;
            margin-bottom: 8px;
            border-left: 4px solid #2196F3;
            font-size: 12px;
        }
        .customer-name {
            font-weight: bold;
            color: #1976D2;
            font-size: 13px;
            margin-bottom: 3px;
        }
        .customer-details {
            font-size: 11px;
            color: #555;
            margin-bottom: 3px;
        }
        .payment-info {
            font-size: 11px;
            color: #388E3C;
            background-color: #E8F5E9;
            padding: 3px 6px;
            border-radius: 3px;
            margin: 3px 0;
        }
        .items-info {
            font-size: 11px;
            color: #7B1FA2;
            background-color: #F3E5F5;
            padding: 3px 6px;
            border-radius: 3px;
            margin: 3px 0;
        }
        .status-info {
            font-size: 10px;
            color: #FF9800;
            background-color: #FFF3E0;
            padding: 2px 5px;
            border-radius: 3px;
            margin: 2px 0;
            font-weight: bold;
        }
        .summary-box {
            background-color: #f8f9fa;
            border: 1px solid #dee2e6;
            border-radius: 5px;
            padding: 15px;
            margin-bottom: 20px;
        }
        .summary-label {
            font-weight: bold;
            color: #495057;
        }
        .summary-value {
            font-size: 18px;
            font-weight: bold;
            color: #28a745;
        }
        .filter-section {
            background-color: #f8f9fa;
            padding: 15px;
            border-radius: 5px;
            margin-bottom: 20px;
        }
        .empty-cell {
            background-color: #f9f9f9;
            color: #999;
            text-align: center;
            font-style: italic;
            min-height: 60px;
            vertical-align: middle;
        }
        .even-row {
            background-color: #ffffff;
        }
        .odd-row {
            background-color: #f8f9fa;
        }
        .grid-header {
            background-color: #343a40 !important;
            color: white;
        }
        .table-responsive {
            border: 1px solid #dee2e6;
            border-radius: 5px;
            overflow-x: auto;
        }
        .debug-info {
            background-color: #fff3cd;
            border: 1px solid #ffeaa7;
            border-radius: 5px;
            padding: 10px;
            margin: 10px 0;
            font-size: 12px;
            color: #856404;
        }
        @media (max-width: 768px) {
            .reservation-grid {
                font-size: 12px;
            }
            .date-column {
                width: 100px;
            }
            .accommodation-cell {
                min-width: 200px;
            }
        }
    </style>

    <div class="container-fluid">
        <h2 class="mb-4">รายการผู้เข้าพักรายเดือน</h2>
        
        <!-- Debug Information (optional) -->
        <asp:Panel ID="pnlDebug" runat="server" CssClass="debug-info" Visible="false">
            <strong>Debug Info:</strong> 
            Year: <asp:Label ID="lblDebugYear" runat="server" Text=""></asp:Label> | 
            Month: <asp:Label ID="lblDebugMonth" runat="server" Text=""></asp:Label> | 
            Found Records: <asp:Label ID="lblDebugRecords" runat="server" Text=""></asp:Label>
        </asp:Panel>
        
        <div class="filter-section">
            <div class="row align-items-center">
                <div class="col-md-3">
                    <div class="form-group">
                        <label for="DropDownList1">ปี:</label>
                        <asp:DropDownList ID="DropDownList1" runat="server" CssClass="form-control">
                        </asp:DropDownList>
                    </div>
                </div>
                <div class="col-md-3">
                    <div class="form-group">
                        <label for="DropDownList2">เดือน:</label>
                        <asp:DropDownList ID="DropDownList2" runat="server" CssClass="form-control">
                            <asp:ListItem Value="1">มกราคม</asp:ListItem>
                            <asp:ListItem Value="2">กุมภาพันธ์</asp:ListItem>
                            <asp:ListItem Value="3">มีนาคม</asp:ListItem>
                            <asp:ListItem Value="4">เมษายน</asp:ListItem>
                            <asp:ListItem Value="5">พฤษภาคม</asp:ListItem>
                            <asp:ListItem Value="6">มิถุนายน</asp:ListItem>
                            <asp:ListItem Value="7">กรกฎาคม</asp:ListItem>
                            <asp:ListItem Value="8">สิงหาคม</asp:ListItem>
                            <asp:ListItem Value="9">กันยายน</asp:ListItem>
                            <asp:ListItem Value="10">ตุลาคม</asp:ListItem>
                            <asp:ListItem Value="11">พฤศจิกายน</asp:ListItem>
                            <asp:ListItem Value="12">ธันวาคม</asp:ListItem>
                        </asp:DropDownList>
                    </div>
                </div>
                <div class="col-md-3">
                    <div class="form-group">
                        <label>&nbsp;</label>
                        <asp:Button ID="Button1" runat="server" Text="ค้นหา" OnClick="Button1_Click" CssClass="btn btn-primary btn-block" />
                    </div>
                </div>
                <div class="col-md-3">
                    <div class="form-group">
                        <label>&nbsp;</label>
                        <asp:Button ID="btnRefresh" runat="server" Text="รีเฟรช" OnClick="Button1_Click" CssClass="btn btn-outline-secondary btn-block" />
                    </div>
                </div>
            </div>
        </div>

        <div class="summary-box">
            <div class="row">
                <div class="col-md-4">
                    <div class="text-center">
                        <div class="summary-label">ยอดเงินทั้งหมด</div>
                        <div class="summary-value">
                            <asp:Label ID="Label4" runat="server" Text="0"></asp:Label> บาท
                        </div>
                    </div>
                </div>
                <div class="col-md-4">
                    <div class="text-center">
                        <div class="summary-label">ยอดเงินรับมา</div>
                        <div class="summary-value">
                            <asp:Label ID="Label5" runat="server" Text="0"></asp:Label> บาท
                        </div>
                    </div>
                </div>
                <div class="col-md-4">
                    <div class="text-center">
                        <div class="summary-label">ยอดค้างชำระ</div>
                        <div class="summary-value">
                            <asp:Label ID="Label6" runat="server" Text="0"></asp:Label> บาท
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <div class="mobile-table-wrapper table-responsive">
            <asp:GridView ID="GridView1" runat="server" Width="100%" CssClass="reservation-grid gridview-table" 
                PagerStyle-CssClass="pager" HeaderStyle-CssClass="header" RowStyle-CssClass="rows" 
                AlternatingRowStyle-Wrap="true" EditRowStyle-Wrap="true" RowStyle-Wrap="true"
                AutoGenerateColumns="false" OnRowDataBound="GridView1_RowDataBound">
                <HeaderStyle CssClass="grid-header" />
                <RowStyle CssClass="rows" />
                <AlternatingRowStyle BackColor="#f8f9fa" />
            </asp:GridView>
        </div>

        <!-- Status Message -->
        <asp:Panel ID="pnlMessage" runat="server" CssClass="alert alert-info mt-3" Visible="false">
            <asp:Label ID="lblMessage" runat="server" Text=""></asp:Label>
        </asp:Panel>
    </div>

    <script type="text/javascript">
        function showLoading() {
            // คุณสามารถเพิ่ม loading indicator ได้ที่นี่
            console.log('Loading reservation data...');
        }
    </script>
</asp:Content>