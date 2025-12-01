<%@ Page Title="จัดการข้อมูลพนักงาน" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="EmployeeManagement.aspx.cs" Inherits="Take_Time_BangPhra.Admin.HR.EmployeeManagement" MaintainScrollPositionOnPostback="true" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .employee-management {
            padding: 20px;
        }

        .section-header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 15px 20px;
            border-radius: 8px;
            margin-bottom: 20px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
        }

        .section-header h2 {
            margin: 0;
            font-size: 24px;
            font-weight: 600;
        }

        .stats-container {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
            gap: 20px;
            margin-bottom: 30px;
        }

        .stat-card {
            background: white;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            border-left: 4px solid #667eea;
        }

        .stat-card.warning {
            border-left-color: #f5576c;
        }

        .stat-card.success {
            border-left-color: #38ef7d;
        }

        .stat-card.info {
            border-left-color: #4facfe;
        }

        .stat-card h3 {
            margin: 0 0 10px 0;
            font-size: 14px;
            color: #666;
            font-weight: 500;
        }

        .stat-card .stat-value {
            font-size: 32px;
            font-weight: 700;
            color: #333;
        }

        .search-section {
            background: white;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            margin-bottom: 20px;
        }

        .search-controls {
            display: flex;
            gap: 10px;
            align-items: center;
            flex-wrap: wrap;
        }

        .search-controls .form-control {
            flex: 1;
            min-width: 200px;
            padding: 10px 15px;
            border: 2px solid #e0e0e0;
            border-radius: 6px;
            font-size: 14px;
        }

        .search-controls .form-control:focus {
            border-color: #667eea;
            outline: none;
        }

        .btn-primary {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            border: none;
            color: white;
            padding: 10px 25px;
            border-radius: 6px;
            font-weight: 500;
            cursor: pointer;
            transition: transform 0.2s;
        }

        .btn-primary:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(102, 126, 234, 0.4);
        }

        .btn-success {
            background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%);
            border: none;
            color: white;
            padding: 10px 25px;
            border-radius: 6px;
            font-weight: 500;
            cursor: pointer;
        }

        .btn-info {
            background: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%);
            border: none;
            color: white;
            padding: 8px 15px;
            border-radius: 6px;
            font-size: 13px;
            cursor: pointer;
        }

        .data-table {
            background: white;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            overflow: hidden;
        }

        .data-table table {
            width: 100%;
            border-collapse: collapse;
        }

        .data-table th {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 12px;
            text-align: left;
            font-weight: 500;
        }

        .data-table td {
            padding: 12px;
            border-bottom: 1px solid #f0f0f0;
        }

        .data-table tr:hover {
            background-color: #f8f9fa;
        }

        .badge {
            display: inline-block;
            padding: 4px 8px;
            border-radius: 4px;
            font-size: 11px;
            font-weight: 500;
        }

        .badge-success {
            background: #d4edda;
            color: #155724;
        }

        .badge-warning {
            background: #fff3cd;
            color: #856404;
        }

        .badge-danger {
            background: #f8d7da;
            color: #721c24;
        }

        .employee-photo {
            width: 40px;
            height: 40px;
            border-radius: 50%;
            object-fit: cover;
        }
    </style>

    <div class="employee-management">
        <div class="section-header">
            <h2>📋 ระบบจัดการข้อมูลพนักงาน</h2>
        </div>

        <!-- Statistics Cards -->
        <div class="stats-container">
            <div class="stat-card">
                <h3>พนักงานทั้งหมด</h3>
                <div class="stat-value">
                    <asp:Label ID="lblTotalEmployees" runat="server" Text="0"></asp:Label>
                </div>
            </div>
            <div class="stat-card success">
                <h3>พนักงานใหม่เดือนนี้</h3>
                <div class="stat-value">
                    <asp:Label ID="lblNewEmployees" runat="server" Text="0"></asp:Label>
                </div>
            </div>
            <div class="stat-card warning">
                <h3>สัญญาใกล้หมดอายุ (30 วัน)</h3>
                <div class="stat-value">
                    <asp:Label ID="lblExpiringContracts" runat="server" Text="0"></asp:Label>
                </div>
            </div>
            <div class="stat-card info">
                <h3>เอกสารใกล้หมดอายุ</h3>
                <div class="stat-value">
                    <asp:Label ID="lblExpiringDocuments" runat="server" Text="0"></asp:Label>
                </div>
            </div>
        </div>

        <!-- Search Section -->
        <div class="search-section">
            <h4 style="margin-top: 0;">🔍 ค้นหาพนักงาน</h4>
            <div class="search-controls">
                <asp:TextBox ID="txtSearch" runat="server" CssClass="form-control" placeholder="ชื่อ, เบอร์โทร, ตำแหน่ง..."></asp:TextBox>
                <asp:DropDownList ID="ddlDepartment" runat="server" CssClass="form-control">
                    <asp:ListItem Value="">ทุกแผนก</asp:ListItem>
                    <asp:ListItem Value="Front Desk">Front Desk</asp:ListItem>
                    <asp:ListItem Value="Housekeeping">Housekeeping</asp:ListItem>
                    <asp:ListItem Value="Kitchen">Kitchen</asp:ListItem>
                    <asp:ListItem Value="Management">Management</asp:ListItem>
                </asp:DropDownList>
                <asp:Button ID="btnSearch" runat="server" Text="ค้นหา" CssClass="btn-primary" OnClick="btnSearch_Click" />
                <asp:Button ID="btnReset" runat="server" Text="รีเซ็ต" CssClass="btn-success" OnClick="btnReset_Click" />
            </div>
        </div>

        <!-- Employee List -->
        <div class="data-table">
            <asp:GridView ID="gvEmployees" runat="server" AutoGenerateColumns="False"
                CssClass="table" GridLines="None" OnRowCommand="gvEmployees_RowCommand">
                <Columns>
                    <asp:TemplateField HeaderText="รูปภาพ">
                        <ItemTemplate>
                            <img src='<%# Eval("PhotoPath") != DBNull.Value ? ResolveUrl("~/" + Eval("PhotoPath")) : ResolveUrl("~/Images/default-avatar.png") %>'
                                 alt="Photo" class="employee-photo" />
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:BoundField DataField="Name" HeaderText="ชื่อ-นามสกุล" />
                    <asp:BoundField DataField="CurrentPosition" HeaderText="ตำแหน่ง" />
                    <asp:BoundField DataField="Department" HeaderText="แผนก" />

                    <asp:TemplateField HeaderText="อายุงาน">
                        <ItemTemplate>
                            <%# GetServiceAgeText(Eval("TotalServiceYears"), Eval("TotalServiceMonths")) %>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="เงินเดือน">
                        <ItemTemplate>
                            ฿<%# Eval("CurrentSalary") != DBNull.Value ? string.Format("{0:N2}", Eval("CurrentSalary")) : "N/A" %>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:BoundField DataField="MobilePhone" HeaderText="เบอร์โทร" />

                    <asp:TemplateField HeaderText="สถานะสัญญา">
                        <ItemTemplate>
                            <%# GetContractStatusBadge(Eval("ContractDaysUntilExpiry")) %>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="จัดการ">
                        <ItemTemplate>
                            <asp:Button ID="btnViewProfile" runat="server" Text="ดูข้อมูล"
                                CssClass="btn-info" CommandName="ViewProfile"
                                CommandArgument='<%# Eval("Admin_ID") %>' />
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
                <EmptyDataTemplate>
                    <div style="text-align: center; padding: 40px; color: #999;">
                        ไม่พบข้อมูลพนักงาน
                    </div>
                </EmptyDataTemplate>
            </asp:GridView>
        </div>
    </div>
</asp:Content>
