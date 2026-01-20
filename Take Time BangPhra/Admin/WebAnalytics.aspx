<%@ Page Title="Web Analytics" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="WebAnalytics.aspx.cs" Inherits="Take_Time_BangPhra.Admin.WebAnalytics" MaintainScrollPositionOnPostback="true" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .analytics-page {
            padding: 20px;
            max-width: 1600px;
            margin: 0 auto;
        }

        .page-header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 20px 25px;
            border-radius: 10px;
            margin-bottom: 25px;
            box-shadow: 0 4px 15px rgba(102, 126, 234, 0.3);
        }

        .page-header h1 {
            margin: 0;
            font-size: 28px;
            font-weight: 600;
        }

        .page-header p {
            margin: 5px 0 0 0;
            opacity: 0.9;
        }

        /* Filter Section */
        .filter-section {
            background: white;
            padding: 20px;
            border-radius: 10px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
            margin-bottom: 25px;
        }

        .filter-row {
            display: flex;
            gap: 20px;
            flex-wrap: wrap;
            align-items: flex-end;
        }

        .filter-group {
            display: flex;
            flex-direction: column;
            gap: 5px;
        }

        .filter-group label {
            font-size: 13px;
            color: #666;
            font-weight: 500;
        }

        .form-control {
            padding: 10px 15px;
            border: 2px solid #e0e0e0;
            border-radius: 6px;
            font-size: 14px;
            min-width: 150px;
            white-space: nowrap;
        }

        .form-control:focus {
            border-color: #667eea;
            outline: none;
        }

        /* Dropdown specific styles */
        select.form-control {
            min-width: 180px;
            padding-right: 30px;
            appearance: none;
            -webkit-appearance: none;
            -moz-appearance: none;
            background: white url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='%23666'%3E%3Cpath d='M7 10l5 5 5-5z'/%3E%3C/svg%3E") no-repeat right 8px center;
            background-size: 20px;
            cursor: pointer;
        }

        select.form-control option {
            padding: 10px;
        }

        /* Stats Cards */
        .stats-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 20px;
            margin-bottom: 25px;
        }

        .stat-card {
            background: white;
            padding: 25px;
            border-radius: 10px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
            text-align: center;
            border-top: 4px solid #667eea;
            transition: transform 0.3s;
        }

        .stat-card:hover {
            transform: translateY(-5px);
        }

        .stat-card.green { border-top-color: #27ae60; }
        .stat-card.orange { border-top-color: #f39c12; }
        .stat-card.red { border-top-color: #e74c3c; }
        .stat-card.purple { border-top-color: #9b59b6; }
        .stat-card.blue { border-top-color: #3498db; }

        .stat-card h4 {
            margin: 0 0 10px 0;
            font-size: 13px;
            color: #666;
            text-transform: uppercase;
            letter-spacing: 0.5px;
        }

        .stat-card .value {
            font-size: 36px;
            font-weight: 700;
            color: #333;
        }

        .stat-card .sub-value {
            font-size: 13px;
            color: #999;
            margin-top: 5px;
        }

        /* Tab Navigation */
        .tab-navigation {
            display: flex;
            gap: 5px;
            margin-bottom: 20px;
            border-bottom: 2px solid #e0e0e0;
            padding-bottom: 0;
            flex-wrap: wrap;
            overflow-x: auto;
        }

        .tab-btn {
            background: #f5f5f5;
            border: none;
            padding: 12px 20px;
            font-size: 13px;
            font-weight: 500;
            color: #666;
            cursor: pointer;
            border-radius: 8px 8px 0 0;
            transition: all 0.3s ease;
            margin-bottom: -2px;
            white-space: nowrap;
        }

        .tab-btn:hover {
            background: #e0e0e0;
        }

        .tab-btn.active {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
        }

        .tab-content {
            display: none;
        }

        .tab-content.active {
            display: block;
        }

        /* Data Cards */
        .data-card {
            background: white;
            border-radius: 10px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
            margin-bottom: 20px;
            overflow: hidden;
        }

        .data-card-header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 15px 20px;
            font-weight: 600;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .data-card-body {
            padding: 20px;
            overflow-x: auto;
            -webkit-overflow-scrolling: touch;
        }

        /* Tables */
        .data-table {
            width: 100%;
            border-collapse: collapse;
            min-width: 600px;
        }

        .data-table th {
            background: #f8f9fa;
            padding: 12px 15px;
            text-align: left;
            font-weight: 600;
            font-size: 13px;
            color: #333;
            border-bottom: 2px solid #e0e0e0;
        }

        .data-table td {
            padding: 12px 15px;
            border-bottom: 1px solid #f0f0f0;
            font-size: 13px;
        }

        .data-table tr:hover {
            background-color: #f8f9ff;
        }

        /* Progress Bar */
        .progress-bar-container {
            background: #e0e0e0;
            border-radius: 10px;
            height: 20px;
            overflow: hidden;
        }

        .progress-bar {
            height: 100%;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            border-radius: 10px;
            display: flex;
            align-items: center;
            justify-content: center;
            color: white;
            font-size: 11px;
            font-weight: 600;
        }

        /* Chart Container */
        .chart-container {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(400px, 1fr));
            gap: 20px;
            margin-bottom: 20px;
        }

        .chart-card {
            background: white;
            border-radius: 10px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
            padding: 20px;
        }

        .chart-card h3 {
            margin: 0 0 20px 0;
            font-size: 16px;
            color: #333;
        }

        /* Hour Chart */
        .hour-chart {
            display: flex;
            align-items: flex-end;
            gap: 3px;
            height: 150px;
        }

        .hour-bar {
            flex: 1;
            background: linear-gradient(180deg, #667eea 0%, #764ba2 100%);
            border-radius: 3px 3px 0 0;
            min-width: 15px;
            position: relative;
        }

        .hour-bar:hover {
            opacity: 0.8;
        }

        .hour-label {
            font-size: 10px;
            text-align: center;
            color: #999;
            margin-top: 5px;
        }

        /* Day Chart */
        .day-chart {
            display: flex;
            gap: 10px;
            justify-content: space-between;
        }

        .day-item {
            text-align: center;
            flex: 1;
        }

        .day-bar-container {
            height: 120px;
            display: flex;
            align-items: flex-end;
            justify-content: center;
        }

        .day-bar {
            width: 40px;
            background: linear-gradient(180deg, #27ae60 0%, #2ecc71 100%);
            border-radius: 5px 5px 0 0;
        }

        .day-label {
            font-size: 12px;
            color: #666;
            margin-top: 8px;
            font-weight: 500;
        }

        .day-value {
            font-size: 11px;
            color: #999;
            margin-top: 3px;
        }

        /* Badges */
        .badge {
            display: inline-block;
            padding: 4px 10px;
            border-radius: 20px;
            font-size: 11px;
            font-weight: 500;
        }

        .badge-error { background: #ffeef0; color: #e74c3c; }
        .badge-warning { background: #fff8e6; color: #f39c12; }
        .badge-info { background: #e8f4fd; color: #3498db; }
        .badge-success { background: #e8f8ef; color: #27ae60; }
        .badge-critical { background: #e74c3c; color: white; }

        /* Buttons */
        .btn {
            padding: 10px 20px;
            border: none;
            border-radius: 6px;
            font-weight: 500;
            cursor: pointer;
            font-size: 14px;
            transition: all 0.3s;
        }

        .btn-primary {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
        }

        .btn-success {
            background: linear-gradient(135deg, #27ae60 0%, #2ecc71 100%);
            color: white;
        }

        .btn:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(0,0,0,0.2);
        }

        /* Mobile */
        @media (max-width: 768px) {
            .analytics-page {
                padding: 10px;
            }
            .stats-grid {
                grid-template-columns: repeat(2, 1fr);
            }
            .filter-row {
                flex-direction: column;
            }
            .filter-group {
                width: 100%;
            }
            .filter-group .form-control {
                width: 100%;
            }
            .chart-container {
                grid-template-columns: 1fr;
            }
            .tab-btn {
                padding: 10px 15px;
                font-size: 12px;
            }
        }
    </style>

    <div class="analytics-page">
        <div class="page-header">
            <h1><i class="fas fa-chart-line"></i> Web Analytics</h1>
            <p>วิเคราะห์การเข้าใช้งานเว็บไซต์</p>
        </div>

        <!-- Filter Section -->
        <div class="filter-section">
            <div class="filter-row">
                <div class="filter-group">
                    <label>วันที่เริ่มต้น</label>
                    <asp:TextBox ID="txtStartDate" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                </div>
                <div class="filter-group">
                    <label>วันที่สิ้นสุด</label>
                    <asp:TextBox ID="txtEndDate" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                </div>
                <div class="filter-group">
                    <label>ช่วงเวลา</label>
                    <asp:DropDownList ID="ddlDateRange" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlDateRange_SelectedIndexChanged">
                        <asp:ListItem Value="today">วันนี้</asp:ListItem>
                        <asp:ListItem Value="yesterday">เมื่อวาน</asp:ListItem>
                        <asp:ListItem Value="7days" Selected="True">7 วันที่ผ่านมา</asp:ListItem>
                        <asp:ListItem Value="30days">30 วันที่ผ่านมา</asp:ListItem>
                        <asp:ListItem Value="thismonth">เดือนนี้</asp:ListItem>
                        <asp:ListItem Value="lastmonth">เดือนที่แล้ว</asp:ListItem>
                        <asp:ListItem Value="thisyear">ปีนี้</asp:ListItem>
                        <asp:ListItem Value="custom">กำหนดเอง</asp:ListItem>
                    </asp:DropDownList>
                </div>
                <div class="filter-group">
                    <label>ประเภทผู้ใช้</label>
                    <asp:DropDownList ID="ddlUserType" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlUserType_SelectedIndexChanged">
                        <asp:ListItem Value="0">ทั้งหมด</asp:ListItem>
                        <asp:ListItem Value="1">ทีมงาน (Staff)</asp:ListItem>
                        <asp:ListItem Value="2">ลูกค้า (Customer)</asp:ListItem>
                    </asp:DropDownList>
                </div>
                <div class="filter-group">
                    <label>&nbsp;</label>
                    <asp:Button ID="btnApplyFilter" runat="server" Text="ค้นหา" CssClass="btn btn-primary" OnClick="btnApplyFilter_Click" />
                </div>
            </div>
        </div>

        <!-- Stats Cards -->
        <div class="stats-grid">
            <div class="stat-card">
                <h4>ผู้เข้าชมทั้งหมด</h4>
                <div class="value"><asp:Label ID="lblTotalVisits" runat="server" Text="0"></asp:Label></div>
                <div class="sub-value">ในช่วงเวลาที่เลือก</div>
            </div>
            <div class="stat-card green">
                <h4>ผู้เข้าชมไม่ซ้ำ</h4>
                <div class="value"><asp:Label ID="lblUniqueVisitors" runat="server" Text="0"></asp:Label></div>
                <div class="sub-value">Unique Visitors</div>
            </div>
            <div class="stat-card blue">
                <h4>วันนี้</h4>
                <div class="value"><asp:Label ID="lblTodayVisits" runat="server" Text="0"></asp:Label></div>
                <div class="sub-value">ผู้เข้าชม</div>
            </div>
            <div class="stat-card orange">
                <h4>7 วันที่ผ่านมา</h4>
                <div class="value"><asp:Label ID="lblWeeklyVisits" runat="server" Text="0"></asp:Label></div>
                <div class="sub-value">ผู้เข้าชม</div>
            </div>
            <div class="stat-card purple">
                <h4>30 วันที่ผ่านมา</h4>
                <div class="value"><asp:Label ID="lblMonthlyVisits" runat="server" Text="0"></asp:Label></div>
                <div class="sub-value">ผู้เข้าชม</div>
            </div>
            <div class="stat-card red">
                <h4>ข้อผิดพลาด</h4>
                <div class="value"><asp:Label ID="lblErrorCount" runat="server" Text="0"></asp:Label></div>
                <div class="sub-value">Error Logs</div>
            </div>
        </div>

        <!-- Staff vs Customer Summary -->
        <div class="data-card" style="margin-bottom: 25px;">
            <div class="data-card-header">
                <span><i class="fas fa-users-cog"></i> สรุปทีมงาน vs ลูกค้า</span>
            </div>
            <div class="data-card-body">
                <div style="display: flex; gap: 30px; flex-wrap: wrap;">
                    <div style="flex: 1; min-width: 200px; text-align: center; padding: 20px; background: linear-gradient(135deg, #3498db 0%, #2980b9 100%); border-radius: 10px; color: white;">
                        <div style="font-size: 14px; opacity: 0.9; margin-bottom: 10px;">ทีมงาน (Staff)</div>
                        <div style="font-size: 32px; font-weight: 700;"><asp:Label ID="lblStaffVisits" runat="server" Text="0"></asp:Label></div>
                        <div style="font-size: 13px; opacity: 0.8; margin-top: 5px;"><asp:Label ID="lblStaffPercent" runat="server" Text="0%"></asp:Label> | <asp:Label ID="lblStaffUnique" runat="server" Text="0"></asp:Label> IPs</div>
                    </div>
                    <div style="flex: 1; min-width: 200px; text-align: center; padding: 20px; background: linear-gradient(135deg, #27ae60 0%, #2ecc71 100%); border-radius: 10px; color: white;">
                        <div style="font-size: 14px; opacity: 0.9; margin-bottom: 10px;">ลูกค้า (Customer)</div>
                        <div style="font-size: 32px; font-weight: 700;"><asp:Label ID="lblCustomerVisits" runat="server" Text="0"></asp:Label></div>
                        <div style="font-size: 13px; opacity: 0.8; margin-top: 5px;"><asp:Label ID="lblCustomerPercent" runat="server" Text="0%"></asp:Label> | <asp:Label ID="lblCustomerUnique" runat="server" Text="0"></asp:Label> IPs</div>
                    </div>
                </div>
            </div>
        </div>

        <!-- Tab Navigation -->
        <div class="tab-navigation">
            <asp:Button ID="btnTabOverview" runat="server" Text="ภาพรวม" CssClass="tab-btn active" OnClick="btnTabOverview_Click" />
            <asp:Button ID="btnTabBrowser" runat="server" Text="เบราว์เซอร์" CssClass="tab-btn" OnClick="btnTabBrowser_Click" />
            <asp:Button ID="btnTabTime" runat="server" Text="ช่วงเวลา" CssClass="tab-btn" OnClick="btnTabTime_Click" />
            <asp:Button ID="btnTabVisitors" runat="server" Text="ผู้เข้าชม" CssClass="tab-btn" OnClick="btnTabVisitors_Click" />
            <asp:Button ID="btnTabActivity" runat="server" Text="กิจกรรม" CssClass="tab-btn" OnClick="btnTabActivity_Click" />
            <asp:Button ID="btnTabErrors" runat="server" Text="ข้อผิดพลาด" CssClass="tab-btn" OnClick="btnTabErrors_Click" />
            <asp:Button ID="btnTabAccessLog" runat="server" Text="Access Log" CssClass="tab-btn" OnClick="btnTabAccessLog_Click" />
        </div>

        <asp:HiddenField ID="hdnActiveTab" runat="server" Value="overview" />

        <!-- Tab 1: Overview -->
        <asp:Panel ID="pnlOverview" runat="server" CssClass="tab-content active">
            <div class="chart-container">
                <div class="chart-card">
                    <h3><i class="fas fa-chart-area"></i> แนวโน้มการเข้าชมรายวัน</h3>
                    <asp:GridView ID="gvDailyTrend" runat="server" AutoGenerateColumns="False" CssClass="data-table" GridLines="None">
                        <Columns>
                            <asp:TemplateField HeaderText="วันที่">
                                <ItemTemplate>
                                    <%# Convert.ToDateTime(Eval("VisitDate")).ToString("dd/MM/yyyy") %>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:BoundField DataField="VisitCount" HeaderText="ผู้เข้าชม" />
                            <asp:BoundField DataField="UniqueVisitors" HeaderText="ไม่ซ้ำ" />
                            <asp:TemplateField HeaderText="กราฟ">
                                <ItemTemplate>
                                    <div class="progress-bar-container">
                                        <div class="progress-bar" style='width: <%# GetPercentage(Eval("VisitCount")) %>%'>
                                            <%# Eval("VisitCount") %>
                                        </div>
                                    </div>
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                    </asp:GridView>
                </div>

                <div class="chart-card">
                    <h3><i class="fas fa-calendar-week"></i> วันในสัปดาห์</h3>
                    <asp:GridView ID="gvDayOfWeek" runat="server" AutoGenerateColumns="False" CssClass="data-table" GridLines="None">
                        <Columns>
                            <asp:BoundField DataField="DayName" HeaderText="วัน" />
                            <asp:BoundField DataField="VisitCount" HeaderText="ผู้เข้าชม" />
                            <asp:TemplateField HeaderText="กราฟ">
                                <ItemTemplate>
                                    <div class="progress-bar-container">
                                        <div class="progress-bar" style='width: <%# GetDayPercentage(Eval("VisitCount")) %>%'>
                                            <%# Eval("VisitCount") %>
                                        </div>
                                    </div>
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                    </asp:GridView>
                </div>
            </div>
        </asp:Panel>

        <!-- Tab 2: Browser -->
        <asp:Panel ID="pnlBrowser" runat="server" CssClass="tab-content">
            <div class="data-card">
                <div class="data-card-header">
                    <span><i class="fas fa-globe"></i> สถิติเบราว์เซอร์</span>
                </div>
                <div class="data-card-body">
                    <asp:GridView ID="gvBrowser" runat="server" AutoGenerateColumns="False" CssClass="data-table" GridLines="None">
                        <Columns>
                            <asp:BoundField DataField="Browser" HeaderText="เบราว์เซอร์" />
                            <asp:BoundField DataField="VisitCount" HeaderText="จำนวน" />
                            <asp:TemplateField HeaderText="สัดส่วน">
                                <ItemTemplate>
                                    <div class="progress-bar-container" style="width: 200px;">
                                        <div class="progress-bar" style='width: <%# Eval("Percentage") %>%'>
                                            <%# Eval("Percentage") %>%
                                        </div>
                                    </div>
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                        <EmptyDataTemplate>
                            <div style="text-align: center; padding: 40px; color: #999;">
                                ไม่พบข้อมูลเบราว์เซอร์
                            </div>
                        </EmptyDataTemplate>
                    </asp:GridView>
                </div>
            </div>
        </asp:Panel>

        <!-- Tab 3: Time Analysis -->
        <asp:Panel ID="pnlTime" runat="server" CssClass="tab-content">
            <div class="chart-container">
                <div class="data-card">
                    <div class="data-card-header">
                        <span><i class="fas fa-clock"></i> การเข้าชมตามชั่วโมง</span>
                    </div>
                    <div class="data-card-body">
                        <asp:GridView ID="gvHourly" runat="server" AutoGenerateColumns="False" CssClass="data-table" GridLines="None">
                            <Columns>
                                <asp:TemplateField HeaderText="ชั่วโมง">
                                    <ItemTemplate>
                                        <%# Eval("Hour") %>:00 - <%# Convert.ToInt32(Eval("Hour")) + 1 %>:00
                                    </ItemTemplate>
                                </asp:TemplateField>
                                <asp:BoundField DataField="VisitCount" HeaderText="ผู้เข้าชม" />
                                <asp:TemplateField HeaderText="กราฟ">
                                    <ItemTemplate>
                                        <div class="progress-bar-container" style="width: 200px;">
                                            <div class="progress-bar" style='width: <%# GetHourPercentage(Eval("VisitCount")) %>%'>
                                                <%# Eval("VisitCount") %>
                                            </div>
                                        </div>
                                    </ItemTemplate>
                                </asp:TemplateField>
                            </Columns>
                        </asp:GridView>
                    </div>
                </div>

                <div class="data-card">
                    <div class="data-card-header">
                        <span><i class="fas fa-calendar-alt"></i> สรุปรายเดือน</span>
                    </div>
                    <div class="data-card-body">
                        <asp:GridView ID="gvMonthly" runat="server" AutoGenerateColumns="False" CssClass="data-table" GridLines="None">
                            <Columns>
                                <asp:BoundField DataField="MonthName" HeaderText="เดือน" />
                                <asp:BoundField DataField="VisitCount" HeaderText="ผู้เข้าชม" />
                                <asp:BoundField DataField="UniqueVisitors" HeaderText="ไม่ซ้ำ" />
                            </Columns>
                        </asp:GridView>
                    </div>
                </div>
            </div>
        </asp:Panel>

        <!-- Tab 4: Visitors -->
        <asp:Panel ID="pnlVisitors" runat="server" CssClass="tab-content">
            <div class="chart-container">
                <div class="data-card">
                    <div class="data-card-header">
                        <span><i class="fas fa-users"></i> ผู้เข้าชมใหม่ vs กลับมาใหม่</span>
                    </div>
                    <div class="data-card-body">
                        <asp:GridView ID="gvNewVsReturning" runat="server" AutoGenerateColumns="False" CssClass="data-table" GridLines="None">
                            <Columns>
                                <asp:TemplateField HeaderText="ประเภท">
                                    <ItemTemplate>
                                        <%# Eval("VisitorType").ToString() == "New Visitor" ? "ผู้เข้าชมใหม่" : "ผู้เข้าชมที่กลับมา" %>
                                    </ItemTemplate>
                                </asp:TemplateField>
                                <asp:BoundField DataField="VisitCount" HeaderText="จำนวน" />
                            </Columns>
                        </asp:GridView>
                    </div>
                </div>

                <div class="data-card">
                    <div class="data-card-header">
                        <span><i class="fas fa-network-wired"></i> Top Visitors (IP)</span>
                    </div>
                    <div class="data-card-body">
                        <asp:GridView ID="gvTopVisitors" runat="server" AutoGenerateColumns="False" CssClass="data-table" GridLines="None">
                            <Columns>
                                <asp:BoundField DataField="DeviceIP" HeaderText="IP Address" />
                                <asp:BoundField DataField="DeviceName" HeaderText="Device" />
                                <asp:TemplateField HeaderText="ประเภท">
                                    <ItemTemplate>
                                        <span class='badge <%# Eval("UserCategory").ToString() == "Staff" ? "badge-info" : "badge-success" %>'>
                                            <%# Eval("UserCategory").ToString() == "Staff" ? "ทีมงาน" : "ลูกค้า" %>
                                        </span>
                                    </ItemTemplate>
                                </asp:TemplateField>
                                <asp:BoundField DataField="VisitCount" HeaderText="เข้าชม" />
                                <asp:BoundField DataField="VisitDays" HeaderText="วัน" />
                                <asp:TemplateField HeaderText="เข้าชมล่าสุด">
                                    <ItemTemplate>
                                        <%# Convert.ToDateTime(Eval("LastVisit")).ToString("dd/MM/yyyy HH:mm") %>
                                    </ItemTemplate>
                                </asp:TemplateField>
                            </Columns>
                        </asp:GridView>
                    </div>
                </div>
            </div>
        </asp:Panel>

        <!-- Tab 5: User Activity -->
        <asp:Panel ID="pnlActivity" runat="server" CssClass="tab-content">
            <div class="chart-container">
                <div class="data-card">
                    <div class="data-card-header">
                        <span><i class="fas fa-user-clock"></i> ผู้ใช้ที่ Active</span>
                    </div>
                    <div class="data-card-body">
                        <asp:GridView ID="gvActiveUsers" runat="server" AutoGenerateColumns="False" CssClass="data-table" GridLines="None">
                            <Columns>
                                <asp:BoundField DataField="Username" HeaderText="ผู้ใช้" />
                                <asp:BoundField DataField="ActionCount" HeaderText="กิจกรรม" />
                                <asp:BoundField DataField="UniqueActions" HeaderText="ประเภท" />
                                <asp:TemplateField HeaderText="ล่าสุด">
                                    <ItemTemplate>
                                        <%# Convert.ToDateTime(Eval("LastActivity")).ToString("dd/MM HH:mm") %>
                                    </ItemTemplate>
                                </asp:TemplateField>
                            </Columns>
                        </asp:GridView>
                    </div>
                </div>

                <div class="data-card">
                    <div class="data-card-header">
                        <span><i class="fas fa-tasks"></i> สรุป Action</span>
                    </div>
                    <div class="data-card-body">
                        <asp:GridView ID="gvActionSummary" runat="server" AutoGenerateColumns="False" CssClass="data-table" GridLines="None">
                            <Columns>
                                <asp:BoundField DataField="LogAction" HeaderText="Action" />
                                <asp:BoundField DataField="ActionCount" HeaderText="จำนวน" />
                                <asp:BoundField DataField="UniqueUsers" HeaderText="ผู้ใช้" />
                            </Columns>
                        </asp:GridView>
                    </div>
                </div>
            </div>
        </asp:Panel>

        <!-- Tab 6: Errors -->
        <asp:Panel ID="pnlErrors" runat="server" CssClass="tab-content">
            <div class="chart-container">
                <div class="data-card">
                    <div class="data-card-header">
                        <span><i class="fas fa-chart-pie"></i> Log ตาม Category</span>
                    </div>
                    <div class="data-card-body">
                        <asp:GridView ID="gvLogsByCategory" runat="server" AutoGenerateColumns="False" CssClass="data-table" GridLines="None">
                            <Columns>
                                <asp:BoundField DataField="CategoryName" HeaderText="Category" />
                                <asp:BoundField DataField="LogCount" HeaderText="จำนวน" />
                                <asp:TemplateField HeaderText="Errors">
                                    <ItemTemplate>
                                        <span class='<%# Convert.ToInt32(Eval("ErrorCount")) > 0 ? "badge badge-error" : "badge badge-success" %>'>
                                            <%# Eval("ErrorCount") %>
                                        </span>
                                    </ItemTemplate>
                                </asp:TemplateField>
                            </Columns>
                        </asp:GridView>
                    </div>
                </div>

                <div class="data-card">
                    <div class="data-card-header">
                        <span><i class="fas fa-layer-group"></i> Log ตาม Level</span>
                    </div>
                    <div class="data-card-body">
                        <asp:GridView ID="gvLogsByLevel" runat="server" AutoGenerateColumns="False" CssClass="data-table" GridLines="None">
                            <Columns>
                                <asp:TemplateField HeaderText="Level">
                                    <ItemTemplate>
                                        <span class='badge <%# GetLevelBadgeClass(Eval("LevelName").ToString()) %>'>
                                            <%# Eval("LevelName") %>
                                        </span>
                                    </ItemTemplate>
                                </asp:TemplateField>
                                <asp:BoundField DataField="LogCount" HeaderText="จำนวน" />
                            </Columns>
                        </asp:GridView>
                    </div>
                </div>
            </div>

            <div class="data-card">
                <div class="data-card-header">
                    <span><i class="fas fa-exclamation-triangle"></i> Recent Errors</span>
                </div>
                <div class="data-card-body">
                    <asp:GridView ID="gvRecentErrors" runat="server" AutoGenerateColumns="False" CssClass="data-table" GridLines="None">
                        <Columns>
                            <asp:TemplateField HeaderText="เวลา">
                                <ItemTemplate>
                                    <%# Convert.ToDateTime(Eval("CreatedDate")).ToString("dd/MM HH:mm") %>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="Level">
                                <ItemTemplate>
                                    <span class='badge <%# GetLevelBadgeClass(Eval("LevelName").ToString()) %>'>
                                        <%# Eval("LevelName") %>
                                    </span>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:BoundField DataField="CategoryName" HeaderText="Category" />
                            <asp:BoundField DataField="Message" HeaderText="Message" />
                        </Columns>
                        <EmptyDataTemplate>
                            <div style="text-align: center; padding: 40px; color: #27ae60;">
                                <i class="fas fa-check-circle fa-2x"></i><br />
                                ไม่พบข้อผิดพลาด
                            </div>
                        </EmptyDataTemplate>
                    </asp:GridView>
                </div>
            </div>
        </asp:Panel>

        <!-- Tab 7: Access Log -->
        <asp:Panel ID="pnlAccessLog" runat="server" CssClass="tab-content">
            <div class="data-card">
                <div class="data-card-header">
                    <span><i class="fas fa-list"></i> Access Log Detail</span>
                    <asp:Label ID="lblAccessLogCount" runat="server" CssClass="badge badge-info"></asp:Label>
                </div>
                <div class="data-card-body">
                    <asp:GridView ID="gvAccessLog" runat="server" AutoGenerateColumns="False" CssClass="data-table" GridLines="None">
                        <Columns>
                            <asp:TemplateField HeaderText="เวลา">
                                <ItemTemplate>
                                    <%# Convert.ToDateTime(Eval("AccessDateTime")).ToString("dd/MM/yyyy HH:mm:ss") %>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:BoundField DataField="DeviceIP" HeaderText="IP Address" />
                            <asp:BoundField DataField="DeviceName" HeaderText="Device" />
                            <asp:BoundField DataField="Browser" HeaderText="Browser" />
                            <asp:TemplateField HeaderText="ประเภท">
                                <ItemTemplate>
                                    <span class='badge <%# Eval("UserCategory").ToString() == "Staff" ? "badge-info" : "badge-success" %>'>
                                        <%# Eval("UserCategory").ToString() == "Staff" ? "ทีมงาน" : "ลูกค้า" %>
                                    </span>
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                        <EmptyDataTemplate>
                            <div style="text-align: center; padding: 40px; color: #999;">
                                ไม่พบข้อมูล Access Log
                            </div>
                        </EmptyDataTemplate>
                    </asp:GridView>
                </div>
            </div>
        </asp:Panel>
    </div>
</asp:Content>
