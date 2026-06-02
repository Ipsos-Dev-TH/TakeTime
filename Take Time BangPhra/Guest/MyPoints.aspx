<%@ Page Title="My Points & Rewards" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="MyPoints.aspx.cs" Inherits="Take_Time_BangPhra.Guest.MyPoints" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .points-container { max-width: 1200px; margin: 0 auto; padding: 15px; padding-bottom: 100px; }

        .page-header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white; padding: 22px 25px; border-radius: 18px; margin-bottom: 20px;
            display: flex; justify-content: space-between; align-items: center;
        }
        .page-header h2 { margin: 0; font-size: 22px; font-weight: 700; }
        .btn-back {
            background: rgba(255,255,255,0.2); color: white; border: none; padding: 9px 18px;
            border-radius: 20px; text-decoration: none; font-weight: 500;
        }
        .btn-back:hover { background: rgba(255,255,255,0.3); color: white; text-decoration: none; }

        /* Dual points hero */
        .points-hero {
            display: grid; grid-template-columns: 1fr 1fr; gap: 15px; margin-bottom: 20px;
        }
        .points-box {
            border-radius: 18px; padding: 22px; color: white; position: relative; overflow: hidden;
        }
        .points-box.redeemable { background: linear-gradient(135deg, #11998e, #38ef7d); }
        .points-box.yearly { background: linear-gradient(135deg, #fc4a1a, #f7b733); }
        .points-box .label { font-size: 13px; opacity: 0.95; display: flex; align-items: center; gap: 6px; }
        .points-box .value { font-size: 38px; font-weight: 800; line-height: 1.1; margin: 6px 0 2px; }
        .points-box .sub { font-size: 12px; opacity: 0.9; }
        .points-box i.bg { position: absolute; right: -10px; bottom: -10px; font-size: 90px; opacity: 0.15; }

        /* Tier progress card */
        .tier-progress-card {
            background: white; border-radius: 18px; padding: 22px; box-shadow: 0 4px 18px rgba(0,0,0,0.07);
            margin-bottom: 25px;
        }
        .tier-progress-top { display: flex; align-items: center; gap: 15px; margin-bottom: 14px; }
        .tier-emblem {
            width: 56px; height: 56px; border-radius: 50%; display: flex; align-items: center;
            justify-content: center; font-size: 26px; color: white; flex-shrink: 0;
        }
        .tier-progress-top h3 { margin: 0; font-size: 18px; color: #333; }
        .tier-progress-top p { margin: 0; font-size: 13px; color: #888; }
        .progress-track { background: #eef0f4; border-radius: 10px; height: 12px; overflow: hidden; }
        .progress-bar { height: 100%; background: linear-gradient(90deg, #667eea, #764ba2); border-radius: 10px; transition: width .6s ease; }
        .progress-meta { display: flex; justify-content: space-between; font-size: 12px; color: #888; margin-top: 6px; }

        .section-title {
            display: flex; align-items: center; gap: 10px; margin: 25px 0 15px;
            font-size: 17px; font-weight: 700; color: #333;
        }
        .section-title i { color: #667eea; }

        /* Rewards grid */
        .rewards-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px,1fr)); gap: 18px; }
        .reward-card {
            background: white; border-radius: 16px; overflow: hidden; box-shadow: 0 4px 18px rgba(0,0,0,0.07);
            display: flex; flex-direction: column; transition: all .3s ease;
        }
        .reward-card:hover { transform: translateY(-4px); box-shadow: 0 8px 26px rgba(0,0,0,0.13); }
        .reward-top {
            padding: 20px; background: linear-gradient(135deg,#f5f7fa,#eef1f6); display: flex;
            align-items: center; gap: 14px;
        }
        .reward-icon {
            width: 52px; height: 52px; border-radius: 14px; display: flex; align-items: center;
            justify-content: center; font-size: 24px; color: white; flex-shrink: 0;
            background: linear-gradient(135deg,#667eea,#764ba2);
        }
        .reward-cost { font-size: 20px; font-weight: 800; color: #11998e; }
        .reward-cost small { font-size: 12px; color: #888; font-weight: 500; }
        .reward-body { padding: 16px 20px; flex: 1; }
        .reward-body h4 { margin: 0 0 6px; font-size: 16px; color: #333; }
        .reward-body p { margin: 0; font-size: 13px; color: #777; line-height: 1.5; }
        .reward-foot { padding: 0 20px 18px; }
        .reward-tier-req { font-size: 11px; color: #999; margin-bottom: 10px; display: block; }
        .btn-redeem {
            width: 100%; border: none; padding: 11px; border-radius: 12px; font-size: 14px;
            font-weight: 700; cursor: pointer; transition: all .3s ease;
            background: linear-gradient(135deg,#11998e,#38ef7d); color: white;
        }
        .btn-redeem:hover { box-shadow: 0 4px 14px rgba(17,153,142,.4); }
        .btn-redeem:disabled, .btn-redeem.disabled {
            background: #e0e0e0; color: #999; cursor: not-allowed; box-shadow: none;
        }

        /* Vouchers */
        .voucher {
            background: white; border-radius: 14px; padding: 16px 18px; box-shadow: 0 3px 12px rgba(0,0,0,0.06);
            display: flex; align-items: center; gap: 15px; margin-bottom: 12px; border-left: 5px solid #f7b733;
        }
        .voucher .vcode {
            font-family: monospace; font-size: 16px; font-weight: 800; color: #333;
            background: #fff8e1; padding: 6px 12px; border-radius: 8px; letter-spacing: 1px;
        }
        .voucher .vinfo { flex: 1; }
        .voucher .vinfo h4 { margin: 0 0 3px; font-size: 15px; color: #333; }
        .voucher .vinfo span { font-size: 12px; color: #888; }
        .vstatus { font-size: 12px; font-weight: 700; padding: 5px 12px; border-radius: 12px; }
        .vstatus.PENDING { background: #fff3e0; color: #e65100; }
        .vstatus.FULFILLED { background: #e8f5e9; color: #2e7d32; }
        .vstatus.CANCELLED { background: #ffebee; color: #c62828; }
        .vstatus.EXPIRED { background: #eceff1; color: #607d8b; }

        /* Transactions */
        .txn {
            display: flex; justify-content: space-between; align-items: center; padding: 13px 16px;
            background: white; border-radius: 12px; margin-bottom: 8px; box-shadow: 0 2px 8px rgba(0,0,0,0.04);
        }
        .txn .tleft { display: flex; align-items: center; gap: 12px; }
        .txn .ticon { width: 38px; height: 38px; border-radius: 50%; display: flex; align-items: center; justify-content: center; font-size: 15px; }
        .txn .ticon.earn { background: #e8f5e9; color: #2e7d32; }
        .txn .ticon.redeem { background: #ffebee; color: #c62828; }
        .txn .ticon.adjust { background: #e3f2fd; color: #1565c0; }
        .txn .tdesc { font-size: 14px; color: #333; }
        .txn .tdate { font-size: 11px; color: #999; }
        .txn .tpts { font-size: 16px; font-weight: 800; }
        .txn .tpts.pos { color: #2e7d32; }
        .txn .tpts.neg { color: #c62828; }

        .empty-state { text-align: center; padding: 40px 20px; color: #999; }
        .empty-state i { font-size: 48px; opacity: .4; margin-bottom: 12px; display: block; }

        .alert { padding: 14px 18px; border-radius: 12px; margin-bottom: 18px; font-size: 14px; }
        .alert-success { background: #e8f5e9; color: #2e7d32; border: 1px solid #c8e6c9; }
        .alert-warning { background: #fff8e1; color: #f57f17; border: 1px solid #ffe082; }
        .alert-danger  { background: #ffebee; color: #c62828; border: 1px solid #ffcdd2; }

        /* Bottom nav */
        .bottom-nav { position: fixed; bottom: 0; left: 0; right: 0; background: white; display: flex;
            justify-content: space-around; padding: 10px 0; box-shadow: 0 -3px 15px rgba(0,0,0,0.1); z-index: 1000; border-top: 1px solid #eee; }
        .bottom-nav a { display: flex; flex-direction: column; align-items: center; text-decoration: none; padding: 5px 15px; border-radius: 10px; }
        .bottom-nav a.active { background: #f5f5f5; }
        .bottom-nav a i { font-size: 22px; margin-bottom: 4px; color: #888; }
        .bottom-nav a.active i { color: #667eea; }
        .bottom-nav a span { font-size: 10px; color: #888; font-weight: 500; }
        .bottom-nav a.active span { color: #667eea; }

        @media (min-width: 768px) { .points-container { padding-bottom: 30px; } .bottom-nav { display: none; } }
        @media (max-width: 420px) { .points-hero { grid-template-columns: 1fr; } }
    </style>

    <div class="points-container">
        <div class="page-header">
            <h2><i class="fas fa-coins"></i> คะแนนสะสม & ของรางวัล</h2>
            <a href="Dashboard.aspx" class="btn-back"><i class="fas fa-arrow-left"></i> กลับ</a>
        </div>

        <asp:Panel ID="pnlAlert" runat="server" Visible="false">
            <div id="alertBox" runat="server" class="alert">
                <asp:Label ID="lblAlert" runat="server"></asp:Label>
            </div>
        </asp:Panel>

        <!-- Dual points -->
        <div class="points-hero">
            <div class="points-box redeemable">
                <div class="label"><i class="fas fa-gift"></i> คะแนนที่แลกได้</div>
                <div class="value"><asp:Label ID="lblRedeemable" runat="server">0</asp:Label></div>
                <div class="sub">ใช้แลกของรางวัล (แลกแล้วระดับสมาชิกไม่ลด)</div>
                <i class="fas fa-gift bg"></i>
            </div>
            <div class="points-box yearly">
                <div class="label"><i class="fas fa-chart-line"></i> คะแนนสะสมปี <asp:Label ID="lblYear" runat="server"></asp:Label></div>
                <div class="value"><asp:Label ID="lblYearly" runat="server">0</asp:Label></div>
                <div class="sub">ใช้ปรับระดับสมาชิก (รีเซ็ตทุกปี)</div>
                <i class="fas fa-chart-line bg"></i>
            </div>
        </div>

        <!-- Tier progress -->
        <div class="tier-progress-card">
            <div class="tier-progress-top">
                <div class="tier-emblem" id="tierEmblem" runat="server" style="background: linear-gradient(135deg,#667eea,#764ba2);">
                    <i class="fas fa-crown"></i>
                </div>
                <div>
                    <h3>ระดับ <asp:Label ID="lblTierName" runat="server">Member</asp:Label></h3>
                    <p><asp:Label ID="lblTierHint" runat="server"></asp:Label></p>
                </div>
            </div>
            <div class="progress-track">
                <div class="progress-bar" id="tierBar" runat="server" style="width:0%;"></div>
            </div>
            <div class="progress-meta">
                <span><asp:Label ID="lblProgressNow" runat="server">0</asp:Label> แต้ม</span>
                <span><asp:Label ID="lblProgressNext" runat="server"></asp:Label></span>
            </div>
        </div>

        <!-- Rewards -->
        <div class="section-title"><i class="fas fa-store"></i> ของรางวัลที่แลกได้</div>
        <div class="rewards-grid">
            <asp:Repeater ID="rptRewards" runat="server" OnItemCommand="rptRewards_ItemCommand">
                <ItemTemplate>
                    <div class="reward-card">
                        <div class="reward-top">
                            <div class="reward-icon"><i class='<%# GetRewardIcon(Eval("Category")) %>'></i></div>
                            <div>
                                <div class="reward-cost"><%# Convert.ToInt32(Eval("PointsCost")).ToString("N0") %> <small>แต้ม</small></div>
                                <%# Eval("MonetaryValue") != DBNull.Value && Convert.ToDecimal(Eval("MonetaryValue")) > 0
                                    ? "<small style='color:#999;'>มูลค่า ฿" + Convert.ToDecimal(Eval("MonetaryValue")).ToString("N0") + "</small>" : "" %>
                            </div>
                        </div>
                        <div class="reward-body">
                            <h4><%# Eval("RewardName") %></h4>
                            <p><%# Eval("Description") %></p>
                        </div>
                        <div class="reward-foot">
                            <span class="reward-tier-req"><%# GetTierRequirement(Eval("MinTierRequired")) %></span>
                            <asp:LinkButton runat="server" CommandName="Redeem" CommandArgument='<%# Eval("ID") %>'
                                CssClass='<%# CanRedeem(Eval("PointsCost"), Eval("MinTierRequired"), Eval("StockQuantity")) ? "btn-redeem" : "btn-redeem disabled" %>'
                                Enabled='<%# CanRedeem(Eval("PointsCost"), Eval("MinTierRequired"), Eval("StockQuantity")) %>'
                                OnClientClick="return confirm('ยืนยันการแลกรางวัลนี้?');">
                                <i class="fas fa-exchange-alt"></i> <%# GetRedeemLabel(Eval("PointsCost"), Eval("MinTierRequired"), Eval("StockQuantity")) %>
                            </asp:LinkButton>
                        </div>
                    </div>
                </ItemTemplate>
            </asp:Repeater>
        </div>
        <asp:Label ID="lblNoRewards" runat="server" Visible="false">
            <div class="empty-state"><i class="fas fa-store"></i><p>ยังไม่มีของรางวัลให้แลกในขณะนี้</p></div>
        </asp:Label>

        <!-- My vouchers -->
        <div class="section-title"><i class="fas fa-ticket-alt"></i> ของรางวัลของฉัน</div>
        <asp:Repeater ID="rptVouchers" runat="server">
            <ItemTemplate>
                <div class="voucher">
                    <div class="vcode"><%# Eval("RedemptionCode") %></div>
                    <div class="vinfo">
                        <h4><%# Eval("RewardName") %></h4>
                        <span><i class="fas fa-coins"></i> <%# Convert.ToInt32(Eval("PointsCost")).ToString("N0") %> แต้ม ·
                            แลกเมื่อ <%# Eval("RedeemedDate", "{0:dd MMM yyyy}") %>
                            <%# Eval("ExpiryDate") != DBNull.Value ? " · ใช้ได้ถึง " + Convert.ToDateTime(Eval("ExpiryDate")).ToString("dd MMM yyyy") : "" %>
                        </span>
                    </div>
                    <span class='vstatus <%# Eval("Status") %>'><%# GetStatusLabel(Eval("Status")) %></span>
                </div>
            </ItemTemplate>
        </asp:Repeater>
        <asp:Label ID="lblNoVouchers" runat="server" Visible="false">
            <div class="empty-state"><i class="fas fa-ticket-alt"></i><p>คุณยังไม่ได้แลกของรางวัล</p></div>
        </asp:Label>

        <!-- Transaction history -->
        <div class="section-title"><i class="fas fa-history"></i> ประวัติคะแนน</div>
        <asp:Repeater ID="rptTransactions" runat="server">
            <ItemTemplate>
                <div class="txn">
                    <div class="tleft">
                        <div class='ticon <%# Eval("TransactionType").ToString().ToLower() %>'>
                            <i class='<%# GetTxnIcon(Eval("TransactionType")) %>'></i>
                        </div>
                        <div>
                            <div class="tdesc"><%# Eval("Description") %></div>
                            <div class="tdate"><%# Eval("TransactionDate", "{0:dd MMM yyyy HH:mm}") %></div>
                        </div>
                    </div>
                    <div class='tpts <%# Convert.ToInt32(Eval("Points")) >= 0 ? "pos" : "neg" %>'>
                        <%# Convert.ToInt32(Eval("Points")) >= 0 ? "+" : "" %><%# Convert.ToInt32(Eval("Points")).ToString("N0") %>
                    </div>
                </div>
            </ItemTemplate>
        </asp:Repeater>
        <asp:Label ID="lblNoTxn" runat="server" Visible="false">
            <div class="empty-state"><i class="fas fa-history"></i><p>ยังไม่มีประวัติคะแนน</p></div>
        </asp:Label>
    </div>

    <div class="bottom-nav">
        <a href="Dashboard.aspx"><i class="fas fa-home"></i><span>หน้าหลัก</span></a>
        <a href="RoomService.aspx"><i class="fas fa-utensils"></i><span>สั่งอาหาร</span></a>
        <a href="Activities.aspx"><i class="fas fa-hiking"></i><span>กิจกรรม</span></a>
        <a href="MyPoints.aspx" class="active"><i class="fas fa-coins"></i><span>คะแนน</span></a>
        <a href="Review.aspx"><i class="fas fa-star"></i><span>รีวิว</span></a>
    </div>
</asp:Content>
