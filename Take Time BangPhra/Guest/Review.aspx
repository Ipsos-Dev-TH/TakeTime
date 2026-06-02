<%@ Page Title="Review & Earn Rewards" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Review.aspx.cs" Inherits="Take_Time_BangPhra.Guest.Review" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .review-container {
            max-width: 1200px;
            margin: 0 auto;
            padding: 20px;
        }

        .page-header {
            background: linear-gradient(135deg, #FF9800 0%, #F57C00 100%);
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

        /* Hero Reward Section */
        .reward-hero {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            border-radius: 20px;
            padding: 40px;
            text-align: center;
            color: white;
            margin-bottom: 30px;
            position: relative;
            overflow: hidden;
        }

        .reward-hero::before {
            content: '';
            position: absolute;
            top: -50%;
            left: -50%;
            width: 200%;
            height: 200%;
            background: radial-gradient(circle, rgba(255,255,255,0.1) 0%, transparent 50%);
            animation: shimmer 3s infinite;
        }

        @keyframes shimmer {
            0%, 100% { transform: rotate(0deg); }
            50% { transform: rotate(180deg); }
        }

        .reward-hero h1 {
            font-size: 32px;
            margin: 0 0 15px 0;
            position: relative;
            z-index: 1;
        }

        .reward-hero p {
            font-size: 18px;
            margin: 0;
            opacity: 0.95;
            position: relative;
            z-index: 1;
        }

        .points-highlight {
            display: inline-flex;
            align-items: center;
            gap: 10px;
            background: rgba(255,255,255,0.2);
            padding: 15px 30px;
            border-radius: 30px;
            margin-top: 20px;
            font-size: 24px;
            font-weight: 700;
            position: relative;
            z-index: 1;
        }

        .points-highlight i {
            font-size: 28px;
            color: #FFD700;
        }

        /* Member Status Card */
        .member-status {
            background: white;
            border-radius: 15px;
            padding: 25px;
            margin-bottom: 30px;
            box-shadow: 0 5px 20px rgba(0,0,0,0.08);
            display: flex;
            align-items: center;
            gap: 20px;
            flex-wrap: wrap;
        }

        .member-badge {
            width: 80px;
            height: 80px;
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 36px;
            flex-shrink: 0;
        }

        .member-badge.bronze { background: linear-gradient(135deg, #cd7f32, #a0522d); color: white; }
        .member-badge.silver { background: linear-gradient(135deg, #c0c0c0, #808080); color: white; }
        .member-badge.gold { background: linear-gradient(135deg, #ffd700, #daa520); color: white; }
        .member-badge.platinum { background: linear-gradient(135deg, #e5e4e2, #a8a8a8); color: #333; }

        .member-info {
            flex: 1;
        }

        .member-info h3 {
            margin: 0 0 5px 0;
            color: #333;
            font-size: 20px;
        }

        .member-info .tier-name {
            color: #FF9800;
            font-weight: 700;
        }

        .member-info .points-balance {
            font-size: 14px;
            color: #666;
        }

        .points-balance strong {
            color: #667eea;
            font-size: 18px;
        }

        .progress-to-next {
            flex: 1;
            min-width: 200px;
        }

        .progress-label {
            display: flex;
            justify-content: space-between;
            font-size: 12px;
            color: #888;
            margin-bottom: 5px;
        }

        .progress-bar-container {
            background: #e0e0e0;
            border-radius: 10px;
            height: 10px;
            overflow: hidden;
        }

        .progress-bar-fill {
            height: 100%;
            background: linear-gradient(90deg, #667eea, #764ba2);
            border-radius: 10px;
            transition: width 0.5s ease;
        }

        /* Google Review CTA */
        .google-review-card {
            background: white;
            border-radius: 20px;
            padding: 40px;
            text-align: center;
            box-shadow: 0 5px 20px rgba(0,0,0,0.08);
            margin-bottom: 30px;
            border: 3px solid #4285F4;
        }

        .google-logo {
            display: flex;
            align-items: center;
            justify-content: center;
            gap: 10px;
            margin-bottom: 20px;
        }

        .google-logo img {
            height: 40px;
        }

        .google-logo span {
            font-size: 28px;
            font-weight: 500;
            color: #333;
        }

        .google-review-card h3 {
            margin: 0 0 15px 0;
            color: #333;
            font-size: 24px;
        }

        .google-review-card p {
            color: #666;
            font-size: 16px;
            margin: 0 0 25px 0;
            line-height: 1.6;
        }

        .star-rating {
            display: flex;
            justify-content: center;
            gap: 5px;
            margin-bottom: 20px;
        }

        .star-rating i {
            font-size: 40px;
            color: #FFC107;
            cursor: pointer;
            transition: transform 0.2s;
        }

        .star-rating i:hover {
            transform: scale(1.2);
        }

        .btn-google-review {
            display: inline-flex;
            align-items: center;
            gap: 12px;
            background: #4285F4;
            color: white;
            padding: 18px 40px;
            border-radius: 30px;
            text-decoration: none;
            font-size: 18px;
            font-weight: 600;
            transition: all 0.3s ease;
            box-shadow: 0 4px 15px rgba(66, 133, 244, 0.3);
        }

        .btn-google-review:hover {
            transform: translateY(-3px);
            box-shadow: 0 8px 25px rgba(66, 133, 244, 0.4);
            color: white;
            text-decoration: none;
        }

        .btn-google-review img {
            height: 24px;
        }

        /* Rewards Grid */
        .rewards-section {
            margin-bottom: 30px;
        }

        .rewards-section h3 {
            text-align: center;
            color: #333;
            font-size: 24px;
            margin: 0 0 25px 0;
        }

        .rewards-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
            gap: 20px;
        }

        .reward-card {
            background: white;
            border-radius: 15px;
            padding: 25px;
            text-align: center;
            box-shadow: 0 5px 20px rgba(0,0,0,0.08);
            transition: all 0.3s ease;
            position: relative;
            overflow: hidden;
        }

        .reward-card:hover {
            transform: translateY(-5px);
            box-shadow: 0 10px 30px rgba(0,0,0,0.12);
        }

        .reward-card.highlight {
            border: 2px solid #FF9800;
        }

        .reward-card.highlight::before {
            content: 'BEST VALUE';
            position: absolute;
            top: 15px;
            right: -30px;
            background: #FF9800;
            color: white;
            padding: 5px 40px;
            font-size: 10px;
            font-weight: 700;
            transform: rotate(45deg);
        }

        .reward-icon {
            width: 70px;
            height: 70px;
            margin: 0 auto 15px;
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 32px;
        }

        .reward-card:nth-child(1) .reward-icon { background: #e3f2fd; color: #1976D2; }
        .reward-card:nth-child(2) .reward-icon { background: #fff3e0; color: #F57C00; }
        .reward-card:nth-child(3) .reward-icon { background: #e8f5e9; color: #388E3C; }
        .reward-card:nth-child(4) .reward-icon { background: #fce4ec; color: #C2185B; }

        .reward-card h4 {
            margin: 0 0 10px 0;
            color: #333;
            font-size: 18px;
        }

        .reward-card p {
            margin: 0 0 15px 0;
            color: #666;
            font-size: 14px;
            line-height: 1.5;
        }

        .reward-points {
            display: inline-flex;
            align-items: center;
            gap: 5px;
            background: linear-gradient(135deg, #667eea, #764ba2);
            color: white;
            padding: 8px 20px;
            border-radius: 20px;
            font-weight: 600;
            font-size: 14px;
        }

        /* Tier Benefits */
        .tier-section {
            background: white;
            border-radius: 20px;
            padding: 35px;
            margin-bottom: 30px;
            box-shadow: 0 5px 20px rgba(0,0,0,0.08);
        }

        .tier-section h3 {
            text-align: center;
            color: #333;
            font-size: 24px;
            margin: 0 0 25px 0;
        }

        .tier-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
            gap: 20px;
        }

        .tier-card {
            border-radius: 15px;
            padding: 25px;
            text-align: center;
            position: relative;
        }

        .tier-card.bronze {
            background: linear-gradient(135deg, #fbe9e7, #ffccbc);
            border: 2px solid #cd7f32;
        }

        .tier-card.silver {
            background: linear-gradient(135deg, #f5f5f5, #e0e0e0);
            border: 2px solid #9e9e9e;
        }

        .tier-card.gold {
            background: linear-gradient(135deg, #fff8e1, #ffecb3);
            border: 2px solid #ffc107;
        }

        .tier-card.platinum {
            background: linear-gradient(135deg, #eceff1, #cfd8dc);
            border: 2px solid #607d8b;
        }

        .tier-card .tier-icon {
            font-size: 36px;
            margin-bottom: 10px;
        }

        .tier-card.bronze .tier-icon { color: #cd7f32; }
        .tier-card.silver .tier-icon { color: #9e9e9e; }
        .tier-card.gold .tier-icon { color: #ffc107; }
        .tier-card.platinum .tier-icon { color: #607d8b; }

        .tier-card h4 {
            margin: 0 0 5px 0;
            font-size: 18px;
            color: #333;
        }

        .tier-card .points-required {
            font-size: 12px;
            color: #666;
            margin-bottom: 15px;
        }

        .tier-card ul {
            text-align: left;
            padding-left: 20px;
            margin: 0;
            font-size: 13px;
            color: #555;
        }

        .tier-card li {
            margin-bottom: 5px;
        }

        .tier-card.current::after {
            content: 'YOUR TIER';
            position: absolute;
            top: -10px;
            left: 50%;
            transform: translateX(-50%);
            background: #667eea;
            color: white;
            padding: 3px 15px;
            border-radius: 10px;
            font-size: 10px;
            font-weight: 700;
        }

        /* How It Works */
        .how-it-works {
            background: linear-gradient(135deg, #e8f5e9, #c8e6c9);
            border-radius: 20px;
            padding: 35px;
            margin-bottom: 30px;
        }

        .how-it-works h3 {
            text-align: center;
            color: #1b5e20;
            font-size: 24px;
            margin: 0 0 25px 0;
        }

        .steps-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 30px;
        }

        .step-item {
            text-align: center;
        }

        .step-number {
            width: 50px;
            height: 50px;
            margin: 0 auto 15px;
            background: #2e7d32;
            color: white;
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 24px;
            font-weight: 700;
        }

        .step-item h4 {
            margin: 0 0 8px 0;
            color: #1b5e20;
            font-size: 16px;
        }

        .step-item p {
            margin: 0;
            color: #555;
            font-size: 13px;
            line-height: 1.5;
        }

        /* Review History */
        .review-history {
            background: white;
            border-radius: 15px;
            padding: 25px;
            box-shadow: 0 5px 20px rgba(0,0,0,0.08);
        }

        .review-history h3 {
            margin: 0 0 20px 0;
            color: #333;
            font-size: 20px;
        }

        .no-reviews {
            text-align: center;
            padding: 30px;
            color: #888;
        }

        .no-reviews i {
            font-size: 48px;
            margin-bottom: 15px;
            opacity: 0.5;
        }

        @media (max-width: 768px) {
            .reward-hero h1 {
                font-size: 24px;
            }

            .member-status {
                flex-direction: column;
                text-align: center;
            }

            .progress-to-next {
                width: 100%;
            }

            .google-review-card {
                padding: 25px;
            }

            .star-rating i {
                font-size: 32px;
            }
        }
    </style>

    <div class="review-container">
        <!-- Header -->
        <div class="page-header">
            <h2><i class="fas fa-star"></i> Review & Earn Rewards</h2>
            <a href="Dashboard.aspx" class="btn-back">
                <i class="fas fa-arrow-left"></i> Back
            </a>
        </div>

        <!-- Reward Hero -->
        <div class="reward-hero">
            <h1><i class="fas fa-gift"></i> รีวิวและรับรางวัล!</h1>
            <p>แบ่งปันประสบการณ์การพักของคุณบน Google และรับแต้มสะสมทันที</p>
            <div class="points-highlight">
                <i class="fas fa-coins"></i>
                <span>+<asp:Label ID="lblReviewPoints" runat="server">100</asp:Label> Points</span>
            </div>
        </div>

        <!-- Member Status -->
        <div class="member-status">
            <div class="member-badge" id="memberBadge" runat="server">
                <i class="fas fa-crown"></i>
            </div>
            <div class="member-info">
                <h3>
                    <asp:Label ID="lblGuestName" runat="server">Guest</asp:Label>
                    <span class="tier-name">(<asp:Label ID="lblCurrentTier" runat="server">Bronze</asp:Label> Member)</span>
                </h3>
                <p class="points-balance">
                    คะแนนสะสมปัจจุบัน: <strong><asp:Label ID="lblCurrentPoints" runat="server">0</asp:Label> Points</strong>
                </p>
            </div>
            <div class="progress-to-next">
                <div class="progress-label">
                    <span>Progress to <asp:Label ID="lblNextTier" runat="server">Silver</asp:Label></span>
                    <span><asp:Label ID="lblPointsToNext" runat="server">500</asp:Label> pts more</span>
                </div>
                <div class="progress-bar-container">
                    <div class="progress-bar-fill" style="width: 30%;" id="progressBar" runat="server"></div>
                </div>
            </div>
        </div>

        <!-- Google Review CTA -->
        <div class="google-review-card">
            <div class="google-logo">
                <img src="https://www.google.com/images/branding/googlelogo/2x/googlelogo_color_92x30dp.png" alt="Google" style="height: 30px;">
                <span>Reviews</span>
            </div>
            <h3>แบ่งปันประสบการณ์ของคุณ</h3>
            <p>
                รีวิวของคุณช่วยให้นักท่องเที่ยวคนอื่นตัดสินใจได้ดีขึ้น<br>
                และเป็นกำลังใจให้ทีมงานของเราในการพัฒนาบริการให้ดียิ่งขึ้น
            </p>

            <div class="star-rating">
                <i class="fas fa-star" onclick="selectRating(1)"></i>
                <i class="fas fa-star" onclick="selectRating(2)"></i>
                <i class="fas fa-star" onclick="selectRating(3)"></i>
                <i class="fas fa-star" onclick="selectRating(4)"></i>
                <i class="fas fa-star" onclick="selectRating(5)"></i>
            </div>

            <a href="https://www.google.com/travel/hotels/entity/CgsIsoLIgrCxpI6cARAB/reviews"
               target="_blank"
               class="btn-google-review"
               id="btnGoogleReview"
               onclick="trackReviewClick()">
                <img src="https://www.gstatic.com/images/branding/product/1x/googleg_48dp.png" alt="G">
                เขียนรีวิวบน Google
            </a>

            <p style="margin-top: 20px; font-size: 13px; color: #888;">
                <i class="fas fa-info-circle"></i>
                หลังจากรีวิวเสร็จ กรุณากดปุ่ม "ยืนยันการรีวิว" ด้านล่างเพื่อรับแต้ม
            </p>
        </div>

        <!-- Upload Screenshot & Confirm -->
        <div class="screenshot-upload-card" style="background: white; border-radius: 20px; padding: 30px; box-shadow: 0 5px 20px rgba(0,0,0,0.08); margin-bottom: 30px; border: 2px dashed #667eea;">
            <h3 style="text-align: center; color: #333; margin: 0 0 10px 0; font-size: 20px;">
                <i class="fas fa-camera" style="color: #667eea;"></i> ยืนยันการรีวิว
            </h3>
            <p style="text-align: center; color: #666; font-size: 14px; margin: 0 0 20px 0;">
                แนบภาพหน้าจอรีวิวของคุณเพื่อรับแต้มสะสม
            </p>

            <div id="uploadArea" style="text-align: center; padding: 25px; border: 2px dashed #ddd; border-radius: 15px; background: #fafafa; margin-bottom: 20px; cursor: pointer; transition: all 0.3s ease;"
                 onclick="document.getElementById('<%= fuReviewScreenshot.ClientID %>').click();">
                <div id="previewContainer" style="display: none; margin-bottom: 15px;">
                    <img id="imgPreview" src="" alt="Preview" style="max-width: 300px; max-height: 200px; border-radius: 10px; box-shadow: 0 3px 10px rgba(0,0,0,0.15);" />
                </div>
                <div id="uploadPrompt">
                    <i class="fas fa-cloud-upload-alt" style="font-size: 48px; color: #667eea; margin-bottom: 10px; display: block;"></i>
                    <span style="color: #667eea; font-weight: 600; font-size: 16px;">คลิกเพื่ออัพโหลดภาพหน้าจอรีวิว</span><br>
                    <span style="color: #999; font-size: 12px;">รองรับไฟล์ JPG, PNG, GIF, WEBP (ไม่เกิน 10 MB)</span>
                </div>
                <asp:FileUpload ID="fuReviewScreenshot" runat="server"
                    style="display: none;"
                    accept=".jpg,.jpeg,.png,.gif,.webp"
                    onchange="previewScreenshot(this);" />
            </div>

            <div style="text-align: center;">
                <asp:Button ID="btnConfirmReview" runat="server" Text="ยืนยันการรีวิว - รับ 100 Points"
                    CssClass="btn btn-success btn-lg" OnClick="btnConfirmReview_Click"
                    style="background: linear-gradient(135deg, #4CAF50, #388E3C); border: none; padding: 15px 40px; border-radius: 30px; font-size: 18px; color: white; cursor: pointer;" />
                <asp:Label ID="lblReviewStatus" runat="server" CssClass="d-block mt-3"></asp:Label>
            </div>
        </div>

        <!-- Rewards You Can Earn -->
        <div class="rewards-section">
            <h3><i class="fas fa-gift"></i> รางวัลที่คุณสามารถรับได้</h3>
            <div class="rewards-grid">
                <div class="reward-card">
                    <div class="reward-icon">
                        <i class="fas fa-star"></i>
                    </div>
                    <h4>รีวิวบน Google</h4>
                    <p>เขียนรีวิวแบ่งปันประสบการณ์การพักของคุณ</p>
                    <span class="reward-points"><i class="fas fa-coins"></i> +100 Points</span>
                </div>

                <div class="reward-card highlight">
                    <div class="reward-icon">
                        <i class="fas fa-camera"></i>
                    </div>
                    <h4>รีวิวพร้อมรูปภาพ</h4>
                    <p>อัพโหลดรูปภาพประกอบรีวิวของคุณ</p>
                    <span class="reward-points"><i class="fas fa-coins"></i> +150 Points</span>
                </div>

                <div class="reward-card">
                    <div class="reward-icon">
                        <i class="fas fa-share-alt"></i>
                    </div>
                    <h4>แชร์บน Social Media</h4>
                    <p>แชร์ประสบการณ์บน Facebook หรือ Instagram</p>
                    <span class="reward-points"><i class="fas fa-coins"></i> +50 Points</span>
                </div>

                <div class="reward-card">
                    <div class="reward-icon">
                        <i class="fas fa-user-plus"></i>
                    </div>
                    <h4>แนะนำเพื่อน</h4>
                    <p>เพื่อนจองที่พักด้วยรหัสแนะนำของคุณ</p>
                    <span class="reward-points"><i class="fas fa-coins"></i> +200 Points</span>
                </div>
            </div>
        </div>

        <!-- Tier Benefits -->
        <div class="tier-section">
            <h3><i class="fas fa-crown"></i> สิทธิพิเศษตามระดับสมาชิก</h3>
            <div class="tier-grid">
                <div class="tier-card bronze" id="tierBronze" runat="server">
                    <div class="tier-icon"><i class="fas fa-medal"></i></div>
                    <h4>Bronze</h4>
                    <p class="points-required">0 - 499 Points</p>
                    <ul>
                        <li>ส่วนลด 5% สำหรับการจองครั้งต่อไป</li>
                        <li>เช็คเอาท์ช้าฟรี 1 ชม.</li>
                        <li>น้ำดื่มฟรี 2 ขวด/วัน</li>
                    </ul>
                </div>

                <div class="tier-card silver" id="tierSilver" runat="server">
                    <div class="tier-icon"><i class="fas fa-medal"></i></div>
                    <h4>Silver</h4>
                    <p class="points-required">500 - 999 Points</p>
                    <ul>
                        <li>ส่วนลด 10% สำหรับการจองครั้งต่อไป</li>
                        <li>เช็คเอาท์ช้าฟรี 2 ชม.</li>
                        <li>อาหารเช้าฟรี 1 มื้อ</li>
                        <li>อัพเกรดห้องพัก (ขึ้นอยู่กับห้องว่าง)</li>
                    </ul>
                </div>

                <div class="tier-card gold" id="tierGold" runat="server">
                    <div class="tier-icon"><i class="fas fa-crown"></i></div>
                    <h4>Gold</h4>
                    <p class="points-required">1,000 - 2,499 Points</p>
                    <ul>
                        <li>ส่วนลด 15% สำหรับการจองครั้งต่อไป</li>
                        <li>เช็คอินเร็ว / เช็คเอาท์ช้าฟรี</li>
                        <li>อาหารเช้าฟรีทุกวัน</li>
                        <li>อัพเกรดห้องพักอัตโนมัติ</li>
                        <li>บริการสปา 30 นาที</li>
                    </ul>
                </div>

                <div class="tier-card platinum" id="tierPlatinum" runat="server">
                    <div class="tier-icon"><i class="fas fa-gem"></i></div>
                    <h4>Platinum</h4>
                    <p class="points-required">2,500+ Points</p>
                    <ul>
                        <li>ส่วนลด 20% สำหรับการจองครั้งต่อไป</li>
                        <li>พักฟรี 1 คืน ทุก 10 คืน</li>
                        <li>บริการรับ-ส่งสนามบินฟรี</li>
                        <li>อาหารค่ำฟรี 1 มื้อ/การเข้าพัก</li>
                        <li>เข้าถึงห้อง Lounge VIP</li>
                        <li>บริการซักรีดฟรี</li>
                    </ul>
                </div>
            </div>
        </div>

        <!-- How It Works -->
        <div class="how-it-works">
            <h3><i class="fas fa-question-circle"></i> วิธีการสะสมแต้ม</h3>
            <div class="steps-grid">
                <div class="step-item">
                    <div class="step-number">1</div>
                    <h4>เขียนรีวิว</h4>
                    <p>คลิกปุ่ม "เขียนรีวิวบน Google" และแบ่งปันประสบการณ์ของคุณ</p>
                </div>
                <div class="step-item">
                    <div class="step-number">2</div>
                    <h4>แคปหน้าจอ</h4>
                    <p>ถ่ายภาพหน้าจอรีวิวของคุณเป็นหลักฐาน</p>
                </div>
                <div class="step-item">
                    <div class="step-number">3</div>
                    <h4>อัพโหลดและยืนยัน</h4>
                    <p>แนบภาพหน้าจอแล้วกด "ยืนยันการรีวิว" เพื่อรับแต้มทันที</p>
                </div>
                <div class="step-item">
                    <div class="step-number">4</div>
                    <h4>แลกรับสิทธิพิเศษ</h4>
                    <p>ใช้แต้มแลกส่วนลดหรืออัพเกรดระดับสมาชิก</p>
                </div>
            </div>
        </div>

        <!-- Review History -->
        <div class="review-history">
            <h3><i class="fas fa-history"></i> ประวัติการรีวิวของคุณ</h3>
            <asp:Repeater ID="rptReviewHistory" runat="server">
                <ItemTemplate>
                    <div class="review-item" style="padding: 15px; border-bottom: 1px solid #eee;">
                        <div style="display: flex; justify-content: space-between; align-items: center;">
                            <div>
                                <strong><%# Eval("ReviewType") %></strong>
                                <span style="color: #888; font-size: 13px; margin-left: 10px;"><%# Eval("ReviewDate", "{0:dd MMM yyyy}") %></span>
                            </div>
                            <span style="background: #e8f5e9; color: #2e7d32; padding: 5px 15px; border-radius: 15px; font-weight: 600;">
                                +<%# Eval("PointsEarned") %> pts
                            </span>
                        </div>
                    </div>
                </ItemTemplate>
            </asp:Repeater>
            <asp:Label ID="lblNoReviews" runat="server" Visible="true">
                <div class="no-reviews">
                    <i class="fas fa-star"></i>
                    <p>คุณยังไม่มีประวัติการรีวิว<br>เริ่มต้นรีวิววันนี้เพื่อรับแต้มสะสม!</p>
                </div>
            </asp:Label>
        </div>
    </div>

    <script>
        function selectRating(rating) {
            const stars = document.querySelectorAll('.star-rating i');
            stars.forEach((star, index) => {
                if (index < rating) {
                    star.style.color = '#FFC107';
                } else {
                    star.style.color = '#e0e0e0';
                }
            });
        }

        function trackReviewClick() {
            // Track that user clicked to review
            if (typeof gtag !== 'undefined') {
                gtag('event', 'click', {
                    'event_category': 'Review',
                    'event_label': 'Google Review Click'
                });
            }

            // Store timestamp in session/local storage
            localStorage.setItem('reviewClickTime', new Date().toISOString());
        }

        // Initialize with 5 stars selected
        selectRating(5);

        function previewScreenshot(input) {
            if (input.files && input.files[0]) {
                var reader = new FileReader();
                reader.onload = function (e) {
                    document.getElementById('imgPreview').src = e.target.result;
                    document.getElementById('previewContainer').style.display = 'block';
                    document.getElementById('uploadPrompt').innerHTML =
                        '<span style="color: #4CAF50; font-weight: 600;"><i class="fas fa-check-circle"></i> ' + input.files[0].name + '</span><br>' +
                        '<span style="color: #999; font-size: 12px;">คลิกเพื่อเปลี่ยนรูป</span>';
                    document.getElementById('uploadArea').style.borderColor = '#4CAF50';
                    document.getElementById('uploadArea').style.background = '#f0fff0';
                };
                reader.readAsDataURL(input.files[0]);
            }
        }
    </script>
</asp:Content>
