<%@ Page Title="Guest Portal" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Portal.aspx.cs" Inherits="Take_Time_BangPhra.Guest.Portal" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .portal-container {
            max-width: 500px;
            margin: 50px auto;
            padding: 0;
        }

        .portal-header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 40px 30px;
            text-align: center;
            border-radius: 15px 15px 0 0;
        }

        .portal-header h2 {
            margin: 0 0 10px 0;
            font-size: 32px;
            font-weight: 700;
        }

        .portal-header p {
            margin: 0;
            opacity: 0.95;
            font-size: 16px;
        }

        .portal-icon {
            font-size: 60px;
            margin-bottom: 20px;
            opacity: 0.9;
        }

        .portal-body {
            background: white;
            padding: 40px 30px;
            border-radius: 0 0 15px 15px;
            box-shadow: 0 10px 40px rgba(0,0,0,0.1);
        }

        .form-group {
            margin-bottom: 25px;
        }

        .form-label {
            display: block;
            margin-bottom: 8px;
            font-weight: 600;
            color: #333;
            font-size: 14px;
        }

        .form-control {
            width: 100%;
            padding: 14px 18px;
            border: 2px solid #e0e0e0;
            border-radius: 10px;
            font-size: 16px;
            transition: all 0.3s ease;
            box-sizing: border-box;
        }

        .form-control:focus {
            outline: none;
            border-color: #667eea;
            box-shadow: 0 0 0 3px rgba(102, 126, 234, 0.1);
        }

        .btn-verify {
            width: 100%;
            background: linear-gradient(135deg, #667eea, #764ba2);
            color: white;
            border: none;
            padding: 16px;
            border-radius: 10px;
            font-size: 18px;
            font-weight: 600;
            cursor: pointer;
            transition: all 0.3s ease;
            margin-top: 10px;
        }

        .btn-verify:hover {
            transform: translateY(-2px);
            box-shadow: 0 8px 20px rgba(102, 126, 234, 0.3);
        }

        .btn-verify:active {
            transform: translateY(0);
        }

        .alert {
            padding: 15px 20px;
            border-radius: 10px;
            margin-bottom: 25px;
            font-size: 14px;
            display: none;
        }

        .alert-error {
            background: #ffebee;
            color: #c62828;
            border-left: 4px solid #f44336;
        }

        .alert-success {
            background: #e8f5e9;
            color: #2e7d32;
            border-left: 4px solid #4CAF50;
        }

        .info-box {
            background: #f8f9fa;
            border-left: 4px solid #667eea;
            padding: 20px;
            border-radius: 8px;
            margin-top: 25px;
        }

        .info-box h4 {
            margin: 0 0 10px 0;
            color: #667eea;
            font-size: 16px;
            font-weight: 600;
        }

        .info-box ul {
            margin: 0;
            padding-left: 20px;
        }

        .info-box li {
            margin: 8px 0;
            color: #666;
            font-size: 14px;
        }

        .qr-info {
            background: #e3f2fd;
            padding: 15px;
            border-radius: 8px;
            margin-bottom: 20px;
            text-align: center;
        }

        .qr-info i {
            color: #1976d2;
            font-size: 40px;
            margin-bottom: 10px;
        }

        .qr-info p {
            margin: 5px 0;
            color: #1565c0;
            font-weight: 500;
        }

        .help-text {
            text-align: center;
            margin-top: 20px;
            color: #999;
            font-size: 13px;
        }

        .help-text a {
            color: #667eea;
            text-decoration: none;
            font-weight: 600;
        }

        .help-text a:hover {
            text-decoration: underline;
        }

        @media (max-width: 600px) {
            .portal-container {
                margin: 20px;
            }

            .portal-header {
                padding: 30px 20px;
            }

            .portal-body {
                padding: 30px 20px;
            }
        }
    </style>

    <div class="portal-container">
        <div class="portal-header">
            <div class="portal-icon">
                <i class="fas fa-door-open"></i>
            </div>
            <h2>Welcome Guest</h2>
            <p>เข้าสู่ระบบแขกผู้เข้าพัก</p>
        </div>

        <div class="portal-body">
            <!-- Alert Messages -->
            <asp:Panel ID="pnlAlert" runat="server" Visible="false" CssClass="alert">
                <asp:Label ID="lblAlert" runat="server"></asp:Label>
            </asp:Panel>

            <!-- QR Code Info (if scanned) -->
            <asp:Panel ID="pnlQRInfo" runat="server" Visible="false" CssClass="qr-info">
                <i class="fas fa-qrcode"></i>
                <p><strong>QR Code Verified</strong></p>
                <p>Please enter your mobile phone number to continue</p>
            </asp:Panel>

            <!-- Verification Form -->
            <div class="form-group">
                <label class="form-label">
                    <i class="fas fa-mobile-alt"></i> เบอร์มือถือ (Mobile Phone)
                </label>
                <asp:TextBox ID="txtMobilePhone" runat="server" CssClass="form-control"
                    placeholder="0xx-xxx-xxxx" MaxLength="20" required></asp:TextBox>
            </div>

            <asp:Button ID="btnVerify" runat="server" Text="เข้าสู่ระบบ (Login)"
                CssClass="btn-verify" OnClick="btnVerify_Click" />

            <!-- Info Box -->
            <div class="info-box">
                <h4><i class="fas fa-info-circle"></i> How to use Guest Portal</h4>
                <ul>
                    <li>Scan QR code in your room</li>
                    <li>Enter your registered mobile phone number</li>
                    <li>Access room service, housekeeping, and more</li>
                </ul>
            </div>

            <!-- Help Text -->
            <div class="help-text">
                Need help? <a href="~/Contact" runat="server">Contact Front Desk</a>
            </div>
        </div>
    </div>

    <script>
        // Show alert if exists
        window.addEventListener('load', function() {
            var alert = document.querySelector('.alert');
            if (alert && alert.style.display !== 'none') {
                alert.style.display = 'block';
            }
        });

        // Format phone number as user types
        document.addEventListener('DOMContentLoaded', function() {
            var phoneInput = document.getElementById('<%= txtMobilePhone.ClientID %>');
            if (phoneInput) {
                phoneInput.addEventListener('input', function(e) {
                    var value = e.target.value.replace(/\D/g, '');
                    e.target.value = value;
                });
            }
        });
    </script>
</asp:Content>
