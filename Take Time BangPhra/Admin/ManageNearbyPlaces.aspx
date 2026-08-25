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
                    <asp:Button ID="btnTabCategories" runat="server" Text="🏷️ ประเภทสถานที่" CssClass="btn-secondary" OnClick="btnTabCategories_Click" CausesValidation="false" />
                    <asp:Button ID="btnTabZones" runat="server" Text="🗺️ โซน / ขอบเขต" CssClass="btn-secondary" OnClick="btnTabZones_Click" CausesValidation="false" />
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
                    <asp:DropDownList ID="ddlFilterCategory" runat="server" AutoPostBack="false"></asp:DropDownList>
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
                        <asp:TemplateField HeaderText="แผนที่">
                            <ItemTemplate>
                                <%# HasCoords(Eval("Latitude"), Eval("Longitude")) ? "📍 มีพิกัด" : "<span style='color:#c62828'>ยังไม่มีพิกัด</span>" %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="รูป">
                            <ItemTemplate>
                                <%# string.IsNullOrEmpty(Eval("Image_Path") == null ? "" : Eval("Image_Path").ToString())
                                    ? "-" : "<img src='" + Eval("Image_Path") + "' style='width:48px;height:36px;object-fit:cover;border-radius:4px' />" %>
                            </ItemTemplate>
                        </asp:TemplateField>
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
                        <asp:DropDownList ID="ddlCategory" runat="server"></asp:DropDownList>
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
                        <label>ลิงก์แผนที่ (ถ้าเว้นว่าง ระบบสร้างลิงก์นำทางจากพิกัดให้เอง)</label>
                        <asp:TextBox ID="txtMapUrl" runat="server" MaxLength="500" placeholder="https://maps.google.com/..."></asp:TextBox>
                    </div>

                    <!-- โซน / พื้นที่ -->
                    <div class="form-group">
                        <label>โซน / พื้นที่</label>
                        <asp:DropDownList ID="ddlZone" runat="server"></asp:DropDownList>
                    </div>

                    <!-- ที่อยู่ -->
                    <div class="form-group">
                        <label>ที่อยู่</label>
                        <asp:TextBox ID="txtAddress" runat="server" MaxLength="300" placeholder="เช่น ต.บางพระ อ.ศรีราชา"></asp:TextBox>
                    </div>

                    <!-- เวลาเปิด-ปิด -->
                    <div class="form-group">
                        <label>เวลาเปิด-ปิด</label>
                        <asp:TextBox ID="txtOpenHours" runat="server" MaxLength="100" placeholder="เช่น 09:00 - 18:00"></asp:TextBox>
                    </div>

                    <!-- หมุด: อิโมจิ -->
                    <div class="form-group">
                        <label>อิโมจิบนหมุด (ว่าง = ใช้ของประเภท)</label>
                        <asp:TextBox ID="txtMarkerIcon" runat="server" MaxLength="50" placeholder="เช่น 🏖️"></asp:TextBox>
                    </div>

                    <!-- หมุด: สี -->
                    <div class="form-group">
                        <label>สีหมุด (ว่าง = ใช้ของประเภท)</label>
                        <asp:TextBox ID="txtMarkerColor" runat="server" MaxLength="20" placeholder="#0288D1"></asp:TextBox>
                    </div>

                    <!-- เปิดใช้งาน -->
                    <div class="form-group">
                        <label>สถานะ</label>
                        <asp:CheckBox ID="chkActive" runat="server" Checked="true" Text=" แสดงให้แขกเห็น" />
                    </div>
                </div>

                <!-- ── พิกัด + ตัวเลือกจากแผนที่ ───────────────────────────────── -->
                <div class="form-group" style="margin-bottom:10px;">
                    <label>พิกัด (ละติจูด / ลองจิจูด) — จำเป็นสำหรับแสดงหมุดบนแผนที่</label>
                    <div style="display:flex; gap:8px; flex-wrap:wrap; align-items:center;">
                        <asp:TextBox ID="txtLat" runat="server" MaxLength="20" placeholder="13.174800" ClientIDMode="Static" Style="max-width:160px;"></asp:TextBox>
                        <asp:TextBox ID="txtLng" runat="server" MaxLength="20" placeholder="100.930600" ClientIDMode="Static" Style="max-width:160px;"></asp:TextBox>
                        <input type="text" id="pasteMapUrl" placeholder="วางลิงก์ Google Maps ที่นี่แล้วกดดึงพิกัด" style="flex:1; min-width:240px; padding:8px 12px; border:2px solid #e0e0e0; border-radius:6px;" />
                        <button type="button" class="btn-primary" onclick="nbExtractFromUrl()">ดึงพิกัดจากลิงก์</button>
                    </div>
                    <small style="color:#777;">คลิกบนแผนที่ด้านล่างเพื่อวางหมุด หรือวางลิงก์ Google Maps แล้วกดปุ่มดึงพิกัด</small>
                    <div id="pickMap" style="height:320px; border-radius:10px; margin-top:8px; border:1px solid #e0e0e0;"></div>
                </div>

                <!-- ── รูปภาพ ──────────────────────────────────────────────────── -->
                <div class="form-group" style="margin-bottom:10px;">
                    <label>รูปภาพสถานที่</label>
                    <asp:FileUpload ID="fuImage" runat="server" accept="image/*" />
                    <asp:Panel ID="pnlCurrentImage" runat="server" Visible="false" style="margin-top:8px;">
                        <asp:Image ID="imgCurrent" runat="server" Style="max-width:200px; border-radius:8px; display:block;" />
                        <asp:CheckBox ID="chkRemoveImage" runat="server" Text=" ลบรูปนี้" />
                    </asp:Panel>
                </div>

                <div class="form-group" style="margin-bottom:10px;">
                    <label>รูปหมุดแบบกำหนดเอง (ถ้าใส่ จะใช้แทนหมุดอิโมจิ)</label>
                    <asp:FileUpload ID="fuMarkerImage" runat="server" accept="image/*" />
                    <asp:Panel ID="pnlCurrentMarker" runat="server" Visible="false" style="margin-top:8px;">
                        <asp:Image ID="imgCurrentMarker" runat="server" Style="width:48px; height:48px; object-fit:cover; border-radius:50%; display:block;" />
                        <asp:CheckBox ID="chkRemoveMarkerImage" runat="server" Text=" ลบรูปหมุดนี้" />
                    </asp:Panel>
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

        <!-- ========== ประเภทสถานที่ ========== -->
        <asp:Panel ID="pnlCategories" runat="server" Visible="false">
            <div class="section-header">
                <h2>🏷️ ประเภทสถานที่</h2>
                <div class="header-actions">
                    <asp:Button ID="btnBackFromCat" runat="server" Text="← กลับไปรายการสถานที่" CssClass="btn-secondary" OnClick="btnBackToList_Click" CausesValidation="false" />
                </div>
            </div>
            <div class="form-section">
                <asp:HiddenField ID="hfCatId" runat="server" />
                <div class="form-grid">
                    <div class="form-group">
                        <label>รหัส (ภาษาอังกฤษ ห้ามซ้ำ)<span class="required">*</span></label>
                        <asp:TextBox ID="txtCatCode" runat="server" MaxLength="50" placeholder="เช่น waterfall"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label>ชื่อที่แสดง<span class="required">*</span></label>
                        <asp:TextBox ID="txtCatName" runat="server" MaxLength="100" placeholder="เช่น น้ำตก"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label>อิโมจิ</label>
                        <asp:TextBox ID="txtCatIcon" runat="server" MaxLength="50" placeholder="🏞️"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label>สีหมุดประจำประเภท</label>
                        <asp:TextBox ID="txtCatColor" runat="server" MaxLength="20" placeholder="#0288D1"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label>ลำดับ</label>
                        <asp:TextBox ID="txtCatOrder" runat="server" MaxLength="5" Text="0"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label>สถานะ</label>
                        <asp:CheckBox ID="chkCatActive" runat="server" Checked="true" Text=" เปิดใช้งาน" />
                    </div>
                </div>
                <div class="form-actions">
                    <asp:Button ID="btnCatSave" runat="server" Text="บันทึกประเภท" CssClass="btn-success" OnClick="btnCatSave_Click" CausesValidation="false" />
                    <asp:Button ID="btnCatClear" runat="server" Text="ล้างฟอร์ม" CssClass="btn-secondary" OnClick="btnCatClear_Click" CausesValidation="false" />
                </div>
            </div>
            <div class="grid-section">
                <asp:GridView ID="gvCategories" runat="server" AutoGenerateColumns="False" CssClass="places-grid"
                    OnRowCommand="gvCategories_RowCommand" ShowHeaderWhenEmpty="True">
                    <Columns>
                        <asp:BoundField DataField="Code" HeaderText="รหัส" />
                        <asp:BoundField DataField="Name" HeaderText="ชื่อ" />
                        <asp:TemplateField HeaderText="หมุด">
                            <ItemTemplate>
                                <span style="display:inline-block;width:26px;height:26px;line-height:24px;text-align:center;border-radius:50%;color:#fff;background:<%# Eval("Marker_Color") %>"><%# Eval("Icon") %></span>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="Sort_Order" HeaderText="ลำดับ" />
                        <asp:BoundField DataField="Status" HeaderText="สถานะ" />
                        <asp:TemplateField HeaderText="การจัดการ">
                            <ItemTemplate>
                                <asp:Button runat="server" Text="แก้ไข" CssClass="btn-warning btn-sm" CommandName="EditCat" CommandArgument='<%# Eval("ID") %>' CausesValidation="false" />
                                <asp:Button runat="server" Text="ลบ" CssClass="btn-danger btn-sm" CommandName="DeleteCat" CommandArgument='<%# Eval("ID") %>' CausesValidation="false"
                                    OnClientClick="return confirm('ลบประเภทนี้?');" />
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                    <EmptyDataTemplate><div class="empty-data">📭 ยังไม่มีประเภท (รันไมเกรชัน PHASE19_01 ก่อน)</div></EmptyDataTemplate>
                </asp:GridView>
            </div>
        </asp:Panel>

        <!-- ========== โซน / ขอบเขต ========== -->
        <asp:Panel ID="pnlZones" runat="server" Visible="false">
            <div class="section-header">
                <h2>🗺️ โซน / ขอบเขตพื้นที่</h2>
                <div class="header-actions">
                    <asp:Button ID="btnBackFromZone" runat="server" Text="← กลับไปรายการสถานที่" CssClass="btn-secondary" OnClick="btnBackToList_Click" CausesValidation="false" />
                </div>
            </div>
            <div class="form-section">
                <asp:HiddenField ID="hfZoneId" runat="server" />
                <div class="form-grid">
                    <div class="form-group">
                        <label>ชื่อโซน<span class="required">*</span></label>
                        <asp:TextBox ID="txtZoneName" runat="server" MaxLength="150" placeholder="เช่น อำเภอศรีราชา"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label>จุดกึ่งกลาง — ละติจูด</label>
                        <asp:TextBox ID="txtZoneLat" runat="server" MaxLength="20" ClientIDMode="Static" placeholder="13.174800"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label>จุดกึ่งกลาง — ลองจิจูด</label>
                        <asp:TextBox ID="txtZoneLng" runat="server" MaxLength="20" ClientIDMode="Static" placeholder="100.930600"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label>ระดับซูมเริ่มต้น</label>
                        <asp:TextBox ID="txtZoneZoom" runat="server" MaxLength="3" Text="12"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label>สีพื้นขอบเขต</label>
                        <asp:TextBox ID="txtZoneFill" runat="server" MaxLength="20" Text="#00b09b"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label>สีเส้นขอบ</label>
                        <asp:TextBox ID="txtZoneLine" runat="server" MaxLength="20" Text="#00796B"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label>ลำดับ</label>
                        <asp:TextBox ID="txtZoneOrder" runat="server" MaxLength="5" Text="0"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label>ตั้งค่า</label>
                        <asp:CheckBox ID="chkZoneDefault" runat="server" Text=" เป็นโซนที่เปิดมาเห็นก่อน" /><br />
                        <asp:CheckBox ID="chkZoneActive" runat="server" Checked="true" Text=" เปิดใช้งาน" />
                    </div>
                </div>

                <div class="form-group" style="margin-bottom:10px;">
                    <label>ขอบเขตพื้นที่ (GeoJSON)</label>
                    <small style="display:block; color:#777; margin-bottom:6px;">
                        เว้นว่างได้ — แผนที่จะย่อ/ขยายให้พอดีกับหมุดทั้งหมดแทน ·
                        วาดเองได้โดยกด "เริ่มวาดขอบเขต" แล้วคลิกไปตามแนวเขตบนแผนที่ ·
                        หรือวาง GeoJSON ของเขตการปกครองจาก OpenStreetMap
                    </small>
                    <asp:TextBox ID="txtZoneGeo" runat="server" TextMode="MultiLine" Rows="4" ClientIDMode="Static"
                        placeholder='{"type":"Polygon","coordinates":[[[100.9,13.1],[100.95,13.1],[100.95,13.2],[100.9,13.2],[100.9,13.1]]]}'></asp:TextBox>
                    <div style="display:flex; gap:8px; flex-wrap:wrap; margin-top:8px;">
                        <button type="button" class="btn-primary" onclick="nbDrawStart()">✏️ เริ่มวาดขอบเขต</button>
                        <button type="button" class="btn-warning" onclick="nbDrawUndo()">↶ ถอยหนึ่งจุด</button>
                        <button type="button" class="btn-success" onclick="nbDrawFinish()">✔ ปิดรูปแล้วบันทึกลงช่อง</button>
                        <button type="button" class="btn-danger" onclick="nbDrawClear()">✖ ล้างที่วาด</button>
                        <button type="button" class="btn-secondary" onclick="nbZonePreview()">🔄 แสดงตัวอย่างจากช่อง GeoJSON</button>
                    </div>
                    <div id="zoneMap" style="height:380px; border-radius:10px; margin-top:8px; border:1px solid #e0e0e0;"></div>
                </div>

                <div class="form-actions">
                    <asp:Button ID="btnZoneSave" runat="server" Text="บันทึกโซน" CssClass="btn-success" OnClick="btnZoneSave_Click" CausesValidation="false" />
                    <asp:Button ID="btnZoneClear" runat="server" Text="ล้างฟอร์ม" CssClass="btn-secondary" OnClick="btnZoneClear_Click" CausesValidation="false" />
                </div>
            </div>
            <div class="grid-section">
                <asp:GridView ID="gvZones" runat="server" AutoGenerateColumns="False" CssClass="places-grid"
                    OnRowCommand="gvZones_RowCommand" ShowHeaderWhenEmpty="True">
                    <Columns>
                        <asp:BoundField DataField="Name" HeaderText="ชื่อโซน" />
                        <asp:TemplateField HeaderText="ขอบเขต">
                            <ItemTemplate>
                                <%# string.IsNullOrEmpty(Eval("Boundary_GeoJson") == null ? "" : Eval("Boundary_GeoJson").ToString())
                                    ? "<span style='color:#c62828'>ยังไม่ได้ใส่</span>" : "✔ มีแล้ว" %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="เริ่มต้น">
                            <ItemTemplate><%# Convert.ToBoolean(Eval("Is_Default")) ? "⭐" : "" %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="Status" HeaderText="สถานะ" />
                        <asp:TemplateField HeaderText="การจัดการ">
                            <ItemTemplate>
                                <asp:Button runat="server" Text="แก้ไข" CssClass="btn-warning btn-sm" CommandName="EditZone" CommandArgument='<%# Eval("ID") %>' CausesValidation="false" />
                                <asp:Button runat="server" Text="ลบ" CssClass="btn-danger btn-sm" CommandName="DeleteZone" CommandArgument='<%# Eval("ID") %>' CausesValidation="false"
                                    OnClientClick="return confirm('ลบโซนนี้? สถานที่ที่อยู่ในโซนจะถูกปลดออกจากโซน');" />
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                    <EmptyDataTemplate><div class="empty-data">📭 ยังไม่มีโซน (รันไมเกรชัน PHASE19_01 ก่อน)</div></EmptyDataTemplate>
                </asp:GridView>
            </div>
        </asp:Panel>
    </div>

    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/leaflet.min.css" />
    <script src="https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/leaflet.min.js"></script>
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

        // ══════════════════════════════════════════════════════════════════════
        // ตัวเลือกพิกัดบนแผนที่ (ฟอร์มสถานที่)
        // ══════════════════════════════════════════════════════════════════════
        var pickMap = null, pickMarker = null;

        function nbNum(v) { var n = parseFloat(v); return isNaN(n) ? null : n; }

        function nbSetLatLng(lat, lng) {
            document.getElementById('txtLat').value = lat.toFixed(6);
            document.getElementById('txtLng').value = lng.toFixed(6);
            if (!pickMap) return;
            if (pickMarker) pickMarker.setLatLng([lat, lng]);
            else pickMarker = L.marker([lat, lng], { draggable: true }).addTo(pickMap)
                    .on('dragend', function (e) {
                        var ll = e.target.getLatLng();
                        document.getElementById('txtLat').value = ll.lat.toFixed(6);
                        document.getElementById('txtLng').value = ll.lng.toFixed(6);
                    });
            pickMap.setView([lat, lng], Math.max(pickMap.getZoom(), 15));
        }

        // ดึงพิกัดจากลิงก์ Google Maps — รองรับรูปแบบที่พบบ่อย
        //   .../@13.1748,100.9306,17z          (ลิงก์จากแถบที่อยู่)
        //   ...!3d13.1748!4d100.9306           (ลิงก์แชร์สถานที่)
        //   ?q=13.1748,100.9306                (ลิงก์ปักหมุด)
        function nbExtractFromUrl() {
            var url = (document.getElementById('pasteMapUrl') || {}).value || '';
            if (!url.trim()) { alert('วางลิงก์ Google Maps ในช่องก่อน'); return; }
            var m = url.match(/!3d(-?\d+\.\d+)!4d(-?\d+\.\d+)/)
                 || url.match(/[?&]q=(-?\d+\.\d+),\s*(-?\d+\.\d+)/)
                 || url.match(/[?&]ll=(-?\d+\.\d+),\s*(-?\d+\.\d+)/)
                 || url.match(/@(-?\d+\.\d+),(-?\d+\.\d+)/);
            if (!m) {
                alert('อ่านพิกัดจากลิงก์นี้ไม่ได้\n\nวิธีที่ได้ผลแน่นอน: เปิด Google Maps → คลิกขวาตรงจุดที่ต้องการ '
                    + '→ กดที่ตัวเลขพิกัดเพื่อคัดลอก → วางในช่องละติจูด/ลองจิจูดตรง ๆ');
                return;
            }
            nbSetLatLng(parseFloat(m[1]), parseFloat(m[2]));
        }

        function nbInitPickMap() {
            var el = document.getElementById('pickMap');
            if (!el || typeof L === 'undefined') return;
            var lat = nbNum(document.getElementById('txtLat').value);
            var lng = nbNum(document.getElementById('txtLng').value);
            var hasPoint = lat !== null && lng !== null && !(lat === 0 && lng === 0);

            pickMap = L.map(el).setView(hasPoint ? [lat, lng] : [13.1748, 100.9306], hasPoint ? 15 : 12);
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
                { maxZoom: 19, attribution: '&copy; OpenStreetMap' }).addTo(pickMap);
            if (hasPoint) nbSetLatLng(lat, lng);
            pickMap.on('click', function (e) { nbSetLatLng(e.latlng.lat, e.latlng.lng); });
        }

        // ══════════════════════════════════════════════════════════════════════
        // วาด/ดูขอบเขตโซน
        // ══════════════════════════════════════════════════════════════════════
        var zoneMap = null, zoneLayer = null, drawPts = [], drawLine = null, drawing = false;

        function nbInitZoneMap() {
            var el = document.getElementById('zoneMap');
            if (!el || typeof L === 'undefined') return;
            var lat = nbNum(document.getElementById('txtZoneLat').value);
            var lng = nbNum(document.getElementById('txtZoneLng').value);
            zoneMap = L.map(el).setView([lat !== null ? lat : 13.1748, lng !== null ? lng : 100.9306], 11);
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
                { maxZoom: 19, attribution: '&copy; OpenStreetMap' }).addTo(zoneMap);
            zoneMap.on('click', function (e) {
                if (!drawing) return;
                drawPts.push([e.latlng.lat, e.latlng.lng]);
                nbDrawRefresh();
            });
            nbZonePreview();
        }

        function nbDrawRefresh() {
            if (drawLine) { zoneMap.removeLayer(drawLine); drawLine = null; }
            if (drawPts.length > 0)
                drawLine = L.polyline(drawPts, { color: '#E64A19', weight: 3, dashArray: '5,5' }).addTo(zoneMap);
        }

        function nbDrawStart() {
            if (!zoneMap) return;
            drawing = true; drawPts = []; nbDrawRefresh();
            if (zoneLayer) { zoneMap.removeLayer(zoneLayer); zoneLayer = null; }
            alert('คลิกบนแผนที่ไปตามแนวเขตที่ต้องการ (อย่างน้อย 3 จุด) แล้วกด "ปิดรูปแล้วบันทึกลงช่อง"');
        }

        function nbDrawUndo() { if (drawPts.length) { drawPts.pop(); nbDrawRefresh(); } }
        function nbDrawClear() { drawPts = []; drawing = false; nbDrawRefresh(); }

        function nbDrawFinish() {
            if (drawPts.length < 3) { alert('ต้องมีอย่างน้อย 3 จุด'); return; }
            // GeoJSON ใช้ลำดับ [lng, lat] และต้องปิดรูป (จุดแรก = จุดสุดท้าย)
            var ring = drawPts.map(function (p) { return [ +p[1].toFixed(6), +p[0].toFixed(6) ]; });
            ring.push(ring[0]);
            document.getElementById('txtZoneGeo').value =
                JSON.stringify({ type: 'Polygon', coordinates: [ring] });
            drawing = false;
            nbDrawClear();
            nbZonePreview();
        }

        function nbZonePreview() {
            if (!zoneMap) return;
            if (zoneLayer) { zoneMap.removeLayer(zoneLayer); zoneLayer = null; }
            var raw = (document.getElementById('txtZoneGeo').value || '').trim();
            if (!raw) return;
            try {
                zoneLayer = L.geoJSON(JSON.parse(raw), {
                    style: { color: '#00796B', weight: 2, fillColor: '#00b09b', fillOpacity: 0.15 }
                }).addTo(zoneMap);
                zoneMap.fitBounds(zoneLayer.getBounds(), { padding: [16, 16] });
            } catch (e) {
                alert('GeoJSON ไม่ถูกต้อง: ' + e.message);
            }
        }

        document.addEventListener('DOMContentLoaded', function () {
            nbInitPickMap();
            nbInitZoneMap();
        });
    </script>
</asp:Content>
