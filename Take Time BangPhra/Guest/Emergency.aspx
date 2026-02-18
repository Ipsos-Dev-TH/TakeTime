<%@ Page Title="Emergency & Useful Contacts" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Emergency.aspx.cs" Inherits="Take_Time_BangPhra.Guest.Emergency" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .emergency-container {
            max-width: 1200px;
            margin: 0 auto;
            padding: 20px;
        }

        .page-header {
            background: linear-gradient(135deg, #e53935 0%, #c62828 100%);
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

        .emergency-banner {
            background: linear-gradient(135deg, #ff5252, #f44336);
            color: white;
            padding: 20px;
            border-radius: 12px;
            text-align: center;
            margin-bottom: 25px;
            animation: pulse 2s infinite;
        }

        @keyframes pulse {
            0%, 100% { box-shadow: 0 0 0 0 rgba(244, 67, 54, 0.4); }
            50% { box-shadow: 0 0 0 15px rgba(244, 67, 54, 0); }
        }

        .emergency-banner h3 {
            margin: 0 0 10px 0;
            font-size: 24px;
        }

        .emergency-banner .hotline {
            font-size: 36px;
            font-weight: 700;
            letter-spacing: 2px;
        }

        .section-title {
            font-size: 20px;
            font-weight: 600;
            color: #333;
            margin: 30px 0 20px 0;
            padding-bottom: 10px;
            border-bottom: 2px solid #e0e0e0;
        }

        .contacts-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
            gap: 20px;
            margin-bottom: 30px;
        }

        .contact-card {
            background: white;
            border-radius: 12px;
            padding: 20px;
            box-shadow: 0 3px 15px rgba(0,0,0,0.1);
            transition: all 0.3s ease;
            display: flex;
            align-items: flex-start;
            gap: 15px;
        }

        .contact-card:hover {
            transform: translateY(-3px);
            box-shadow: 0 6px 20px rgba(0,0,0,0.15);
        }

        .contact-card.urgent {
            border-left: 4px solid #f44336;
        }

        .contact-card.hospital {
            border-left: 4px solid #4CAF50;
        }

        .contact-card.police {
            border-left: 4px solid #2196F3;
        }

        .contact-card.hotel {
            border-left: 4px solid #9C27B0;
        }

        .contact-card.transport {
            border-left: 4px solid #FF9800;
        }

        .contact-icon {
            width: 50px;
            height: 50px;
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 24px;
            flex-shrink: 0;
        }

        .contact-card.urgent .contact-icon { background: #ffebee; color: #f44336; }
        .contact-card.hospital .contact-icon { background: #e8f5e9; color: #4CAF50; }
        .contact-card.police .contact-icon { background: #e3f2fd; color: #2196F3; }
        .contact-card.hotel .contact-icon { background: #f3e5f5; color: #9C27B0; }
        .contact-card.transport .contact-icon { background: #fff3e0; color: #FF9800; }

        .contact-info {
            flex: 1;
        }

        .contact-info h4 {
            margin: 0 0 5px 0;
            font-size: 16px;
            font-weight: 600;
            color: #333;
        }

        .contact-info .phone {
            font-size: 20px;
            font-weight: 700;
            color: #1976D2;
            margin: 5px 0;
        }

        .contact-info .phone a {
            color: inherit;
            text-decoration: none;
        }

        .contact-info .phone a:hover {
            text-decoration: underline;
        }

        .contact-info .description {
            font-size: 13px;
            color: #666;
            margin: 5px 0 0 0;
        }

        .contact-info .address {
            font-size: 12px;
            color: #999;
            margin-top: 8px;
        }

        .contact-info .address i {
            margin-right: 5px;
        }

        .btn-call {
            display: inline-flex;
            align-items: center;
            gap: 8px;
            background: linear-gradient(135deg, #4CAF50, #388E3C);
            color: white;
            padding: 8px 15px;
            border-radius: 20px;
            text-decoration: none;
            font-size: 14px;
            font-weight: 500;
            margin-top: 10px;
            transition: all 0.3s ease;
        }

        .btn-call:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(76, 175, 80, 0.3);
            color: white;
            text-decoration: none;
        }

        .btn-map {
            display: inline-flex;
            align-items: center;
            gap: 8px;
            background: linear-gradient(135deg, #2196F3, #1976D2);
            color: white;
            padding: 8px 15px;
            border-radius: 20px;
            text-decoration: none;
            font-size: 14px;
            font-weight: 500;
            margin-top: 10px;
            margin-left: 5px;
            transition: all 0.3s ease;
        }

        .btn-map:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(33, 150, 243, 0.3);
            color: white;
            text-decoration: none;
        }

        .quick-dial {
            background: white;
            border-radius: 12px;
            padding: 25px;
            box-shadow: 0 3px 15px rgba(0,0,0,0.1);
            margin-bottom: 30px;
        }

        .quick-dial h3 {
            margin: 0 0 20px 0;
            font-size: 18px;
            color: #333;
        }

        .quick-buttons {
            display: flex;
            flex-wrap: wrap;
            gap: 15px;
        }

        .quick-btn {
            display: flex;
            flex-direction: column;
            align-items: center;
            padding: 20px 25px;
            border-radius: 12px;
            text-decoration: none;
            transition: all 0.3s ease;
            min-width: 120px;
        }

        .quick-btn:hover {
            transform: translateY(-3px);
            text-decoration: none;
        }

        .quick-btn i {
            font-size: 28px;
            margin-bottom: 8px;
        }

        .quick-btn span {
            font-size: 13px;
            font-weight: 600;
        }

        .quick-btn.emergency {
            background: linear-gradient(135deg, #f44336, #d32f2f);
            color: white;
        }

        .quick-btn.police {
            background: linear-gradient(135deg, #2196F3, #1976D2);
            color: white;
        }

        .quick-btn.ambulance {
            background: linear-gradient(135deg, #4CAF50, #388E3C);
            color: white;
        }

        .quick-btn.fire {
            background: linear-gradient(135deg, #FF9800, #F57C00);
            color: white;
        }

        .quick-btn.hotel {
            background: linear-gradient(135deg, #9C27B0, #7B1FA2);
            color: white;
        }

        .tips-section {
            background: #fff8e1;
            border-radius: 12px;
            padding: 25px;
            border-left: 4px solid #FFC107;
        }

        .tips-section h3 {
            margin: 0 0 15px 0;
            color: #F57C00;
            font-size: 18px;
        }

        .tips-section ul {
            margin: 0;
            padding-left: 20px;
        }

        .tips-section li {
            margin-bottom: 10px;
            color: #666;
            line-height: 1.6;
        }

        @media (max-width: 768px) {
            .contacts-grid {
                grid-template-columns: 1fr;
            }

            .quick-buttons {
                display: grid;
                grid-template-columns: repeat(3, 1fr);
                gap: 10px;
            }

            .quick-btn {
                min-width: 0;
                padding: 15px 10px;
            }

            .quick-btn i {
                font-size: 22px;
            }

            .quick-btn span {
                font-size: 12px;
            }

            .emergency-banner .hotline {
                font-size: 24px;
            }

            .page-header h2 {
                font-size: 18px;
            }

            .page-header {
                padding: 15px;
                flex-direction: column;
                gap: 10px;
                text-align: center;
            }

            .emergency-banner {
                padding: 15px;
            }

            .emergency-banner h3 {
                font-size: 18px;
            }

            .contact-card {
                padding: 15px;
            }

            .section-title {
                font-size: 16px;
            }
        }

        @media (max-width: 380px) {
            .quick-buttons {
                grid-template-columns: repeat(2, 1fr);
            }
        }
    </style>

    <div class="emergency-container">
        <!-- Header -->
        <div class="page-header">
            <h2><i class="fas fa-phone-alt"></i> Emergency & Useful Contacts</h2>
            <a href="Dashboard.aspx" class="btn-back">
                <i class="fas fa-arrow-left"></i> Back
            </a>
        </div>

        <!-- Emergency Banner -->
        <div class="emergency-banner">
            <h3><i class="fas fa-exclamation-triangle"></i> Hotel Emergency Hotline</h3>
            <div class="hotline">
                <asp:Literal ID="litHotline" runat="server" />
            </div>
            <p style="margin: 10px 0 0 0; opacity: 0.9;">Available 24/7 - Front Desk</p>
        </div>

        <!-- No Data Panel -->
        <asp:Panel ID="pnlNoData" runat="server" Visible="false">
            <div style="text-align: center; padding: 40px 20px; background: white; border-radius: 12px; box-shadow: 0 3px 15px rgba(0,0,0,0.1); margin-bottom: 25px;">
                <i class="fas fa-phone-slash" style="font-size: 48px; color: #ccc; margin-bottom: 15px;"></i>
                <h3 style="color: #666; margin: 0 0 10px 0;">No Emergency Contacts Available</h3>
                <p style="color: #999; margin: 0;">Please contact the front desk for assistance.</p>
            </div>
        </asp:Panel>

        <!-- Quick Dial Buttons -->
        <asp:Repeater ID="rptQuickDial" runat="server">
            <HeaderTemplate>
                <div class="quick-dial">
                    <h3><i class="fas fa-bolt"></i> Quick Dial</h3>
                    <div class="quick-buttons">
            </HeaderTemplate>
            <ItemTemplate>
                <a href='tel:<%# Eval("Phone") %>' class="quick-dial-btn">
                    <i class="fas <%# Eval("Icon") %>"></i>
                    <span><%# Eval("Name") %></span>
                    <small><%# Eval("Phone") %></small>
                </a>
            </ItemTemplate>
            <FooterTemplate>
                    </div>
                </div>
            </FooterTemplate>
        </asp:Repeater>

        <!-- Hospital & Medical -->
        <asp:Repeater ID="rptHospitals" runat="server">
            <HeaderTemplate>
                <h3 class="section-title"><i class="fas fa-hospital"></i> Hospitals & Medical Services</h3>
                <div class="contacts-grid">
            </HeaderTemplate>
            <ItemTemplate>
                <div class="contact-card hospital">
                    <div class="contact-icon">
                        <i class="fas <%# Eval("Icon") %>"></i>
                    </div>
                    <div class="contact-info">
                        <h4><%# Eval("Name") %></h4>
                        <p><%# Eval("Description") %></p>
                        <a href='tel:<%# Eval("Phone") %>' class="phone-link">
                            <i class="fas fa-phone-alt"></i> <%# Eval("Phone") %>
                        </a>
                    </div>
                </div>
            </ItemTemplate>
            <FooterTemplate>
                </div>
            </FooterTemplate>
        </asp:Repeater>

        <!-- Police Stations -->
        <asp:Repeater ID="rptPolice" runat="server">
            <HeaderTemplate>
                <h3 class="section-title"><i class="fas fa-shield-alt"></i> Police Stations</h3>
                <div class="contacts-grid">
            </HeaderTemplate>
            <ItemTemplate>
                <div class="contact-card police">
                    <div class="contact-icon">
                        <i class="fas <%# Eval("Icon") %>"></i>
                    </div>
                    <div class="contact-info">
                        <h4><%# Eval("Name") %></h4>
                        <p><%# Eval("Description") %></p>
                        <a href='tel:<%# Eval("Phone") %>' class="phone-link">
                            <i class="fas fa-phone-alt"></i> <%# Eval("Phone") %>
                        </a>
                    </div>
                </div>
            </ItemTemplate>
            <FooterTemplate>
                </div>
            </FooterTemplate>
        </asp:Repeater>

        <!-- Hotel Services -->
        <asp:Repeater ID="rptHotelServices" runat="server">
            <HeaderTemplate>
                <h3 class="section-title"><i class="fas fa-hotel"></i> Hotel Services</h3>
                <div class="contacts-grid">
            </HeaderTemplate>
            <ItemTemplate>
                <div class="contact-card hotel">
                    <div class="contact-icon">
                        <i class="fas <%# Eval("Icon") %>"></i>
                    </div>
                    <div class="contact-info">
                        <h4><%# Eval("Name") %></h4>
                        <p><%# Eval("Description") %></p>
                        <a href='tel:<%# Eval("Phone") %>' class="phone-link">
                            <i class="fas fa-phone-alt"></i> <%# Eval("Phone") %>
                        </a>
                    </div>
                </div>
            </ItemTemplate>
            <FooterTemplate>
                </div>
            </FooterTemplate>
        </asp:Repeater>

        <!-- Transportation -->
        <asp:Repeater ID="rptTransport" runat="server">
            <HeaderTemplate>
                <h3 class="section-title"><i class="fas fa-car"></i> Transportation</h3>
                <div class="contacts-grid">
            </HeaderTemplate>
            <ItemTemplate>
                <div class="contact-card transport">
                    <div class="contact-icon">
                        <i class="fas <%# Eval("Icon") %>"></i>
                    </div>
                    <div class="contact-info">
                        <h4><%# Eval("Name") %></h4>
                        <p><%# Eval("Description") %></p>
                        <a href='tel:<%# Eval("Phone") %>' class="phone-link">
                            <i class="fas fa-phone-alt"></i> <%# Eval("Phone") %>
                        </a>
                    </div>
                </div>
            </ItemTemplate>
            <FooterTemplate>
                </div>
            </FooterTemplate>
        </asp:Repeater>

        <!-- Tips Section -->
        <div class="tips-section">
            <h3><i class="fas fa-lightbulb"></i> Useful Tips for Emergencies</h3>
            <ul>
                <li><strong>Keep Calm</strong> - พยายามตั้งสติและประเมินสถานการณ์</li>
                <li><strong>Know Your Location</strong> - จำชื่อโรงแรมและที่อยู่: <strong>TakeTime BangPhra, บางพระ ศรีราชา ชลบุรี</strong></li>
                <li><strong>Save Important Numbers</strong> - บันทึกเบอร์โทรฉุกเฉินไว้ในโทรศัพท์</li>
                <li><strong>Hotel Staff Can Help</strong> - เจ้าหน้าที่โรงแรมพร้อมช่วยเหลือตลอด 24 ชั่วโมง</li>
                <li><strong>Medical Emergency</strong> - หากมีอาการเจ็บป่วยรุนแรง โทร 1669 หรือแจ้ง Front Desk ทันที</li>
                <li><strong>Lost Passport</strong> - ติดต่อสถานทูต/สถานกงสุลประเทศของท่าน และแจ้ง Tourist Police 1155</li>
            </ul>
        </div>
    </div>
</asp:Content>
