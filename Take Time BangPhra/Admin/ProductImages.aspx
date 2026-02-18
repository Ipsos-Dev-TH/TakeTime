<%@ Page Title="จัดการรูปภาพสินค้า" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ProductImages.aspx.cs" Inherits="Take_Time_BangPhra.Admin.ProductImages" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .product-images-container {
            max-width: 1400px;
            margin: 20px auto;
            padding: 20px;
        }

        .tabs {
            display: flex;
            gap: 10px;
            margin-bottom: 20px;
            border-bottom: 2px solid #ddd;
        }

        .tab {
            padding: 12px 24px;
            background: #f8f9fa;
            border: none;
            cursor: pointer;
            font-weight: bold;
            color: #7f8c8d;
            border-radius: 8px 8px 0 0;
            transition: all 0.3s;
        }

        .tab.active {
            background: #3498db;
            color: white;
        }

        .tab:hover {
            background: #2980b9;
            color: white;
        }

        .section-card {
            background: white;
            border-radius: 8px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
            padding: 25px;
            margin-bottom: 20px;
        }

        .card-header {
            font-size: 20px;
            font-weight: bold;
            color: #2c3e50;
            margin-bottom: 20px;
            padding-bottom: 10px;
            border-bottom: 2px solid #3498db;
        }

        .product-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
            gap: 20px;
            margin-top: 20px;
        }

        .product-card {
            border: 1px solid #ddd;
            border-radius: 8px;
            overflow: hidden;
            transition: transform 0.3s, box-shadow 0.3s;
        }

        .product-card:hover {
            transform: translateY(-5px);
            box-shadow: 0 4px 12px rgba(0,0,0,0.15);
        }

        .product-card-header {
            background: #34495e;
            color: white;
            padding: 15px;
            font-weight: bold;
        }

        .product-card-body {
            padding: 15px;
        }

        .product-info {
            margin-bottom: 10px;
        }

        .product-label {
            font-weight: bold;
            color: #7f8c8d;
        }

        .product-value {
            color: #2c3e50;
        }

        .image-gallery {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(100px, 1fr));
            gap: 10px;
            margin-top: 10px;
        }

        .image-thumbnail {
            position: relative;
            width: 100%;
            padding-bottom: 100%;
            overflow: hidden;
            border-radius: 4px;
            border: 2px solid #ddd;
        }

        .image-thumbnail img {
            position: absolute;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            object-fit: cover;
        }

        .image-thumbnail.main {
            border-color: #27ae60;
            border-width: 3px;
        }

        .main-badge {
            position: absolute;
            top: 5px;
            right: 5px;
            background: #27ae60;
            color: white;
            padding: 2px 8px;
            border-radius: 3px;
            font-size: 10px;
            font-weight: bold;
        }

        .btn-manage {
            background-color: #3498db;
            color: white;
            padding: 8px 16px;
            border: none;
            border-radius: 4px;
            cursor: pointer;
            margin-top: 10px;
            width: 100%;
        }

        .btn-manage:hover {
            background-color: #2980b9;
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
            line-height: 1.6;
        }

        select.form-control {
            height: auto;
            min-height: 44px;
            padding-top: 8px;
            padding-bottom: 8px;
        }

        select.form-control option {
            padding: 8px 10px;
            line-height: 1.6;
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

        .modal {
            display: none;
            position: fixed;
            z-index: 1000;
            left: 0;
            top: 0;
            width: 100%;
            height: 100%;
            overflow: auto;
            background-color: rgba(0,0,0,0.7);
        }

        .modal-content {
            background-color: #fefefe;
            margin: 2% auto;
            padding: 20px;
            border: 1px solid #888;
            width: 90%;
            max-width: 1000px;
            border-radius: 8px;
            max-height: 90vh;
            overflow-y: auto;
        }

        .modal-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 20px;
            padding-bottom: 15px;
            border-bottom: 2px solid #3498db;
        }

        .close {
            color: #aaa;
            font-size: 28px;
            font-weight: bold;
            cursor: pointer;
        }

        .close:hover {
            color: #000;
        }

        .image-manager {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
            gap: 15px;
        }

        .image-item {
            border: 1px solid #ddd;
            border-radius: 8px;
            padding: 10px;
            text-align: center;
        }

        .image-preview {
            width: 100%;
            height: 150px;
            object-fit: cover;
            border-radius: 4px;
            margin-bottom: 10px;
        }

        .image-actions {
            display: flex;
            gap: 5px;
            justify-content: center;
        }

        .btn-sm {
            padding: 5px 10px;
            font-size: 12px;
            border: none;
            border-radius: 3px;
            cursor: pointer;
        }

        .btn-primary { background-color: #3498db; color: white; }
        .btn-success { background-color: #27ae60; color: white; }
        .btn-danger { background-color: #e74c3c; color: white; }
    </style>

    <div class="product-images-container">
        <h2 style="color: #2c3e50; margin-bottom: 30px;">
            <i class="fa fa-images"></i> จัดการรูปภาพสินค้า
        </h2>

        <!-- Tabs -->
        <div class="tabs">
            <button type="button" class="tab" onclick="switchTab('accommodation')">
                <i class="fa fa-home"></i> ที่พัก
            </button>
            <button type="button" class="tab" onclick="switchTab('items')">
                <i class="fa fa-shopping-bag"></i> อุปกรณ์/บริการ
            </button>
            <button type="button" class="tab active" onclick="switchTab('products')">
                <i class="fa fa-utensils"></i> สินค้า (Room Service)
            </button>
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
                    <label class="form-label">ประเภทสินค้า <span style="color: red;">*</span></label>
                    <asp:DropDownList ID="ddlProductType" runat="server" CssClass="form-control"
                        AutoPostBack="true" OnSelectedIndexChanged="ddlProductType_SelectedIndexChanged">
                        <asp:ListItem Value="">-- เลือกประเภท --</asp:ListItem>
                        <asp:ListItem Value="PRODUCT">สินค้า (Room Service)</asp:ListItem>
                        <asp:ListItem Value="ACCOMMODATION">ที่พัก</asp:ListItem>
                        <asp:ListItem Value="ITEM">อุปกรณ์/บริการ</asp:ListItem>
                    </asp:DropDownList>
                </div>

                <div class="form-group">
                    <label class="form-label">เลือกสินค้า <span style="color: red;">*</span></label>
                    <asp:DropDownList ID="ddlProduct" runat="server" CssClass="form-control">
                        <asp:ListItem Value="">-- เลือกสินค้า --</asp:ListItem>
                    </asp:DropDownList>
                </div>

                <div class="form-group">
                    <label class="form-label">อัพโหลดรูปภาพ (JPG, PNG เท่านั้น, ไม่เกิน 10MB) <span style="color: red;">*</span></label>
                    <asp:FileUpload ID="fuImage" runat="server" CssClass="form-control" />
                </div>

                <div class="form-group">
                    <label class="form-label">คำอธิบายรูป</label>
                    <asp:TextBox ID="txtCaption" runat="server" CssClass="form-control"
                        placeholder="ระบุคำอธิบายรูปภาพ (ถ้ามี)"></asp:TextBox>
                </div>

                <div class="form-group">
                    <asp:CheckBox ID="chkIsMainImage" runat="server" Text=" ตั้งเป็นรูปหลัก" />
                    <small style="color: #7f8c8d; display: block; margin-top: 5px;">
                        หมายเหตุ: รูปหลักจะถูกแสดงเป็นหลักในหน้าแสดงสินค้า
                    </small>
                </div>

                <asp:Button ID="btnUpload" runat="server" Text="อัพโหลดรูปภาพ"
                    CssClass="btn-upload" OnClick="btnUpload_Click" />
            </div>
        </div>

        <!-- Product List -->
        <div class="section-card">
            <div class="card-header">
                <i class="fa fa-list"></i> รายการสินค้า
            </div>

            <asp:UpdatePanel ID="upProducts" runat="server">
                <ContentTemplate>
                    <div id="productsList" class="product-grid">
                        <asp:Repeater ID="rptProducts" runat="server">
                            <ItemTemplate>
                                <div class="product-card">
                                    <div class="product-card-header" style="background: #27ae60;">
                                        <%# Eval("ProductName") %>
                                    </div>
                                    <div class="product-card-body">
                                        <div class="product-info">
                                            <span class="product-label">ราคา:</span>
                                            <span class="product-value"><%# Convert.ToDecimal(Eval("Price")).ToString("N2") %> บาท</span>
                                        </div>
                                        <div class="product-info">
                                            <span class="product-label">สต็อก:</span>
                                            <span class="product-value"><%# Eval("Quantity") %> ชิ้น</span>
                                        </div>
                                        <div class="product-info">
                                            <span class="product-label">จำนวนรูป:</span>
                                            <span class="product-value"><%# Eval("TotalImages") %> รูป</span>
                                        </div>
                                        <button type="button" class="btn-manage"
                                            onclick='openImageManager("PRODUCT", <%# Eval("ProductID") %>, "<%# Eval("ProductName") %>")'>
                                            <i class="fa fa-edit"></i> จัดการรูปภาพ
                                        </button>
                                    </div>
                                </div>
                            </ItemTemplate>
                        </asp:Repeater>
                    </div>

                    <div id="accommodationList" class="product-grid" style="display: none;">
                        <asp:Repeater ID="rptAccommodations" runat="server">
                            <ItemTemplate>
                                <div class="product-card">
                                    <div class="product-card-header">
                                        <%# Eval("AccomName") %>
                                    </div>
                                    <div class="product-card-body">
                                        <div class="product-info">
                                            <span class="product-label">ราคา:</span>
                                            <span class="product-value"><%# Convert.ToDecimal(Eval("Price")).ToString("N2") %> บาท</span>
                                        </div>
                                        <div class="product-info">
                                            <span class="product-label">จำนวนรูป:</span>
                                            <span class="product-value"><%# Eval("TotalImages") %> รูป</span>
                                        </div>
                                        <button type="button" class="btn-manage"
                                            onclick='openImageManager("ACCOMMODATION", <%# Eval("AccommodationID") %>, "<%# Eval("AccomName") %>")'>
                                            <i class="fa fa-edit"></i> จัดการรูปภาพ
                                        </button>
                                    </div>
                                </div>
                            </ItemTemplate>
                        </asp:Repeater>
                    </div>

                    <div id="itemsList" class="product-grid" style="display: none;">
                        <asp:Repeater ID="rptItems" runat="server">
                            <ItemTemplate>
                                <div class="product-card">
                                    <div class="product-card-header">
                                        <%# Eval("ItemName") %>
                                    </div>
                                    <div class="product-card-body">
                                        <div class="product-info">
                                            <span class="product-label">ราคา:</span>
                                            <span class="product-value"><%# Convert.ToDecimal(Eval("Price")).ToString("N2") %> บาท</span>
                                        </div>
                                        <div class="product-info">
                                            <span class="product-label">จำนวนรูป:</span>
                                            <span class="product-value"><%# Eval("TotalImages") %> รูป</span>
                                        </div>
                                        <button type="button" class="btn-manage"
                                            onclick='openImageManager("ITEM", <%# Eval("ItemID") %>, "<%# Eval("ItemName") %>")'>
                                            <i class="fa fa-edit"></i> จัดการรูปภาพ
                                        </button>
                                    </div>
                                </div>
                            </ItemTemplate>
                        </asp:Repeater>
                    </div>
                </ContentTemplate>
            </asp:UpdatePanel>
        </div>
    </div>

    <!-- Image Manager Modal -->
    <div id="imageModal" class="modal">
        <div class="modal-content">
            <div class="modal-header">
                <h3 id="modalTitle">จัดการรูปภาพ</h3>
                <span class="close" onclick="closeImageModal()">&times;</span>
            </div>
            <div id="modalBody">
                <asp:HiddenField ID="hfProductType" runat="server" />
                <asp:HiddenField ID="hfProductID" runat="server" />
                <!-- Images will be loaded here via JavaScript -->
            </div>
        </div>
    </div>

    <script type="text/javascript">
        function switchTab(type) {
            // Update tab buttons
            var tabs = document.querySelectorAll('.tab');
            tabs.forEach(tab => tab.classList.remove('active'));
            event.target.classList.add('active');

            // Hide all lists
            document.getElementById('productsList').style.display = 'none';
            document.getElementById('accommodationList').style.display = 'none';
            document.getElementById('itemsList').style.display = 'none';

            // Show selected list
            if (type === 'products') {
                document.getElementById('productsList').style.display = 'grid';
            } else if (type === 'accommodation') {
                document.getElementById('accommodationList').style.display = 'grid';
            } else if (type === 'items') {
                document.getElementById('itemsList').style.display = 'grid';
            }
        }

        function openImageManager(productType, productId, productName) {
            document.getElementById('modalTitle').innerText = 'จัดการรูปภาพ: ' + productName;
            document.getElementById('<%= hfProductType.ClientID %>').value = productType;
            document.getElementById('<%= hfProductID.ClientID %>').value = productId;

            // Load images via AJAX or redirect
            window.location.href = 'ManageProductImages.aspx?type=' + productType + '&id=' + productId;
        }

        function closeImageModal() {
            document.getElementById('imageModal').style.display = 'none';
        }

        window.onclick = function (event) {
            var modal = document.getElementById('imageModal');
            if (event.target == modal) {
                closeImageModal();
            }
        }
    </script>
</asp:Content>
