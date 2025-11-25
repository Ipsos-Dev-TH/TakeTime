<%@ Page Title="Guest Dashboard" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Dashboard.aspx.cs" Inherits="Take_Time_BangPhra.Guest.Dashboard" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .dashboard-header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 30px;
            border-radius: 15px;
            margin-bottom: 30px;
        }

        .dashboard-header h2 {
            margin: 0 0 5px 0;
            font-size: 28px;
            font-weight: 700;
        }

        .dashboard-header .room-info {
            display: flex;
            gap: 20px;
            margin-top: 15px;
            flex-wrap: wrap;
        }

        .room-info-item {
            display: flex;
            align-items: center;
            gap: 8px;
            font-size: 14px;
            opacity: 0.95;
        }

        .room-info-item i {
            font-size: 16px;
        }

        .stats-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 20px;
            margin-bottom: 30px;
        }

        .stat-card {
            background: white;
            padding: 20px;
            border-radius: 12px;
            box-shadow: 0 3px 10px rgba(0,0,0,0.1);
            border-left: 4px solid #667eea;
        }

        .stat-card h4 {
            margin: 0 0 5px 0;
            font-size: 14px;
            color: #666;
            font-weight: 500;
        }

        .stat-card .value {
            font-size: 28px;
            font-weight: 700;
            color: #333;
        }

        .stat-card .unit {
            font-size: 16px;
            color: #999;
            font-weight: 400;
        }

        .services-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
            gap: 20px;
            margin-bottom: 30px;
        }

        .service-card {
            background: white;
            border-radius: 15px;
            padding: 30px;
            text-align: center;
            box-shadow: 0 5px 20px rgba(0,0,0,0.08);
            transition: all 0.3s ease;
            cursor: pointer;
            text-decoration: none;
            display: block;
        }

        .service-card:hover {
            transform: translateY(-8px);
            box-shadow: 0 10px 30px rgba(0,0,0,0.15);
            text-decoration: none;
        }

        .service-icon {
            width: 80px;
            height: 80px;
            margin: 0 auto 20px;
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 36px;
            background: linear-gradient(135deg, #667eea, #764ba2);
            color: white;
            box-shadow: 0 4px 15px rgba(102, 126, 234, 0.3);
        }

        .service-card:nth-child(2) .service-icon {
            background: linear-gradient(135deg, #f093fb, #f5576c);
        }

        .service-card:nth-child(3) .service-icon {
            background: linear-gradient(135deg, #4facfe, #00f2fe);
        }

        .service-card:nth-child(4) .service-icon {
            background: linear-gradient(135deg, #43e97b, #38f9d7);
        }

        .service-card:nth-child(5) .service-icon {
            background: linear-gradient(135deg, #fa709a, #fee140);
        }

        .service-card:nth-child(6) .service-icon {
            background: linear-gradient(135deg, #30cfd0, #330867);
        }

        .service-card h3 {
            margin: 0 0 10px 0;
            font-size: 20px;
            font-weight: 600;
            color: #333;
        }

        .service-card p {
            margin: 0;
            color: #666;
            font-size: 14px;
            line-height: 1.5;
        }

        .logout-btn {
            background: linear-gradient(135deg, #D32F2F, #B71C1C);
            color: white;
            border: none;
            padding: 12px 30px;
            border-radius: 25px;
            font-weight: 600;
            cursor: pointer;
            transition: all 0.3s ease;
            margin-top: 20px;
        }

        .logout-btn:hover {
            transform: translateY(-2px);
            box-shadow: 0 5px 15px rgba(211, 47, 47, 0.3);
        }

        .welcome-message {
            background: white;
            padding: 25px;
            border-radius: 12px;
            margin-bottom: 30px;
            box-shadow: 0 3px 10px rgba(0,0,0,0.05);
            border-left: 4px solid #4CAF50;
        }

        .welcome-message h3 {
            margin: 0 0 10px 0;
            color: #2e7d32;
            font-size: 18px;
        }

        .welcome-message p {
            margin: 0;
            color: #666;
            line-height: 1.6;
        }

        @media (max-width: 768px) {
            .services-grid {
                grid-template-columns: 1fr;
            }

            .stats-grid {
                grid-template-columns: 1fr;
            }

            .room-info {
                flex-direction: column;
                gap: 10px !important;
            }
        }
    </style>

    <!-- Dashboard Header -->
    <div class="dashboard-header">
        <h2>
            <i class="fas fa-home"></i>
            Welcome, <asp:Label ID="lblGuestName" runat="server"></asp:Label>
        </h2>
        <div class="room-info">
            <div class="room-info-item">
                <i class="fas fa-door-open"></i>
                <span>Room: <strong><asp:Label ID="lblRoomName" runat="server"></asp:Label></strong></span>
            </div>
            <div class="room-info-item">
                <i class="fas fa-calendar-check"></i>
                <span>Check-in: <strong><asp:Label ID="lblCheckIn" runat="server"></asp:Label></strong></span>
            </div>
            <div class="room-info-item">
                <i class="fas fa-calendar-times"></i>
                <span>Check-out: <strong><asp:Label ID="lblCheckOut" runat="server"></asp:Label></strong></span>
            </div>
        </div>
    </div>

    <!-- Welcome Message -->
    <div class="welcome-message">
        <h3><i class="fas fa-info-circle"></i> Welcome to Take Time Nature Resort!</h3>
        <p>
            ยินดีต้อนรับสู่รีสอร์ท! ท่านสามารถใช้งานระบบนี้เพื่อสั่งอาหาร/เครื่องดื่ม, ขอบริการทำความสะอาด,
            จองกิจกรรม, ตรวจสอบยอดชำระเงิน และติดต่อเจ้าหน้าที่ได้ตลอด 24 ชั่วโมง
        </p>
    </div>

    <!-- Quick Stats -->
    <div class="stats-grid">
        <div class="stat-card">
            <h4>Balance Due</h4>
            <div class="value">฿<asp:Label ID="lblBalanceDue" runat="server">0</asp:Label></div>
        </div>
        <div class="stat-card">
            <h4>Loyalty Points</h4>
            <div class="value"><asp:Label ID="lblLoyaltyPoints" runat="server">0</asp:Label> <span class="unit">pts</span></div>
        </div>
        <div class="stat-card">
            <h4>Room Service Orders</h4>
            <div class="value"><asp:Label ID="lblRSOrders" runat="server">0</asp:Label></div>
        </div>
        <div class="stat-card">
            <h4>Pending Requests</h4>
            <div class="value"><asp:Label ID="lblPendingRequests" runat="server">0</asp:Label></div>
        </div>
    </div>

    <!-- Services Grid -->
    <div class="services-grid">
        <a href="RoomService.aspx" class="service-card">
            <div class="service-icon">
                <i class="fas fa-utensils"></i>
            </div>
            <h3>Room Service</h3>
            <p>สั่งอาหาร เครื่องดื่ม และสินค้าต่างๆ ส่งถึงห้อง</p>
        </a>

        <a href="Housekeeping.aspx" class="service-card">
            <div class="service-icon">
                <i class="fas fa-broom"></i>
            </div>
            <h3>Housekeeping</h3>
            <p>ขอบริการทำความสะอาด ผ้าเช็ดตัว หรือของใช้ในห้อง</p>
        </a>

        <a href="Concierge.aspx" class="service-card">
            <div class="service-icon">
                <i class="fas fa-concierge-bell"></i>
            </div>
            <h3>Concierge Services</h3>
            <p>จองทัวร์ สปา ร้านอาหาร และบริการอื่นๆ</p>
        </a>

        <a href="Balance.aspx" class="service-card">
            <div class="service-icon">
                <i class="fas fa-credit-card"></i>
            </div>
            <h3>Check Balance & Pay</h3>
            <p>ตรวจสอบยอดค่าใช้จ่ายและชำระเงิน</p>
        </a>

        <a href="Review.aspx" class="service-card">
            <div class="service-icon">
                <i class="fas fa-star"></i>
            </div>
            <h3>Review & Feedback</h3>
            <p>แสดงความคิดเห็นและรีวิวประสบการณ์การพัก</p>
        </a>

        <a href="Chat.aspx" class="service-card">
            <div class="service-icon">
                <i class="fas fa-comments"></i>
            </div>
            <h3>Chat with Front Desk</h3>
            <p>สนทนากับเจ้าหน้าที่แผนกต้อนรับ</p>
        </a>
    </div>

    <!-- Logout Button -->
    <div style="text-align: center;">
        <asp:Button ID="btnLogout" runat="server" Text="Logout / ออกจากระบบ"
            CssClass="logout-btn" OnClick="btnLogout_Click" />
    </div>
</asp:Content>
