<%@ Page Title="จัดการเกี่ยวกับเรา" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeFile="ManageAboutUs.aspx.cs" Inherits="Take_Time_BangPhra.Admin.ManageAboutUs" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .aboutus-management {
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

        .toolbar {
            display: flex;
            gap: 10px;
            align-items: center;
            flex-wrap: wrap;
            margin-bottom: 20px;
            background: white;
            padding: 15px 20px;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
        }

        .toolbar label {
            font-weight: 500;
            color: #333;
            font-size: 14px;
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
        }

        textarea.form-control {
            resize: vertical;
            font-family: inherit;
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
            background: linear-gradient(135deg, #f2994a 0%, #f2c94c 100%);
            border: none;
            color: white;
            padding: 10px 25px;
            border-radius: 6px;
            font-weight: 500;
            cursor: pointer;
            font-size: 14px;
        }

        .btn-seed:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(242, 153, 74, 0.4);
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

        .message-label {
            display: block;
            margin-bottom: 15px;
            padding: 10px 15px;
            border-radius: 6px;
            font-weight: 500;
        }

        .section-title {
            font-size: 16px;
            font-weight: 600;
            color: #667eea;
            margin: 20px 0 10px 0;
            padding-bottom: 5px;
            border-bottom: 2px solid #e0e0e0;
        }

        .scroll-hint {
            display: none;
        }

        @media (max-width: 768px) {
            .aboutus-management {
                padding: 10px;
            }

            .section-header h2 {
                font-size: 18px;
            }

            .toolbar {
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

    <div class="aboutus-management">
        <div class="section-header">
            <h2>&#x1F3E8; จัดการเกี่ยวกับเรา (About Us)</h2>
        </div>

        <asp:Label ID="lblMessage" runat="server" CssClass="message-label"></asp:Label>

        <!-- List Panel -->
        <asp:Panel ID="pnlList" runat="server">
            <div class="toolbar">
                <label>กรองประเภท:</label>
                <asp:DropDownList ID="ddlFilterSectionType" runat="server" CssClass="form-control"
                    AutoPostBack="true" OnSelectedIndexChanged="ddlFilterSectionType_SelectedIndexChanged"
                    style="max-width: 220px; display: inline-block;">
                </asp:DropDownList>

                <asp:Button ID="btnAdd" runat="server" Text="+ เพิ่มใหม่"
                    CssClass="btn-success" OnClick="btnAdd_Click" />

                <asp:Button ID="btnSeedData" runat="server" Text="&#x1F331; Seed ข้อมูลตัวอย่าง"
                    CssClass="btn-seed" OnClick="btnSeedData_Click"
                    OnClientClick="return confirm('ต้องการ Seed ข้อมูลตัวอย่างหรือไม่?');" />
            </div>

            <div class="scroll-hint">&#x1F446; เลื่อนซ้าย-ขวาเพื่อดูข้อมูลเพิ่มเติม</div>
            <div class="data-table">
                <asp:GridView ID="gvSections" runat="server" AutoGenerateColumns="False"
                    CssClass="table" GridLines="None" OnRowCommand="gvSections_RowCommand">
                    <Columns>
                        <asp:BoundField DataField="ID" HeaderText="ID" HeaderStyle-Width="60px" />

                        <asp:TemplateField HeaderText="ประเภท" HeaderStyle-Width="130px">
                            <ItemTemplate>
                                <%# GetSectionTypeText(Eval("Section_Type")) %>
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:BoundField DataField="Title" HeaderText="หัวข้อ" />

                        <asp:TemplateField HeaderText="เนื้อหา">
                            <ItemTemplate>
                                <%# TruncateText(Eval("Content"), 50) %>
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:BoundField DataField="Sort_Order" HeaderText="ลำดับ" HeaderStyle-Width="70px" />

                        <asp:TemplateField HeaderText="จัดการ" HeaderStyle-Width="150px">
                            <ItemTemplate>
                                <asp:Button ID="btnEdit" runat="server" Text="แก้ไข"
                                    CssClass="btn-info" CommandName="EditSection"
                                    CommandArgument='<%# Eval("ID") %>' />
                                <asp:Button ID="btnDelete" runat="server" Text="ลบ"
                                    CssClass="btn-danger" CommandName="DeleteSection"
                                    CommandArgument='<%# Eval("ID") %>'
                                    OnClientClick="return confirm('ยืนยันการลบข้อมูลนี้?');" />
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                    <EmptyDataTemplate>
                        <div style="text-align: center; padding: 40px; color: #999;">
                            ยังไม่มีข้อมูล<br />
                            กรุณาคลิก "เพิ่มใหม่" หรือ "Seed ข้อมูลตัวอย่าง" เพื่อเริ่มต้น
                        </div>
                    </EmptyDataTemplate>
                </asp:GridView>
            </div>
        </asp:Panel>

        <!-- Form Panel -->
        <asp:Panel ID="pnlForm" runat="server" CssClass="form-card" Visible="false">
            <h3 style="margin-top: 0; color: #667eea;">
                <asp:Label ID="lblFormTitle" runat="server" Text="เพิ่มข้อมูลใหม่"></asp:Label>
            </h3>

            <div class="section-title">&#x1F4CB; ข้อมูลทั่วไป</div>

            <div class="form-row">
                <div class="form-group">
                    <label class="required">ประเภท (Section Type)</label>
                    <asp:DropDownList ID="ddlSectionType" runat="server" CssClass="form-control">
                        <asp:ListItem Value="hero">Hero</asp:ListItem>
                        <asp:ListItem Value="story">เรื่องราว (Story)</asp:ListItem>
                        <asp:ListItem Value="values">คุณค่า (Values)</asp:ListItem>
                        <asp:ListItem Value="timeline">ไทม์ไลน์ (Timeline)</asp:ListItem>
                        <asp:ListItem Value="concept">แนวคิด (Concept)</asp:ListItem>
                        <asp:ListItem Value="team">ทีมงาน (Team)</asp:ListItem>
                        <asp:ListItem Value="quote">คำพูด (Quote)</asp:ListItem>
                    </asp:DropDownList>
                </div>

                <div class="form-group">
                    <label>ลำดับการแสดงผล (Sort Order)</label>
                    <asp:TextBox ID="txtSortOrder" runat="server" CssClass="form-control"
                        TextMode="Number" Text="0"></asp:TextBox>
                </div>
            </div>

            <div class="form-group">
                <label class="required">หัวข้อ (Title)</label>
                <asp:TextBox ID="txtTitle" runat="server" CssClass="form-control"
                    MaxLength="200"></asp:TextBox>
            </div>

            <div class="form-group">
                <label>เนื้อหา (Content)</label>
                <asp:TextBox ID="txtContent" runat="server" CssClass="form-control"
                    TextMode="MultiLine" Rows="4"></asp:TextBox>
            </div>

            <div class="form-group">
                <label>เนื้อหาเสริม (Sub Content)</label>
                <asp:TextBox ID="txtSubContent" runat="server" CssClass="form-control"
                    TextMode="MultiLine" Rows="2"></asp:TextBox>
            </div>

            <div class="section-title">&#x1F3A8; รูปแบบและสื่อ</div>

            <div class="form-row">
                <div class="form-group">
                    <label>ไอคอน (Icon / Emoji)</label>
                    <asp:TextBox ID="txtIcon" runat="server" CssClass="form-control"
                        MaxLength="50" placeholder="เช่น &#x1F33F; &#x1F3A8; &#x1F4A1;"></asp:TextBox>
                </div>

                <div class="form-group">
                    <label>URL รูปภาพ (Image URL)</label>
                    <asp:TextBox ID="txtImageUrl" runat="server" CssClass="form-control"
                        MaxLength="500" placeholder="เช่น /Images/about/story.jpg"></asp:TextBox>
                </div>
            </div>

            <div class="form-row">
                <div class="form-group">
                    <label>ปี (Year Text) - สำหรับ Timeline</label>
                    <asp:TextBox ID="txtYearText" runat="server" CssClass="form-control"
                        MaxLength="50" placeholder="เช่น 2019, 2020"></asp:TextBox>
                </div>
            </div>

            <div class="button-group">
                <asp:Button ID="btnSave" runat="server" Text="&#x1F4BE; บันทึก"
                    CssClass="btn-success" OnClick="btnSave_Click" />
                <asp:Button ID="btnCancel" runat="server" Text="ยกเลิก"
                    CssClass="btn-warning" OnClick="btnCancel_Click" />
            </div>

            <asp:HiddenField ID="hfEditID" runat="server" />
        </asp:Panel>
    </div>
</asp:Content>
