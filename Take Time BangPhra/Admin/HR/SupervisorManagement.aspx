<%@ Page Title="จัดการหัวหน้างาน" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="SupervisorManagement.aspx.cs" Inherits="Take_Time_BangPhra.Admin.HR.SupervisorManagement" MaintainScrollPositionOnPostback="true" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .supervisor-management {
            padding: 20px;
        }

        .section-header {
            background: linear-gradient(135deg, #fa709a 0%, #fee140 100%);
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

        .btn {
            padding: 10px 20px;
            border: none;
            border-radius: 6px;
            font-weight: 500;
            cursor: pointer;
            transition: all 0.3s;
        }

        .btn-add {
            background: white;
            color: #fa709a;
        }

        .btn-primary {
            background: linear-gradient(135deg, #fa709a 0%, #fee140 100%);
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
            border-left: 4px solid #fa709a;
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
            border-color: #fa709a;
            outline: none;
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
            background: linear-gradient(135deg, #fa709a 0%, #fee140 100%);
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

        .badge-primary { background: #cce5ff; color: #004085; }
        .badge-success { background: #d4edda; color: #155724; }
        .badge-warning { background: #fff3cd; color: #856404; }
        .badge-secondary { background: #e2e3e5; color: #383d41; }

        .alert {
            padding: 15px;
            border-radius: 6px;
            margin-bottom: 20px;
        }

        .alert-success { background: #d4edda; color: #155724; border: 1px solid #c3e6cb; }
        .alert-error { background: #f8d7da; color: #721c24; border: 1px solid #f5c6cb; }

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
            min-width: 500px;
            max-width: 90%;
            max-height: 90vh;
            overflow-y: auto;
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

        .modal .checkbox-group {
            display: flex;
            gap: 20px;
            flex-wrap: wrap;
        }

        .modal .checkbox-item {
            display: flex;
            align-items: center;
            gap: 5px;
        }

        .modal .btn-group {
            display: flex;
            gap: 10px;
            justify-content: flex-end;
            margin-top: 20px;
        }

        .relationship-info {
            display: flex;
            flex-direction: column;
            gap: 5px;
        }

        .relationship-info .arrow {
            text-align: center;
            color: #fa709a;
            font-size: 18px;
        }
    </style>

    <div class="supervisor-management">
        <div class="section-header">
            <h2>&#128101; จัดการหัวหน้างาน-ลูกน้อง</h2>
            <button type="button" class="btn btn-add" onclick="showAddModal()">+ เพิ่มความสัมพันธ์</button>
        </div>

        <!-- Message Panel -->
        <asp:Panel ID="pnlMessage" runat="server" Visible="false" CssClass="alert">
            <asp:Label ID="lblMessage" runat="server"></asp:Label>
        </asp:Panel>

        <!-- Statistics -->
        <div class="stats-container">
            <div class="stat-card">
                <h4>&#128101; ความสัมพันธ์ทั้งหมด</h4>
                <div class="value">
                    <asp:Label ID="lblTotalRelationships" runat="server" Text="0"></asp:Label>
                </div>
            </div>
            <div class="stat-card">
                <h4>&#128100; พนักงานที่มีหัวหน้างาน</h4>
                <div class="value">
                    <asp:Label ID="lblEmployeesWithSupervisor" runat="server" Text="0"></asp:Label>
                </div>
            </div>
            <div class="stat-card">
                <h4>&#127919; หัวหน้างานทั้งหมด</h4>
                <div class="value">
                    <asp:Label ID="lblTotalSupervisors" runat="server" Text="0"></asp:Label>
                </div>
            </div>
        </div>

        <!-- Filter Section -->
        <div class="filter-section">
            <h4 style="margin-top: 0;">&#128269; กรองข้อมูล</h4>
            <div class="filter-controls">
                <asp:TextBox ID="txtSearch" runat="server" CssClass="form-control" placeholder="ค้นหาชื่อพนักงานหรือหัวหน้างาน..."></asp:TextBox>
                <asp:Button ID="btnSearch" runat="server" Text="&#128269; ค้นหา" CssClass="btn btn-primary" OnClick="btnSearch_Click" />
                <asp:Button ID="btnReset" runat="server" Text="&#8635; รีเซ็ต" CssClass="btn btn-secondary" OnClick="btnReset_Click" CausesValidation="false" />
            </div>
        </div>

        <!-- Relationships Table -->
        <div class="data-table">
            <asp:GridView ID="gvRelationships" runat="server" AutoGenerateColumns="False"
                CssClass="table" GridLines="None" OnRowCommand="gvRelationships_RowCommand" DataKeyNames="ID">
                <Columns>
                    <asp:TemplateField HeaderText="พนักงาน (ลูกน้อง)">
                        <ItemTemplate>
                            <strong><%# Eval("EmployeeName") %></strong><br />
                            <small style="color: #666;"><%# Eval("EmployeeUsername") %> | <%# Eval("EmployeeRole") %></small>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="">
                        <ItemTemplate>
                            <div class="relationship-info">
                                <div class="arrow">&#8594;</div>
                            </div>
                        </ItemTemplate>
                        <ItemStyle Width="40px" HorizontalAlign="Center" />
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="หัวหน้างาน">
                        <ItemTemplate>
                            <strong><%# Eval("SupervisorName") %></strong><br />
                            <small style="color: #666;"><%# Eval("SupervisorUsername") %> | <%# Eval("SupervisorRole") %></small>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="ประเภท">
                        <ItemTemplate>
                            <%# Convert.ToBoolean(Eval("IsPrimary")) ? "<span class='badge badge-primary'>หัวหน้าหลัก</span>" : "<span class='badge badge-secondary'>หัวหน้ารอง</span>" %>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="สิทธิ์">
                        <ItemTemplate>
                            <%# Convert.ToBoolean(Eval("CanApproveLeave")) ? "<span class='badge badge-success'>อนุมัติลา</span>" : "" %>
                            <%# Convert.ToBoolean(Eval("CanViewSalary")) ? "<span class='badge badge-warning'>ดูเงินเดือน</span>" : "" %>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="วันที่กำหนด">
                        <ItemTemplate>
                            <%# Convert.ToDateTime(Eval("AssignedDate")).ToString("dd/MM/yyyy") %>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="สถานะ">
                        <ItemTemplate>
                            <%# Convert.ToBoolean(Eval("IsActive")) ? "<span class='badge badge-success'>ใช้งาน</span>" : "<span class='badge badge-secondary'>ปิดใช้งาน</span>" %>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="จัดการ">
                        <ItemTemplate>
                            <asp:Button ID="btnDelete" runat="server" Text="&#128465; ลบ"
                                CssClass="btn btn-danger btn-sm" CommandName="DeleteRelation"
                                CommandArgument='<%# Eval("Employee_AdminID") + "," + Eval("Supervisor_AdminID") %>'
                                OnClientClick="return confirm('ยืนยันการลบความสัมพันธ์นี้?');" />
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
                <EmptyDataTemplate>
                    <div style="text-align: center; padding: 40px; color: #999;">
                        &#128230; ยังไม่มีข้อมูลความสัมพันธ์หัวหน้างาน-ลูกน้อง
                    </div>
                </EmptyDataTemplate>
            </asp:GridView>
        </div>
    </div>

    <!-- Add Modal -->
    <div id="addModalBackdrop" class="modal-backdrop"></div>
    <div id="addModal" class="modal">
        <h3>&#10133; เพิ่มความสัมพันธ์หัวหน้างาน-ลูกน้อง</h3>
        <div class="form-group">
            <label>เลือกพนักงาน (ลูกน้อง): <span style="color: red;">*</span></label>
            <asp:DropDownList ID="ddlEmployee" runat="server" CssClass="form-control" style="width: 100%;">
                <asp:ListItem Value="">-- เลือกพนักงาน --</asp:ListItem>
            </asp:DropDownList>
        </div>
        <div class="form-group">
            <label>เลือกหัวหน้างาน: <span style="color: red;">*</span></label>
            <asp:DropDownList ID="ddlSupervisor" runat="server" CssClass="form-control" style="width: 100%;">
                <asp:ListItem Value="">-- เลือกหัวหน้างาน --</asp:ListItem>
            </asp:DropDownList>
        </div>
        <div class="form-group">
            <label>ตัวเลือก:</label>
            <div class="checkbox-group">
                <div class="checkbox-item">
                    <asp:CheckBox ID="chkIsPrimary" runat="server" />
                    <label for="<%= chkIsPrimary.ClientID %>">หัวหน้างานหลัก</label>
                </div>
                <div class="checkbox-item">
                    <asp:CheckBox ID="chkCanApproveLeave" runat="server" Checked="true" />
                    <label for="<%= chkCanApproveLeave.ClientID %>">อนุมัติการลาได้</label>
                </div>
                <div class="checkbox-item">
                    <asp:CheckBox ID="chkCanViewSalary" runat="server" />
                    <label for="<%= chkCanViewSalary.ClientID %>">ดูเงินเดือนได้</label>
                </div>
            </div>
        </div>
        <div class="form-group">
            <label>หมายเหตุ:</label>
            <asp:TextBox ID="txtNotes" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="2"></asp:TextBox>
        </div>
        <div class="btn-group">
            <button type="button" class="btn btn-secondary" onclick="hideAddModal()">ยกเลิก</button>
            <asp:Button ID="btnSaveRelationship" runat="server" Text="&#128190; บันทึก" CssClass="btn btn-success" OnClick="btnSaveRelationship_Click" />
        </div>
    </div>

    <script type="text/javascript">
        function showAddModal() {
            document.getElementById('addModalBackdrop').style.display = 'block';
            document.getElementById('addModal').style.display = 'block';
        }

        function hideAddModal() {
            document.getElementById('addModalBackdrop').style.display = 'none';
            document.getElementById('addModal').style.display = 'none';
        }

        document.getElementById('addModalBackdrop').onclick = hideAddModal;
    </script>
</asp:Content>
