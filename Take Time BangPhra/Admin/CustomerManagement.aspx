<%@ Page Title="จัดการข้อมูลลูกค้า" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="CustomerManagement.aspx.cs" Inherits="Take_Time_BangPhra.Admin.CustomerManagement" MaintainScrollPositionOnPostback="true" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .customer-management {
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
            transition: border-color 0.3s;
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
            transition: transform 0.2s, box-shadow 0.2s;
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
            transition: transform 0.2s, box-shadow 0.2s;
        }

        .btn-success:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(17, 153, 142, 0.4);
        }

        .btn-warning {
            background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
            border: none;
            color: white;
            padding: 8px 20px;
            border-radius: 6px;
            font-weight: 500;
            cursor: pointer;
            margin-right: 5px;
        }

        .btn-danger {
            background: linear-gradient(135deg, #fa709a 0%, #fee140 100%);
            border: none;
            color: white;
            padding: 8px 20px;
            border-radius: 6px;
            font-weight: 500;
            cursor: pointer;
        }

        .form-section {
            background: white;
            padding: 25px;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            margin-bottom: 20px;
        }

        .form-section h3 {
            color: #333;
            margin-bottom: 20px;
            font-size: 20px;
            font-weight: 600;
            border-bottom: 2px solid #667eea;
            padding-bottom: 10px;
        }

        .form-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
            gap: 15px;
            margin-bottom: 20px;
        }

        .form-group {
            display: flex;
            flex-direction: column;
        }

        .form-group label {
            font-weight: 500;
            color: #555;
            margin-bottom: 5px;
            font-size: 14px;
        }

        .form-group .required {
            color: #e74c3c;
            margin-left: 3px;
        }

        .form-group input[type="text"],
        .form-group input[type="email"],
        .form-group select,
        .form-group textarea {
            padding: 10px 15px;
            border: 2px solid #e0e0e0;
            border-radius: 6px;
            font-size: 14px;
            transition: border-color 0.3s;
        }

        .form-group input:focus,
        .form-group select:focus,
        .form-group textarea:focus {
            border-color: #667eea;
            outline: none;
        }

        .form-group textarea {
            resize: vertical;
            min-height: 80px;
        }

        .form-actions {
            display: flex;
            gap: 10px;
            margin-top: 20px;
            padding-top: 20px;
            border-top: 1px solid #e0e0e0;
        }

        .grid-section {
            background: white;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            overflow-x: auto;
        }

        .grid-section h3 {
            color: #333;
            margin-bottom: 15px;
            font-size: 18px;
            font-weight: 600;
        }

        .customer-grid {
            width: 100%;
            border-collapse: collapse;
            font-size: 14px;
        }

        .customer-grid th {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 12px;
            text-align: left;
            font-weight: 600;
            border: none;
        }

        .customer-grid td {
            padding: 12px;
            border-bottom: 1px solid #e0e0e0;
        }

        .customer-grid tr:hover {
            background-color: #f8f9fa;
        }

        .customer-grid .btn-sm {
            padding: 5px 12px;
            font-size: 12px;
            margin-right: 5px;
        }

        .alert {
            padding: 15px 20px;
            border-radius: 6px;
            margin-bottom: 20px;
            font-weight: 500;
        }

        .alert-success {
            background-color: #d4edda;
            border: 1px solid #c3e6cb;
            color: #155724;
        }

        .alert-danger {
            background-color: #f8d7da;
            border: 1px solid #f5c6cb;
            color: #721c24;
        }

        .alert-warning {
            background-color: #fff3cd;
            border: 1px solid #ffeaa7;
            color: #856404;
        }

        .badge {
            padding: 5px 10px;
            border-radius: 12px;
            font-size: 12px;
            font-weight: 600;
        }

        .badge-success {
            background-color: #28a745;
            color: white;
        }

        .badge-danger {
            background-color: #dc3545;
            color: white;
        }

        .badge-info {
            background-color: #17a2b8;
            color: white;
        }

        .stats-cards {
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
            text-align: center;
        }

        .stat-card h4 {
            margin: 0;
            font-size: 32px;
            font-weight: 700;
            color: #667eea;
        }

        .stat-card p {
            margin: 5px 0 0;
            color: #666;
            font-size: 14px;
        }

        /* Modal Styles */
        .modal {
            display: none;
            position: fixed;
            z-index: 1000;
            left: 0;
            top: 0;
            width: 100%;
            height: 100%;
            background-color: rgba(0,0,0,0.5);
            animation: fadeIn 0.3s;
        }

        .modal-content {
            background-color: white;
            margin: 5% auto;
            padding: 30px;
            border-radius: 8px;
            width: 80%;
            max-width: 800px;
            max-height: 80vh;
            overflow-y: auto;
            box-shadow: 0 4px 20px rgba(0,0,0,0.3);
            animation: slideDown 0.3s;
        }

        .modal-header {
            border-bottom: 2px solid #667eea;
            padding-bottom: 15px;
            margin-bottom: 20px;
        }

        .modal-header h3 {
            margin: 0;
            color: #333;
            font-size: 22px;
        }

        .close {
            color: #aaa;
            float: right;
            font-size: 28px;
            font-weight: bold;
            cursor: pointer;
            line-height: 20px;
        }

        .close:hover,
        .close:focus {
            color: #000;
        }

        @keyframes fadeIn {
            from { opacity: 0; }
            to { opacity: 1; }
        }

        @keyframes slideDown {
            from { transform: translateY(-50px); opacity: 0; }
            to { transform: translateY(0); opacity: 1; }
        }

        .history-item {
            padding: 15px;
            border-left: 3px solid #667eea;
            background: #f8f9fa;
            margin-bottom: 10px;
            border-radius: 4px;
        }

        .history-item .date {
            color: #666;
            font-size: 12px;
            margin-bottom: 5px;
        }

        .history-item .action {
            font-weight: 600;
            color: #667eea;
            margin-bottom: 5px;
        }

        .history-item .details {
            color: #333;
            font-size: 14px;
        }

        /* Responsive */
        @media (max-width: 768px) {
            .search-controls {
                flex-direction: column;
            }

            .search-controls .form-control {
                width: 100%;
            }

            .form-grid {
                grid-template-columns: 1fr;
            }

            .modal-content {
                width: 95%;
                margin: 10% auto;
            }
        }
    </style>

    <div class="customer-management">
        <!-- Header -->
        <div class="section-header">
            <h2>📋 จัดการข้อมูลลูกค้า</h2>
        </div>

        <!-- Alert Messages -->
        <asp:Panel ID="pnlSuccess" runat="server" CssClass="alert alert-success" Visible="false">
            <asp:Label ID="lblSuccess" runat="server"></asp:Label>
        </asp:Panel>
        <asp:Panel ID="pnlError" runat="server" CssClass="alert alert-danger" Visible="false">
            <asp:Label ID="lblError" runat="server"></asp:Label>
        </asp:Panel>

        <!-- Statistics -->
        <div class="stats-cards">
            <div class="stat-card">
                <h4><asp:Label ID="lblTotalCustomers" runat="server" Text="0"></asp:Label></h4>
                <p>ลูกค้าทั้งหมด</p>
            </div>
            <div class="stat-card">
                <h4><asp:Label ID="lblActiveCustomers" runat="server" Text="0"></asp:Label></h4>
                <p>ลูกค้าที่ใช้งาน</p>
            </div>
            <div class="stat-card">
                <h4><asp:Label ID="lblNewCustomersThisMonth" runat="server" Text="0"></asp:Label></h4>
                <p>ลูกค้าใหม่เดือนนี้</p>
            </div>
        </div>

        <!-- Search Section -->
        <div class="search-section">
            <h3 style="margin-top: 0; color: #333; font-size: 18px; margin-bottom: 15px;">🔍 ค้นหาลูกค้า</h3>
            <div class="search-controls">
                <asp:TextBox ID="txtSearch" runat="server" CssClass="form-control" Placeholder="ค้นหาด้วย ชื่อ, เบอร์โทร, หรืออีเมล"></asp:TextBox>
                <asp:Button ID="btnSearch" runat="server" Text="ค้นหา" CssClass="btn-primary" OnClick="btnSearch_Click" />
                <asp:Button ID="btnClearSearch" runat="server" Text="ล้างการค้นหา" CssClass="btn-warning" OnClick="btnClearSearch_Click" />
                <asp:Button ID="btnShowAddForm" runat="server" Text="+ เพิ่มลูกค้าใหม่" CssClass="btn-success" OnClick="btnShowAddForm_Click" />
            </div>
        </div>

        <!-- Add/Edit Form -->
        <asp:Panel ID="pnlCustomerForm" runat="server" CssClass="form-section" Visible="false">
            <h3>
                <asp:Label ID="lblFormTitle" runat="server" Text="เพิ่มลูกค้าใหม่"></asp:Label>
            </h3>
            <asp:HiddenField ID="hfEditMode" runat="server" Value="false" />
            <asp:HiddenField ID="hfOriginalPhone" runat="server" />

            <div class="form-grid">
                <!-- Mobile Phone -->
                <div class="form-group">
                    <label>
                        เบอร์โทรศัพท์<span class="required">*</span>
                    </label>
                    <asp:TextBox ID="txtMobilePhone" runat="server" MaxLength="30" placeholder="0812345678"></asp:TextBox>
                    <asp:RequiredFieldValidator ID="rfvMobilePhone" runat="server"
                        ControlToValidate="txtMobilePhone"
                        ErrorMessage="กรุณากรอกเบอร์โทรศัพท์"
                        ForeColor="Red"
                        Display="Dynamic"
                        ValidationGroup="CustomerForm" />
                </div>

                <!-- Name -->
                <div class="form-group">
                    <label>
                        ชื่อ-นามสกุล<span class="required">*</span>
                    </label>
                    <asp:TextBox ID="txtName" runat="server" MaxLength="200" placeholder="นายสมชาย ใจดี"></asp:TextBox>
                    <asp:RequiredFieldValidator ID="rfvName" runat="server"
                        ControlToValidate="txtName"
                        ErrorMessage="กรุณากรอกชื่อ-นามสกุล"
                        ForeColor="Red"
                        Display="Dynamic"
                        ValidationGroup="CustomerForm" />
                </div>

                <!-- Email -->
                <div class="form-group">
                    <label>อีเมล</label>
                    <asp:TextBox ID="txtEmail" runat="server" MaxLength="100" TextMode="Email" placeholder="example@email.com"></asp:TextBox>
                    <asp:RegularExpressionValidator ID="revEmail" runat="server"
                        ControlToValidate="txtEmail"
                        ErrorMessage="รูปแบบอีเมลไม่ถูกต้อง"
                        ForeColor="Red"
                        Display="Dynamic"
                        ValidationExpression="\w+([-+.']\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*"
                        ValidationGroup="CustomerForm" />
                </div>

                <!-- Tax ID -->
                <div class="form-group">
                    <label>เลขประจำตัวผู้เสียภาษี</label>
                    <asp:TextBox ID="txtTaxID" runat="server" MaxLength="20" placeholder="1234567890123"></asp:TextBox>
                </div>

                <!-- Customer Type -->
                <div class="form-group">
                    <label>ประเภทลูกค้า</label>
                    <asp:DropDownList ID="ddlCustomerType" runat="server">
                    </asp:DropDownList>
                </div>

                <!-- Province -->
                <div class="form-group">
                    <label>จังหวัด</label>
                    <asp:TextBox ID="txtProvince" runat="server" MaxLength="100" placeholder="ชลบุรี"></asp:TextBox>
                </div>

                <!-- District -->
                <div class="form-group">
                    <label>อำเภอ/เขต</label>
                    <asp:TextBox ID="txtDistrict" runat="server" MaxLength="100" placeholder="บางละมุง"></asp:TextBox>
                </div>

                <!-- Subdistrict -->
                <div class="form-group">
                    <label>ตำบล/แขวง</label>
                    <asp:TextBox ID="txtSubdistrict" runat="server" MaxLength="100" placeholder="บางพระ"></asp:TextBox>
                </div>

                <!-- Postcode -->
                <div class="form-group">
                    <label>รหัสไปรษณีย์</label>
                    <asp:TextBox ID="txtPostcode" runat="server" MaxLength="10" placeholder="20150"></asp:TextBox>
                </div>
            </div>

            <!-- Address (Full Width) -->
            <div class="form-group">
                <label>ที่อยู่</label>
                <asp:TextBox ID="txtAddress" runat="server" TextMode="MultiLine" Rows="3" placeholder="บ้านเลขที่ หมู่ ซอย ถนน"></asp:TextBox>
            </div>

            <!-- Form Actions -->
            <div class="form-actions">
                <asp:Button ID="btnSaveCustomer" runat="server" Text="บันทึก" CssClass="btn-success" OnClick="btnSaveCustomer_Click" ValidationGroup="CustomerForm" />
                <asp:Button ID="btnCancelForm" runat="server" Text="ยกเลิก" CssClass="btn-warning" OnClick="btnCancelForm_Click" CausesValidation="false" />
            </div>
        </asp:Panel>

        <!-- Customer List Grid -->
        <div class="grid-section">
            <h3>📋 รายการลูกค้าทั้งหมด</h3>
            <asp:GridView ID="gvCustomers" runat="server"
                AutoGenerateColumns="False"
                CssClass="customer-grid"
                AllowPaging="True"
                PageSize="20"
                OnPageIndexChanging="gvCustomers_PageIndexChanging"
                OnRowCommand="gvCustomers_RowCommand"
                EmptyDataText="ไม่พบข้อมูลลูกค้า">
                <Columns>
                    <asp:BoundField DataField="MobilePhone" HeaderText="เบอร์โทร" />
                    <asp:BoundField DataField="Name" HeaderText="ชื่อ-นามสกุล" />
                    <asp:BoundField DataField="Email" HeaderText="อีเมล" NullDisplayText="-" />
                    <asp:BoundField DataField="Province" HeaderText="จังหวัด" NullDisplayText="-" />
                    <asp:BoundField DataField="CustomerTypeName" HeaderText="ประเภท" NullDisplayText="-" />
                    <asp:BoundField DataField="TotalReservations" HeaderText="จำนวนจอง" />
                    <asp:TemplateField HeaderText="สถานะ">
                        <ItemTemplate>
                            <span class='badge <%# Convert.ToBoolean(Eval("IsActive")) ? "badge-success" : "badge-danger" %>'>
                                <%# Convert.ToBoolean(Eval("IsActive")) ? "ใช้งาน" : "ไม่ใช้งาน" %>
                            </span>
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="การจัดการ">
                        <ItemTemplate>
                            <asp:Button ID="btnEdit" runat="server"
                                Text="แก้ไข"
                                CssClass="btn-warning btn-sm"
                                CommandName="EditCustomer"
                                CommandArgument='<%# Eval("MobilePhone") %>'
                                CausesValidation="false" />
                            <asp:Button ID="btnViewHistory" runat="server"
                                Text="ประวัติ"
                                CssClass="btn-primary btn-sm"
                                CommandName="ViewHistory"
                                CommandArgument='<%# Eval("MobilePhone") %>'
                                CausesValidation="false" />
                            <asp:Button ID="btnDelete" runat="server"
                                Text="ลบ"
                                CssClass="btn-danger btn-sm"
                                CommandName="DeleteCustomer"
                                CommandArgument='<%# Eval("MobilePhone") %>'
                                OnClientClick="return confirm('คุณแน่ใจหรือไม่ว่าต้องการลบลูกค้านี้?');"
                                CausesValidation="false" />
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
                <PagerStyle HorizontalAlign="Center" CssClass="pager" />
            </asp:GridView>
        </div>
    </div>

    <!-- History Modal (Hidden by default, shown via JavaScript) -->
    <div id="historyModal" class="modal">
        <div class="modal-content">
            <div class="modal-header">
                <span class="close" onclick="closeHistoryModal()">&times;</span>
                <h3>📜 ประวัติการแก้ไขข้อมูล</h3>
            </div>
            <div id="historyContent">
                <asp:Label ID="lblHistoryContent" runat="server"></asp:Label>
            </div>
        </div>
    </div>

    <script type="text/javascript">
        function showHistoryModal() {
            document.getElementById('historyModal').style.display = 'block';
        }

        function closeHistoryModal() {
            document.getElementById('historyModal').style.display = 'none';
        }

        // Close modal when clicking outside
        window.onclick = function (event) {
            var modal = document.getElementById('historyModal');
            if (event.target == modal) {
                modal.style.display = 'none';
            }
        }

        // Auto-hide alerts after 5 seconds
        setTimeout(function () {
            var alerts = document.querySelectorAll('.alert');
            alerts.forEach(function (alert) {
                alert.style.display = 'none';
            });
        }, 5000);
    </script>
</asp:Content>
