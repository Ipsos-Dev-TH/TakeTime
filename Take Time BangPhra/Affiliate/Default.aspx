<%@ Page Title="Affiliate Dashboard" Language="C#" MaintainScrollPositionOnPostback="true" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="Take_Time_BangPhra.Affiliate.Default" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .affiliate-dashboard {
            padding: 20px;
            max-width: 1200px;
            margin: 0 auto;
        }

        .dashboard-header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 30px;
            border-radius: 15px;
            margin-bottom: 25px;
            box-shadow: 0 10px 30px rgba(102, 126, 234, 0.3);
        }

        .dashboard-header h1 {
            margin: 0 0 10px 0;
            font-size: 28px;
            font-weight: 700;
        }

        .dashboard-header p {
            margin: 0;
            opacity: 0.9;
        }

        .welcome-section {
            display: flex;
            justify-content: space-between;
            align-items: center;
            flex-wrap: wrap;
            gap: 15px;
        }

        .btn-logout {
            background: rgba(255,255,255,0.2);
            border: 2px solid white;
            color: white;
            padding: 10px 25px;
            border-radius: 8px;
            font-weight: 600;
            cursor: pointer;
            text-decoration: none;
            transition: all 0.3s;
        }

        .btn-logout:hover {
            background: white;
            color: #667eea;
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
            border-radius: 12px;
            box-shadow: 0 4px 15px rgba(0,0,0,0.08);
            text-align: center;
            transition: transform 0.3s;
        }

        .stat-card:hover {
            transform: translateY(-5px);
        }

        .stat-card.primary {
            border-top: 4px solid #667eea;
        }

        .stat-card.success {
            border-top: 4px solid #28a745;
        }

        .stat-card.warning {
            border-top: 4px solid #ffc107;
        }

        .stat-card .icon {
            width: 60px;
            height: 60px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            margin: 0 auto 15px;
            color: white;
            font-size: 24px;
        }

        .stat-card.success .icon {
            background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%);
        }

        .stat-card.warning .icon {
            background: linear-gradient(135deg, #f5af19 0%, #f12711 100%);
        }

        .stat-card h3 {
            margin: 0 0 5px 0;
            font-size: 13px;
            color: #666;
            font-weight: 500;
        }

        .stat-card .value {
            font-size: 28px;
            font-weight: 700;
            color: #333;
        }

        /* Profile Card */
        .profile-card {
            background: white;
            border-radius: 12px;
            box-shadow: 0 4px 15px rgba(0,0,0,0.08);
            margin-bottom: 25px;
            overflow: hidden;
        }

        .profile-header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 20px;
        }

        .profile-header h2 {
            margin: 0;
            font-size: 18px;
            font-weight: 600;
        }

        .profile-body {
            padding: 25px;
        }

        .profile-item {
            display: flex;
            align-items: center;
            padding: 15px 0;
            border-bottom: 1px solid #f0f0f0;
        }

        .profile-item:last-child {
            border-bottom: none;
        }

        .profile-item .label {
            width: 150px;
            font-weight: 600;
            color: #666;
            font-size: 14px;
        }

        .profile-item .value {
            flex: 1;
            font-size: 14px;
            color: #333;
        }

        .coupon-code {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 15px 25px;
            border-radius: 10px;
            font-family: 'Courier New', monospace;
            font-size: 24px;
            font-weight: 700;
            letter-spacing: 3px;
            text-align: center;
            display: inline-block;
        }

        .referral-link {
            background: #f8f9fa;
            padding: 15px;
            border-radius: 8px;
            font-size: 12px;
            word-break: break-all;
            color: #667eea;
            border: 2px dashed #667eea;
        }

        .btn-copy {
            background: #667eea;
            color: white;
            border: none;
            padding: 8px 15px;
            border-radius: 5px;
            font-size: 12px;
            cursor: pointer;
            margin-top: 10px;
        }

        /* Report Section */
        .report-section {
            background: white;
            border-radius: 12px;
            box-shadow: 0 4px 15px rgba(0,0,0,0.08);
            overflow: hidden;
        }

        .report-header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 20px;
            display: flex;
            justify-content: space-between;
            align-items: center;
            flex-wrap: wrap;
            gap: 15px;
        }

        .report-header h2 {
            margin: 0;
            font-size: 18px;
            font-weight: 600;
        }

        .report-filters {
            display: flex;
            gap: 10px;
            flex-wrap: wrap;
        }

        .form-control {
            padding: 10px 15px;
            border: none;
            border-radius: 6px;
            font-size: 14px;
            min-width: 200px;
        }

        .btn-search {
            background: white;
            color: #667eea;
            border: none;
            padding: 10px 20px;
            border-radius: 6px;
            font-weight: 600;
            cursor: pointer;
        }

        .report-body {
            padding: 0;
        }

        /* Data Table */
        .data-table {
            width: 100%;
            overflow-x: auto;
            -webkit-overflow-scrolling: touch;
        }

        .data-table table {
            width: 100%;
            border-collapse: collapse;
            min-width: 600px;
        }

        .data-table th {
            background: #f8f9fa;
            padding: 15px;
            text-align: left;
            font-weight: 600;
            color: #333;
            font-size: 13px;
            border-bottom: 2px solid #e0e0e0;
        }

        .data-table td {
            padding: 15px;
            border-bottom: 1px solid #f0f0f0;
            font-size: 13px;
        }

        .data-table tr:hover {
            background-color: #f8f9fa;
        }

        .badge {
            display: inline-block;
            padding: 4px 10px;
            border-radius: 4px;
            font-size: 11px;
            font-weight: 500;
        }

        .badge-new {
            background: #fff3cd;
            color: #856404;
        }

        .badge-transferred {
            background: #d4edda;
            color: #155724;
        }

        .btn-sm {
            padding: 6px 12px;
            border: none;
            border-radius: 5px;
            font-size: 12px;
            cursor: pointer;
            margin: 2px;
        }

        .btn-primary {
            background: #667eea;
            color: white;
        }

        .btn-success {
            background: #28a745;
            color: white;
        }

        .btn-info {
            background: #17a2b8;
            color: white;
        }

        .empty-state {
            text-align: center;
            padding: 60px 20px;
            color: #999;
        }

        .empty-state i {
            font-size: 60px;
            margin-bottom: 20px;
            opacity: 0.5;
        }

        /* Mobile Responsive */
        @media (max-width: 768px) {
            .affiliate-dashboard {
                padding: 15px;
            }
            .dashboard-header {
                padding: 20px;
            }
            .dashboard-header h1 {
                font-size: 22px;
            }
            .stats-grid {
                grid-template-columns: repeat(2, 1fr);
            }
            .profile-item {
                flex-direction: column;
                align-items: flex-start;
            }
            .profile-item .label {
                margin-bottom: 5px;
            }
            .report-header {
                flex-direction: column;
                align-items: stretch;
            }
            .report-filters {
                flex-direction: column;
            }
            .form-control {
                width: 100%;
            }
            .data-table {
                margin: 0 -15px;
                overflow-x: scroll !important;
                -webkit-overflow-scrolling: touch !important;
                touch-action: pan-x pan-y !important;
            }
        }
    </style>

    <div class="affiliate-dashboard">
        <!-- Header -->
        <div class="dashboard-header">
            <div class="welcome-section">
                <div>
                    <h1>Affiliate Dashboard</h1>
                    <p>ยินดีต้อนรับ, <asp:Label ID="lblWelcomeName" runat="server"></asp:Label></p>
                </div>
                <a href="Login.aspx" class="btn-logout" onclick="<%= "Session.Clear();" %>">
                    <i class="fas fa-sign-out-alt"></i> ออกจากระบบ
                </a>
            </div>
        </div>

        <!-- Stats Cards -->
        <div class="stats-grid">
            <div class="stat-card primary">
                <div class="icon"><i class="fas fa-tag"></i></div>
                <h3>รหัส Coupon</h3>
                <div class="value"><asp:Label ID="TextBox4" runat="server"></asp:Label></div>
            </div>
            <div class="stat-card success">
                <div class="icon"><i class="fas fa-money-bill-wave"></i></div>
                <h3>รายได้รวมที่ได้รับ</h3>
                <div class="value" style="color: #28a745;"><asp:Label ID="TextBox6" runat="server" Text="0"></asp:Label> ฿</div>
            </div>
            <div class="stat-card warning">
                <div class="icon"><i class="fas fa-hourglass-half"></i></div>
                <h3>รอรับเงิน</h3>
                <div class="value" style="color: #ffc107;"><asp:Label ID="lblPendingAmount" runat="server" Text="0"></asp:Label> ฿</div>
            </div>
        </div>

        <!-- Profile Card -->
        <div class="profile-card">
            <div class="profile-header">
                <h2><i class="fas fa-user-circle"></i> ข้อมูลของฉัน</h2>
            </div>
            <div class="profile-body">
                <div class="profile-item">
                    <span class="label"><i class="fas fa-user"></i> ชื่อ</span>
                    <span class="value"><asp:Label ID="TextBox1" runat="server"></asp:Label></span>
                </div>
                <div class="profile-item">
                    <span class="label"><i class="fas fa-university"></i> ธนาคาร</span>
                    <span class="value"><asp:Label ID="TextBox2" runat="server"></asp:Label></span>
                </div>
                <div class="profile-item">
                    <span class="label"><i class="fas fa-credit-card"></i> เลขบัญชี</span>
                    <span class="value"><asp:Label ID="TextBox3" runat="server"></asp:Label></span>
                </div>
                <div class="profile-item">
                    <span class="label"><i class="fas fa-link"></i> ลิงค์แนะนำ</span>
                    <span class="value">
                        <div class="referral-link">
                            <asp:Label ID="TextBox5" runat="server"></asp:Label>
                        </div>
                        <button type="button" class="btn-copy" onclick="copyLink()">
                            <i class="fas fa-copy"></i> คัดลอกลิงค์
                        </button>
                    </span>
                </div>
            </div>
        </div>

        <!-- Report Section -->
        <div class="report-section">
            <div class="report-header">
                <h2><i class="fas fa-chart-bar"></i> รายงานการจอง</h2>
                <div class="report-filters">
                    <asp:DropDownList ID="DropDownList1" runat="server" CssClass="form-control">
                        <asp:ListItem Value="">--- เลือกรายงาน ---</asp:ListItem>
                        <asp:ListItem Value="1">รายการจองที่ยังไม่ได้เข้าพัก</asp:ListItem>
                        <asp:ListItem Value="2">รายการรอรอบโอนเงิน</asp:ListItem>
                        <asp:ListItem Value="3">รายการที่ได้รับเงินแล้ว</asp:ListItem>
                        <asp:ListItem Value="4">รายการที่ยกเลิก</asp:ListItem>
                        <asp:ListItem Value="5">ประวัติการรับเงิน</asp:ListItem>
                    </asp:DropDownList>
                    <asp:Button ID="Button2" runat="server" Text="ค้นหา" CssClass="btn-search" OnClick="Button2_Click" />
                </div>
            </div>
            <div class="report-body">
                <div class="data-table">
                    <asp:GridView ID="GridView1" runat="server" AutoGenerateColumns="true"
                        AllowPaging="True" OnPageIndexChanging="GridView1_PageIndexChanging"
                        PageSize="20" OnRowCommand="GridView1_RowCommand" CssClass="table" GridLines="None">
                        <Columns>
                            <asp:TemplateField HeaderText="เอกสาร">
                                <ItemTemplate>
                                    <asp:Button ID="btnVoucher" runat="server" Text="ใบสำคัญจ่าย"
                                        CssClass="btn-sm btn-primary" CommandName="REC"
                                        CommandArgument='<%# Container.DataItemIndex %>' />
                                    <asp:Button ID="btnSlip" runat="server" Text="สลิป"
                                        CssClass="btn-sm btn-success" CommandName="Slip"
                                        CommandArgument='<%# Container.DataItemIndex %>' />
                                    <asp:Button ID="btnTax" runat="server" Text="ใบหักภาษี"
                                        CssClass="btn-sm btn-info" CommandName="TAX"
                                        CommandArgument='<%# Container.DataItemIndex %>' />
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                        <EmptyDataTemplate>
                            <div class="empty-state">
                                <i class="fas fa-inbox"></i>
                                <p>ไม่พบข้อมูล กรุณาเลือกรายงานที่ต้องการดู</p>
                            </div>
                        </EmptyDataTemplate>
                        <PagerStyle CssClass="pagination-container" />
                    </asp:GridView>
                </div>
            </div>
        </div>
    </div>

    <script type="text/javascript">
        function copyLink() {
            var linkText = document.querySelector('.referral-link').innerText;
            navigator.clipboard.writeText(linkText).then(function() {
                alert('คัดลอกลิงค์เรียบร้อยแล้ว!');
            }, function(err) {
                // Fallback for older browsers
                var tempInput = document.createElement('input');
                tempInput.value = linkText;
                document.body.appendChild(tempInput);
                tempInput.select();
                document.execCommand('copy');
                document.body.removeChild(tempInput);
                alert('คัดลอกลิงค์เรียบร้อยแล้ว!');
            });
        }
    </script>
</asp:Content>
