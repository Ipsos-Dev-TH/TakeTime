<%@ Page Title="บันทึกชั่วโมง OT" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="OTEntry.aspx.cs" Inherits="Take_Time_BangPhra.Admin.HR.OTEntry" MaintainScrollPositionOnPostback="true" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .ot-entry {
            padding: 20px;
            max-width: 1400px;
            margin: 0 auto;
        }

        .section-header {
            background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
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
            border-left: 4px solid #f093fb;
        }

        .stat-card.warning { border-left-color: #f5576c; }
        .stat-card.success { border-left-color: #11998e; }
        .stat-card.info { border-left-color: #4facfe; }

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
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
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
            border-color: #f093fb;
            outline: none;
        }

        select.form-control {
            height: auto;
            min-height: 44px;
        }

        textarea.form-control {
            min-height: 80px;
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
            background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
            color: white;
        }

        .btn-success {
            background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%);
            color: white;
        }

        .btn-danger {
            background: #dc3545;
            color: white;
        }

        .btn-secondary {
            background: #6c757d;
            color: white;
        }

        .btn-sm {
            padding: 6px 12px;
            font-size: 12px;
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
            background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
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

        .badge-pending { background: #fff3cd; color: #856404; }
        .badge-approved { background: #d4edda; color: #155724; }
        .badge-rejected { background: #f8d7da; color: #721c24; }

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

        .filter-controls {
            display: flex;
            gap: 10px;
            flex-wrap: wrap;
            align-items: center;
            margin-bottom: 15px;
        }

        .tabs {
            display: flex;
            border-bottom: 2px solid #f0f0f0;
            margin-bottom: 20px;
        }

        .tab {
            padding: 10px 20px;
            cursor: pointer;
            border-bottom: 3px solid transparent;
            color: #666;
            font-weight: 500;
        }

        .tab.active {
            color: #f093fb;
            border-bottom-color: #f093fb;
        }

        .tab-content {
            display: none;
        }

        .tab-content.active {
            display: block;
        }
    </style>

    <div class="ot-entry">
        <div class="section-header">
            <h2>&#128337; บันทึกชั่วโมง OT</h2>
        </div>

        <!-- Message Panel -->
        <asp:Panel ID="pnlMessage" runat="server" Visible="false" CssClass="alert">
            <asp:Label ID="lblMessage" runat="server"></asp:Label>
        </asp:Panel>

        <!-- Statistics -->
        <div class="stats-container">
            <div class="stat-card">
                <h4>&#9989; OT อนุมัติแล้ว (เดือนนี้)</h4>
                <div class="value">
                    <asp:Label ID="lblApprovedHours" runat="server" Text="0"></asp:Label>
                    <span style="font-size: 14px; font-weight: normal;">ชม.</span>
                </div>
            </div>
            <div class="stat-card warning">
                <h4>&#128336; OT รออนุมัติ</h4>
                <div class="value">
                    <asp:Label ID="lblPendingHours" runat="server" Text="0"></asp:Label>
                    <span style="font-size: 14px; font-weight: normal;">ชม.</span>
                </div>
            </div>
            <div class="stat-card info">
                <h4>&#128101; ลูกน้อง OT รออนุมัติ</h4>
                <div class="value">
                    <asp:Label ID="lblSubordinatePending" runat="server" Text="0"></asp:Label>
                    <span style="font-size: 14px; font-weight: normal;">รายการ</span>
                </div>
            </div>
        </div>

        <!-- Tabs -->
        <div class="tabs">
            <div class="tab active" onclick="showTab('entry')">&#128221; บันทึก OT</div>
            <div class="tab" onclick="showTab('myot')">&#128203; OT ของฉัน</div>
            <asp:Panel ID="pnlApprovalTab" runat="server">
                <div class="tab" onclick="showTab('approval')">&#9989; อนุมัติ OT ลูกน้อง</div>
            </asp:Panel>
        </div>

        <!-- Tab: Entry Form -->
        <div id="tabEntry" class="tab-content active">
            <!-- Supervisor Mode -->
            <asp:Panel ID="pnlSupervisorMode" runat="server" Visible="false" CssClass="supervisor-mode">
                <h4>&#128101; โหมดหัวหน้างาน - บันทึก OT ให้ลูกน้อง</h4>
                <div class="form-group">
                    <label>เลือกพนักงาน:</label>
                    <asp:DropDownList ID="ddlEmployee" runat="server" CssClass="form-control" style="max-width: 400px;">
                        <asp:ListItem Value="">-- บันทึก OT ให้ตัวเอง --</asp:ListItem>
                    </asp:DropDownList>
                </div>
            </asp:Panel>

            <div class="form-section">
                <h3>&#128221; บันทึกชั่วโมง OT</h3>
                <div class="form-row">
                    <div class="form-group">
                        <label>วันที่ทำ OT <span class="required">*</span></label>
                        <asp:TextBox ID="txtOTDate" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label>จำนวนชั่วโมง OT <span class="required">*</span></label>
                        <asp:TextBox ID="txtOTHours" runat="server" CssClass="form-control" TextMode="Number" step="0.5" min="0.5" max="24" Text="1"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label>อัตรา OT (เท่า)</label>
                        <asp:DropDownList ID="ddlOTRate" runat="server" CssClass="form-control">
                            <asp:ListItem Value="1.5">1.5 เท่า (ปกติ)</asp:ListItem>
                            <asp:ListItem Value="2">2 เท่า (วันหยุด)</asp:ListItem>
                        </asp:DropDownList>
                    </div>
                </div>
                <div class="form-group">
                    <label>รายละเอียดงานที่ทำ <span class="required">*</span></label>
                    <asp:TextBox ID="txtWorkDescription" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="3" placeholder="อธิบายงานที่ทำระหว่าง OT..."></asp:TextBox>
                </div>
                <div class="form-group">
                    <label>หมายเหตุ (ถ้ามี)</label>
                    <asp:TextBox ID="txtNotes" runat="server" CssClass="form-control" placeholder="หมายเหตุเพิ่มเติม..."></asp:TextBox>
                </div>
                <div class="btn-group">
                    <asp:Button ID="btnSaveOT" runat="server" Text="&#128190; บันทึก OT" CssClass="btn btn-primary" OnClick="btnSaveOT_Click" />
                    <asp:Button ID="btnClear" runat="server" Text="&#128260; ล้างฟอร์ม" CssClass="btn btn-secondary" OnClick="btnClear_Click" CausesValidation="false" />
                </div>
            </div>
        </div>

        <!-- Tab: My OT -->
        <div id="tabMyOT" class="tab-content">
            <div class="form-section">
                <h3>&#128203; ประวัติ OT ของฉัน</h3>
                <div class="filter-controls">
                    <asp:DropDownList ID="ddlMyMonth" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlMyMonth_SelectedIndexChanged">
                    </asp:DropDownList>
                    <asp:DropDownList ID="ddlMyYear" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlMyYear_SelectedIndexChanged">
                    </asp:DropDownList>
                </div>
                <div class="data-table">
                    <asp:GridView ID="gvMyOT" runat="server" AutoGenerateColumns="False"
                        CssClass="table" GridLines="None" OnRowCommand="gvMyOT_RowCommand">
                        <Columns>
                            <asp:TemplateField HeaderText="วันที่">
                                <ItemTemplate>
                                    <%# Convert.ToDateTime(Eval("OTDate")).ToString("dd/MM/yyyy") %>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="ชั่วโมง">
                                <ItemTemplate>
                                    <%# Eval("OTHours") %> ชม. x <%# Eval("OTRate") %> = <strong><%# string.Format("{0:N1}", Eval("OTMultipliedHours")) %></strong>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:BoundField DataField="WorkDescription" HeaderText="รายละเอียดงาน" />
                            <asp:TemplateField HeaderText="สถานะ">
                                <ItemTemplate>
                                    <%# GetStatusBadge(Eval("Status").ToString()) %>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:BoundField DataField="EnteredByName" HeaderText="บันทึกโดย" />
                            <asp:TemplateField HeaderText="จัดการ">
                                <ItemTemplate>
                                    <asp:Button ID="btnDelete" runat="server" Text="&#128465; ลบ"
                                        CssClass="btn btn-danger btn-sm" CommandName="DeleteOT"
                                        CommandArgument='<%# Eval("ID") %>'
                                        Visible='<%# Eval("Status").ToString() == "PENDING" %>'
                                        OnClientClick="return confirm('ยืนยันการลบรายการนี้?');" />
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                        <EmptyDataTemplate>
                            <div style="text-align: center; padding: 40px; color: #999;">
                                ยังไม่มีประวัติ OT
                            </div>
                        </EmptyDataTemplate>
                    </asp:GridView>
                </div>
            </div>
        </div>

        <!-- Tab: Approval -->
        <div id="tabApproval" class="tab-content">
            <div class="form-section">
                <h3>&#9989; อนุมัติ OT ลูกน้อง</h3>
                <div class="filter-controls">
                    <asp:DropDownList ID="ddlApprovalStatus" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlApprovalStatus_SelectedIndexChanged">
                        <asp:ListItem Value="PENDING">รออนุมัติ</asp:ListItem>
                        <asp:ListItem Value="">ทั้งหมด</asp:ListItem>
                        <asp:ListItem Value="APPROVED">อนุมัติแล้ว</asp:ListItem>
                        <asp:ListItem Value="REJECTED">ปฏิเสธ</asp:ListItem>
                    </asp:DropDownList>
                    <asp:DropDownList ID="ddlApprovalMonth" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlApprovalMonth_SelectedIndexChanged">
                    </asp:DropDownList>
                </div>
                <div class="data-table">
                    <asp:GridView ID="gvSubordinateOT" runat="server" AutoGenerateColumns="False"
                        CssClass="table" GridLines="None" OnRowCommand="gvSubordinateOT_RowCommand">
                        <Columns>
                            <asp:TemplateField HeaderText="พนักงาน">
                                <ItemTemplate>
                                    <strong><%# Eval("EmployeeName") %></strong><br />
                                    <small style="color: #666;"><%# Eval("EmployeePosition") %></small>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="วันที่">
                                <ItemTemplate>
                                    <%# Convert.ToDateTime(Eval("OTDate")).ToString("dd/MM/yyyy") %>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="ชั่วโมง">
                                <ItemTemplate>
                                    <%# Eval("OTHours") %> ชม. x <%# Eval("OTRate") %> = <strong><%# string.Format("{0:N1}", Eval("OTMultipliedHours")) %></strong>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:BoundField DataField="WorkDescription" HeaderText="รายละเอียดงาน" />
                            <asp:TemplateField HeaderText="สถานะ">
                                <ItemTemplate>
                                    <%# GetStatusBadge(Eval("Status").ToString()) %>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="จัดการ">
                                <ItemTemplate>
                                    <asp:Panel ID="pnlActions" runat="server" Visible='<%# Eval("Status").ToString() == "PENDING" %>'>
                                        <asp:Button ID="btnApprove" runat="server" Text="&#9989; อนุมัติ"
                                            CssClass="btn btn-success btn-sm" CommandName="ApproveOT"
                                            CommandArgument='<%# Eval("ID") %>'
                                            OnClientClick="return confirm('ยืนยันการอนุมัติ OT นี้?');" />
                                        <asp:Button ID="btnReject" runat="server" Text="&#10060; ปฏิเสธ"
                                            CssClass="btn btn-danger btn-sm" CommandName="RejectOT"
                                            CommandArgument='<%# Eval("ID") %>'
                                            OnClientClick="return confirm('ยืนยันการปฏิเสธ OT นี้?');" />
                                    </asp:Panel>
                                    <asp:Panel ID="pnlProcessed" runat="server" Visible='<%# Eval("Status").ToString() != "PENDING" %>'>
                                        <small style="color: #666;">
                                            <%# Eval("ApprovedByName") != DBNull.Value ? "โดย " + Eval("ApprovedByName") : "" %>
                                        </small>
                                    </asp:Panel>
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                        <EmptyDataTemplate>
                            <div style="text-align: center; padding: 40px; color: #999;">
                                &#128230; ไม่พบรายการ OT
                            </div>
                        </EmptyDataTemplate>
                    </asp:GridView>
                </div>
            </div>
        </div>
    </div>

    <script type="text/javascript">
        function showTab(tabName) {
            // Hide all tabs
            document.querySelectorAll('.tab-content').forEach(function (tab) {
                tab.classList.remove('active');
            });
            document.querySelectorAll('.tab').forEach(function (tab) {
                tab.classList.remove('active');
            });

            // Show selected tab
            if (tabName === 'entry') {
                document.getElementById('tabEntry').classList.add('active');
                document.querySelectorAll('.tab')[0].classList.add('active');
            } else if (tabName === 'myot') {
                document.getElementById('tabMyOT').classList.add('active');
                document.querySelectorAll('.tab')[1].classList.add('active');
            } else if (tabName === 'approval') {
                document.getElementById('tabApproval').classList.add('active');
                document.querySelectorAll('.tab')[2].classList.add('active');
            }
        }
    </script>
</asp:Content>
