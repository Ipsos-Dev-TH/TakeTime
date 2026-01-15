<%@ Page Title="ขอลางาน" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="LeaveRequest.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Leave.LeaveRequest" MaintainScrollPositionOnPostback="true" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .leave-request {
            padding: 20px;
            max-width: 1200px;
            margin: 0 auto;
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

        .info-cards {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 15px;
            margin-bottom: 20px;
        }

        .info-card {
            background: white;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
        }

        .info-card h4 {
            margin: 0 0 10px 0;
            color: #666;
            font-size: 13px;
        }

        .info-card .value {
            font-size: 24px;
            font-weight: 700;
            color: #333;
        }

        .info-card.warning { border-left: 4px solid #f5576c; }
        .info-card.success { border-left: 4px solid #11998e; }
        .info-card.info { border-left: 4px solid #4facfe; }

        .form-section {
            background: white;
            padding: 25px;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            margin-bottom: 20px;
        }

        .form-section h3 {
            margin: 0 0 20px 0;
            padding-bottom: 10px;
            border-bottom: 2px solid #f0f0f0;
            color: #333;
        }

        .form-row {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
            gap: 20px;
            margin-bottom: 15px;
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

        .form-group label .required {
            color: #dc3545;
        }

        .form-control {
            width: 100%;
            padding: 10px 15px;
            border: 2px solid #e0e0e0;
            border-radius: 6px;
            font-size: 14px;
            box-sizing: border-box;
            min-height: 44px;
            line-height: 1.6;
        }

        .form-control:focus {
            border-color: #667eea;
            outline: none;
        }

        select.form-control {
            height: auto;
            min-height: 44px;
        }

        textarea.form-control {
            min-height: 100px;
            resize: vertical;
        }

        .btn {
            padding: 12px 25px;
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

        .btn-secondary {
            background: #6c757d;
            color: white;
        }

        .btn:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(0,0,0,0.2);
        }

        .btn-group {
            display: flex;
            gap: 10px;
            margin-top: 20px;
        }

        .leave-quota-table {
            width: 100%;
            border-collapse: collapse;
            margin-top: 15px;
        }

        .leave-quota-table th,
        .leave-quota-table td {
            padding: 10px;
            border: 1px solid #e0e0e0;
            text-align: left;
        }

        .leave-quota-table th {
            background: #f8f9fa;
            font-weight: 500;
        }

        .data-table {
            background: white;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            overflow: hidden;
            margin-top: 20px;
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

        .alert-success { background: #d4edda; color: #155724; border: 1px solid #c3e6cb; }
        .alert-error { background: #f8d7da; color: #721c24; border: 1px solid #f5c6cb; }
        .alert-info { background: #cce5ff; color: #004085; border: 1px solid #b8daff; }

        .supervisor-mode {
            background: #fff3cd;
            border: 2px solid #ffc107;
            padding: 15px;
            border-radius: 6px;
            margin-bottom: 20px;
        }

        .supervisor-mode h4 {
            margin: 0 0 10px 0;
            color: #856404;
        }
    </style>

    <div class="leave-request">
        <div class="section-header">
            <h2>&#128221; ขอลางาน</h2>
        </div>

        <!-- Message Panel -->
        <asp:Panel ID="pnlMessage" runat="server" Visible="false" CssClass="alert">
            <asp:Label ID="lblMessage" runat="server"></asp:Label>
        </asp:Panel>

        <!-- Supervisor Mode Panel -->
        <asp:Panel ID="pnlSupervisorMode" runat="server" Visible="false" CssClass="supervisor-mode">
            <h4>&#128101; โหมดหัวหน้างาน - ขอลาแทนลูกน้อง</h4>
            <div class="form-group">
                <label>เลือกพนักงาน:</label>
                <asp:DropDownList ID="ddlSubordinates" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlSubordinates_SelectedIndexChanged">
                    <asp:ListItem Value="">-- เลือกพนักงาน --</asp:ListItem>
                </asp:DropDownList>
            </div>
        </asp:Panel>

        <!-- Employee Info -->
        <div class="info-cards">
            <div class="info-card info">
                <h4>&#128100; พนักงาน</h4>
                <div class="value" style="font-size: 18px;">
                    <asp:Label ID="lblEmployeeName" runat="server" Text="-"></asp:Label>
                </div>
                <small><asp:Label ID="lblPosition" runat="server" Text=""></asp:Label></small>
            </div>
            <div class="info-card" style="border-left: 4px solid #6f42c1;">
                <h4>&#128197; สิทธิ์วันลาทั้งปี</h4>
                <div class="value" style="color: #6f42c1;">
                    <asp:Label ID="lblTotalQuota" runat="server" Text="0"></asp:Label>
                    <span style="font-size: 14px; font-weight: normal;">วัน</span>
                </div>
            </div>
            <div class="info-card" style="border-left: 4px solid #fd7e14;">
                <h4>&#128200; ใช้ไปแล้ว</h4>
                <div class="value" style="color: #fd7e14;">
                    <asp:Label ID="lblTotalUsed" runat="server" Text="0"></asp:Label>
                    <span style="font-size: 14px; font-weight: normal;">วัน</span>
                </div>
            </div>
            <div class="info-card success">
                <h4>&#127919; คงเหลือ</h4>
                <div class="value" style="color: #11998e;">
                    <asp:Label ID="lblTotalRemainingDays" runat="server" Text="0"></asp:Label>
                    <span style="font-size: 14px; font-weight: normal;">วัน</span>
                </div>
            </div>
            <div class="info-card warning">
                <h4>&#128336; รออนุมัติ</h4>
                <div class="value">
                    <asp:Label ID="lblPendingRequests" runat="server" Text="0"></asp:Label>
                    <span style="font-size: 14px; font-weight: normal;">รายการ</span>
                </div>
            </div>
        </div>

        <!-- Leave Quota Table -->
        <div class="form-section">
            <h3>&#128200; โควต้าวันลา ปี <asp:Label ID="lblYear" runat="server"></asp:Label></h3>
            <asp:GridView ID="gvLeaveQuota" runat="server" AutoGenerateColumns="False"
                CssClass="leave-quota-table" GridLines="None">
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
                        ยังไม่มีโควต้าวันลา
                    </div>
                </EmptyDataTemplate>
            </asp:GridView>
        </div>

        <!-- Leave Request Form -->
        <div class="form-section">
            <h3>&#128221; สร้างคำขอลา</h3>
            <div class="form-row">
                <div class="form-group">
                    <label>ประเภทการลา <span class="required">*</span></label>
                    <asp:DropDownList ID="ddlLeaveType" runat="server" CssClass="form-control">
                        <asp:ListItem Value="">-- เลือกประเภทการลา --</asp:ListItem>
                    </asp:DropDownList>
                </div>
                <div class="form-group">
                    <label>จำนวนวันที่ลา <span class="required">*</span></label>
                    <asp:TextBox ID="txtTotalDays" runat="server" CssClass="form-control" TextMode="Number" step="0.5" min="0.5" Text="1"></asp:TextBox>
                </div>
            </div>
            <div class="form-row">
                <div class="form-group">
                    <label>วันที่เริ่มต้น <span class="required">*</span></label>
                    <asp:TextBox ID="txtStartDate" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                </div>
                <div class="form-group">
                    <label>วันที่สิ้นสุด <span class="required">*</span></label>
                    <asp:TextBox ID="txtEndDate" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                </div>
            </div>
            <div class="form-group">
                <label>เหตุผลการลา <span class="required">*</span></label>
                <asp:TextBox ID="txtReason" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="3" placeholder="กรุณาระบุเหตุผลการลา..."></asp:TextBox>
            </div>
            <div class="form-group">
                <label>แนบใบรับรองแพทย์ (ถ้ามี)</label>
                <asp:FileUpload ID="fuMedicalCert" runat="server" CssClass="form-control" />
                <small style="color: #666;">รองรับไฟล์ .jpg, .png, .pdf ขนาดไม่เกิน 5MB</small>
            </div>
            <div class="btn-group">
                <asp:Button ID="btnSubmit" runat="server" Text="&#128228; ส่งคำขอลา" CssClass="btn btn-primary" OnClick="btnSubmit_Click" />
                <asp:Button ID="btnClear" runat="server" Text="&#128260; ล้างฟอร์ม" CssClass="btn btn-secondary" OnClick="btnClear_Click" CausesValidation="false" />
            </div>
        </div>

        <!-- My Leave Requests -->
        <div class="form-section">
            <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 15px;">
                <h3 style="margin: 0;">&#128203; ประวัติการลาของฉัน (ปีนี้)</h3>
                <a href="LeaveHistory.aspx" class="btn btn-secondary" style="padding: 8px 15px; text-decoration: none;">
                    &#128197; ดูประวัติทั้งหมด
                </a>
            </div>
            <div class="data-table">
                <asp:GridView ID="gvMyLeaveRequests" runat="server" AutoGenerateColumns="False"
                    CssClass="table" GridLines="None" OnRowCommand="gvMyLeaveRequests_RowCommand">
                    <Columns>
                        <asp:BoundField DataField="RequestNumber" HeaderText="เลขที่" />
                        <asp:TemplateField HeaderText="ประเภท">
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
                        <asp:TemplateField HeaderText="ผู้อนุมัติ">
                            <ItemTemplate>
                                <%# Eval("ApprovedByName") != DBNull.Value ? Eval("ApprovedByName") : "-" %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="จัดการ">
                            <ItemTemplate>
                                <asp:LinkButton ID="btnCancel" runat="server"
                                    CommandName="CancelRequest"
                                    CommandArgument='<%# Eval("ID") %>'
                                    Visible='<%# CanCancelRequest(Convert.ToString(Eval("Status"))) %>'
                                    CssClass="btn-cancel"
                                    OnClientClick="return confirm('ต้องการยกเลิกคำขอลานี้หรือไม่?');">
                                    &#10060; ยกเลิก
                                </asp:LinkButton>
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                    <EmptyDataTemplate>
                        <div style="text-align: center; padding: 40px; color: #999;">
                            ยังไม่มีประวัติการลา
                        </div>
                    </EmptyDataTemplate>
                </asp:GridView>
            </div>
        </div>
    </div>

    <style>
        .btn-cancel {
            color: #dc3545;
            text-decoration: none;
            padding: 4px 8px;
            border: 1px solid #dc3545;
            border-radius: 4px;
            font-size: 12px;
            cursor: pointer;
        }
        .btn-cancel:hover {
            background: #dc3545;
            color: white;
        }
    </style>
</asp:Content>
