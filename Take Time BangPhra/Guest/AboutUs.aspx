<%@ Page Title="About Us - TakeTime Nature Resort" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="AboutUs.aspx.cs" Inherits="Take_Time_BangPhra.Guest.AboutUs" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .about-container {
            max-width: 1200px;
            margin: 0 auto;
            padding: 20px;
        }

        .page-header {
            background: linear-gradient(135deg, #2e7d32 0%, #1b5e20 100%);
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

        .hero-section {
            background: linear-gradient(rgba(0,0,0,0.4), rgba(0,0,0,0.4)),
                        url('https://images.unsplash.com/photo-1571003123894-1f0594d2b5d9?w=1200') center/cover;
            border-radius: 20px;
            padding: 80px 40px;
            text-align: center;
            color: white;
            margin-bottom: 40px;
        }

        .hero-section h1 {
            font-size: 42px;
            font-weight: 700;
            margin: 0 0 15px 0;
            text-shadow: 2px 2px 4px rgba(0,0,0,0.3);
        }

        .hero-section .tagline {
            font-size: 22px;
            opacity: 0.95;
            margin: 0;
            font-weight: 300;
        }

        .story-section {
            background: white;
            border-radius: 20px;
            padding: 40px;
            margin-bottom: 30px;
            box-shadow: 0 5px 20px rgba(0,0,0,0.08);
        }

        .story-section h3 {
            color: #2e7d32;
            font-size: 24px;
            margin: 0 0 20px 0;
            font-weight: 600;
            display: flex;
            align-items: center;
            gap: 12px;
        }

        .story-section p {
            color: #555;
            line-height: 1.8;
            font-size: 16px;
            margin-bottom: 15px;
        }

        .story-section p:last-child {
            margin-bottom: 0;
        }

        .values-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
            gap: 25px;
            margin-bottom: 30px;
        }

        .value-card {
            background: white;
            border-radius: 15px;
            padding: 30px;
            text-align: center;
            box-shadow: 0 5px 20px rgba(0,0,0,0.08);
            transition: all 0.3s ease;
        }

        .value-card:hover {
            transform: translateY(-5px);
            box-shadow: 0 10px 30px rgba(0,0,0,0.12);
        }

        .value-icon {
            width: 80px;
            height: 80px;
            margin: 0 auto 20px;
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 32px;
        }

        .value-card:nth-child(1) .value-icon { background: #e8f5e9; color: #2e7d32; }
        .value-card:nth-child(2) .value-icon { background: #e3f2fd; color: #1565c0; }
        .value-card:nth-child(3) .value-icon { background: #fff3e0; color: #ef6c00; }
        .value-card:nth-child(4) .value-icon { background: #f3e5f5; color: #7b1fa2; }

        .value-card h4 {
            margin: 0 0 10px 0;
            color: #333;
            font-size: 18px;
            font-weight: 600;
        }

        .value-card p {
            margin: 0;
            color: #666;
            font-size: 14px;
            line-height: 1.6;
        }

        .timeline {
            background: white;
            border-radius: 20px;
            padding: 40px;
            margin-bottom: 30px;
            box-shadow: 0 5px 20px rgba(0,0,0,0.08);
        }

        .timeline h3 {
            color: #2e7d32;
            font-size: 24px;
            margin: 0 0 30px 0;
            font-weight: 600;
            text-align: center;
        }

        .timeline-item {
            display: flex;
            gap: 20px;
            margin-bottom: 30px;
            position: relative;
        }

        .timeline-item:last-child {
            margin-bottom: 0;
        }

        .timeline-year {
            min-width: 80px;
            height: 40px;
            background: linear-gradient(135deg, #2e7d32, #1b5e20);
            color: white;
            border-radius: 20px;
            display: flex;
            align-items: center;
            justify-content: center;
            font-weight: 700;
            font-size: 14px;
        }

        .timeline-content {
            flex: 1;
            background: #f5f5f5;
            padding: 20px;
            border-radius: 12px;
        }

        .timeline-content h4 {
            margin: 0 0 8px 0;
            color: #333;
            font-size: 16px;
        }

        .timeline-content p {
            margin: 0;
            color: #666;
            font-size: 14px;
            line-height: 1.5;
        }

        .concept-section {
            background: linear-gradient(135deg, #e8f5e9, #c8e6c9);
            border-radius: 20px;
            padding: 40px;
            margin-bottom: 30px;
        }

        .concept-section h3 {
            color: #1b5e20;
            font-size: 24px;
            margin: 0 0 20px 0;
            font-weight: 600;
            text-align: center;
        }

        .concept-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
            gap: 20px;
        }

        .concept-item {
            background: white;
            border-radius: 12px;
            padding: 25px;
            display: flex;
            align-items: flex-start;
            gap: 15px;
        }

        .concept-item i {
            font-size: 28px;
            color: #2e7d32;
            flex-shrink: 0;
        }

        .concept-item h5 {
            margin: 0 0 8px 0;
            color: #333;
            font-size: 16px;
            font-weight: 600;
        }

        .concept-item p {
            margin: 0;
            color: #666;
            font-size: 13px;
            line-height: 1.5;
        }

        .team-section {
            background: white;
            border-radius: 20px;
            padding: 40px;
            margin-bottom: 30px;
            box-shadow: 0 5px 20px rgba(0,0,0,0.08);
            text-align: center;
        }

        .team-section h3 {
            color: #2e7d32;
            font-size: 24px;
            margin: 0 0 15px 0;
            font-weight: 600;
        }

        .team-section p {
            color: #666;
            font-size: 16px;
            line-height: 1.6;
            max-width: 800px;
            margin: 0 auto;
        }

        .quote-section {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            border-radius: 20px;
            padding: 50px 40px;
            text-align: center;
        }

        .quote-section blockquote {
            font-size: 24px;
            font-style: italic;
            font-weight: 300;
            margin: 0 0 15px 0;
            line-height: 1.5;
        }

        .quote-section cite {
            font-size: 16px;
            opacity: 0.9;
        }

        .no-data-panel {
            background: white;
            border-radius: 20px;
            padding: 60px 40px;
            text-align: center;
            box-shadow: 0 5px 20px rgba(0,0,0,0.08);
        }

        .no-data-panel i {
            font-size: 48px;
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
        }

        @media (max-width: 768px) {
            .hero-section {
                padding: 50px 25px;
            }

            .hero-section h1 {
                font-size: 28px;
            }

            .hero-section .tagline {
                font-size: 16px;
            }

            .story-section, .timeline, .concept-section, .team-section {
                padding: 25px;
            }

            .timeline-item {
                flex-direction: column;
                gap: 10px;
            }

            .timeline-year {
                min-width: auto;
                width: fit-content;
            }
        }
    </style>

    <div class="about-container">
        <!-- Header -->
        <div class="page-header">
            <h2><i class="fas fa-info-circle"></i> About Us</h2>
            <a href="Dashboard.aspx" class="btn-back">
                <i class="fas fa-arrow-left"></i> Back
            </a>
        </div>

        <!-- No Data Panel -->
        <asp:Panel ID="pnlNoData" runat="server" Visible="false" CssClass="no-data-panel">
            <i class="fas fa-database"></i>
            <h3>No About Us Data Available</h3>
            <p>Content has not been configured yet. Please contact the administrator.</p>
        </asp:Panel>

        <!-- Hero Section -->
        <div class="hero-section">
            <h1><%= GetSectionValue("hero", "Title") != "" ? GetSectionValue("hero", "Title") : "TakeTime Nature Resort" %></h1>
            <p class="tagline"><%= GetSectionValue("hero", "Content") != "" ? GetSectionValue("hero", "Content") : "พักผ่อน ใกล้ชิดธรรมชาติ ห่างไกลความวุ่นวาย" %></p>
        </div>

        <!-- Our Story -->
        <div class="story-section">
            <h3><i class="fas fa-book-open"></i> <%= GetSectionValue("story", "Title") != "" ? GetSectionValue("story", "Title") : "Our Story / เรื่องราวของเรา" %></h3>
            <p><%= GetSectionValue("story", "Content") != "" ? GetSectionValue("story", "Content") : "เรื่องราวของ TakeTime Nature Resort" %></p>
            <% if (!string.IsNullOrEmpty(GetSectionValue("story", "Sub_Content"))) { %>
                <p><%= GetSectionValue("story", "Sub_Content") %></p>
            <% } %>
        </div>

        <!-- Our Values -->
        <div class="values-grid">
            <asp:Repeater ID="rptValues" runat="server">
                <ItemTemplate>
                    <div class="value-card">
                        <div class="value-icon"><%# Eval("Icon") %></div>
                        <h4><%# Eval("Title") %></h4>
                        <p><%# Eval("Content") %></p>
                    </div>
                </ItemTemplate>
            </asp:Repeater>
        </div>

        <!-- Timeline -->
        <div class="timeline">
            <h3><i class="fas fa-history"></i> Our Journey</h3>
            <asp:Repeater ID="rptTimeline" runat="server">
                <ItemTemplate>
                    <div class="timeline-item">
                        <div class="timeline-year"><%# Eval("Year_Text") %></div>
                        <div class="timeline-content">
                            <h4><%# Eval("Title") %></h4>
                            <p><%# Eval("Content") %></p>
                        </div>
                    </div>
                </ItemTemplate>
            </asp:Repeater>
        </div>

        <!-- Resort Concept -->
        <div class="concept-section">
            <h3><i class="fas fa-lightbulb"></i> Resort Concept / แนวคิดที่พัก</h3>
            <div class="concept-grid">
                <asp:Repeater ID="rptConcepts" runat="server">
                    <ItemTemplate>
                        <div class="concept-item">
                            <div class="concept-icon"><%# Eval("Icon") %></div>
                            <div>
                                <h5><%# Eval("Title") %></h5>
                                <p><%# Eval("Content") %></p>
                            </div>
                        </div>
                    </ItemTemplate>
                </asp:Repeater>
            </div>
        </div>

        <!-- Team -->
        <div class="team-section">
            <h3><i class="fas fa-users"></i> <%= GetSectionValue("team", "Title") != "" ? GetSectionValue("team", "Title") : "Our Team / ทีมงานของเรา" %></h3>
            <p><%= GetSectionValue("team", "Content") != "" ? GetSectionValue("team", "Content") : "ทีมงานของเราพร้อมดูแลท่านด้วยความใส่ใจ" %></p>
        </div>

        <!-- Quote -->
        <div class="quote-section">
            <blockquote><%= GetSectionValue("quote", "Title") != "" ? GetSectionValue("quote", "Title") : "The greatest gift you can give yourself is time." %></blockquote>
            <cite><%= GetSectionValue("quote", "Content") != "" ? GetSectionValue("quote", "Content") : "— TakeTime Philosophy" %></cite>
        </div>
    </div>
</asp:Content>
