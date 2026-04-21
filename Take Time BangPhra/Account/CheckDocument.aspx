<%@ Page MaintainScrollPositionOnPostback="true" Title="ตรวจสอบเอกสาร" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="CheckDocument.aspx.cs" Inherits="Take_Time_BangPhra.Account.CheckDocument" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .check-document-page {
            padding: 20px;
            max-width: 1400px;
            margin: 0 auto;
        }

        .page-header {
            background: linear-gradient(135deg, #5D4037 0%, #8D6E63 100%);
            color: white;
            padding: 20px 25px;
            border-radius: 12px;
            margin-bottom: 20px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
        }

        .page-header h2 {
            margin: 0;
            font-size: 1.5em;
        }

        .filter-card {
            background: white;
            border-radius: 12px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.08);
            padding: 20px;
            margin-bottom: 20px;
        }

        .filter-row {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 15px;
            margin-bottom: 15px;
        }

        .filter-group label {
            display: block;
            margin-bottom: 5px;
            font-weight: 500;
            color: #5D4037;
            font-size: 14px;
        }

        .filter-group select,
        .filter-group input {
            width: 100%;
            padding: 10px 15px;
            border: 2px solid #e0e0e0;
            border-radius: 8px;
            font-size: 14px;
            min-height: 44px;
        }

        .filter-group select:focus,
        .filter-group input:focus {
            border-color: #5D4037;
            outline: none;
        }

        .button-row {
            display: flex;
            gap: 10px;
            flex-wrap: wrap;
            margin-top: 15px;
        }

        .btn-search {
            background: linear-gradient(135deg, #5D4037 0%, #8D6E63 100%);
            color: white;
            border: none;
            padding: 12px 25px;
            border-radius: 8px;
            cursor: pointer;
            font-weight: 500;
        }

        .btn-export {
            background: linear-gradient(135deg, #4CAF50 0%, #388E3C 100%);
            color: white;
            border: none;
            padding: 12px 25px;
            border-radius: 8px;
            cursor: pointer;
        }

        .summary-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
            gap: 15px;
            margin-bottom: 20px;
        }

        .summary-card {
            background: white;
            border-radius: 10px;
            padding: 15px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.08);
            border-left: 4px solid #5D4037;
        }

        .summary-card h4 {
            margin: 0 0 8px 0;
            color: #666;
            font-size: 12px;
        }

        .summary-card .value {
            font-size: 1.3em;
            font-weight: 700;
            color: #5D4037;
        }

        .summary-card.cash { border-left-color: #4CAF50; }
        .summary-card.transfer { border-left-color: #2196F3; }
        .summary-card.director { border-left-color: #FF9800; }
        .summary-card.tax { border-left-color: #9C27B0; }
        .summary-card.count { border-left-color: #E91E63; }

        .data-section {
            background: white;
            border-radius: 12px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.08);
            overflow: hidden;
        }

        .mobile-table-wrapper {
            width: 100%;
            overflow-x: auto;
            -webkit-overflow-scrolling: touch;
        }

        .mobile-table-wrapper table,
        .mobile-table-wrapper #GridView1 {
            min-width: 800px;
            width: 100%;
            border-collapse: collapse;
        }

        .mobile-table-wrapper th {
            background: linear-gradient(135deg, #5D4037 0%, #8D6E63 100%);
            color: white;
            padding: 12px;
            text-align: left;
            font-weight: 500;
            white-space: nowrap;
        }

        .mobile-table-wrapper td {
            padding: 10px 12px;
            border-bottom: 1px solid #eee;
        }

        .mobile-table-wrapper tr:hover {
            background: #FFF8E1;
        }

        .mobile-table-wrapper input[type="button"],
        .mobile-table-wrapper button {
            padding: 6px 12px;
            border-radius: 5px;
            cursor: pointer;
            margin: 2px;
            font-size: 12px;
        }

        .sync-badge {
            display: inline-block;
            padding: 3px 10px;
            border-radius: 12px;
            font-size: 11px;
            font-weight: 600;
            white-space: nowrap;
        }
        .sync-badge.completed { background: #d4edda; color: #155724; }
        .sync-badge.pending { background: #fff3cd; color: #856404; }
        .sync-badge.failed { background: #f8d7da; color: #721c24; }
        .sync-badge.none { background: #e2e3e5; color: #383d41; }
        .btn-sync-sm {
            background: linear-gradient(135deg, #3498db 0%, #2980b9 100%);
            color: white;
            padding: 4px 10px;
            border: none;
            border-radius: 5px;
            font-size: 11px;
            cursor: pointer;
            white-space: nowrap;
        }

        @media (max-width: 768px) {
            .check-document-page {
                padding: 10px;
            }

            .filter-row {
                grid-template-columns: 1fr;
            }

            .summary-grid {
                grid-template-columns: repeat(2, 1fr);
            }

            .mobile-table-wrapper {
                margin: 0 -10px;
                overflow-x: scroll !important;
                -webkit-overflow-scrolling: touch !important;
            }

            .mobile-table-wrapper::before {
                content: '👆 เลื่อนซ้าย-ขวา';
                display: block;
                text-align: center;
                padding: 8px;
                font-size: 12px;
                color: #fff;
                background: linear-gradient(135deg, #5D4037, #8D6E63);
            }

            .button-row {
                flex-direction: column;
            }

            .button-row .btn-search,
            .button-row .btn-export {
                width: 100%;
            }
        }
    </style>

    <div class="check-document-page">
        <div class="page-header">
            <h2>📋 ตรวจสอบเอกสาร</h2>
        </div>

        <div class="filter-card">
            <div class="filter-row">
                <div class="filter-group">
                    <label>ประเภทเอกสาร</label>
                    <asp:DropDownList ID="DropDownList1" runat="server">
                        <asp:ListItem Selected="True" Value="Account_Payment">ใบสำคัญจ่าย</asp:ListItem>
                        <asp:ListItem Value="Account_Receipt">ใบเสร็จรับเงิน</asp:ListItem>
                        <asp:ListItem Value="P&L">งบกำไรขาดทุน</asp:ListItem>
                        <asp:ListItem Value="Detail_Account_Payment">รายละเอียดใบสำคัญจ่าย</asp:ListItem>
                        <asp:ListItem Value="Detail_Account_Receipt">รายละเอียดใบเสร็จรับเงิน</asp:ListItem>
                    </asp:DropDownList>
                </div>
                <div class="filter-group">
                    <label>สถานะ</label>
                    <asp:DropDownList ID="DropDownList2" runat="server">
                        <asp:ListItem Selected="True" Value="%">ทั้งหมด</asp:ListItem>
                        <asp:ListItem Value="Normal">ปกติ</asp:ListItem>
                        <asp:ListItem Value="Cancel">ยกเลิก</asp:ListItem>
                    </asp:DropDownList>
                </div>
            </div>

            <div class="filter-row">
                <div class="filter-group">
                    <label>วันที่เริ่มต้น</label>
                    <asp:TextBox ID="TextBox1" runat="server" TextMode="Date"></asp:TextBox>
                </div>
                <div class="filter-group">
                    <label>วันที่สิ้นสุด</label>
                    <asp:TextBox ID="TextBox2" runat="server" TextMode="Date"></asp:TextBox>
                </div>
                <div class="filter-group">
                    <label>เดือน</label>
                    <asp:DropDownList ID="DropDownList3" runat="server">
                        <asp:ListItem>--เลือกเดือน--</asp:ListItem>
                        <asp:ListItem>1</asp:ListItem>
                        <asp:ListItem>2</asp:ListItem>
                        <asp:ListItem>3</asp:ListItem>
                        <asp:ListItem>4</asp:ListItem>
                        <asp:ListItem>5</asp:ListItem>
                        <asp:ListItem>6</asp:ListItem>
                        <asp:ListItem>7</asp:ListItem>
                        <asp:ListItem>8</asp:ListItem>
                        <asp:ListItem>9</asp:ListItem>
                        <asp:ListItem>10</asp:ListItem>
                        <asp:ListItem>11</asp:ListItem>
                        <asp:ListItem>12</asp:ListItem>
                    </asp:DropDownList>
                </div>
                <div class="filter-group">
                    <label>ปี</label>
                    <asp:DropDownList ID="DropDownList4" runat="server">
                    </asp:DropDownList>
                </div>
            </div>

            <div class="button-row">
                <asp:Button ID="Button2" runat="server" Text="🔍 ค้นหา" CssClass="btn-search" OnClick="Button2_Click" />
                <asp:Button ID="Button3" runat="server" Text="📥 Export CSV" CssClass="btn-export" OnClick="Button3_Click" />
                <asp:CheckBox ID="CheckBox1" runat="server" Text=" แสดงปุ่มลบ" />
            </div>
        </div>

        <div class="summary-grid">
            <div class="summary-card cash">
                <h4>💵 ยอดรวมเงินสด</h4>
                <div class="value"><asp:Label ID="Label10" runat="server" Text="0"></asp:Label></div>
            </div>
            <div class="summary-card transfer">
                <h4>💳 ยอดรวมเงินโอน</h4>
                <div class="value"><asp:Label ID="Label11" runat="server" Text="0"></asp:Label></div>
            </div>
            <div class="summary-card director">
                <h4>👔 ยอดเงินกรรมการ</h4>
                <div class="value"><asp:Label ID="Label13" runat="server" Text="0"></asp:Label></div>
            </div>
            <div class="summary-card tax">
                <h4>📊 ยอดรวมภาษี</h4>
                <div class="value"><asp:Label ID="Label12" runat="server" Text="0"></asp:Label></div>
            </div>
            <div class="summary-card count">
                <h4>📄 จำนวนเอกสาร</h4>
                <div class="value"><asp:Label ID="Label18" runat="server" Text="0"></asp:Label></div>
            </div>
        </div>

        <!-- Hidden labels for additional data if needed -->
        <asp:Label ID="Label14" runat="server" Visible="false"></asp:Label>
        <asp:Label ID="Label15" runat="server" Visible="false"></asp:Label>
        <asp:Label ID="Label16" runat="server" Visible="false"></asp:Label>
        <asp:Label ID="Label17" runat="server" Visible="false"></asp:Label>

        <div class="data-section">
            <div class="mobile-table-wrapper">
                <asp:GridView ID="GridView1" runat="server" CssClass="gridview-table"
                    OnRowDeleting="GridView1_RowDeleting1"
                    OnSelectedIndexChanging="GridView1_SelectedIndexChanging"
                    OnRowCommand="GridView1_RowCommand"
                    OnRowDataBound="GridView1_RowDataBound">
                    <Columns>
                        <asp:CommandField ButtonType="Button" HeaderText="ลบ" ShowDeleteButton="True" />
                        <asp:CommandField ButtonType="Button" HeaderText="ดู" SelectText="ดู" ShowSelectButton="True" />
                        <asp:ButtonField ButtonType="Button" CommandName="edit" Text="แก้ไข" />
                    </Columns>
                </asp:GridView>
            </div>
        </div>
    </div>
</asp:Content>