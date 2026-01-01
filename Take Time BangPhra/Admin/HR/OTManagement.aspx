<%@ Page Title="จัดการ OT ทั้งหมด" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="OTManagement.aspx.cs" Inherits="Take_Time_BangPhra.Admin.HR.OTManagement" MaintainScrollPositionOnPostback="true" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .ot-management {
            padding: 20px;
            max-width: 1400px;
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

        .filter-section {
            background: white;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            margin-bottom: 20px;
        }

        .filter-row {
            display: flex;
            gap: 15px;
            flex-wrap: wrap;
            align-items: flex-end;
        }

        .filter-group {
            display: flex;
            flex-direction: column;
            gap: 5px;
        }

        .filter-group label {
            font-size: 13px;
            color: #666;
            font-weight: 500;
        }

        .form-control {
            padding: 8px 12px;
            border: 2px solid #e0e0e0;
            border-radius: 6px;
            font-size: 14px;
            min-height: 38px;
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
            font-size: 14px;
            transition: all 0.3s;
        }

        .btn-primary {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
        }

        .btn-success {
            background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%);
            color: white;
        }

        .btn-warning {
            background: #ffc107;
            color: #333;
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
            padding: 5px 10px;
            font-size: 12px;
        }

        .btn:hover {
            transform: translateY(-1px);
            box-shadow: 0 4px 12px rgba(0,0,0,0.2);
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
            font-size: 13px;
        }

        .data-table td {
            padding: 12px;
            border-bottom: 1px solid #f0f0f0;
            font-size: 13px;
        }

        .data-table tr:hover {
            background-color: #f8f9fa;
        }

        .badge {
            display: inline-block;
            padding: 4px 10px;
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

        .stats-row {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
            gap: 15px;
            margin-bottom: 20px;
        }

        .stat-card {
            background: white;
            padding: 15px;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            text-align: center;
        }

        .stat-card .value {
            font-size: 24px;
            font-weight: 700;
            color: #667eea;
        }

        .stat-card .label {
            font-size: 12px;
            color: #666;
            margin-top: 5px;
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
            padding: 25px;
            border-radius: 10px;
            width: 90%;
            max-width: 500px;
            max-height: 90vh;
            overflow-y: auto;
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
            color: #333;
        }

        .form-group .form-control {
            width: 100%;
            box-sizing: border-box;
        }

        textarea.form-control {
            min-height: 80px;
            resize: vertical;
        }

        .action-buttons {
            display: flex;
            gap: 8px;
        }

        @media (max-width: 768px) {
            .filter-row {
                flex-direction: column;
            }
            .filter-group {
                width: 100%;
            }
            .data-table {
                overflow-x: auto;
            }
        }
    </style>

    <div class="ot-management">
        <div class="section-header">
            <h2>&#128202; จัดการ OT ทั้งหมด</h2>
        </div>

        <!-- Message Panel -->
        <asp:Panel ID="pnlMessage" runat="server" Visible="false" CssClass="alert">
            <asp:Label ID="lblMessage" runat="server"></asp:Label>
        </asp:Panel>

        <!-- Statistics -->
        <div class="stats-row">
            <div class="stat-card">
                <div class="value"><asp:Label ID="lblTotalEntries" runat="server" Text="0"></asp:Label></div>
                <div class="label">รายการทั้งหมด</div>
            </div>
            <div class="stat-card">
                <div class="value" style="color: #ffc107;"><asp:Label ID="lblPendingCount" runat="server" Text="0"></asp:Label></div>
                <div class="label">รออนุมัติ</div>
            </div>
            <div class="stat-card">
                <div class="value" style="color: #28a745;"><asp:Label ID="lblApprovedCount" runat="server" Text="0"></asp:Label></div>
                <div class="label">อนุมัติแล้ว</div>
            </div>
            <div class="stat-card">
                <div class="value" style="color: #667eea;"><asp:Label ID="lblTotalHours" runat="server" Text="0"></asp:Label></div>
                <div class="label">ชั่วโมง OT รวม</div>
            </div>
        </div>

        <!-- Filters -->
        <div class="filter-section">
            <div class="filter-row">
                <div class="filter-group">
                    <label>สถานะ</label>
                    <asp:DropDownList ID="ddlStatus" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlFilter_SelectedIndexChanged">
                        <asp:ListItem Value="">ทั้งหมด</asp:ListItem>
                        <asp:ListItem Value="PENDING">รออนุมัติ</asp:ListItem>
                        <asp:ListItem Value="APPROVED">อนุมัติแล้ว</asp:ListItem>
                        <asp:ListItem Value="REJECTED">ปฏิเสธ</asp:ListItem>
                    </asp:DropDownList>
                </div>
                <div class="filter-group">
                    <label>เดือน</label>
                    <asp:DropDownList ID="ddlMonth" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlFilter_SelectedIndexChanged">
                    </asp:DropDownList>
                </div>
                <div class="filter-group">
                    <label>ปี</label>
                    <asp:DropDownList ID="ddlYear" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlFilter_SelectedIndexChanged">
                    </asp:DropDownList>
                </div>
                <div class="filter-group">
                    <label>พนักงาน</label>
                    <asp:DropDownList ID="ddlEmployee" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlFilter_SelectedIndexChanged">
                        <asp:ListItem Value="">ทั้งหมด</asp:ListItem>
                    </asp:DropDownList>
                </div>
            </div>
        </div>

        <!-- Hidden fields for edit modal -->
        <asp:HiddenField ID="hdnEditId" runat="server" />

        <!-- Data Table -->
        <div class="data-table">
            <asp:GridView ID="gvOTEntries" runat="server" AutoGenerateColumns="False"
                CssClass="table" GridLines="None" OnRowCommand="gvOTEntries_RowCommand">
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
                            <%# Eval("OTHours") %> ชม. x <%# Eval("OTRate") %><br />
                            <strong style="color: #667eea;">= <%# string.Format("{0:N1}", Eval("OTMultipliedHours")) %> ชม.</strong>
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:BoundField DataField="WorkDescription" HeaderText="รายละเอียดงาน" />
                    <asp:TemplateField HeaderText="สถานะ">
                        <ItemTemplate>
                            <%# GetStatusBadge(Eval("Status").ToString()) %>
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="บันทึกโดย">
                        <ItemTemplate>
                            <%# Eval("EnteredByName") %><br />
                            <small style="color: #999;"><%# Convert.ToDateTime(Eval("EntryDate")).ToString("dd/MM/yy HH:mm") %></small>
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="อนุมัติโดย">
                        <ItemTemplate>
                            <%# Eval("ApprovedByName") != DBNull.Value ? Eval("ApprovedByName") : "-" %><br />
                            <small style="color: #999;"><%# Eval("ApprovedDate") != DBNull.Value ? Convert.ToDateTime(Eval("ApprovedDate")).ToString("dd/MM/yy HH:mm") : "" %></small>
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="จัดการ">
                        <ItemTemplate>
                            <div class="action-buttons">
                                <asp:Button ID="btnEdit" runat="server" Text="&#9998; แก้ไข"
                                    CssClass="btn btn-warning btn-sm" CommandName="EditOT"
                                    CommandArgument='<%# Eval("ID") %>' />
                                <asp:Button ID="btnDelete" runat="server" Text="&#128465; ลบ"
                                    CssClass="btn btn-danger btn-sm" CommandName="DeleteOT"
                                    CommandArgument='<%# Eval("ID") %>'
                                    OnClientClick="return confirm('ยืนยันการลบรายการนี้?');" />
                            </div>
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

    <!-- Edit Modal -->
    <div id="editModal" class="modal-overlay">
        <div class="modal-content">
            <div class="modal-header">
                <h3>&#9998; แก้ไขรายการ OT</h3>
                <button type="button" class="modal-close" onclick="closeEditModal()">&times;</button>
            </div>
            <asp:Panel ID="pnlEditForm" runat="server">
                <div class="form-group">
                    <label>พนักงาน</label>
                    <asp:Label ID="lblEditEmployee" runat="server" CssClass="form-control" style="background: #f5f5f5;"></asp:Label>
                </div>
                <div class="form-group">
                    <label>วันที่ทำ OT</label>
                    <asp:TextBox ID="txtEditDate" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                </div>
                <div class="form-group">
                    <label>จำนวนชั่วโมง</label>
                    <asp:TextBox ID="txtEditHours" runat="server" CssClass="form-control" TextMode="Number" step="0.5"></asp:TextBox>
                </div>
                <div class="form-group">
                    <label>อัตรา OT (เท่า)</label>
                    <asp:DropDownList ID="ddlEditRate" runat="server" CssClass="form-control">
                        <asp:ListItem Value="1.5">1.5 เท่า (ปกติ)</asp:ListItem>
                        <asp:ListItem Value="2">2 เท่า (วันหยุด)</asp:ListItem>
                        <asp:ListItem Value="3">3 เท่า (พิเศษ)</asp:ListItem>
                    </asp:DropDownList>
                </div>
                <div class="form-group">
                    <label>รายละเอียดงาน</label>
                    <asp:TextBox ID="txtEditDescription" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="3"></asp:TextBox>
                </div>
                <div class="form-group">
                    <label>สถานะ</label>
                    <asp:DropDownList ID="ddlEditStatus" runat="server" CssClass="form-control">
                        <asp:ListItem Value="PENDING">รออนุมัติ</asp:ListItem>
                        <asp:ListItem Value="APPROVED">อนุมัติแล้ว</asp:ListItem>
                        <asp:ListItem Value="REJECTED">ปฏิเสธ</asp:ListItem>
                    </asp:DropDownList>
                </div>
                <div class="form-group">
                    <label>หมายเหตุ</label>
                    <asp:TextBox ID="txtEditNotes" runat="server" CssClass="form-control"></asp:TextBox>
                </div>
                <div style="display: flex; gap: 10px; margin-top: 20px;">
                    <asp:Button ID="btnSaveEdit" runat="server" Text="&#128190; บันทึก" CssClass="btn btn-success" OnClick="btnSaveEdit_Click" />
                    <button type="button" class="btn btn-secondary" onclick="closeEditModal()">ยกเลิก</button>
                </div>
            </asp:Panel>
        </div>
    </div>

    <script type="text/javascript">
        function openEditModal() {
            document.getElementById('editModal').style.display = 'flex';
        }

        function closeEditModal() {
            document.getElementById('editModal').style.display = 'none';
        }

        // Close modal when clicking outside
        document.getElementById('editModal').addEventListener('click', function(e) {
            if (e.target === this) {
                closeEditModal();
            }
        });
    </script>
</asp:Content>
