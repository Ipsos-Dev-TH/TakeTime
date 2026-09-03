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
            height: 460px;
            border-radius: 15px;
            overflow: hidden;
            margin-bottom: 12px;
            box-shadow: 0 5px 20px rgba(0,0,0,0.1);
            position: relative;
        }

        .map-container iframe,
        .map-container #nearbyMap {
            width: 100%;
            height: 100%;
            border: none;
        }

        /* หมุดวาดเองด้วย divIcon — วงกลมสีประจำประเภท + อิโมจิตรงกลาง + หางชี้ลงพิกัด */
        .nb-pin {
            width: 34px; height: 34px; line-height: 32px;
            border-radius: 50% 50% 50% 0;
            transform: rotate(-45deg);
            border: 2px solid #fff;
            box-shadow: 0 2px 6px rgba(0,0,0,.35);
            text-align: center;
            font-size: 16px;
        }
        .nb-pin > span { display: inline-block; transform: rotate(45deg); }
        .nb-pin.img { background-size: cover; background-position: center; border-radius: 50%; transform: none; }

        .nb-popup { min-width: 190px; max-width: 240px; }
        .nb-popup img { width: 100%; height: 110px; object-fit: cover; border-radius: 8px; margin-bottom: 8px; }
        .nb-popup h4 { margin: 0 0 4px; font-size: 15px; font-weight: 700; }
        .nb-popup .cat { font-size: 11px; color: #666; margin-bottom: 6px; }
        .nb-popup p { margin: 0 0 8px; font-size: 12px; color: #444; }
        .nb-popup .nb-nav {
            display: block; text-align: center; background: #1a73e8; color: #fff;
            padding: 8px; border-radius: 8px; text-decoration: none; font-weight: 600; font-size: 13px;
        }
        .nb-popup .nb-nav:hover { background: #1558b0; color: #fff; }

        .map-hint {
            font-size: 12px; color: #777; margin: 0 0 22px; text-align: center;
        }

        .place-thumb {
            width: 100%; height: 150px; object-fit: cover;
            border-radius: 12px 12px 0 0; display: block;
        }
        .place-thumb-icon {
            display: flex; align-items: center; justify-content: center;
            font-size: 46px; background: linear-gradient(135deg, #e8f5f1, #d6ece3);
        }
        .place-thumb-wrap { position: relative; }

        /* ป้ายโปรโมทมุมรูป */
        .place-badge {
            position: absolute; top: 10px; left: 10px;
            font-size: 11.5px; font-weight: 700; color: #fff;
            padding: 4px 11px; border-radius: 20px; box-shadow: 0 2px 6px rgba(0,0,0,.25);
        }
        .place-badge.feat { left: auto; right: 10px; background: #d81b60; }

        /* ข้อความโปรโมท — "ที่นี่ดียังไง" ให้เด่นกว่าคำอธิบายทั่วไป */
        .place-highlight {
            background: #fff8e1; border-left: 3px solid #ffb300;
            padding: 7px 10px; border-radius: 0 8px 8px 0;
            font-size: 13px; color: #6d4c41; font-weight: 600;
            margin: 0 0 8px; line-height: 1.5;
        }

        /* หัวข้อกลุ่มประเภท */
        .cat-group { margin-bottom: 26px; }
        .cat-title {
            display: flex; align-items: center; gap: 8px;
            font-size: 17px; font-weight: 700; color: #2e5d3a;
            margin: 0 0 12px; padding-bottom: 8px; border-bottom: 2px solid #eaf3ec;
        }
        .cat-title .cat-ico { font-size: 20px; }
        .cat-title small { color: #9aa; font-weight: 400; font-size: 13px; }

        .place-card.featured { box-shadow: 0 4px 16px rgba(216,27,96,.18); border: 1.5px solid #f8bbd0; }

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

        <!-- แผนที่จริง: ขอบเขตพื้นที่ + หมุดของแต่ละสถานที่ (Leaflet + OpenStreetMap) -->
        <% if (HasMapPoints) { %>
        <div class="map-container"><div id="nearbyMap"></div></div>
        <p class="map-hint"><i class="fas fa-hand-pointer"></i> แตะที่หมุดเพื่อดูรายละเอียดและกดนำทาง</p>
        <% } %>

        <!-- ประเภทสถานที่ — ดึงจากฐานข้อมูล (เดิม hard-code ไว้ 5 ชนิด เพิ่มเองไม่ได้) -->
        <div class="category-tabs">
            <button type="button" class="category-tab active" data-cat="all" onclick="filterPlaces('all', this)">
                <i class="fas fa-globe"></i> ทั้งหมด
            </button>
            <% if (DtCategories != null) { foreach (System.Data.DataRow c in DtCategories.Rows) { %>
                <button type="button" class="category-tab" data-cat="<%= Esc(c["Code"]) %>"
                        onclick="filterPlaces('<%= Esc(c["Code"]) %>', this)">
                    <span><%= Esc(c["Icon"]) %></span> <%= Esc(c["Name"]) %>
                </button>
            <% } } %>
        </div>

        <!-- รายการสถานที่ — จัดกลุ่มตามประเภท -->
        <asp:Repeater ID="rptGroups" runat="server">
            <ItemTemplate>
                <div class="cat-group" data-category='<%# Eval("Code") %>'>
                    <h3 class="cat-title">
                        <span class="cat-ico"><%# Eval("Icon") %></span>
                        <%# Eval("Name") %>
                        <small>(<%# Eval("Count") %>)</small>
                    </h3>
                    <div class="places-grid">
                        <asp:Repeater ID="rptGroupPlaces" runat="server" DataSource='<%# PlacesIn(Eval("Code")) %>'>
                            <ItemTemplate>
                                <div class="place-card<%# IsFeatured(Eval("Is_Featured")) ? " featured" : "" %>"
                                     data-category='<%# Eval("Category") %>' data-id='<%# Eval("ID") %>'>
                                    <div class="place-thumb-wrap">
                                        <%# !string.IsNullOrEmpty(PlaceImage(Eval("Image_Path")))
                                            ? "<img src='" + PlaceImage(Eval("Image_Path")) + "' class='place-thumb' alt='' loading='lazy' />"
                                            : "<div class='place-thumb place-thumb-icon'>" + Eval("Icon") + "</div>" %>
                                        <%# BadgeHtml(Eval("Badge_Text"), Eval("Badge_Color"), Eval("Is_Featured")) %>
                                    </div>
                                    <div class="place-info">
                                        <h3><%# Eval("Name") %></h3>
                                        <%# HighlightHtml(Eval("Highlight")) %>
                                        <p><%# Eval("Description") %></p>
                                        <div class="place-meta">
                                            <%# MetaHtml(Eval("Distance"), Eval("Travel_Time"), Eval("Open_Hours"), Eval("Price_Range")) %>
                                        </div>
                                        <div class="place-actions">
                                            <%# !string.IsNullOrEmpty(NavUrl(Eval("Map_Url"), Eval("Latitude"), Eval("Longitude")))
                                                ? "<a href='" + NavUrl(Eval("Map_Url"), Eval("Latitude"), Eval("Longitude")) + "' target='_blank' rel='noopener' class='btn-map'><i class='fas fa-diamond-turn-right'></i> นำทาง</a>"
                                                : "" %>
                                            <%# !string.IsNullOrEmpty(Eval("Phone") == null ? "" : Eval("Phone").ToString())
                                                ? "<a href='tel:" + Eval("Phone") + "' class='btn-call'><i class='fas fa-phone'></i> โทร</a>"
                                                : "" %>
                                        </div>
                                    </div>
                                </div>
                            </ItemTemplate>
                        </asp:Repeater>
                    </div>
                </div>
            </ItemTemplate>
        </asp:Repeater>

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

    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/leaflet.min.css" />
    <script src="https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/leaflet.min.js"></script>
    <script>
        // ข้อมูลแผนที่มาจากฐานข้อมูลทั้งก้อน (หมุด + ขอบเขตโซน + ประเภท)
        var NB = <%= MapJson %>;
        var nbMap = null, nbMarkers = [], nbBoundaryLayer = null;

        function nbEsc(t) {
            return (t == null ? '' : String(t))
                .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
        }

        // หมุด: ใช้รูปที่กำหนดเองถ้ามี ไม่งั้นเป็นวงกลมสีประจำประเภท + อิโมจิ
        function nbIcon(p) {
            if (p.markerImg) {
                return L.divIcon({
                    className: '',
                    html: '<div class="nb-pin img" style="background-image:url(' + nbEsc(p.markerImg) + ')"></div>',
                    iconSize: [34, 34], iconAnchor: [17, 17], popupAnchor: [0, -16]
                });
            }
            return L.divIcon({
                className: '',
                html: '<div class="nb-pin" style="background:' + nbEsc(p.color || '#1976D2') + '"><span>'
                    + nbEsc(p.icon || '📍') + '</span></div>',
                iconSize: [34, 34], iconAnchor: [17, 34], popupAnchor: [0, -32]
            });
        }

        function nbPopup(p) {
            var h = '<div class="nb-popup">';
            if (p.img) h += '<img src="' + nbEsc(p.img) + '" alt="" />';
            h += '<h4>' + nbEsc(p.name) + '</h4>';
            if (p.catName) h += '<div class="cat">' + nbEsc(p.icon) + ' ' + nbEsc(p.catName) + '</div>';
            if (p.badge) h += '<div style="display:inline-block;background:' + nbEsc(p.badgeColor)
                            + ';color:#fff;font-size:11px;font-weight:700;padding:3px 9px;border-radius:12px;margin-bottom:6px">'
                            + nbEsc(p.badge) + '</div>';
            if (p.highlight) h += '<div style="background:#fff8e1;border-left:3px solid #ffb300;padding:6px 9px;'
                            + 'border-radius:0 6px 6px 0;font-size:12px;color:#6d4c41;font-weight:600;margin-bottom:6px">💡 '
                            + nbEsc(p.highlight) + '</div>';
            if (p.desc) h += '<p>' + nbEsc(p.desc) + '</p>';
            var meta = [];
            if (p.dist) meta.push('📍 ' + nbEsc(p.dist));
            if (p.time) meta.push('⏱ ' + nbEsc(p.time));
            if (p.hours) meta.push('🕒 ' + nbEsc(p.hours));
                if (p.priceRange) meta.push('💰 ' + nbEsc(p.priceRange));
            if (meta.length) h += '<p>' + meta.join(' · ') + '</p>';
            if (p.phone) h += '<p>📞 <a href="tel:' + nbEsc(p.phone) + '">' + nbEsc(p.phone) + '</a></p>';
            if (p.nav) h += '<a class="nb-nav" target="_blank" rel="noopener" href="' + nbEsc(p.nav) + '">นำทางด้วย Google Maps</a>';
            h += '</div>';
            return h;
        }

        function nbInitMap() {
            var el = document.getElementById('nearbyMap');
            if (!el || typeof L === 'undefined') return;   // แผนที่โหลดไม่ได้ → รายการด้านล่างยังใช้งานได้ปกติ

            var zone = NB.zone || {};
            nbMap = L.map(el, { scrollWheelZoom: false })
                     .setView([zone.lat || 13.1748, zone.lng || 100.9306], zone.zoom || 12);
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                maxZoom: 19,
                attribution: '&copy; OpenStreetMap'
            }).addTo(nbMap);
            nbMap.on('click', function () { nbMap.scrollWheelZoom.enable(); });

            // ขอบเขตพื้นที่ (เช่น รูปทรงอำเภอศรีราชา) — ไม่ได้ใส่ไว้ก็ข้ามไป แล้วไป fit ตามหมุดแทน
            if (zone.geojson) {
                try {
                    nbBoundaryLayer = L.geoJSON(JSON.parse(zone.geojson), {
                        style: {
                            color: zone.line || '#00796B', weight: 2,
                            fillColor: zone.fill || '#00b09b', fillOpacity: 0.12
                        }
                    }).addTo(nbMap);
                    nbMap.fitBounds(nbBoundaryLayer.getBounds(), { padding: [16, 16] });
                } catch (e) { nbBoundaryLayer = null; }
            }

            var pts = [];
            (NB.places || []).forEach(function (p) {
                var m = L.marker([p.lat, p.lng], { icon: nbIcon(p) }).addTo(nbMap);
                m.bindPopup(nbPopup(p));
                m._nbCat = p.cat;
                m._nbId = p.id;
                nbMarkers.push(m);
                pts.push([p.lat, p.lng]);
            });

            // ไม่มีขอบเขต → ซูมให้พอดีกับหมุดทั้งหมด (ยังเห็นภาพรวมพื้นที่เหมือนกัน)
            if (!nbBoundaryLayer && pts.length > 0) {
                if (pts.length === 1) nbMap.setView(pts[0], 15);
                else nbMap.fitBounds(L.latLngBounds(pts), { padding: [30, 30] });
            }
        }

        // กรองประเภท: ซ่อนทั้งการ์ดและหมุดให้ตรงกัน
        function filterPlaces(category, btn) {
            document.querySelectorAll('.category-tab').forEach(function (tab) { tab.classList.remove('active'); });
            if (btn) btn.classList.add('active');

            document.querySelectorAll('.place-card').forEach(function (card) {
                card.style.display = (category === 'all' || card.dataset.category === category) ? 'block' : 'none';
            });
            // ซ่อนหัวข้อกลุ่มที่ไม่เหลือรายการ ไม่งั้นจะเห็นหัวข้อลอยไม่มีอะไรข้างใต้
            document.querySelectorAll('.cat-group').forEach(function (g) {
                g.style.display = (category === 'all' || g.dataset.category === category) ? '' : 'none';
            });

            if (!nbMap) return;
            var shown = [];
            nbMarkers.forEach(function (m) {
                var show = (category === 'all' || m._nbCat === category);
                if (show) { m.addTo(nbMap); shown.push(m.getLatLng()); }
                else { nbMap.removeLayer(m); }
            });
            if (shown.length > 1) nbMap.fitBounds(L.latLngBounds(shown), { padding: [30, 30] });
            else if (shown.length === 1) nbMap.setView(shown[0], 15);
            else if (nbBoundaryLayer) nbMap.fitBounds(nbBoundaryLayer.getBounds(), { padding: [16, 16] });
        }

        // แตะการ์ด → เลื่อนไปที่หมุดบนแผนที่แล้วเปิด popup
        document.addEventListener('DOMContentLoaded', function () {
            nbInitMap();
            document.querySelectorAll('.place-card').forEach(function (card) {
                card.addEventListener('click', function (ev) {
                    if (ev.target.closest('a')) return;      // กดปุ่มนำทาง/โทร ไม่ต้องเด้งแผนที่
                    var id = parseInt(card.dataset.id, 10);
                    var m = nbMarkers.filter(function (x) { return x._nbId === id; })[0];
                    if (!m || !nbMap) return;
                    nbMap.setView(m.getLatLng(), 16);
                    m.openPopup();
                    document.querySelector('.map-container').scrollIntoView({ behavior: 'smooth', block: 'center' });
                });
            });
        });
    </script>
</asp:Content>
