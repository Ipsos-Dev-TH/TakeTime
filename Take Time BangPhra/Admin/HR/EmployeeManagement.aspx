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

        .form-section-header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 10px 15px;
            border-radius: 6px;
            margin: 20px 0 15px 0;
        }

        .form-section-header h4 {
            margin: 0;
            font-size: 14px;
            font-weight: 600;
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

        .alert-success {
            background: linear-gradient(135deg, #d4edda 0%, #c3e6cb 100%);
            color: #155724;
            border: 1px solid #b7d7a8;
            box-shadow: 0 2px 8px rgba(40, 167, 69, 0.15);
        }
        .alert-success::before { content: "✓ "; font-weight: bold; }

        .alert-error {
            background: linear-gradient(135deg, #f8d7da 0%, #f5c6cb 100%);
            color: #721c24;
            border: 1px solid #f1b0b7;
            box-shadow: 0 2px 8px rgba(220, 53, 69, 0.15);
        }
        .alert-error::before { content: "⚠ "; font-weight: bold; }

        .alert-info {
            background: linear-gradient(135deg, #d1ecf1 0%, #bee5eb 100%);
            color: #0c5460;
            border: 1px solid #a2d3e0;
            box-shadow: 0 2px 8px rgba(23, 162, 184, 0.15);
        }
        .alert-info::before { content: "ℹ "; font-weight: bold; }

        @keyframes fadeIn {
            from { opacity: 0; transform: translateY(-10px); }
            to { opacity: 1; transform: translateY(0); }
        }

        .alert { animation: fadeIn 0.3s ease-out; }

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

        /* Salary History Modal */
        .salary-history-table {
            width: 100%;
            border-collapse: collapse;
            margin: 15px 0;
        }

        .salary-history-table th {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 10px 8px;
            text-align: left;
            font-weight: 500;
            font-size: 13px;
        }

        .salary-history-table td {
            padding: 10px 8px;
            border-bottom: 1px solid #f0f0f0;
            font-size: 13px;
        }

        .salary-history-table tr:hover {
            background-color: #f8f9fa;
        }

        .salary-history-content {
            max-height: 400px;
            overflow-y: auto;
        }

        /* Signature Section Styles */
        .signature-container {
            border: 2px dashed #e0e0e0;
            border-radius: 8px;
            padding: 15px;
            background: #fafafa;
        }

        .signature-preview {
            min-height: 80px;
            display: flex;
            align-items: center;
            justify-content: center;
            margin-bottom: 10px;
            background: white;
            border-radius: 4px;
            padding: 10px;
        }

        .signature-image {
            max-width: 200px;
            max-height: 80px;
            border: 1px solid #e0e0e0;
            border-radius: 4px;
        }

        .no-signature {
            color: #999;
            font-style: italic;
        }

        .signature-upload {
            margin-top: 10px;
        }

        .text-muted {
            color: #6c757d;
            font-size: 12px;
        }

        .btn-sm {
            padding: 5px 10px;
            font-size: 12px;
        }

        .btn-info {
            background: linear-gradient(135deg, #17a2b8 0%, #138496 100%);
            color: white;
            border: none;
            border-radius: 5px;
        }

        .btn-info:hover {
            background: linear-gradient(135deg, #138496 0%, #117a8b 100%);
        }

        /* Document Section Styles */
        .document-list {
            max-height: 300px;
            overflow-y: auto;
            border: 1px solid #e0e0e0;
            border-radius: 6px;
            margin-bottom: 15px;
        }

        .document-item {
            display: flex;
            align-items: center;
            justify-content: space-between;
            padding: 10px 15px;
            border-bottom: 1px solid #f0f0f0;
            transition: background 0.2s;
        }

        .document-item:last-child {
            border-bottom: none;
        }

        .document-item:hover {
            background-color: #f8f9fa;
        }

        .document-info {
            flex: 1;
            min-width: 0;
        }

        .document-name {
            font-weight: 500;
            color: #333;
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
        }

        .document-meta {
            font-size: 11px;
            color: #888;
            margin-top: 2px;
        }

        .document-type-badge {
            display: inline-block;
            padding: 2px 8px;
            border-radius: 3px;
            font-size: 10px;
            font-weight: 500;
            margin-right: 8px;
        }

        .doc-type-id_card { background: #e3f2fd; color: #1565c0; }
        .doc-type-house_reg { background: #f3e5f5; color: #7b1fa2; }
        .doc-type-bank_book { background: #e8f5e9; color: #2e7d32; }
        .doc-type-contract { background: #fff3e0; color: #ef6c00; }
        .doc-type-resume { background: #e0f7fa; color: #00838f; }
        .doc-type-certificate { background: #fce4ec; color: #c2185b; }
        .doc-type-medical { background: #ffebee; color: #c62828; }
        .doc-type-other { background: #f5f5f5; color: #616161; }

        .document-actions {
            display: flex;
            gap: 5px;
        }

        .btn-view-doc {
            background: #17a2b8;
            color: white;
            border: none;
            padding: 4px 8px;
            border-radius: 4px;
            font-size: 11px;
            cursor: pointer;
        }

        .btn-delete-doc {
            background: #dc3545;
            color: white;
            border: none;
            padding: 4px 8px;
            border-radius: 4px;
            font-size: 11px;
            cursor: pointer;
        }

        .upload-section {
            background: #f8f9fa;
            border: 2px dashed #dee2e6;
            border-radius: 8px;
            padding: 15px;
        }

        .no-documents {
            text-align: center;
            padding: 20px;
            color: #999;
            font-style: italic;
        }

        .btn-docs {
            background: linear-gradient(135deg, #00b09b 0%, #96c93d 100%);
            color: white;
            padding: 6px 12px;
            font-size: 12px;
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

                                <asp:Button ID="btnSalaryHistory" runat="server" Text="&#128176; ประวัติเงินเดือน"
                                    CssClass="btn-info" CommandName="ViewSalaryHistory"
                                    CommandArgument='<%# Eval("Admin_ID") %>' />

                                <asp:Button ID="btnDocuments" runat="server" Text="&#128194; เอกสาร"
                                    CssClass="btn-docs" CommandName="ViewDocuments"
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

        <!-- Salary History Modal -->
        <div id="salaryHistoryModal" class="modal-overlay">
            <div class="modal-content">
                <div class="modal-header">
                    <h3 id="salaryHistoryTitle">&#128176; ประวัติเงินเดือน</h3>
                    <button type="button" class="modal-close" onclick="closeSalaryHistoryModal()">&times;</button>
                </div>
                <div class="salary-history-content">
                    <table class="salary-history-table">
                        <thead>
                            <tr>
                                <th>วันที่มีผล</th>
                                <th>ตำแหน่ง</th>
                                <th style="text-align:right">เงินเดือน</th>
                                <th>สถานะ</th>
                            </tr>
                        </thead>
                        <tbody id="salaryHistoryBody">
                        </tbody>
                    </table>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-cancel" onclick="closeSalaryHistoryModal()">ปิด</button>
                </div>
            </div>
        </div>

        <!-- Documents Modal -->
        <div id="documentsModal" class="modal-overlay">
            <div class="modal-content" style="max-width: 700px;">
                <div class="modal-header">
                    <h3 id="documentsTitle">&#128194; เอกสารพนักงาน</h3>
                    <button type="button" class="modal-close" onclick="closeDocumentsModal()">&times;</button>
                </div>

                <asp:HiddenField ID="hdnDocEmployeeId" runat="server" />

                <!-- Existing Documents List -->
                <div style="margin-bottom: 20px;">
                    <h4 style="margin: 0 0 10px 0; font-size: 14px; color: #555;">&#128196; รายการเอกสาร</h4>
                    <asp:Panel ID="pnlDocumentList" runat="server" CssClass="document-list">
                        <asp:Repeater ID="rptDocuments" runat="server" OnItemCommand="rptDocuments_ItemCommand">
                            <ItemTemplate>
                                <div class="document-item">
                                    <div class="document-info">
                                        <span class='document-type-badge doc-type-<%# Eval("DocumentType").ToString().ToLower() %>'>
                                            <%# GetDocumentTypeText(Eval("DocumentType").ToString()) %>
                                        </span>
                                        <span class="document-name"><%# Eval("DocumentName") %></span>
                                        <div class="document-meta">
                                            อัพโหลด: <%# Eval("UploadedDate", "{0:dd/MM/yyyy HH:mm}") %>
                                            <%# Eval("ExpiryDate") != DBNull.Value ? " | หมดอายุ: " + Convert.ToDateTime(Eval("ExpiryDate")).ToString("dd/MM/yyyy") : "" %>
                                        </div>
                                    </div>
                                    <div class="document-actions">
                                        <asp:LinkButton ID="btnViewDoc" runat="server" CssClass="btn-view-doc"
                                            CommandName="ViewDocument" CommandArgument='<%# Eval("ID") + "|" + Eval("FilePath") %>'>
                                            &#128065; ดู
                                        </asp:LinkButton>
                                        <asp:LinkButton ID="btnDeleteDoc" runat="server" CssClass="btn-delete-doc"
                                            CommandName="DeleteDocument" CommandArgument='<%# Eval("ID") %>'
                                            OnClientClick="return confirm('ยืนยันการลบเอกสารนี้?');">
                                            &#128465; ลบ
                                        </asp:LinkButton>
                                    </div>
                                </div>
                            </ItemTemplate>
                        </asp:Repeater>
                        <asp:Panel ID="pnlNoDocuments" runat="server" CssClass="no-documents" Visible="false">
                            &#128230; ยังไม่มีเอกสาร
                        </asp:Panel>
                    </asp:Panel>
                </div>

                <!-- Upload New Document -->
                <div class="upload-section">
                    <h4 style="margin: 0 0 15px 0; font-size: 14px; color: #555;">&#128228; อัพโหลดเอกสารใหม่</h4>

                    <div class="form-row">
                        <div class="form-group">
                            <label>ประเภทเอกสาร <span class="required">*</span></label>
                            <asp:DropDownList ID="ddlDocumentType" runat="server" CssClass="form-control">
                                <asp:ListItem Value="ID_CARD">สำเนาบัตรประชาชน</asp:ListItem>
                                <asp:ListItem Value="HOUSE_REG">สำเนาทะเบียนบ้าน</asp:ListItem>
                                <asp:ListItem Value="BANK_BOOK">สำเนาหน้าสมุดบัญชี</asp:ListItem>
                                <asp:ListItem Value="CONTRACT">สัญญาจ้างงาน</asp:ListItem>
                                <asp:ListItem Value="RESUME">ประวัติย่อ/Resume</asp:ListItem>
                                <asp:ListItem Value="CERTIFICATE">ใบรับรอง/Certificate</asp:ListItem>
                                <asp:ListItem Value="MEDICAL">ใบรับรองแพทย์</asp:ListItem>
                                <asp:ListItem Value="OTHER">เอกสารอื่นๆ</asp:ListItem>
                            </asp:DropDownList>
                        </div>
                        <div class="form-group">
                            <label>ชื่อเอกสาร</label>
                            <asp:TextBox ID="txtDocumentName" runat="server" CssClass="form-control" placeholder="ระบุชื่อเอกสาร (ถ้าว่างจะใช้ชื่อไฟล์)"></asp:TextBox>
                        </div>
                    </div>

                    <div class="form-row">
                        <div class="form-group">
                            <label>วันหมดอายุ (ถ้ามี)</label>
                            <asp:TextBox ID="txtDocExpiryDate" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                        </div>
                        <div class="form-group">
                            <label>หมายเหตุ</label>
                            <asp:TextBox ID="txtDocDescription" runat="server" CssClass="form-control" placeholder="หมายเหตุเพิ่มเติม"></asp:TextBox>
                        </div>
                    </div>

                    <div class="form-group">
                        <label>เลือกไฟล์ <span class="required">*</span></label>
                        <asp:FileUpload ID="fuDocument" runat="server" CssClass="form-control" />
                        <small class="text-muted">รองรับไฟล์ PDF, รูปภาพ (JPG, PNG), Word, Excel ขนาดไม่เกิน 10MB</small>
                    </div>

                    <div style="text-align: right; margin-top: 15px;">
                        <asp:Button ID="btnUploadDocument" runat="server" Text="&#128228; อัพโหลดเอกสาร"
                            CssClass="btn btn-success" OnClick="btnUploadDocument_Click" />
                    </div>
                </div>

                <div class="modal-footer">
                    <button type="button" class="btn btn-cancel" onclick="closeDocumentsModal()">ปิด</button>
                </div>
            </div>
        </div>

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

                <!-- Personal Info Section -->
                <div class="form-section-header">
                    <h4>&#128100; ข้อมูลส่วนตัว</h4>
                </div>

                <div class="form-row">
                    <div class="form-group">
                        <label>เลขบัตรประชาชน</label>
                        <asp:TextBox ID="txtIDCard" runat="server" CssClass="form-control" placeholder="เลขบัตรประชาชน 13 หลัก" MaxLength="13"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label>วันเกิด</label>
                        <asp:TextBox ID="txtBirthDate" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                    </div>
                </div>

                <div class="form-row">
                    <div class="form-group">
                        <label>เบอร์โทรศัพท์</label>
                        <asp:TextBox ID="txtPhone" runat="server" CssClass="form-control" placeholder="0xx-xxx-xxxx" MaxLength="15"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label>อีเมล</label>
                        <asp:TextBox ID="txtEmail" runat="server" CssClass="form-control" placeholder="email@example.com" TextMode="Email"></asp:TextBox>
                    </div>
                </div>

                <div class="form-group">
                    <label>ที่อยู่</label>
                    <asp:TextBox ID="txtAddress" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="2" placeholder="ที่อยู่ปัจจุบัน"></asp:TextBox>
                </div>

                <div class="form-row">
                    <div class="form-group">
                        <label>วันที่เริ่มงาน</label>
                        <asp:TextBox ID="txtHireDate" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                    </div>
                </div>

                <!-- Bank Info Section -->
                <div class="form-section-header">
                    <h4>&#127974; ข้อมูลธนาคาร</h4>
                </div>

                <div class="form-row">
                    <div class="form-group">
                        <label>ธนาคาร</label>
                        <asp:DropDownList ID="ddlBank" runat="server" CssClass="form-control">
                            <asp:ListItem Value="">-- เลือกธนาคาร --</asp:ListItem>
                            <asp:ListItem Value="002">ธนาคารกรุงเทพ</asp:ListItem>
                            <asp:ListItem Value="004">ธนาคารกสิกรไทย</asp:ListItem>
                            <asp:ListItem Value="006">ธนาคารกรุงไทย</asp:ListItem>
                            <asp:ListItem Value="011">ธนาคารทหารไทยธนชาต</asp:ListItem>
                            <asp:ListItem Value="014">ธนาคารไทยพาณิชย์</asp:ListItem>
                            <asp:ListItem Value="025">ธนาคารกรุงศรีอยุธยา</asp:ListItem>
                            <asp:ListItem Value="030">ธนาคารออมสิน</asp:ListItem>
                            <asp:ListItem Value="034">ธนาคารเพื่อการเกษตรและสหกรณ์การเกษตร</asp:ListItem>
                            <asp:ListItem Value="069">ธนาคารเกียรตินาคินภัทร</asp:ListItem>
                        </asp:DropDownList>
                    </div>
                    <div class="form-group">
                        <label>เลขบัญชี</label>
                        <asp:TextBox ID="txtBankAccountNumber" runat="server" CssClass="form-control" placeholder="เลขบัญชีธนาคาร" MaxLength="20"></asp:TextBox>
                    </div>
                </div>

                <div class="form-group">
                    <label>ชื่อบัญชี</label>
                    <asp:TextBox ID="txtBankAccountName" runat="server" CssClass="form-control" placeholder="ชื่อบัญชีตามหน้าสมุดบัญชี"></asp:TextBox>
                </div>

                <!-- Emergency Contact Section -->
                <div class="form-section-header">
                    <h4>&#128222; ผู้ติดต่อกรณีฉุกเฉิน</h4>
                </div>

                <div class="form-row">
                    <div class="form-group">
                        <label>ชื่อผู้ติดต่อ</label>
                        <asp:TextBox ID="txtEmergencyContact" runat="server" CssClass="form-control" placeholder="ชื่อ-นามสกุล (ความสัมพันธ์)"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label>เบอร์โทรฉุกเฉิน</label>
                        <asp:TextBox ID="txtEmergencyPhone" runat="server" CssClass="form-control" placeholder="0xx-xxx-xxxx" MaxLength="15"></asp:TextBox>
                    </div>
                </div>

                <!-- Signature Section -->
                <div class="form-section-header">
                    <h4>&#9999; ลายเซ็น</h4>
                </div>

                <div class="form-group" id="signatureSection">
                    <div class="signature-container">
                        <div class="signature-preview" id="signaturePreview">
                            <asp:Image ID="imgSignature" runat="server" CssClass="signature-image" Visible="false" />
                            <span class="no-signature" id="noSignatureText">ยังไม่มีลายเซ็น</span>
                        </div>
                        <div class="signature-upload">
                            <asp:FileUpload ID="fuSignature" runat="server" CssClass="form-control" accept="image/*" />
                            <small class="text-muted">รองรับไฟล์ PNG, JPG ขนาดไม่เกิน 2MB</small>
                        </div>
                        <asp:Button ID="btnUploadSignature" runat="server" Text="อัพโหลดลายเซ็น"
                            CssClass="btn btn-info btn-sm" OnClick="btnUploadSignature_Click"
                            style="margin-top: 5px;" />
                        <asp:Button ID="btnDeleteSignature" runat="server" Text="ลบลายเซ็น"
                            CssClass="btn btn-danger btn-sm" OnClick="btnDeleteSignature_Click"
                            style="margin-top: 5px; margin-left: 5px;" Visible="false" />
                    </div>
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

            // Clear form - Basic info
            document.getElementById('<%= txtUsername.ClientID %>').value = '';
            document.getElementById('<%= txtPassword.ClientID %>').value = '';
            document.getElementById('<%= txtFirstName.ClientID %>').value = '';
            document.getElementById('<%= txtLastName.ClientID %>').value = '';
            document.getElementById('<%= txtSalary.ClientID %>').value = '';
            document.getElementById('<%= txtPosition.ClientID %>').value = '';
            document.getElementById('<%= ddlRole.ClientID %>').selectedIndex = 0;

            // Clear form - Personal info
            document.getElementById('<%= txtIDCard.ClientID %>').value = '';
            document.getElementById('<%= txtBirthDate.ClientID %>').value = '';
            document.getElementById('<%= txtPhone.ClientID %>').value = '';
            document.getElementById('<%= txtEmail.ClientID %>').value = '';
            document.getElementById('<%= txtAddress.ClientID %>').value = '';
            document.getElementById('<%= txtHireDate.ClientID %>').value = '';

            // Clear form - Bank info
            document.getElementById('<%= ddlBank.ClientID %>').selectedIndex = 0;
            document.getElementById('<%= txtBankAccountNumber.ClientID %>').value = '';
            document.getElementById('<%= txtBankAccountName.ClientID %>').value = '';

            // Clear form - Emergency contact
            document.getElementById('<%= txtEmergencyContact.ClientID %>').value = '';
            document.getElementById('<%= txtEmergencyPhone.ClientID %>').value = '';

            // Enable username field
            document.getElementById('<%= txtUsername.ClientID %>').disabled = false;

            document.getElementById('employeeModal').style.display = 'flex';
        }

        function openEditModal(adminId, username, firstName, lastName, role, salary, position,
                              idCard, birthDate, phone, email, address, hireDate,
                              bankCode, bankAccountNumber, bankAccountName,
                              emergencyContact, emergencyPhone) {
            document.getElementById('<%= hdnEditMode.ClientID %>').value = 'edit';
            document.getElementById('<%= hdnEmployeeId.ClientID %>').value = adminId;
            document.getElementById('modalTitle').innerHTML = '&#9998; แก้ไขข้อมูลพนักงาน';
            document.getElementById('passwordRequired').style.display = 'none';

            // Fill form - Basic info
            document.getElementById('<%= txtUsername.ClientID %>').value = username;
            document.getElementById('<%= txtPassword.ClientID %>').value = '';
            document.getElementById('<%= txtFirstName.ClientID %>').value = firstName;
            document.getElementById('<%= txtLastName.ClientID %>').value = lastName;
            document.getElementById('<%= txtSalary.ClientID %>').value = salary || '';
            document.getElementById('<%= txtPosition.ClientID %>').value = position || '';

            // Fill form - Personal info
            document.getElementById('<%= txtIDCard.ClientID %>').value = idCard || '';
            document.getElementById('<%= txtBirthDate.ClientID %>').value = birthDate || '';
            document.getElementById('<%= txtPhone.ClientID %>').value = phone || '';
            document.getElementById('<%= txtEmail.ClientID %>').value = email || '';
            document.getElementById('<%= txtAddress.ClientID %>').value = address || '';
            document.getElementById('<%= txtHireDate.ClientID %>').value = hireDate || '';

            // Fill form - Bank info
            var bankSelect = document.getElementById('<%= ddlBank.ClientID %>');
            bankSelect.selectedIndex = 0;
            for (var i = 0; i < bankSelect.options.length; i++) {
                if (bankSelect.options[i].value === bankCode) {
                    bankSelect.selectedIndex = i;
                    break;
                }
            }
            document.getElementById('<%= txtBankAccountNumber.ClientID %>').value = bankAccountNumber || '';
            document.getElementById('<%= txtBankAccountName.ClientID %>').value = bankAccountName || '';

            // Fill form - Emergency contact
            document.getElementById('<%= txtEmergencyContact.ClientID %>').value = emergencyContact || '';
            document.getElementById('<%= txtEmergencyPhone.ClientID %>').value = emergencyPhone || '';

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

        function openSalaryHistoryModal(employeeName, tableRows) {
            document.getElementById('salaryHistoryTitle').innerHTML = '&#128176; ประวัติเงินเดือน - ' + employeeName;
            document.getElementById('salaryHistoryBody').innerHTML = tableRows;
            document.getElementById('salaryHistoryModal').style.display = 'flex';
        }

        function closeSalaryHistoryModal() {
            document.getElementById('salaryHistoryModal').style.display = 'none';
        }

        function openDocumentsModal(employeeName) {
            document.getElementById('documentsTitle').innerHTML = '&#128194; เอกสาร - ' + employeeName;
            document.getElementById('documentsModal').style.display = 'flex';
        }

        function closeDocumentsModal() {
            document.getElementById('documentsModal').style.display = 'none';
        }

        // Close modal when clicking outside
        window.onclick = function (event) {
            if (event.target.className === 'modal-overlay') {
                event.target.style.display = 'none';
            }
        }

        // Auto-dismiss alert messages after 5 seconds
        document.addEventListener('DOMContentLoaded', function() {
            var alerts = document.querySelectorAll('.alert');
            alerts.forEach(function(alert) {
                setTimeout(function() {
                    alert.style.transition = 'opacity 0.5s ease-out';
                    alert.style.opacity = '0';
                    setTimeout(function() { alert.style.display = 'none'; }, 500);
                }, 5000);
            });
        });
    </script>
</asp:Content>
