<%@ Page Title="จัดการสิ่งอำนวยความสะดวก" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeFile="ManageFacilities.aspx.cs" Inherits="Take_Time_BangPhra.Admin.ManageFacilities" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .facilities-management {
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
            display: flex;
            align-items: center;
            gap: 15px;
            flex-wrap: wrap;
        }

        .filter-section label {
            font-weight: 500;
            color: #333;
            font-size: 14px;
            white-space: nowrap;
        }

        .form-card {
            background: white;
            padding: 25px;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            margin-bottom: 20px;
        }

        .form-group {
            margin-bottom: 15px;
        }

        .form-group label {
            display: block;
            margin-bottom: 5px;
            font-weight: 500;
            color: #333;
            font-size: 14px;
        }

        .form-group label.required::after {
            content: " *";
            color: #d32f2f;
        }

        .form-control {
            width: 100%;
            padding: 10px 15px;
            border: 2px solid #e0e0e0;
            border-radius: 6px;
            font-size: 14px;
            transition: border-color 0.3s;
            box-sizing: border-box;
        }

        .form-control:focus {
            border-color: #667eea;
            outline: none;
        }

        select.form-control {
            height: auto;
            min-height: 44px;
            line-height: 1.6;
            padding: 8px 15px;
            -webkit-appearance: none;
            -moz-appearance: none;
            appearance: none;
            background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 12 12'%3E%3Cpath fill='%23333' d='M6 8L1 3h10z'/%3E%3C/svg%3E");
            background-repeat: no-repeat;
            background-position: right 12px center;
            padding-right: 35px;
        }

        .form-row {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
            gap: 15px;
        }

        .hint {
            font-size: 12px;
            color: #888;
            margin-top: 4px;
        }

        .btn-primary {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            border: none;
            color: white;
            padding: 10px 25px;
            border-radius: 6px;
            font-weight: 500;
            cursor: pointer;
            font-size: 14px;
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
            font-size: 14px;
        }

        .btn-success:hover {
            transform: translateY(-1px);
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
            font-size: 14px;
        }

        .btn-secondary {
            background: #6c757d;
            border: none;
            color: white;
            padding: 10px 25px;
            border-radius: 6px;
            font-weight: 500;
            cursor: pointer;
            font-size: 14px;
        }

        .btn-seed {
            background: linear-gradient(135deg, #f5af19 0%, #f12711 100%);
            border: none;
            color: white;
            padding: 10px 20px;
            border-radius: 6px;
            font-weight: 500;
            cursor: pointer;
            font-size: 13px;
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

        .btn-danger {
            background: linear-gradient(135deg, #fa709a 0%, #fee140 100%);
            border: none;
            color: white;
            padding: 8px 15px;
            border-radius: 6px;
            font-size: 13px;
            cursor: pointer;
        }

        .button-group {
            display: flex;
            gap: 10px;
            margin-top: 20px;
        }

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
            min-width: 900px;
        }

        .data-table th {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 12px;
            text-align: left;
            font-weight: 500;
            font-size: 14px;
        }

        .data-table td {
            padding: 12px;
            border-bottom: 1px solid #f0f0f0;
            font-size: 14px;
        }

        .data-table tr:hover {
            background-color: #f8f9fa;
        }

        .badge-facility {
            background: #667eea;
            color: white;
            padding: 4px 10px;
            border-radius: 12px;
            font-size: 12px;
            white-space: nowrap;
        }

        .badge-amenity {
            background: #11998e;
            color: white;
            padding: 4px 10px;
            border-radius: 12px;
            font-size: 12px;
            white-space: nowrap;
        }

        .badge-policy {
            background: #f5576c;
            color: white;
            padding: 4px 10px;
            border-radius: 12px;
            font-size: 12px;
            white-space: nowrap;
        }

        .badge-default {
            background: #6c757d;
            color: white;
            padding: 4px 10px;
            border-radius: 12px;
            font-size: 12px;
        }

        .icon-preview {
            display: inline-flex;
            align-items: center;
            gap: 6px;
            color: #555;
        }

        .icon-preview i {
            font-size: 16px;
            color: #667eea;
        }

        .section-title {
            font-size: 16px;
            font-weight: 600;
            color: #667eea;
            margin: 20px 0 10px 0;
            padding-bottom: 5px;
            border-bottom: 2px solid #e0e0e0;
        }

        .alert {
            padding: 12px 20px;
            border-radius: 6px;
            margin-bottom: 15px;
            font-size: 14px;
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

        .scroll-hint {
            display: none;
        }

        @media (max-width: 768px) {
            .facilities-management {
                padding: 10px;
            }

            .section-header {
                flex-direction: column;
                text-align: center;
            }

            .section-header h2 {
                font-size: 18px;
            }

            .header-actions {
                justify-content: center;
            }

            .filter-section {
                flex-direction: column;
                align-items: stretch;
            }

            .form-row {
                grid-template-columns: 1fr;
            }

            .button-group {
                flex-direction: column;
            }

            .button-group button,
            .button-group input[type="submit"] {
                width: 100%;
            }

            .scroll-hint {
                display: block;
                text-align: center;
                padding: 8px;
                background: #fff3cd;
                color: #856404;
                font-size: 13px;
                border-radius: 4px 4px 0 0;
            }
        }
    </style>

    <div class="facilities-management">

        <!-- Message -->
        <asp:Label ID="lblMessage" runat="server" Visible="false"></asp:Label>

        <!-- List Panel -->
        <asp:Panel ID="pnlList" runat="server">

            <div class="section-header">
                <h2><i class="fa fa-concierge-bell"></i> จัดการสิ่งอำนวยความสะดวก</h2>
                <div class="header-actions">
                    <asp:Button ID="btnAdd" runat="server" Text="+ เพิ่มใหม่" CssClass="btn-success" OnClick="btnAdd_Click" />
                    <asp:Button ID="btnSeed" runat="server" Text="Seed ข้อมูลตัวอย่าง" CssClass="btn-seed"
                        OnClick="btnSeed_Click" OnClientClick="return confirm('ต้องการ Seed ข้อมูลตัวอย่าง (Facilities, Amenities, Policies)?');" />
                </div>
            </div>

            <!-- Category Filter -->
            <div class="filter-section">
                <label><i class="fa fa-filter"></i> กรองตามหมวดหมู่:</label>
                <asp:DropDownList ID="ddlFilterCategory" runat="server" CssClass="form-control"
                    AutoPostBack="true" OnSelectedIndexChanged="ddlFilterCategory_SelectedIndexChanged"
                    style="max-width: 300px;">
                    <asp:ListItem Value="">-- ทั้งหมด --</asp:ListItem>
                    <asp:ListItem Value="facility">สิ่งอำนวยความสะดวก (Facility)</asp:ListItem>
                    <asp:ListItem Value="amenity">สิ่งอำนวยความสะดวกในห้อง (Amenity)</asp:ListItem>
                    <asp:ListItem Value="policy">กฎระเบียบ (Policy)</asp:ListItem>
                </asp:DropDownList>
                <asp:Button ID="btnClearFilter" runat="server" Text="ล้างตัวกรอง" CssClass="btn-secondary" OnClick="btnClearFilter_Click" />
            </div>

            <!-- Data Grid -->
            <div class="scroll-hint"><i class="fa fa-arrows-alt-h"></i> เลื่อนซ้าย-ขวาเพื่อดูข้อมูลเพิ่มเติม</div>
            <div class="data-table">
                <asp:GridView ID="gvFacilities" runat="server" AutoGenerateColumns="False"
                    CssClass="table" GridLines="None" OnRowCommand="gvFacilities_RowCommand">
                    <Columns>
                        <asp:BoundField DataField="ID" HeaderText="ID" ItemStyle-Width="60px" />

                        <asp:TemplateField HeaderText="หมวดหมู่">
                            <ItemTemplate>
                                <span class='<%# GetCategoryBadgeClass(Eval("Category")) %>'>
                                    <%# GetCategoryText(Eval("Category")) %>
                                </span>
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:BoundField DataField="Name" HeaderText="ชื่อ" />

                        <asp:TemplateField HeaderText="Icon">
                            <ItemTemplate>
                                <span class="icon-preview">
                                    <i class='fa <%# Eval("Icon") %>'></i>
                                    <small><%# Eval("Icon") %></small>
                                </span>
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:BoundField DataField="Hours" HeaderText="เวลาเปิด-ปิด" />
                        <asp:BoundField DataField="Sort_Order" HeaderText="ลำดับ" ItemStyle-Width="70px" ItemStyle-HorizontalAlign="Center" />

                        <asp:TemplateField HeaderText="จัดการ">
                            <ItemTemplate>
                                <asp:Button ID="btnEdit" runat="server" Text="แก้ไข"
                                    CssClass="btn-info" CommandName="EditItem"
                                    CommandArgument='<%# Eval("ID") %>' />
                                <asp:Button ID="btnDelete" runat="server" Text="ลบ"
                                    CssClass="btn-danger" CommandName="DeleteItem"
                                    CommandArgument='<%# Eval("ID") %>'
                                    OnClientClick="return confirm('ยืนยันการลบรายการนี้?');" />
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                    <EmptyDataTemplate>
                        <div style="text-align: center; padding: 40px; color: #999;">
                            <i class="fa fa-inbox" style="font-size: 48px; display: block; margin-bottom: 15px;"></i>
                            ไม่พบข้อมูล<br />
                            กรุณาคลิก "เพิ่มใหม่" หรือ "Seed ข้อมูลตัวอย่าง" เพื่อเริ่มต้น
                        </div>
                    </EmptyDataTemplate>
                </asp:GridView>
            </div>

        </asp:Panel>

        <!-- Form Panel -->
        <asp:Panel ID="pnlForm" runat="server" CssClass="form-card" Visible="false">
            <h3 style="margin-top: 0; color: #667eea;">
                <i class="fa fa-edit"></i>
                <asp:Label ID="lblFormTitle" runat="server" Text="เพิ่มรายการใหม่"></asp:Label>
            </h3>

            <div class="section-title">ข้อมูลหลัก</div>

            <div class="form-row">
                <div class="form-group">
                    <label class="required">หมวดหมู่</label>
                    <asp:DropDownList ID="ddlCategory" runat="server" CssClass="form-control">
                        <asp:ListItem Value="">-- เลือกหมวดหมู่ --</asp:ListItem>
                        <asp:ListItem Value="facility">สิ่งอำนวยความสะดวก (Facility)</asp:ListItem>
                        <asp:ListItem Value="amenity">สิ่งอำนวยความสะดวกในห้อง (Amenity)</asp:ListItem>
                        <asp:ListItem Value="policy">กฎระเบียบ (Policy)</asp:ListItem>
                    </asp:DropDownList>
                </div>

                <div class="form-group">
                    <label class="required">ชื่อ</label>
                    <asp:TextBox ID="txtName" runat="server" CssClass="form-control" placeholder="เช่น Swimming Pool, Air Conditioning"></asp:TextBox>
                </div>
            </div>

            <div class="form-group">
                <label>คำอธิบาย</label>
                <asp:TextBox ID="txtDescription" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="3" placeholder="รายละเอียดเพิ่มเติม"></asp:TextBox>
            </div>

            <div class="section-title">การแสดงผล</div>

            <div class="form-row">
                <div class="form-group">
                    <label>Icon (FontAwesome Class)</label>
                    <asp:TextBox ID="txtIcon" runat="server" CssClass="form-control" placeholder="เช่น fa-swimming-pool, fa-wifi"></asp:TextBox>
                    <div class="hint">ใช้ชื่อ class จาก FontAwesome เช่น fa-swimming-pool, fa-utensils, fa-spa</div>
                </div>

                <div class="form-group">
                    <label>CSS Class</label>
                    <asp:TextBox ID="txtCssClass" runat="server" CssClass="form-control" placeholder="เช่น pool, restaurant, spa"></asp:TextBox>
                    <div class="hint">ใช้สำหรับจัดสไตล์การ์ด เช่น pool, restaurant, gym</div>
                </div>
            </div>

            <div class="form-row">
                <div class="form-group">
                    <label>เวลาเปิด-ปิด</label>
                    <asp:TextBox ID="txtHours" runat="server" CssClass="form-control" placeholder="เช่น 06:00 - 20:00, 24hr"></asp:TextBox>
                </div>

                <div class="form-group">
                    <label>ข้อมูลเพิ่มเติม</label>
                    <asp:TextBox ID="txtExtraInfo" runat="server" CssClass="form-control" placeholder="เช่น 25m x 12m, 80 seats, Ext. 103"></asp:TextBox>
                    <div class="hint">ความจุ, ขนาด, เบอร์ต่อ ฯลฯ</div>
                </div>
            </div>

            <div class="form-row">
                <div class="form-group">
                    <label>ลำดับการแสดง (Sort Order)</label>
                    <asp:TextBox ID="txtSortOrder" runat="server" CssClass="form-control" TextMode="Number" Text="0" placeholder="0"></asp:TextBox>
                    <div class="hint">ตัวเลขน้อยแสดงก่อน</div>
                </div>
            </div>

            <div class="button-group">
                <asp:Button ID="btnSave" runat="server" Text="บันทึก" CssClass="btn-success" OnClick="btnSave_Click" />
                <asp:Button ID="btnCancel" runat="server" Text="ยกเลิก" CssClass="btn-warning" OnClick="btnCancel_Click" CausesValidation="false" />
            </div>

            <asp:HiddenField ID="hfEditID" runat="server" />
        </asp:Panel>

    </div>
</asp:Content>
