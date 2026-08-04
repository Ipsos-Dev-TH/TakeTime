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
            <h1><i class="fas fa-person-hiking"></i> กิจกรรมและสิ่งอำนวยความสะดวก</h1>
            <p>สนุกกับกิจกรรมภายในที่พัก และสถานที่ท่องเที่ยวใกล้เคียง</p>
        </div>

        <div class="act-tabs">
            <button type="button" class="act-tab active" data-cat="ALL">ทั้งหมด</button>
            <button type="button" class="act-tab" data-cat="ON_PROPERTY">
                <i class="fas fa-tree"></i> ในที่พัก
            </button>
            <button type="button" class="act-tab" data-cat="OFF_PROPERTY">
                <i class="fas fa-map-location-dot"></i> ใกล้เคียง
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
                });
            });
        })();
    </script>
</asp:Content>
