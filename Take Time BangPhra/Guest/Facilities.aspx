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

        <!-- Main Facilities -->
        <div class="facilities-grid">
            <div class="facility-card">
                <div class="facility-image pool">
                    <i class="fas fa-swimming-pool"></i>
                </div>
                <div class="facility-content">
                    <h3>Swimming Pool</h3>
                    <p>สระว่ายน้ำกลางแจ้ง วิวทะเล น้ำสะอาด ปลอดภัย มีเก้าอี้นอนอาบแดดและร่มชายหาด</p>
                    <div class="facility-details">
                        <span><i class="fas fa-clock"></i> 06:00 - 20:00</span>
                        <span class="status-open"><i class="fas fa-check-circle"></i> Open</span>
                        <span><i class="fas fa-ruler"></i> 25m x 12m</span>
                    </div>
                </div>
            </div>

            <div class="facility-card">
                <div class="facility-image restaurant">
                    <i class="fas fa-utensils"></i>
                </div>
                <div class="facility-content">
                    <h3>Restaurant & Bar</h3>
                    <p>ร้านอาหารและบาร์ เสิร์ฟอาหารไทย-นานาชาติ อาหารเช้าบุฟเฟ่ต์ เครื่องดื่มหลากหลาย</p>
                    <div class="facility-details">
                        <span><i class="fas fa-clock"></i> 07:00 - 22:00</span>
                        <span class="status-open"><i class="fas fa-check-circle"></i> Open</span>
                        <span><i class="fas fa-users"></i> 80 seats</span>
                    </div>
                </div>
            </div>

            <div class="facility-card">
                <div class="facility-image spa">
                    <i class="fas fa-spa"></i>
                </div>
                <div class="facility-content">
                    <h3>Spa & Massage</h3>
                    <p>สปาและนวดแผนไทย ผ่อนคลายร่างกายและจิตใจ ด้วยทีมงานมืออาชีพ (ต้องจองล่วงหน้า)</p>
                    <div class="facility-details">
                        <span><i class="fas fa-clock"></i> 10:00 - 21:00</span>
                        <span class="status-open"><i class="fas fa-check-circle"></i> Open</span>
                        <span><i class="fas fa-phone"></i> Book via Concierge</span>
                    </div>
                </div>
            </div>

            <div class="facility-card">
                <div class="facility-image gym">
                    <i class="fas fa-dumbbell"></i>
                </div>
                <div class="facility-content">
                    <h3>Fitness Center</h3>
                    <p>ห้องออกกำลังกาย อุปกรณ์ครบครัน Cardio และ Weight Training รวมถึงโยคะมุม</p>
                    <div class="facility-details">
                        <span><i class="fas fa-clock"></i> 06:00 - 22:00</span>
                        <span class="status-open"><i class="fas fa-check-circle"></i> Open</span>
                        <span><i class="fas fa-key"></i> Room Key Access</span>
                    </div>
                </div>
            </div>

            <div class="facility-card">
                <div class="facility-image beach">
                    <i class="fas fa-umbrella-beach"></i>
                </div>
                <div class="facility-content">
                    <h3>Private Beach Access</h3>
                    <p>ทางลงหาดส่วนตัว เก้าอี้ชายหาด ร่มสนาม บริการน้ำดื่มฟรีสำหรับแขกที่เข้าพัก</p>
                    <div class="facility-details">
                        <span><i class="fas fa-clock"></i> 06:00 - 19:00</span>
                        <span class="status-open"><i class="fas fa-check-circle"></i> Open</span>
                        <span><i class="fas fa-walking"></i> 3 min walk</span>
                    </div>
                </div>
            </div>

            <div class="facility-card">
                <div class="facility-image bbq">
                    <i class="fas fa-fire"></i>
                </div>
                <div class="facility-content">
                    <h3>BBQ Area</h3>
                    <p>พื้นที่ปิ้งบาร์บีคิว อุปกรณ์ครบ เหมาะสำหรับครอบครัวและกลุ่มเพื่อน (ต้องจองล่วงหน้า)</p>
                    <div class="facility-details">
                        <span><i class="fas fa-clock"></i> 16:00 - 22:00</span>
                        <span class="status-open"><i class="fas fa-check-circle"></i> Available</span>
                        <span><i class="fas fa-users"></i> Max 20 persons</span>
                    </div>
                </div>
            </div>

            <div class="facility-card">
                <div class="facility-image karaoke">
                    <i class="fas fa-microphone"></i>
                </div>
                <div class="facility-content">
                    <h3>Karaoke Room</h3>
                    <p>ห้องคาราโอเกะ ระบบเสียงคุณภาพสูง เพลงหลายภาษา (มีค่าใช้จ่ายเพิ่มเติม)</p>
                    <div class="facility-details">
                        <span><i class="fas fa-clock"></i> 14:00 - 23:00</span>
                        <span class="status-open"><i class="fas fa-check-circle"></i> Available</span>
                        <span><i class="fas fa-users"></i> 10-15 persons</span>
                    </div>
                </div>
            </div>

            <div class="facility-card">
                <div class="facility-image meeting">
                    <i class="fas fa-chalkboard-teacher"></i>
                </div>
                <div class="facility-content">
                    <h3>Meeting Room</h3>
                    <p>ห้องประชุมขนาดกลาง อุปกรณ์โสตทัศนูปกรณ์ครบ เหมาะสำหรับสัมมนาและงานเลี้ยงเล็กๆ</p>
                    <div class="facility-details">
                        <span><i class="fas fa-clock"></i> 08:00 - 20:00</span>
                        <span class="status-open"><i class="fas fa-check-circle"></i> Available</span>
                        <span><i class="fas fa-users"></i> 30 persons</span>
                    </div>
                </div>
            </div>

            <div class="facility-card">
                <div class="facility-image garden">
                    <i class="fas fa-tree"></i>
                </div>
                <div class="facility-content">
                    <h3>Garden & Playground</h3>
                    <p>สวนและสนามเด็กเล่น พื้นที่สีเขียวร่มรื่น สนามหญ้าสำหรับเด็กวิ่งเล่น</p>
                    <div class="facility-details">
                        <span><i class="fas fa-clock"></i> เปิดตลอด 24 ชม.</span>
                        <span class="status-open"><i class="fas fa-check-circle"></i> Open</span>
                        <span><i class="fas fa-child"></i> Kid Friendly</span>
                    </div>
                </div>
            </div>

            <div class="facility-card">
                <div class="facility-image parking">
                    <i class="fas fa-car"></i>
                </div>
                <div class="facility-content">
                    <h3>Free Parking</h3>
                    <p>ที่จอดรถฟรี กล้องวงจรปิด 24 ชม. มีพื้นที่จอดรถหลายคันต่อห้อง</p>
                    <div class="facility-details">
                        <span><i class="fas fa-clock"></i> เปิดตลอด 24 ชม.</span>
                        <span class="status-open"><i class="fas fa-check-circle"></i> Free</span>
                        <span><i class="fas fa-video"></i> CCTV 24hr</span>
                    </div>
                </div>
            </div>

            <div class="facility-card">
                <div class="facility-image wifi">
                    <i class="fas fa-wifi"></i>
                </div>
                <div class="facility-content">
                    <h3>High-Speed WiFi</h3>
                    <p>อินเทอร์เน็ตความเร็วสูงฟรี ครอบคลุมทุกพื้นที่ในรีสอร์ท รองรับ Streaming และ Video Call</p>
                    <div class="facility-details">
                        <span><i class="fas fa-clock"></i> เปิดตลอด 24 ชม.</span>
                        <span class="status-open"><i class="fas fa-check-circle"></i> Free</span>
                        <span><i class="fas fa-tachometer-alt"></i> 100 Mbps</span>
                    </div>
                </div>
            </div>

            <div class="facility-card">
                <div class="facility-image laundry">
                    <i class="fas fa-tshirt"></i>
                </div>
                <div class="facility-content">
                    <h3>Laundry Service</h3>
                    <p>บริการซักรีดเสื้อผ้า รับ-ส่งถึงห้อง บริการด่วนภายใน 4 ชม. (มีค่าใช้จ่ายเพิ่มเติม)</p>
                    <div class="facility-details">
                        <span><i class="fas fa-clock"></i> 08:00 - 18:00</span>
                        <span class="status-open"><i class="fas fa-check-circle"></i> Available</span>
                        <span><i class="fas fa-phone"></i> Ext. 103</span>
                    </div>
                </div>
            </div>
        </div>

        <!-- Room Amenities -->
        <div class="amenities-section">
            <h3><i class="fas fa-bed"></i> In-Room Amenities</h3>
            <div class="amenities-grid">
                <div class="amenity-item">
                    <i class="fas fa-snowflake"></i>
                    <span>Air Conditioning</span>
                </div>
                <div class="amenity-item">
                    <i class="fas fa-tv"></i>
                    <span>Smart TV with Netflix</span>
                </div>
                <div class="amenity-item">
                    <i class="fas fa-box"></i>
                    <span>Mini Bar / Refrigerator</span>
                </div>
                <div class="amenity-item">
                    <i class="fas fa-coffee"></i>
                    <span>Coffee & Tea Maker</span>
                </div>
                <div class="amenity-item">
                    <i class="fas fa-shower"></i>
                    <span>Rain Shower</span>
                </div>
                <div class="amenity-item">
                    <i class="fas fa-pump-soap"></i>
                    <span>Premium Toiletries</span>
                </div>
                <div class="amenity-item">
                    <i class="fas fa-wind"></i>
                    <span>Hair Dryer</span>
                </div>
                <div class="amenity-item">
                    <i class="fas fa-lock"></i>
                    <span>In-Room Safe</span>
                </div>
                <div class="amenity-item">
                    <i class="fas fa-phone"></i>
                    <span>Direct Dial Phone</span>
                </div>
                <div class="amenity-item">
                    <i class="fas fa-bed"></i>
                    <span>Premium Bedding</span>
                </div>
                <div class="amenity-item">
                    <i class="fas fa-door-open"></i>
                    <span>Private Balcony</span>
                </div>
                <div class="amenity-item">
                    <i class="fas fa-wifi"></i>
                    <span>Free WiFi</span>
                </div>
            </div>
        </div>

        <!-- Hotel Rules -->
        <div class="rules-section">
            <h3><i class="fas fa-clipboard-list"></i> Hotel Policies</h3>
            <div class="rules-grid">
                <div class="rule-item">
                    <i class="fas fa-clock"></i>
                    <div>
                        <strong>Check-in / Check-out:</strong><br>
                        Check-in: 14:00 น. / Check-out: 12:00 น.<br>
                        Early check-in และ Late check-out สามารถขอได้ (ขึ้นอยู่กับห้องว่าง)
                    </div>
                </div>
                <div class="rule-item">
                    <i class="fas fa-smoking-ban"></i>
                    <div>
                        <strong>No Smoking:</strong><br>
                        ห้ามสูบบุหรี่ในห้องพักและพื้นที่ปิด สูบบุหรี่ได้ในพื้นที่กำหนดเท่านั้น
                    </div>
                </div>
                <div class="rule-item">
                    <i class="fas fa-paw"></i>
                    <div>
                        <strong>Pet Policy:</strong><br>
                        อนุญาตให้นำสัตว์เลี้ยงเข้าพักได้ในบางห้อง กรุณาแจ้งล่วงหน้า (มีค่าใช้จ่ายเพิ่มเติม)
                    </div>
                </div>
                <div class="rule-item">
                    <i class="fas fa-volume-down"></i>
                    <div>
                        <strong>Quiet Hours:</strong><br>
                        22:00 - 07:00 น. กรุณารักษาความเงียบเพื่อความสะดวกของผู้เข้าพักท่านอื่น
                    </div>
                </div>
                <div class="rule-item">
                    <i class="fas fa-swimmer"></i>
                    <div>
                        <strong>Pool Rules:</strong><br>
                        อาบน้ำก่อนลงสระ ห้ามนำอาหาร/เครื่องดื่มลงสระ เด็กต้องมีผู้ปกครองดูแล
                    </div>
                </div>
                <div class="rule-item">
                    <i class="fas fa-user-friends"></i>
                    <div>
                        <strong>Visitors:</strong><br>
                        ผู้เยี่ยมสามารถเข้าพื้นที่ส่วนกลางได้ แต่ไม่อนุญาตให้ค้างคืนโดยไม่ลงทะเบียน
                    </div>
                </div>
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
