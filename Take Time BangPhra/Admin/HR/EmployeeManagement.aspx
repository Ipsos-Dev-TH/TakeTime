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
            grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
            gap: 20px;
            margin-bottom: 30px;
        }

        .stat-card {
            background: white;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            border-left: 4px solid #667eea;
        }

        .stat-card.warning {
            border-left-color: #f5576c;
        }

        .stat-card.success {
            border-left-color: #38ef7d;
        }

        .stat-card.info {
            border-left-color: #4facfe;
        }

        .stat-card h3 {
            margin: 0 0 10px 0;
            font-size: 14px;
            color: #666;
            font-weight: 500;
        }

        .stat-card .stat-value {
            font-size: 32px;
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

        .search-controls .form-control {
            flex: 1;
            min-width: 200px;
            padding: 10px 15px;
            border: 2px solid #e0e0e0;
            border-radius: 6px;
            font-size: 14px;
            min-height: 44px;
            line-height: 1.6;
        }

        /* Fix Thai text display in dropdowns */
        .search-controls select.form-control {
            height: auto;
            min-height: 44px;
            padding-top: 8px;
            padding-bottom: 8px;
        }

        .search-controls select.form-control option {
            padding: 8px 10px;
            line-height: 1.6;
        }

        .search-controls .form-control:focus {
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
            transition: transform 0.2s;
        }

        .btn-primary:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(102, 126, 234, 0.4);
        }

        .btn-success {
            background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%);
            border: none;
            color: white;
            padding: 10px 25px;
            border-radius: 6px;
            font-weight: 500;
            cursor: pointer;
        }

        .btn-info {
            background: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%);
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

        .badge-success {
            background: #d4edda;
            color: #155724;
        }

        .badge-warning {
            background: #fff3cd;
            color: #856404;
        }

        .badge-danger {
            background: #f8d7da;
            color: #721c24;
        }

        .employee-photo {
            width: 40px;
            height: 40px;
            border-radius: 50%;
            object-fit: cover;
        }

        .action-buttons {
            display: flex;
            gap: 10px;
            margin-bottom: 20px;
            flex-wrap: wrap;
        }

        .btn-add {
            background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%);
            border: none;
            color: white;
            padding: 12px 25px;
            border-radius: 6px;
            font-weight: 500;
            cursor: pointer;
            font-size: 14px;
        }

        .btn-danger {
            background: linear-gradient(135deg, #f5576c 0%, #f093fb 100%);
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

        .btn-edit {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            border: none;
            color: white;
            padding: 8px 15px;
            border-radius: 6px;
            font-size: 13px;
            cursor: pointer;
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
            padding: 30px;
            max-width: 600px;
            width: 90%;
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
        }

        .form-group input, .form-group select {
            width: 100%;
            padding: 10px 15px;
            border: 2px solid #e0e0e0;
            border-radius: 6px;
            font-size: 14px;
            box-sizing: border-box;
        }

        .form-group input:focus, .form-group select:focus {
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

        .btn-cancel {
            background: #e0e0e0;
            border: none;
            color: #333;
            padding: 10px 25px;
            border-radius: 6px;
            font-weight: 500;
            cursor: pointer;
        }

        .action-cell {
            display: flex;
            gap: 5px;
            flex-wrap: wrap;
        }
    </style>

    <div class="employee-management">
        <div class="section-header">
            <h2>📋 ระบบจัดการข้อมูลพนักงาน</h2>
        </div>

        <!-- Statistics Cards -->
        <div class="stats-container">
            <div class="stat-card">
                <h3>พนักงานทั้งหมด</h3>
                <div class="stat-value">
                    <asp:Label ID="lblTotalEmployees" runat="server" Text="0"></asp:Label>
                </div>
            </div>
            <div class="stat-card success">
                <h3>พนักงานใหม่เดือนนี้</h3>
                <div class="stat-value">
                    <asp:Label ID="lblNewEmployees" runat="server" Text="0"></asp:Label>
                </div>
            </div>
            <div class="stat-card warning">
                <h3>สัญญาใกล้หมดอายุ (30 วัน)</h3>
                <div class="stat-value">
                    <asp:Label ID="lblExpiringContracts" runat="server" Text="0"></asp:Label>
                </div>
            </div>
            <div class="stat-card info">
                <h3>เอกสารใกล้หมดอายุ</h3>
                <div class="stat-value">
                    <asp:Label ID="lblExpiringDocuments" runat="server" Text="0"></asp:Label>
                </div>
            </div>
        </div>

        <!-- Action Buttons -->
        <div class="action-buttons">
            <asp:Button ID="btnAddEmployee" runat="server" Text="➕ เพิ่มพนักงานใหม่" CssClass="btn-add" OnClientClick="openAddModal(); return false;" />
        </div>

        <!-- Search Section -->
        <div class="search-section">
            <h4 style="margin-top: 0;">🔍 ค้นหาพนักงาน</h4>
            <div class="search-controls">
                <asp:TextBox ID="txtSearch" runat="server" CssClass="form-control" placeholder="ชื่อ, เบอร์โทร, ตำแหน่ง..."></asp:TextBox>
                <asp:DropDownList ID="ddlDepartment" runat="server" CssClass="form-control">
                    <asp:ListItem Value="">ทุกแผนก</asp:ListItem>
                    <asp:ListItem Value="Owner">Owner</asp:ListItem>
                    <asp:ListItem Value="Admin">Admin</asp:ListItem>
                    <asp:ListItem Value="Staff">Staff</asp:ListItem>
                </asp:DropDownList>
                <asp:DropDownList ID="ddlStatus" runat="server" CssClass="form-control">
                    <asp:ListItem Value="1">พนักงานปัจจุบัน</asp:ListItem>
                    <asp:ListItem Value="0">ลาออก/พ้นสภาพ</asp:ListItem>
                    <asp:ListItem Value="">ทั้งหมด</asp:ListItem>
                </asp:DropDownList>
                <asp:Button ID="btnSearch" runat="server" Text="ค้นหา" CssClass="btn-primary" OnClick="btnSearch_Click" />
                <asp:Button ID="btnReset" runat="server" Text="รีเซ็ต" CssClass="btn-success" OnClick="btnReset_Click" />
            </div>
        </div>

        <!-- Employee List -->
        <div class="data-table">
            <asp:GridView ID="gvEmployees" runat="server" AutoGenerateColumns="False"
                CssClass="table" GridLines="None" OnRowCommand="gvEmployees_RowCommand">
                <Columns>
                    <asp:TemplateField HeaderText="รูปภาพ">
                        <ItemTemplate>
                            <img src='<%# Eval("PhotoPath") != DBNull.Value ? ResolveUrl("~/" + Eval("PhotoPath")) : ResolveUrl("~/Images/default-avatar.png") %>'
                                 alt="Photo" class="employee-photo" />
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:BoundField DataField="Name" HeaderText="ชื่อ-นามสกุล" />
                    <asp:BoundField DataField="CurrentPosition" HeaderText="ตำแหน่ง" />
                    <asp:BoundField DataField="Department" HeaderText="แผนก" />

                    <asp:TemplateField HeaderText="อายุงาน">
                        <ItemTemplate>
                            <%# GetServiceAgeText(Eval("TotalServiceYears"), Eval("TotalServiceMonths")) %>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="เงินเดือน">
                        <ItemTemplate>
                            ฿<%# Eval("CurrentSalary") != DBNull.Value ? string.Format("{0:N2}", Eval("CurrentSalary")) : "N/A" %>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:BoundField DataField="MobilePhone" HeaderText="เบอร์โทร" />

                    <asp:TemplateField HeaderText="สถานะสัญญา">
                        <ItemTemplate>
                            <%# GetContractStatusBadge(Eval("ContractDaysUntilExpiry")) %>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="จัดการ">
                        <ItemTemplate>
                            <div class="action-cell">
                                <asp:Button ID="btnViewProfile" runat="server" Text="ดูข้อมูล"
                                    CssClass="btn-info" CommandName="ViewProfile"
                                    CommandArgument='<%# Eval("Admin_ID") %>' />
                                <asp:Button ID="btnResign" runat="server" Text="ลาออก"
                                    CssClass="btn-danger" CommandName="Resign"
                                    CommandArgument='<%# Eval("Admin_ID") %>'
                                    OnClientClick="return confirm('ยืนยันการบันทึกลาออกของพนักงานนี้?');" />
                            </div>
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
                <EmptyDataTemplate>
                    <div style="text-align: center; padding: 40px; color: #999;">
                        ไม่พบข้อมูลพนักงาน
                    </div>
                </EmptyDataTemplate>
            </asp:GridView>
        </div>

        <!-- Hidden fields for modal data -->
        <asp:HiddenField ID="hdnEmployeeId" runat="server" />

        <!-- Add Employee Modal -->
        <div id="addEmployeeModal" class="modal-overlay">
            <div class="modal-content">
                <div class="modal-header">
                    <h3>➕ เพิ่มพนักงานใหม่</h3>
                    <button type="button" class="modal-close" onclick="closeAddModal()">&times;</button>
                </div>

                <div class="form-row">
                    <div class="form-group">
                        <label>ชื่อผู้ใช้ (Username) *</label>
                        <asp:TextBox ID="txtUsername" runat="server" CssClass="form-control" placeholder="username"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label>รหัสผ่าน *</label>
                        <asp:TextBox ID="txtPassword" runat="server" CssClass="form-control" TextMode="Password" placeholder="รหัสผ่าน"></asp:TextBox>
                    </div>
                </div>

                <div class="form-row">
                    <div class="form-group">
                        <label>ชื่อ *</label>
                        <asp:TextBox ID="txtFirstName" runat="server" CssClass="form-control" placeholder="ชื่อ"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label>นามสกุล *</label>
                        <asp:TextBox ID="txtLastName" runat="server" CssClass="form-control" placeholder="นามสกุล"></asp:TextBox>
                    </div>
                </div>

                <div class="form-row">
                    <div class="form-group">
                        <label>ตำแหน่ง/สิทธิ์ *</label>
                        <asp:DropDownList ID="ddlRole" runat="server" CssClass="form-control">
                            <asp:ListItem Value="Staff">Staff (พนักงานทั่วไป)</asp:ListItem>
                            <asp:ListItem Value="Admin">Admin (ผู้ดูแลระบบ)</asp:ListItem>
                            <asp:ListItem Value="Owner">Owner (เจ้าของ)</asp:ListItem>
                        </asp:DropDownList>
                    </div>
                    <div class="form-group">
                        <label>เงินเดือน (บาท)</label>
                        <asp:TextBox ID="txtSalary" runat="server" CssClass="form-control" placeholder="0.00" TextMode="Number"></asp:TextBox>
                    </div>
                </div>

                <div class="form-group">
                    <label>ตำแหน่งงาน</label>
                    <asp:TextBox ID="txtPosition" runat="server" CssClass="form-control" placeholder="เช่น พนักงานต้อนรับ, แม่บ้าน"></asp:TextBox>
                </div>

                <div class="modal-footer">
                    <button type="button" class="btn-cancel" onclick="closeAddModal()">ยกเลิก</button>
                    <asp:Button ID="btnSaveEmployee" runat="server" Text="บันทึก" CssClass="btn-add" OnClick="btnSaveEmployee_Click" />
                </div>
            </div>
        </div>

        <!-- Resign Modal -->
        <div id="resignModal" class="modal-overlay">
            <div class="modal-content">
                <div class="modal-header">
                    <h3>📝 บันทึกการลาออก/พ้นสภาพ</h3>
                    <button type="button" class="modal-close" onclick="closeResignModal()">&times;</button>
                </div>

                <div class="form-group">
                    <label>วันที่มีผล *</label>
                    <asp:TextBox ID="txtResignDate" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                </div>

                <div class="form-group">
                    <label>ประเภท *</label>
                    <asp:DropDownList ID="ddlResignType" runat="server" CssClass="form-control">
                        <asp:ListItem Value="ลาออก">ลาออก</asp:ListItem>
                        <asp:ListItem Value="เลิกจ้าง">เลิกจ้าง</asp:ListItem>
                        <asp:ListItem Value="สิ้นสุดสัญญา">สิ้นสุดสัญญา</asp:ListItem>
                        <asp:ListItem Value="เกษียณ">เกษียณ</asp:ListItem>
                        <asp:ListItem Value="อื่นๆ">อื่นๆ</asp:ListItem>
                    </asp:DropDownList>
                </div>

                <div class="form-group">
                    <label>เหตุผล/หมายเหตุ</label>
                    <asp:TextBox ID="txtResignReason" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="3" placeholder="ระบุเหตุผล..."></asp:TextBox>
                </div>

                <div class="modal-footer">
                    <button type="button" class="btn-cancel" onclick="closeResignModal()">ยกเลิก</button>
                    <asp:Button ID="btnConfirmResign" runat="server" Text="ยืนยันลาออก" CssClass="btn-danger" OnClick="btnConfirmResign_Click" />
                </div>
            </div>
        </div>

        <!-- Message Panel -->
        <asp:Panel ID="pnlMessage" runat="server" Visible="false" CssClass="alert" style="margin-top: 20px; padding: 15px; border-radius: 8px;">
            <asp:Label ID="lblMessage" runat="server"></asp:Label>
        </asp:Panel>
    </div>

    <script type="text/javascript">
        function openAddModal() {
            document.getElementById('addEmployeeModal').style.display = 'flex';
        }

        function closeAddModal() {
            document.getElementById('addEmployeeModal').style.display = 'none';
        }

        function openResignModal(employeeId) {
            document.getElementById('<%= hdnEmployeeId.ClientID %>').value = employeeId;
            document.getElementById('resignModal').style.display = 'flex';
        }

        function closeResignModal() {
            document.getElementById('resignModal').style.display = 'none';
        }

        // Close modal when clicking outside
        window.onclick = function(event) {
            if (event.target.className === 'modal-overlay') {
                event.target.style.display = 'none';
            }
        }
    </script>
</asp:Content>
