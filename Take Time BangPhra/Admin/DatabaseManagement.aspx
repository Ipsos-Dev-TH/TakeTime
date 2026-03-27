<%@ Page Title="จัดการฐานข้อมูล" Language="C#" MaintainScrollPositionOnPostback="true" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="DatabaseManagement.aspx.cs" Inherits="Take_Time_BangPhra.Admin.DatabaseManagement" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .db-mgmt-page {
            padding: 20px;
            max-width: 1400px;
            margin: 0 auto;
        }

        .db-mgmt-header {
            text-align: center;
            margin-bottom: 30px;
        }

        .db-mgmt-header h2 {
            color: #5D4037;
            font-weight: 600;
            margin-bottom: 5px;
        }

        .db-mgmt-header p {
            color: #8D6E63;
            font-size: 14px;
        }

        /* Search Box */
        .db-search-box {
            max-width: 500px;
            margin: 0 auto 30px;
            position: relative;
        }

        .db-search-box input {
            width: 100%;
            padding: 12px 20px 12px 45px;
            border: 2px solid #D7CCC8;
            border-radius: 25px;
            font-size: 15px;
            outline: none;
            transition: border-color 0.3s;
            font-family: 'Prompt', sans-serif;
        }

        .db-search-box input:focus {
            border-color: #8D6E63;
        }

        .db-search-box i {
            position: absolute;
            left: 18px;
            top: 50%;
            transform: translateY(-50%);
            color: #8D6E63;
        }

        /* Stats Bar */
        .db-stats {
            display: flex;
            justify-content: center;
            gap: 30px;
            margin-bottom: 30px;
            flex-wrap: wrap;
        }

        .db-stat-item {
            text-align: center;
            padding: 10px 20px;
            background: linear-gradient(135deg, #EFEBE9 0%, #D7CCC8 100%);
            border-radius: 10px;
            min-width: 120px;
        }

        .db-stat-item .stat-number {
            font-size: 24px;
            font-weight: 700;
            color: #5D4037;
        }

        .db-stat-item .stat-label {
            font-size: 12px;
            color: #8D6E63;
        }

        /* Category Sections */
        .db-category {
            margin-bottom: 30px;
        }

        .db-category-header {
            display: flex;
            align-items: center;
            gap: 10px;
            margin-bottom: 15px;
            padding: 12px 20px;
            background: linear-gradient(135deg, #5D4037 0%, #8D6E63 100%);
            border-radius: 10px;
            color: white;
            cursor: pointer;
            user-select: none;
            transition: transform 0.2s;
        }

        .db-category-header:hover {
            transform: translateY(-1px);
        }

        .db-category-header h3 {
            margin: 0;
            font-size: 16px;
            font-weight: 600;
            flex: 1;
        }

        .db-category-header .category-icon {
            width: 35px;
            height: 35px;
            background: rgba(255,255,255,0.2);
            border-radius: 8px;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 16px;
        }

        .db-category-header .category-count {
            background: rgba(255,255,255,0.25);
            padding: 3px 10px;
            border-radius: 15px;
            font-size: 13px;
        }

        .db-category-header .toggle-icon {
            transition: transform 0.3s;
        }

        .db-category-header.collapsed .toggle-icon {
            transform: rotate(-90deg);
        }

        /* Table Cards Grid */
        .db-table-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
            gap: 12px;
            transition: max-height 0.4s ease, opacity 0.3s ease;
            overflow: hidden;
        }

        .db-table-grid.collapsed {
            max-height: 0;
            opacity: 0;
            margin-bottom: 0;
        }

        /* Table Card */
        .db-table-card {
            background: white;
            border: 1px solid #EFEBE9;
            border-radius: 10px;
            padding: 15px 18px;
            display: flex;
            align-items: center;
            gap: 12px;
            cursor: pointer;
            transition: all 0.2s ease;
            text-decoration: none;
            color: inherit;
        }

        .db-table-card:hover {
            border-color: #8D6E63;
            box-shadow: 0 4px 15px rgba(93, 64, 55, 0.15);
            transform: translateY(-2px);
            text-decoration: none;
            color: inherit;
        }

        .db-table-card .card-icon {
            width: 40px;
            height: 40px;
            border-radius: 8px;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 18px;
            flex-shrink: 0;
        }

        .db-table-card .card-info {
            flex: 1;
            min-width: 0;
        }

        .db-table-card .card-info .table-name {
            font-weight: 600;
            font-size: 14px;
            color: #3E2723;
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
        }

        .db-table-card .card-info .table-desc {
            font-size: 12px;
            color: #8D6E63;
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
        }

        .db-table-card .card-arrow {
            color: #BCAAA4;
            font-size: 14px;
            transition: transform 0.2s;
        }

        .db-table-card:hover .card-arrow {
            transform: translateX(3px);
            color: #5D4037;
        }

        /* Color variants for card icons */
        .icon-blue { background: #E3F2FD; color: #1565C0; }
        .icon-green { background: #E8F5E9; color: #2E7D32; }
        .icon-orange { background: #FFF3E0; color: #E65100; }
        .icon-purple { background: #F3E5F5; color: #6A1B9A; }
        .icon-red { background: #FFEBEE; color: #C62828; }
        .icon-teal { background: #E0F2F1; color: #00695C; }
        .icon-brown { background: #EFEBE9; color: #4E342E; }
        .icon-pink { background: #FCE4EC; color: #AD1457; }
        .icon-indigo { background: #E8EAF6; color: #283593; }
        .icon-amber { background: #FFF8E1; color: #FF6F00; }

        /* No results */
        .db-no-results {
            text-align: center;
            padding: 40px;
            color: #8D6E63;
            display: none;
        }

        .db-no-results i {
            font-size: 48px;
            margin-bottom: 10px;
            opacity: 0.5;
        }

        /* Back to top button */
        .db-back-top {
            position: fixed;
            bottom: 30px;
            right: 30px;
            width: 45px;
            height: 45px;
            background: #5D4037;
            color: white;
            border: none;
            border-radius: 50%;
            font-size: 18px;
            cursor: pointer;
            display: none;
            align-items: center;
            justify-content: center;
            box-shadow: 0 4px 15px rgba(0,0,0,0.2);
            z-index: 100;
            transition: background 0.2s;
        }

        .db-back-top:hover {
            background: #3E2723;
        }

        /* Responsive */
        @media (max-width: 768px) {
            .db-mgmt-page {
                padding: 10px;
            }

            .db-table-grid {
                grid-template-columns: 1fr;
            }

            .db-stats {
                gap: 10px;
            }

            .db-stat-item {
                min-width: 80px;
                padding: 8px 12px;
            }

            .db-stat-item .stat-number {
                font-size: 18px;
            }

            .db-category-header h3 {
                font-size: 14px;
            }
        }
    </style>

    <div class="db-mgmt-page">
        <!-- Header -->
        <div class="db-mgmt-header">
            <h2><i class="fas fa-database"></i> จัดการฐานข้อมูล</h2>
            <p>ระบบจัดการข้อมูลทุกตาราง - เพิ่ม แก้ไข ลบ ข้อมูลในฐานข้อมูล</p>
        </div>

        <!-- Search -->
        <div class="db-search-box">
            <i class="fas fa-search"></i>
            <input type="text" id="txtSearch" placeholder="ค้นหาตาราง... (เช่น Address, Customer, Product)" oninput="filterTables(this.value)" autocomplete="off" />
        </div>

        <!-- Stats -->
        <div class="db-stats">
            <div class="db-stat-item">
                <div class="stat-number" id="statTotal">0</div>
                <div class="stat-label">ตารางทั้งหมด</div>
            </div>
            <div class="db-stat-item">
                <div class="stat-number" id="statCategories">0</div>
                <div class="stat-label">หมวดหมู่</div>
            </div>
        </div>

        <!-- Table Categories Container (rendered from server) -->
        <asp:Panel ID="pnlCategories" runat="server"></asp:Panel>

        <!-- No Results -->
        <div class="db-no-results" id="noResults">
            <i class="fas fa-search"></i>
            <p>ไม่พบตารางที่ค้นหา</p>
        </div>
    </div>

    <!-- Back to Top -->
    <button class="db-back-top" id="btnBackTop" onclick="window.scrollTo({top:0,behavior:'smooth'})">
        <i class="fas fa-chevron-up"></i>
    </button>

    <script type="text/javascript">
        // Filter tables by search text
        function filterTables(query) {
            query = query.toLowerCase().trim();
            var cards = document.querySelectorAll('.db-table-card');
            var categories = document.querySelectorAll('.db-category');
            var visibleCount = 0;

            categories.forEach(function (cat) {
                var grid = cat.querySelector('.db-table-grid');
                var header = cat.querySelector('.db-category-header');
                var catVisible = 0;

                var catCards = cat.querySelectorAll('.db-table-card');
                catCards.forEach(function (card) {
                    var name = (card.getAttribute('data-table') || '').toLowerCase();
                    var desc = (card.getAttribute('data-desc') || '').toLowerCase();
                    if (query === '' || name.indexOf(query) >= 0 || desc.indexOf(query) >= 0) {
                        card.style.display = '';
                        catVisible++;
                        visibleCount++;
                    } else {
                        card.style.display = 'none';
                    }
                });

                cat.style.display = catVisible > 0 ? '' : 'none';

                // Update count badge
                var countBadge = header.querySelector('.category-count');
                if (countBadge) {
                    countBadge.textContent = catVisible + ' ตาราง';
                }

                // Expand if searching
                if (query !== '' && catVisible > 0) {
                    grid.classList.remove('collapsed');
                    header.classList.remove('collapsed');
                }
            });

            document.getElementById('noResults').style.display = visibleCount === 0 ? 'block' : 'none';
        }

        // Toggle category collapse
        function toggleCategory(header) {
            var grid = header.nextElementSibling;
            header.classList.toggle('collapsed');
            grid.classList.toggle('collapsed');
        }

        // Back to top button visibility
        window.addEventListener('scroll', function () {
            var btn = document.getElementById('btnBackTop');
            btn.style.display = window.scrollY > 300 ? 'flex' : 'none';
        });

        // Count stats on load
        document.addEventListener('DOMContentLoaded', function () {
            var totalCards = document.querySelectorAll('.db-table-card').length;
            var totalCats = document.querySelectorAll('.db-category').length;
            document.getElementById('statTotal').textContent = totalCards;
            document.getElementById('statCategories').textContent = totalCats;
        });
    </script>
</asp:Content>
