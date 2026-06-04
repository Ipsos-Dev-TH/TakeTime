<%@ Page Title="Activities" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Activities.aspx.cs" Inherits="Take_Time_BangPhra.Guest.Activities" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .activities-container {
            max-width: 1200px;
            margin: 0 auto;
            padding: 15px;
            padding-bottom: 100px;
        }

        .page-header {
            background: linear-gradient(135deg, #00b09b 0%, #96c93d 100%);
            color: white;
            padding: 25px;
            border-radius: 20px;
            margin-bottom: 25px;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .page-header h2 {
            margin: 0;
            font-size: 24px;
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

        /* Tab Navigation */
        .activity-tabs {
            display: flex;
            gap: 0;
            margin-bottom: 25px;
            background: white;
            border-radius: 15px;
            overflow: hidden;
            box-shadow: 0 3px 15px rgba(0,0,0,0.08);
        }

        .activity-tab {
            flex: 1;
            padding: 16px 20px;
            text-align: center;
            cursor: pointer;
            font-weight: 600;
            font-size: 15px;
            color: #666;
            transition: all 0.3s ease;
            border: none;
            background: white;
            position: relative;
        }

        .activity-tab:hover {
            background: #f8f9fa;
        }

        .activity-tab.active {
            color: white;
        }

        .activity-tab.tab-onsite.active {
            background: linear-gradient(135deg, #667eea, #764ba2);
        }

        .activity-tab.tab-offsite.active {
            background: linear-gradient(135deg, #00b09b, #96c93d);
        }

        .activity-tab i {
            display: block;
            font-size: 22px;
            margin-bottom: 5px;
        }

        .tab-count {
            display: inline-block;
            background: rgba(255,255,255,0.3);
            padding: 2px 10px;
            border-radius: 10px;
            font-size: 11px;
            margin-left: 5px;
        }

        /* Activity Panels */
        .activity-panel {
            display: none;
        }

        .activity-panel.active {
            display: block;
        }

        /* Section Header */
        .section-header {
            display: flex;
            align-items: center;
            gap: 12px;
            margin-bottom: 20px;
            padding: 0 5px;
        }

        .section-header .icon-circle {
            width: 45px;
            height: 45px;
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 20px;
            color: white;
            flex-shrink: 0;
        }

        .section-header.onsite .icon-circle {
            background: linear-gradient(135deg, #667eea, #764ba2);
        }

        .section-header.offsite .icon-circle {
            background: linear-gradient(135deg, #00b09b, #96c93d);
        }

        .section-header h3 {
            margin: 0;
            font-size: 20px;
            color: #333;
            font-weight: 700;
        }

        .section-header p {
            margin: 0;
            font-size: 13px;
            color: #888;
        }

        /* Activity Cards */
        .activity-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
            gap: 20px;
            margin-bottom: 30px;
        }

        .activity-card {
            background: white;
            border-radius: 18px;
            overflow: hidden;
            box-shadow: 0 4px 20px rgba(0,0,0,0.08);
            transition: all 0.3s ease;
        }

        .activity-card:hover {
            transform: translateY(-5px);
            box-shadow: 0 8px 30px rgba(0,0,0,0.15);
        }

        .activity-image {
            width: 100%;
            height: 200px;
            object-fit: cover;
            display: block;
            background: linear-gradient(135deg, #e0e0e0, #f5f5f5);
        }

        .activity-image-placeholder {
            width: 100%;
            height: 200px;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 48px;
            color: white;
        }

        .activity-image-placeholder.onsite {
            background: linear-gradient(135deg, #667eea, #764ba2);
        }

        .activity-image-placeholder.offsite {
            background: linear-gradient(135deg, #00b09b, #96c93d);
        }

        .activity-content {
            padding: 20px;
        }

        .activity-content h4 {
            margin: 0 0 8px 0;
            font-size: 18px;
            font-weight: 700;
            color: #333;
        }

        .activity-content p {
            margin: 0 0 15px 0;
            color: #666;
            font-size: 14px;
            line-height: 1.6;
        }

        .activity-meta {
            display: flex;
            flex-wrap: wrap;
            gap: 10px;
        }

        .activity-meta .meta-item {
            display: inline-flex;
            align-items: center;
            gap: 5px;
            font-size: 12px;
            color: #888;
            background: #f5f5f5;
            padding: 5px 12px;
            border-radius: 20px;
        }

        .activity-meta .meta-item i {
            font-size: 11px;
        }

        .activity-meta .meta-item.price {
            background: #e8f5e9;
            color: #2e7d32;
            font-weight: 600;
        }

        .activity-meta .meta-item.free {
            background: #e3f2fd;
            color: #1565c0;
            font-weight: 600;
        }

        /* Empty State */
        .empty-state {
            text-align: center;
            padding: 60px 20px;
            color: #999;
        }

        .empty-state i {
            font-size: 60px;
            margin-bottom: 20px;
            opacity: 0.4;
        }

        .empty-state p {
            font-size: 16px;
        }

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
            color: #00b09b;
        }

        .bottom-nav a span {
            font-size: 10px;
            color: #888;
            font-weight: 500;
        }

        .bottom-nav a.active span {
            color: #00b09b;
        }

        @media (min-width: 768px) {
            .activities-container {
                padding-bottom: 30px;
            }

            .bottom-nav {
                display: none;
            }
        }

        @media (max-width: 380px) {
            .activity-grid {
                grid-template-columns: 1fr;
            }

            .activity-tab {
                padding: 12px 10px;
                font-size: 13px;
            }

            .activity-tab i {
                font-size: 18px;
            }
        }
    </style>

    <div class="activities-container">
        <!-- Header -->
        <div class="page-header">
            <h2><i class="fas fa-hiking"></i> กิจกรรม</h2>
            <a href="Dashboard.aspx" class="btn-back">
                <i class="fas fa-arrow-left"></i> กลับ
            </a>
        </div>

        <!-- Tab Navigation -->
        <div class="activity-tabs">
            <button class="activity-tab tab-onsite active" onclick="switchTab('onsite', this)">
                <i class="fas fa-hotel"></i>
                กิจกรรมในที่พัก
                <span class="tab-count"><asp:Label ID="lblOnsiteCount" runat="server">0</asp:Label></span>
            </button>
            <button class="activity-tab tab-offsite" onclick="switchTab('offsite', this)">
                <i class="fas fa-mountain"></i>
                กิจกรรมนอกที่พัก
                <span class="tab-count"><asp:Label ID="lblOffsiteCount" runat="server">0</asp:Label></span>
            </button>
        </div>

        <!-- On-Property Activities -->
        <div id="panelOnsite" class="activity-panel active">
            <div class="section-header onsite">
                <div class="icon-circle"><i class="fas fa-hotel"></i></div>
                <div>
                    <h3>กิจกรรมในที่พัก</h3>
                    <p>สิ่งที่คุณสามารถทำได้ภายในรีสอร์ท</p>
                </div>
            </div>

            <div class="activity-grid">
                <asp:Repeater ID="rptOnsiteActivities" runat="server">
                    <ItemTemplate>
                        <div class="activity-card">
                            <asp:Panel runat="server" Visible='<%# !string.IsNullOrEmpty(Eval("ImagePath")?.ToString()) %>'>
                                <img src='<%# ResolveUrl(Eval("ImagePath").ToString()) %>' alt='<%# Eval("ActivityName") %>' class="activity-image"
                                     onerror="this.parentElement.innerHTML='<div class=\'activity-image-placeholder onsite\'><i class=\'fas fa-hotel\'></i></div>';" />
                            </asp:Panel>
                            <asp:Panel runat="server" Visible='<%# string.IsNullOrEmpty(Eval("ImagePath")?.ToString()) %>'>
                                <div class="activity-image-placeholder onsite">
                                    <i class="fas fa-hotel"></i>
                                </div>
                            </asp:Panel>
                            <div class="activity-content">
                                <h4><%# Eval("ActivityName") %></h4>
                                <p><%# Eval("Description") %></p>
                                <div class="activity-meta">
                                    <%# Eval("Price") != DBNull.Value && Convert.ToDecimal(Eval("Price")) > 0
                                        ? "<span class='meta-item price'><i class='fas fa-tag'></i> ฿" + Convert.ToDecimal(Eval("Price")).ToString("N0") + "</span>"
                                        : "<span class='meta-item free'><i class='fas fa-gift'></i> ฟรี</span>" %>
                                    <%# !string.IsNullOrEmpty(Eval("Duration")?.ToString())
                                        ? "<span class='meta-item'><i class='fas fa-clock'></i> " + Eval("Duration") + "</span>"
                                        : "" %>
                                    <%# !string.IsNullOrEmpty(Eval("Location")?.ToString())
                                        ? "<span class='meta-item'><i class='fas fa-map-marker-alt'></i> " + Eval("Location") + "</span>"
                                        : "" %>
                                </div>
                            </div>
                        </div>
                    </ItemTemplate>
                </asp:Repeater>
            </div>

            <asp:Label ID="lblNoOnsite" runat="server" Visible="false">
                <div class="empty-state">
                    <i class="fas fa-hotel"></i>
                    <p>ยังไม่มีกิจกรรมในที่พัก</p>
                </div>
            </asp:Label>
        </div>

        <!-- Off-Property Activities -->
        <div id="panelOffsite" class="activity-panel">
            <div class="section-header offsite">
                <div class="icon-circle"><i class="fas fa-mountain"></i></div>
                <div>
                    <h3>กิจกรรมนอกที่พัก</h3>
                    <p>สถานที่ท่องเที่ยวและกิจกรรมในบริเวณใกล้เคียง</p>
                </div>
            </div>

            <div class="activity-grid">
                <asp:Repeater ID="rptOffsiteActivities" runat="server">
                    <ItemTemplate>
                        <div class="activity-card">
                            <asp:Panel runat="server" Visible='<%# !string.IsNullOrEmpty(Eval("ImagePath")?.ToString()) %>'>
                                <img src='<%# ResolveUrl(Eval("ImagePath").ToString()) %>' alt='<%# Eval("ActivityName") %>' class="activity-image"
                                     onerror="this.parentElement.innerHTML='<div class=\'activity-image-placeholder offsite\'><i class=\'fas fa-mountain\'></i></div>';" />
                            </asp:Panel>
                            <asp:Panel runat="server" Visible='<%# string.IsNullOrEmpty(Eval("ImagePath")?.ToString()) %>'>
                                <div class="activity-image-placeholder offsite">
                                    <i class="fas fa-mountain"></i>
                                </div>
                            </asp:Panel>
                            <div class="activity-content">
                                <h4><%# Eval("ActivityName") %></h4>
                                <p><%# Eval("Description") %></p>
                                <div class="activity-meta">
                                    <%# Eval("Price") != DBNull.Value && Convert.ToDecimal(Eval("Price")) > 0
                                        ? "<span class='meta-item price'><i class='fas fa-tag'></i> ฿" + Convert.ToDecimal(Eval("Price")).ToString("N0") + "</span>"
                                        : (Eval("Price") == DBNull.Value
                                            ? "<span class='meta-item'><i class='fas fa-tag'></i> ราคาแตกต่างกัน</span>"
                                            : "<span class='meta-item free'><i class='fas fa-gift'></i> ฟรี</span>") %>
                                    <%# !string.IsNullOrEmpty(Eval("Duration")?.ToString())
                                        ? "<span class='meta-item'><i class='fas fa-clock'></i> " + Eval("Duration") + "</span>"
                                        : "" %>
                                    <%# !string.IsNullOrEmpty(Eval("Location")?.ToString())
                                        ? "<span class='meta-item'><i class='fas fa-map-marker-alt'></i> " + Eval("Location") + "</span>"
                                        : "" %>
                                </div>
                            </div>
                        </div>
                    </ItemTemplate>
                </asp:Repeater>
            </div>

            <asp:Label ID="lblNoOffsite" runat="server" Visible="false">
                <div class="empty-state">
                    <i class="fas fa-mountain"></i>
                    <p>ยังไม่มีกิจกรรมนอกที่พัก</p>
                </div>
            </asp:Label>
        </div>
    </div>

    <!-- Bottom Navigation -->
    <div class="bottom-nav">
        <a href="Dashboard.aspx">
            <i class="fas fa-home"></i>
            <span>หน้าหลัก</span>
        </a>
        <a href="RoomService.aspx">
            <i class="fas fa-utensils"></i>
            <span>สั่งอาหาร</span>
        </a>
        <a href="Activities.aspx" class="active">
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

    <script>
        function switchTab(tab, el) {
            document.querySelectorAll('.activity-tab').forEach(function (t) { t.classList.remove('active'); });
            document.querySelectorAll('.activity-panel').forEach(function (p) { p.classList.remove('active'); });
            el.classList.add('active');
            if (tab === 'onsite') {
                document.getElementById('panelOnsite').classList.add('active');
            } else {
                document.getElementById('panelOffsite').classList.add('active');
            }
        }
    </script>
</asp:Content>
