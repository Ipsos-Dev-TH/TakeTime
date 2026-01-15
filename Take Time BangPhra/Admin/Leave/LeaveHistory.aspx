<%@ Page Title="ประวัติการลา" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="LeaveHistory.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Leave.LeaveHistory" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .leave-history {
            padding: 20px;
            max-width: 1400px;
            margin: 0 auto;
        }

        .section-header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 15px 20px;
            border-radius: 8px;
            margin-bottom: 20px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .section-header h2 {
            margin: 0;
            font-size: 24px;
            font-weight: 600;
        }

        .back-link {
            color: white;
            text-decoration: none;
            padding: 8px 15px;
            border: 1px solid rgba(255,255,255,0.5);
            border-radius: 6px;
            font-size: 14px;
        }

        .back-link:hover {
            background: rgba(255,255,255,0.2);
        }

        .summary-cards {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
            gap: 15px;
            margin-bottom: 20px;
        }

        .summary-card {
            background: white;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            text-align: center;
        }

        .summary-card h4 {
            margin: 0 0 10px 0;
            color: #666;
            font-size: 13px;
        }

        .summary-card .value {
            font-size: 28px;
            font-weight: 700;
        }

        .summary-card.purple { border-top: 4px solid #6f42c1; }
        .summary-card.purple .value { color: #6f42c1; }
        .summary-card.orange { border-top: 4px solid #fd7e14; }
        .summary-card.orange .value { color: #fd7e14; }
        .summary-card.green { border-top: 4px solid #11998e; }
        .summary-card.green .value { color: #11998e; }
        .summary-card.blue { border-top: 4px solid #4facfe; }
        .summary-card.blue .value { color: #4facfe; }

        .filter-section {
            background: white;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            margin-bottom: 20px;
        }

        .filter-row {
            display: flex;
            gap: 15px;
            align-items: flex-end;
            flex-wrap: wrap;
        }

        .filter-group {
            display: flex;
            flex-direction: column;
            gap: 5px;
        }

        .filter-group label {
            font-weight: 500;
            color: #333;
            font-size: 13px;
        }

        .filter-control {
            padding: 10px 15px;
            border: 2px solid #e0e0e0;
            border-radius: 6px;
            font-size: 14px;
            min-width: 150px;
        }

        .filter-control:focus {
            border-color: #667eea;
            outline: none;
        }

        .btn {
            padding: 10px 20px;
            border: none;
            border-radius: 6px;
            cursor: pointer;
            font-size: 14px;
            font-weight: 500;
            transition: all 0.3s;
        }

        .btn-primary {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
        }

        .btn-secondary {
            background: #6c757d;
            color: white;
        }

        .btn:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(0,0,0,0.2);
        }

        .data-section {
            background: white;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            overflow: hidden;
        }

        .data-section h3 {
            margin: 0;
            padding: 15px 20px;
            background: #f8f9fa;
            border-bottom: 1px solid #e0e0e0;
        }

        .data-table {
            overflow-x: auto;
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
            white-space: nowrap;
        }

        .data-table td {
            padding: 12px;
            border-bottom: 1px solid #f0f0f0;
        }

        .data-table tr:hover {
            background: #f8f9fa;
        }

        .badge {
            display: inline-block;
            padding: 4px 8px;
            border-radius: 4px;
            font-size: 11px;
            font-weight: 500;
        }

        .badge-pending { background: #fff3cd; color: #856404; }
        .badge-approved { background: #d4edda; color: #155724; }
        .badge-rejected { background: #f8d7da; color: #721c24; }
        .badge-cancelled { background: #e2e3e5; color: #6c757d; }

        .leave-type-badge {
            display: inline-block;
            padding: 4px 8px;
            border-radius: 4px;
            font-size: 11px;
            background: #e3f2fd;
            color: #1976d2;
        }

        .alert {
            padding: 15px;
            border-radius: 6px;
            margin-bottom: 20px;
        }

        .alert-info { background: #cce5ff; color: #004085; border: 1px solid #b8daff; }

        .quota-table {
            width: 100%;
            border-collapse: collapse;
            margin-top: 15px;
        }

        .quota-table th, .quota-table td {
            padding: 10px;
            border: 1px solid #e0e0e0;
            text-align: left;
        }

        .quota-table th {
            background: #f8f9fa;
            font-weight: 500;
        }

        @media (max-width: 768px) {
            .filter-row {
                flex-direction: column;
                align-items: stretch;
            }
            .filter-control {
                width: 100%;
            }
            .summary-cards {
                grid-template-columns: repeat(2, 1fr);
            }
        }
    </style>

    <div class="leave-history">
        <div class="section-header">
            <h2>&#128197; ประวัติการลา</h2>
            <a href="LeaveRequest.aspx" class="back-link">&#8592; กลับหน้าขอลา</a>
        </div>

        <!-- Employee Info -->
        <div class="alert alert-info">
            <strong>&#128100; พนักงาน:</strong> <asp:Label ID="lblEmployeeName" runat="server"></asp:Label>
            <span style="margin-left: 20px;"><strong>&#128188; ตำแหน่ง:</strong> <asp:Label ID="lblPosition" runat="server"></asp:Label></span>
        </div>

        <!-- Filter Section -->
        <div class="filter-section">
            <div class="filter-row">
                <div class="filter-group">
                    <label>ปี</label>
                    <asp:DropDownList ID="ddlYear" runat="server" CssClass="filter-control" AutoPostBack="true" OnSelectedIndexChanged="ddlYear_SelectedIndexChanged">
                    </asp:DropDownList>
                </div>
                <div class="filter-group">
                    <label>ประเภทการลา</label>
                    <asp:DropDownList ID="ddlLeaveType" runat="server" CssClass="filter-control" AutoPostBack="true" OnSelectedIndexChanged="ddlFilter_SelectedIndexChanged">
                        <asp:ListItem Value="">-- ทั้งหมด --</asp:ListItem>
                    </asp:DropDownList>
                </div>
                <div class="filter-group">
                    <label>สถานะ</label>
                    <asp:DropDownList ID="ddlStatus" runat="server" CssClass="filter-control" AutoPostBack="true" OnSelectedIndexChanged="ddlFilter_SelectedIndexChanged">
                        <asp:ListItem Value="">-- ทั้งหมด --</asp:ListItem>
                        <asp:ListItem Value="PENDING">รออนุมัติ</asp:ListItem>
                        <asp:ListItem Value="APPROVED">อนุมัติแล้ว</asp:ListItem>
                        <asp:ListItem Value="REJECTED">ปฏิเสธ</asp:ListItem>
                        <asp:ListItem Value="CANCELLED">ยกเลิก</asp:ListItem>
                    </asp:DropDownList>
                </div>
                <asp:Button ID="btnClear" runat="server" Text="&#128260; ล้างตัวกรอง" CssClass="btn btn-secondary" OnClick="btnClear_Click" />
            </div>
        </div>

        <!-- Summary Cards for Selected Year -->
        <div class="summary-cards">
            <div class="summary-card purple">
                <h4>&#128197; สิทธิ์วันลาทั้งปี</h4>
                <div class="value"><asp:Label ID="lblTotalQuota" runat="server" Text="0"></asp:Label></div>
                <small>วัน</small>
            </div>
            <div class="summary-card orange">
                <h4>&#128200; ใช้ไปแล้ว</h4>
                <div class="value"><asp:Label ID="lblTotalUsed" runat="server" Text="0"></asp:Label></div>
                <small>วัน</small>
            </div>
            <div class="summary-card green">
                <h4>&#127919; คงเหลือ</h4>
                <div class="value"><asp:Label ID="lblTotalRemaining" runat="server" Text="0"></asp:Label></div>
                <small>วัน</small>
            </div>
            <div class="summary-card blue">
                <h4>&#128203; จำนวนครั้งที่ลา</h4>
                <div class="value"><asp:Label ID="lblTotalRequests" runat="server" Text="0"></asp:Label></div>
                <small>ครั้ง</small>
            </div>
        </div>

        <!-- Leave Quota by Type -->
        <div class="data-section" style="margin-bottom: 20px;">
            <h3>&#128200; โควต้าวันลา ปี <asp:Label ID="lblSelectedYear" runat="server"></asp:Label></h3>
            <div style="padding: 15px;">
                <asp:GridView ID="gvLeaveQuota" runat="server" AutoGenerateColumns="False"
                    CssClass="quota-table" GridLines="None">
                    <Columns>
                        <asp:BoundField DataField="LeaveTypeName" HeaderText="ประเภทการลา" />
                        <asp:BoundField DataField="TotalDays" HeaderText="โควต้า" DataFormatString="{0:N1}" />
                        <asp:BoundField DataField="UsedDays" HeaderText="ใช้ไป" DataFormatString="{0:N1}" />
                        <asp:BoundField DataField="RemainingDays" HeaderText="คงเหลือ" DataFormatString="{0:N1}" />
                        <asp:TemplateField HeaderText="หักเงิน">
                            <ItemTemplate>
                                <%# Convert.ToBoolean(Eval("DeductSalary")) ? "หัก" : "ไม่หัก" %>
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                    <EmptyDataTemplate>
                        <div style="padding: 20px; text-align: center; color: #999;">
                            ยังไม่มีโควต้าวันลาสำหรับปีนี้
                        </div>
                    </EmptyDataTemplate>
                </asp:GridView>
            </div>
        </div>

        <!-- Leave History Table -->
        <div class="data-section">
            <h3>&#128203; รายการขอลา (<asp:Label ID="lblResultCount" runat="server" Text="0"></asp:Label> รายการ)</h3>
            <div class="data-table">
                <asp:GridView ID="gvLeaveHistory" runat="server" AutoGenerateColumns="False"
                    CssClass="table" GridLines="None">
                    <Columns>
                        <asp:BoundField DataField="RequestNumber" HeaderText="เลขที่" />
                        <asp:TemplateField HeaderText="ประเภท">
                            <ItemTemplate>
                                <span class="leave-type-badge"><%# Eval("LeaveTypeName") %></span>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="วันที่เริ่ม">
                            <ItemTemplate>
                                <%# Convert.ToDateTime(Eval("StartDate")).ToString("dd/MM/yyyy") %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="วันที่สิ้นสุด">
                            <ItemTemplate>
                                <%# Convert.ToDateTime(Eval("EndDate")).ToString("dd/MM/yyyy") %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="จำนวน">
                            <ItemTemplate>
                                <%# string.Format("{0:N1}", Eval("TotalDays")) %> วัน
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="Reason" HeaderText="เหตุผล" />
                        <asp:TemplateField HeaderText="สถานะ">
                            <ItemTemplate>
                                <%# GetStatusBadge(Convert.ToString(Eval("Status"))) %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="วันที่ขอ">
                            <ItemTemplate>
                                <%# Eval("SubmittedDate") != DBNull.Value ? Convert.ToDateTime(Eval("SubmittedDate")).ToString("dd/MM/yyyy HH:mm") : "-" %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="ผู้อนุมัติ">
                            <ItemTemplate>
                                <%# Eval("ApprovedByName") != DBNull.Value ? Eval("ApprovedByName") : "-" %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="วันที่อนุมัติ">
                            <ItemTemplate>
                                <%# Eval("ApprovedDate") != DBNull.Value ? Convert.ToDateTime(Eval("ApprovedDate")).ToString("dd/MM/yyyy HH:mm") : "-" %>
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                    <EmptyDataTemplate>
                        <div style="text-align: center; padding: 40px; color: #999;">
                            &#128466; ไม่พบรายการขอลาตามเงื่อนไขที่เลือก
                        </div>
                    </EmptyDataTemplate>
                </asp:GridView>
            </div>
        </div>
    </div>
</asp:Content>
