<%@ Page Title="จัดการทำงานทดแทน" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="LeaveReplacement.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Leave.LeaveReplacement" MaintainScrollPositionOnPostback="true" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .leave-replacement {
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

        .section-header p {
            margin: 5px 0 0 0;
            font-size: 14px;
            opacity: 0.9;
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
            border-left: 4px solid #667eea;
        }

        .stat-card.success {
            border-left-color: #38ef7d;
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

        .btn-warning {
            background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
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

        .badge-replaced {
            background: #d4edda;
            color: #155724;
        }

        .badge-not-replaced {
            background: #fff3cd;
            color: #856404;
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
            justify-content: center;
            align-items: center;
        }

        .modal-content {
            background: white;
            padding: 30px;
            border-radius: 12px;
            width: 400px;
            max-width: 90%;
            box-shadow: 0 10px 40px rgba(0,0,0,0.2);
        }

        .modal-header {
            margin-bottom: 20px;
        }

        .modal-header h3 {
            margin: 0;
            color: #333;
        }

        .modal-body {
            margin-bottom: 20px;
        }

        .form-group {
            margin-bottom: 15px;
        }

        .form-group label {
            display: block;
            margin-bottom: 5px;
            font-weight: 500;
            color: #666;
        }

        .modal-footer {
            display: flex;
            gap: 10px;
            justify-content: flex-end;
        }

        .btn-cancel {
            background: #e0e0e0;
            border: none;
            color: #333;
            padding: 10px 20px;
            border-radius: 6px;
            cursor: pointer;
        }

        .alert {
            padding: 15px 20px;
            border-radius: 8px;
            margin-bottom: 20px;
            display: none;
        }

        .alert-success {
            background: linear-gradient(135deg, #d4edda 0%, #c3e6cb 100%);
            border-left: 4px solid #28a745;
            color: #155724;
        }

        .alert-error {
            background: linear-gradient(135deg, #f8d7da 0%, #f5c6cb 100%);
            border-left: 4px solid #dc3545;
            color: #721c24;
        }
    </style>

    <div class="leave-replacement">
        <div class="section-header">
            <h2>ระบบจัดการทำงานทดแทน</h2>
            <p>บันทึกวันที่พนักงานมาทำงานทดแทนวันลา เพื่อไม่หักเงินเดือนจากการลา</p>
        </div>

        <!-- Alert Messages -->
        <asp:Panel ID="pnlAlert" runat="server" CssClass="alert" Visible="false">
            <asp:Label ID="lblAlertMessage" runat="server"></asp:Label>
        </asp:Panel>

        <!-- Statistics -->
        <div class="stats-container">
            <div class="stat-card">
                <h4>ใบลาอนุมัติแล้ว (ปีนี้)</h4>
                <div class="value">
                    <asp:Label ID="lblApprovedLeaves" runat="server" Text="0"></asp:Label>
                </div>
            </div>
            <div class="stat-card success">
                <h4>ทำงานทดแทนแล้ว</h4>
                <div class="value">
                    <asp:Label ID="lblReplacedLeaves" runat="server" Text="0"></asp:Label>
                </div>
            </div>
            <div class="stat-card">
                <h4>ยังไม่ทำงานทดแทน</h4>
                <div class="value">
                    <asp:Label ID="lblPendingReplacement" runat="server" Text="0"></asp:Label>
                </div>
            </div>
        </div>

        <!-- Filter Section -->
        <div class="filter-section">
            <h4 style="margin-top: 0;">กรองข้อมูล</h4>
            <div class="filter-controls">
                <asp:DropDownList ID="ddlYear" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlYear_SelectedIndexChanged">
                </asp:DropDownList>

                <asp:DropDownList ID="ddlReplacementStatus" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlReplacementStatus_SelectedIndexChanged">
                    <asp:ListItem Value="">ทั้งหมด</asp:ListItem>
                    <asp:ListItem Value="0">ยังไม่ทดแทน</asp:ListItem>
                    <asp:ListItem Value="1">ทดแทนแล้ว</asp:ListItem>
                </asp:DropDownList>

                <asp:TextBox ID="txtSearchEmployee" runat="server" CssClass="form-control" placeholder="ชื่อพนักงาน..."></asp:TextBox>
                <asp:Button ID="btnSearch" runat="server" Text="ค้นหา" CssClass="btn-primary" OnClick="btnSearch_Click" />
            </div>
        </div>

        <!-- Leave Requests Table -->
        <div class="data-table">
            <asp:GridView ID="gvLeaveRequests" runat="server" AutoGenerateColumns="False"
                CssClass="table" GridLines="None" OnRowCommand="gvLeaveRequests_RowCommand"
                DataKeyNames="ID">
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

                    <asp:TemplateField HeaderText="วันที่ลา">
                        <ItemTemplate>
                            <%# Convert.ToDateTime(Eval("StartDate")).ToString("dd/MM/yyyy") %> -
                            <%# Convert.ToDateTime(Eval("EndDate")).ToString("dd/MM/yyyy") %>
                            <br /><small style="color: #666;"><%# string.Format("{0:N1}", Eval("TotalDays")) %> วัน</small>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:BoundField DataField="Reason" HeaderText="เหตุผล" />

                    <asp:TemplateField HeaderText="สถานะทดแทน">
                        <ItemTemplate>
                            <%# GetReplacementBadge(Eval("IsReplaced"), Eval("ReplacementDate")) %>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="อนุมัติโดย">
                        <ItemTemplate>
                            <small>
                                <%# Eval("ApprovedByName") %><br />
                                <%# Convert.ToDateTime(Eval("ApprovedDate")).ToString("dd/MM/yyyy") %>
                            </small>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="จัดการ">
                        <ItemTemplate>
                            <asp:Panel ID="pnlNotReplaced" runat="server" Visible='<%# !Convert.ToBoolean(Eval("IsReplaced")) %>'>
                                <asp:Button ID="btnMarkReplaced" runat="server" Text="บันทึกทดแทน"
                                    CssClass="btn-success" CommandName="MarkReplaced"
                                    CommandArgument='<%# Eval("ID") %>' />
                            </asp:Panel>
                            <asp:Panel ID="pnlReplaced" runat="server" Visible='<%# Convert.ToBoolean(Eval("IsReplaced")) %>'>
                                <asp:Button ID="btnCancelReplacement" runat="server" Text="ยกเลิก"
                                    CssClass="btn-warning" CommandName="CancelReplacement"
                                    CommandArgument='<%# Eval("ID") %>'
                                    OnClientClick="return confirm('ยืนยันการยกเลิกทำงานทดแทน? วันลาจะถูกนับเป็นวันหักเงินเดือน');" />
                                <br />
                                <small style="color: #28a745;">
                                    <%# Eval("ReplacedByName") %><br />
                                    <%# Eval("ReplacementApprovedDate") != DBNull.Value ? Convert.ToDateTime(Eval("ReplacementApprovedDate")).ToString("dd/MM/yyyy") : "" %>
                                </small>
                            </asp:Panel>
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
                <EmptyDataTemplate>
                    <div style="text-align: center; padding: 40px; color: #999;">
                        ไม่พบข้อมูลใบลาที่อนุมัติแล้ว
                    </div>
                </EmptyDataTemplate>
            </asp:GridView>
        </div>

        <!-- Hidden fields for modal -->
        <asp:HiddenField ID="hfSelectedLeaveId" runat="server" />

        <!-- Replacement Date Modal -->
        <div id="replacementModal" class="modal-overlay">
            <div class="modal-content">
                <div class="modal-header">
                    <h3>บันทึกวันทำงานทดแทน</h3>
                </div>
                <div class="modal-body">
                    <div class="form-group">
                        <label>วันที่มาทำงานทดแทน:</label>
                        <asp:TextBox ID="txtReplacementDate" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                    </div>
                    <p style="color: #666; font-size: 13px;">
                        * เมื่อบันทึกทำงานทดแทนแล้ว วันลานี้จะไม่ถูกนำไปหักเงินเดือน และโควตาวันลาจะถูกคืน
                    </p>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn-cancel" onclick="closeModal()">ยกเลิก</button>
                    <asp:Button ID="btnConfirmReplacement" runat="server" Text="บันทึก" CssClass="btn-success" OnClick="btnConfirmReplacement_Click" />
                </div>
            </div>
        </div>
    </div>

    <script type="text/javascript">
        function showReplacementModal(leaveId) {
            document.getElementById('<%= hfSelectedLeaveId.ClientID %>').value = leaveId;
            document.getElementById('replacementModal').style.display = 'flex';
            // Set default date to today
            var today = new Date().toISOString().split('T')[0];
            document.getElementById('<%= txtReplacementDate.ClientID %>').value = today;
        }

        function closeModal() {
            document.getElementById('replacementModal').style.display = 'none';
        }

        // Close modal when clicking outside
        document.getElementById('replacementModal').addEventListener('click', function (e) {
            if (e.target === this) {
                closeModal();
            }
        });
    </script>
</asp:Content>
