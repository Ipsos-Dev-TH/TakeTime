<%@ Page Title="อนุมัติการลา (หัวหน้างาน)" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="SupervisorLeaveApproval.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Leave.SupervisorLeaveApproval" MaintainScrollPositionOnPostback="true" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .supervisor-leave {
            padding: 20px;
        }

        .section-header {
            background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%);
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
            border-left: 4px solid #11998e;
        }

        .stat-card.warning {
            border-left-color: #f5576c;
        }

        .stat-card.info {
            border-left-color: #4facfe;
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

        select.form-control {
            height: auto;
            min-height: 44px;
        }

        .form-control:focus {
            border-color: #11998e;
            outline: none;
        }

        .btn {
            padding: 10px 20px;
            border: none;
            border-radius: 6px;
            font-weight: 500;
            cursor: pointer;
            transition: all 0.3s;
        }

        .btn-primary {
            background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%);
            color: white;
        }

        .btn-success {
            background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%);
            color: white;
            padding: 8px 15px;
            font-size: 13px;
        }

        .btn-danger {
            background: linear-gradient(135deg, #f5576c 0%, #f093fb 100%);
            color: white;
            padding: 8px 15px;
            font-size: 13px;
        }

        .btn-sm {
            padding: 6px 12px;
            font-size: 12px;
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
            background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%);
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

        .badge-primary {
            background: #cce5ff;
            color: #004085;
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

        .employee-info {
            display: flex;
            flex-direction: column;
            gap: 2px;
        }

        .employee-info .name {
            font-weight: 500;
            color: #333;
        }

        .employee-info .position {
            font-size: 12px;
            color: #666;
        }

        .alert {
            padding: 15px;
            border-radius: 6px;
            margin-bottom: 20px;
        }

        .alert-success { background: #d4edda; color: #155724; border: 1px solid #c3e6cb; }
        .alert-error { background: #f8d7da; color: #721c24; border: 1px solid #f5c6cb; }
        .alert-warning { background: #fff3cd; color: #856404; border: 1px solid #ffeaa7; }

        /* Modal styles */
        .modal-backdrop {
            display: none;
            position: fixed;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            background: rgba(0,0,0,0.5);
            z-index: 1000;
        }

        .modal {
            display: none;
            position: fixed;
            top: 50%;
            left: 50%;
            transform: translate(-50%, -50%);
            background: white;
            border-radius: 8px;
            padding: 25px;
            z-index: 1001;
            min-width: 400px;
            max-width: 90%;
            box-shadow: 0 5px 20px rgba(0,0,0,0.3);
        }

        .modal h3 {
            margin: 0 0 20px 0;
            padding-bottom: 10px;
            border-bottom: 2px solid #f0f0f0;
        }

        .modal .form-group {
            margin-bottom: 15px;
        }

        .modal .form-group label {
            display: block;
            margin-bottom: 5px;
            font-weight: 500;
        }

        .modal textarea {
            width: 100%;
            padding: 10px;
            border: 2px solid #e0e0e0;
            border-radius: 6px;
            font-size: 14px;
            min-height: 100px;
            box-sizing: border-box;
        }

        .modal .btn-group {
            display: flex;
            gap: 10px;
            justify-content: flex-end;
            margin-top: 20px;
        }

        .no-subordinates {
            text-align: center;
            padding: 60px 20px;
            background: white;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
        }

        .no-subordinates h3 {
            color: #666;
            margin-bottom: 10px;
        }

        .no-subordinates p {
            color: #999;
        }
    </style>

    <div class="supervisor-leave">
        <div class="section-header">
            <h2>&#9989; อนุมัติการลา (หัวหน้างาน)</h2>
        </div>

        <!-- Message Panel -->
        <asp:Panel ID="pnlMessage" runat="server" Visible="false" CssClass="alert">
            <asp:Label ID="lblMessage" runat="server"></asp:Label>
        </asp:Panel>

        <!-- No Subordinates Panel -->
        <asp:Panel ID="pnlNoSubordinates" runat="server" Visible="false">
            <div class="no-subordinates">
                <h3>&#128101; ไม่พบลูกน้องในความดูแล</h3>
                <p>คุณยังไม่มีพนักงานที่อยู่ในความดูแล กรุณาติดต่อผู้ดูแลระบบเพื่อกำหนดสิทธิ์หัวหน้างาน</p>
            </div>
        </asp:Panel>

        <!-- Main Content -->
        <asp:Panel ID="pnlMainContent" runat="server">
            <!-- Statistics -->
            <div class="stats-container">
                <div class="stat-card info">
                    <h4>&#128101; ลูกน้องในความดูแล</h4>
                    <div class="value">
                        <asp:Label ID="lblTotalSubordinates" runat="server" Text="0"></asp:Label>
                        <span style="font-size: 14px; font-weight: normal;">คน</span>
                    </div>
                </div>
                <div class="stat-card warning">
                    <h4>&#128336; คำขอรออนุมัติ</h4>
                    <div class="value">
                        <asp:Label ID="lblPendingRequests" runat="server" Text="0"></asp:Label>
                        <span style="font-size: 14px; font-weight: normal;">รายการ</span>
                    </div>
                </div>
                <div class="stat-card">
                    <h4>&#9989; อนุมัติแล้ว (ปีนี้)</h4>
                    <div class="value">
                        <asp:Label ID="lblApprovedThisYear" runat="server" Text="0"></asp:Label>
                        <span style="font-size: 14px; font-weight: normal;">รายการ</span>
                    </div>
                </div>
            </div>

            <!-- Filter Section -->
            <div class="filter-section">
                <h4 style="margin-top: 0;">&#128269; กรองข้อมูล</h4>
                <div class="filter-controls">
                    <asp:DropDownList ID="ddlStatus" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlStatus_SelectedIndexChanged">
                        <asp:ListItem Value="PENDING">รออนุมัติ</asp:ListItem>
                        <asp:ListItem Value="">ทุกสถานะ</asp:ListItem>
                        <asp:ListItem Value="APPROVED">อนุมัติแล้ว</asp:ListItem>
                        <asp:ListItem Value="REJECTED">ปฏิเสธ</asp:ListItem>
                    </asp:DropDownList>

                    <asp:DropDownList ID="ddlYear" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlYear_SelectedIndexChanged">
                    </asp:DropDownList>

                    <asp:TextBox ID="txtSearchEmployee" runat="server" CssClass="form-control" placeholder="ชื่อพนักงาน..."></asp:TextBox>
                    <asp:Button ID="btnSearch" runat="server" Text="&#128269; ค้นหา" CssClass="btn btn-primary" OnClick="btnSearch_Click" />
                </div>
            </div>

            <!-- Leave Requests Table -->
            <div class="data-table">
                <asp:GridView ID="gvLeaveRequests" runat="server" AutoGenerateColumns="False"
                    CssClass="table" GridLines="None" OnRowCommand="gvLeaveRequests_RowCommand" DataKeyNames="ID">
                    <Columns>
                        <asp:BoundField DataField="RequestNumber" HeaderText="เลขที่" />

                        <asp:TemplateField HeaderText="พนักงาน">
                            <ItemTemplate>
                                <div class="employee-info">
                                    <span class="name"><%# Eval("EmployeeName") %></span>
                                    <span class="position"><%# Eval("EmployeePosition") %></span>
                                    <%# Convert.ToBoolean(Eval("IsPrimarySupervisor")) ? "<span class='badge badge-primary'>หัวหน้าหลัก</span>" : "" %>
                                </div>
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

                        <asp:TemplateField HeaderText="วันที่ส่ง">
                            <ItemTemplate>
                                <%# Convert.ToDateTime(Eval("SubmittedDate")).ToString("dd/MM/yyyy HH:mm") %>
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:TemplateField HeaderText="สถานะ">
                            <ItemTemplate>
                                <%# GetStatusBadge(Convert.ToString(Eval("Status"))) %>
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:TemplateField HeaderText="จัดการ">
                            <ItemTemplate>
                                <asp:Panel ID="pnlActions" runat="server" Visible='<%# Convert.ToString(Eval("Status")) == "PENDING" && Convert.ToBoolean(Eval("CanApproveLeave")) %>'>
                                    <asp:Button ID="btnApprove" runat="server" Text="&#9989; อนุมัติ"
                                        CssClass="btn btn-success btn-sm" CommandName="Approve"
                                        CommandArgument='<%# Eval("ID") %>'
                                        OnClientClick="return confirm('ยืนยันการอนุมัติคำขอลานี้?');" />
                                    <asp:Button ID="btnReject" runat="server" Text="&#10060; ปฏิเสธ"
                                        CssClass="btn btn-danger btn-sm" CommandName="ShowReject"
                                        CommandArgument='<%# Eval("ID") %>' />
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
                            &#128230; ไม่พบคำขอลา
                        </div>
                    </EmptyDataTemplate>
                </asp:GridView>
            </div>
        </asp:Panel>
    </div>

    <!-- Reject Modal -->
    <div id="rejectModalBackdrop" class="modal-backdrop"></div>
    <div id="rejectModal" class="modal">
        <h3>&#10060; ปฏิเสธคำขอลา</h3>
        <div class="form-group">
            <label>เหตุผลในการปฏิเสธ:</label>
            <asp:TextBox ID="txtRejectReason" runat="server" TextMode="MultiLine" Rows="4" placeholder="กรุณาระบุเหตุผล..."></asp:TextBox>
            <asp:HiddenField ID="hdnRejectRequestId" runat="server" />
        </div>
        <div class="btn-group">
            <button type="button" class="btn" onclick="hideRejectModal()">ยกเลิก</button>
            <asp:Button ID="btnConfirmReject" runat="server" Text="&#10060; ยืนยันปฏิเสธ" CssClass="btn btn-danger" OnClick="btnConfirmReject_Click" />
        </div>
    </div>

    <script type="text/javascript">
        function showRejectModal(requestId) {
            document.getElementById('rejectModalBackdrop').style.display = 'block';
            document.getElementById('rejectModal').style.display = 'block';
            document.getElementById('<%= hdnRejectRequestId.ClientID %>').value = requestId;
        }

        function hideRejectModal() {
            document.getElementById('rejectModalBackdrop').style.display = 'none';
            document.getElementById('rejectModal').style.display = 'none';
        }

        document.getElementById('rejectModalBackdrop').onclick = hideRejectModal;
    </script>
</asp:Content>
