<%@ Page Title="จัดการข้อมูล Vendor / ผู้ขาย" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Vendor.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Vendor" MaintainScrollPositionOnPostback="true" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .vendor-management {
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
        }

        .form-control:focus {
            border-color: #667eea;
            outline: none;
        }

        /* Fix dropdown height for Thai text */
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

        select.form-control option {
            padding: 10px;
            line-height: 1.6;
        }

        .form-row {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
            gap: 15px;
        }

        .btn-primary {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            border: none;
            color: white;
            padding: 12px 30px;
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
            padding: 12px 30px;
            border-radius: 6px;
            font-weight: 500;
            cursor: pointer;
            font-size: 14px;
        }

        .btn-warning {
            background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
            border: none;
            color: white;
            padding: 12px 30px;
            border-radius: 6px;
            font-weight: 500;
            cursor: pointer;
            font-size: 14px;
        }

        .btn-secondary {
            background: #6c757d;
            border: none;
            color: white;
            padding: 12px 30px;
            border-radius: 6px;
            font-weight: 500;
            cursor: pointer;
            font-size: 14px;
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
            min-width: 800px;
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

        .hint {
            font-size: 12px;
            color: #666;
            margin-top: 3px;
        }

        .hint.success {
            color: #4caf50;
        }

        .hint.warning {
            color: #d32f2f;
        }

        .section-title {
            font-size: 16px;
            font-weight: 600;
            color: #667eea;
            margin: 20px 0 10px 0;
            padding-bottom: 5px;
            border-bottom: 2px solid #e0e0e0;
        }

        /* Mobile responsive */
        @media (max-width: 768px) {
            .vendor-management {
                padding: 10px;
            }

            .section-header h2 {
                font-size: 18px;
            }

            .search-controls {
                flex-direction: column;
            }

            .search-controls .form-control,
            .search-controls .btn-primary,
            .search-controls .btn-secondary,
            .search-controls .btn-success {
                width: 100%;
                max-width: 100% !important;
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

        @media (min-width: 769px) {
            .scroll-hint {
                display: none;
            }
        }
    </style>

    <div class="vendor-management">
        <div class="section-header">
            <h2>🏢 จัดการข้อมูล Vendor / ผู้ขาย</h2>
        </div>

        <!-- Search Section -->
        <asp:Panel ID="pnlSearch" runat="server" CssClass="search-section">
            <h4 style="margin-top: 0;">🔍 ค้นหา Vendor</h4>
            <div class="search-controls">
                <asp:TextBox ID="txtSearchTax" runat="server" CssClass="form-control" placeholder="เลขผู้เสียภาษี (13 หลัก)" style="max-width: 250px;" TextMode="Number" AutoPostBack="true" OnTextChanged="txtSearchTax_TextChanged"></asp:TextBox>
                <asp:TextBox ID="txtSearchName" runat="server" CssClass="form-control" placeholder="ชื่อผู้ขาย/บริษัท" style="max-width: 300px;"></asp:TextBox>
                <asp:DropDownList ID="ddlSearchGroup" runat="server" CssClass="form-control" style="max-width: 200px;">
                    <asp:ListItem Value="">ทุกประเภท</asp:ListItem>
                </asp:DropDownList>
                <asp:Button ID="btnSearch" runat="server" Text="ค้นหา" CssClass="btn-primary" OnClick="btnSearch_Click" />
                <asp:Button ID="btnClearSearch" runat="server" Text="ล้างค่า" CssClass="btn-secondary" OnClick="btnClearSearch_Click" />
                <asp:Button ID="btnNewVendor" runat="server" Text="+ เพิ่ม Vendor ใหม่" CssClass="btn-success" OnClick="btnNewVendor_Click" />
            </div>
        </asp:Panel>

        <!-- Vendor List -->
        <div class="scroll-hint">👆 เลื่อนซ้าย-ขวาเพื่อดูข้อมูลเพิ่มเติม</div>
        <asp:Panel ID="pnlVendorList" runat="server" CssClass="data-table">
            <asp:GridView ID="gvVendors" runat="server" AutoGenerateColumns="False"
                CssClass="table" GridLines="None" OnRowCommand="gvVendors_RowCommand">
                <Columns>
                    <asp:BoundField DataField="IDNumber" HeaderText="เลขผู้เสียภาษี" />
                    <asp:BoundField DataField="Name" HeaderText="ชื่อผู้ขาย/บริษัท" />
                    <asp:BoundField DataField="Branch_Number" HeaderText="สาขา" />
                    <asp:BoundField DataField="Customer_Type" HeaderText="ประเภท" />
                    <asp:BoundField DataField="Vendor_Group" HeaderText="กลุ่ม" />
                    <asp:BoundField DataField="Phone_Number" HeaderText="เบอร์โทร" />

                    <asp:TemplateField HeaderText="ที่อยู่">
                        <ItemTemplate>
                            <%# GetShortAddress(Eval("Address"), Eval("Province")) %>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="จัดการ">
                        <ItemTemplate>
                            <asp:Button ID="btnEdit" runat="server" Text="แก้ไข"
                                CssClass="btn-info" CommandName="EditVendor"
                                CommandArgument='<%# Eval("ID") %>' />
                            <asp:Button ID="btnDelete" runat="server" Text="ลบ"
                                CssClass="btn-danger" CommandName="DeleteVendor"
                                CommandArgument='<%# Eval("ID") %>'
                                OnClientClick="return confirm('ยืนยันการลบ Vendor นี้?');" />
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
                <EmptyDataTemplate>
                    <div style="text-align: center; padding: 40px; color: #999;">
                        ไม่พบข้อมูล Vendor<br/>
                        กรุณาคลิก "เพิ่ม Vendor ใหม่" เพื่อเพิ่มข้อมูล
                    </div>
                </EmptyDataTemplate>
            </asp:GridView>
        </asp:Panel>

        <!-- Vendor Form -->
        <asp:Panel ID="pnlVendorForm" runat="server" CssClass="form-card" Visible="false">
            <h3 style="margin-top: 0; color: #667eea;">
                <asp:Label ID="lblFormTitle" runat="server" Text="เพิ่ม Vendor ใหม่"></asp:Label>
            </h3>

            <div class="section-title">📋 ข้อมูลทั่วไป</div>

            <div class="form-row">
                <div class="form-group">
                    <label>เลขประจำตัวผู้เสียภาษี (13 หลัก)</label>
                    <asp:TextBox ID="txtTaxID" runat="server" CssClass="form-control" TextMode="Number" MaxLength="13" AutoPostBack="true" OnTextChanged="txtTaxID_TextChanged"></asp:TextBox>
                    <div class="hint success">✓ เว้นว่างได้ถ้าไม่มีเลขผู้เสียภาษี</div>
                </div>

                <div class="form-group">
                    <label class="required">ประเภท Vendor</label>
                    <asp:DropDownList ID="ddlVendorType" runat="server" CssClass="form-control"></asp:DropDownList>
                </div>
            </div>

            <div class="form-row">
                <div class="form-group">
                    <label class="required">ชื่อผู้เสียภาษี / ชื่อบริษัท</label>
                    <asp:TextBox ID="txtVendorName" runat="server" CssClass="form-control" required></asp:TextBox>
                    <div class="hint warning">* บังคับ - ใช้เป็น unique key หลัก</div>
                </div>

                <div class="form-group" id="branchNumberGroup">
                    <label id="lblBranch">เลขสาขา (00000 = สำนักงานใหญ่)</label>
                    <asp:TextBox ID="txtBranchNumber" runat="server" CssClass="form-control" TextMode="Number" MaxLength="5" Text="00000"></asp:TextBox>
                    <div class="hint success" id="hintBranch">✓ กรอก 00000 สำหรับสำนักงานใหญ่</div>
                </div>
            </div>

            <div class="form-row">
                <div class="form-group">
                    <label>เบอร์โทรศัพท์</label>
                    <asp:TextBox ID="txtPhone" runat="server" CssClass="form-control"></asp:TextBox>
                    <div class="hint success">✓ เว้นว่างได้</div>
                </div>

                <div class="form-group">
                    <label>กลุ่ม Vendor</label>
                    <asp:DropDownList ID="ddlVendorGroup" runat="server" CssClass="form-control"></asp:DropDownList>
                </div>
            </div>

            <div class="section-title">📍 ที่อยู่</div>

            <div class="form-row">
                <div class="form-group">
                    <label>บ้านเลขที่</label>
                    <asp:TextBox ID="txtAddress" runat="server" CssClass="form-control"></asp:TextBox>
                </div>

                <div class="form-group">
                    <label>อาคาร หมู่ที่ ซอย ถนน</label>
                    <asp:TextBox ID="txtAddress1" runat="server" CssClass="form-control"></asp:TextBox>
                </div>
            </div>

            <div class="form-row">
                <div class="form-group">
                    <label>รหัสไปรษณีย์</label>
                    <div style="display: flex; gap: 10px;">
                        <asp:TextBox ID="txtPostalCode" runat="server" CssClass="form-control" TextMode="Number" MaxLength="5"></asp:TextBox>
                        <asp:Button ID="btnSearchPostal" runat="server" Text="ค้นหา" CssClass="btn-info" OnClick="btnSearchPostal_Click" style="min-width: 100px;" />
                        <asp:Button ID="btnClearPostal" runat="server" Text="ยกเลิก" CssClass="btn-secondary" OnClick="btnClearPostal_Click" style="min-width: 100px;" />
                    </div>
                </div>
            </div>

            <div class="form-row">
                <div class="form-group">
                    <label>จังหวัด</label>
                    <asp:DropDownList ID="ddlProvince" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlProvince_SelectedIndexChanged"></asp:DropDownList>
                </div>

                <div class="form-group">
                    <label>อำเภอ/เขต</label>
                    <asp:DropDownList ID="ddlDistrict" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlDistrict_SelectedIndexChanged"></asp:DropDownList>
                </div>

                <div class="form-group">
                    <label>ตำบล/แขวง</label>
                    <asp:DropDownList ID="ddlSubDistrict" runat="server" CssClass="form-control"></asp:DropDownList>
                </div>
            </div>

            <div class="button-group">
                <asp:Button ID="btnSave" runat="server" Text="💾 บันทึก" CssClass="btn-success" OnClick="btnSave_Click" />
                <asp:Button ID="btnCancel" runat="server" Text="ยกเลิก" CssClass="btn-warning" OnClick="btnCancel_Click" />
            </div>

            <asp:HiddenField ID="hfVendorID" runat="server" />
        </asp:Panel>
    </div>

    <script type="text/javascript">
        // Toggle branch number field based on vendor type
        function toggleBranchNumber() {
            var ddl = document.getElementById('<%= ddlVendorType.ClientID %>');
            var txtBranch = document.getElementById('<%= txtBranchNumber.ClientID %>');
            var branchGroup = document.getElementById('branchNumberGroup');
            var lblBranch = document.getElementById('lblBranch');
            var hintBranch = document.getElementById('hintBranch');

            if (ddl && txtBranch) {
                // Type ID 1 = นิติบุคคล (Corporate), Type ID 2 = บุคคลธรรมดา (Individual)
                var isCorporate = ddl.value === '1';
                var isIndividual = ddl.value === '2';

                if (isIndividual) {
                    // บุคคลธรรมดา - disable and clear value
                    txtBranch.disabled = true;
                    txtBranch.value = '';
                    txtBranch.style.backgroundColor = '#f0f0f0';
                    lblBranch.innerHTML = 'เลขสาขา <small style="color:#888;">(ไม่จำเป็นสำหรับบุคคลธรรมดา)</small>';
                    hintBranch.innerHTML = '<span style="color:#888;">ไม่ต้องกรอกสำหรับบุคคลธรรมดา</span>';
                } else {
                    // นิติบุคคล หรือ ยังไม่เลือก - enable
                    txtBranch.disabled = false;
                    txtBranch.style.backgroundColor = '';
                    lblBranch.innerHTML = 'เลขสาขา (00000 = สำนักงานใหญ่)';
                    hintBranch.innerHTML = '✓ กรอก 00000 สำหรับสำนักงานใหญ่';
                    if (isCorporate) {
                        lblBranch.className = 'required';
                    } else {
                        lblBranch.className = '';
                    }
                }
            }
        }

        // Run on page load
        document.addEventListener('DOMContentLoaded', function() {
            toggleBranchNumber();
            var ddl = document.getElementById('<%= ddlVendorType.ClientID %>');
            if (ddl) {
                ddl.addEventListener('change', toggleBranchNumber);
            }
        });

        // Also run after postback
        if (typeof Sys !== 'undefined') {
            Sys.WebForms.PageRequestManager.getInstance().add_endRequest(function() {
                toggleBranchNumber();
            });
        }
    </script>
</asp:Content>
