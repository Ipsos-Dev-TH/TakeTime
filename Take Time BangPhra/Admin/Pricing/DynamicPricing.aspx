<%@ Page Title="Dynamic Pricing" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="DynamicPricing.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Pricing.DynamicPricing" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .pricing-dashboard { padding: 20px 0; }
        .page-header { margin-bottom: 30px; }
        .page-header h1 { color: #5D4037; margin: 0 0 10px 0; }

        .pricing-cards { display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 20px; margin-bottom: 30px; }
        .pricing-card { background: white; border-radius: 12px; padding: 25px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
        .pricing-card h3 { margin: 0 0 20px 0; font-size: 16px; color: #333; display: flex; align-items: center; gap: 10px; }
        .pricing-card h3 i { color: #5D4037; }

        .multiplier-display { text-align: center; padding: 20px; background: #f9f9f9; border-radius: 10px; margin-bottom: 20px; }
        .multiplier-value { font-size: 48px; font-weight: 700; color: #5D4037; }
        .multiplier-label { font-size: 14px; color: #666; }

        .slider-container { margin: 20px 0; }
        .slider-container label { font-size: 14px; color: #333; margin-bottom: 10px; display: block; }
        .slider-container input[type="range"] { width: 100%; }
        .slider-values { display: flex; justify-content: space-between; font-size: 12px; color: #999; }

        .rule-list { list-style: none; padding: 0; margin: 0; }
        .rule-list li { padding: 12px 15px; background: #f9f9f9; border-radius: 8px; margin-bottom: 10px; display: flex; justify-content: space-between; align-items: center; }
        .rule-list .rule-name { font-weight: 500; }
        .rule-list .rule-value { color: #5D4037; font-weight: 600; }

        .toggle-switch { position: relative; width: 50px; height: 26px; background: #ccc; border-radius: 13px; cursor: pointer; transition: background 0.3s; }
        .toggle-switch.active { background: #4CAF50; }
        .toggle-switch::after { content: ''; position: absolute; width: 22px; height: 22px; background: white; border-radius: 50%; top: 2px; left: 2px; transition: left 0.3s; }
        .toggle-switch.active::after { left: 26px; }

        .btn-save { width: 100%; padding: 12px; background: #5D4037; color: white; border: none; border-radius: 8px; font-weight: 500; cursor: pointer; margin-top: 15px; }
        .btn-save:hover { background: #4E342E; }

        .occupancy-chart { height: 200px; background: linear-gradient(to top, #E8F5E9 0%, #E8F5E9 30%, #FFF3E0 30%, #FFF3E0 70%, #FFEBEE 70%, #FFEBEE 100%); border-radius: 10px; position: relative; margin-bottom: 20px; }
        .chart-labels { display: flex; justify-content: space-between; padding: 10px 0; font-size: 12px; color: #666; }
        .chart-line { position: absolute; width: 100%; height: 2px; background: #5D4037; left: 0; }
    </style>

    <div class="pricing-dashboard">
        <div class="page-header">
            <h1><i class="fas fa-tags"></i> Dynamic Pricing</h1>
            <p>ตั้งค่าราคาแบบไดนามิกตามความต้องการและ Occupancy</p>
        </div>

        <div class="pricing-cards">
            <!-- Current Multiplier -->
            <div class="pricing-card">
                <h3><i class="fas fa-percentage"></i> ตัวคูณราคาปัจจุบัน</h3>
                <div class="multiplier-display">
                    <div class="multiplier-value"><asp:Label ID="lblCurrentMultiplier" runat="server" Text="1.00" />x</div>
                    <div class="multiplier-label">Base Price Multiplier</div>
                </div>
                <div class="chart-labels">
                    <span>Occupancy ต่ำ (ลดราคา)</span>
                    <span>สูง (เพิ่มราคา)</span>
                </div>
                <ul class="rule-list">
                    <li>
                        <span class="rule-name">Occupancy วันนี้</span>
                        <span class="rule-value"><asp:Label ID="lblTodayOccupancy" runat="server" Text="0%" /></span>
                    </li>
                    <li>
                        <span class="rule-name">ราคาแนะนำ</span>
                        <span class="rule-value"><asp:Label ID="lblSuggestedPrice" runat="server" Text="ราคาปกติ" /></span>
                    </li>
                </ul>
            </div>

            <!-- Pricing Rules -->
            <div class="pricing-card">
                <h3><i class="fas fa-sliders-h"></i> กฎการปรับราคา</h3>
                <ul class="rule-list">
                    <li>
                        <span class="rule-name">Occupancy > 80%</span>
                        <span class="rule-value">+20%</span>
                    </li>
                    <li>
                        <span class="rule-name">Occupancy 60-80%</span>
                        <span class="rule-value">ราคาปกติ</span>
                    </li>
                    <li>
                        <span class="rule-name">Occupancy < 40%</span>
                        <span class="rule-value">-15%</span>
                    </li>
                    <li>
                        <span class="rule-name">วันหยุดนักขัตฤกษ์</span>
                        <span class="rule-value">+30%</span>
                    </li>
                    <li>
                        <span class="rule-name">Last Minute (< 24 ชม.)</span>
                        <span class="rule-value">-10%</span>
                    </li>
                </ul>
            </div>

            <!-- Enable/Disable -->
            <div class="pricing-card">
                <h3><i class="fas fa-power-off"></i> เปิด/ปิดการใช้งาน</h3>
                <ul class="rule-list">
                    <li>
                        <span class="rule-name">Dynamic Pricing</span>
                        <div class="toggle-switch" onclick="this.classList.toggle('active')"></div>
                    </li>
                    <li>
                        <span class="rule-name">Holiday Surcharge</span>
                        <div class="toggle-switch active" onclick="this.classList.toggle('active')"></div>
                    </li>
                    <li>
                        <span class="rule-name">Last Minute Discount</span>
                        <div class="toggle-switch" onclick="this.classList.toggle('active')"></div>
                    </li>
                    <li>
                        <span class="rule-name">Weekend Premium</span>
                        <div class="toggle-switch active" onclick="this.classList.toggle('active')"></div>
                    </li>
                </ul>
                <button class="btn-save"><i class="fas fa-save"></i> บันทึกการตั้งค่า</button>
            </div>
        </div>
    </div>
</asp:Content>
