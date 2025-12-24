<%@ Page Title="จัดการสินทรัพย์" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="AssetManagement.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Assets.AssetManagement" MaintainScrollPositionOnPostback="true" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .asset-management {
            padding: 20px;
            max-width: 1400px;
            margin: 0 auto;
        }

        .page-header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 20px 25px;
            border-radius: 10px;
            margin-bottom: 25px;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .page-header h2 {
            margin: 0;
            font-size: 24px;
        }

        .summary-cards {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 15px;
            margin-bottom: 25px;
        }

        .summary-card {
            background: white;
            padding: 20px;
            border-radius: 10px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.08);
            border-left: 4px solid #667eea;
        }

        .summary-card h4 {
            margin: 0 0 10px 0;
            color: #666;
            font-size: 14px;
            font-weight: 500;
            line-height: 1.5;
        }

        .summary-card .value {
            font-size: 28px;
            font-weight: 700;
            color: #333;
            line-height: 1.2;
        }

        .summary-card .sub-value {
            font-size: 13px;
            color: #888;
            margin-top: 6px;
            line-height: 1.4;
        }

        .summary-card.success { border-left-color: #28a745; }
        .summary-card.warning { border-left-color: #ffc107; }
        .summary-card.danger { border-left-color: #dc3545; }
        .summary-card.info { border-left-color: #17a2b8; }

        .filter-section {
            background: white;
            padding: 20px;
            border-radius: 10px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.08);
            margin-bottom: 20px;
        }

        .filter-row {
            display: flex;
            flex-wrap: wrap;
            gap: 15px;
            align-items: end;
        }

        .filter-group {
            flex: 1;
            min-width: 220px;
        }

        .filter-group select,
        .filter-group .form-control {
            min-width: 100%;
        }

        .filter-group label {
            display: block;
            margin-bottom: 8px;
            font-weight: 500;
            color: #555;
            font-size: 14px;
        }

        .form-control {
            width: 100%;
            padding: 12px 14px;
            border: 2px solid #e0e0e0;
            border-radius: 6px;
            font-size: 15px;
            transition: border-color 0.2s;
            box-sizing: border-box;
            line-height: 1.5;
        }

        /* Fix Thai text display in dropdowns */
        select.form-control {
            height: auto;
            min-height: 48px;
            padding: 10px 14px;
            font-size: 15px;
            line-height: 1.6;
            -webkit-appearance: none;
            -moz-appearance: none;
            appearance: none;
            background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 12 12'%3E%3Cpath fill='%23666' d='M6 8L1 3h10z'/%3E%3C/svg%3E");
            background-repeat: no-repeat;
            background-position: right 12px center;
            padding-right: 35px;
        }

        select.form-control option {
            padding: 12px 14px;
            font-size: 15px;
            line-height: 1.6;
        }

        .form-control:focus {
            border-color: #667eea;
            outline: none;
        }

        .btn {
            padding: 12px 22px;
            border: none;
            border-radius: 6px;
            font-weight: 500;
            cursor: pointer;
            transition: all 0.2s;
            font-size: 15px;
            line-height: 1.5;
        }

        .btn-primary {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
        }

        .btn-success {
            background: linear-gradient(135deg, #28a745 0%, #20c997 100%);
            color: white;
        }

        .btn-secondary {
            background: #6c757d;
            color: white;
        }

        .btn-danger {
            background: #dc3545;
            color: white;
        }

        .btn-sm {
            padding: 6px 12px;
            font-size: 12px;
        }

        .btn:hover {
            transform: translateY(-1px);
            box-shadow: 0 4px 12px rgba(0,0,0,0.15);
        }

        .data-section {
            background: white;
            border-radius: 10px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.08);
            overflow: hidden;
        }

        .section-header {
            background: #f8f9fa;
            padding: 15px 20px;
            border-bottom: 1px solid #e9ecef;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .section-header h3 {
            margin: 0;
            font-size: 16px;
            color: #333;
            line-height: 1.5;
        }

        .asset-table {
            width: 100%;
            border-collapse: collapse;
        }

        .asset-table th {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 14px 16px;
            text-align: left;
            font-weight: 500;
            font-size: 14px;
            line-height: 1.5;
            white-space: nowrap;
        }

        .asset-table td {
            padding: 14px 16px;
            border-bottom: 1px solid #f0f0f0;
            font-size: 14px;
            line-height: 1.5;
        }

        .asset-table tr:hover {
            background: #f8f9fa;
        }

        .badge {
            display: inline-block;
            padding: 6px 12px;
            border-radius: 20px;
            font-size: 13px;
            font-weight: 500;
            line-height: 1.4;
        }

        .badge-active { background: #d4edda; color: #155724; }
        .badge-disposed { background: #f8d7da; color: #721c24; }
        .badge-sold { background: #fff3cd; color: #856404; }

        .category-badge {
            background: #e3f2fd;
            color: #1976d2;
            padding: 6px 10px;
            border-radius: 4px;
            font-size: 13px;
            line-height: 1.4;
        }

        .text-right { text-align: right; }
        .text-center { text-align: center; }

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
            overflow-y: auto;
        }

        .modal-content {
            background: white;
            max-width: 800px;
            margin: 30px auto;
            border-radius: 10px;
            box-shadow: 0 10px 40px rgba(0,0,0,0.3);
        }

        .modal-header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 15px 20px;
            border-radius: 10px 10px 0 0;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .modal-header h3 { margin: 0; }

        .modal-close {
            background: none;
            border: none;
            color: white;
            font-size: 24px;
            cursor: pointer;
            padding: 0;
            line-height: 1;
        }

        .modal-body {
            padding: 25px;
            max-height: 70vh;
            overflow-y: auto;
        }

        .form-row {
            display: grid;
            grid-template-columns: repeat(2, 1fr);
            gap: 20px;
            margin-bottom: 15px;
        }

        .form-group {
            margin-bottom: 18px;
        }

        .form-group label {
            display: block;
            margin-bottom: 8px;
            font-weight: 500;
            color: #333;
            font-size: 14px;
            line-height: 1.5;
        }

        .form-group label .required {
            color: #dc3545;
        }

        /* Modal form controls */
        .modal-body .form-control {
            font-size: 15px;
            padding: 12px 14px;
            min-height: 48px;
        }

        .modal-body select.form-control {
            min-height: 48px;
        }

        .modal-body textarea.form-control {
            min-height: auto;
        }

        .form-group.full-width {
            grid-column: 1 / -1;
        }

        .modal-footer {
            padding: 15px 25px;
            border-top: 1px solid #e9ecef;
            display: flex;
            justify-content: flex-end;
            gap: 10px;
        }

        .alert {
            padding: 15px;
            border-radius: 6px;
            margin-bottom: 20px;
        }

        .alert-success { background: #d4edda; color: #155724; border: 1px solid #c3e6cb; }
        .alert-error { background: #f8d7da; color: #721c24; border: 1px solid #f5c6cb; }

        @media (max-width: 768px) {
            .form-row {
                grid-template-columns: 1fr;
            }

            .filter-row {
                flex-direction: column;
            }

            .filter-group {
                min-width: 100%;
            }

            .summary-cards {
                grid-template-columns: 1fr 1fr;
            }

            .asset-table {
                font-size: 13px;
            }

            .asset-table th, .asset-table td {
                padding: 10px 12px;
            }

            select.form-control {
                min-height: 44px;
                font-size: 14px;
            }
        }
    </style>

    <div class="asset-management">
        <div class="page-header">
            <h2><i class="fas fa-boxes"></i> จัดการสินทรัพย์ถาวร</h2>
            <asp:Button ID="btnAddNew" runat="server" Text="+ เพิ่มสินทรัพย์" CssClass="btn btn-success" OnClientClick="showAddModal(); return false;" />
        </div>

        <!-- Message Panel -->
        <asp:Panel ID="pnlMessage" runat="server" Visible="false" CssClass="alert">
            <asp:Label ID="lblMessage" runat="server"></asp:Label>
        </asp:Panel>

        <!-- Summary Cards -->
        <div class="summary-cards">
            <div class="summary-card">
                <h4><i class="fas fa-cubes"></i> สินทรัพย์ทั้งหมด</h4>
                <div class="value"><asp:Label ID="lblTotalAssets" runat="server" Text="0"></asp:Label></div>
                <div class="sub-value">รายการ</div>
            </div>
            <div class="summary-card success">
                <h4><i class="fas fa-check-circle"></i> ใช้งานอยู่</h4>
                <div class="value"><asp:Label ID="lblActiveAssets" runat="server" Text="0"></asp:Label></div>
                <div class="sub-value">รายการ</div>
            </div>
            <div class="summary-card info">
                <h4><i class="fas fa-money-bill-wave"></i> มูลค่าซื้อรวม</h4>
                <div class="value"><asp:Label ID="lblTotalPurchase" runat="server" Text="0"></asp:Label></div>
                <div class="sub-value">บาท</div>
            </div>
            <div class="summary-card warning">
                <h4><i class="fas fa-chart-line"></i> ค่าเสื่อมสะสม</h4>
                <div class="value"><asp:Label ID="lblTotalDepreciation" runat="server" Text="0"></asp:Label></div>
                <div class="sub-value">บาท</div>
            </div>
            <div class="summary-card">
                <h4><i class="fas fa-book"></i> มูลค่าตามบัญชี</h4>
                <div class="value"><asp:Label ID="lblTotalBookValue" runat="server" Text="0"></asp:Label></div>
                <div class="sub-value">บาท</div>
            </div>
        </div>

        <!-- Filter Section -->
        <div class="filter-section">
            <div class="filter-row">
                <div class="filter-group">
                    <label>หมวดหมู่</label>
                    <asp:DropDownList ID="ddlFilterCategory" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlFilterCategory_SelectedIndexChanged">
                        <asp:ListItem Value="">-- ทั้งหมด --</asp:ListItem>
                    </asp:DropDownList>
                </div>
                <div class="filter-group">
                    <label>สถานะ</label>
                    <asp:DropDownList ID="ddlFilterStatus" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlFilterStatus_SelectedIndexChanged">
                        <asp:ListItem Value="">-- ทั้งหมด --</asp:ListItem>
                        <asp:ListItem Value="ACTIVE">ใช้งานอยู่</asp:ListItem>
                        <asp:ListItem Value="DISPOSED">จำหน่ายแล้ว</asp:ListItem>
                        <asp:ListItem Value="SOLD">ขายแล้ว</asp:ListItem>
                        <asp:ListItem Value="WRITTEN_OFF">ตัดจำหน่าย</asp:ListItem>
                    </asp:DropDownList>
                </div>
                <div class="filter-group">
                    <label>ค้นหา</label>
                    <asp:TextBox ID="txtSearch" runat="server" CssClass="form-control" placeholder="รหัส, ชื่อ, ซีเรียล..."></asp:TextBox>
                </div>
                <div class="filter-group">
                    <asp:Button ID="btnSearch" runat="server" Text="ค้นหา" CssClass="btn btn-primary" OnClick="btnSearch_Click" />
                </div>
            </div>
        </div>

        <!-- Assets Table -->
        <div class="data-section">
            <div class="section-header">
                <h3><i class="fas fa-list"></i> รายการสินทรัพย์</h3>
                <asp:Button ID="btnCalcDepreciation" runat="server" Text="คำนวณค่าเสื่อม" CssClass="btn btn-secondary btn-sm" OnClick="btnCalcDepreciation_Click" />
            </div>
            <asp:GridView ID="gvAssets" runat="server" AutoGenerateColumns="False"
                CssClass="asset-table" GridLines="None" OnRowCommand="gvAssets_RowCommand">
                <Columns>
                    <asp:BoundField DataField="AssetCode" HeaderText="รหัส" />
                    <asp:TemplateField HeaderText="ชื่อสินทรัพย์">
                        <ItemTemplate>
                            <strong><%# Eval("AssetName") %></strong>
                            <br /><small style="color:#888;"><%# Eval("Brand") %> <%# Eval("Model") %></small>
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="หมวดหมู่">
                        <ItemTemplate>
                            <span class="category-badge"><%# Eval("CategoryName") %></span>
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="วันที่ซื้อ">
                        <ItemTemplate>
                            <%# Convert.ToDateTime(Eval("PurchaseDate")).ToString("dd/MM/yyyy") %>
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="ราคาซื้อ" ItemStyle-CssClass="text-right" HeaderStyle-CssClass="text-right">
                        <ItemTemplate>
                            <%# string.Format("{0:N2}", Eval("PurchasePrice")) %>
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="ค่าเสื่อมสะสม" ItemStyle-CssClass="text-right" HeaderStyle-CssClass="text-right">
                        <ItemTemplate>
                            <%# string.Format("{0:N2}", Eval("AccumulatedDepreciation")) %>
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="มูลค่าตามบัญชี" ItemStyle-CssClass="text-right" HeaderStyle-CssClass="text-right">
                        <ItemTemplate>
                            <strong><%# string.Format("{0:N2}", Eval("BookValue")) %></strong>
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="สถานะ" ItemStyle-CssClass="text-center">
                        <ItemTemplate>
                            <%# GetStatusBadge(Eval("Status").ToString()) %>
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="จัดการ" ItemStyle-CssClass="text-center">
                        <ItemTemplate>
                            <asp:LinkButton ID="btnView" runat="server" CommandName="ViewAsset" CommandArgument='<%# Eval("ID") %>'
                                CssClass="btn btn-primary btn-sm" ToolTip="ดูรายละเอียด">
                                <i class="fas fa-eye"></i>
                            </asp:LinkButton>
                            <asp:LinkButton ID="btnEdit" runat="server" CommandName="EditAsset" CommandArgument='<%# Eval("ID") %>'
                                CssClass="btn btn-secondary btn-sm" ToolTip="แก้ไข" Visible='<%# Eval("Status").ToString() == "ACTIVE" %>'>
                                <i class="fas fa-edit"></i>
                            </asp:LinkButton>
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
                <EmptyDataTemplate>
                    <div style="text-align: center; padding: 40px; color: #999;">
                        <i class="fas fa-inbox" style="font-size: 48px; margin-bottom: 15px; display: block;"></i>
                        ไม่พบข้อมูลสินทรัพย์
                    </div>
                </EmptyDataTemplate>
            </asp:GridView>
        </div>
    </div>

    <!-- Add/Edit Modal -->
    <div id="assetModal" class="modal-overlay">
        <div class="modal-content">
            <div class="modal-header">
                <h3><i class="fas fa-plus-circle"></i> <asp:Label ID="lblModalTitle" runat="server" Text="เพิ่มสินทรัพย์ใหม่"></asp:Label></h3>
                <button type="button" class="modal-close" onclick="closeModal()">&times;</button>
            </div>
            <div class="modal-body">
                <asp:HiddenField ID="hdnAssetId" runat="server" Value="0" />

                <div class="form-row">
                    <div class="form-group">
                        <label>หมวดหมู่ <span class="required">*</span></label>
                        <asp:DropDownList ID="ddlCategory" runat="server" CssClass="form-control">
                        </asp:DropDownList>
                    </div>
                    <div class="form-group">
                        <label>ชื่อสินทรัพย์ <span class="required">*</span></label>
                        <asp:TextBox ID="txtAssetName" runat="server" CssClass="form-control" placeholder="เช่น คอมพิวเตอร์ตั้งโต๊ะ"></asp:TextBox>
                    </div>
                </div>

                <div class="form-row">
                    <div class="form-group">
                        <label>ยี่ห้อ</label>
                        <asp:TextBox ID="txtBrand" runat="server" CssClass="form-control" placeholder="เช่น Dell, HP"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label>รุ่น</label>
                        <asp:TextBox ID="txtModel" runat="server" CssClass="form-control" placeholder="Model number"></asp:TextBox>
                    </div>
                </div>

                <div class="form-row">
                    <div class="form-group">
                        <label>หมายเลขซีเรียล</label>
                        <asp:TextBox ID="txtSerialNumber" runat="server" CssClass="form-control"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label>วันที่ซื้อ <span class="required">*</span></label>
                        <asp:TextBox ID="txtPurchaseDate" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                    </div>
                </div>

                <div class="form-row">
                    <div class="form-group">
                        <label>ราคาซื้อ (รวม VAT) <span class="required">*</span></label>
                        <asp:TextBox ID="txtPurchasePrice" runat="server" CssClass="form-control" TextMode="Number" step="0.01"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label>อายุการใช้งาน (ปี) <span class="required">*</span></label>
                        <asp:TextBox ID="txtUsefulLife" runat="server" CssClass="form-control" TextMode="Number" Text="5"></asp:TextBox>
                    </div>
                </div>

                <div class="form-row">
                    <div class="form-group">
                        <label>มูลค่าซาก</label>
                        <asp:TextBox ID="txtResidualValue" runat="server" CssClass="form-control" TextMode="Number" step="0.01" Text="0"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label>วันหมดประกัน</label>
                        <asp:TextBox ID="txtWarrantyExpire" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                    </div>
                </div>

                <div class="form-row">
                    <div class="form-group">
                        <label>ผู้ขาย (Vendor)</label>
                        <asp:DropDownList ID="ddlVendor" runat="server" CssClass="form-control">
                            <asp:ListItem Value="">-- ไม่ระบุ --</asp:ListItem>
                        </asp:DropDownList>
                    </div>
                    <div class="form-group">
                        <label>เลขที่ใบกำกับภาษี</label>
                        <asp:TextBox ID="txtInvoiceNumber" runat="server" CssClass="form-control"></asp:TextBox>
                    </div>
                </div>

                <div class="form-row">
                    <div class="form-group">
                        <label>สถานที่ตั้ง</label>
                        <asp:TextBox ID="txtLocation" runat="server" CssClass="form-control" placeholder="เช่น สำนักงาน, โซน A"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label>แผนก</label>
                        <asp:TextBox ID="txtDepartment" runat="server" CssClass="form-control"></asp:TextBox>
                    </div>
                </div>

                <div class="form-group full-width">
                    <label>รายละเอียด/หมายเหตุ</label>
                    <asp:TextBox ID="txtDescription" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="3"></asp:TextBox>
                </div>
            </div>
            <div class="modal-footer">
                <button type="button" class="btn btn-secondary" onclick="closeModal()">ยกเลิก</button>
                <asp:Button ID="btnSaveAsset" runat="server" Text="บันทึก" CssClass="btn btn-success" OnClick="btnSaveAsset_Click" />
            </div>
        </div>
    </div>

    <script type="text/javascript">
        function showAddModal() {
            document.getElementById('assetModal').style.display = 'block';
            document.body.style.overflow = 'hidden';
        }

        function closeModal() {
            document.getElementById('assetModal').style.display = 'none';
            document.body.style.overflow = 'auto';
        }

        // Close modal when clicking outside
        document.getElementById('assetModal').addEventListener('click', function(e) {
            if (e.target === this) {
                closeModal();
            }
        });

        // Close modal on Escape key
        document.addEventListener('keydown', function(e) {
            if (e.key === 'Escape') {
                closeModal();
            }
        });
    </script>
</asp:Content>
