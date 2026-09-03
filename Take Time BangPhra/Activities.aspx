<%@ Page Title="กิจกรรม" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Activities.aspx.cs" Inherits="Take_Time_BangPhra.ActivitiesPublic" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .act-hero {
            background: linear-gradient(135deg, #2e5d3a 0%, #4a7c59 60%, #6b9b7a 100%);
            color: #fff; border-radius: 16px; padding: 42px 28px; margin-bottom: 30px; text-align: center;
        }
        .act-hero h1 { margin: 0 0 10px; font-weight: 700; font-size: 2.1em; }
        .act-hero p { margin: 0; opacity: .92; font-size: 1.05em; }

        .act-tabs { display: flex; gap: 10px; justify-content: center; flex-wrap: wrap; margin-bottom: 26px; }
        .act-tab {
            border: 2px solid #4a7c59; background: #fff; color: #2e5d3a; border-radius: 30px;
            padding: 9px 24px; cursor: pointer; font-weight: 600; transition: .2s; font-size: 15px;
        }
        .act-tab:hover { background: #eaf3ec; }
        .act-tab.active { background: #4a7c59; color: #fff; box-shadow: 0 4px 12px rgba(74,124,89,.3); }

        .act-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 24px; }
        .act-card {
            background: #fff; border-radius: 14px; overflow: hidden; box-shadow: 0 3px 14px rgba(0,0,0,.09);
            display: flex; flex-direction: column; transition: transform .22s, box-shadow .22s;
        }
        .act-card:hover { transform: translateY(-5px); box-shadow: 0 10px 28px rgba(0,0,0,.16); }
        .act-thumb {
            height: 190px; background: linear-gradient(135deg, #dfeae2, #c3d9c9);
            background-size: cover; background-position: center;
            display: flex; align-items: center; justify-content: center; position: relative;
        }
        .act-thumb i { font-size: 3.4em; color: #6b9b7a; }
        .act-badges { position: absolute; top: 12px; left: 12px; display: flex; gap: 6px; flex-wrap: wrap; }
        .act-badge {
            font-size: 11.5px; font-weight: 700; padding: 4px 11px; border-radius: 20px; color: #fff;
            box-shadow: 0 2px 6px rgba(0,0,0,.2);
        }
        .badge-free { background: #27ae60; }
        .badge-paid { background: #e67e22; }
        .badge-book { background: #2980b9; }
        .act-body { padding: 18px 20px 20px; flex: 1; display: flex; flex-direction: column; }
        .act-body h3 { margin: 0 0 8px; font-size: 1.18em; color: #2e5d3a; font-weight: 700; }
        .act-desc { color: #667; font-size: 14px; line-height: 1.6; flex: 1; margin-bottom: 14px; }
        .act-meta { display: flex; flex-wrap: wrap; gap: 14px; font-size: 13px; color: #7a8a80; margin-bottom: 12px; }
        .act-meta span i { color: #6b9b7a; margin-right: 4px; }
        .act-price {
            font-size: 1.15em; font-weight: 700; color: #e67e22;
            padding-top: 12px; border-top: 1px dashed #e2e8e4;
        }
        .act-price.free { color: #27ae60; }
        .badge-nearby { background: #16a085; }
        .badge-featured { background: #d81b60; }

        /* หัวข้อกลุ่มประเภทของสถานที่ใกล้เคียง */
        .nb-group { margin-bottom: 26px; }
        .nb-group-title {
            display: flex; align-items: center; gap: 8px;
            font-size: 1.12em; font-weight: 700; color: #4a7c59;
            margin: 0 0 14px; padding-bottom: 8px; border-bottom: 2px solid #eaf3ec;
        }
        .nb-group-title small { color: #9aa; font-weight: 400; font-size: .78em; }

        /* ข้อความโปรโมท "ที่นี่ดียังไง" */
        .nb-highlight {
            background: #fff8e1; border-left: 3px solid #ffb300;
            padding: 8px 11px; border-radius: 0 8px 8px 0;
            font-size: 13px; color: #6d4c41; font-weight: 600;
            margin-bottom: 10px; line-height: 1.5;
        }

        /* แผนที่สถานที่ใกล้เคียง */
        .nb-map-wrap {
            width: 100%; height: 440px; border-radius: 14px; overflow: hidden;
            box-shadow: 0 3px 14px rgba(0,0,0,.09); margin-bottom: 10px;
        }
        .nb-map-wrap #publicNearbyMap { width: 100%; height: 100%; }
        .nb-map-hint { text-align: center; font-size: 13px; color: #90a096; margin-bottom: 22px; }

        .nb-actions { display: flex; gap: 8px; padding-top: 12px; border-top: 1px dashed #e2e8e4; }
        .nb-btn {
            flex: 1; text-align: center; padding: 9px 10px; border-radius: 9px;
            font-size: 13.5px; font-weight: 700; text-decoration: none; transition: .2s;
        }
        .nb-btn-nav { background: #1a73e8; color: #fff; }
        .nb-btn-nav:hover { background: #1558b0; color: #fff; text-decoration: none; }
        .nb-btn-call { background: #eaf3ec; color: #2e5d3a; }
        .nb-btn-call:hover { background: #d8e9dd; color: #2e5d3a; text-decoration: none; }

        /* หมุดบนแผนที่ */
        .nb-pin {
            width: 34px; height: 34px; line-height: 32px; border-radius: 50% 50% 50% 0;
            transform: rotate(-45deg); border: 2px solid #fff;
            box-shadow: 0 2px 6px rgba(0,0,0,.35); text-align: center; font-size: 16px;
        }
        .nb-pin > span { display: inline-block; transform: rotate(45deg); }
        .nb-pin.img { background-size: cover; background-position: center; border-radius: 50%; transform: none; }
        .nb-popup { min-width: 190px; max-width: 240px; }
        .nb-popup img { width: 100%; height: 110px; object-fit: cover; border-radius: 8px; margin-bottom: 8px; }
        .nb-popup h4 { margin: 0 0 4px; font-size: 15px; font-weight: 700; color: #2e5d3a; }
        .nb-popup .cat { font-size: 11px; color: #666; margin-bottom: 6px; }
        .nb-popup p { margin: 0 0 8px; font-size: 12px; color: #444; }
        .nb-popup .nb-nav {
            display: block; text-align: center; background: #1a73e8; color: #fff;
            padding: 8px; border-radius: 8px; text-decoration: none; font-weight: 600; font-size: 13px;
        }
        .nb-popup .nb-nav:hover { background: #1558b0; color: #fff; text-decoration: none; }

        .act-empty { text-align: center; padding: 60px 20px; color: #90a096; }
        .act-empty i { font-size: 3.4em; margin-bottom: 14px; display: block; opacity: .5; }
        .act-cta {
            margin-top: 34px; background: #f5f9f6; border: 2px dashed #b8d4c1;
            border-radius: 14px; padding: 26px; text-align: center;
        }
        .act-cta h4 { color: #2e5d3a; font-weight: 700; margin: 0 0 8px; }
        .act-cta p { color: #667; margin-bottom: 16px; }
        @media (max-width: 600px) {
            .act-hero { padding: 30px 18px; } .act-hero h1 { font-size: 1.6em; }
            .act-grid { grid-template-columns: 1fr; }
        }
    </style>

    <div class="container" style="max-width: 1200px; padding-top: 20px; padding-bottom: 50px;">
        <div class="act-hero">
            <h1><i class="fas fa-person-hiking"></i> กิจกรรมและสถานที่ใกล้เคียง</h1>
            <p>สนุกกับกิจกรรมภายในที่พัก และสถานที่แนะนำรอบ ๆ พร้อมแผนที่นำทาง</p>
        </div>

        <div class="act-tabs">
            <button type="button" class="act-tab active" data-cat="ALL">ทั้งหมด</button>
            <button type="button" class="act-tab" data-cat="ON_PROPERTY">
                <i class="fas fa-tree"></i> ในที่พัก
            </button>
            <button type="button" class="act-tab" data-cat="OFF_PROPERTY">
                <i class="fas fa-mountain-sun"></i> กิจกรรมนอกที่พัก
            </button>
            <button type="button" class="act-tab" data-cat="NEARBY">
                <i class="fas fa-map-location-dot"></i> สถานที่ใกล้เคียง
            </button>
        </div>

        <asp:Literal ID="litActivities" runat="server" />

        <div class="act-cta">
            <h4><i class="fas fa-calendar-check"></i> อยากใช้บริการกิจกรรมที่ต้องจองล่วงหน้า?</h4>
            <p>ผู้ที่เข้าพักกับเราสามารถจองช่วงเวลาได้เองผ่าน Guest Portal — สแกน QR ในห้องพักได้เลย</p>
            <a href="/Reserve?command=reserve" class="btn btn-success btn-lg">
                <i class="fas fa-bed"></i> จองที่พักเลย
            </a>
        </div>
    </div>

    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/leaflet.min.css" />
    <script src="https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/leaflet.min.js"></script>
    <script>
        (function () {
            var tabs = document.querySelectorAll('.act-tab');
            tabs.forEach(function (tab) {
                tab.addEventListener('click', function () {
                    tabs.forEach(function (t) { t.classList.remove('active'); });
                    tab.classList.add('active');
                    var cat = tab.getAttribute('data-cat');
                    document.querySelectorAll('.act-card').forEach(function (c) {
                        c.style.display = (cat === 'ALL' || c.getAttribute('data-cat') === cat) ? '' : 'none';
                    });
                    document.querySelectorAll('.act-section').forEach(function (s) {
                        var visible = s.querySelectorAll('.act-card:not([style*="display: none"])').length;
                        s.style.display = visible > 0 ? '' : 'none';
                    });
                    // แผนที่โผล่เฉพาะตอนดู "ทั้งหมด" หรือ "สถานที่ใกล้เคียง"
                    var showMap = (cat === 'ALL' || cat === 'NEARBY');
                    document.querySelectorAll('[data-mapsection]').forEach(function (el) {
                        el.style.display = showMap ? '' : 'none';
                    });
                    // Leaflet ต้องคำนวณขนาดใหม่หลังถูกซ่อนแล้วโชว์ ไม่งั้นแผนที่จะเป็นพื้นเทา
                    if (showMap && window.nbPublicMap) setTimeout(function () { window.nbPublicMap.invalidateSize(); }, 60);
                });
            });
        })();

        // ── แผนที่สถานที่ใกล้เคียง ────────────────────────────────────────────
        (function () {
            var NB = <%= MapJson %>;
            var el = document.getElementById('publicNearbyMap');
            if (!el || typeof L === 'undefined' || !NB || !NB.places || !NB.places.length) return;

            function esc(t) {
                return (t == null ? '' : String(t))
                    .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
                    .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
            }

            function icon(p) {
                if (p.markerImg) {
                    return L.divIcon({
                        className: '',
                        html: '<div class="nb-pin img" style="background-image:url(' + esc(p.markerImg) + ')"></div>',
                        iconSize: [34, 34], iconAnchor: [17, 17], popupAnchor: [0, -16]
                    });
                }
                return L.divIcon({
                    className: '',
                    html: '<div class="nb-pin" style="background:' + esc(p.color || '#16a085') + '"><span>'
                        + esc(p.icon || '📍') + '</span></div>',
                    iconSize: [34, 34], iconAnchor: [17, 34], popupAnchor: [0, -32]
                });
            }

            function popup(p) {
                var h = '<div class="nb-popup">';
                if (p.img) h += '<img src="' + esc(p.img) + '" alt="" />';
                h += '<h4>' + esc(p.name) + '</h4>';
                if (p.catName) h += '<div class="cat">' + esc(p.icon) + ' ' + esc(p.catName) + '</div>';
                if (p.badge) h += '<div style="display:inline-block;background:' + esc(p.badgeColor)
                                + ';color:#fff;font-size:11px;font-weight:700;padding:3px 9px;border-radius:12px;margin-bottom:6px">'
                                + esc(p.badge) + '</div>';
                if (p.highlight) h += '<div style="background:#fff8e1;border-left:3px solid #ffb300;padding:6px 9px;'
                                + 'border-radius:0 6px 6px 0;font-size:12px;color:#6d4c41;font-weight:600;margin-bottom:6px">💡 '
                                + esc(p.highlight) + '</div>';
                if (p.desc) h += '<p>' + esc(p.desc) + '</p>';
                var meta = [];
                if (p.dist) meta.push('📍 ' + esc(p.dist));
                if (p.time) meta.push('⏱ ' + esc(p.time));
                if (p.hours) meta.push('🕒 ' + esc(p.hours));
                if (p.priceRange) meta.push('💰 ' + esc(p.priceRange));
                if (meta.length) h += '<p>' + meta.join(' · ') + '</p>';
                if (p.nav) h += '<a class="nb-nav" target="_blank" rel="noopener" href="' + esc(p.nav) + '">นำทางด้วย Google Maps</a>';
                return h + '</div>';
            }

            var zone = NB.zone || {};
            var map = L.map(el, { scrollWheelZoom: false })
                       .setView([zone.lat || 13.1748, zone.lng || 100.9306], zone.zoom || 12);
            window.nbPublicMap = map;
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
                { maxZoom: 19, attribution: '&copy; OpenStreetMap' }).addTo(map);
            map.on('click', function () { map.scrollWheelZoom.enable(); });

            // ขอบเขตพื้นที่ (เช่น รูปทรงอำเภอศรีราชา) — ไม่ได้ตั้งไว้ก็ย่อ/ขยายตามหมุดแทน
            var boundary = null;
            if (zone.geojson) {
                try {
                    boundary = L.geoJSON(JSON.parse(zone.geojson), {
                        style: { color: zone.line || '#00796B', weight: 2,
                                 fillColor: zone.fill || '#16a085', fillOpacity: 0.12 }
                    }).addTo(map);
                    map.fitBounds(boundary.getBounds(), { padding: [16, 16] });
                } catch (e) { boundary = null; }
            }

            var pts = [];
            NB.places.forEach(function (p) {
                L.marker([p.lat, p.lng], { icon: icon(p) }).addTo(map).bindPopup(popup(p));
                pts.push([p.lat, p.lng]);
            });
            if (!boundary && pts.length) {
                if (pts.length === 1) map.setView(pts[0], 15);
                else map.fitBounds(L.latLngBounds(pts), { padding: [30, 30] });
            }
        })();
    </script>
</asp:Content>
