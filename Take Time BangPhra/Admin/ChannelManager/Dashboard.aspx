<%@ Page Title="Channel Manager" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Dashboard.aspx.cs" Inherits="Take_Time_BangPhra.Admin.ChannelManager.Dashboard" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .channel-dashboard { padding: 20px 0; }
        .page-header { margin-bottom: 30px; }
        .page-header h1 { color: #5D4037; margin: 0 0 10px 0; }

        .channel-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 20px; margin-bottom: 30px; }
        .channel-card { background: white; border-radius: 12px; padding: 25px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
        .channel-card .header { display: flex; align-items: center; gap: 15px; margin-bottom: 20px; }
        .channel-card .logo { width: 50px; height: 50px; border-radius: 10px; display: flex; align-items: center; justify-content: center; font-size: 24px; }
        .channel-card .name { font-weight: 600; font-size: 18px; }
        .channel-card .status { font-size: 12px; padding: 4px 10px; border-radius: 10px; }
        .status-connected { background: #E8F5E9; color: #2E7D32; }
        .status-disconnected { background: #FFEBEE; color: #C62828; }

        .channel-stats { display: grid; grid-template-columns: repeat(2, 1fr); gap: 10px; }
        .stat-item { text-align: center; padding: 10px; background: #f5f5f5; border-radius: 8px; }
        .stat-item .value { font-size: 20px; font-weight: 700; color: #333; }
        .stat-item .label { font-size: 11px; color: #666; }

        .logo-booking { background: linear-gradient(135deg, #003580, #0057b8); color: white; }
        .logo-agoda { background: linear-gradient(135deg, #5542F6, #7B68EE); color: white; }
        .logo-airbnb { background: linear-gradient(135deg, #FF5A5F, #FF7E82); color: white; }
        .logo-direct { background: linear-gradient(135deg, #5D4037, #8D6E63); color: white; }

        .sync-btn { width: 100%; margin-top: 15px; padding: 10px; border: none; border-radius: 8px; background: #5D4037; color: white; cursor: pointer; font-weight: 500; }
        .sync-btn:hover { background: #4E342E; }

        .last-sync { font-size: 11px; color: #999; text-align: center; margin-top: 10px; }

        .summary-section { background: white; border-radius: 12px; padding: 25px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
        .summary-title { font-weight: 600; font-size: 18px; margin-bottom: 20px; }
        .summary-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 15px; }
        .summary-item { text-align: center; padding: 20px; background: #f9f9f9; border-radius: 10px; }
        .summary-item .value { font-size: 28px; font-weight: 700; color: #5D4037; }
        .summary-item .label { font-size: 13px; color: #666; margin-top: 5px; }
    </style>

    <div class="channel-dashboard">
        <div class="page-header">
            <h1><i class="fas fa-network-wired"></i> Channel Manager</h1>
            <p>จัดการการเชื่อมต่อกับ OTA และแพลตฟอร์มการจอง</p>
        </div>

        <div class="channel-grid">
            <!-- Booking.com -->
            <div class="channel-card">
                <div class="header">
                    <div class="logo logo-booking"><i class="fas fa-b"></i></div>
                    <div>
                        <div class="name">Booking.com</div>
                        <span class="status status-disconnected">ยังไม่เชื่อมต่อ</span>
                    </div>
                </div>
                <div class="channel-stats">
                    <div class="stat-item">
                        <div class="value">-</div>
                        <div class="label">การจองเดือนนี้</div>
                    </div>
                    <div class="stat-item">
                        <div class="value">-</div>
                        <div class="label">รายได้</div>
                    </div>
                </div>
                <button class="sync-btn" disabled><i class="fas fa-link"></i> เชื่อมต่อ</button>
                <div class="last-sync">ยังไม่เคยเชื่อมต่อ</div>
            </div>

            <!-- Agoda -->
            <div class="channel-card">
                <div class="header">
                    <div class="logo logo-agoda"><i class="fas fa-a"></i></div>
                    <div>
                        <div class="name">Agoda</div>
                        <span class="status status-disconnected">ยังไม่เชื่อมต่อ</span>
                    </div>
                </div>
                <div class="channel-stats">
                    <div class="stat-item">
                        <div class="value">-</div>
                        <div class="label">การจองเดือนนี้</div>
                    </div>
                    <div class="stat-item">
                        <div class="value">-</div>
                        <div class="label">รายได้</div>
                    </div>
                </div>
                <button class="sync-btn" disabled><i class="fas fa-link"></i> เชื่อมต่อ</button>
                <div class="last-sync">ยังไม่เคยเชื่อมต่อ</div>
            </div>

            <!-- Airbnb -->
            <div class="channel-card">
                <div class="header">
                    <div class="logo logo-airbnb"><i class="fas fa-home"></i></div>
                    <div>
                        <div class="name">Airbnb</div>
                        <span class="status status-disconnected">ยังไม่เชื่อมต่อ</span>
                    </div>
                </div>
                <div class="channel-stats">
                    <div class="stat-item">
                        <div class="value">-</div>
                        <div class="label">การจองเดือนนี้</div>
                    </div>
                    <div class="stat-item">
                        <div class="value">-</div>
                        <div class="label">รายได้</div>
                    </div>
                </div>
                <button class="sync-btn" disabled><i class="fas fa-link"></i> เชื่อมต่อ</button>
                <div class="last-sync">ยังไม่เคยเชื่อมต่อ</div>
            </div>

            <!-- Direct Booking -->
            <div class="channel-card">
                <div class="header">
                    <div class="logo logo-direct"><i class="fas fa-globe"></i></div>
                    <div>
                        <div class="name">Direct Booking</div>
                        <span class="status status-connected">เชื่อมต่อแล้ว</span>
                    </div>
                </div>
                <div class="channel-stats">
                    <div class="stat-item">
                        <div class="value"><asp:Label ID="lblDirectBookings" runat="server" Text="0" /></div>
                        <div class="label">การจองเดือนนี้</div>
                    </div>
                    <div class="stat-item">
                        <div class="value"><asp:Label ID="lblDirectRevenue" runat="server" Text="0" /></div>
                        <div class="label">รายได้</div>
                    </div>
                </div>
                <button class="sync-btn" onclick="return false;"><i class="fas fa-sync"></i> Sync Now</button>
                <div class="last-sync">อัปเดตล่าสุด: เมื่อสักครู่</div>
            </div>
        </div>

        <!-- Summary -->
        <div class="summary-section">
            <div class="summary-title"><i class="fas fa-chart-bar"></i> สรุปภาพรวมเดือนนี้</div>
            <div class="summary-grid">
                <div class="summary-item">
                    <div class="value"><asp:Label ID="lblTotalBookings" runat="server" Text="0" /></div>
                    <div class="label">การจองทั้งหมด</div>
                </div>
                <div class="summary-item">
                    <div class="value"><asp:Label ID="lblTotalRevenue" runat="server" Text="0" /></div>
                    <div class="label">รายได้รวม (บาท)</div>
                </div>
                <div class="summary-item">
                    <div class="value"><asp:Label ID="lblAvgRate" runat="server" Text="0" /></div>
                    <div class="label">ราคาเฉลี่ย/คืน</div>
                </div>
                <div class="summary-item">
                    <div class="value"><asp:Label ID="lblOccupancy" runat="server" Text="0%" /></div>
                    <div class="label">Occupancy Rate</div>
                </div>
            </div>
        </div>
    </div>
</asp:Content>
