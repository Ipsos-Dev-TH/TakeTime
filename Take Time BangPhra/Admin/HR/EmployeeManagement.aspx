<%@ Page Title="จัดการข้อมูลพนักงาน" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="EmployeeManagement.aspx.cs" Inherits="Take_Time_BangPhra.Admin.HR.EmployeeManagement" MaintainScrollPositionOnPostback="true" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .employee-management {
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

        .stat-card.warning { border-left-color: #f5576c; }
        .stat-card.success { border-left-color: #38ef7d; }
        .stat-card.info { border-left-color: #4facfe; }

        .stat-card h3 {
            margin: 0 0 10px 0;
            font-size: 13px;
            color: #666;
            font-weight: 500;
        }

        .stat-card .stat-value {
            font-size: 28px;
            font-weight: 700;
            color: #333;
        }

        .search-section {
            background: white;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            margin-bottom: 20px;
        }

        .search-controls {
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
            min-height: 44px;
            line-height: 1.6;
        }

        .search-controls .form-control {
            flex: 1;
            min-width: 150px;
        }

        select.form-control {
            height: auto;
            min-height: 44px;
        }

        .form-control:focus {
            border-color: #667eea;
            outline: none;
        }

        .btn {
            padding: 10px 20px;
            border: none;
            border-radius: 6px;
            font-weight: 500;
            cursor: pointer;
            transition: all 0.2s;
            font-size: 14px;
        }

        .btn:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(0,0,0,0.2);
        }

        .btn-primary {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
        }

        .btn-success {
            background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%);
            color: white;
        }

        .btn-info {
            background: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%);
            color: white;
            padding: 6px 12px;
            font-size: 12px;
        }

        .btn-warning {
            background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
            color: white;
            padding: 6px 12px;
            font-size: 12px;
        }

        .btn-danger {
            background: #dc3545;
            color: white;
            padding: 6px 12px;
            font-size: 12px;
        }

        .btn-edit {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 6px 12px;
            font-size: 12px;
        }

        .btn-reactivate {
            background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%);
            color: white;
            padding: 6px 12px;
            font-size: 12px;
        }

        .btn-add {
            background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%);
            color: white;
            padding: 12px 25px;
        }

        .btn-cancel {
            background: #e0e0e0;
            color: #333;
        }

        .action-buttons {
            display: flex;
            gap: 10px;
            margin-bottom: 20px;
            flex-wrap: wrap;
        }

        .data-table {
            background: white;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            overflow-x: auto;
        }

        .data-table table {
            width: 100%;
            border-collapse: collapse;
            min-width: 900px;
        }

        .data-table th {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 12px 8px;
            text-align: left;
            font-weight: 500;
            font-size: 13px;
        }

        .data-table td {
            padding: 10px 8px;
            border-bottom: 1px solid #f0f0f0;
            font-size: 13px;
        }

        .data-table tr:hover {
            background-color: #f8f9fa;
        }

        .data-table tr.inactive {
            background-color: #fff5f5;
        }

        .badge {
            display: inline-block;
            padding: 4px 8px;
            border-radius: 4px;
            font-size: 11px;
            font-weight: 500;
        }

        .badge-success { background: #d4edda; color: #155724; }
        .badge-warning { background: #fff3cd; color: #856404; }
        .badge-danger { background: #f8d7da; color: #721c24; }
        .badge-secondary { background: #e2e3e5; color: #383d41; }

        .employee-photo {
            width: 40px;
            height: 40px;
            border-radius: 50%;
            object-fit: cover;
        }

        .action-cell {
            display: flex;
            gap: 4px;
            flex-wrap: wrap;
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
            border-radius: 10px;
            padding: 25px;
            max-width: 600px;
            width: 95%;
            max-height: 90vh;
            overflow-y: auto;
            box-shadow: 0 10px 40px rgba(0,0,0,0.3);
        }

        .modal-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 20px;
            padding-bottom: 15px;
            border-bottom: 2px solid #f0f0f0;
        }

        .modal-header h3 {
            margin: 0;
            color: #333;
            font-size: 18px;
        }

        .modal-close {
            background: none;
            border: none;
            font-size: 24px;
            cursor: pointer;
            color: #999;
        }

        .form-group {
            margin-bottom: 15px;
        }

        .form-group label {
            display: block;
            margin-bottom: 5px;
            font-weight: 500;
            color: #555;
            font-size: 13px;
        }

        .form-group label .required {
            color: #dc3545;
        }

        .form-group input, .form-group select, .form-group textarea {
            width: 100%;
            padding: 10px 12px;
            border: 2px solid #e0e0e0;
            border-radius: 6px;
            font-size: 14px;
            box-sizing: border-box;
        }

        .form-group input:focus, .form-group select:focus, .form-group textarea:focus {
            border-color: #667eea;
            outline: none;
        }

        .form-row {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 15px;
        }

        .modal-footer {
            margin-top: 20px;
            padding-top: 15px;
            border-top: 2px solid #f0f0f0;
            display: flex;
            justify-content: flex-end;
            gap: 10px;
        }

        .alert {
            padding: 15px;
            border-radius: 8px;
            margin-bottom: 20px;
        }

        .alert-success { background: #d4edda; color: #155724; border: 1px solid #c3e6cb; }
        .alert-error { background: #f8d7da; color: #721c24; border: 1px solid #f5c6cb; }

        .employee-name-cell {
            display: flex;
            flex-direction: column;
        }

        .employee-name-cell .name {
            font-weight: 500;
        }

        .employee-name-cell .username {
            font-size: 11px;
            color: #888;
        }
    </style>

    <div class="employee-management">
        <div class="section-header">
            <h2>&#128203; ระบบจัดการข้อมูลพนักงาน</h2>
        </div>

        <!-- Message Panel -->
        <asp:Panel ID="pnlMessage" runat="server" Visible="false" CssClass="alert">
            <asp:Label ID="lblMessage" runat="server"></asp:Label>
        </asp:Panel>

        <!-- Statistics Cards -->
        <div class="stats-container">
            <div class="stat-card">
                <h3>&#128100; พนักงานปัจจุบัน</h3>
                <div class="stat-value">
                    <asp:Label ID="lblTotalEmployees" runat="server" Text="0"></asp:Label>
                </div>
            </div>
            <div class="stat-card success">
                <h3>&#10133; พนักงานใหม่เดือนนี้</h3>
                <div class="stat-value">
                    <asp:Label ID="lblNewEmployees" runat="server" Text="0"></asp:Label>
                </div>
            </div>
            <div class="stat-card warning">
                <h3>&#128683; ลาออก/พ้นสภาพ</h3>
                <div class="stat-value">
                    <asp:Label ID="lblExpiringContracts" runat="server" Text="0"></asp:Label>
                </div>
            </div>
            <div class="stat-card info">
                <h3>&#128101; พนักงานทั้งหมด</h3>
                <div class="stat-value">
                    <asp:Label ID="lblExpiringDocuments" runat="server" Text="0"></asp:Label>
                </div>
            </div>
        </div>

        <!-- Action Buttons -->
        <div class="action-buttons">
            <button type="button" class="btn btn-add" onclick="openAddModal()">&#10133; เพิ่มพนักงานใหม่</button>
        </div>

        <!-- Search Section -->
        <div class="search-section">
            <h4 style="margin-top: 0;">&#128269; ค้นหาพนักงาน</h4>
            <div class="search-controls">
                <asp:TextBox ID="txtSearch" runat="server" CssClass="form-control" placeholder="ชื่อ, Username..."></asp:TextBox>
                <asp:DropDownList ID="ddlDepartment" runat="server" CssClass="form-control">
                    <asp:ListItem Value="">ทุกแผนก/สิทธิ์</asp:ListItem>
                    <asp:ListItem Value="Owner">Owner</asp:ListItem>
                    <asp:ListItem Value="Admin">Admin</asp:ListItem>
                    <asp:ListItem Value="Staff">Staff</asp:ListItem>
                </asp:DropDownList>
                <asp:DropDownList ID="ddlStatus" runat="server" CssClass="form-control">
                    <asp:ListItem Value="1">พนักงานปัจจุบัน</asp:ListItem>
                    <asp:ListItem Value="0">ลาออก/พ้นสภาพ</asp:ListItem>
                    <asp:ListItem Value="">ทั้งหมด</asp:ListItem>
                </asp:DropDownList>
                <asp:Button ID="btnSearch" runat="server" Text="&#128269; ค้นหา" CssClass="btn btn-primary" OnClick="btnSearch_Click" />
                <asp:Button ID="btnReset" runat="server" Text="&#128260; รีเซ็ต" CssClass="btn btn-cancel" OnClick="btnReset_Click" />
            </div>
        </div>

        <!-- Employee List -->
        <div class="data-table">
            <asp:GridView ID="gvEmployees" runat="server" AutoGenerateColumns="False"
                CssClass="table" GridLines="None" OnRowCommand="gvEmployees_RowCommand" DataKeyNames="Admin_ID">
                <Columns>
                    <asp:TemplateField HeaderText="ชื่อ-นามสกุล">
                        <ItemTemplate>
                            <div class="employee-name-cell">
                                <span class="name"><%# Eval("Name") %></span>
                                <span class="username">@<%# Eval("Username") %></span>
                            </div>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:BoundField DataField="CurrentPosition" HeaderText="ตำแหน่ง" />
                    <asp:BoundField DataField="Department" HeaderText="สิทธิ์" />

                    <asp:TemplateField HeaderText="เงินเดือน">
                        <ItemTemplate>
                            <%# Eval("CurrentSalary") != DBNull.Value ? string.Format("฿{0:N0}", Eval("CurrentSalary")) : "-" %>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="สถานะ">
                        <ItemTemplate>
                            <%# Convert.ToInt32(Eval("Status")) == 1
                                ? "<span class='badge badge-success'>ทำงาน</span>"
                                : "<span class='badge badge-danger'>ลาออก</span>" %>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="จัดการ">
                        <ItemTemplate>
                            <div class="action-cell">
                                <asp:Button ID="btnEdit" runat="server" Text="&#9998; แก้ไข"
                                    CssClass="btn-edit" CommandName="EditEmployee"
                                    CommandArgument='<%# Eval("Admin_ID") %>' />

                                <asp:Button ID="btnResign" runat="server" Text="&#128683; ลาออก"
                                    CssClass="btn-danger" CommandName="Resign"
                                    CommandArgument='<%# Eval("Admin_ID") %>'
                                    Visible='<%# Convert.ToInt32(Eval("Status")) == 1 %>'
                                    OnClientClick="return confirm('ยืนยันการบันทึกลาออก?');" />

                                <asp:Button ID="btnReactivate" runat="server" Text="&#9989; เรียกกลับ"
                                    CssClass="btn-reactivate" CommandName="Reactivate"
                                    CommandArgument='<%# Eval("Admin_ID") %>'
                                    Visible='<%# Convert.ToInt32(Eval("Status")) == 0 %>'
                                    OnClientClick="return confirm('ยืนยันการเรียกกลับเข้าทำงาน?');" />

                                <asp:Button ID="btnDelete" runat="server" Text="&#128465; ลบ"
                                    CssClass="btn-danger" CommandName="DeleteEmployee"
                                    CommandArgument='<%# Eval("Admin_ID") %>'
                                    OnClientClick="return confirm('ยืนยันการลบพนักงานนี้? (ไม่สามารถกู้คืนได้)');" />
                            </div>
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
                <EmptyDataTemplate>
                    <div style="text-align: center; padding: 40px; color: #999;">
                        &#128230; ไม่พบข้อมูลพนักงาน
                    </div>
                </EmptyDataTemplate>
            </asp:GridView>
        </div>

        <!-- Hidden fields -->
        <asp:HiddenField ID="hdnEmployeeId" runat="server" />
        <asp:HiddenField ID="hdnEditMode" runat="server" Value="add" />

        <!-- Add/Edit Employee Modal -->
        <div id="employeeModal" class="modal-overlay">
            <div class="modal-content">
                <div class="modal-header">
                    <h3 id="modalTitle">&#10133; เพิ่มพนักงานใหม่</h3>
                    <button type="button" class="modal-close" onclick="closeModal()">&times;</button>
                </div>

                <div class="form-row">
                    <div class="form-group">
                        <label>ชื่อผู้ใช้ (Username) <span class="required">*</span></label>
                        <asp:TextBox ID="txtUsername" runat="server" CssClass="form-control" placeholder="username"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label>รหัสผ่าน <span class="required" id="passwordRequired">*</span></label>
                        <asp:TextBox ID="txtPassword" runat="server" CssClass="form-control" placeholder="รหัสผ่าน (ว่างไว้ถ้าไม่เปลี่ยน)"></asp:TextBox>
                    </div>
                </div>

                <div class="form-row">
                    <div class="form-group">
                        <label>ชื่อ <span class="required">*</span></label>
                        <asp:TextBox ID="txtFirstName" runat="server" CssClass="form-control" placeholder="ชื่อ"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label>นามสกุล</label>
                        <asp:TextBox ID="txtLastName" runat="server" CssClass="form-control" placeholder="นามสกุล"></asp:TextBox>
                    </div>
                </div>

                <div class="form-row">
                    <div class="form-group">
                        <label>สิทธิ์/Role <span class="required">*</span></label>
                        <asp:DropDownList ID="ddlRole" runat="server" CssClass="form-control">
                            <asp:ListItem Value="Staff">Staff (พนักงานทั่วไป)</asp:ListItem>
                            <asp:ListItem Value="Admin">Admin (ผู้ดูแลระบบ)</asp:ListItem>
                            <asp:ListItem Value="Owner">Owner (เจ้าของ)</asp:ListItem>
                        </asp:DropDownList>
                    </div>
                    <div class="form-group">
                        <label>เงินเดือน (บาท)</label>
                        <asp:TextBox ID="txtSalary" runat="server" CssClass="form-control" placeholder="0" TextMode="Number"></asp:TextBox>
                    </div>
                </div>

                <div class="form-group">
                    <label>ตำแหน่งงาน</label>
                    <asp:TextBox ID="txtPosition" runat="server" CssClass="form-control" placeholder="เช่น พนักงานต้อนรับ, แม่บ้าน, ผู้จัดการ"></asp:TextBox>
                </div>

                <div class="modal-footer">
                    <button type="button" class="btn btn-cancel" onclick="closeModal()">ยกเลิก</button>
                    <asp:Button ID="btnSaveEmployee" runat="server" Text="&#128190; บันทึก" CssClass="btn btn-success" OnClick="btnSaveEmployee_Click" />
                </div>
            </div>
        </div>
    </div>

    <script type="text/javascript">
        function openAddModal() {
            document.getElementById('<%= hdnEditMode.ClientID %>').value = 'add';
            document.getElementById('<%= hdnEmployeeId.ClientID %>').value = '';
            document.getElementById('modalTitle').innerHTML = '&#10133; เพิ่มพนักงานใหม่';
            document.getElementById('passwordRequired').style.display = 'inline';

            // Clear form
            document.getElementById('<%= txtUsername.ClientID %>').value = '';
            document.getElementById('<%= txtPassword.ClientID %>').value = '';
            document.getElementById('<%= txtFirstName.ClientID %>').value = '';
            document.getElementById('<%= txtLastName.ClientID %>').value = '';
            document.getElementById('<%= txtSalary.ClientID %>').value = '';
            document.getElementById('<%= txtPosition.ClientID %>').value = '';
            document.getElementById('<%= ddlRole.ClientID %>').selectedIndex = 0;

            // Enable username field
            document.getElementById('<%= txtUsername.ClientID %>').disabled = false;

            document.getElementById('employeeModal').style.display = 'flex';
        }

        function openEditModal(adminId, username, firstName, lastName, role, salary, position) {
            document.getElementById('<%= hdnEditMode.ClientID %>').value = 'edit';
            document.getElementById('<%= hdnEmployeeId.ClientID %>').value = adminId;
            document.getElementById('modalTitle').innerHTML = '&#9998; แก้ไขข้อมูลพนักงาน';
            document.getElementById('passwordRequired').style.display = 'none';

            // Fill form
            document.getElementById('<%= txtUsername.ClientID %>').value = username;
            document.getElementById('<%= txtPassword.ClientID %>').value = '';
            document.getElementById('<%= txtFirstName.ClientID %>').value = firstName;
            document.getElementById('<%= txtLastName.ClientID %>').value = lastName;
            document.getElementById('<%= txtSalary.ClientID %>').value = salary || '';
            document.getElementById('<%= txtPosition.ClientID %>').value = position || '';

            // Set role
            var roleSelect = document.getElementById('<%= ddlRole.ClientID %>');
            for (var i = 0; i < roleSelect.options.length; i++) {
                if (roleSelect.options[i].value === role) {
                    roleSelect.selectedIndex = i;
                    break;
                }
            }

            // Disable username field in edit mode
            document.getElementById('<%= txtUsername.ClientID %>').disabled = true;

            document.getElementById('employeeModal').style.display = 'flex';
        }

        function closeModal() {
            document.getElementById('employeeModal').style.display = 'none';
        }

        // Close modal when clicking outside
        window.onclick = function (event) {
            if (event.target.className === 'modal-overlay') {
                event.target.style.display = 'none';
            }
        }
    </script>
</asp:Content>
