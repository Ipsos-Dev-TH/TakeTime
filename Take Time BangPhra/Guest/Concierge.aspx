<%@ Page Title="Concierge Services" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Concierge.aspx.cs" Inherits="Take_Time_BangPhra.Guest.Concierge" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .cc-container {
            max-width: 800px;
            margin: 0 auto;
            padding: 15px;
        }

        .cc-header {
            background: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%);
            color: white;
            padding: 20px;
            border-radius: 12px;
            margin-bottom: 20px;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .cc-header h2 {
            margin: 0;
            font-size: 20px;
            font-weight: 700;
        }

        .btn-back {
            background: rgba(255,255,255,0.2);
            color: white;
            border: none;
            padding: 8px 16px;
            border-radius: 20px;
            text-decoration: none;
            font-weight: 500;
            font-size: 13px;
            transition: all 0.3s ease;
        }

        .btn-back:hover {
            background: rgba(255,255,255,0.3);
            color: white;
            text-decoration: none;
        }

        .tabs {
            display: flex;
            gap: 0;
            margin-bottom: 20px;
            border-bottom: 2px solid #e0e0e0;
            overflow-x: auto;
        }

        .tab-btn {
            background: none;
            border: none;
            padding: 12px 20px;
            font-size: 14px;
            font-weight: 600;
            color: #999;
            cursor: pointer;
            border-bottom: 3px solid transparent;
            transition: all 0.3s ease;
            white-space: nowrap;
        }

        .tab-btn.active {
            color: #4facfe;
            border-bottom-color: #4facfe;
        }

        .tab-content {
            display: none;
        }

        .tab-content.active {
            display: block;
        }

        .request-form {
            background: white;
            padding: 25px;
            border-radius: 12px;
            box-shadow: 0 5px 20px rgba(0,0,0,0.1);
        }

        .request-form h3 {
            margin: 0 0 20px 0;
            color: #333;
            font-size: 18px;
        }

        .form-group {
            margin-bottom: 20px;
        }

        .form-label {
            display: block;
            margin-bottom: 8px;
            font-weight: 600;
            color: #333;
            font-size: 14px;
        }

        .form-control {
            width: 100%;
            padding: 12px;
            border: 2px solid #e0e0e0;
            border-radius: 8px;
            font-size: 14px;
            box-sizing: border-box;
            transition: all 0.3s ease;
        }

        .form-control:focus {
            outline: none;
            border-color: #4facfe;
            box-shadow: 0 0 0 3px rgba(79, 172, 254, 0.1);
        }

        .service-types {
            display: grid;
            grid-template-columns: repeat(3, 1fr);
            gap: 12px;
        }

        .service-type-card {
            border: 2px solid #e0e0e0;
            border-radius: 10px;
            padding: 15px 10px;
            text-align: center;
            cursor: pointer;
            transition: all 0.3s ease;
            min-height: 44px;
        }

        .service-type-card:hover {
            border-color: #4facfe;
            background: #f0f8ff;
        }

        .service-type-card.selected {
            border-color: #4facfe;
            background: linear-gradient(135deg, #f0f8ff, #e0f0ff);
        }

        .service-type-card i {
            font-size: 28px;
            color: #4facfe;
            margin-bottom: 8px;
            display: block;
        }

        .service-type-card label {
            display: block;
            font-weight: 600;
            cursor: pointer;
            font-size: 12px;
        }

        .date-guest-row {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 15px;
        }

        .btn-submit-request {
            width: 100%;
            background: linear-gradient(135deg, #4facfe, #00f2fe);
            color: white;
            border: none;
            padding: 15px;
            border-radius: 10px;
            font-size: 16px;
            font-weight: 700;
            cursor: pointer;
            transition: all 0.3s ease;
            min-height: 50px;
        }

        .btn-submit-request:hover {
            transform: translateY(-2px);
            box-shadow: 0 6px 20px rgba(79, 172, 254, 0.3);
        }

        .requests-list {
            background: white;
            padding: 20px;
            border-radius: 12px;
            box-shadow: 0 3px 10px rgba(0,0,0,0.1);
        }

        .requests-list h3 {
            margin: 0 0 15px 0;
            font-size: 18px;
        }

        .request-card {
            border: 2px solid #e0e0e0;
            border-radius: 10px;
            padding: 15px;
            margin-bottom: 12px;
            transition: all 0.3s ease;
        }

        .request-card:hover {
            box-shadow: 0 3px 12px rgba(0,0,0,0.1);
        }

        .request-header {
            display: flex;
            justify-content: space-between;
            align-items: flex-start;
            margin-bottom: 10px;
            padding-bottom: 10px;
            border-bottom: 2px solid #f0f0f0;
            flex-wrap: wrap;
            gap: 8px;
        }

        .request-number {
            font-weight: 700;
            color: #4facfe;
            font-size: 14px;
        }

        .request-type-badge {
            background: #e3f2fd;
            color: #1565c0;
            padding: 4px 10px;
            border-radius: 12px;
            font-size: 11px;
            font-weight: 600;
            display: inline-block;
            margin-top: 4px;
        }

        .request-status {
            padding: 5px 12px;
            border-radius: 20px;
            font-size: 11px;
            font-weight: 600;
        }

        .status-PENDING { background: #fff3cd; color: #856404; }
        .status-CONFIRMED { background: #d1ecf1; color: #0c5460; }
        .status-COMPLETED { background: #d4edda; color: #155724; }
        .status-CANCELLED { background: #f8d7da; color: #721c24; }

        .request-description {
            background: #f8f9fa;
            padding: 12px;
            border-radius: 8px;
            margin: 10px 0;
            color: #555;
            line-height: 1.5;
            font-size: 14px;
        }

        .request-meta {
            display: flex;
            flex-wrap: wrap;
            gap: 12px;
            font-size: 13px;
            color: #666;
        }

        .request-meta div {
            display: flex;
            align-items: center;
            gap: 6px;
        }

        .request-meta i {
            color: #4facfe;
        }

        @media (max-width: 480px) {
            .service-types {
                grid-template-columns: repeat(2, 1fr);
            }

            .date-guest-row {
                grid-template-columns: 1fr;
            }

            .cc-header h2 {
                font-size: 18px;
            }
        }

        @media (min-width: 768px) {
            .cc-header h2 {
                font-size: 24px;
            }

            .service-types {
                grid-template-columns: repeat(5, 1fr);
            }
        }
    </style>

    <div class="cc-container">
    <div class="cc-header">
        <h2><i class="fas fa-concierge-bell"></i> Concierge Services</h2>
        <a href="Dashboard.aspx" class="btn-back"><i class="fas fa-arrow-left"></i> กลับ</a>
    </div>

    <div class="tabs">
        <button type="button" class="tab-btn active" onclick="switchTab(event, 'new')">
            <i class="fas fa-plus-circle"></i> ขอบริการใหม่
        </button>
        <button type="button" class="tab-btn" onclick="switchTab(event, 'history')">
            <i class="fas fa-history"></i> ประวัติ
        </button>
    </div>

    <div id="new" class="tab-content active">
        <div class="request-form">
            <h3><i class="fas fa-clipboard-list"></i> ขอบริการ Concierge</h3>

            <div class="form-group">
                <label class="form-label">ประเภทบริการ *</label>
                <div class="service-types">
                    <div class="service-type-card" onclick="selectServiceType(this, 'TOUR')">
                        <i class="fas fa-map-marked-alt"></i>
                        <label>ทัวร์</label>
                    </div>
                    <div class="service-type-card" onclick="selectServiceType(this, 'SPA')">
                        <i class="fas fa-spa"></i>
                        <label>สปา</label>
                    </div>
                    <div class="service-type-card" onclick="selectServiceType(this, 'RESTAURANT')">
                        <i class="fas fa-utensils"></i>
                        <label>ร้านอาหาร</label>
                    </div>
                    <div class="service-type-card" onclick="selectServiceType(this, 'TRANSPORTATION')">
                        <i class="fas fa-car"></i>
                        <label>รถรับส่ง</label>
                    </div>
                    <div class="service-type-card" onclick="selectServiceType(this, 'ACTIVITY')">
                        <i class="fas fa-hiking"></i>
                        <label>กิจกรรม</label>
                    </div>
                </div>
                <asp:HiddenField ID="hfServiceType" runat="server" />
            </div>

            <div class="form-group">
                <label class="form-label">ชื่อบริการ *</label>
                <asp:TextBox ID="txtServiceName" runat="server" CssClass="form-control" placeholder="เช่น ทัวร์เกาะเสม็ด 1 วัน"></asp:TextBox>
            </div>

            <div class="form-group">
                <label class="form-label">รายละเอียด *</label>
                <asp:TextBox ID="txtDescription" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="3"
                    placeholder="อธิบายรายละเอียดที่ต้องการ..."></asp:TextBox>
            </div>

            <div class="date-guest-row">
                <div class="form-group">
                    <label class="form-label">วันที่ต้องการ</label>
                    <asp:TextBox ID="txtPreferredDate" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                </div>
                <div class="form-group">
                    <label class="form-label">จำนวนผู้เข้าพัก</label>
                    <asp:TextBox ID="txtNumberOfGuests" runat="server" CssClass="form-control" TextMode="Number" Value="1"></asp:TextBox>
                </div>
            </div>

            <asp:Button ID="btnSubmitRequest" runat="server" Text="ส่งคำขอ" CssClass="btn-submit-request" OnClick="btnSubmitRequest_Click" />
        </div>
    </div>

    <div id="history" class="tab-content">
        <div class="requests-list">
            <h3><i class="fas fa-history"></i> ประวัติคำขอ</h3>
            <asp:Repeater ID="rptRequests" runat="server">
                <ItemTemplate>
                    <div class="request-card">
                        <div class="request-header">
                            <div>
                                <div class="request-number">คำขอ #<%# Eval("Request_Number") %></div>
                                <span class="request-type-badge"><%# Eval("Service_Type") %></span>
                            </div>
                            <span class="request-status status-<%# Eval("Request_Status") %>"><%# Eval("Request_Status") %></span>
                        </div>
                        <div><strong><%# Eval("Service_Name") %></strong></div>
                        <div class="request-description"><%# Eval("Request_Description") %></div>
                        <div class="request-meta">
                            <div><i class="fas fa-calendar"></i> <%# Eval("Request_Date", "{0:dd MMM yyyy}") %></div>
                            <div style='<%# Eval("Estimated_Cost") != DBNull.Value ? "" : "display:none;" %>'>
                                <i class="fas fa-tag"></i> ฿<%# Eval("Estimated_Cost", "{0:N0}") %>
                            </div>
                        </div>
                    </div>
                </ItemTemplate>
            </asp:Repeater>
            <asp:Label ID="lblNoRequests" runat="server" Visible="false" Text="<div style='text-align:center;padding:40px;color:#999;'><i class='fas fa-inbox' style='font-size:48px;display:block;margin-bottom:10px;'></i><p>ยังไม่มีคำขอ</p></div>"></asp:Label>
        </div>
    </div>
    </div>

    <script>
        function selectServiceType(card, type) {
            var cards = document.querySelectorAll('.service-type-card');
            for (var i = 0; i < cards.length; i++) { cards[i].classList.remove('selected'); }
            card.classList.add('selected');
            document.getElementById('<%= hfServiceType.ClientID %>').value = type;
        }

        function switchTab(evt, tabName) {
            var tabs = document.querySelectorAll('.tab-content');
            for (var i = 0; i < tabs.length; i++) { tabs[i].classList.remove('active'); }
            var btns = document.querySelectorAll('.tab-btn');
            for (var i = 0; i < btns.length; i++) { btns[i].classList.remove('active'); }
            document.getElementById(tabName).classList.add('active');
            evt.currentTarget.classList.add('active');
        }
    </script>
</asp:Content>
