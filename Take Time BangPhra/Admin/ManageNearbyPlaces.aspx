<%@ Page Title="จัดการสถานที่ใกล้เคียง" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeFile="ManageNearbyPlaces.aspx.cs" Inherits="Take_Time_BangPhra.Admin.ManageNearbyPlaces" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .nearby-management {
            padding: 20px;
        }

        .section-header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
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
            font-size: 24px;
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
        }

        .filter-controls {
            display: flex;
            gap: 10px;
            align-items: center;
            flex-wrap: wrap;
        }

        .filter-controls label {
            font-weight: 500;
            color: #555;
            font-size: 14px;
            white-space: nowrap;
        }

        .filter-controls select {
            padding: 8px 12px;
            border: 2px solid #e0e0e0;
            border-radius: 6px;
            font-size: 14px;
            min-width: 200px;
            transition: border-color 0.3s;
        }

        .filter-controls select:focus {
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
            padding: 10px 25px;
            border-radius: 6px;
            font-weight: 500;
            cursor: pointer;
            transition: transform 0.2s, box-shadow 0.2s;
        }

        .btn-warning:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(240, 147, 251, 0.4);
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

        .btn-seed {
            background: linear-gradient(135deg, #ffecd2 0%, #fcb69f 100%);
            border: none;
            color: #333;
            padding: 10px 25px;
            border-radius: 6px;
            font-weight: 500;
            cursor: pointer;
            transition: transform 0.2s, box-shadow 0.2s;
        }

        .btn-seed:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(252, 182, 159, 0.4);
        }

        .btn-secondary {
            background: linear-gradient(135deg, #bdc3c7 0%, #95a5a6 100%);
            border: none;
            color: white;
            padding: 10px 25px;
            border-radius: 6px;
            font-weight: 500;
            cursor: pointer;
            transition: transform 0.2s, box-shadow 0.2s;
        }

        .btn-secondary:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(149, 165, 166, 0.4);
        }

        .grid-section {
            background: white;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            overflow-x: auto;
        }

        .places-grid {
            width: 100%;
            border-collapse: collapse;
            font-size: 14px;
        }

        .places-grid th {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 12px;
            text-align: left;
            font-weight: 600;
            border: none;
        }

        .places-grid td {
            padding: 12px;
            border-bottom: 1px solid #e0e0e0;
        }

        .places-grid tr:hover {
            background-color: #f8f9fa;
        }

        .places-grid .btn-sm {
            padding: 5px 12px;
            font-size: 12px;
            margin-right: 5px;
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
            margin-top: 0;
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

        .empty-data {
            text-align: center;
            padding: 40px 20px;
            color: #999;
            font-size: 16px;
        }

        @media (max-width: 768px) {
            .section-header {
                flex-direction: column;
                align-items: flex-start;
            }

            .filter-controls {
                flex-direction: column;
                align-items: stretch;
            }

            .filter-controls select {
                min-width: unset;
            }

            .form-grid {
                grid-template-columns: 1fr;
            }

            .header-actions {
                width: 100%;
            }

            .header-actions .btn-success,
            .header-actions .btn-seed {
                flex: 1;
                text-align: center;
            }
        }
    </style>

    <div class="nearby-management">
        <!-- Message -->
        <asp:Label ID="lblMessage" runat="server" Visible="false"></asp:Label>

        <!-- ========== LIST PANEL ========== -->
        <asp:Panel ID="pnlList" runat="server" Visible="true">
            <!-- Header -->
            <div class="section-header">
                <h2>📍 จัดการสถานที่ใกล้เคียง</h2>
                <div class="header-actions">
                    <asp:Button ID="btnNew" runat="server" Text="+ เพิ่มใหม่" CssClass="btn-success" OnClick="btnNew_Click" CausesValidation="false" />
                    <asp:Button ID="btnSeedData" runat="server" Text="🌱 Seed ข้อมูลตัวอย่าง" CssClass="btn-seed"
                        OnClick="btnSeedData_Click" CausesValidation="false"
                        OnClientClick="return confirm('ต้องการเพิ่มข้อมูลตัวอย่างหรือไม่?');" />
                </div>
            </div>

            <!-- Filter -->
            <div class="filter-section">
                <div class="filter-controls">
                    <label>กรองตามหมวดหมู่:</label>
                    <asp:DropDownList ID="ddlFilterCategory" runat="server" AutoPostBack="false">
                        <asp:ListItem Value="" Text="-- ทั้งหมด --" />
                        <asp:ListItem Value="beach" Text="🏖️ ชายหาด" />
                        <asp:ListItem Value="restaurant" Text="🍽️ ร้านอาหาร" />
                        <asp:ListItem Value="cafe" Text="☕ คาเฟ่" />
                        <asp:ListItem Value="attraction" Text="🎯 สถานที่ท่องเที่ยว" />
                        <asp:ListItem Value="shopping" Text="🛒 ช้อปปิ้ง" />
                    </asp:DropDownList>
                    <asp:Button ID="btnFilter" runat="server" Text="กรอง" CssClass="btn-primary" OnClick="btnFilter_Click" CausesValidation="false" />
                    <asp:Button ID="btnClearFilter" runat="server" Text="ล้างตัวกรอง" CssClass="btn-secondary" OnClick="btnClearFilter_Click" CausesValidation="false" />
                </div>
            </div>

            <!-- Data Grid -->
            <div class="grid-section">
                <asp:GridView ID="gvList" runat="server"
                    AutoGenerateColumns="False"
                    CssClass="places-grid"
                    OnRowCommand="gvList_RowCommand"
                    ShowHeaderWhenEmpty="True">
                    <Columns>
                        <asp:BoundField DataField="ID" HeaderText="ID" />
                        <asp:TemplateField HeaderText="หมวดหมู่">
                            <ItemTemplate>
                                <%# GetCategoryText(Eval("Category").ToString()) %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="Name" HeaderText="ชื่อสถานที่" />
                        <asp:BoundField DataField="Distance" HeaderText="ระยะทาง" NullDisplayText="-" />
                        <asp:BoundField DataField="Phone" HeaderText="โทรศัพท์" NullDisplayText="-" />
                        <asp:BoundField DataField="Sort_Order" HeaderText="ลำดับ" />
                        <asp:TemplateField HeaderText="การจัดการ">
                            <ItemTemplate>
                                <asp:Button ID="btnEdit" runat="server"
                                    Text="แก้ไข"
                                    CssClass="btn-warning btn-sm"
                                    CommandName="EditItem"
                                    CommandArgument='<%# Eval("ID") %>'
                                    CausesValidation="false" />
                                <asp:Button ID="btnDelete" runat="server"
                                    Text="ลบ"
                                    CssClass="btn-danger btn-sm"
                                    CommandName="DeleteItem"
                                    CommandArgument='<%# Eval("ID") %>'
                                    OnClientClick="return confirm('คุณแน่ใจหรือไม่ว่าต้องการลบรายการนี้?');"
                                    CausesValidation="false" />
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                    <EmptyDataTemplate>
                        <div class="empty-data">
                            📭 ไม่พบข้อมูล
                        </div>
                    </EmptyDataTemplate>
                </asp:GridView>
            </div>
        </asp:Panel>

        <!-- ========== FORM PANEL ========== -->
        <asp:Panel ID="pnlForm" runat="server" Visible="false">
            <div class="form-section">
                <h3>
                    <asp:Label ID="lblFormTitle" runat="server" Text="เพิ่มสถานที่ใกล้เคียง"></asp:Label>
                </h3>
                <asp:HiddenField ID="hfEditId" runat="server" />

                <div class="form-grid">
                    <!-- Category -->
                    <div class="form-group">
                        <label>หมวดหมู่<span class="required">*</span></label>
                        <asp:DropDownList ID="ddlCategory" runat="server">
                            <asp:ListItem Value="beach" Text="🏖️ ชายหาด" />
                            <asp:ListItem Value="restaurant" Text="🍽️ ร้านอาหาร" />
                            <asp:ListItem Value="cafe" Text="☕ คาเฟ่" />
                            <asp:ListItem Value="attraction" Text="🎯 สถานที่ท่องเที่ยว" />
                            <asp:ListItem Value="shopping" Text="🛒 ช้อปปิ้ง" />
                        </asp:DropDownList>
                    </div>

                    <!-- Name -->
                    <div class="form-group">
                        <label>ชื่อสถานที่<span class="required">*</span></label>
                        <asp:TextBox ID="txtName" runat="server" MaxLength="200" placeholder="เช่น หาดบางพระ"></asp:TextBox>
                    </div>

                    <!-- Distance -->
                    <div class="form-group">
                        <label>ระยะทาง</label>
                        <asp:TextBox ID="txtDistance" runat="server" MaxLength="50" placeholder="เช่น 1.5 km"></asp:TextBox>
                    </div>

                    <!-- Travel Time -->
                    <div class="form-group">
                        <label>เวลาเดินทาง</label>
                        <asp:TextBox ID="txtTravelTime" runat="server" MaxLength="50" placeholder="เช่น 5 นาที"></asp:TextBox>
                    </div>

                    <!-- Phone -->
                    <div class="form-group">
                        <label>เบอร์โทรศัพท์</label>
                        <asp:TextBox ID="txtPhone" runat="server" MaxLength="50" placeholder="เช่น 038-123456"></asp:TextBox>
                    </div>

                    <!-- Icon -->
                    <div class="form-group">
                        <label>ไอคอน (Emoji)</label>
                        <asp:TextBox ID="txtIcon" runat="server" MaxLength="50" placeholder="เช่น 🏖️"></asp:TextBox>
                    </div>

                    <!-- Sort Order -->
                    <div class="form-group">
                        <label>ลำดับการแสดงผล</label>
                        <asp:TextBox ID="txtSortOrder" runat="server" MaxLength="5" Text="0" placeholder="0"></asp:TextBox>
                    </div>

                    <!-- Map URL -->
                    <div class="form-group">
                        <label>ลิงก์แผนที่ (Google Maps URL)</label>
                        <asp:TextBox ID="txtMapUrl" runat="server" MaxLength="500" placeholder="https://maps.google.com/..."></asp:TextBox>
                    </div>
                </div>

                <!-- Description (Full Width) -->
                <div class="form-group" style="margin-bottom: 10px;">
                    <label>คำอธิบาย</label>
                    <asp:TextBox ID="txtDescription" runat="server" TextMode="MultiLine" Rows="3" MaxLength="500" placeholder="รายละเอียดเพิ่มเติมเกี่ยวกับสถานที่"></asp:TextBox>
                </div>

                <!-- Form Actions -->
                <div class="form-actions">
                    <asp:Button ID="btnSave" runat="server" Text="บันทึก" CssClass="btn-success" OnClick="btnSave_Click" />
                    <asp:Button ID="btnCancel" runat="server" Text="ยกเลิก" CssClass="btn-secondary" OnClick="btnCancel_Click" CausesValidation="false" />
                </div>
            </div>
        </asp:Panel>
    </div>

    <script type="text/javascript">
        // Auto-hide alerts after 5 seconds
        setTimeout(function () {
            var alerts = document.querySelectorAll('.alert');
            alerts.forEach(function (alert) {
                alert.style.transition = 'opacity 0.5s';
                alert.style.opacity = '0';
                setTimeout(function () { alert.style.display = 'none'; }, 500);
            });
        }, 5000);
    </script>
</asp:Content>
