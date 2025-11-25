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
            <h3 style="margin-bottom: 20px;">Your Orders</h3>
            <asp:Repeater ID="rptOrders" runat="server">
                <ItemTemplate>
                    <div class="order-card">
                        <div class="order-header">
                            <div>
                                <div class="order-number">Order #<%# Eval("Order_Number") %></div>
                                <div style="color: #666; font-size: 13px;">
                                    <%# Eval("Order_Date", "{0:dd MMM yyyy HH:mm}") %>
                                </div>
                            </div>
                            <span class="order-status status-<%# Eval("Order_Status") %>">
                                <%# Eval("Order_Status") %>
                            </span>
                        </div>
                        <div style="margin-bottom: 10px;">
                            <strong>Total:</strong> ฿<%# Eval("Total_Amount", "{0:N0}") %>
                        </div>
                        <div style="margin-bottom: 10px;">
                            <strong>Payment:</strong> <%# Eval("Payment_Method") %>
                            (<%# Eval("Payment_Status") %>)
                        </div>
                        <div style="font-size: 13px; color: #666;">
                            <i class="fas fa-comment"></i> <%# Eval("Delivery_Instructions") %>
                        </div>
                    </div>
                </ItemTemplate>
            </asp:Repeater>

            <asp:Label ID="lblNoOrders" runat="server" Visible="false"
                Text="<div class='empty-cart'><i class='fas fa-inbox'></i><p>No orders yet</p></div>"></asp:Label>
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
