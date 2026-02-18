<%@ Page Title="จัดการข้อมูลฉุกเฉิน" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeFile="ManageEmergency.aspx.cs" Inherits="Take_Time_BangPhra.Admin.ManageEmergency" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .emergency-management {
            padding: 20px;
            max-width: 1200px;
            margin: 0 auto;
        }

        .section-header {
            background: linear-gradient(135deg, #e74c3c 0%, #c0392b 100%);
            color: white;
            padding: 15px 20px;
            border-radius: 8px;
            margin-bottom: 20px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
            display: flex;
            justify-content: space-between;
            align-items: center;
            flex-wrap: wrap;
            gap: 10px;
        }

        .section-header h2 {
            margin: 0;
            font-size: 22px;
            font-weight: 600;
        }

        .header-actions {
            display: flex;
            gap: 10px;
            flex-wrap: wrap;
        }

        .filter-section {
            background: white;
            padding: 15px 20px;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            margin-bottom: 20px;
            display: flex;
            align-items: center;
            gap: 15px;
            flex-wrap: wrap;
        }

        .filter-section label {
            font-weight: 500;
            color: #555;
            font-size: 14px;
        }

        .form-control {
            padding: 10px 15px;
            border: 2px solid #e0e0e0;
            border-radius: 6px;
            font-size: 14px;
            min-height: 42px;
            transition: border-color 0.3s;
        }

        .form-control:focus {
            border-color: #e74c3c;
            outline: none;
        }

        /* Buttons */
        .btn {
            padding: 10px 20px;
            border: none;
            border-radius: 6px;
            font-weight: 500;
            cursor: pointer;
            font-size: 14px;
            transition: all 0.3s;
            text-decoration: none;
            display: inline-block;
        }

        .btn:hover {
            transform: translateY(-1px);
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

        .btn-warning {
            background: linear-gradient(135deg, #f7971e 0%, #ffd200 100%);
            color: #333;
        }

        .btn-danger {
            background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
            color: white;
        }

        .btn-secondary {
            background: #95a5a6;
            color: white;
        }

        .btn-sm {
            padding: 6px 12px;
            font-size: 12px;
        }

        /* Data Table */
        .data-table {
            background: white;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            overflow-x: auto;
            -webkit-overflow-scrolling: touch;
        }

        .data-table table {
            width: 100%;
            border-collapse: collapse;
            min-width: 700px;
        }

        .data-table th {
            background: linear-gradient(135deg, #e74c3c 0%, #c0392b 100%);
            color: white;
            padding: 12px;
            text-align: left;
            font-weight: 500;
            font-size: 13px;
            white-space: nowrap;
        }

        .data-table td {
            padding: 12px;
            border-bottom: 1px solid #f0f0f0;
            font-size: 13px;
            vertical-align: middle;
        }

        .data-table tr:hover {
            background-color: #fef5f5;
        }

        /* Category Badge */
        .category-badge {
            display: inline-block;
            padding: 4px 10px;
            border-radius: 4px;
            font-size: 12px;
            font-weight: 500;
            white-space: nowrap;
        }

        .cat-quickdial { background: #fff3cd; color: #856404; }
        .cat-hospital { background: #d4edda; color: #155724; }
        .cat-police { background: #cce5ff; color: #004085; }
        .cat-hotel { background: #e2d5f1; color: #4a235a; }
        .cat-transport { background: #d1ecf1; color: #0c5460; }

        /* Quick Dial Indicator */
        .quick-dial-yes {
            color: #27ae60;
            font-weight: bold;
        }

        .quick-dial-no {
            color: #bdc3c7;
        }

        /* Form Card */
        .form-card {
            background: white;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            padding: 25px;
        }

        .form-card h3 {
            margin: 0 0 20px 0;
            font-size: 18px;
            color: #2c3e50;
            padding-bottom: 10px;
            border-bottom: 2px solid #e74c3c;
        }

        .form-group {
            margin-bottom: 18px;
        }

        .form-group label {
            display: block;
            font-weight: 600;
            margin-bottom: 6px;
            color: #2c3e50;
            font-size: 14px;
        }

        .form-group .form-control {
            width: 100%;
            box-sizing: border-box;
        }

        .form-group small {
            display: block;
            margin-top: 4px;
            color: #999;
            font-size: 12px;
        }

        .form-row {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 20px;
        }

        .form-actions {
            display: flex;
            gap: 10px;
            margin-top: 20px;
            padding-top: 15px;
            border-top: 1px solid #eee;
        }

        /* Alert */
        .alert {
            padding: 15px 20px;
            border-radius: 8px;
            margin-bottom: 20px;
            font-size: 14px;
        }

        .alert-success {
            background: #d4edda;
            color: #155724;
            border: 1px solid #c3e6cb;
        }

        .alert-danger {
            background: #f8d7da;
            color: #721c24;
            border: 1px solid #f5c6cb;
        }

        /* Action buttons in grid */
        .action-btns {
            display: flex;
            gap: 5px;
        }

        /* Mobile responsive */
        @media (max-width: 768px) {
            .emergency-management {
                padding: 10px;
            }
            .section-header {
                flex-direction: column;
                text-align: center;
            }
            .filter-section {
                flex-direction: column;
                align-items: stretch;
            }
            .form-row {
                grid-template-columns: 1fr;
            }
            .data-table {
                margin: 10px -10px;
                border-radius: 0;
            }
        }
    </style>

    <div class="emergency-management">

        <!-- Message -->
        <asp:Label ID="lblMessage" runat="server" Visible="false"></asp:Label>

        <!-- ==================== LIST PANEL ==================== -->
        <asp:Panel ID="pnlList" runat="server" Visible="true">

            <div class="section-header">
                <h2><i class="fa fa-phone-alt"></i> จัดการข้อมูลฉุกเฉิน</h2>
                <div class="header-actions">
                    <asp:Button ID="btnAdd" runat="server" Text="+ เพิ่มใหม่" CssClass="btn btn-success" OnClick="btnAdd_Click" />
                    <asp:Button ID="btnSeed" runat="server" Text="Seed ข้อมูลตัวอย่าง" CssClass="btn btn-warning"
                        OnClick="btnSeed_Click" OnClientClick="return confirm('ต้องการเพิ่มข้อมูลตัวอย่างเบอร์ฉุกเฉิน?');" />
                </div>
            </div>

            <!-- Category Filter -->
            <div class="filter-section">
                <label><i class="fa fa-filter"></i> กรองตามหมวดหมู่:</label>
                <asp:DropDownList ID="ddlFilterCategory" runat="server" CssClass="form-control" AutoPostBack="true"
                    OnSelectedIndexChanged="ddlFilterCategory_SelectedIndexChanged">
                    <asp:ListItem Value="" Text="-- ทั้งหมด --"></asp:ListItem>
                    <asp:ListItem Value="quickdial" Text="โทรด่วน"></asp:ListItem>
                    <asp:ListItem Value="hospital" Text="โรงพยาบาล"></asp:ListItem>
                    <asp:ListItem Value="police" Text="ตำรวจ/ฉุกเฉิน"></asp:ListItem>
                    <asp:ListItem Value="hotel" Text="บริการโรงแรม"></asp:ListItem>
                    <asp:ListItem Value="transport" Text="การเดินทาง"></asp:ListItem>
                </asp:DropDownList>
            </div>

            <!-- Grid -->
            <div class="data-table">
                <asp:GridView ID="gvList" runat="server" AutoGenerateColumns="False" CssClass="table"
                    GridLines="None" OnRowCommand="gvList_RowCommand">
                    <Columns>
                        <asp:BoundField DataField="ID" HeaderText="ID" HeaderStyle-Width="50px" />
                        <asp:TemplateField HeaderText="หมวดหมู่">
                            <ItemTemplate>
                                <span class='category-badge cat-<%# Eval("Category") %>'>
                                    <%# GetCategoryText(Eval("Category")) %>
                                </span>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="ชื่อ">
                            <ItemTemplate>
                                <i class='fa <%# Eval("Icon") %>' style="color: #e74c3c; margin-right: 5px;"></i>
                                <strong><%# Eval("Name") %></strong>
                                <%# !string.IsNullOrEmpty(Eval("Description")?.ToString()) ? "<br/><small style='color:#888;'>" + Eval("Description") + "</small>" : "" %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="Phone" HeaderText="เบอร์โทร" />
                        <asp:TemplateField HeaderText="โทรด่วน" HeaderStyle-Width="70px" HeaderStyle-HorizontalAlign="Center" ItemStyle-HorizontalAlign="Center">
                            <ItemTemplate>
                                <%# (Eval("Is_Quick_Dial") != DBNull.Value && Convert.ToBoolean(Eval("Is_Quick_Dial")))
                                    ? "<span class='quick-dial-yes'><i class='fa fa-check-circle'></i></span>"
                                    : "<span class='quick-dial-no'><i class='fa fa-minus-circle'></i></span>" %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="Sort_Order" HeaderText="ลำดับ" HeaderStyle-Width="60px" HeaderStyle-HorizontalAlign="Center" ItemStyle-HorizontalAlign="Center" />
                        <asp:TemplateField HeaderText="จัดการ" HeaderStyle-Width="140px">
                            <ItemTemplate>
                                <div class="action-btns">
                                    <asp:Button ID="btnEdit" runat="server" Text="แก้ไข" CssClass="btn btn-primary btn-sm"
                                        CommandName="EditItem" CommandArgument='<%# Eval("ID") %>' />
                                    <asp:Button ID="btnDelete" runat="server" Text="ลบ" CssClass="btn btn-danger btn-sm"
                                        CommandName="DeleteItem" CommandArgument='<%# Eval("ID") %>'
                                        OnClientClick="return confirm('ยืนยันการลบ?');" />
                                </div>
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                    <EmptyDataTemplate>
                        <div style="text-align: center; padding: 40px; color: #999;">
                            <i class="fa fa-phone-slash" style="font-size: 36px; display: block; margin-bottom: 10px;"></i>
                            ไม่พบข้อมูลเบอร์ฉุกเฉิน กรุณาเพิ่มข้อมูลหรือกด "Seed ข้อมูลตัวอย่าง"
                        </div>
                    </EmptyDataTemplate>
                </asp:GridView>
            </div>

        </asp:Panel>

        <!-- ==================== FORM PANEL ==================== -->
        <asp:Panel ID="pnlForm" runat="server" Visible="false">

            <asp:HiddenField ID="hdnEditId" runat="server" />

            <div class="form-card">
                <h3><i class="fa fa-edit"></i> เพิ่ม/แก้ไขข้อมูลฉุกเฉิน</h3>

                <div class="form-row">
                    <div class="form-group">
                        <label>หมวดหมู่ <span style="color:red;">*</span></label>
                        <asp:DropDownList ID="ddlCategory" runat="server" CssClass="form-control">
                            <asp:ListItem Value="quickdial" Text="โทรด่วน (Quick Dial)"></asp:ListItem>
                            <asp:ListItem Value="hospital" Text="โรงพยาบาล"></asp:ListItem>
                            <asp:ListItem Value="police" Text="ตำรวจ/ฉุกเฉิน"></asp:ListItem>
                            <asp:ListItem Value="hotel" Text="บริการโรงแรม"></asp:ListItem>
                            <asp:ListItem Value="transport" Text="การเดินทาง"></asp:ListItem>
                        </asp:DropDownList>
                    </div>
                    <div class="form-group">
                        <label>ชื่อ <span style="color:red;">*</span></label>
                        <asp:TextBox ID="txtName" runat="server" CssClass="form-control" placeholder="เช่น แผนกต้อนรับ, โรงพยาบาลชลบุรี"></asp:TextBox>
                    </div>
                </div>

                <div class="form-row">
                    <div class="form-group">
                        <label>เบอร์โทร / หมายเลข</label>
                        <asp:TextBox ID="txtPhone" runat="server" CssClass="form-control" placeholder="เช่น 038-931-200, 1669, 1000"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label>ไอคอน (FontAwesome class)</label>
                        <asp:TextBox ID="txtIcon" runat="server" CssClass="form-control" placeholder="เช่น fa-hospital, fa-phone-alt"></asp:TextBox>
                        <small>ดูรายการไอคอนที่ fontawesome.com/icons</small>
                    </div>
                </div>

                <div class="form-group">
                    <label>รายละเอียด</label>
                    <asp:TextBox ID="txtDescription" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="3"
                        placeholder="รายละเอียดเพิ่มเติม เช่น ระยะทาง, เวลาเปิดให้บริการ"></asp:TextBox>
                </div>

                <div class="form-row">
                    <div class="form-group">
                        <label>ลำดับการแสดง (Sort Order)</label>
                        <asp:TextBox ID="txtSortOrder" runat="server" CssClass="form-control" Text="0" TextMode="Number"></asp:TextBox>
                        <small>ตัวเลขน้อย = แสดงก่อน</small>
                    </div>
                    <div class="form-group" style="display: flex; align-items: center; padding-top: 28px;">
                        <asp:CheckBox ID="chkQuickDial" runat="server" Text=" แสดงในโทรด่วน (Quick Dial)" />
                    </div>
                </div>

                <div class="form-actions">
                    <asp:Button ID="btnSave" runat="server" Text="บันทึก" CssClass="btn btn-success" OnClick="btnSave_Click" />
                    <asp:Button ID="btnCancel" runat="server" Text="ยกเลิก" CssClass="btn btn-secondary" OnClick="btnCancel_Click" CausesValidation="false" />
                </div>
            </div>

        </asp:Panel>

    </div>
</asp:Content>
