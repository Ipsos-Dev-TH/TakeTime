<%@ Page Title="Guest Profile 360°" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="GuestProfile.aspx.cs" Inherits="Take_Time_BangPhra.Admin.CRM.GuestProfile" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .guest-profile-container {
            max-width: 1600px;
            margin: 20px auto;
            padding: 20px;
        }

        .page-header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 30px;
            border-radius: 12px;
            margin-bottom: 30px;
            box-shadow: 0 4px 6px rgba(0,0,0,0.1);
        }

        .page-header h1 {
            margin: 0 0 10px 0;
            font-size: 32px;
            font-weight: bold;
        }

        .search-section {
            background: white;
            padding: 25px;
            border-radius: 12px;
            margin-bottom: 30px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
        }

        .search-row {
            display: flex;
            gap: 15px;
            align-items: flex-end;
        }

        .search-field {
            flex: 1;
        }

        .search-field label {
            display: block;
            font-weight: 600;
            margin-bottom: 8px;
            color: #555;
        }

        .search-input {
            width: 100%;
            padding: 12px;
            border: 1px solid #ddd;
            border-radius: 6px;
            font-size: 14px;
        }

        .btn-search {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 12px 30px;
            border: none;
            border-radius: 6px;
            font-size: 16px;
            font-weight: 500;
            cursor: pointer;
            transition: all 0.3s;
        }

        .btn-search:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(102, 126, 234, 0.3);
        }

        .profile-not-found {
            text-align: center;
            padding: 60px 20px;
            background: white;
            border-radius: 12px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
        }

        .profile-not-found i {
            font-size: 80px;
            color: #ddd;
            margin-bottom: 20px;
        }

        .profile-grid {
            display: grid;
            grid-template-columns: 1fr 2fr;
            gap: 20px;
            margin-bottom: 20px;
        }

        @media (max-width: 1200px) {
            .profile-grid {
                grid-template-columns: 1fr;
            }
        }

        .profile-card {
            background: white;
            border-radius: 12px;
            padding: 25px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
        }

        .profile-card h3 {
            margin: 0 0 20px 0;
            color: #333;
            font-size: 20px;
            border-bottom: 2px solid #667eea;
            padding-bottom: 10px;
            display: flex;
            align-items: center;
            gap: 10px;
        }

        .customer-header {
            display: flex;
            align-items: center;
            gap: 20px;
            padding: 25px;
            background: white;
            border-radius: 12px;
            margin-bottom: 20px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
        }

        .customer-avatar {
            width: 100px;
            height: 100px;
            border-radius: 50%;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 40px;
            color: white;
            font-weight: bold;
        }

        .customer-info h2 {
            margin: 0 0 10px 0;
            color: #333;
            font-size: 28px;
        }

        .customer-badges {
            display: flex;
            gap: 10px;
            flex-wrap: wrap;
        }

        .badge {
            padding: 6px 12px;
            border-radius: 20px;
            font-size: 12px;
            font-weight: 500;
            color: white;
        }

        .badge-vip {
            background: #E74C3C;
        }

        .badge-tier {
            background: #667eea;
        }

        .info-row {
            display: grid;
            grid-template-columns: 120px 1fr;
            padding: 12px 0;
            border-bottom: 1px solid #f0f0f0;
        }

        .info-row:last-child {
            border-bottom: none;
        }

        .info-label {
            font-weight: 600;
            color: #666;
        }

        .info-value {
            color: #333;
        }

        .stat-boxes {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 15px;
            margin-bottom: 20px;
        }

        .stat-box {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 20px;
            border-radius: 10px;
            text-align: center;
        }

        .stat-box h4 {
            margin: 0 0 10px 0;
            font-size: 14px;
            opacity: 0.9;
        }

        .stat-box .value {
            font-size: 28px;
            font-weight: bold;
        }

        .booking-list {
            max-height: 400px;
            overflow-y: auto;
        }

        .booking-item {
            padding: 15px;
            background: #f8f9fa;
            border-radius: 8px;
            margin-bottom: 10px;
            border-left: 4px solid #667eea;
        }

        .booking-item h4 {
            margin: 0 0 8px 0;
            color: #333;
        }

        .booking-item p {
            margin: 5px 0;
            font-size: 14px;
            color: #666;
        }

        .review-item {
            padding: 15px;
            background: #f8f9fa;
            border-radius: 8px;
            margin-bottom: 15px;
        }

        .review-stars {
            color: #f39c12;
            font-size: 18px;
            margin-bottom: 8px;
        }

        .review-text {
            color: #555;
            line-height: 1.6;
            margin-bottom: 8px;
        }

        .review-date {
            font-size: 12px;
            color: #999;
        }

        .preference-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
            gap: 15px;
        }

        .preference-item {
            padding: 12px;
            background: #f8f9fa;
            border-radius: 8px;
            border-left: 3px solid #667eea;
        }

        .preference-label {
            font-weight: 600;
            color: #555;
            font-size: 12px;
            text-transform: uppercase;
            margin-bottom: 5px;
        }

        .preference-value {
            color: #333;
            font-size: 14px;
        }

        .notes-list {
            max-height: 400px;
            overflow-y: auto;
        }

        .note-item {
            padding: 15px;
            background: #fff3cd;
            border-radius: 8px;
            margin-bottom: 10px;
            border-left: 4px solid #ffc107;
        }

        .note-item.warning {
            background: #f8d7da;
            border-left-color: #dc3545;
        }

        .note-item.vip {
            background: #d1ecf1;
            border-left-color: #17a2b8;
        }

        .note-type {
            font-weight: 600;
            color: #333;
            margin-bottom: 5px;
        }

        .note-text {
            color: #555;
            margin-bottom: 5px;
        }

        .note-date {
            font-size: 12px;
            color: #999;
        }

        .timeline {
            position: relative;
            padding-left: 30px;
        }

        .timeline::before {
            content: '';
            position: absolute;
            left: 10px;
            top: 0;
            bottom: 0;
            width: 2px;
            background: #ddd;
        }

        .timeline-item {
            position: relative;
            padding: 15px 0;
        }

        .timeline-item::before {
            content: '';
            position: absolute;
            left: -24px;
            top: 20px;
            width: 12px;
            height: 12px;
            border-radius: 50%;
            background: #667eea;
            border: 2px solid white;
            box-shadow: 0 0 0 2px #667eea;
        }

        .timeline-date {
            font-weight: 600;
            color: #667eea;
            margin-bottom: 5px;
        }

        .timeline-content {
            background: #f8f9fa;
            padding: 10px;
            border-radius: 6px;
            font-size: 14px;
        }

        .btn-action {
            background: #667eea;
            color: white;
            padding: 8px 16px;
            border: none;
            border-radius: 6px;
            font-size: 14px;
            cursor: pointer;
            margin-right: 10px;
            margin-top: 10px;
            transition: all 0.3s;
        }

        .btn-action:hover {
            background: #5568d3;
        }

        .alert {
            padding: 15px 20px;
            border-radius: 8px;
            margin-bottom: 20px;
        }

        .alert-success {
            background: #d4edda;
            border: 1px solid #c3e6cb;
            color: #155724;
        }

        .alert-error {
            background: #f8d7da;
            border: 1px solid #f5c6cb;
            color: #721c24;
        }

        .empty-state {
            text-align: center;
            padding: 40px 20px;
            color: #999;
        }

        .empty-state i {
            font-size: 48px;
            margin-bottom: 15px;
            opacity: 0.5;
        }
    </style>

    <div class="guest-profile-container">
        <!-- Page Header -->
        <div class="page-header">
            <h1><i class="fa fa-user-circle"></i> Guest Profile 360°</h1>
            <p>ข้อมูลลูกค้าครบวงจร - Complete customer view with booking history, loyalty status, and preferences</p>
        </div>

        <!-- Alert Panel -->
        <asp:Panel ID="pnlAlert" runat="server" Visible="false">
            <div class="alert" id="alertBox" runat="server">
                <asp:Label ID="lblAlertMessage" runat="server"></asp:Label>
            </div>
        </asp:Panel>

        <!-- Search Section -->
        <div class="search-section">
            <div class="search-row">
                <div class="search-field">
                    <label>เบอร์โทรศัพท์ / Mobile Phone</label>
                    <asp:TextBox ID="txtSearchPhone" runat="server" CssClass="search-input" placeholder="0812345678"></asp:TextBox>
                </div>
                <div class="search-field">
                    <label>ชื่อ / Name</label>
                    <asp:TextBox ID="txtSearchName" runat="server" CssClass="search-input" placeholder="ชื่อลูกค้า"></asp:TextBox>
                </div>
                <div class="search-field">
                    <label>Email</label>
                    <asp:TextBox ID="txtSearchEmail" runat="server" CssClass="search-input" placeholder="email@example.com"></asp:TextBox>
                </div>
                <div>
                    <asp:Button ID="btnSearch" runat="server" Text="Search" CssClass="btn-search" OnClick="btnSearch_Click" />
                </div>
            </div>
        </div>

        <!-- Profile Not Found -->
        <asp:Panel ID="pnlNotFound" runat="server" Visible="false">
            <div class="profile-not-found">
                <i class="fa fa-search"></i>
                <h2>ไม่พบข้อมูลลูกค้า</h2>
                <p>กรุณาค้นหาด้วยเบอร์โทรศัพท์, ชื่อ หรืออีเมล</p>
            </div>
        </asp:Panel>

        <!-- Profile Content -->
        <asp:Panel ID="pnlProfile" runat="server" Visible="false">
            <!-- Customer Header -->
            <div class="customer-header">
                <div class="customer-avatar">
                    <asp:Label ID="lblInitials" runat="server"></asp:Label>
                </div>
                <div class="customer-info" style="flex: 1;">
                    <h2><asp:Label ID="lblCustomerName" runat="server"></asp:Label></h2>
                    <div class="customer-badges">
                        <asp:Panel ID="pnlTierBadge" runat="server" CssClass="badge badge-tier">
                            <i class="fa fa-star"></i> <asp:Label ID="lblTierName" runat="server"></asp:Label>
                        </asp:Panel>
                        <asp:Panel ID="pnlVipBadge" runat="server" CssClass="badge badge-vip" Visible="false">
                            <i class="fa fa-crown"></i> VIP Customer
                        </asp:Panel>
                    </div>
                    <div style="margin-top: 10px; color: #666;">
                        <i class="fa fa-phone"></i> <asp:Label ID="lblPhone" runat="server"></asp:Label> |
                        <i class="fa fa-envelope"></i> <asp:Label ID="lblEmail" runat="server"></asp:Label>
                    </div>
                </div>
                <div style="text-align: right;">
                    <div style="font-size: 14px; color: #999; margin-bottom: 5px;">Customer Since</div>
                    <div style="font-size: 18px; font-weight: 600; color: #667eea;">
                        <asp:Label ID="lblMemberSince" runat="server"></asp:Label>
                    </div>
                </div>
            </div>

            <!-- Statistics Boxes -->
            <div class="stat-boxes">
                <div class="stat-box">
                    <h4>Loyalty Points</h4>
                    <div class="value"><asp:Label ID="lblPoints" runat="server">0</asp:Label></div>
                </div>
                <div class="stat-box">
                    <h4>Total Bookings</h4>
                    <div class="value"><asp:Label ID="lblTotalBookings" runat="server">0</asp:Label></div>
                </div>
                <div class="stat-box">
                    <h4>Lifetime Value</h4>
                    <div class="value">฿<asp:Label ID="lblLifetimeValue" runat="server">0</asp:Label></div>
                </div>
                <div class="stat-box">
                    <h4>Avg Rating</h4>
                    <div class="value"><asp:Label ID="lblAvgRating" runat="server">0.0</asp:Label> <i class="fa fa-star"></i></div>
                </div>
            </div>

            <!-- Main Profile Grid -->
            <div class="profile-grid">
                <!-- Left Column -->
                <div>
                    <!-- Basic Information -->
                    <div class="profile-card">
                        <h3><i class="fa fa-user"></i> Basic Information</h3>
                        <div class="info-row">
                            <div class="info-label">Full Name:</div>
                            <div class="info-value"><asp:Label ID="lblFullName" runat="server"></asp:Label></div>
                        </div>
                        <div class="info-row">
                            <div class="info-label">Nickname:</div>
                            <div class="info-value"><asp:Label ID="lblNickname" runat="server"></asp:Label></div>
                        </div>
                        <div class="info-row">
                            <div class="info-label">ID Number:</div>
                            <div class="info-value"><asp:Label ID="lblIDNumber" runat="server"></asp:Label></div>
                        </div>
                        <div class="info-row">
                            <div class="info-label">Address:</div>
                            <div class="info-value"><asp:Label ID="lblAddress" runat="server"></asp:Label></div>
                        </div>
                        <div class="info-row">
                            <div class="info-label">Segment:</div>
                            <div class="info-value"><asp:Label ID="lblSegment" runat="server"></asp:Label></div>
                        </div>
                    </div>

                    <!-- Loyalty Status -->
                    <div class="profile-card" style="margin-top: 20px;">
                        <h3><i class="fa fa-trophy"></i> Loyalty Status</h3>
                        <div class="info-row">
                            <div class="info-label">Current Tier:</div>
                            <div class="info-value">
                                <asp:Label ID="lblCurrentTier" runat="server"></asp:Label>
                            </div>
                        </div>
                        <div class="info-row">
                            <div class="info-label">Points:</div>
                            <div class="info-value">
                                <asp:Label ID="lblCurrentPoints" runat="server"></asp:Label> points
                            </div>
                        </div>
                        <div class="info-row">
                            <div class="info-label">Next Tier:</div>
                            <div class="info-value">
                                <asp:Label ID="lblNextTier" runat="server"></asp:Label>
                                (<asp:Label ID="lblPointsToNext" runat="server"></asp:Label> points needed)
                            </div>
                        </div>
                        <div class="info-row">
                            <div class="info-label">Multiplier:</div>
                            <div class="info-value">
                                <asp:Label ID="lblMultiplier" runat="server"></asp:Label>x
                            </div>
                        </div>
                        <div class="info-row">
                            <div class="info-label">Discount:</div>
                            <div class="info-value">
                                <asp:Label ID="lblDiscountPercent" runat="server"></asp:Label>%
                            </div>
                        </div>
                    </div>

                    <!-- Special Dates -->
                    <div class="profile-card" style="margin-top: 20px;">
                        <h3><i class="fa fa-calendar"></i> Special Dates</h3>
                        <asp:Panel ID="pnlSpecialDates" runat="server"></asp:Panel>
                        <asp:Label ID="lblNoSpecialDates" runat="server" CssClass="empty-state" Visible="false">
                            <i class="fa fa-calendar"></i><br />No special dates recorded
                        </asp:Label>
                    </div>
                </div>

                <!-- Right Column -->
                <div>
                    <!-- Booking History -->
                    <div class="profile-card">
                        <h3><i class="fa fa-history"></i> Booking History (Recent 10)</h3>
                        <div class="booking-list">
                            <asp:Repeater ID="rptBookings" runat="server">
                                <ItemTemplate>
                                    <div class="booking-item">
                                        <h4>
                                            Booking #<%# Eval("ReservationID") %>
                                            <span style="float: right; font-size: 14px; color: <%# GetStatusColor(Eval("Status")) %>">
                                                <%# Eval("Status") %>
                                            </span>
                                        </h4>
                                        <p><i class="fa fa-calendar"></i> <%# Eval("CheckInDate", "{0:dd MMM yyyy}") %> - <%# Eval("CheckOutDate", "{0:dd MMM yyyy}") %> (<%# Eval("StayDays") %> nights)</p>
                                        <p><i class="fa fa-home"></i> <%# Eval("AccommodationNames") %></p>
                                        <p><i class="fa fa-money"></i> ฿<%# Eval("TotalAmount", "{0:N2}") %></p>
                                    </div>
                                </ItemTemplate>
                            </asp:Repeater>
                            <asp:Label ID="lblNoBookings" runat="server" CssClass="empty-state" Visible="false">
                                <i class="fa fa-calendar-times-o"></i><br />No bookings yet
                            </asp:Label>
                        </div>
                    </div>

                    <!-- Reviews -->
                    <div class="profile-card" style="margin-top: 20px;">
                        <h3><i class="fa fa-star"></i> Reviews</h3>
                        <asp:Repeater ID="rptReviews" runat="server">
                            <ItemTemplate>
                                <div class="review-item">
                                    <div class="review-stars">
                                        <%# GetStarRating(Eval("OverallRating")) %>
                                        <span style="color: #333; font-size: 14px; margin-left: 10px;">
                                            <%# Eval("OverallRating", "{0:N1}") %>/5.0
                                        </span>
                                    </div>
                                    <div class="review-text"><%# Eval("ReviewText") %></div>
                                    <div class="review-date">
                                        Booking #<%# Eval("ReservationID") %> | <%# Eval("ReviewDate", "{0:dd MMM yyyy}") %>
                                    </div>
                                </div>
                            </ItemTemplate>
                        </asp:Repeater>
                        <asp:Label ID="lblNoReviews" runat="server" CssClass="empty-state" Visible="false">
                            <i class="fa fa-star-o"></i><br />No reviews yet
                        </asp:Label>
                    </div>

                    <!-- Guest Preferences -->
                    <div class="profile-card" style="margin-top: 20px;">
                        <h3><i class="fa fa-heart"></i> Guest Preferences</h3>
                        <div class="preference-grid">
                            <asp:Panel ID="pnlPreferences" runat="server"></asp:Panel>
                        </div>
                        <asp:Label ID="lblNoPreferences" runat="server" CssClass="empty-state" Visible="false">
                            <i class="fa fa-heart-o"></i><br />No preferences recorded
                        </asp:Label>
                    </div>

                    <!-- Important Notes -->
                    <div class="profile-card" style="margin-top: 20px;">
                        <h3><i class="fa fa-exclamation-circle"></i> Important Notes</h3>
                        <div class="notes-list">
                            <asp:Repeater ID="rptNotes" runat="server">
                                <ItemTemplate>
                                    <div class="note-item <%# Eval("NoteType").ToString().ToLower() %>">
                                        <div class="note-type">
                                            <%# GetNoteIcon(Eval("NoteType")) %> <%# Eval("NoteType") %>
                                        </div>
                                        <div class="note-text"><%# Eval("NoteText") %></div>
                                        <div class="note-date">
                                            Added: <%# Eval("CreatedDate", "{0:dd MMM yyyy}") %>
                                            by <%# Eval("CreatedByName") %>
                                        </div>
                                    </div>
                                </ItemTemplate>
                            </asp:Repeater>
                            <asp:Label ID="lblNoNotes" runat="server" CssClass="empty-state" Visible="false">
                                <i class="fa fa-sticky-note-o"></i><br />No important notes
                            </asp:Label>
                        </div>
                        <asp:Button ID="btnAddNote" runat="server" Text="Add Note" CssClass="btn-action" />
                    </div>
                </div>
            </div>

            <!-- Action Buttons -->
            <div style="text-align: center; margin-top: 30px;">
                <asp:Button ID="btnEditProfile" runat="server" Text="Edit Profile" CssClass="btn-action" style="background: #28a745;" />
                <asp:Button ID="btnViewFullHistory" runat="server" Text="View Full History" CssClass="btn-action" />
                <asp:Button ID="btnManagePoints" runat="server" Text="Manage Points" CssClass="btn-action" style="background: #f39c12;" />
            </div>
        </asp:Panel>
    </div>
</asp:Content>
