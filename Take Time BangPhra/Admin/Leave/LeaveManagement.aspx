<%@ Page Title="จัดการการลา" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="LeaveManagement.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Leave.LeaveManagement" MaintainScrollPositionOnPostback="true" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .leave-management {
            padding: 20px;
        }

        .section-header {
            background: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%);
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
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 15px;
            margin-bottom: 20px;
        }

        .stat-card {
            background: white;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            border-left: 4px solid #4facfe;
        }

        .stat-card.warning {
            border-left-color: #f5576c;
        }

        .stat-card h4 {
            margin: 0 0 8px 0;
            font-size: 13px;
            color: #666;
        }

        .stat-card .value {
            font-size: 28px;
            font-weight: 700;
            color: #333;
        }

        .filter-section {
            background: white;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            margin-bottom: 20px;
        }

        .filter-controls {
            display: flex;
            gap: 10px;
            align-items: center;
            flex-wrap: wrap;
        }

        .form-control {
            padding: 10px 15px;
            border: 2px solid #e0e0e0;
            border-radius: 6px;
            font-size: 14px;
            min-width: 150px;
        }

        .form-control:focus {
            border-color: #4facfe;
            outline: none;
        }

        .btn-primary {
            background: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%);
            border: none;
            color: white;
            padding: 10px 25px;
            border-radius: 6px;
            font-weight: 500;
            cursor: pointer;
        }

        .btn-success {
            background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%);
            border: none;
            color: white;
            padding: 8px 15px;
            border-radius: 6px;
            font-size: 13px;
            cursor: pointer;
        }

        .btn-danger {
            background: linear-gradient(135deg, #fa709a 0%, #fee140 100%);
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
            background: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%);
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

        .badge-pending {
            background: #fff3cd;
            color: #856404;
        }

        .badge-approved {
            background: #d4edda;
            color: #155724;
        }

        .badge-rejected {
            background: #f8d7da;
            color: #721c24;
        }

        .badge-cancelled {
            background: #e2e3e5;
            color: #383d41;
        }

        .leave-type-badge {
            display: inline-block;
            padding: 4px 8px;
            border-radius: 4px;
            font-size: 11px;
            font-weight: 500;
            background: #e3f2fd;
            color: #1976d2;
        }
    </style>

    <div class="leave-management">
        <div class="section-header">
            <h2>📅 ระบบจัดการการลา</h2>
        </div>

        <!-- Statistics -->
        <div class="stats-container">
            <div class="stat-card warning">
                <h4>คำขอลารออนุมัติ</h4>
                <div class="value">
                    <asp:Label ID="lblPendingRequests" runat="server" Text="0"></asp:Label>
                </div>
            </div>
            <div class="stat-card">
                <h4>อนุมัติแล้ว (ปีนี้)</h4>
                <div class="value">
                    <asp:Label ID="lblApprovedThisYear" runat="server" Text="0"></asp:Label>
                </div>
            </div>
            <div class="stat-card">
                <h4>ปฏิเสธ (ปีนี้)</h4>
                <div class="value">
                    <asp:Label ID="lblRejectedThisYear" runat="server" Text="0"></asp:Label>
                </div>
            </div>
            <div class="stat-card">
                <h4>วันลารวม (ปีนี้)</h4>
                <div class="value">
                    <asp:Label ID="lblTotalDays" runat="server" Text="0"></asp:Label>
                </div>
            </div>
        </div>

        <!-- Filter Section -->
        <div class="filter-section">
            <h4 style="margin-top: 0;">🔍 กรองข้อมูล</h4>
            <div class="filter-controls">
                <asp:DropDownList ID="ddlStatus" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlStatus_SelectedIndexChanged">
                    <asp:ListItem Value="">ทุกสถานะ</asp:ListItem>
                    <asp:ListItem Value="PENDING">รออนุมัติ</asp:ListItem>
                    <asp:ListItem Value="APPROVED">อนุมัติแล้ว</asp:ListItem>
                    <asp:ListItem Value="REJECTED">ปฏิเสธ</asp:ListItem>
                    <asp:ListItem Value="CANCELLED">ยกเลิก</asp:ListItem>
                </asp:DropDownList>

                <asp:DropDownList ID="ddlYear" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlYear_SelectedIndexChanged">
                </asp:DropDownList>

                <asp:TextBox ID="txtSearchEmployee" runat="server" CssClass="form-control" placeholder="ชื่อพนักงาน..."></asp:TextBox>
                <asp:Button ID="btnSearch" runat="server" Text="ค้นหา" CssClass="btn-primary" OnClick="btnSearch_Click" />
            </div>
        </div>

        <!-- Leave Requests Table -->
        <div class="data-table">
            <asp:GridView ID="gvLeaveRequests" runat="server" AutoGenerateColumns="False"
                CssClass="table" GridLines="None" OnRowCommand="gvLeaveRequests_RowCommand">
                <Columns>
                    <asp:BoundField DataField="RequestNumber" HeaderText="เลขที่" />

                    <asp:TemplateField HeaderText="พนักงาน">
                        <ItemTemplate>
                            <%# Eval("EmployeeName") %><br />
                            <small style="color: #666;"><%# Eval("NickName") %></small>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="ประเภทการลา">
                        <ItemTemplate>
                            <span class="leave-type-badge"><%# Eval("LeaveTypeName") %></span>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="วันที่">
                        <ItemTemplate>
                            <%# Convert.ToDateTime(Eval("StartDate")).ToString("dd/MM/yyyy") %> -
                            <%# Convert.ToDateTime(Eval("EndDate")).ToString("dd/MM/yyyy") %>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="จำนวนวัน">
                        <ItemTemplate>
                            <%# string.Format("{0:N1}", Eval("TotalDays")) %> วัน
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="หักเงิน">
                        <ItemTemplate>
                            <%# GetDeductionDisplay(Eval("DeductSalary"), Eval("DeductionAmount")) %>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:BoundField DataField="Reason" HeaderText="เหตุผล" />

                    <asp:TemplateField HeaderText="สถานะ">
                        <ItemTemplate>
                            <%# GetStatusBadge(Eval("Status")?.ToString()) %>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="จัดการ">
                        <ItemTemplate>
                            <asp:Panel ID="pnlActions" runat="server" Visible='<%# Eval("Status")?.ToString() == "PENDING" %>'>
                                <asp:Button ID="btnApprove" runat="server" Text="อนุมัติ"
                                    CssClass="btn-success" CommandName="Approve"
                                    CommandArgument='<%# Eval("ID") %>'
                                    OnClientClick="return confirm('ยืนยันการอนุมัติคำขอลานี้?');" />
                                <asp:Button ID="btnReject" runat="server" Text="ปฏิเสธ"
                                    CssClass="btn-danger" CommandName="Reject"
                                    CommandArgument='<%# Eval("ID") %>'
                                    OnClientClick="return confirm('ยืนยันการปฏิเสธคำขอลานี้?');" />
                            </asp:Panel>
                            <asp:Panel ID="pnlApproved" runat="server" Visible='<%# Eval("Status")?.ToString() != "PENDING" %>'>
                                <small style="color: #666;">
                                    <%# Eval("ApprovedByName") != DBNull.Value ? "โดย " + Eval("ApprovedByName") : "" %><br />
                                    <%# Eval("ApprovedDate") != DBNull.Value ? Convert.ToDateTime(Eval("ApprovedDate")).ToString("dd/MM/yyyy HH:mm") : "" %>
                                </small>
                            </asp:Panel>
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
                <EmptyDataTemplate>
                    <div style="text-align: center; padding: 40px; color: #999;">
                        ไม่พบข้อมูลคำขอลา
                    </div>
                </EmptyDataTemplate>
            </asp:GridView>
        </div>
    </div>
</asp:Content>
