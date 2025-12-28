<%@ Page Title="Notification Settings" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Settings.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Notifications.Settings" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .notifications-page { padding: 20px 0; }
        .page-header { margin-bottom: 30px; }
        .page-header h1 { color: #5D4037; margin: 0 0 10px 0; }

        .settings-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(350px, 1fr)); gap: 20px; }
        .settings-card { background: white; border-radius: 12px; padding: 25px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
        .settings-card h3 { margin: 0 0 20px 0; font-size: 16px; color: #333; display: flex; align-items: center; gap: 10px; border-bottom: 2px solid #f0f0f0; padding-bottom: 15px; }
        .settings-card h3 i { color: #5D4037; font-size: 20px; }

        .setting-item { display: flex; justify-content: space-between; align-items: center; padding: 15px 0; border-bottom: 1px solid #f5f5f5; }
        .setting-item:last-child { border-bottom: none; }
        .setting-info .name { font-weight: 500; margin-bottom: 4px; }
        .setting-info .desc { font-size: 12px; color: #999; }

        .toggle-switch { position: relative; width: 50px; height: 26px; background: #ccc; border-radius: 13px; cursor: pointer; transition: background 0.3s; flex-shrink: 0; }
        .toggle-switch.active { background: #4CAF50; }
        .toggle-switch::after { content: ''; position: absolute; width: 22px; height: 22px; background: white; border-radius: 50%; top: 2px; left: 2px; transition: left 0.3s; box-shadow: 0 2px 4px rgba(0,0,0,0.2); }
        .toggle-switch.active::after { left: 26px; }

        .channel-icons { display: flex; gap: 10px; margin-top: 15px; padding-top: 15px; border-top: 1px solid #f0f0f0; }
        .channel-icon { width: 40px; height: 40px; border-radius: 8px; display: flex; align-items: center; justify-content: center; font-size: 18px; cursor: pointer; transition: all 0.3s; }
        .channel-icon.active { transform: scale(1.1); box-shadow: 0 4px 12px rgba(0,0,0,0.2); }
        .channel-icon.inactive { opacity: 0.4; }
        .icon-line { background: #06C755; color: white; }
        .icon-telegram { background: #0088cc; color: white; }
        .icon-email { background: #EA4335; color: white; }
        .icon-sms { background: #5D4037; color: white; }

        .test-section { margin-top: 20px; padding-top: 20px; border-top: 2px solid #f0f0f0; }
        .btn-test { padding: 10px 20px; background: #5D4037; color: white; border: none; border-radius: 8px; cursor: pointer; font-weight: 500; }
        .btn-test:hover { background: #4E342E; }

        .status-badge { padding: 4px 10px; border-radius: 10px; font-size: 11px; font-weight: 500; }
        .status-connected { background: #E8F5E9; color: #2E7D32; }
        .status-not-configured { background: #FFF3E0; color: #E65100; }
    </style>

    <div class="notifications-page">
        <div class="page-header">
            <h1><i class="fas fa-bell"></i> Notification Settings</h1>
            <p>ตั้งค่าการแจ้งเตือนสำหรับเหตุการณ์ต่างๆ</p>
        </div>

        <div class="settings-grid">
            <!-- Booking Notifications -->
            <div class="settings-card">
                <h3><i class="fas fa-calendar-check"></i> การจองใหม่</h3>
                <div class="setting-item">
                    <div class="setting-info">
                        <div class="name">แจ้งเตือนเมื่อมีการจองใหม่</div>
                        <div class="desc">รับการแจ้งเตือนทันทีเมื่อลูกค้าจอง</div>
                    </div>
                    <div class="toggle-switch active" onclick="this.classList.toggle('active')"></div>
                </div>
                <div class="setting-item">
                    <div class="setting-info">
                        <div class="name">แจ้งเตือนการยกเลิก</div>
                        <div class="desc">รับการแจ้งเตือนเมื่อมีการยกเลิกการจอง</div>
                    </div>
                    <div class="toggle-switch active" onclick="this.classList.toggle('active')"></div>
                </div>
                <div class="setting-item">
                    <div class="setting-info">
                        <div class="name">สรุปการจองรายวัน</div>
                        <div class="desc">รับสรุปการจองทุกวันเวลา 08:00</div>
                    </div>
                    <div class="toggle-switch" onclick="this.classList.toggle('active')"></div>
                </div>
                <div class="channel-icons">
                    <div class="channel-icon icon-line active" title="LINE"><i class="fab fa-line"></i></div>
                    <div class="channel-icon icon-telegram active" title="Telegram"><i class="fab fa-telegram"></i></div>
                    <div class="channel-icon icon-email inactive" title="Email"><i class="fas fa-envelope"></i></div>
                </div>
            </div>

            <!-- Guest Messages -->
            <div class="settings-card">
                <h3><i class="fas fa-comments"></i> ข้อความจากแขก</h3>
                <div class="setting-item">
                    <div class="setting-info">
                        <div class="name">แจ้งเตือนข้อความใหม่</div>
                        <div class="desc">รับการแจ้งเตือนเมื่อแขกส่งข้อความ</div>
                    </div>
                    <div class="toggle-switch active" onclick="this.classList.toggle('active')"></div>
                </div>
                <div class="setting-item">
                    <div class="setting-info">
                        <div class="name">แจ้งเตือนซ้ำทุก 5 นาที</div>
                        <div class="desc">แจ้งเตือนซ้ำจนกว่าจะตอบกลับ</div>
                    </div>
                    <div class="toggle-switch active" onclick="this.classList.toggle('active')"></div>
                </div>
                <div class="channel-icons">
                    <div class="channel-icon icon-line active" title="LINE"><i class="fab fa-line"></i></div>
                    <div class="channel-icon icon-telegram active" title="Telegram"><i class="fab fa-telegram"></i></div>
                </div>
            </div>

            <!-- Check-in Reminders -->
            <div class="settings-card">
                <h3><i class="fas fa-door-open"></i> เตือนการเช็คอิน</h3>
                <div class="setting-item">
                    <div class="setting-info">
                        <div class="name">เตือนแขก 1 วันก่อนเช็คอิน</div>
                        <div class="desc">ส่งข้อความเตือนแขกอัตโนมัติ</div>
                    </div>
                    <div class="toggle-switch active" onclick="this.classList.toggle('active')"></div>
                </div>
                <div class="setting-item">
                    <div class="setting-info">
                        <div class="name">ส่งข้อมูลการเดินทาง</div>
                        <div class="desc">ส่งแผนที่และข้อมูลที่พัก</div>
                    </div>
                    <div class="toggle-switch active" onclick="this.classList.toggle('active')"></div>
                </div>
                <div class="channel-icons">
                    <div class="channel-icon icon-line active" title="LINE"><i class="fab fa-line"></i></div>
                    <div class="channel-icon icon-sms inactive" title="SMS"><i class="fas fa-sms"></i></div>
                </div>
            </div>

            <!-- Channel Configuration -->
            <div class="settings-card">
                <h3><i class="fas fa-cog"></i> ตั้งค่าช่องทาง</h3>
                <div class="setting-item">
                    <div class="setting-info">
                        <div class="name">LINE Notify</div>
                        <div class="desc">Token: ••••••••</div>
                    </div>
                    <span class="status-badge status-connected">เชื่อมต่อแล้ว</span>
                </div>
                <div class="setting-item">
                    <div class="setting-info">
                        <div class="name">Telegram Bot</div>
                        <div class="desc">@TakeTimeBot</div>
                    </div>
                    <span class="status-badge status-connected">เชื่อมต่อแล้ว</span>
                </div>
                <div class="setting-item">
                    <div class="setting-info">
                        <div class="name">Email (SMTP)</div>
                        <div class="desc">ยังไม่ได้ตั้งค่า</div>
                    </div>
                    <span class="status-badge status-not-configured">ยังไม่ตั้งค่า</span>
                </div>
                <div class="test-section">
                    <button class="btn-test"><i class="fas fa-paper-plane"></i> ทดสอบส่งการแจ้งเตือน</button>
                </div>
            </div>
        </div>
    </div>
</asp:Content>
