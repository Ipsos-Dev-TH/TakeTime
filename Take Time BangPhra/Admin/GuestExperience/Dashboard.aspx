<%@ Page Title="Guest Experience" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Dashboard.aspx.cs" Inherits="Take_Time_BangPhra.Admin.GuestExperience.Dashboard" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .experience-dashboard { padding: 20px 0; }
        .page-header { margin-bottom: 30px; }
        .page-header h1 { color: #5D4037; margin: 0 0 10px 0; }

        .score-section { background: linear-gradient(135deg, #5D4037, #8D6E63); border-radius: 16px; padding: 30px; color: white; margin-bottom: 30px; display: flex; align-items: center; justify-content: space-around; flex-wrap: wrap; gap: 20px; }
        .score-main { text-align: center; }
        .score-main .value { font-size: 72px; font-weight: 700; line-height: 1; }
        .score-main .label { font-size: 14px; opacity: 0.9; margin-top: 10px; }
        .score-breakdown { display: flex; gap: 30px; }
        .score-item { text-align: center; padding: 15px 25px; background: rgba(255,255,255,0.1); border-radius: 10px; }
        .score-item .value { font-size: 28px; font-weight: 600; }
        .score-item .label { font-size: 12px; opacity: 0.8; margin-top: 5px; }

        .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 20px; margin-bottom: 30px; }
        .metric-card { background: white; border-radius: 12px; padding: 25px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
        .metric-card h3 { margin: 0 0 20px 0; font-size: 16px; color: #333; display: flex; align-items: center; gap: 10px; }
        .metric-card h3 i { color: #5D4037; }

        .metric-list { list-style: none; padding: 0; margin: 0; }
        .metric-list li { display: flex; justify-content: space-between; padding: 12px 0; border-bottom: 1px solid #f5f5f5; }
        .metric-list li:last-child { border-bottom: none; }
        .metric-list .name { color: #666; }
        .metric-list .value { font-weight: 600; color: #333; }

        .progress-bar { height: 8px; background: #f0f0f0; border-radius: 4px; overflow: hidden; margin-top: 8px; }
        .progress-bar .fill { height: 100%; border-radius: 4px; }
        .fill-green { background: linear-gradient(90deg, #4CAF50, #8BC34A); }
        .fill-orange { background: linear-gradient(90deg, #FF9800, #FFC107); }
        .fill-red { background: linear-gradient(90deg, #f44336, #FF5722); }

        .review-item { padding: 15px; background: #f9f9f9; border-radius: 10px; margin-bottom: 10px; }
        .review-header { display: flex; justify-content: space-between; margin-bottom: 10px; }
        .review-name { font-weight: 600; }
        .review-rating { color: #FFB300; }
        .review-text { font-size: 14px; color: #666; line-height: 1.5; }
        .review-date { font-size: 12px; color: #999; margin-top: 8px; }

        .action-buttons { display: flex; gap: 10px; margin-top: 20px; }
        .btn-action { padding: 10px 20px; border-radius: 8px; border: none; cursor: pointer; font-weight: 500; }
        .btn-primary { background: #5D4037; color: white; }
        .btn-secondary { background: #f0f0f0; color: #333; }
    </style>

    <div class="experience-dashboard">
        <div class="page-header">
            <h1><i class="fas fa-concierge-bell"></i> Guest Experience Dashboard</h1>
            <p>ติดตามความพึงพอใจและประสบการณ์ของแขก</p>
        </div>

        <!-- Overall Score -->
        <div class="score-section">
            <div class="score-main">
                <div class="value"><asp:Label ID="lblOverallScore" runat="server" Text="4.5" /></div>
                <div class="label">Overall Guest Satisfaction</div>
            </div>
            <div class="score-breakdown">
                <div class="score-item">
                    <div class="value"><asp:Label ID="lblCleanliness" runat="server" Text="4.8" /></div>
                    <div class="label">ความสะอาด</div>
                </div>
                <div class="score-item">
                    <div class="value"><asp:Label ID="lblService" runat="server" Text="4.6" /></div>
                    <div class="label">การบริการ</div>
                </div>
                <div class="score-item">
                    <div class="value"><asp:Label ID="lblValue" runat="server" Text="4.3" /></div>
                    <div class="label">ความคุ้มค่า</div>
                </div>
                <div class="score-item">
                    <div class="value"><asp:Label ID="lblLocation" runat="server" Text="4.7" /></div>
                    <div class="label">ทำเล</div>
                </div>
            </div>
        </div>

        <div class="metrics-grid">
            <!-- Response Metrics -->
            <div class="metric-card">
                <h3><i class="fas fa-clock"></i> Response Metrics</h3>
                <ul class="metric-list">
                    <li>
                        <span class="name">เวลาตอบกลับเฉลี่ย</span>
                        <span class="value"><asp:Label ID="lblAvgResponse" runat="server" Text="5 นาที" /></span>
                    </li>
                    <li>
                        <span class="name">อัตราการตอบกลับ</span>
                        <span class="value"><asp:Label ID="lblResponseRate" runat="server" Text="98%" /></span>
                    </li>
                    <li>
                        <span class="name">ข้อความที่รอตอบ</span>
                        <span class="value"><asp:Label ID="lblPendingMessages" runat="server" Text="0" /></span>
                    </li>
                </ul>
                <div class="progress-bar">
                    <div class="fill fill-green" style="width: 98%"></div>
                </div>
            </div>

            <!-- Loyalty Metrics -->
            <div class="metric-card">
                <h3><i class="fas fa-heart"></i> Loyalty Metrics</h3>
                <ul class="metric-list">
                    <li>
                        <span class="name">Returning Guests</span>
                        <span class="value"><asp:Label ID="lblReturning" runat="server" Text="32%" /></span>
                    </li>
                    <li>
                        <span class="name">สมาชิก Loyalty Program</span>
                        <span class="value"><asp:Label ID="lblLoyaltyMembers" runat="server" Text="156" /></span>
                    </li>
                    <li>
                        <span class="name">Points Redeemed เดือนนี้</span>
                        <span class="value"><asp:Label ID="lblPointsRedeemed" runat="server" Text="12,500" /></span>
                    </li>
                </ul>
                <div class="progress-bar">
                    <div class="fill fill-orange" style="width: 32%"></div>
                </div>
            </div>

            <!-- Recent Reviews -->
            <div class="metric-card">
                <h3><i class="fas fa-star"></i> รีวิวล่าสุด</h3>
                <div class="review-item">
                    <div class="review-header">
                        <span class="review-name">คุณสมชาย</span>
                        <span class="review-rating"><i class="fas fa-star"></i><i class="fas fa-star"></i><i class="fas fa-star"></i><i class="fas fa-star"></i><i class="fas fa-star"></i></span>
                    </div>
                    <div class="review-text">บรรยากาศดีมาก ห้องสะอาด พนักงานใจดี จะกลับมาอีกแน่นอน!</div>
                    <div class="review-date">2 วันที่แล้ว</div>
                </div>
                <div class="review-item">
                    <div class="review-header">
                        <span class="review-name">คุณมานี</span>
                        <span class="review-rating"><i class="fas fa-star"></i><i class="fas fa-star"></i><i class="fas fa-star"></i><i class="fas fa-star"></i><i class="far fa-star"></i></span>
                    </div>
                    <div class="review-text">ที่พักดี แต่อยากให้มีกิจกรรมเพิ่มเติม</div>
                    <div class="review-date">5 วันที่แล้ว</div>
                </div>
                <div class="action-buttons">
                    <button class="btn-action btn-primary"><i class="fas fa-eye"></i> ดูทั้งหมด</button>
                    <button class="btn-action btn-secondary"><i class="fas fa-reply"></i> ตอบกลับ</button>
                </div>
            </div>
        </div>
    </div>
</asp:Content>
