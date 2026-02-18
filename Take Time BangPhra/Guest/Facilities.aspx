<%@ Page Title="Facilities & Amenities" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Facilities.aspx.cs" Inherits="Take_Time_BangPhra.Guest.Facilities" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .facilities-container {
            max-width: 1200px;
            margin: 0 auto;
            padding: 20px;
        }

        .page-header {
            background: linear-gradient(135deg, #7b1fa2 0%, #4a148c 100%);
            color: white;
            padding: 25px;
            border-radius: 12px;
            margin-bottom: 25px;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .page-header h2 {
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

        .hero-banner {
            background: linear-gradient(rgba(0,0,0,0.4), rgba(0,0,0,0.4)),
                        url('https://images.unsplash.com/photo-1566073771259-6a8506099945?w=1200') center/cover;
            border-radius: 20px;
            padding: 60px 40px;
            text-align: center;
            color: white;
            margin-bottom: 40px;
        }

        .hero-banner h1 {
            font-size: 36px;
            font-weight: 700;
            margin: 0 0 10px 0;
            text-shadow: 2px 2px 4px rgba(0,0,0,0.3);
        }

        .hero-banner p {
            font-size: 18px;
            margin: 0;
            opacity: 0.95;
        }

        .facilities-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(350px, 1fr));
            gap: 25px;
            margin-bottom: 40px;
        }

        .facility-card {
            background: white;
            border-radius: 15px;
            overflow: hidden;
            box-shadow: 0 5px 20px rgba(0,0,0,0.08);
            transition: all 0.3s ease;
        }

        .facility-card:hover {
            transform: translateY(-5px);
            box-shadow: 0 10px 30px rgba(0,0,0,0.12);
        }

        .facility-image {
            width: 100%;
            height: 180px;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 60px;
        }

        .facility-image.pool { background: linear-gradient(135deg, #4fc3f7, #0288d1); color: white; }
        .facility-image.restaurant { background: linear-gradient(135deg, #ffb74d, #f57c00); color: white; }
        .facility-image.spa { background: linear-gradient(135deg, #ce93d8, #7b1fa2); color: white; }
        .facility-image.gym { background: linear-gradient(135deg, #81c784, #388e3c); color: white; }
        .facility-image.wifi { background: linear-gradient(135deg, #64b5f6, #1976d2); color: white; }
        .facility-image.parking { background: linear-gradient(135deg, #90a4ae, #546e7a); color: white; }
        .facility-image.laundry { background: linear-gradient(135deg, #4dd0e1, #00838f); color: white; }
        .facility-image.garden { background: linear-gradient(135deg, #aed581, #689f38); color: white; }
        .facility-image.beach { background: linear-gradient(135deg, #ffe082, #ffa000); color: white; }
        .facility-image.bbq { background: linear-gradient(135deg, #ff8a65, #d84315); color: white; }
        .facility-image.karaoke { background: linear-gradient(135deg, #f48fb1, #c2185b); color: white; }
        .facility-image.meeting { background: linear-gradient(135deg, #9fa8da, #3949ab); color: white; }

        .facility-content {
            padding: 25px;
        }

        .facility-content h3 {
            margin: 0 0 12px 0;
            color: #333;
            font-size: 20px;
            font-weight: 600;
        }

        .facility-content p {
            margin: 0 0 15px 0;
            color: #666;
            font-size: 14px;
            line-height: 1.6;
        }

        .facility-details {
            display: flex;
            flex-wrap: wrap;
            gap: 15px;
            font-size: 13px;
            color: #888;
        }

        .facility-details span {
            display: flex;
            align-items: center;
            gap: 5px;
        }

        .facility-details .status-open {
            color: #2e7d32;
            font-weight: 600;
        }

        .facility-details .status-closed {
            color: #c62828;
            font-weight: 600;
        }

        .amenities-section {
            background: white;
            border-radius: 20px;
            padding: 35px;
            margin-bottom: 30px;
            box-shadow: 0 5px 20px rgba(0,0,0,0.08);
        }

        .amenities-section h3 {
            margin: 0 0 25px 0;
            color: #333;
            font-size: 22px;
            font-weight: 600;
            text-align: center;
        }

        .amenities-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
            gap: 15px;
        }

        .amenity-item {
            display: flex;
            align-items: center;
            gap: 12px;
            padding: 15px;
            background: #f5f5f5;
            border-radius: 10px;
            transition: all 0.3s ease;
        }

        .amenity-item:hover {
            background: #ede7f6;
        }

        .amenity-item i {
            font-size: 24px;
            color: #7b1fa2;
            width: 30px;
            text-align: center;
        }

        .amenity-item span {
            font-size: 14px;
            color: #333;
            font-weight: 500;
        }

        .rules-section {
            background: linear-gradient(135deg, #fff8e1, #ffecb3);
            border-radius: 20px;
            padding: 35px;
            margin-bottom: 30px;
        }

        .rules-section h3 {
            margin: 0 0 20px 0;
            color: #f57c00;
            font-size: 20px;
            font-weight: 600;
            display: flex;
            align-items: center;
            gap: 10px;
        }

        .rules-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
            gap: 20px;
        }

        .rule-item {
            display: flex;
            align-items: flex-start;
            gap: 12px;
            background: white;
            padding: 15px;
            border-radius: 10px;
        }

        .rule-item i {
            font-size: 20px;
            color: #f57c00;
            flex-shrink: 0;
        }

        .rule-item div {
            font-size: 13px;
            color: #666;
            line-height: 1.5;
        }

        .rule-item strong {
            color: #333;
        }

        .services-banner {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            border-radius: 20px;
            padding: 40px;
            text-align: center;
        }

        .services-banner h3 {
            margin: 0 0 15px 0;
            font-size: 24px;
            font-weight: 600;
        }

        .services-banner p {
            margin: 0 0 25px 0;
            font-size: 16px;
            opacity: 0.95;
        }

        .service-buttons {
            display: flex;
            gap: 15px;
            justify-content: center;
            flex-wrap: wrap;
        }

        .service-btn {
            display: inline-flex;
            align-items: center;
            gap: 8px;
            padding: 12px 25px;
            background: white;
            color: #7b1fa2;
            border-radius: 25px;
            text-decoration: none;
            font-weight: 600;
            transition: all 0.3s ease;
        }

        .service-btn:hover {
            transform: translateY(-2px);
            box-shadow: 0 5px 15px rgba(0,0,0,0.2);
            color: #7b1fa2;
            text-decoration: none;
        }

        .no-data-panel {
            text-align: center;
            padding: 60px 20px;
            background: white;
            border-radius: 20px;
            box-shadow: 0 5px 20px rgba(0,0,0,0.08);
            margin-bottom: 30px;
        }

        .no-data-panel i {
            font-size: 60px;
            color: #ccc;
            margin-bottom: 20px;
        }

        .no-data-panel h3 {
            color: #999;
            font-size: 20px;
            margin: 0 0 10px 0;
        }

        .no-data-panel p {
            color: #bbb;
            font-size: 14px;
            margin: 0;
        }

        @media (max-width: 768px) {
            .facilities-grid {
                grid-template-columns: 1fr;
            }

            .amenities-grid {
                grid-template-columns: 1fr;
            }

            .rules-grid {
                grid-template-columns: 1fr;
            }

            .hero-banner {
                padding: 40px 20px;
            }

            .hero-banner h1 {
                font-size: 26px;
            }

            .service-buttons {
                flex-direction: column;
            }
        }
    </style>

    <div class="facilities-container">
        <!-- Header -->
        <div class="page-header">
            <h2><i class="fas fa-concierge-bell"></i> Facilities & Amenities</h2>
            <a href="Dashboard.aspx" class="btn-back">
                <i class="fas fa-arrow-left"></i> Back
            </a>
        </div>

        <!-- Hero Banner -->
        <div class="hero-banner">
            <h1>World-Class Facilities</h1>
            <p>Experience comfort and convenience with our premium amenities</p>
        </div>

        <!-- No Data Panel -->
        <asp:Panel ID="pnlNoData" runat="server" Visible="false" CssClass="no-data-panel">
            <i class="fas fa-info-circle"></i>
            <h3>ยังไม่มีข้อมูล</h3>
            <p>กรุณาเพิ่มข้อมูลผ่านหน้า Admin</p>
        </asp:Panel>

        <!-- Main Facilities -->
        <div class="facilities-grid">
            <asp:Repeater ID="rptFacilities" runat="server">
                <ItemTemplate>
                    <div class="facility-card">
                        <div class="facility-image <%# Eval("Css_Class") %>">
                            <i class="fas <%# Eval("Icon") %>"></i>
                        </div>
                        <div class="facility-content">
                            <h3><%# Eval("Name") %></h3>
                            <p><%# Eval("Description") %></p>
                            <div class="facility-details">
                                <span><i class="fas fa-clock"></i> <%# Eval("Hours") %></span>
                                <span class="status-open"><i class="fas fa-check-circle"></i> Open</span>
                                <span><%# Eval("Extra_Info") %></span>
                            </div>
                        </div>
                    </div>
                </ItemTemplate>
            </asp:Repeater>
        </div>

        <!-- Room Amenities -->
        <div class="amenities-section">
            <h3><i class="fas fa-bed"></i> In-Room Amenities</h3>
            <div class="amenities-grid">
                <asp:Repeater ID="rptAmenities" runat="server">
                    <ItemTemplate>
                        <div class="amenity-item">
                            <i class="fas <%# Eval("Icon") %>"></i>
                            <span><%# Eval("Name") %></span>
                        </div>
                    </ItemTemplate>
                </asp:Repeater>
            </div>
        </div>

        <!-- Hotel Rules -->
        <div class="rules-section">
            <h3><i class="fas fa-clipboard-list"></i> Hotel Policies</h3>
            <div class="rules-grid">
                <asp:Repeater ID="rptPolicies" runat="server">
                    <ItemTemplate>
                        <div class="rule-item">
                            <i class="fas <%# Eval("Icon") %>"></i>
                            <div>
                                <strong><%# Eval("Name") %></strong><br/>
                                <%# Eval("Description") %>
                            </div>
                        </div>
                    </ItemTemplate>
                </asp:Repeater>
            </div>
        </div>

        <!-- Services Banner -->
        <div class="services-banner">
            <h3><i class="fas fa-concierge-bell"></i> Need Something?</h3>
            <p>เรายินดีช่วยเหลือคุณตลอด 24 ชั่วโมง</p>
            <div class="service-buttons">
                <a href="RoomService.aspx" class="service-btn">
                    <i class="fas fa-utensils"></i> Room Service
                </a>
                <a href="Housekeeping.aspx" class="service-btn">
                    <i class="fas fa-broom"></i> Housekeeping
                </a>
                <a href="Concierge.aspx" class="service-btn">
                    <i class="fas fa-concierge-bell"></i> Concierge
                </a>
                <a href="Emergency.aspx" class="service-btn">
                    <i class="fas fa-phone-alt"></i> Emergency
                </a>
            </div>
        </div>
    </div>
</asp:Content>
