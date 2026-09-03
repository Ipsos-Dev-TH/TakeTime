<%@ Page Title="Room Service" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RoomService.aspx.cs" Inherits="Take_Time_BangPhra.Guest.RoomService" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        * {
            box-sizing: border-box;
        }

        .rs-container {
            max-width: 1200px;
            margin: 0 auto;
            padding: 15px;
            padding-bottom: 90px;
            overflow-x: hidden;
        }

        .rs-header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 20px;
            border-radius: 12px;
            margin-bottom: 20px;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .rs-header h2 {
            margin: 0;
            font-size: 20px;
            font-weight: 700;
        }

        .btn-back {
            background: rgba(255,255,255,0.2);
            color: white;
            border: none;
            padding: 8px 16px;
            border-radius: 20px;
            text-decoration: none;
            font-weight: 500;
            font-size: 13px;
            transition: all 0.3s ease;
        }

        .btn-back:hover {
            background: rgba(255,255,255,0.3);
            color: white;
            text-decoration: none;
        }

        .tabs {
            display: flex;
            gap: 0;
            margin-bottom: 20px;
            border-bottom: 2px solid #e0e0e0;
            overflow-x: auto;
            -webkit-overflow-scrolling: touch;
        }

        .tab-btn {
            background: none;
            border: none;
            padding: 12px 20px;
            font-size: 14px;
            font-weight: 600;
            color: #999;
            cursor: pointer;
            border-bottom: 3px solid transparent;
            transition: all 0.3s ease;
            white-space: nowrap;
        }

        .tab-btn.active {
            color: #667eea;
            border-bottom-color: #667eea;
        }

        .tab-btn:hover {
            color: #667eea;
        }

        .tab-content {
            display: none;
        }

        .tab-content.active {
            display: block;
        }

        /* Search Bar */
        .search-bar {
            position: relative;
            margin-bottom: 15px;
        }

        .search-bar input {
            width: 100%;
            padding: 12px 15px 12px 42px;
            border: 2px solid #e0e0e0;
            border-radius: 25px;
            font-size: 14px;
            outline: none;
            transition: all 0.3s ease;
            box-sizing: border-box;
        }

        .search-bar input:focus {
            border-color: #667eea;
            box-shadow: 0 0 0 3px rgba(102,126,234,0.15);
        }

        .search-bar i {
            position: absolute;
            left: 15px;
            top: 50%;
            transform: translateY(-50%);
            color: #999;
            font-size: 14px;
        }

        .search-bar .search-clear {
            position: absolute;
            right: 15px;
            top: 50%;
            transform: translateY(-50%);
            color: #999;
            border: none;
            background: none;
            font-size: 14px;
            cursor: pointer;
            display: none;
            padding: 4px;
        }

        .search-no-result {
            text-align: center;
            padding: 30px 15px;
            color: #999;
            display: none;
            grid-column: 1 / -1;
        }

        .search-no-result i {
            font-size: 36px;
            display: block;
            margin-bottom: 8px;
        }

        /* Min Order Warning */
        .min-order-warning {
            background: #fff3cd;
            border: 1px solid #ffc107;
            border-radius: 8px;
            padding: 10px 15px;
            font-size: 13px;
            color: #856404;
            display: flex;
            align-items: center;
            gap: 8px;
            margin-bottom: 15px;
        }

        .min-order-warning i {
            font-size: 16px;
            flex-shrink: 0;
        }

        .min-order-ok {
            background: #d4edda;
            border-color: #28a745;
            color: #155724;
        }

        /* Main Layout */
        .order-layout {
            display: flex;
            flex-direction: column;
            gap: 20px;
        }

        /* Products Grid - Mobile first (2 columns) */
        .products-grid {
            display: grid;
            grid-template-columns: repeat(2, 1fr);
            gap: 12px;
            margin-bottom: 20px;
        }

        .product-card {
            background: white;
            border-radius: 12px;
            padding: 12px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.08);
            transition: all 0.3s ease;
            display: flex;
            flex-direction: column;
        }

        .product-card:active {
            transform: scale(0.98);
        }

        .product-name {
            font-weight: 600;
            color: #333;
            margin-bottom: 4px;
            font-size: 14px;
            line-height: 1.3;
            overflow: hidden;
            text-overflow: ellipsis;
            display: -webkit-box;
            -webkit-line-clamp: 2;
            -webkit-box-orient: vertical;
            word-break: break-word;
        }

        .product-price {
            color: #4CAF50;
            font-size: 18px;
            font-weight: 700;
            margin: 6px 0;
        }

        .product-stock {
            font-size: 11px;
            color: #999;
            margin-bottom: 8px;
        }

        .qty-control {
            display: flex;
            align-items: center;
            gap: 8px;
            margin-top: auto;
        }

        .qty-btn {
            width: 32px;
            height: 32px;
            border-radius: 50%;
            border: 2px solid #667eea;
            background: white;
            color: #667eea;
            font-weight: 700;
            font-size: 16px;
            cursor: pointer;
            transition: all 0.3s ease;
            display: flex;
            align-items: center;
            justify-content: center;
            flex-shrink: 0;
        }

        .qty-btn:hover {
            background: #667eea;
            color: white;
        }

        .qty-input {
            width: 40px;
            text-align: center;
            border: 1px solid #e0e0e0;
            border-radius: 6px;
            padding: 4px;
            font-size: 14px;
        }

        .btn-add-cart {
            width: 100%;
            background: linear-gradient(135deg, #4CAF50, #2E7D32);
            color: white;
            border: none;
            padding: 10px;
            border-radius: 8px;
            font-weight: 600;
            font-size: 13px;
            cursor: pointer;
            margin-top: 8px;
            transition: all 0.3s ease;
            min-height: 44px;
        }

        .btn-add-cart:active {
            transform: scale(0.97);
        }

        /* Floating Cart Bar (Mobile) */
        .cart-floating-bar {
            position: fixed;
            bottom: 0;
            left: 0;
            right: 0;
            background: white;
            padding: 12px 20px;
            box-shadow: 0 -4px 20px rgba(0,0,0,0.15);
            z-index: 1000;
            display: flex;
            align-items: center;
            justify-content: space-between;
            gap: 12px;
        }

        .cart-bar-info {
            display: flex;
            align-items: center;
            gap: 10px;
        }

        .cart-badge {
            background: #667eea;
            color: white;
            width: 36px;
            height: 36px;
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            font-weight: 700;
            font-size: 14px;
            position: relative;
        }

        .cart-bar-total {
            font-size: 18px;
            font-weight: 700;
            color: #333;
        }

        .cart-bar-count {
            font-size: 12px;
            color: #999;
        }

        .btn-view-cart {
            background: linear-gradient(135deg, #667eea, #764ba2);
            color: white;
            border: none;
            padding: 12px 24px;
            border-radius: 25px;
            font-weight: 600;
            font-size: 14px;
            cursor: pointer;
            white-space: nowrap;
            min-height: 44px;
        }

        .btn-view-cart:active {
            transform: scale(0.97);
        }

        /* Cart Overlay (Mobile) */
        .cart-overlay {
            display: none;
            position: fixed;
            top: 0;
            left: 0;
            right: 0;
            bottom: 0;
            background: rgba(0,0,0,0.5);
            z-index: 2000;
        }

        .cart-overlay.active {
            display: block;
        }

        .cart-sheet {
            position: absolute;
            bottom: 0;
            left: 0;
            right: 0;
            background: white;
            border-radius: 20px 20px 0 0;
            max-height: 80vh;
            overflow-y: auto;
            padding: 20px;
            transform: translateY(100%);
            transition: transform 0.3s ease;
        }

        .cart-overlay.active .cart-sheet {
            transform: translateY(0);
        }

        .cart-sheet-handle {
            width: 40px;
            height: 4px;
            background: #ddd;
            border-radius: 2px;
            margin: 0 auto 15px;
        }

        .cart-sheet h3 {
            margin: 0 0 15px 0;
            font-size: 18px;
            color: #333;
        }

        /* Cart Summary (Desktop sidebar) */
        .cart-sidebar {
            display: none;
        }

        .cart-summary {
            position: sticky;
            top: 20px;
            background: white;
            padding: 20px;
            border-radius: 12px;
            box-shadow: 0 5px 20px rgba(0,0,0,0.1);
        }

        .cart-summary h3 {
            margin: 0 0 15px 0;
            font-size: 18px;
            color: #333;
            border-bottom: 2px solid #e0e0e0;
            padding-bottom: 10px;
        }

        .cart-item {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 10px 0;
            border-bottom: 1px solid #f0f0f0;
            gap: 8px;
        }

        .cart-item-name {
            flex: 1;
            font-size: 14px;
        }

        .cart-item-qty {
            color: #666;
            font-size: 13px;
        }

        .cart-item-price {
            font-weight: 600;
            color: #4CAF50;
            white-space: nowrap;
        }

        .cart-item-remove {
            border: none;
            background: none;
            color: #f44336;
            cursor: pointer;
            padding: 4px;
            font-size: 16px;
            min-width: 32px;
            min-height: 32px;
            display: flex;
            align-items: center;
            justify-content: center;
        }

        .cart-charge-lines { border-top: 1px dashed #d9dee3; margin-top: 8px; padding-top: 8px; }
        .cart-charge-row { display: flex; justify-content: space-between; font-size: 13.5px;
                           color: #667; padding: 3px 0; }
        .cart-total {
            padding: 15px 0;
            font-size: 20px;
            font-weight: 700;
            color: #333;
            text-align: right;
        }

        .form-group {
            margin-bottom: 15px;
        }

        .form-label {
            display: block;
            margin-bottom: 6px;
            font-weight: 600;
            color: #333;
            font-size: 14px;
        }

        .form-control {
            width: 100%;
            padding: 12px;
            border: 2px solid #e0e0e0;
            border-radius: 8px;
            font-size: 14px;
            box-sizing: border-box;
        }

        .form-control:focus {
            outline: none;
            border-color: #667eea;
        }

        .payment-methods {
            display: grid;
            gap: 10px;
        }

        .payment-option {
            border: 2px solid #e0e0e0;
            border-radius: 8px;
            padding: 12px 15px;
            cursor: pointer;
            transition: all 0.3s ease;
            display: flex;
            align-items: center;
            gap: 10px;
            min-height: 44px;
        }

        .payment-option:hover {
            border-color: #667eea;
        }

        .payment-option input[type="radio"] {
            margin: 0;
        }

        .payment-option.selected {
            border-color: #667eea;
            background: #f8f9fe;
        }

        .btn-place-order {
            width: 100%;
            background: linear-gradient(135deg, #667eea, #764ba2);
            color: white;
            border: none;
            padding: 15px;
            border-radius: 10px;
            font-size: 16px;
            font-weight: 700;
            cursor: pointer;
            margin-top: 15px;
            transition: all 0.3s ease;
            min-height: 50px;
        }

        .btn-place-order:hover:not(:disabled) {
            transform: translateY(-2px);
            box-shadow: 0 6px 20px rgba(102, 126, 234, 0.3);
        }

        .btn-place-order:disabled {
            opacity: 0.5;
            cursor: not-allowed;
        }

        /* Order History */
        .orders-list {
            background: white;
            border-radius: 12px;
            padding: 15px;
            box-shadow: 0 3px 10px rgba(0,0,0,0.1);
        }

        .order-card {
            border: 1px solid #e0e0e0;
            border-radius: 10px;
            padding: 15px;
            margin-bottom: 12px;
        }

        .order-header {
            display: flex;
            justify-content: space-between;
            align-items: flex-start;
            margin-bottom: 10px;
            padding-bottom: 10px;
            border-bottom: 2px solid #f0f0f0;
            flex-wrap: wrap;
            gap: 8px;
        }

        .order-number {
            font-weight: 700;
            color: #667eea;
            font-size: 14px;
        }

        .order-status {
            padding: 4px 12px;
            border-radius: 20px;
            font-size: 11px;
            font-weight: 600;
        }

        .status-PENDING { background: #fff3cd; color: #856404; }
        .status-CONFIRMED { background: #d1ecf1; color: #0c5460; }
        .status-PREPARING { background: #e2e3e5; color: #383d41; }
        .status-DELIVERED { background: #d4edda; color: #155724; }
        .status-CANCELLED { background: #f8d7da; color: #721c24; }

        /* Order Timeline */
        .order-timeline {
            display: flex;
            justify-content: space-between;
            margin: 15px 0;
            position: relative;
            padding: 0 5px;
        }

        .order-timeline::before {
            content: '';
            position: absolute;
            top: 15px;
            left: 25px;
            right: 25px;
            height: 3px;
            background: #e0e0e0;
            z-index: 1;
        }

        .timeline-step {
            display: flex;
            flex-direction: column;
            align-items: center;
            position: relative;
            z-index: 2;
            flex: 1;
        }

        .timeline-icon {
            width: 30px;
            height: 30px;
            border-radius: 50%;
            background: #e0e0e0;
            display: flex;
            align-items: center;
            justify-content: center;
            color: #999;
            font-size: 12px;
            margin-bottom: 6px;
        }

        .timeline-step.completed .timeline-icon {
            background: #4CAF50;
            color: white;
        }

        .timeline-step.active .timeline-icon {
            background: #2196F3;
            color: white;
            animation: pulse-blue 1.5s infinite;
        }

        .timeline-step.cancelled .timeline-icon {
            background: #f44336;
            color: white;
        }

        @keyframes pulse-blue {
            0%, 100% { box-shadow: 0 0 0 0 rgba(33, 150, 243, 0.4); }
            50% { box-shadow: 0 0 0 8px rgba(33, 150, 243, 0); }
        }

        .timeline-label {
            font-size: 10px;
            color: #999;
            text-align: center;
            max-width: 60px;
        }

        .timeline-step.completed .timeline-label,
        .timeline-step.active .timeline-label {
            color: #333;
            font-weight: 600;
        }

        .timeline-time {
            font-size: 9px;
            color: #666;
            margin-top: 2px;
        }

        .order-items-list {
            background: #f8f9fa;
            border-radius: 8px;
            padding: 10px;
            margin: 10px 0;
        }

        .order-item-row {
            display: flex;
            justify-content: space-between;
            padding: 4px 0;
            border-bottom: 1px solid #eee;
            font-size: 13px;
        }

        .order-item-row:last-child {
            border-bottom: none;
        }

        .order-item-name {
            flex: 1;
        }

        .order-item-qty {
            color: #666;
            margin: 0 8px;
        }

        .order-item-subtotal {
            font-weight: 600;
            color: #4CAF50;
        }

        .order-footer {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-top: 10px;
            flex-wrap: wrap;
            gap: 8px;
            font-size: 13px;
        }

        .order-total-price {
            font-size: 18px;
            font-weight: 700;
            color: #4CAF50;
        }

        .empty-cart {
            text-align: center;
            padding: 30px;
            color: #999;
        }

        .empty-cart i {
            font-size: 40px;
            margin-bottom: 10px;
        }

        /* Desktop: show sidebar cart, hide floating bar */
        @media (min-width: 768px) {
            .rs-container {
                padding-bottom: 30px;
            }

            .rs-header h2 {
                font-size: 24px;
            }

            .order-layout {
                flex-direction: row;
            }

            .order-layout > .products-section {
                flex: 2;
            }

            .cart-sidebar {
                display: block;
                flex: 1;
            }

            .cart-floating-bar {
                display: none !important;
            }

            .products-grid {
                grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
                gap: 15px;
            }

            .product-name {
                font-size: 15px;
            }

            .timeline-icon {
                width: 35px;
                height: 35px;
                font-size: 14px;
            }

            .timeline-label {
                font-size: 11px;
                max-width: 70px;
            }
        }

        /* Small mobile */
        @media (max-width: 380px) {
            .products-grid {
                grid-template-columns: 1fr;
            }

            .product-card {
                flex-direction: row;
                gap: 12px;
                align-items: center;
            }

            .product-card .product-info {
                flex: 1;
            }

            .product-card .product-actions {
                display: flex;
                flex-direction: column;
                align-items: center;
                gap: 4px;
            }

            .btn-add-cart {
                padding: 8px 12px;
                font-size: 12px;
            }
        }
    </style>

    <div class="rs-container">
    <!-- Header -->
    <div class="rs-header">
        <h2><i class="fas fa-utensils"></i> Room Service</h2>
        <a href="Dashboard.aspx" class="btn-back">
            <i class="fas fa-arrow-left"></i> กลับ
        </a>
    </div>

    <!-- Tabs -->
    <div class="tabs">
        <button type="button" class="tab-btn active" onclick="switchTab(event, 'order')">
            <i class="fas fa-shopping-cart"></i> สั่งอาหาร
        </button>
        <button type="button" class="tab-btn" onclick="switchTab(event, 'history')">
            <i class="fas fa-history"></i> ประวัติ
        </button>
    </div>

    <!-- Tab: New Order -->
    <div id="order" class="tab-content active">
        <asp:Panel ID="pnlClosed" runat="server" Visible="false"
            style="background:#fff3cd;border:1px solid #ffe08a;color:#7a5d00;border-radius:12px;padding:16px 18px;margin:0 0 16px;display:flex;gap:10px;align-items:center;font-weight:600;">
            <i class="fas fa-clock" style="font-size:20px;"></i>
            <asp:Label ID="lblClosedMessage" runat="server" Text=""></asp:Label>
        </asp:Panel>
        <div class="order-layout">
            <!-- Products Section -->
            <div class="products-section">
                <!-- Search Bar -->
                <div class="search-bar">
                    <i class="fas fa-search"></i>
                    <input type="text" id="searchProduct" placeholder="ค้นหาเมนู..." oninput="filterProducts()" />
                    <button type="button" class="search-clear" id="searchClear" onclick="clearSearch()">
                        <i class="fas fa-times"></i>
                    </button>
                </div>

                <!-- Min Order Warning -->
                <div class="min-order-warning" id="minOrderWarning">
                    <i class="fas fa-info-circle"></i>
                    <span>ยอดสั่งซื้อขั้นต่ำ ฿<span id="minOrderAmount">200</span> (ยอดปัจจุบัน: ฿<span id="currentOrderAmount">0</span>)</span>
                </div>

                <div class="products-grid" id="productsGrid">
                    <asp:Repeater ID="rptProducts" runat="server">
                        <ItemTemplate>
                            <div class="product-card" data-name='<%# Eval("Name").ToString().Replace("'","&#39;") %>'>
                                <div class="product-name"><%# Eval("Name") %></div>
                                <div class="product-price">฿<%# Eval("Price", "{0:N0}") %></div>
                                <div class="product-stock">
                                    เหลือ <%# Eval("Quantity") %> ชิ้น
                                </div>
                                <div class="qty-control">
                                    <button type="button" class="qty-btn" onclick="changeQty(this, -1)">−</button>
                                    <input type="number" class="qty-input" min="1"
                                           max='<%# Eval("Quantity") %>'
                                           value="1"
                                           data-product-id='<%# Eval("ID") %>'
                                           data-product-name='<%# Eval("Name") %>'
                                           data-price='<%# Eval("Price") %>' />
                                    <button type="button" class="qty-btn" onclick="changeQty(this, 1)">+</button>
                                </div>
                                <button type="button" class="btn-add-cart"
                                        onclick="addToCart(this, <%# Eval("ID") %>, '<%# Eval("Name") %>', <%# Eval("Price") %>)">
                                    <i class="fas fa-plus"></i> เพิ่มลงตะกร้า
                                </button>
                            </div>
                        </ItemTemplate>
                    </asp:Repeater>
                    <div class="search-no-result" id="noResult">
                        <i class="fas fa-search"></i>
                        <p>ไม่พบเมนูที่ค้นหา</p>
                    </div>
                </div>
            </div>

            <!-- Cart Sidebar (Desktop only) -->
            <div class="cart-sidebar">
                <div class="cart-summary">
                    <h3><i class="fas fa-shopping-cart"></i> ตะกร้าของคุณ</h3>
                    <div id="cartItemsDesktop"></div>
                    <div class="cart-charge-lines" id="chargeLinesDesktop" style="display:none;">
                        <div class="cart-charge-row"><span>ค่าสินค้า</span><span>฿<span id="cartSubtotalDesktop">0</span></span></div>
                        <div class="cart-charge-row"><span id="cartSvcLabelDesktop">ค่าบริการ</span><span>฿<span id="cartSvcDesktop">0</span></span></div>
                    </div>
                    <div class="cart-total">
                        รวม: ฿<span id="cartTotalDesktop">0</span>
                    </div>

                    <div class="form-group">
                        <label class="form-label">หมายเหตุการจัดส่ง</label>
                        <asp:TextBox ID="txtDeliveryInstructions" runat="server" CssClass="form-control"
                            TextMode="MultiLine" Rows="2"
                            placeholder="เช่น เคาะเบาๆ ลูกนอนอยู่"></asp:TextBox>
                    </div>

                    <div class="form-group">
                        <label class="form-label">วิธีชำระเงิน</label>
                        <div class="payment-methods">
                            <label class="payment-option selected" onclick="selectPayment(this, 'CHARGE_TO_ROOM')">
                                <input type="radio" name="paymentMethod" value="CHARGE_TO_ROOM" checked />
                                <i class="fas fa-door-open"></i> เรียกเก็บกับห้องพัก
                            </label>
                            <label class="payment-option" onclick="selectPayment(this, 'TRANSFER')">
                                <input type="radio" name="paymentMethod" value="TRANSFER" />
                                <i class="fas fa-money-check-alt"></i> โอนเงิน
                            </label>
                        </div>
                    </div>

                    <div id="transferSectionDesktop" style="display: none;" class="form-group">
                        <label class="form-label">อัพโหลดสลิป</label>
                        <asp:FileUpload ID="filePaymentSlip" runat="server" CssClass="form-control" accept="image/*" />
                    </div>

                    <asp:HiddenField ID="hfCartItems" runat="server" />

                    <asp:Button ID="btnPlaceOrder" runat="server" Text="สั่งเลย"
                        CssClass="btn-place-order" OnClick="btnPlaceOrder_Click"
                        OnClientClick="return validateOrder();" />
                </div>
            </div>
        </div>
    </div>

    <!-- Tab: Order History -->
    <div id="history" class="tab-content">
        <div class="orders-list">
            <h3 style="margin-bottom: 15px;"><i class="fas fa-history"></i> ประวัติการสั่งซื้อ</h3>
            <asp:Repeater ID="rptOrders" runat="server">
                <ItemTemplate>
                    <div class="order-card" data-order-id='<%# Eval("ID") %>'>
                        <div class="order-header">
                            <div>
                                <div class="order-number">#<%# Eval("Order_Number") %></div>
                                <div style="color: #666; font-size: 13px;">
                                    <i class="fas fa-clock"></i> <%# Eval("Order_Date", "{0:dd MMM yyyy HH:mm}") %>
                                </div>
                            </div>
                            <span class="order-status status-<%# Eval("Order_Status") %>">
                                <%# GetStatusText(Eval("Order_Status").ToString()) %>
                            </span>
                        </div>

                        <!-- Status Timeline -->
                        <div class="order-timeline">
                            <div class='timeline-step <%# GetTimelineClass(Eval("Order_Status").ToString(), "PENDING") %>'>
                                <div class="timeline-icon"><i class="fas fa-clipboard-list"></i></div>
                                <span class="timeline-label">สั่งซื้อแล้ว</span>
                                <span class="timeline-time"><%# Eval("Order_Date", "{0:HH:mm}") %></span>
                            </div>
                            <div class='timeline-step <%# GetTimelineClass(Eval("Order_Status").ToString(), "CONFIRMED") %>'>
                                <div class="timeline-icon"><i class="fas fa-utensils"></i></div>
                                <span class="timeline-label">กำลังจัดเตรียม</span>
                                <span class="timeline-time"><%# Eval("Confirmed_Date") != DBNull.Value ? Convert.ToDateTime(Eval("Confirmed_Date")).ToString("HH:mm") : "-" %></span>
                            </div>
                            <div class='timeline-step <%# GetTimelineClass(Eval("Order_Status").ToString(), "DELIVERED") %>'>
                                <div class="timeline-icon"><i class="fas fa-check-circle"></i></div>
                                <span class="timeline-label">จัดส่งแล้ว</span>
                                <span class="timeline-time"><%# Eval("Delivered_Date") != DBNull.Value ? Convert.ToDateTime(Eval("Delivered_Date")).ToString("HH:mm") : "-" %></span>
                            </div>
                        </div>

                        <!-- Order Items -->
                        <div class="order-items-list">
                            <%# GetOrderItemsHtml(Eval("ID")) %>
                        </div>

                        <div style="display: flex; justify-content: space-between; align-items: center; margin-top: 10px;">
                            <div>
                                <strong>การชำระเงิน:</strong>
                                <%# GetPaymentText(Eval("Payment_Method").ToString()) %>
                                <span class="order-status status-<%# Eval("Payment_Status") %>" style="font-size: 11px;">
                                    <%# GetPaymentStatusText(Eval("Payment_Status").ToString()) %>
                                </span>
                            </div>
                            <div style="font-size: 18px; font-weight: 700; color: #4CAF50;">
                                ฿<%# Eval("Total_Amount", "{0:N0}") %>
                            </div>
                        </div>

                        <%# !string.IsNullOrEmpty(Eval("Delivery_Instructions").ToString()) ?
                            "<div style='font-size: 13px; color: #666; margin-top: 10px; padding: 10px; background: #f5f5f5; border-radius: 5px;'><i class='fas fa-comment'></i> " + Eval("Delivery_Instructions") + "</div>" : "" %>
                    </div>
                </ItemTemplate>
            </asp:Repeater>

            <asp:Label ID="lblNoOrders" runat="server" Visible="false"
                Text="<div class='empty-cart'><i class='fas fa-inbox'></i><p>ยังไม่มีรายการสั่งซื้อ</p></div>"></asp:Label>
        </div>
    </div>

    </div><!-- /.rs-container -->

    <!-- Floating Cart Bar (Mobile) -->
    <div class="cart-floating-bar" id="cartFloatingBar" style="display: none;">
        <div class="cart-bar-info">
            <div class="cart-badge">
                <i class="fas fa-shopping-cart"></i>
            </div>
            <div>
                <div class="cart-bar-total">฿<span id="cartBarTotal">0</span></div>
                <div class="cart-bar-count"><span id="cartBarCount">0</span> รายการ</div>
            </div>
        </div>
        <button type="button" class="btn-view-cart" onclick="openCartSheet()">
            ดูตะกร้า <i class="fas fa-chevron-up"></i>
        </button>
    </div>

    <!-- Cart Overlay / Bottom Sheet (Mobile) -->
    <div class="cart-overlay" id="cartOverlay">
        <div class="cart-sheet">
            <div class="cart-sheet-handle"></div>
            <h3><i class="fas fa-shopping-cart"></i> ตะกร้าของคุณ</h3>
            <div id="cartItemsMobile"></div>
            <div class="cart-charge-lines" id="chargeLinesMobile" style="display:none;">
                <div class="cart-charge-row"><span>ค่าสินค้า</span><span>฿<span id="cartSubtotalMobile">0</span></span></div>
                <div class="cart-charge-row"><span id="cartSvcLabelMobile">ค่าบริการ</span><span>฿<span id="cartSvcMobile">0</span></span></div>
            </div>
            <div class="cart-total">
                รวม: ฿<span id="cartTotalMobile">0</span>
            </div>

            <div class="form-group">
                <label class="form-label">หมายเหตุการจัดส่ง</label>
                <textarea id="mobileDeliveryNote" class="form-control" rows="2"
                    placeholder="เช่น เคาะเบาๆ ลูกนอนอยู่"></textarea>
            </div>

            <div class="form-group">
                <label class="form-label">วิธีชำระเงิน</label>
                <div class="payment-methods">
                    <label class="payment-option selected" onclick="selectPaymentMobile(this, 'CHARGE_TO_ROOM')">
                        <input type="radio" name="paymentMethodMobile" value="CHARGE_TO_ROOM" checked />
                        <i class="fas fa-door-open"></i> เรียกเก็บกับห้องพัก
                    </label>
                    <label class="payment-option" onclick="selectPaymentMobile(this, 'TRANSFER')">
                        <input type="radio" name="paymentMethodMobile" value="TRANSFER" />
                        <i class="fas fa-money-check-alt"></i> โอนเงิน
                    </label>
                </div>
            </div>

            <button type="button" class="btn-place-order" onclick="submitOrderFromMobile()">
                <i class="fas fa-paper-plane"></i> สั่งเลย
            </button>
            <button type="button" onclick="closeCartSheet()"
                style="width:100%;margin-top:10px;padding:12px;border:2px solid #e0e0e0;border-radius:10px;background:white;font-size:14px;cursor:pointer;color:#666;">
                ปิด
            </button>
        </div>
    </div>

    <script>
        var cart = [];
        var MIN_ORDER_AMOUNT = 200; // ยอดสั่งซื้อขั้นต่ำ (฿)

        // ===== Search =====
        function filterProducts() {
            var input = document.getElementById('searchProduct');
            var clearBtn = document.getElementById('searchClear');
            var keyword = input.value.trim().toLowerCase();
            var cards = document.querySelectorAll('.product-card');
            var noResult = document.getElementById('noResult');
            var visibleCount = 0;

            clearBtn.style.display = keyword.length > 0 ? 'block' : 'none';

            for (var i = 0; i < cards.length; i++) {
                var name = (cards[i].getAttribute('data-name') || '').toLowerCase();
                if (keyword === '' || name.indexOf(keyword) !== -1) {
                    cards[i].style.display = '';
                    visibleCount++;
                } else {
                    cards[i].style.display = 'none';
                }
            }

            if (noResult) {
                noResult.style.display = (visibleCount === 0 && keyword !== '') ? 'block' : 'none';
            }
        }

        function clearSearch() {
            var input = document.getElementById('searchProduct');
            input.value = '';
            filterProducts();
            input.focus();
        }

        // ===== Qty Controls =====
        function changeQty(btn, delta) {
            var input = btn.parentElement.querySelector('.qty-input');
            var newValue = parseInt(input.value) + delta;
            var max = parseInt(input.max);
            if (newValue >= 1 && newValue <= max) {
                input.value = newValue;
            }
        }

        // ===== Cart =====
        function addToCart(btn, productId, productName, price) {
            var card = btn.closest('.product-card');
            var qtyInput = card.querySelector('.qty-input');
            var quantity = parseInt(qtyInput.value);

            var existingIndex = -1;
            for (var i = 0; i < cart.length; i++) {
                if (cart[i].productId === productId) { existingIndex = i; break; }
            }
            if (existingIndex !== -1) {
                cart[existingIndex].quantity += quantity;
            } else {
                cart.push({ productId: productId, productName: productName, price: price, quantity: quantity });
            }

            updateCartDisplay();
            qtyInput.value = 1;

            btn.innerHTML = '<i class="fas fa-check"></i> เพิ่มแล้ว!';
            setTimeout(function() {
                btn.innerHTML = '<i class="fas fa-plus"></i> เพิ่มลงตะกร้า';
            }, 1000);
        }

        function removeFromCart(index) {
            cart.splice(index, 1);
            updateCartDisplay();
        }

        function buildCartHtml() {
            if (cart.length === 0) {
                return '<div class="empty-cart"><i class="fas fa-shopping-cart" style="font-size:40px;display:block;margin-bottom:10px;"></i><p>ตะกร้าว่างเปล่า</p></div>';
            }
            var html = '';
            for (var i = 0; i < cart.length; i++) {
                var item = cart[i];
                var subtotal = item.price * item.quantity;
                html += '<div class="cart-item">' +
                    '<div class="cart-item-name">' + item.productName + '</div>' +
                    '<div class="cart-item-qty">x' + item.quantity + '</div>' +
                    '<div class="cart-item-price">฿' + subtotal.toLocaleString() + '</div>' +
                    '<button type="button" class="cart-item-remove" onclick="removeFromCart(' + i + ')">' +
                    '<i class="fas fa-times"></i></button>' +
                    '</div>';
            }
            return html;
        }

        function getCartTotal() {
            var total = 0;
            for (var i = 0; i < cart.length; i++) {
                total += cart[i].price * cart[i].quantity;
            }
            return total;
        }

        // ค่าบริการ — คำนวณตามที่แอดมินตั้งไว้ (แสดงผลเท่านั้น เซิร์ฟเวอร์คิดใหม่ตอนกดสั่ง)
        function getCartQuantity() {
            var q = 0;
            for (var i = 0; i < cart.length; i++) q += cart[i].quantity;
            return q;
        }

        function getServiceCharge(subtotal) {
            var cfg = window.rsServiceCharge;
            if (!cfg || !cfg.mode || cfg.mode === 'NONE' || !(cfg.value > 0) || subtotal <= 0) return 0;
            var amt = 0;
            if (cfg.mode === 'PERCENT') amt = subtotal * cfg.value / 100;
            else if (cfg.mode === 'PER_ITEM') amt = cfg.value * getCartQuantity();
            else if (cfg.mode === 'PER_ORDER') amt = cfg.value;
            amt = Math.round(amt * 100) / 100;
            if (cfg.max > 0 && amt > cfg.max) amt = cfg.max;
            return amt > 0 ? amt : 0;
        }

        function renderChargeLines(subtotal, charge) {
            var cfg = window.rsServiceCharge || {};
            var show = charge > 0;
            var ids = ['Desktop', 'Mobile'];
            for (var i = 0; i < ids.length; i++) {
                var box = document.getElementById('chargeLines' + ids[i]);
                if (box) box.style.display = show ? 'block' : 'none';
                if (!show) continue;
                var sub = document.getElementById('cartSubtotal' + ids[i]);
                var svc = document.getElementById('cartSvc' + ids[i]);
                var lbl = document.getElementById('cartSvcLabel' + ids[i]);
                if (sub) sub.textContent = subtotal.toLocaleString();
                if (svc) svc.textContent = charge.toLocaleString();
                if (lbl) {
                    var text = cfg.label || 'ค่าบริการ';
                    if (cfg.mode === 'PERCENT') text += ' ' + cfg.value + '%';
                    else if (cfg.mode === 'PER_ITEM') text += ' (฿' + cfg.value + '/ชิ้น)';
                    lbl.textContent = text;
                }
            }
        }

        function updateCartDisplay() {
            var subtotal = getCartTotal();
            var serviceCharge = getServiceCharge(subtotal);
            var total = subtotal + serviceCharge;
            var totalStr = total.toLocaleString();
            var cartHtml = buildCartHtml();
            // ขั้นต่ำเทียบกับค่าสินค้า ไม่รวมค่าบริการ (ตรงกับที่เซิร์ฟเวอร์ตรวจ)
            var meetsMinimum = subtotal >= MIN_ORDER_AMOUNT;

            renderChargeLines(subtotal, serviceCharge);

            // Desktop sidebar
            var desktopItems = document.getElementById('cartItemsDesktop');
            var desktopTotal = document.getElementById('cartTotalDesktop');
            if (desktopItems) desktopItems.innerHTML = cartHtml;
            if (desktopTotal) desktopTotal.textContent = totalStr;

            // Mobile bottom sheet
            var mobileItems = document.getElementById('cartItemsMobile');
            var mobileTotal = document.getElementById('cartTotalMobile');
            if (mobileItems) mobileItems.innerHTML = cartHtml;
            if (mobileTotal) mobileTotal.textContent = totalStr;

            // Floating bar
            var floatingBar = document.getElementById('cartFloatingBar');
            var barTotal = document.getElementById('cartBarTotal');
            var barCount = document.getElementById('cartBarCount');
            if (floatingBar) {
                floatingBar.style.display = cart.length > 0 ? 'flex' : 'none';
            }
            if (barTotal) barTotal.textContent = totalStr;
            if (barCount) barCount.textContent = cart.length;

            // Min order warning
            var warning = document.getElementById('minOrderWarning');
            var currentAmount = document.getElementById('currentOrderAmount');
            if (warning && currentAmount) {
                currentAmount.textContent = subtotal.toLocaleString();
                if (cart.length === 0) {
                    warning.className = 'min-order-warning';
                } else if (meetsMinimum) {
                    warning.className = 'min-order-warning min-order-ok';
                } else {
                    warning.className = 'min-order-warning';
                }
            }

            // Place order button - disable if no items, below minimum, or ordering closed
            var btnOrder = document.getElementById('<%= btnPlaceOrder.ClientID %>');
            if (btnOrder) btnOrder.disabled = (cart.length === 0 || !meetsMinimum || window.rsOrderingClosed === true);

            // Hidden field
            document.getElementById('<%= hfCartItems.ClientID %>').value = JSON.stringify(cart);
        }

        // ===== Payment =====
        function selectPayment(element, method) {
            var options = element.closest('.payment-methods').querySelectorAll('.payment-option');
            for (var i = 0; i < options.length; i++) { options[i].classList.remove('selected'); }
            element.classList.add('selected');

            var transferSection = document.getElementById('transferSectionDesktop');
            if (transferSection) {
                transferSection.style.display = (method === 'TRANSFER') ? 'block' : 'none';
            }
        }

        function selectPaymentMobile(element, method) {
            var options = element.closest('.payment-methods').querySelectorAll('.payment-option');
            for (var i = 0; i < options.length; i++) { options[i].classList.remove('selected'); }
            element.classList.add('selected');
        }

        // ===== Cart Overlay =====
        function openCartSheet() {
            var overlay = document.getElementById('cartOverlay');
            if (overlay) overlay.classList.add('active');
            document.body.style.overflow = 'hidden';
        }

        function closeCartSheet() {
            var overlay = document.getElementById('cartOverlay');
            if (overlay) overlay.classList.remove('active');
            document.body.style.overflow = '';
        }

        function submitOrderFromMobile() {
            if (cart.length === 0) {
                alert('กรุณาเพิ่มสินค้าลงตะกร้า');
                return;
            }
            var total = getCartTotal();
            if (total < MIN_ORDER_AMOUNT) {
                alert('ยอดสั่งซื้อขั้นต่ำ ฿' + MIN_ORDER_AMOUNT.toLocaleString() + '\nยอดปัจจุบัน: ฿' + total.toLocaleString());
                return;
            }
            // Sync mobile delivery note to desktop field
            var mobileNote = document.getElementById('mobileDeliveryNote');
            var desktopNote = document.getElementById('<%= txtDeliveryInstructions.ClientID %>');
            if (mobileNote && desktopNote) {
                desktopNote.value = mobileNote.value;
            }
            // Sync payment method
            var mobilePayment = document.querySelector('input[name="paymentMethodMobile"]:checked');
            if (mobilePayment) {
                var allDesktop = document.querySelectorAll('input[name="paymentMethod"]');
                for (var i = 0; i < allDesktop.length; i++) {
                    if (allDesktop[i].value === mobilePayment.value) {
                        allDesktop[i].checked = true;
                    }
                }
            }
            closeCartSheet();
            document.getElementById('<%= btnPlaceOrder.ClientID %>').click();
        }

        // Close overlay on backdrop click
        document.addEventListener('click', function(e) {
            var overlay = document.getElementById('cartOverlay');
            if (e.target === overlay) {
                closeCartSheet();
            }
        });

        // ===== Tabs =====
        function switchTab(evt, tabName) {
            var tabs = document.querySelectorAll('.tab-content');
            for (var i = 0; i < tabs.length; i++) { tabs[i].classList.remove('active'); }
            var btns = document.querySelectorAll('.tab-btn');
            for (var i = 0; i < btns.length; i++) { btns[i].classList.remove('active'); }
            document.getElementById(tabName).classList.add('active');
            evt.currentTarget.classList.add('active');
        }

        function validateOrder() {
            if (cart.length === 0) {
                alert('กรุณาเพิ่มสินค้าลงตะกร้า');
                return false;
            }
            var total = getCartTotal();
            if (total < MIN_ORDER_AMOUNT) {
                alert('ยอดสั่งซื้อขั้นต่ำ ฿' + MIN_ORDER_AMOUNT.toLocaleString() + '\nยอดปัจจุบัน: ฿' + total.toLocaleString());
                return false;
            }
            return true;
        }

        // Initialize
        document.addEventListener('DOMContentLoaded', function() {
            updateCartDisplay();
            // Set min order amount display
            var minEl = document.getElementById('minOrderAmount');
            if (minEl) minEl.textContent = MIN_ORDER_AMOUNT.toLocaleString();
        });
    </script>
</asp:Content>
