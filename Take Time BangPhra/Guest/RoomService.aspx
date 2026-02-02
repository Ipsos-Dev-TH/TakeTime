<%@ Page Title="Room Service" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RoomService.aspx.cs" Inherits="Take_Time_BangPhra.Guest.RoomService" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .rs-header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 25px;
            border-radius: 12px;
            margin-bottom: 25px;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .rs-header h2 {
            margin: 0;
            font-size: 26px;
            font-weight: 700;
        }

        .btn-back {
            background: rgba(255,255,255,0.2);
            color: white;
            border: none;
            padding: 10px 20px;
            border-radius: 20px;
            text-decoration: none;
            font-weight: 500;
            transition: all 0.3s ease;
        }

        .btn-back:hover {
            background: rgba(255,255,255,0.3);
            color: white;
            text-decoration: none;
        }

        .tabs {
            display: flex;
            gap: 10px;
            margin-bottom: 25px;
            border-bottom: 2px solid #e0e0e0;
        }

        .tab-btn {
            background: none;
            border: none;
            padding: 12px 25px;
            font-size: 16px;
            font-weight: 600;
            color: #999;
            cursor: pointer;
            border-bottom: 3px solid transparent;
            transition: all 0.3s ease;
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

        .products-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
            gap: 20px;
            margin-bottom: 25px;
        }

        .product-card {
            background: white;
            border-radius: 12px;
            padding: 15px;
            box-shadow: 0 3px 10px rgba(0,0,0,0.1);
            transition: all 0.3s ease;
        }

        .product-card:hover {
            transform: translateY(-3px);
            box-shadow: 0 6px 15px rgba(0,0,0,0.15);
        }

        .product-name {
            font-weight: 600;
            color: #333;
            margin-bottom: 5px;
            font-size: 16px;
        }

        .product-price {
            color: #4CAF50;
            font-size: 20px;
            font-weight: 700;
            margin: 10px 0;
        }

        .product-stock {
            font-size: 13px;
            color: #666;
            margin-bottom: 10px;
        }

        .qty-control {
            display: flex;
            align-items: center;
            gap: 10px;
            margin-top: 10px;
        }

        .qty-btn {
            width: 30px;
            height: 30px;
            border-radius: 50%;
            border: 2px solid #667eea;
            background: white;
            color: #667eea;
            font-weight: 700;
            cursor: pointer;
            transition: all 0.3s ease;
        }

        .qty-btn:hover {
            background: #667eea;
            color: white;
        }

        .qty-input {
            width: 50px;
            text-align: center;
            border: 1px solid #e0e0e0;
            border-radius: 5px;
            padding: 5px;
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
            cursor: pointer;
            margin-top: 10px;
            transition: all 0.3s ease;
        }

        .btn-add-cart:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(76, 175, 80, 0.3);
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
            font-size: 20px;
            color: #333;
            border-bottom: 2px solid #e0e0e0;
            padding-bottom: 10px;
        }

        .cart-item {
            display: flex;
            justify-content: space-between;
            padding: 10px 0;
            border-bottom: 1px solid #f0f0f0;
        }

        .cart-item-name {
            flex: 1;
            font-size: 14px;
        }

        .cart-item-qty {
            color: #666;
            margin: 0 10px;
        }

        .cart-item-price {
            font-weight: 600;
            color: #4CAF50;
        }

        .cart-total {
            padding: 15px 0;
            font-size: 20px;
            font-weight: 700;
            color: #333;
            text-align: right;
        }

        .form-group {
            margin-bottom: 20px;
        }

        .form-label {
            display: block;
            margin-bottom: 8px;
            font-weight: 600;
            color: #333;
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
            padding: 15px;
            cursor: pointer;
            transition: all 0.3s ease;
        }

        .payment-option:hover {
            border-color: #667eea;
        }

        .payment-option input[type="radio"] {
            margin-right: 10px;
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
            font-size: 18px;
            font-weight: 700;
            cursor: pointer;
            margin-top: 15px;
            transition: all 0.3s ease;
        }

        .btn-place-order:hover:not(:disabled) {
            transform: translateY(-2px);
            box-shadow: 0 6px 20px rgba(102, 126, 234, 0.3);
        }

        .btn-place-order:disabled {
            opacity: 0.5;
            cursor: not-allowed;
        }

        .orders-list {
            background: white;
            border-radius: 12px;
            padding: 20px;
            box-shadow: 0 3px 10px rgba(0,0,0,0.1);
        }

        .order-card {
            border: 1px solid #e0e0e0;
            border-radius: 10px;
            padding: 15px;
            margin-bottom: 15px;
        }

        .order-header {
            display: flex;
            justify-content: space-between;
            margin-bottom: 10px;
            padding-bottom: 10px;
            border-bottom: 2px solid #f0f0f0;
        }

        .order-number {
            font-weight: 700;
            color: #667eea;
        }

        .order-status {
            padding: 5px 15px;
            border-radius: 20px;
            font-size: 12px;
            font-weight: 600;
        }

        .status-PENDING { background: #fff3cd; color: #856404; }
        .status-CONFIRMED { background: #d1ecf1; color: #0c5460; }
        .status-PREPARING { background: #e2e3e5; color: #383d41; }
        .status-DELIVERED { background: #d4edda; color: #155724; }
        .status-CANCELLED { background: #f8d7da; color: #721c24; }

        /* Order Status Timeline */
        .order-timeline {
            display: flex;
            justify-content: space-between;
            margin: 20px 0;
            position: relative;
            padding: 0 10px;
        }

        .order-timeline::before {
            content: '';
            position: absolute;
            top: 15px;
            left: 30px;
            right: 30px;
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
            width: 35px;
            height: 35px;
            border-radius: 50%;
            background: #e0e0e0;
            display: flex;
            align-items: center;
            justify-content: center;
            color: #999;
            font-size: 14px;
            margin-bottom: 8px;
            transition: all 0.3s ease;
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
            50% { box-shadow: 0 0 0 10px rgba(33, 150, 243, 0); }
        }

        .timeline-label {
            font-size: 11px;
            color: #999;
            text-align: center;
            max-width: 70px;
        }

        .timeline-step.completed .timeline-label,
        .timeline-step.active .timeline-label {
            color: #333;
            font-weight: 600;
        }

        .timeline-time {
            font-size: 10px;
            color: #666;
            margin-top: 3px;
        }

        /* Order Items List */
        .order-items-list {
            background: #f8f9fa;
            border-radius: 8px;
            padding: 12px;
            margin: 10px 0;
        }

        .order-item-row {
            display: flex;
            justify-content: space-between;
            padding: 5px 0;
            border-bottom: 1px solid #eee;
        }

        .order-item-row:last-child {
            border-bottom: none;
        }

        .order-item-name {
            flex: 1;
        }

        .order-item-qty {
            color: #666;
            margin: 0 10px;
        }

        .order-item-subtotal {
            font-weight: 600;
            color: #4CAF50;
        }

        .empty-cart {
            text-align: center;
            padding: 40px;
            color: #999;
        }

        .empty-cart i {
            font-size: 60px;
            margin-bottom: 15px;
        }

        @media (max-width: 768px) {
            .products-grid {
                grid-template-columns: 1fr;
            }

            .cart-summary {
                position: static;
                margin-top: 20px;
            }
        }
    </style>

    <!-- Header -->
    <div class="rs-header">
        <h2><i class="fas fa-utensils"></i> Room Service</h2>
        <a href="Dashboard.aspx" class="btn-back">
            <i class="fas fa-arrow-left"></i> Back to Dashboard
        </a>
    </div>

    <!-- Tabs -->
    <div class="tabs">
        <button class="tab-btn active" onclick="switchTab(event, 'order')">
            <i class="fas fa-shopping-cart"></i> New Order
        </button>
        <button class="tab-btn" onclick="switchTab(event, 'history')">
            <i class="fas fa-history"></i> Order History
        </button>
    </div>

    <!-- Tab: New Order -->
    <div id="order" class="tab-content active">
        <div style="display: grid; grid-template-columns: 2fr 1fr; gap: 25px;">
            <!-- Products -->
            <div>
                <h3 style="margin-bottom: 20px;">Available Products</h3>
                <div class="products-grid">
                    <asp:Repeater ID="rptProducts" runat="server">
                        <ItemTemplate>
                            <div class="product-card">
                                <div class="product-name"><%# Eval("Name") %></div>
                                <div class="product-price">฿<%# Eval("Price", "{0:N0}") %></div>
                                <div class="product-stock">
                                    Stock: <%# Eval("Quantity") %> available
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
                                    <i class="fas fa-plus"></i> Add to Cart
                                </button>
                            </div>
                        </ItemTemplate>
                    </asp:Repeater>
                </div>
            </div>

            <!-- Cart Summary -->
            <div>
                <div class="cart-summary">
                    <h3><i class="fas fa-shopping-cart"></i> Your Cart</h3>
                    <div id="cartItems"></div>
                    <div class="cart-total">
                        Total: ฿<span id="cartTotal">0</span>
                    </div>

                    <div class="form-group">
                        <label class="form-label">Delivery Instructions</label>
                        <asp:TextBox ID="txtDeliveryInstructions" runat="server" CssClass="form-control"
                            TextMode="MultiLine" Rows="3"
                            placeholder="e.g., Please knock softly, baby sleeping"></asp:TextBox>
                    </div>

                    <div class="form-group">
                        <label class="form-label">Payment Method</label>
                        <div class="payment-methods">
                            <label class="payment-option" onclick="selectPayment(this, 'CHARGE_TO_ROOM')">
                                <input type="radio" name="paymentMethod" value="CHARGE_TO_ROOM" checked />
                                <i class="fas fa-door-open"></i> Charge to Room
                            </label>
                            <label class="payment-option" onclick="selectPayment(this, 'TRANSFER')">
                                <input type="radio" name="paymentMethod" value="TRANSFER" />
                                <i class="fas fa-money-check-alt"></i> Bank Transfer
                            </label>
                        </div>
                    </div>

                    <div id="transferSection" style="display: none;" class="form-group">
                        <label class="form-label">Upload Payment Slip</label>
                        <asp:FileUpload ID="filePaymentSlip" runat="server" CssClass="form-control" accept="image/*" />
                    </div>

                    <asp:HiddenField ID="hfCartItems" runat="server" />

                    <asp:Button ID="btnPlaceOrder" runat="server" Text="Place Order"
                        CssClass="btn-place-order" OnClick="btnPlaceOrder_Click"
                        OnClientClick="return validateOrder();" />
                </div>
            </div>
        </div>
    </div>

    <!-- Tab: Order History -->
    <div id="history" class="tab-content">
        <div class="orders-list">
            <h3 style="margin-bottom: 20px;"><i class="fas fa-history"></i> ประวัติการสั่งซื้อ</h3>
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

    <script>
        let cart = [];

        function changeQty(btn, delta) {
            const input = btn.parentElement.querySelector('.qty-input');
            const newValue = parseInt(input.value) + delta;
            const max = parseInt(input.max);
            if (newValue >= 1 && newValue <= max) {
                input.value = newValue;
            }
        }

        function addToCart(btn, productId, productName, price) {
            const card = btn.closest('.product-card');
            const qtyInput = card.querySelector('.qty-input');
            const quantity = parseInt(qtyInput.value);

            // Check if product already in cart
            const existingIndex = cart.findIndex(item => item.productId === productId);
            if (existingIndex !== -1) {
                cart[existingIndex].quantity += quantity;
            } else {
                cart.push({ productId, productName, price, quantity });
            }

            updateCartDisplay();
            qtyInput.value = 1; // Reset quantity

            // Show feedback
            btn.textContent = '✓ Added';
            setTimeout(() => {
                btn.innerHTML = '<i class="fas fa-plus"></i> Add to Cart';
            }, 1000);
        }

        function removeFromCart(index) {
            cart.splice(index, 1);
            updateCartDisplay();
        }

        function updateCartDisplay() {
            const cartItems = document.getElementById('cartItems');
            const cartTotal = document.getElementById('cartTotal');

            if (cart.length === 0) {
                cartItems.innerHTML = '<div class="empty-cart"><i class="fas fa-shopping-cart"></i><p>Cart is empty</p></div>';
                cartTotal.textContent = '0';
                document.getElementById('<%= btnPlaceOrder.ClientID %>').disabled = true;
                return;
            }

            let html = '';
            let total = 0;

            cart.forEach((item, index) => {
                const subtotal = item.price * item.quantity;
                total += subtotal;
                html += `
                    <div class="cart-item">
                        <div class="cart-item-name">${item.productName}</div>
                        <div class="cart-item-qty">x${item.quantity}</div>
                        <div class="cart-item-price">฿${subtotal.toLocaleString()}</div>
                        <button type="button" onclick="removeFromCart(${index})"
                                style="border: none; background: none; color: #f44336; cursor: pointer;">
                            <i class="fas fa-times"></i>
                        </button>
                    </div>
                `;
            });

            cartItems.innerHTML = html;
            cartTotal.textContent = total.toLocaleString();
            document.getElementById('<%= btnPlaceOrder.ClientID %>').disabled = false;

            // Update hidden field
            document.getElementById('<%= hfCartItems.ClientID %>').value = JSON.stringify(cart);
        }

        function selectPayment(element, method) {
            document.querySelectorAll('.payment-option').forEach(opt => {
                opt.classList.remove('selected');
            });
            element.classList.add('selected');

            const transferSection = document.getElementById('transferSection');
            if (method === 'TRANSFER') {
                transferSection.style.display = 'block';
            } else {
                transferSection.style.display = 'none';
            }
        }

        function switchTab(evt, tabName) {
            const tabs = document.querySelectorAll('.tab-content');
            tabs.forEach(tab => tab.classList.remove('active'));

            const btns = document.querySelectorAll('.tab-btn');
            btns.forEach(btn => btn.classList.remove('active'));

            document.getElementById(tabName).classList.add('active');
            evt.currentTarget.classList.add('active');
        }

        function validateOrder() {
            if (cart.length === 0) {
                alert('Please add items to your cart');
                return false;
            }
            return true;
        }

        // Initialize
        document.addEventListener('DOMContentLoaded', function () {
            updateCartDisplay();
        });
    </script>
</asp:Content>
