<%@ Page Title="Membership Management" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="MembershipManagement.aspx.cs" Inherits="Take_Time_BangPhra.Admin.CRM.MembershipManagement" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .mm-container { max-width: 1500px; margin: 20px auto; padding: 20px; }
        .mm-header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white;
            padding: 28px; border-radius: 14px; margin-bottom: 25px;
        }
        .mm-header h1 { margin: 0 0 6px; font-size: 28px; font-weight: 800; }
        .mm-header p { margin: 0; opacity: .92; }

        .mm-stats { display: grid; grid-template-columns: repeat(auto-fit,minmax(200px,1fr)); gap: 16px; margin-bottom: 25px; }
        .mm-stat { background: white; border-radius: 12px; padding: 20px; box-shadow: 0 2px 8px rgba(0,0,0,.08); border-left: 5px solid #667eea; }
        .mm-stat h3 { margin: 0 0 8px; font-size: 13px; color: #888; text-transform: uppercase; letter-spacing: .5px; }
        .mm-stat .v { font-size: 30px; font-weight: 800; color: #667eea; }
        .mm-stat.green { border-left-color: #11998e; } .mm-stat.green .v { color: #11998e; }
        .mm-stat.orange { border-left-color: #f7b733; } .mm-stat.orange .v { color: #f39c12; }
        .mm-stat.red { border-left-color: #e74c3c; } .mm-stat.red .v { color: #e74c3c; }

        .tabs { display: flex; background: #f1f3f7; border-radius: 12px 12px 0 0; overflow: hidden; }
        .tabs .tab-btn { flex: 1; padding: 16px; border: none; background: none; font-size: 15px; font-weight: 700; color: #777; cursor: pointer; border-bottom: 3px solid transparent; }
        .tabs .tab-btn.active { background: white; color: #667eea; border-bottom-color: #667eea; }
        .tab-body { background: white; border-radius: 0 0 12px 12px; padding: 25px; box-shadow: 0 2px 8px rgba(0,0,0,.08); margin-bottom: 25px; }

        .toolbar { display: flex; gap: 10px; flex-wrap: wrap; margin-bottom: 18px; align-items: center; }
        .toolbar .form-control { padding: 10px 14px; border: 1px solid #ddd; border-radius: 8px; font-size: 14px; }
        .btn { padding: 10px 20px; border: none; border-radius: 8px; font-weight: 600; cursor: pointer; font-size: 14px; }
        .btn-primary { background: linear-gradient(135deg,#667eea,#764ba2); color: white; }
        .btn-success { background: linear-gradient(135deg,#11998e,#38ef7d); color: white; }
        .btn-danger { background: #e74c3c; color: white; }
        .btn-sm { padding: 6px 14px; font-size: 13px; }

        .data-table { width: 100%; border-collapse: collapse; }
        .data-table th { background: #667eea; color: white; padding: 13px; text-align: left; font-size: 13px; }
        .data-table td { padding: 12px 13px; border-bottom: 1px solid #eef0f4; font-size: 14px; }
        .data-table tr:hover { background: #f8f9ff; }
        .tier-pill { padding: 4px 12px; border-radius: 12px; font-size: 12px; font-weight: 700; color: white; }
        .pts-yearly { color: #f39c12; font-weight: 700; }
        .pts-avail { color: #11998e; font-weight: 700; }

        .status-pill { padding: 4px 12px; border-radius: 12px; font-size: 12px; font-weight: 700; }
        .status-PENDING { background: #fff3e0; color: #e65100; }
        .status-FULFILLED { background: #e8f5e9; color: #2e7d32; }
        .status-CANCELLED { background: #ffebee; color: #c62828; }
        .status-EXPIRED { background: #eceff1; color: #607d8b; }

        .alert { padding: 14px 18px; border-radius: 10px; margin-bottom: 18px; }
        .alert-success { background: #d4edda; color: #155724; border: 1px solid #c3e6cb; }
        .alert-error { background: #f8d7da; color: #721c24; border: 1px solid #f5c6cb; }

        /* Member detail modal */
        .modal { position: fixed; inset: 0; background: rgba(0,0,0,.5); display: none; align-items: center; justify-content: center; z-index: 1000; }
        .modal.show { display: flex; }
        .modal-card { background: white; border-radius: 14px; width: 90%; max-width: 760px; max-height: 90vh; overflow-y: auto; }
        .modal-head { padding: 22px 25px; border-bottom: 1px solid #eee; display: flex; justify-content: space-between; align-items: center; }
        .modal-head h3 { margin: 0; }
        .modal-body { padding: 25px; }
        .detail-grid { display: grid; grid-template-columns: repeat(3,1fr); gap: 14px; margin-bottom: 22px; }
        .detail-box { background: #f8f9ff; border-radius: 10px; padding: 15px; text-align: center; }
        .detail-box .lbl { font-size: 12px; color: #888; }
        .detail-box .val { font-size: 22px; font-weight: 800; color: #333; margin-top: 4px; }
        .adjust-form { background: #fafbff; border: 1px dashed #c7ceea; border-radius: 10px; padding: 18px; margin-bottom: 22px; }
        .adjust-form label { font-weight: 600; font-size: 13px; color: #555; display: block; margin-bottom: 6px; }
        .adjust-row { display: flex; gap: 10px; flex-wrap: wrap; align-items: end; }
        .form-control { padding: 10px; border: 1px solid #ddd; border-radius: 8px; font-size: 14px; }
    </style>

    <div class="mm-container">
        <div class="mm-header">
            <h1><i class="fas fa-users-cog"></i> จัดการสมาชิก (Membership Management)</h1>
            <p>ระบบสมาชิก 2 กระเป๋าคะแนน: คะแนนสะสมรายปี (ปรับระดับ) + คะแนนคงเหลือ (แลกของรางวัล)</p>
        </div>

        <asp:Panel ID="pnlAlert" runat="server" Visible="false">
            <div id="alertBox" runat="server" class="alert"><asp:Label ID="lblAlert" runat="server"></asp:Label></div>
        </asp:Panel>

        <div class="mm-stats">
            <div class="mm-stat"><h3>สมาชิกทั้งหมด</h3><div class="v"><asp:Label ID="lblTotalMembers" runat="server">0</asp:Label></div></div>
            <div class="mm-stat green"><h3>คะแนนคงเหลือรวม</h3><div class="v"><asp:Label ID="lblTotalAvailable" runat="server">0</asp:Label></div></div>
            <div class="mm-stat orange"><h3>แลกรางวัลรอจ่าย</h3><div class="v"><asp:Label ID="lblPendingRedemptions" runat="server">0</asp:Label></div></div>
            <div class="mm-stat red"><h3>คะแนนแลกเดือนนี้</h3><div class="v"><asp:Label ID="lblRedeemedMonth" runat="server">0</asp:Label></div></div>
        </div>

        <div class="tabs">
            <asp:Button ID="btnTabMembers" runat="server" Text="สมาชิก" CssClass="tab-btn active" OnClick="btnTabMembers_Click" />
            <asp:Button ID="btnTabRedemptions" runat="server" Text="การแลกของรางวัล" CssClass="tab-btn" OnClick="btnTabRedemptions_Click" />
        </div>

        <!-- Members tab -->
        <asp:Panel ID="pnlMembers" runat="server" CssClass="tab-body">
            <div class="toolbar">
                <asp:TextBox ID="txtSearch" runat="server" CssClass="form-control" placeholder="ค้นหาชื่อหรือเบอร์โทร" Width="280px"></asp:TextBox>
                <asp:DropDownList ID="ddlTierFilter" runat="server" CssClass="form-control"></asp:DropDownList>
                <asp:Button ID="btnSearch" runat="server" Text="ค้นหา" CssClass="btn btn-primary" OnClick="btnSearch_Click" />
            </div>

            <asp:GridView ID="gvMembers" runat="server" CssClass="data-table" AutoGenerateColumns="False"
                OnRowCommand="gvMembers_RowCommand" DataKeyNames="Customer_MobilePhone" GridLines="None">
                <Columns>
                    <asp:BoundField DataField="CustomerName" HeaderText="ชื่อสมาชิก" />
                    <asp:BoundField DataField="Customer_MobilePhone" HeaderText="เบอร์โทร" />
                    <asp:TemplateField HeaderText="ระดับ">
                        <ItemTemplate>
                            <span class="tier-pill" style='background: <%# Eval("TierColor") %>'><%# Eval("TierName") %></span>
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="คะแนนรายปี">
                        <ItemTemplate><span class="pts-yearly"><%# Convert.ToInt32(Eval("YearlyPoints")).ToString("N0") %></span></ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="คะแนนคงเหลือ">
                        <ItemTemplate><span class="pts-avail"><%# Convert.ToInt32(Eval("AvailablePoints")).ToString("N0") %></span></ItemTemplate>
                    </asp:TemplateField>
                    <asp:BoundField DataField="LifetimePoints" HeaderText="สะสมทั้งหมด" DataFormatString="{0:N0}" />
                    <asp:BoundField DataField="MemberSince" HeaderText="สมาชิกตั้งแต่" DataFormatString="{0:dd MMM yyyy}" />
                    <asp:TemplateField HeaderText="จัดการ">
                        <ItemTemplate>
                            <asp:LinkButton runat="server" CssClass="btn btn-primary btn-sm" CommandName="ViewMember"
                                CommandArgument='<%# Eval("Customer_MobilePhone") %>'><i class="fas fa-eye"></i> ดู/ปรับคะแนน</asp:LinkButton>
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
            </asp:GridView>
            <asp:Label ID="lblNoMembers" runat="server" Visible="false"
                Text="<div style='text-align:center;padding:40px;color:#999;'>ไม่พบสมาชิก</div>"></asp:Label>
        </asp:Panel>

        <!-- Redemptions tab -->
        <asp:Panel ID="pnlRedemptions" runat="server" CssClass="tab-body" Visible="false">
            <div class="toolbar">
                <asp:DropDownList ID="ddlRedemptionStatus" runat="server" CssClass="form-control" AutoPostBack="true"
                    OnSelectedIndexChanged="ddlRedemptionStatus_SelectedIndexChanged">
                    <asp:ListItem Value="PENDING" Text="รอรับรางวัล"></asp:ListItem>
                    <asp:ListItem Value="" Text="ทั้งหมด"></asp:ListItem>
                    <asp:ListItem Value="FULFILLED" Text="รับแล้ว"></asp:ListItem>
                    <asp:ListItem Value="CANCELLED" Text="ยกเลิก"></asp:ListItem>
                </asp:DropDownList>
            </div>

            <asp:GridView ID="gvRedemptions" runat="server" CssClass="data-table" AutoGenerateColumns="False"
                OnRowCommand="gvRedemptions_RowCommand" DataKeyNames="ID" GridLines="None">
                <Columns>
                    <asp:BoundField DataField="RedemptionCode" HeaderText="รหัส" />
                    <asp:BoundField DataField="CustomerName" HeaderText="สมาชิก" />
                    <asp:BoundField DataField="Customer_MobilePhone" HeaderText="เบอร์โทร" />
                    <asp:BoundField DataField="RewardName" HeaderText="รางวัล" />
                    <asp:BoundField DataField="PointsCost" HeaderText="คะแนน" DataFormatString="{0:N0}" />
                    <asp:BoundField DataField="RedeemedDate" HeaderText="แลกเมื่อ" DataFormatString="{0:dd MMM yyyy HH:mm}" />
                    <asp:TemplateField HeaderText="สถานะ">
                        <ItemTemplate><span class='status-pill status-<%# Eval("Status") %>'><%# GetStatusLabel(Eval("Status")) %></span></ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="จัดการ">
                        <ItemTemplate>
                            <asp:LinkButton runat="server" CssClass="btn btn-success btn-sm" CommandName="Fulfill"
                                CommandArgument='<%# Eval("ID") %>' Visible='<%# Eval("Status").ToString() == "PENDING" %>'>
                                <i class="fas fa-check"></i> จ่ายแล้ว</asp:LinkButton>
                            <asp:LinkButton runat="server" CssClass="btn btn-danger btn-sm" CommandName="Cancel"
                                CommandArgument='<%# Eval("ID") %>' Visible='<%# Eval("Status").ToString() == "PENDING" %>'
                                OnClientClick="return confirm('ยกเลิกและคืนคะแนนให้สมาชิก?');">
                                <i class="fas fa-times"></i> ยกเลิก</asp:LinkButton>
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
            </asp:GridView>
            <asp:Label ID="lblNoRedemptions" runat="server" Visible="false"
                Text="<div style='text-align:center;padding:40px;color:#999;'>ไม่มีรายการแลกรางวัล</div>"></asp:Label>
        </asp:Panel>

        <!-- Member detail modal -->
        <asp:Panel ID="pnlMemberModal" runat="server" CssClass="modal">
            <div class="modal-card">
                <div class="modal-head">
                    <h3><i class="fas fa-user"></i> <asp:Label ID="lblMemberName" runat="server"></asp:Label></h3>
                    <asp:Button ID="btnCloseModal" runat="server" Text="✕" CssClass="btn" OnClick="btnCloseModal_Click" />
                </div>
                <div class="modal-body">
                    <div class="detail-grid">
                        <div class="detail-box"><div class="lbl">ระดับสมาชิก</div><div class="val" style="font-size:18px;"><asp:Label ID="lblDetailTier" runat="server"></asp:Label></div></div>
                        <div class="detail-box"><div class="lbl">คะแนนสะสมรายปี</div><div class="val" style="color:#f39c12;"><asp:Label ID="lblDetailYearly" runat="server"></asp:Label></div></div>
                        <div class="detail-box"><div class="lbl">คะแนนคงเหลือ</div><div class="val" style="color:#11998e;"><asp:Label ID="lblDetailAvailable" runat="server"></asp:Label></div></div>
                    </div>

                    <div class="adjust-form">
                        <label>ปรับคะแนน (ใส่จำนวนบวกเพื่อเพิ่ม, ลบเพื่อหัก)</label>
                        <div class="adjust-row">
                            <asp:TextBox ID="txtAdjustPoints" runat="server" CssClass="form-control" TextMode="Number" Width="120px" placeholder="เช่น 100 หรือ -50"></asp:TextBox>
                            <asp:TextBox ID="txtAdjustReason" runat="server" CssClass="form-control" Width="300px" placeholder="เหตุผล/หมายเหตุ"></asp:TextBox>
                            <label style="display:flex;align-items:center;gap:6px;margin:0;">
                                <asp:CheckBox ID="chkAffectTier" runat="server" Checked="true" /> มีผลต่อคะแนนรายปี (ระดับ)
                            </label>
                            <asp:Button ID="btnAdjust" runat="server" Text="บันทึก" CssClass="btn btn-primary" OnClick="btnAdjust_Click" />
                        </div>
                    </div>

                    <h4 style="margin:0 0 12px;"><i class="fas fa-history"></i> ประวัติคะแนนล่าสุด</h4>
                    <asp:GridView ID="gvMemberHistory" runat="server" CssClass="data-table" AutoGenerateColumns="False" GridLines="None">
                        <Columns>
                            <asp:BoundField DataField="TransactionType" HeaderText="ประเภท" />
                            <asp:BoundField DataField="Points" HeaderText="คะแนน" DataFormatString="{0:+#,##0;-#,##0;0}" />
                            <asp:BoundField DataField="Description" HeaderText="รายละเอียด" />
                            <asp:BoundField DataField="TransactionDate" HeaderText="วันที่" DataFormatString="{0:dd MMM yyyy HH:mm}" />
                        </Columns>
                    </asp:GridView>
                    <asp:HiddenField ID="hfSelectedPhone" runat="server" />
                </div>
            </div>
        </asp:Panel>
    </div>
</asp:Content>
