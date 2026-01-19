<%@ Page Title="Affiliate Login" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Login.aspx.cs" Inherits="Take_Time_BangPhra.Affiliate.Login" %>
<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .affiliate-login-page {
            min-height: 80vh;
            display: flex;
            align-items: center;
            justify-content: center;
            padding: 40px 20px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
        }

        .login-container {
            width: 100%;
            max-width: 450px;
        }

        .login-card {
            background: white;
            border-radius: 20px;
            box-shadow: 0 20px 60px rgba(0,0,0,0.3);
            overflow: hidden;
        }

        .login-header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 40px 30px;
            text-align: center;
        }

        .login-header .icon {
            width: 80px;
            height: 80px;
            background: rgba(255,255,255,0.2);
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            margin: 0 auto 20px;
            font-size: 40px;
        }

        .login-header h1 {
            margin: 0;
            font-size: 28px;
            font-weight: 700;
        }

        .login-header p {
            margin: 10px 0 0;
            opacity: 0.9;
            font-size: 14px;
        }

        .login-body {
            padding: 40px 30px;
        }

        .form-group {
            margin-bottom: 25px;
        }

        .form-group label {
            display: block;
            margin-bottom: 8px;
            font-weight: 600;
            color: #333;
            font-size: 14px;
        }

        .form-group label i {
            margin-right: 8px;
            color: #667eea;
        }

        .form-control {
            width: 100%;
            padding: 15px 20px;
            border: 2px solid #e0e0e0;
            border-radius: 10px;
            font-size: 16px;
            transition: all 0.3s ease;
            box-sizing: border-box;
        }

        .form-control:focus {
            border-color: #667eea;
            outline: none;
            box-shadow: 0 0 0 3px rgba(102, 126, 234, 0.1);
        }

        .form-control::placeholder {
            color: #aaa;
        }

        .btn-login {
            width: 100%;
            padding: 15px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            border: none;
            border-radius: 10px;
            color: white;
            font-size: 16px;
            font-weight: 600;
            cursor: pointer;
            transition: all 0.3s ease;
        }

        .btn-login:hover {
            transform: translateY(-2px);
            box-shadow: 0 10px 30px rgba(102, 126, 234, 0.4);
        }

        .login-footer {
            text-align: center;
            padding: 25px 30px;
            background: #f8f9fa;
            border-top: 1px solid #eee;
        }

        .login-footer p {
            margin: 0;
            color: #666;
            font-size: 14px;
        }

        .login-footer a {
            color: #667eea;
            text-decoration: none;
            font-weight: 600;
        }

        .login-footer a:hover {
            text-decoration: underline;
        }

        .features {
            display: grid;
            grid-template-columns: repeat(3, 1fr);
            gap: 15px;
            margin-top: 30px;
        }

        .feature-item {
            text-align: center;
            padding: 15px;
            background: rgba(255,255,255,0.1);
            border-radius: 10px;
        }

        .feature-item i {
            font-size: 24px;
            margin-bottom: 8px;
            display: block;
        }

        .feature-item span {
            font-size: 12px;
            opacity: 0.9;
        }

        .alert-message {
            padding: 12px 15px;
            border-radius: 8px;
            margin-bottom: 20px;
            font-size: 14px;
        }

        .alert-success {
            background: #d4edda;
            color: #155724;
            border: 1px solid #c3e6cb;
        }

        .alert-error {
            background: #f8d7da;
            color: #721c24;
            border: 1px solid #f5c6cb;
        }

        @media (max-width: 480px) {
            .affiliate-login-page {
                padding: 20px 15px;
            }
            .login-header {
                padding: 30px 20px;
            }
            .login-body {
                padding: 30px 20px;
            }
            .features {
                grid-template-columns: 1fr;
            }
        }
    </style>

    <div class="affiliate-login-page">
        <div class="login-container">
            <div class="login-card">
                <div class="login-header">
                    <div class="icon">
                        <i class="fas fa-handshake"></i>
                    </div>
                    <h1>Affiliate Portal</h1>
                    <p>Take Time BangPhra Partner Program</p>
                </div>

                <div class="login-body">
                    <asp:Panel ID="pnlMessage" runat="server" Visible="false">
                        <div class="alert-message alert-success">
                            <i class="fas fa-check-circle"></i>
                            <asp:Label ID="lblMessage" runat="server"></asp:Label>
                        </div>
                    </asp:Panel>

                    <div class="form-group">
                        <label><i class="fas fa-user"></i>Username</label>
                        <asp:TextBox ID="TextBox1" runat="server" CssClass="form-control"
                            placeholder="รหัสบัตรประชาชน หรือ รหัส Coupon"></asp:TextBox>
                    </div>

                    <div class="form-group">
                        <label><i class="fas fa-lock"></i>Password</label>
                        <asp:TextBox ID="TextBox2" runat="server" TextMode="Password"
                            CssClass="form-control" placeholder="รหัสผ่าน"></asp:TextBox>
                    </div>

                    <asp:Button ID="Button1" runat="server" Text="เข้าสู่ระบบ"
                        CssClass="btn-login" OnClick="Button1_Click" />
                </div>

                <div class="login-footer">
                    <p>ยังไม่มีบัญชี? <a href="Register.aspx">สมัครสมาชิก Affiliate</a></p>
                </div>
            </div>

            <div class="features">
                <div class="feature-item">
                    <i class="fas fa-percent"></i>
                    <span>รับค่าคอมมิชชั่น</span>
                </div>
                <div class="feature-item">
                    <i class="fas fa-chart-line"></i>
                    <span>ติดตามรายได้</span>
                </div>
                <div class="feature-item">
                    <i class="fas fa-money-bill-wave"></i>
                    <span>ถอนเงินง่าย</span>
                </div>
            </div>
        </div>
    </div>
</asp:Content>
