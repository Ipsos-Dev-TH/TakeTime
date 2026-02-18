<%@ Page Title="จัดการรูปภาพ" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeFile="ManageProductImages.aspx.cs" Inherits="Take_Time_BangPhra.Admin.ManageProductImagesPage" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .manage-container {
            max-width: 1200px;
            margin: 20px auto;
            padding: 20px;
        }

        .page-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 25px;
            padding-bottom: 15px;
            border-bottom: 2px solid #3498db;
        }

        .page-title {
            font-size: 22px;
            font-weight: bold;
            color: #2c3e50;
        }

        .btn-back {
            background-color: #95a5a6;
            color: white;
            padding: 10px 20px;
            border: none;
            border-radius: 4px;
            cursor: pointer;
            text-decoration: none;
            font-size: 14px;
        }

        .btn-back:hover {
            background-color: #7f8c8d;
            color: white;
            text-decoration: none;
        }

        .section-card {
            background: white;
            border-radius: 8px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
            padding: 25px;
            margin-bottom: 20px;
        }

        .card-header {
            font-size: 18px;
            font-weight: bold;
            color: #2c3e50;
            margin-bottom: 20px;
            padding-bottom: 10px;
            border-bottom: 2px solid #3498db;
        }

        .upload-section {
            background: #f8f9fa;
            padding: 20px;
            border-radius: 8px;
            margin-bottom: 20px;
        }

        .form-group {
            margin-bottom: 15px;
        }

        .form-label {
            display: block;
            font-weight: bold;
            margin-bottom: 5px;
            color: #2c3e50;
        }

        .form-control {
            width: 100%;
            padding: 10px;
            border: 1px solid #ddd;
            border-radius: 4px;
            min-height: 44px;
        }

        .btn-upload {
            background-color: #27ae60;
            color: white;
            padding: 12px 30px;
            border: none;
            border-radius: 4px;
            font-weight: bold;
            cursor: pointer;
        }

        .btn-upload:hover {
            background-color: #229954;
        }

        .image-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
            gap: 20px;
            margin-top: 15px;
        }

        .image-card {
            border: 2px solid #ddd;
            border-radius: 8px;
            overflow: hidden;
            text-align: center;
            transition: box-shadow 0.3s;
        }

        .image-card:hover {
            box-shadow: 0 4px 12px rgba(0,0,0,0.15);
        }

        .image-card.main-image {
            border-color: #27ae60;
            border-width: 3px;
        }

        .image-preview {
            width: 100%;
            height: 180px;
            object-fit: cover;
            display: block;
        }

        .image-info {
            padding: 12px;
            background: #f8f9fa;
        }

        .image-caption {
            font-size: 13px;
            color: #555;
            margin-bottom: 8px;
            min-height: 20px;
        }

        .main-badge {
            display: inline-block;
            background: #27ae60;
            color: white;
            padding: 3px 10px;
            border-radius: 3px;
            font-size: 11px;
            font-weight: bold;
            margin-bottom: 8px;
        }

        .image-actions {
            display: flex;
            gap: 5px;
            justify-content: center;
        }

        .btn-sm {
            padding: 6px 12px;
            font-size: 12px;
            border: none;
            border-radius: 3px;
            cursor: pointer;
            color: white;
        }

        .btn-primary { background-color: #3498db; }
        .btn-primary:hover { background-color: #2980b9; }
        .btn-danger { background-color: #e74c3c; }
        .btn-danger:hover { background-color: #c0392b; }

        .alert {
            padding: 15px;
            border-radius: 4px;
            margin-bottom: 20px;
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

        .no-images {
            text-align: center;
            padding: 40px;
            color: #95a5a6;
            font-size: 16px;
        }
    </style>

    <div class="manage-container">
        <div class="page-header">
            <span class="page-title">
                <i class="fa fa-images"></i>
                จัดการรูปภาพ:
                <asp:Label ID="lblProductName" runat="server" ForeColor="#3498db"></asp:Label>
            </span>
            <a href="ProductImages.aspx" class="btn-back">
                <i class="fa fa-arrow-left"></i> กลับ
            </a>
        </div>

        <!-- Alert Messages -->
        <asp:Panel ID="pnlSuccess" runat="server" CssClass="alert alert-success" Visible="false">
            <asp:Label ID="lblSuccess" runat="server"></asp:Label>
        </asp:Panel>

        <asp:Panel ID="pnlError" runat="server" CssClass="alert alert-danger" Visible="false">
            <asp:Label ID="lblError" runat="server"></asp:Label>
        </asp:Panel>

        <!-- Upload Section -->
        <div class="section-card">
            <div class="card-header">
                <i class="fa fa-cloud-upload"></i> อัพโหลดรูปภาพใหม่
            </div>

            <div class="upload-section">
                <div class="form-group">
                    <label class="form-label">เลือกไฟล์รูปภาพ (JPG, PNG ไม่เกิน 10MB) <span style="color: red;">*</span></label>
                    <asp:FileUpload ID="fuImage" runat="server" CssClass="form-control" />
                </div>

                <div class="form-group">
                    <label class="form-label">คำอธิบายรูป</label>
                    <asp:TextBox ID="txtCaption" runat="server" CssClass="form-control"
                        placeholder="ระบุคำอธิบายรูปภาพ (ถ้ามี)"></asp:TextBox>
                </div>

                <div class="form-group">
                    <asp:CheckBox ID="chkIsMainImage" runat="server" Text=" ตั้งเป็นรูปหลัก" />
                </div>

                <asp:Button ID="btnUpload" runat="server" Text="อัพโหลดรูปภาพ"
                    CssClass="btn-upload" OnClick="btnUpload_Click" />
            </div>
        </div>

        <!-- Image List -->
        <div class="section-card">
            <div class="card-header">
                <i class="fa fa-th"></i> รูปภาพทั้งหมด
                (<asp:Label ID="lblImageCount" runat="server" Text="0"></asp:Label> รูป)
            </div>

            <asp:Panel ID="pnlNoImages" runat="server" CssClass="no-images">
                <i class="fa fa-image" style="font-size: 48px; display: block; margin-bottom: 10px;"></i>
                ยังไม่มีรูปภาพ กรุณาอัพโหลดรูปภาพ
            </asp:Panel>

            <div class="image-grid">
                <asp:Repeater ID="rptImages" runat="server">
                    <ItemTemplate>
                        <div class='<%# Convert.ToBoolean(Eval("IsMainImage")) ? "image-card main-image" : "image-card" %>'>
                            <img src='<%# ResolveUrl(Eval("ImageURL").ToString()) %>'
                                alt='<%# Eval("Caption") %>'
                                class="image-preview"
                                onerror="this.src='../Images/placeholder.jpg'" />
                            <div class="image-info">
                                <%# Convert.ToBoolean(Eval("IsMainImage")) ? "<span class='main-badge'>รูปหลัก</span><br/>" : "" %>
                                <div class="image-caption"><%# Eval("Caption") %></div>
                                <div class="image-actions">
                                    <asp:LinkButton ID="btnSetMain" runat="server"
                                        CssClass="btn-sm btn-primary"
                                        CommandName="SetMain"
                                        CommandArgument='<%# Eval("ID") %>'
                                        OnCommand="btnAction_Command"
                                        Visible='<%# !Convert.ToBoolean(Eval("IsMainImage")) %>'>
                                        <i class="fa fa-star"></i> ตั้งเป็นหลัก
                                    </asp:LinkButton>
                                    <asp:LinkButton ID="btnDelete" runat="server"
                                        CssClass="btn-sm btn-danger"
                                        CommandName="DeleteImage"
                                        CommandArgument='<%# Eval("ID") %>'
                                        OnCommand="btnAction_Command"
                                        OnClientClick="return confirm('ต้องการลบรูปภาพนี้?');">
                                        <i class="fa fa-trash"></i> ลบ
                                    </asp:LinkButton>
                                </div>
                            </div>
                        </div>
                    </ItemTemplate>
                </asp:Repeater>
            </div>
        </div>
    </div>
</asp:Content>
