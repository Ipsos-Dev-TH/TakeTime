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
                height: 250px;
            }

            .category-tabs {
                overflow-x: auto;
                flex-wrap: nowrap;
                padding-bottom: 10px;
                -webkit-overflow-scrolling: touch;
            }

            .category-tab {
                white-space: nowrap;
                padding: 10px 18px;
                font-size: 13px;
            }

            .page-header {
                padding: 15px;
                flex-direction: column;
                gap: 10px;
                text-align: center;
            }

            .page-header h2 {
                font-size: 18px;
            }

            .places-container {
                padding: 10px;
            }

            .place-content {
                padding: 15px;
            }

            .place-content h4 {
                font-size: 16px;
            }

            .section-divider {
                margin: 25px 0 20px;
            }
        }

        /* Dynamic place card styles */
        .place-icon {
            width: 100%;
            height: 180px;
            background: linear-gradient(135deg, #e3f2fd, #bbdefb);
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 64px;
        }

        .place-info {
            padding: 20px;
        }

        .place-info h3 {
            margin: 0 0 8px 0;
            color: #333;
            font-size: 18px;
            font-weight: 600;
        }

        .place-info p {
            margin: 0 0 12px 0;
            color: #666;
            font-size: 14px;
            line-height: 1.5;
        }

        .btn-map {
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

        .btn-map:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(21, 101, 192, 0.3);
            color: white;
            text-decoration: none;
        }

        .no-data-panel {
            text-align: center;
            padding: 60px 20px;
            background: white;
            border-radius: 15px;
            box-shadow: 0 5px 20px rgba(0,0,0,0.08);
        }

        .no-data-panel i {
            font-size: 64px;
            color: #ccc;
            margin-bottom: 20px;
        }

        .no-data-panel h3 {
            color: #666;
            margin: 0 0 10px 0;
        }

        .no-data-panel p {
            color: #999;
            margin: 0;
        }

        @media (max-width: 768px) {
            .place-info {
                padding: 15px;
            }

            .place-info h3 {
                font-size: 16px;
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
            <button type="button" class="category-tab active" onclick="filterPlaces('all')">
                <i class="fas fa-globe"></i> All
            </button>
            <button type="button" class="category-tab" onclick="filterPlaces('beach')">
                <i class="fas fa-umbrella-beach"></i> Beaches
            </button>
            <button type="button" class="category-tab" onclick="filterPlaces('restaurant')">
                <i class="fas fa-utensils"></i> Restaurants
            </button>
            <button type="button" class="category-tab" onclick="filterPlaces('cafe')">
                <i class="fas fa-coffee"></i> Cafes
            </button>
            <button type="button" class="category-tab" onclick="filterPlaces('attraction')">
                <i class="fas fa-camera"></i> Attractions
            </button>
            <button type="button" class="category-tab" onclick="filterPlaces('shopping')">
                <i class="fas fa-shopping-bag"></i> Shopping
            </button>
        </div>

        <!-- Places from Database -->
        <div class="places-grid">
            <asp:Repeater ID="rptPlaces" runat="server">
                <ItemTemplate>
                    <div class="place-card" data-category='<%# Eval("Category") %>'>
                        <div class="place-icon"><%# Eval("Icon") %></div>
                        <div class="place-info">
                            <h3><%# Eval("Name") %></h3>
                            <p><%# Eval("Description") %></p>
                            <div class="place-meta">
                                <span class="distance"><i class="fas fa-map-marker-alt"></i> <%# Eval("Distance") %></span>
                                <span class="travel-time"><i class="fas fa-clock"></i> <%# Eval("Travel_Time") %></span>
                            </div>
                            <div class="place-actions">
                                <%# !string.IsNullOrEmpty(Eval("Map_Url")?.ToString()) ? "<a href='" + Eval("Map_Url") + "' target='_blank' class='btn-map'><i class='fas fa-map'></i> แผนที่</a>" : "" %>
                                <%# !string.IsNullOrEmpty(Eval("Phone")?.ToString()) ? "<a href='tel:" + Eval("Phone") + "' class='btn-call'><i class='fas fa-phone'></i> โทร</a>" : "" %>
                            </div>
                        </div>
                    </div>
                </ItemTemplate>
            </asp:Repeater>
        </div>

        <!-- No Data Panel -->
        <asp:Panel ID="pnlNoData" runat="server" Visible="false" CssClass="no-data-panel">
            <div>
                <i class="fas fa-map-marked-alt"></i>
                <h3>No nearby places available</h3>
                <p>Places and attractions information is currently being updated. Please check back later.</p>
            </div>
        </asp:Panel>

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
