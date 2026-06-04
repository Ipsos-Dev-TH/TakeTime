<%@ Page Title="Guest Dashboard" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Dashboard.aspx.cs" Inherits="Take_Time_BangPhra.Guest.Dashboard" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        /* Main Container */
        .guest-portal {
            max-width: 1200px;
            margin: 0 auto;
            padding: 15px;
            padding-bottom: 100px; /* Space for bottom nav */
        }

        /* Dashboard Header */
        .dashboard-header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 25px;
            border-radius: 20px;
            margin-bottom: 20px;
            position: relative;
            overflow: hidden;
        }

        .dashboard-header::before {
            content: '';
            position: absolute;
            top: -50%;
            right: -50%;
            width: 100%;
            height: 200%;
            background: radial-gradient(circle, rgba(255,255,255,0.1) 0%, transparent 60%);
        }

        .header-content {
            position: relative;
            z-index: 1;
        }

        .dashboard-header h2 {
            margin: 0 0 5px 0;
            font-size: 22px;
            font-weight: 700;
        }

        .room-badge {
            display: inline-flex;
            align-items: center;
            gap: 8px;
            background: rgba(255,255,255,0.2);
            padding: 8px 15px;
            border-radius: 20px;
            font-size: 14px;
            margin-top: 10px;
        }

        .room-badge i {
            font-size: 16px;
        }

        .stay-dates {
            display: flex;
            gap: 20px;
            margin-top: 15px;
            font-size: 13px;
            opacity: 0.9;
            flex-wrap: wrap;
        }

        .stay-dates span {
            display: flex;
            align-items: center;
            gap: 5px;
        }

        /* Quick Actions */
        .quick-actions {
            display: flex;
            gap: 10px;
            margin-bottom: 20px;
            overflow-x: auto;
            padding: 5px 0;
            -webkit-overflow-scrolling: touch;
        }

        .quick-actions::-webkit-scrollbar {
            display: none;
        }

        .quick-action-btn {
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            min-width: 80px;
            padding: 15px 10px;
            background: white;
            border-radius: 15px;
            text-decoration: none;
            box-shadow: 0 3px 10px rgba(0,0,0,0.08);
            transition: all 0.3s ease;
            flex-shrink: 0;
        }

        .quick-action-btn:hover {
            transform: translateY(-3px);
            box-shadow: 0 5px 15px rgba(0,0,0,0.12);
            text-decoration: none;
        }

        .quick-action-btn i {
            font-size: 24px;
            margin-bottom: 8px;
        }

        .quick-action-btn span {
            font-size: 11px;
            color: #333;
            font-weight: 500;
            text-align: center;
        }

        .quick-action-btn.primary i { color: #667eea; }
        .quick-action-btn.success i { color: #4CAF50; }
        .quick-action-btn.warning i { color: #FF9800; }
        .quick-action-btn.danger i { color: #f44336; }
        .quick-action-btn.info i { color: #2196F3; }

        /* Stats Grid */
        .stats-grid {
            display: grid;
            grid-template-columns: repeat(2, 1fr);
            gap: 12px;
            margin-bottom: 25px;
        }

        .stat-card {
            background: white;
            padding: 18px;
            border-radius: 15px;
            box-shadow: 0 3px 10px rgba(0,0,0,0.08);
            text-align: center;
        }

        .stat-card .icon {
            width: 45px;
            height: 45px;
            margin: 0 auto 10px;
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 20px;
        }

        .stat-card:nth-child(1) .icon { background: #e3f2fd; color: #1976D2; }
        .stat-card:nth-child(2) .icon { background: #fff3e0; color: #F57C00; }
        .stat-card:nth-child(3) .icon { background: #e8f5e9; color: #388E3C; }
        .stat-card:nth-child(4) .icon { background: #fce4ec; color: #C2185B; }

        .stat-card .value {
            font-size: 22px;
            font-weight: 700;
            color: #333;
            margin-bottom: 3px;
        }

        .stat-card .label {
            font-size: 12px;
            color: #888;
        }

        /* Section Title */
        .section-title {
            display: flex;
            align-items: center;
            gap: 10px;
            margin: 25px 0 15px;
            font-size: 16px;
            font-weight: 600;
            color: #333;
        }

        .section-title i {
            color: #667eea;
        }

        /* Services Grid - Compact for Mobile */
        .services-grid {
            display: grid;
            grid-template-columns: repeat(2, 1fr);
            gap: 12px;
            margin-bottom: 20px;
        }

        .service-card {
            background: white;
            border-radius: 15px;
            padding: 20px 15px;
            text-align: center;
            box-shadow: 0 3px 12px rgba(0,0,0,0.08);
            transition: all 0.3s ease;
            text-decoration: none;
            display: block;
        }

        .service-card:hover, .service-card:active {
            transform: translateY(-3px);
            box-shadow: 0 6px 20px rgba(0,0,0,0.12);
            text-decoration: none;
        }

        .service-icon {
            width: 55px;
            height: 55px;
            margin: 0 auto 12px;
            border-radius: 15px;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 24px;
            color: white;
        }

        /* Service Icon Colors */
        .service-icon.food { background: linear-gradient(135deg, #667eea, #764ba2); }
        .service-icon.clean { background: linear-gradient(135deg, #f093fb, #f5576c); }
        .service-icon.concierge { background: linear-gradient(135deg, #4facfe, #00f2fe); }
        .service-icon.payment { background: linear-gradient(135deg, #43e97b, #38f9d7); }
        .service-icon.review { background: linear-gradient(135deg, #fa709a, #fee140); }
        .service-icon.chat { background: linear-gradient(135deg, #30cfd0, #330867); }
        .service-icon.emergency { background: linear-gradient(135deg, #f44336, #c62828); }
        .service-icon.info { background: linear-gradient(135deg, #8bc34a, #558b2f); }
        .service-icon.map { background: linear-gradient(135deg, #00bcd4, #006064); }
        .service-icon.facility { background: linear-gradient(135deg, #9c27b0, #4a148c); }

        .service-card h3 {
            margin: 0 0 5px 0;
            font-size: 14px;
            font-weight: 600;
            color: #333;
        }

        .service-card p {
            margin: 0;
            color: #888;
            font-size: 11px;
            line-height: 1.4;
            display: -webkit-box;
            -webkit-line-clamp: 2;
            -webkit-box-orient: vertical;
            overflow: hidden;
        }

        /* Info Cards */
        .info-cards {
            display: grid;
            grid-template-columns: repeat(4, 1fr);
            gap: 10px;
            margin-bottom: 25px;
        }

        .info-card {
            background: white;
            border-radius: 12px;
            padding: 15px 10px;
            text-align: center;
            box-shadow: 0 2px 8px rgba(0,0,0,0.06);
            text-decoration: none;
            transition: all 0.3s ease;
        }

        .info-card:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(0,0,0,0.1);
            text-decoration: none;
        }

        .info-card i {
            font-size: 22px;
            margin-bottom: 8px;
            display: block;
        }

        .info-card span {
            font-size: 11px;
            color: #555;
            font-weight: 500;
        }

        .info-card.emergency i { color: #f44336; }
        .info-card.about i { color: #8bc34a; }
        .info-card.map i { color: #00bcd4; }
        .info-card.facility i { color: #9c27b0; }

        /* Bottom Navigation */
        .bottom-nav {
            position: fixed;
            bottom: 0;
            left: 0;
            right: 0;
            background: white;
            display: flex;
            justify-content: space-around;
            padding: 10px 0;
            box-shadow: 0 -3px 15px rgba(0,0,0,0.1);
            z-index: 1000;
            border-top: 1px solid #eee;
        }

        .bottom-nav a {
            display: flex;
            flex-direction: column;
            align-items: center;
            text-decoration: none;
            padding: 5px 15px;
            border-radius: 10px;
            transition: all 0.3s ease;
        }

        .bottom-nav a:hover, .bottom-nav a.active {
            background: #f5f5f5;
        }

        .bottom-nav a i {
            font-size: 22px;
            margin-bottom: 4px;
            color: #888;
        }

        .bottom-nav a.active i {
            color: #667eea;
        }

        .bottom-nav a span {
            font-size: 10px;
            color: #888;
            font-weight: 500;
        }

        .bottom-nav a.active span {
            color: #667eea;
        }

        /* Logout Section */
        .logout-section {
            text-align: center;
            padding: 20px 0;
            margin-top: 20px;
            border-top: 1px solid #eee;
        }

        .logout-btn {
            background: linear-gradient(135deg, #ff5252, #d32f2f);
            color: white;
            border: none;
            padding: 12px 35px;
            border-radius: 25px;
            font-weight: 600;
            font-size: 14px;
            cursor: pointer;
            transition: all 0.3s ease;
        }

        .logout-btn:hover {
            transform: translateY(-2px);
            box-shadow: 0 5px 15px rgba(211, 47, 47, 0.3);
        }

        /* Promo Banner */
        .promo-banner {
            background: linear-gradient(135deg, #FF9800, #F57C00);
            color: white;
            padding: 15px 20px;
            border-radius: 15px;
            margin-bottom: 20px;
            display: flex;
            align-items: center;
            gap: 15px;
            text-decoration: none;
            transition: all 0.3s ease;
        }

        .promo-banner:hover {
            transform: translateY(-2px);
            box-shadow: 0 5px 15px rgba(255, 152, 0, 0.3);
            color: white;
            text-decoration: none;
        }

        .promo-banner i {
            font-size: 28px;
        }

        .promo-banner .promo-text h4 {
            margin: 0 0 3px 0;
            font-size: 14px;
            font-weight: 600;
        }

        .promo-banner .promo-text p {
            margin: 0;
            font-size: 12px;
            opacity: 0.9;
        }

        .promo-banner .arrow {
            margin-left: auto;
            font-size: 20px;
        }

        /* Desktop Adjustments */
        @media (min-width: 768px) {
            .guest-portal {
                padding-bottom: 30px;
            }

            .bottom-nav {
                display: none;
            }

            .dashboard-header h2 {
                font-size: 28px;
            }

            .stats-grid {
                grid-template-columns: repeat(4, 1fr);
            }

            .services-grid {
                grid-template-columns: repeat(3, 1fr);
            }

            .info-cards {
                grid-template-columns: repeat(4, 1fr);
            }

            .service-card {
                padding: 25px 20px;
            }

            .service-icon {
                width: 65px;
                height: 65px;
                font-size: 28px;
            }

            .service-card h3 {
                font-size: 16px;
            }

            .service-card p {
                font-size: 13px;
            }
        }

        /* Small Mobile */
        @media (max-width: 380px) {
            .quick-action-btn {
                min-width: 70px;
                padding: 12px 8px;
            }

            .quick-action-btn i {
                font-size: 20px;
            }

            .quick-action-btn span {
                font-size: 10px;
            }

            .service-card {
                padding: 15px 10px;
            }

            .service-icon {
                width: 45px;
                height: 45px;
                font-size: 20px;
            }

            .service-card h3 {
                font-size: 12px;
            }

            .info-cards {
                grid-template-columns: repeat(2, 1fr);
            }
        }
    </style>

    <div class="guest-portal">
        <!-- Dashboard Header -->
        <div class="dashboard-header">
            <div class="header-content">
                <h2>
                    <i class="fas fa-hand-sparkles"></i>
                    สวัสดี, <asp:Label ID="lblGuestName" runat="server"></asp:Label>
                </h2>
                <div class="room-badge">
                    <i class="fas fa-door-open"></i>
                    <strong><asp:Label ID="lblRoomName" runat="server"></asp:Label></strong>
                </div>
                <div class="stay-dates">
                    <span><i class="fas fa-sign-in-alt"></i> <asp:Label ID="lblCheckIn" runat="server"></asp:Label></span>
                    <span><i class="fas fa-sign-out-alt"></i> <asp:Label ID="lblCheckOut" runat="server"></asp:Label></span>
                </div>
            </div>
        </div>

        <!-- Quick Actions -->
        <div class="quick-actions">
            <a href="RoomService.aspx" class="quick-action-btn primary">
                <i class="fas fa-utensils"></i>
                <span>สั่งอาหาร</span>
            </a>
            <a href="Housekeeping.aspx" class="quick-action-btn success">
                <i class="fas fa-broom"></i>
                <span>แม่บ้าน</span>
            </a>
            <a href="Balance.aspx" class="quick-action-btn warning">
                <i class="fas fa-receipt"></i>
                <span>ยอดชำระ</span>
            </a>
            <a href="Emergency.aspx" class="quick-action-btn danger">
                <i class="fas fa-phone-alt"></i>
                <span>ฉุกเฉิน</span>
            </a>
            <a href="Concierge.aspx" class="quick-action-btn info">
                <i class="fas fa-concierge-bell"></i>
                <span>บริการ</span>
            </a>
        </div>

        <!-- Promo Banner -->
        <a href="Review.aspx" class="promo-banner">
            <i class="fas fa-gift"></i>
            <div class="promo-text">
                <h4>รีวิวรับ 100 Points!</h4>
                <p>แชร์ประสบการณ์ของคุณบน Google</p>
            </div>
            <i class="fas fa-chevron-right arrow"></i>
        </a>

        <!-- Stats Grid -->
        <div class="stats-grid">
            <div class="stat-card">
                <div class="icon"><i class="fas fa-wallet"></i></div>
                <div class="value">฿<asp:Label ID="lblBalanceDue" runat="server">0</asp:Label></div>
                <div class="label">ยอดค้างชำระ</div>
            </div>
            <div class="stat-card">
                <div class="icon"><i class="fas fa-coins"></i></div>
                <div class="value"><asp:Label ID="lblLoyaltyPoints" runat="server">0</asp:Label></div>
                <div class="label">แต้มสะสม</div>
            </div>
            <div class="stat-card">
                <div class="icon"><i class="fas fa-shopping-bag"></i></div>
                <div class="value"><asp:Label ID="lblRSOrders" runat="server">0</asp:Label></div>
                <div class="label">ออเดอร์วันนี้</div>
            </div>
            <div class="stat-card">
                <div class="icon"><i class="fas fa-clock"></i></div>
                <div class="value"><asp:Label ID="lblPendingRequests" runat="server">0</asp:Label></div>
                <div class="label">รอดำเนินการ</div>
            </div>
        </div>

        <!-- Services Section -->
        <div class="section-title">
            <i class="fas fa-th-large"></i>
            <span>บริการของเรา</span>
        </div>

        <div class="services-grid">
            <a href="RoomService.aspx" class="service-card">
                <div class="service-icon food">
                    <i class="fas fa-utensils"></i>
                </div>
                <h3>Room Service</h3>
                <p>สั่งอาหาร เครื่องดื่ม ส่งถึงห้อง</p>
            </a>

            <a href="Housekeeping.aspx" class="service-card">
                <div class="service-icon clean">
                    <i class="fas fa-broom"></i>
                </div>
                <h3>Housekeeping</h3>
                <p>ทำความสะอาด ผ้าเช็ดตัว ของใช้</p>
            </a>

            <a href="Concierge.aspx" class="service-card">
                <div class="service-icon concierge">
                    <i class="fas fa-concierge-bell"></i>
                </div>
                <h3>Concierge</h3>
                <p>จองทัวร์ สปา ร้านอาหาร</p>
            </a>

            <a href="Balance.aspx" class="service-card">
                <div class="service-icon payment">
                    <i class="fas fa-credit-card"></i>
                </div>
                <h3>Balance & Pay</h3>
                <p>ตรวจสอบยอดและชำระเงิน</p>
            </a>

            <a href="Activities.aspx" class="service-card">
                <div class="service-icon info">
                    <i class="fas fa-hiking"></i>
                </div>
                <h3>Activities</h3>
                <p>กิจกรรมในและนอกที่พัก</p>
            </a>

            <a href="MyPoints.aspx" class="service-card">
                <div class="service-icon payment">
                    <i class="fas fa-coins"></i>
                </div>
                <h3>คะแนน & รางวัล</h3>
                <p>ดูแต้มสะสมและแลกของรางวัล</p>
            </a>

            <a href="Review.aspx" class="service-card">
                <div class="service-icon review">
                    <i class="fas fa-star"></i>
                </div>
                <h3>Review & Rewards</h3>
                <p>รีวิวสะสมแต้มรับรางวัล</p>
            </a>

            <a href="Chat.aspx" class="service-card">
                <div class="service-icon chat">
                    <i class="fas fa-comments"></i>
                </div>
                <h3>Chat</h3>
                <p>สนทนากับ Front Desk</p>
            </a>
        </div>

        <!-- Info Section -->
        <div class="section-title">
            <i class="fas fa-info-circle"></i>
            <span>ข้อมูลและสถานที่</span>
        </div>

        <div class="info-cards">
            <a href="Emergency.aspx" class="info-card emergency">
                <i class="fas fa-phone-alt"></i>
                <span>ฉุกเฉิน</span>
            </a>
            <a href="AboutUs.aspx" class="info-card about">
                <i class="fas fa-hotel"></i>
                <span>เกี่ยวกับเรา</span>
            </a>
            <a href="NearbyPlaces.aspx" class="info-card map">
                <i class="fas fa-map-marked-alt"></i>
                <span>สถานที่ใกล้เคียง</span>
            </a>
            <a href="Facilities.aspx" class="info-card facility">
                <i class="fas fa-swimming-pool"></i>
                <span>สิ่งอำนวยความสะดวก</span>
            </a>
        </div>

        <!-- Logout -->
        <div class="logout-section">
            <asp:Button ID="btnLogout" runat="server" Text="ออกจากระบบ"
                CssClass="logout-btn" OnClick="btnLogout_Click" />
        </div>
    </div>

    <!-- Bottom Navigation (Mobile) -->
    <div class="bottom-nav">
        <a href="Dashboard.aspx" class="active">
            <i class="fas fa-home"></i>
            <span>หน้าหลัก</span>
        </a>
        <a href="RoomService.aspx">
            <i class="fas fa-utensils"></i>
            <span>สั่งอาหาร</span>
        </a>
        <a href="Activities.aspx">
            <i class="fas fa-hiking"></i>
            <span>กิจกรรม</span>
        </a>
        <a href="Balance.aspx">
            <i class="fas fa-wallet"></i>
            <span>ยอดชำระ</span>
        </a>
        <a href="Review.aspx">
            <i class="fas fa-star"></i>
            <span>รีวิว</span>
        </a>
    </div>
</asp:Content>
