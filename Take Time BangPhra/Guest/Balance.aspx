<%@ Page Title="Check Balance & Pay" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Balance.aspx.cs" Inherits="Take_Time_BangPhra.Guest.Balance" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .balance-header {
            background: linear-gradient(135deg, #43e97b 0%, #38f9d7 100%);
            color: white;
            padding: 25px;
            border-radius: 12px;
            margin-bottom: 25px;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .balance-header h2 {
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
        }

        .balance-summary {
            background: white;
            border-radius: 12px;
            padding: 30px;
            box-shadow: 0 5px 20px rgba(0,0,0,0.1);
            margin-bottom: 25px;
        }

        .charges-grid {
            display: grid;
            gap: 15px;
            margin-bottom: 25px;
        }

        .charge-item {
            display: flex;
            justify-content: space-between;
            padding: 15px;
            background: #f8f9fa;
            border-radius: 8px;
        }

        .charge-label {
            font-weight: 500;
            color: #555;
        }

        .charge-amount {
            font-weight: 700;
            color: #333;
            font-size: 18px;
        }

        .total-row {
            border-top: 3px solid #43e97b;
            padding: 20px 15px;
            margin-top: 10px;
        }

        .total-row .charge-label {
            font-size: 20px;
            font-weight: 700;
            color: #333;
        }

        .total-row .charge-amount {
            font-size: 28px;
            color: #43e97b;
        }

        .balance-due-card {
            background: linear-gradient(135deg, #667eea, #764ba2);
            color: white;
            padding: 25px;
            border-radius: 12px;
            text-align: center;
            margin-bottom: 25px;
        }

        .balance-due-card h3 {
            margin: 0 0 10px 0;
            font-size: 16px;
            opacity: 0.9;
        }

        .balance-due-card .amount {
            font-size: 48px;
            font-weight: 700;
            margin: 0;
        }

        .payment-section {
            background: white;
            border-radius: 12px;
            padding: 30px;
            box-shadow: 0 3px 10px rgba(0,0,0,0.1);
        }

        .payment-section h3 {
            margin: 0 0 20px 0;
            color: #333;
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
            box-sizing: border-box;
        }

        .form-control:focus {
            outline: none;
            border-color: #43e97b;
        }

        .btn-pay {
            width: 100%;
            background: linear-gradient(135deg, #43e97b, #38f9d7);
            color: white;
            border: none;
            padding: 15px;
            border-radius: 10px;
            font-size: 18px;
            font-weight: 700;
            cursor: pointer;
        }

        .btn-pay:hover {
            transform: translateY(-2px);
            box-shadow: 0 6px 20px rgba(67, 233, 123, 0.3);
        }

        .info-box {
            background: #e3f2fd;
            border-left: 4px solid #2196F3;
            padding: 15px;
            border-radius: 8px;
            margin-top: 20px;
        }

        .info-box h4 {
            margin: 0 0 10px 0;
            color: #1565c0;
        }

        .info-box p {
            margin: 5px 0;
            color: #555;
            font-size: 14px;
        }
    </style>

    <!-- Header -->
    <div class="balance-header">
        <h2><i class="fas fa-credit-card"></i> Check Balance & Pay</h2>
        <a href="Dashboard.aspx" class="btn-back">
            <i class="fas fa-arrow-left"></i> Back
        </a>
    </div>

    <!-- Balance Due Highlight -->
    <div class="balance-due-card">
        <h3>Balance Due</h3>
        <div class="amount">฿<asp:Label ID="lblBalanceDue" runat="server">0</asp:Label></div>
    </div>

    <!-- Charges Breakdown -->
    <div class="balance-summary">
        <h3 style="margin-bottom: 20px;"><i class="fas fa-file-invoice-dollar"></i> Charges Breakdown</h3>

        <div class="charges-grid">
            <div class="charge-item">
                <div class="charge-label">
                    <i class="fas fa-bed"></i> Room Charge
                </div>
                <div class="charge-amount">฿<asp:Label ID="lblRoomCharge" runat="server">0</asp:Label></div>
            </div>

            <div class="charge-item">
                <div class="charge-label">
                    <i class="fas fa-concierge-bell"></i> Room Service
                </div>
                <div class="charge-amount">฿<asp:Label ID="lblRoomServiceCharge" runat="server">0</asp:Label></div>
            </div>

            <div class="charge-item total-row">
                <div class="charge-label">Total Charges</div>
                <div class="charge-amount">฿<asp:Label ID="lblTotalCharges" runat="server">0</asp:Label></div>
            </div>

            <div class="charge-item">
                <div class="charge-label">
                    <i class="fas fa-check-circle"></i> Deposit Paid
                </div>
                <div class="charge-amount" style="color: #4CAF50;">-฿<asp:Label ID="lblDepositPaid" runat="server">0</asp:Label></div>
            </div>
        </div>

        <div style="text-align: center; margin-top: 20px; color: #666; font-size: 13px;">
            <i class="fas fa-info-circle"></i> Check-in: <strong><asp:Label ID="lblCheckIn" runat="server"></asp:Label></strong> |
            Check-out: <strong><asp:Label ID="lblCheckOut" runat="server"></asp:Label></strong>
        </div>
    </div>

    <!-- Payment Section -->
    <asp:Panel ID="pnlPayment" runat="server" Visible="false">
        <div class="payment-section">
            <h3><i class="fas fa-money-check-alt"></i> Make a Payment</h3>

            <div class="form-group">
                <label class="form-label">Amount to Pay (฿)</label>
                <asp:TextBox ID="txtPaymentAmount" runat="server" CssClass="form-control"
                    TextMode="Number" step="0.01" placeholder="Enter amount"></asp:TextBox>
            </div>

            <div class="form-group">
                <label class="form-label">Upload Payment Slip *</label>
                <asp:FileUpload ID="filePaymentSlip" runat="server" CssClass="form-control" accept="image/*" />
            </div>

            <div class="form-group">
                <label class="form-label">Note (Optional)</label>
                <asp:TextBox ID="txtPaymentNote" runat="server" CssClass="form-control"
                    TextMode="MultiLine" Rows="2" placeholder="e.g., Bank transfer ref number"></asp:TextBox>
            </div>

            <asp:Button ID="btnSubmitPayment" runat="server" Text="Submit Payment"
                CssClass="btn-pay" OnClick="btnSubmitPayment_Click" />

            <div class="info-box">
                <h4><i class="fas fa-university"></i> Bank Transfer Information</h4>
                <p><strong>Bank:</strong> Bangkok Bank</p>
                <p><strong>Account Name:</strong> Take Time Nature Resort</p>
                <p><strong>Account Number:</strong> 123-456-7890</p>
                <p><strong>Note:</strong> Please upload your payment slip after transfer. Payment will be verified by staff.</p>
            </div>
        </div>
    </asp:Panel>

    <!-- Paid in Full Message -->
    <asp:Panel ID="pnlPaidInFull" runat="server" Visible="false">
        <div style="background: #e8f5e9; border: 2px solid #4CAF50; border-radius: 12px; padding: 30px; text-align: center;">
            <i class="fas fa-check-circle" style="font-size: 60px; color: #4CAF50; margin-bottom: 15px;"></i>
            <h3 style="margin: 0; color: #2e7d32;">Paid in Full</h3>
            <p style="margin: 10px 0 0 0; color: #555;">All charges have been settled. Thank you!</p>
        </div>
    </asp:Panel>
</asp:Content>
