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

        /* Tab Navigation */
        .tab-navigation {
            display: flex;
            gap: 5px;
            margin-bottom: 20px;
            border-bottom: 2px solid #e0e0e0;
            padding-bottom: 0;
        }

        .tab-btn {
            background: #f5f5f5;
            border: none;
            padding: 12px 25px;
            font-size: 14px;
            font-weight: 500;
            color: #666;
            cursor: pointer;
            border-radius: 8px 8px 0 0;
            transition: all 0.3s ease;
            margin-bottom: -2px;
        }

        .tab-btn:hover {
            background: #e0e0e0;
        }

        .tab-btn.active {
            background: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%);
            color: white;
            border-bottom: 2px solid #4facfe;
        }

        .tab-content {
            display: none;
        }

        .tab-content.active {
            display: block;
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
            min-height: 44px;
            line-height: 1.6;
        }

        /* Fix Thai text display in dropdowns */
        select.form-control {
            height: auto;
            min-height: 44px;
            padding-top: 8px;
            padding-bottom: 8px;
        }

        select.form-control option {
            padding: 8px 10px;
            line-height: 1.6;
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

        .btn-warning {
            background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
            border: none;
            color: white;
            padding: 8px 15px;
            border-radius: 6px;
            font-size: 13px;
            cursor: pointer;
        }

        .btn-info {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
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
            overflow-x: auto;
            -webkit-overflow-scrolling: touch;
        }

        .data-table table {
            width: 100%;
            border-collapse: collapse;
            min-width: 900px;
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

        .badge-active {
            background: #d4edda;
            color: #155724;
        }

        .badge-inactive {
            background: #f8d7da;
            color: #721c24;
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

        /* Modal Styles */
        .modal-overlay {
            display: none;
            position: fixed;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            background: rgba(0,0,0,0.5);
            z-index: 1000;
        }

        .modal-overlay.show {
            display: flex;
            align-items: center;
            justify-content: center;
        }

        .modal-content {
            background: white;
            border-radius: 12px;
            width: 90%;
            max-width: 600px;
            max-height: 90vh;
            overflow-y: auto;
            box-shadow: 0 10px 40px rgba(0,0,0,0.2);
        }

        .modal-header {
            background: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%);
            color: white;
            padding: 15px 20px;
            border-radius: 12px 12px 0 0;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .modal-header h3 {
            margin: 0;
            font-size: 18px;
        }

        .modal-close {
            background: none;
            border: none;
            color: white;
            font-size: 24px;
            cursor: pointer;
        }

        .modal-body {
            padding: 20px;
        }

        .form-group {
            margin-bottom: 15px;
        }

        .form-group label {
            display: block;
            margin-bottom: 5px;
            font-weight: 500;
            color: #333;
        }

        .form-group input[type="text"],
        .form-group input[type="number"],
        .form-group textarea,
        .form-group select {
            width: 100%;
            padding: 10px;
            border: 2px solid #e0e0e0;
            border-radius: 6px;
            font-size: 14px;
            box-sizing: border-box;
        }

        .form-group input:focus,
        .form-group textarea:focus,
        .form-group select:focus {
            border-color: #4facfe;
            outline: none;
        }

        .form-group-inline {
            display: flex;
            gap: 15px;
        }

        .form-group-inline .form-group {
            flex: 1;
        }

        .checkbox-group {
            display: flex;
            gap: 20px;
            flex-wrap: wrap;
        }

        .checkbox-item {
            display: flex;
            align-items: center;
            gap: 5px;
        }

        .modal-footer {
            padding: 15px 20px;
            border-top: 1px solid #e0e0e0;
            display: flex;
            justify-content: flex-end;
            gap: 10px;
        }

        .btn-secondary {
            background: #6c757d;
            border: none;
            color: white;
            padding: 10px 20px;
            border-radius: 6px;
            cursor: pointer;
        }

        /* Toolbar */
        .toolbar {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 15px;
            flex-wrap: wrap;
            gap: 10px;
        }

        .toolbar-left {
            display: flex;
            gap: 10px;
            align-items: center;
        }

        .toolbar-right {
            display: flex;
            gap: 10px;
        }

        /* Quota specific styles */
        .quota-filter {
            display: flex;
            gap: 15px;
            align-items: center;
            margin-bottom: 20px;
            flex-wrap: wrap;
        }

        .quota-table td.editable {
            cursor: pointer;
        }

        .quota-table td.editable:hover {
            background-color: #e3f2fd;
        }

        .quota-input {
            width: 80px;
            padding: 5px;
            border: 1px solid #4facfe;
            border-radius: 4px;
            text-align: center;
        }

        /* Mobile responsive */
        @media (max-width: 768px) {
            .leave-management {
                padding: 10px;
            }
            .tab-navigation {
                flex-wrap: wrap;
            }
            .tab-btn {
                padding: 10px 15px;
                font-size: 12px;
                flex: 1;
                text-align: center;
            }
            .stats-container {
                grid-template-columns: repeat(2, 1fr);
            }
            .filter-controls {
                flex-direction: column;
                align-items: stretch;
            }
            .filter-controls .form-control {
                width: 100%;
            }
            .data-table {
                margin: 10px -15px;
                border-radius: 0;
                overflow-x: scroll !important;
                -webkit-overflow-scrolling: touch !important;
                touch-action: pan-x pan-y !important;
            }
            .quota-filter {
                flex-direction: column;
                align-items: stretch;
            }
            .quota-filter label {
                margin-top: 10px;
            }
            .quota-filter .form-control {
                width: 100%;
            }
            .toolbar {
                flex-direction: column;
                align-items: stretch;
            }
            .toolbar-left, .toolbar-right {
                width: 100%;
            }
            .toolbar-right {
                margin-top: 10px;
            }
        }
    </style>

    <div class="leave-management">
        <div class="section-header">
            <h2>ระบบจัดการการลา</h2>
        </div>

        <!-- Tab Navigation -->
        <div class="tab-navigation">
            <asp:Button ID="btnTabRequests" runat="server" Text="คำขอลา" CssClass="tab-btn active" OnClick="btnTabRequests_Click" />
            <asp:Button ID="btnTabLeaveTypes" runat="server" Text="ตั้งค่าประเภทการลา" CssClass="tab-btn" OnClick="btnTabLeaveTypes_Click" />
            <asp:Button ID="btnTabQuotas" runat="server" Text="โควต้าพนักงาน" CssClass="tab-btn" OnClick="btnTabQuotas_Click" />
        </div>

        <!-- Hidden field to track active tab -->
        <asp:HiddenField ID="hdnActiveTab" runat="server" Value="requests" />

        <!-- Tab 1: Leave Requests -->
        <asp:Panel ID="pnlRequests" runat="server" CssClass="tab-content active">
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
                <h4 style="margin-top: 0;">กรองข้อมูล</h4>
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
                                <%# GetStatusBadge(Convert.ToString(Eval("Status"))) %>
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:TemplateField HeaderText="จัดการ">
                            <ItemTemplate>
                                <asp:Panel ID="pnlActions" runat="server" Visible='<%# Convert.ToString(Eval("Status")) == "PENDING" %>'>
                                    <asp:Button ID="btnApprove" runat="server" Text="อนุมัติ"
                                        CssClass="btn-success" CommandName="Approve"
                                        CommandArgument='<%# Eval("ID") %>'
                                        OnClientClick="return confirm('ยืนยันการอนุมัติคำขอลานี้?');" />
                                    <asp:Button ID="btnReject" runat="server" Text="ปฏิเสธ"
                                        CssClass="btn-danger" CommandName="Reject"
                                        CommandArgument='<%# Eval("ID") %>'
                                        OnClientClick="return confirm('ยืนยันการปฏิเสธคำขอลานี้?');" />
                                </asp:Panel>
                                <asp:Panel ID="pnlApproved" runat="server" Visible='<%# Convert.ToString(Eval("Status")) != "PENDING" %>'>
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
        </asp:Panel>

        <!-- Tab 2: Leave Types Settings -->
        <asp:Panel ID="pnlLeaveTypes" runat="server" CssClass="tab-content">
            <div class="toolbar">
                <div class="toolbar-left">
                    <h4 style="margin: 0;">รายการประเภทการลา</h4>
                </div>
                <div class="toolbar-right">
                    <asp:Button ID="btnAddLeaveType" runat="server" Text="+ เพิ่มประเภทการลา"
                        CssClass="btn-primary" OnClick="btnAddLeaveType_Click" />
                </div>
            </div>

            <div class="data-table">
                <asp:GridView ID="gvLeaveTypes" runat="server" AutoGenerateColumns="False"
                    CssClass="table" GridLines="None" OnRowCommand="gvLeaveTypes_RowCommand"
                    DataKeyNames="ID">
                    <Columns>
                        <asp:BoundField DataField="LeaveTypeCode" HeaderText="รหัส" />
                        <asp:BoundField DataField="LeaveTypeName" HeaderText="ชื่อประเภทการลา" />
                        <asp:BoundField DataField="Description" HeaderText="คำอธิบาย" />

                        <asp:TemplateField HeaderText="โควต้า/ปี">
                            <ItemTemplate>
                                <%# Eval("AnnualQuota") %> วัน
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:TemplateField HeaderText="หักเงิน">
                            <ItemTemplate>
                                <%# Convert.ToBoolean(Eval("DeductSalary")) ? "<span style='color:#dc3545'>หัก</span>" : "<span style='color:#28a745'>ไม่หัก</span>" %>
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:TemplateField HeaderText="ใบรับรองแพทย์">
                            <ItemTemplate>
                                <%# MedCertText(Eval("RequiresMedicalCert"), Eval("MedicalCertAfterDays"), Eval("NoCertAction")) %>
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:TemplateField HeaderText="สถานะ">
                            <ItemTemplate>
                                <%# Convert.ToBoolean(Eval("IsActive"))
                                    ? "<span class='badge badge-active'>เปิดใช้งาน</span>"
                                    : "<span class='badge badge-inactive'>ปิดใช้งาน</span>" %>
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:TemplateField HeaderText="ลำดับ">
                            <ItemTemplate>
                                <%# Eval("DisplayOrder") %>
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:TemplateField HeaderText="จัดการ">
                            <ItemTemplate>
                                <asp:Button ID="btnEditType" runat="server" Text="แก้ไข" CssClass="btn-info"
                                    CommandName="EditType" CommandArgument='<%# Eval("ID") %>' />
                                <asp:Button ID="btnDeleteType" runat="server" Text="ลบ" CssClass="btn-danger"
                                    CommandName="DeleteType" CommandArgument='<%# Eval("ID") %>'
                                    OnClientClick="return confirm('ยืนยันการลบประเภทการลานี้?');"
                                    Visible='<%# Convert.ToBoolean(Eval("IsActive")) %>' />
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                    <EmptyDataTemplate>
                        <div style="text-align: center; padding: 40px; color: #999;">
                            ไม่พบข้อมูลประเภทการลา
                        </div>
                    </EmptyDataTemplate>
                </asp:GridView>
            </div>
        </asp:Panel>

        <!-- Tab 3: Employee Quotas -->
        <asp:Panel ID="pnlQuotas" runat="server" CssClass="tab-content">
            <div class="toolbar">
                <div class="toolbar-left">
                    <h4 style="margin: 0;">โควต้าวันลาพนักงาน</h4>
                </div>
                <div class="toolbar-right">
                    <asp:Button ID="btnInitializeQuotas" runat="server" Text="สร้างโควต้าเริ่มต้น"
                        CssClass="btn-success" OnClick="btnInitializeQuotas_Click"
                        OnClientClick="return confirm('ต้องการสร้างโควต้าเริ่มต้นสำหรับพนักงานทุกคนในปีนี้หรือไม่?');" />
                </div>
            </div>

            <div class="filter-section">
                <div class="quota-filter">
                    <label>ปี:</label>
                    <asp:DropDownList ID="ddlQuotaYear" runat="server" CssClass="form-control" AutoPostBack="true"
                        OnSelectedIndexChanged="ddlQuotaYear_SelectedIndexChanged">
                    </asp:DropDownList>

                    <label>ประเภทการลา:</label>
                    <asp:DropDownList ID="ddlQuotaLeaveType" runat="server" CssClass="form-control" AutoPostBack="true"
                        OnSelectedIndexChanged="ddlQuotaLeaveType_SelectedIndexChanged">
                        <asp:ListItem Value="">ทั้งหมด</asp:ListItem>
                    </asp:DropDownList>

                    <asp:TextBox ID="txtQuotaSearch" runat="server" CssClass="form-control" placeholder="ค้นหาพนักงาน..."></asp:TextBox>
                    <asp:Button ID="btnQuotaSearch" runat="server" Text="ค้นหา" CssClass="btn-primary" OnClick="btnQuotaSearch_Click" />
                </div>
            </div>

            <div class="data-table">
                <asp:GridView ID="gvQuotas" runat="server" AutoGenerateColumns="False"
                    CssClass="table quota-table" GridLines="None" OnRowCommand="gvQuotas_RowCommand"
                    DataKeyNames="AdminID,LeaveTypeID">
                    <Columns>
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

                        <asp:TemplateField HeaderText="โควต้ารวม">
                            <ItemTemplate>
                                <%# string.Format("{0:N1}", Eval("TotalDays")) %> วัน
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:TemplateField HeaderText="ใช้ไปแล้ว">
                            <ItemTemplate>
                                <%# string.Format("{0:N1}", Eval("UsedDays")) %> วัน
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:TemplateField HeaderText="คงเหลือ">
                            <ItemTemplate>
                                <span style="color: <%# Convert.ToDecimal(Eval("RemainingDays")) > 0 ? "#28a745" : "#dc3545" %>; font-weight: bold;">
                                    <%# string.Format("{0:N1}", Eval("RemainingDays")) %> วัน
                                </span>
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:TemplateField HeaderText="วันยกยอด">
                            <ItemTemplate>
                                <%# string.Format("{0:N1}", Eval("CarryForwardDays")) %> วัน
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:TemplateField HeaderText="จัดการ">
                            <ItemTemplate>
                                <asp:Button ID="btnEditQuota" runat="server" Text="แก้ไข" CssClass="btn-info"
                                    CommandName="EditQuota" CommandArgument='<%# Eval("AdminID") + "," + Eval("LeaveTypeID") %>' />
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                    <EmptyDataTemplate>
                        <div style="text-align: center; padding: 40px; color: #999;">
                            ไม่พบข้อมูลโควต้า กรุณาสร้างโควต้าเริ่มต้นก่อน
                        </div>
                    </EmptyDataTemplate>
                </asp:GridView>
            </div>
        </asp:Panel>
    </div>

    <!-- Leave Type Modal -->
    <asp:Panel ID="pnlLeaveTypeModal" runat="server" CssClass="modal-overlay">
        <div class="modal-content">
            <div class="modal-header">
                <h3><asp:Label ID="lblLeaveTypeModalTitle" runat="server" Text="เพิ่มประเภทการลา"></asp:Label></h3>
                <asp:Button ID="btnCloseLeaveTypeModal" runat="server" Text="&times;" CssClass="modal-close"
                    OnClick="btnCloseLeaveTypeModal_Click" CausesValidation="false" />
            </div>
            <div class="modal-body">
                <asp:HiddenField ID="hdnLeaveTypeId" runat="server" />

                <div class="form-group-inline">
                    <div class="form-group">
                        <label>รหัสประเภท *</label>
                        <asp:TextBox ID="txtLeaveTypeCode" runat="server" CssClass="form-control" MaxLength="20"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label>ลำดับแสดง</label>
                        <asp:TextBox ID="txtDisplayOrder" runat="server" CssClass="form-control" TextMode="Number" Text="0"></asp:TextBox>
                    </div>
                </div>

                <div class="form-group">
                    <label>ชื่อประเภทการลา *</label>
                    <asp:TextBox ID="txtLeaveTypeName" runat="server" CssClass="form-control" MaxLength="100"></asp:TextBox>
                </div>

                <div class="form-group">
                    <label>คำอธิบาย</label>
                    <asp:TextBox ID="txtLeaveTypeDesc" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="2"></asp:TextBox>
                </div>

                <div class="form-group">
                    <label>โควต้าต่อปี (วัน)</label>
                    <asp:TextBox ID="txtAnnualQuota" runat="server" CssClass="form-control" TextMode="Number" Text="0"></asp:TextBox>
                </div>

                <div class="form-group">
                    <label>ตัวเลือก</label>
                    <div class="checkbox-group">
                        <div class="checkbox-item">
                            <asp:CheckBox ID="chkDeductSalary" runat="server" />
                            <label for="<%=chkDeductSalary.ClientID %>">หักเงินเดือน</label>
                        </div>
                        <div class="checkbox-item">
                            <asp:CheckBox ID="chkRequiresMedicalCert" runat="server" AutoPostBack="true"
                                OnCheckedChanged="chkRequiresMedicalCert_CheckedChanged" />
                            <label for="<%=chkRequiresMedicalCert.ClientID %>">ต้องมีใบรับรองแพทย์</label>
                        </div>
                        <div class="checkbox-item">
                            <asp:CheckBox ID="chkRequiresApproval" runat="server" Checked="true" />
                            <label for="<%=chkRequiresApproval.ClientID %>">ต้องอนุมัติ</label>
                        </div>
                        <div class="checkbox-item">
                            <asp:CheckBox ID="chkIsActive" runat="server" Checked="true" />
                            <label for="<%=chkIsActive.ClientID %>">เปิดใช้งาน</label>
                        </div>
                    </div>
                </div>

                <%-- ── กฎใบรับรองแพทย์ (โผล่เมื่อติ๊ก "ต้องมีใบรับรองแพทย์") ──
                     ตอบโจทย์: ลาป่วย 1 วันไม่ต้องใช้ · 2 วันขึ้นไปต้องใช้ถึงจะไม่หักเงิน
                     · ไม่แนบเลย = หักหมด --%>
                <asp:Panel ID="pnlCertRule" runat="server" Visible="false"
                    CssClass="form-group"
                    style="background:#F1F8E9; border-left:4px solid #7CB342; border-radius:8px; padding:14px;">
                    <label style="font-weight:600; color:#33691E;">กฎใบรับรองแพทย์</label>

                    <div style="margin-top:10px;">
                        <label style="font-weight:500;">ต้องใช้ใบรับรองเมื่อลา<b>เกิน</b>กี่วัน</label>
                        <asp:TextBox ID="txtCertAfterDays" runat="server" CssClass="form-control"
                            TextMode="Number" step="0.5" placeholder="เว้นว่าง = ต้องใช้ทุกกรณี"
                            style="max-width:200px;" />
                        <div style="font-size:12.5px; color:#558B2F; margin-top:5px; line-height:1.6;">
                            ใส่ <b>1</b> = ลา 1 วันไม่ต้องแนบ · ลา 2 วันขึ้นไปต้องแนบ<br />
                            เว้นว่าง = ต้องแนบทุกครั้งไม่ว่าลากี่วัน
                        </div>
                    </div>

                    <div style="margin-top:14px;">
                        <label style="font-weight:500;">ถ้าต้องแนบแต่ไม่ได้แนบ</label>
                        <asp:DropDownList ID="ddlNoCertAction" runat="server" CssClass="form-control"
                            style="max-width:320px;">
                            <asp:ListItem Value="DEDUCT" Text="หักเงินทั้งจำนวนวันที่ลา (รับคำขอไว้)" />
                            <asp:ListItem Value="BLOCK" Text="ปฏิเสธคำขอ ไม่ให้ยื่น" />
                        </asp:DropDownList>
                        <div style="font-size:12.5px; color:#558B2F; margin-top:5px; line-height:1.6;">
                            เลือก <b>หักเงิน</b> ถ้าอยากให้ลาได้แต่ไม่ได้เงิน (พนักงานเลือกเองว่าจะหาใบรับรองมาหรือยอมโดนหัก)
                        </div>
                    </div>
                </asp:Panel>
            </div>
            <div class="modal-footer">
                <asp:Button ID="btnCancelLeaveType" runat="server" Text="ยกเลิก" CssClass="btn-secondary"
                    OnClick="btnCloseLeaveTypeModal_Click" CausesValidation="false" />
                <asp:Button ID="btnSaveLeaveType" runat="server" Text="บันทึก" CssClass="btn-primary"
                    OnClick="btnSaveLeaveType_Click" />
            </div>
        </div>
    </asp:Panel>

    <!-- Quota Edit Modal -->
    <asp:Panel ID="pnlQuotaModal" runat="server" CssClass="modal-overlay">
        <div class="modal-content">
            <div class="modal-header">
                <h3>แก้ไขโควต้าวันลา</h3>
                <asp:Button ID="btnCloseQuotaModal" runat="server" Text="&times;" CssClass="modal-close"
                    OnClick="btnCloseQuotaModal_Click" CausesValidation="false" />
            </div>
            <div class="modal-body">
                <asp:HiddenField ID="hdnQuotaAdminId" runat="server" />
                <asp:HiddenField ID="hdnQuotaLeaveTypeId" runat="server" />

                <div class="form-group">
                    <label>พนักงาน</label>
                    <asp:Label ID="lblQuotaEmployee" runat="server" CssClass="form-control"
                        style="background-color: #f5f5f5;"></asp:Label>
                </div>

                <div class="form-group">
                    <label>ประเภทการลา</label>
                    <asp:Label ID="lblQuotaLeaveType" runat="server" CssClass="form-control"
                        style="background-color: #f5f5f5;"></asp:Label>
                </div>

                <div class="form-group-inline">
                    <div class="form-group">
                        <label>โควต้ารวม (วัน)</label>
                        <asp:TextBox ID="txtQuotaTotalDays" runat="server" CssClass="form-control" TextMode="Number"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label>วันยกยอดมา</label>
                        <asp:TextBox ID="txtQuotaCarryForward" runat="server" CssClass="form-control" TextMode="Number" Text="0"></asp:TextBox>
                    </div>
                </div>

                <div class="form-group">
                    <label>ใช้ไปแล้ว</label>
                    <asp:Label ID="lblQuotaUsed" runat="server" CssClass="form-control"
                        style="background-color: #f5f5f5;"></asp:Label>
                </div>
            </div>
            <div class="modal-footer">
                <asp:Button ID="btnCancelQuota" runat="server" Text="ยกเลิก" CssClass="btn-secondary"
                    OnClick="btnCloseQuotaModal_Click" CausesValidation="false" />
                <asp:Button ID="btnSaveQuota" runat="server" Text="บันทึก" CssClass="btn-primary"
                    OnClick="btnSaveQuota_Click" />
            </div>
        </div>
    </asp:Panel>
</asp:Content>
