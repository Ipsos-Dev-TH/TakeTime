<%@ Page Title="Nearby Places & Attractions" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="NearbyPlaces.aspx.cs" Inherits="Take_Time_BangPhra.Guest.NearbyPlaces" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .places-container {
            max-width: 1400px;
            margin: 0 auto;
            padding: 20px;
        }

        .page-header {
            background: linear-gradient(135deg, #1565c0 0%, #0d47a1 100%);
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

        .map-container {
            width: 100%;
            height: 400px;
            border-radius: 15px;
            overflow: hidden;
            margin-bottom: 30px;
            box-shadow: 0 5px 20px rgba(0,0,0,0.1);
        }

        .map-container iframe {
            width: 100%;
            height: 100%;
            border: none;
        }

        .category-tabs {
            display: flex;
            gap: 10px;
            margin-bottom: 25px;
            flex-wrap: wrap;
        }

        .category-tab {
            padding: 12px 25px;
            border: none;
            border-radius: 25px;
            background: #e0e0e0;
            color: #666;
            font-weight: 600;
            cursor: pointer;
            transition: all 0.3s ease;
            display: flex;
            align-items: center;
            gap: 8px;
        }

        .category-tab:hover {
            background: #bdbdbd;
        }

        .category-tab.active {
            background: linear-gradient(135deg, #1565c0, #0d47a1);
            color: white;
        }

        .places-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(350px, 1fr));
            gap: 25px;
            margin-bottom: 30px;
        }

        .place-card {
            background: white;
            border-radius: 15px;
            overflow: hidden;
            box-shadow: 0 5px 20px rgba(0,0,0,0.08);
            transition: all 0.3s ease;
        }

        .place-card:hover {
            transform: translateY(-5px);
            box-shadow: 0 10px 30px rgba(0,0,0,0.12);
        }

        .place-image {
            width: 100%;
            height: 180px;
            object-fit: cover;
            background: linear-gradient(135deg, #e3f2fd, #bbdefb);
            display: flex;
            align-items: center;
            justify-content: center;
            color: #1565c0;
            font-size: 48px;
        }

        .place-content {
            padding: 20px;
        }

        .place-category {
            display: inline-block;
            padding: 4px 12px;
            border-radius: 15px;
            font-size: 11px;
            font-weight: 600;
            text-transform: uppercase;
            margin-bottom: 10px;
        }

        .place-category.beach { background: #e3f2fd; color: #1565c0; }
        .place-category.restaurant { background: #fff3e0; color: #ef6c00; }
        .place-category.cafe { background: #fce4ec; color: #c2185b; }
        .place-category.attraction { background: #e8f5e9; color: #2e7d32; }
        .place-category.shopping { background: #f3e5f5; color: #7b1fa2; }
        .place-category.temple { background: #fff8e1; color: #f9a825; }

        .place-content h4 {
            margin: 0 0 8px 0;
            color: #333;
            font-size: 18px;
            font-weight: 600;
        }

        .place-content p {
            margin: 0 0 12px 0;
            color: #666;
            font-size: 14px;
            line-height: 1.5;
        }

        .place-meta {
            display: flex;
            flex-wrap: wrap;
            gap: 15px;
            margin-bottom: 15px;
            font-size: 13px;
            color: #888;
        }

        .place-meta span {
            display: flex;
            align-items: center;
            gap: 5px;
        }

        .place-actions {
            display: flex;
            gap: 10px;
        }

        .btn-direction {
            flex: 1;
            display: flex;
            align-items: center;
            justify-content: center;
            gap: 8px;
            padding: 10px 15px;
            background: linear-gradient(135deg, #1565c0, #0d47a1);
            color: white;
            border-radius: 8px;
            text-decoration: none;
            font-weight: 500;
            font-size: 14px;
            transition: all 0.3s ease;
        }

        .btn-direction:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(21, 101, 192, 0.3);
            color: white;
            text-decoration: none;
        }

        .btn-call {
            display: flex;
            align-items: center;
            justify-content: center;
            padding: 10px 15px;
            background: #e8f5e9;
            color: #2e7d32;
            border-radius: 8px;
            text-decoration: none;
            font-weight: 500;
            font-size: 14px;
            transition: all 0.3s ease;
        }

        .btn-call:hover {
            background: #c8e6c9;
            color: #1b5e20;
            text-decoration: none;
        }

        .distance-badge {
            background: #e3f2fd;
            color: #1565c0;
            padding: 3px 10px;
            border-radius: 10px;
            font-size: 12px;
            font-weight: 600;
        }

        .section-divider {
            text-align: center;
            margin: 40px 0 30px;
            position: relative;
        }

        .section-divider::before {
            content: '';
            position: absolute;
            left: 0;
            right: 0;
            top: 50%;
            height: 1px;
            background: #e0e0e0;
        }

        .section-divider span {
            background: #f5f5f5;
            padding: 0 20px;
            color: #666;
            font-weight: 600;
            position: relative;
        }

        .tips-card {
            background: linear-gradient(135deg, #e8f5e9, #c8e6c9);
            border-radius: 15px;
            padding: 25px;
            margin-top: 30px;
        }

        .tips-card h4 {
            margin: 0 0 15px 0;
            color: #1b5e20;
            font-size: 18px;
            display: flex;
            align-items: center;
            gap: 10px;
        }

        .tips-card ul {
            margin: 0;
            padding-left: 20px;
        }

        .tips-card li {
            color: #333;
            margin-bottom: 8px;
            line-height: 1.5;
        }

        @media (max-width: 768px) {
            .places-grid {
                grid-template-columns: 1fr;
            }

            .map-container {
                height: 300px;
            }

            .category-tabs {
                overflow-x: auto;
                flex-wrap: nowrap;
                padding-bottom: 10px;
            }

            .category-tab {
                white-space: nowrap;
            }
        }
    </style>

    <div class="places-container">
        <!-- Header -->
        <div class="page-header">
            <h2><i class="fas fa-map-marked-alt"></i> Nearby Places & Attractions</h2>
            <a href="Dashboard.aspx" class="btn-back">
                <i class="fas fa-arrow-left"></i> Back
            </a>
        </div>

        <!-- Map -->
        <div class="map-container">
            <!-- BangPhra area map centered on resort location -->
            <iframe
                src="https://www.google.com/maps/embed?pb=!1m18!1m12!1m3!1d15500.0!2d100.92!3d13.23!2m3!1f0!2f0!3f0!3m2!1i1024!2i768!4f13.1!3m3!1m2!1s0x0%3A0x0!2zMTPCsDEzJzQ4LjAiTiAxMDDCsDU1JzEyLjAiRQ!5e0!3m2!1sen!2sth!4v1703500000000"
                allowfullscreen=""
                loading="lazy"
                referrerpolicy="no-referrer-when-downgrade">
            </iframe>
        </div>

        <!-- Category Tabs -->
        <div class="category-tabs">
            <button class="category-tab active" onclick="filterPlaces('all')">
                <i class="fas fa-globe"></i> All
            </button>
            <button class="category-tab" onclick="filterPlaces('beach')">
                <i class="fas fa-umbrella-beach"></i> Beaches
            </button>
            <button class="category-tab" onclick="filterPlaces('restaurant')">
                <i class="fas fa-utensils"></i> Restaurants
            </button>
            <button class="category-tab" onclick="filterPlaces('cafe')">
                <i class="fas fa-coffee"></i> Cafes
            </button>
            <button class="category-tab" onclick="filterPlaces('attraction')">
                <i class="fas fa-camera"></i> Attractions
            </button>
            <button class="category-tab" onclick="filterPlaces('shopping')">
                <i class="fas fa-shopping-bag"></i> Shopping
            </button>
        </div>

        <!-- Beaches & Nature -->
        <div class="section-divider">
            <span><i class="fas fa-umbrella-beach"></i> Beaches & Nature</span>
        </div>
        <div class="places-grid">
            <div class="place-card" data-category="beach">
                <div class="place-image">
                    <i class="fas fa-umbrella-beach"></i>
                </div>
                <div class="place-content">
                    <span class="place-category beach">Beach</span>
                    <h4>Bang Phra Beach</h4>
                    <p>ชายหาดบางพระ หาดทรายสะอาด น้ำทะเลใส เหมาะสำหรับเดินเล่น ว่ายน้ำ และชมพระอาทิตย์ตก</p>
                    <div class="place-meta">
                        <span><i class="fas fa-map-marker-alt"></i> 0.5 km</span>
                        <span><i class="fas fa-clock"></i> เปิดตลอด 24 ชม.</span>
                        <span class="distance-badge">5 min walk</span>
                    </div>
                    <div class="place-actions">
                        <a href="https://maps.google.com/?q=Bang+Phra+Beach+Chonburi" target="_blank" class="btn-direction">
                            <i class="fas fa-directions"></i> Directions
                        </a>
                    </div>
                </div>
            </div>

            <div class="place-card" data-category="beach">
                <div class="place-image">
                    <i class="fas fa-water"></i>
                </div>
                <div class="place-content">
                    <span class="place-category beach">Beach</span>
                    <h4>Bang Saen Beach</h4>
                    <p>หาดบางแสน ชายหาดยอดนิยมของคนกรุงเทพฯ มีร้านอาหารทะเล กิจกรรมทางน้ำมากมาย</p>
                    <div class="place-meta">
                        <span><i class="fas fa-map-marker-alt"></i> 8 km</span>
                        <span><i class="fas fa-clock"></i> เปิดตลอด 24 ชม.</span>
                        <span class="distance-badge">15 min drive</span>
                    </div>
                    <div class="place-actions">
                        <a href="https://maps.google.com/?q=Bang+Saen+Beach" target="_blank" class="btn-direction">
                            <i class="fas fa-directions"></i> Directions
                        </a>
                    </div>
                </div>
            </div>

            <div class="place-card" data-category="attraction">
                <div class="place-image">
                    <i class="fas fa-mountain"></i>
                </div>
                <div class="place-content">
                    <span class="place-category attraction">Attraction</span>
                    <h4>Khao Sam Muk</h4>
                    <p>เขาสามมุข จุดชมวิวทะเล มีลิงแสม วัดเขาสามมุข และร้านอาหารบนเนินเขา</p>
                    <div class="place-meta">
                        <span><i class="fas fa-map-marker-alt"></i> 10 km</span>
                        <span><i class="fas fa-clock"></i> 06:00 - 18:00</span>
                        <span class="distance-badge">20 min drive</span>
                    </div>
                    <div class="place-actions">
                        <a href="https://maps.google.com/?q=Khao+Sam+Muk+Chonburi" target="_blank" class="btn-direction">
                            <i class="fas fa-directions"></i> Directions
                        </a>
                    </div>
                </div>
            </div>
        </div>

        <!-- Restaurants -->
        <div class="section-divider">
            <span><i class="fas fa-utensils"></i> Recommended Restaurants</span>
        </div>
        <div class="places-grid">
            <div class="place-card" data-category="restaurant">
                <div class="place-image" style="background: linear-gradient(135deg, #fff3e0, #ffe0b2);">
                    <i class="fas fa-fish" style="color: #ef6c00;"></i>
                </div>
                <div class="place-content">
                    <span class="place-category restaurant">Seafood</span>
                    <h4>Laem Charoen Seafood</h4>
                    <p>ร้านอาหารทะเลสด คุณภาพดี ราคาสมเหตุสมผล บรรยากาศดี วิวทะเล</p>
                    <div class="place-meta">
                        <span><i class="fas fa-map-marker-alt"></i> 3 km</span>
                        <span><i class="fas fa-clock"></i> 10:00 - 22:00</span>
                        <span><i class="fas fa-dollar-sign"></i> ฿฿฿</span>
                    </div>
                    <div class="place-actions">
                        <a href="https://maps.google.com/?q=Laem+Charoen+Seafood+Sriracha" target="_blank" class="btn-direction">
                            <i class="fas fa-directions"></i> Directions
                        </a>
                        <a href="tel:038312000" class="btn-call">
                            <i class="fas fa-phone"></i>
                        </a>
                    </div>
                </div>
            </div>

            <div class="place-card" data-category="restaurant">
                <div class="place-image" style="background: linear-gradient(135deg, #fff3e0, #ffe0b2);">
                    <i class="fas fa-drumstick-bite" style="color: #ef6c00;"></i>
                </div>
                <div class="place-content">
                    <span class="place-category restaurant">Thai Food</span>
                    <h4>Moom Aroi Restaurant</h4>
                    <p>อาหารไทยรสชาติจัดจ้าน เมนูหลากหลาย ปริมาณเต็มๆ ราคาไม่แพง</p>
                    <div class="place-meta">
                        <span><i class="fas fa-map-marker-alt"></i> 2 km</span>
                        <span><i class="fas fa-clock"></i> 10:00 - 21:00</span>
                        <span><i class="fas fa-dollar-sign"></i> ฿฿</span>
                    </div>
                    <div class="place-actions">
                        <a href="https://maps.google.com/?q=Moom+Aroi+Sriracha" target="_blank" class="btn-direction">
                            <i class="fas fa-directions"></i> Directions
                        </a>
                    </div>
                </div>
            </div>

            <div class="place-card" data-category="restaurant">
                <div class="place-image" style="background: linear-gradient(135deg, #fff3e0, #ffe0b2);">
                    <i class="fas fa-pepper-hot" style="color: #ef6c00;"></i>
                </div>
                <div class="place-content">
                    <span class="place-category restaurant">Japanese</span>
                    <h4>Tsukiji Sushi</h4>
                    <p>ร้านซูชิสดใหม่ วัตถุดิบนำเข้าจากญี่ปุ่น บรรยากาศสไตล์ญี่ปุ่นแท้</p>
                    <div class="place-meta">
                        <span><i class="fas fa-map-marker-alt"></i> 5 km</span>
                        <span><i class="fas fa-clock"></i> 11:00 - 21:00</span>
                        <span><i class="fas fa-dollar-sign"></i> ฿฿฿฿</span>
                    </div>
                    <div class="place-actions">
                        <a href="https://maps.google.com/?q=Tsukiji+Sushi+Sriracha" target="_blank" class="btn-direction">
                            <i class="fas fa-directions"></i> Directions
                        </a>
                        <a href="tel:038320000" class="btn-call">
                            <i class="fas fa-phone"></i>
                        </a>
                    </div>
                </div>
            </div>
        </div>

        <!-- Cafes -->
        <div class="section-divider">
            <span><i class="fas fa-coffee"></i> Cafes & Desserts</span>
        </div>
        <div class="places-grid">
            <div class="place-card" data-category="cafe">
                <div class="place-image" style="background: linear-gradient(135deg, #fce4ec, #f8bbd9);">
                    <i class="fas fa-mug-hot" style="color: #c2185b;"></i>
                </div>
                <div class="place-content">
                    <span class="place-category cafe">Cafe</span>
                    <h4>Sea View Cafe</h4>
                    <p>คาเฟ่วิวทะเล กาแฟดี ขนมหวาน บรรยากาศชิลๆ เหมาะนั่งทำงานหรือพักผ่อน</p>
                    <div class="place-meta">
                        <span><i class="fas fa-map-marker-alt"></i> 1 km</span>
                        <span><i class="fas fa-clock"></i> 08:00 - 20:00</span>
                        <span><i class="fas fa-wifi"></i> Free WiFi</span>
                    </div>
                    <div class="place-actions">
                        <a href="https://maps.google.com/?q=Sea+View+Cafe+Bang+Phra" target="_blank" class="btn-direction">
                            <i class="fas fa-directions"></i> Directions
                        </a>
                    </div>
                </div>
            </div>

            <div class="place-card" data-category="cafe">
                <div class="place-image" style="background: linear-gradient(135deg, #fce4ec, #f8bbd9);">
                    <i class="fas fa-ice-cream" style="color: #c2185b;"></i>
                </div>
                <div class="place-content">
                    <span class="place-category cafe">Cafe</span>
                    <h4>After You Dessert Cafe</h4>
                    <p>ร้านขนมหวานชื่อดัง Kakigori, โทสต์ และเครื่องดื่มเย็นๆ หลากหลายเมนู</p>
                    <div class="place-meta">
                        <span><i class="fas fa-map-marker-alt"></i> 12 km</span>
                        <span><i class="fas fa-clock"></i> 10:00 - 22:00</span>
                        <span><i class="fas fa-shopping-bag"></i> Central Sriracha</span>
                    </div>
                    <div class="place-actions">
                        <a href="https://maps.google.com/?q=After+You+Central+Sriracha" target="_blank" class="btn-direction">
                            <i class="fas fa-directions"></i> Directions
                        </a>
                    </div>
                </div>
            </div>

            <div class="place-card" data-category="cafe">
                <div class="place-image" style="background: linear-gradient(135deg, #fce4ec, #f8bbd9);">
                    <i class="fas fa-leaf" style="color: #c2185b;"></i>
                </div>
                <div class="place-content">
                    <span class="place-category cafe">Cafe</span>
                    <h4>Forest Brew Cafe</h4>
                    <p>คาเฟ่สไตล์ธรรมชาติ กลางสวน ร่มรื่น กาแฟคั่วเอง เบเกอรี่โฮมเมด</p>
                    <div class="place-meta">
                        <span><i class="fas fa-map-marker-alt"></i> 4 km</span>
                        <span><i class="fas fa-clock"></i> 09:00 - 18:00</span>
                        <span><i class="fas fa-parking"></i> Free Parking</span>
                    </div>
                    <div class="place-actions">
                        <a href="https://maps.google.com/?q=Forest+Brew+Cafe+Sriracha" target="_blank" class="btn-direction">
                            <i class="fas fa-directions"></i> Directions
                        </a>
                    </div>
                </div>
            </div>
        </div>

        <!-- Attractions -->
        <div class="section-divider">
            <span><i class="fas fa-camera"></i> Tourist Attractions</span>
        </div>
        <div class="places-grid">
            <div class="place-card" data-category="attraction">
                <div class="place-image" style="background: linear-gradient(135deg, #e8f5e9, #c8e6c9);">
                    <i class="fas fa-fish" style="color: #2e7d32;"></i>
                </div>
                <div class="place-content">
                    <span class="place-category attraction">Attraction</span>
                    <h4>Sriracha Tiger Zoo</h4>
                    <p>สวนเสือศรีราชา ชมการแสดงเสือ จระเข้ และสัตว์หลากหลายชนิด เหมาะสำหรับครอบครัว</p>
                    <div class="place-meta">
                        <span><i class="fas fa-map-marker-alt"></i> 15 km</span>
                        <span><i class="fas fa-clock"></i> 08:00 - 18:00</span>
                        <span><i class="fas fa-ticket-alt"></i> ฿450/person</span>
                    </div>
                    <div class="place-actions">
                        <a href="https://maps.google.com/?q=Sriracha+Tiger+Zoo" target="_blank" class="btn-direction">
                            <i class="fas fa-directions"></i> Directions
                        </a>
                        <a href="tel:038296556" class="btn-call">
                            <i class="fas fa-phone"></i>
                        </a>
                    </div>
                </div>
            </div>

            <div class="place-card" data-category="attraction">
                <div class="place-image" style="background: linear-gradient(135deg, #e8f5e9, #c8e6c9);">
                    <i class="fas fa-paw" style="color: #2e7d32;"></i>
                </div>
                <div class="place-content">
                    <span class="place-category attraction">Attraction</span>
                    <h4>Khao Kheow Open Zoo</h4>
                    <p>สวนสัตว์เปิดเขาเขียว พื้นที่กว้างขวาง สัตว์หลากหลาย Safari Night Tour สุดพิเศษ</p>
                    <div class="place-meta">
                        <span><i class="fas fa-map-marker-alt"></i> 20 km</span>
                        <span><i class="fas fa-clock"></i> 08:00 - 18:00</span>
                        <span><i class="fas fa-ticket-alt"></i> ฿300/person</span>
                    </div>
                    <div class="place-actions">
                        <a href="https://maps.google.com/?q=Khao+Kheow+Open+Zoo" target="_blank" class="btn-direction">
                            <i class="fas fa-directions"></i> Directions
                        </a>
                        <a href="tel:038318444" class="btn-call">
                            <i class="fas fa-phone"></i>
                        </a>
                    </div>
                </div>
            </div>

            <div class="place-card" data-category="temple">
                <div class="place-image" style="background: linear-gradient(135deg, #fff8e1, #ffecb3);">
                    <i class="fas fa-pray" style="color: #f9a825;"></i>
                </div>
                <div class="place-content">
                    <span class="place-category temple">Temple</span>
                    <h4>Wat Khao Phra Bat</h4>
                    <p>วัดเขาพระบาท วัดบนยอดเขา วิวสวยงาม สถาปัตยกรรมไทยที่งดงาม เหมาะสำหรับผู้แสวงบุญ</p>
                    <div class="place-meta">
                        <span><i class="fas fa-map-marker-alt"></i> 8 km</span>
                        <span><i class="fas fa-clock"></i> 06:00 - 18:00</span>
                        <span class="distance-badge">Free Entry</span>
                    </div>
                    <div class="place-actions">
                        <a href="https://maps.google.com/?q=Wat+Khao+Phra+Bat+Chonburi" target="_blank" class="btn-direction">
                            <i class="fas fa-directions"></i> Directions
                        </a>
                    </div>
                </div>
            </div>
        </div>

        <!-- Shopping -->
        <div class="section-divider">
            <span><i class="fas fa-shopping-bag"></i> Shopping</span>
        </div>
        <div class="places-grid">
            <div class="place-card" data-category="shopping">
                <div class="place-image" style="background: linear-gradient(135deg, #f3e5f5, #e1bee7);">
                    <i class="fas fa-store" style="color: #7b1fa2;"></i>
                </div>
                <div class="place-content">
                    <span class="place-category shopping">Mall</span>
                    <h4>Central Sriracha</h4>
                    <p>ห้างสรรพสินค้าครบวงจร ร้านค้า ร้านอาหาร โรงหนัง ซุปเปอร์มาร์เก็ต</p>
                    <div class="place-meta">
                        <span><i class="fas fa-map-marker-alt"></i> 12 km</span>
                        <span><i class="fas fa-clock"></i> 10:00 - 22:00</span>
                        <span><i class="fas fa-parking"></i> Free Parking</span>
                    </div>
                    <div class="place-actions">
                        <a href="https://maps.google.com/?q=Central+Sriracha" target="_blank" class="btn-direction">
                            <i class="fas fa-directions"></i> Directions
                        </a>
                    </div>
                </div>
            </div>

            <div class="place-card" data-category="shopping">
                <div class="place-image" style="background: linear-gradient(135deg, #f3e5f5, #e1bee7);">
                    <i class="fas fa-shopping-basket" style="color: #7b1fa2;"></i>
                </div>
                <div class="place-content">
                    <span class="place-category shopping">Market</span>
                    <h4>Sriracha Night Market</h4>
                    <p>ตลาดนัดกลางคืนศรีราชา อาหารท้องถิ่น ของกิน Street Food ราคาถูก บรรยากาศคึกคัก</p>
                    <div class="place-meta">
                        <span><i class="fas fa-map-marker-alt"></i> 10 km</span>
                        <span><i class="fas fa-clock"></i> 17:00 - 23:00</span>
                        <span class="distance-badge">Fri-Sun</span>
                    </div>
                    <div class="place-actions">
                        <a href="https://maps.google.com/?q=Sriracha+Night+Market" target="_blank" class="btn-direction">
                            <i class="fas fa-directions"></i> Directions
                        </a>
                    </div>
                </div>
            </div>

            <div class="place-card" data-category="shopping">
                <div class="place-image" style="background: linear-gradient(135deg, #f3e5f5, #e1bee7);">
                    <i class="fas fa-shopping-cart" style="color: #7b1fa2;"></i>
                </div>
                <div class="place-content">
                    <span class="place-category shopping">Store</span>
                    <h4>Big C Sriracha</h4>
                    <p>ไฮเปอร์มาร์เก็ต ของใช้ประจำวัน ของฝาก ของกินเล่น ครบครัน ราคาประหยัด</p>
                    <div class="place-meta">
                        <span><i class="fas fa-map-marker-alt"></i> 8 km</span>
                        <span><i class="fas fa-clock"></i> 09:00 - 23:00</span>
                        <span><i class="fas fa-parking"></i> Free Parking</span>
                    </div>
                    <div class="place-actions">
                        <a href="https://maps.google.com/?q=Big+C+Sriracha" target="_blank" class="btn-direction">
                            <i class="fas fa-directions"></i> Directions
                        </a>
                    </div>
                </div>
            </div>
        </div>

        <!-- Travel Tips -->
        <div class="tips-card">
            <h4><i class="fas fa-lightbulb"></i> Travel Tips</h4>
            <ul>
                <li><strong>Transportation:</strong> สามารถใช้บริการ Grab หรือขอ Hotel Shuttle ได้ที่ Front Desk</li>
                <li><strong>Best Time:</strong> หลีกเลี่ยงช่วง 08:00-09:00 และ 17:00-18:00 เนื่องจากรถติด</li>
                <li><strong>Payment:</strong> ส่วนใหญ่รับบัตรเครดิต แต่ตลาดนัดควรเตรียมเงินสด</li>
                <li><strong>Weather:</strong> ช่วงพฤศจิกายน-กุมภาพันธ์ อากาศเย็นสบาย เหมาะเที่ยวมากที่สุด</li>
                <li><strong>Need Help?</strong> ติดต่อ Concierge ของโรงแรมเพื่อจองทัวร์หรือขอข้อมูลเพิ่มเติม</li>
            </ul>
        </div>
    </div>

    <script>
        function filterPlaces(category) {
            // Update active tab
            document.querySelectorAll('.category-tab').forEach(tab => {
                tab.classList.remove('active');
            });
            event.target.classList.add('active');

            // Filter places
            document.querySelectorAll('.place-card').forEach(card => {
                if (category === 'all' || card.dataset.category === category) {
                    card.style.display = 'block';
                } else {
                    card.style.display = 'none';
                }
            });
        }
    </script>
</asp:Content>
