<%@ Page Title="จัดการโปรโมชั่น" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeFile="ManagePromotions.aspx.cs" Inherits="Take_Time_BangPhra.Admin.ManagePromotions" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .promo-mgmt { padding: 20px; max-width: 1200px; margin: 0 auto; }
        .promo-header {
            background: linear-gradient(135deg, #e67e22 0%, #d35400 100%);
            color: white; padding: 16px 22px; border-radius: 8px; margin-bottom: 20px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.12);
        }
        .promo-header h2 { margin: 0; font-size: 22px; font-weight: 600; }
        .promo-card {
            background: white; border-radius: 8px; padding: 20px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.08); margin-bottom: 20px;
        }
        .promo-card h3 { margin-top: 0; color: #2c3e50; font-size: 18px; border-bottom: 2px solid #e67e22; padding-bottom: 8px; }
        .form-row { display: flex; flex-wrap: wrap; gap: 16px; margin-bottom: 14px; }
        .form-group { display: flex; flex-direction: column; gap: 4px; }
        .form-group label { font-weight: 600; color: #34495e; font-size: 13px; }
        .form-group .inp, .form-group textarea {
            padding: 8px 10px; border: 1px solid #ddd; border-radius: 5px; font-size: 14px;
        }
        .form-group textarea { min-height: 70px; resize: vertical; }
        .promo-btn {
            background: #e67e22; color: white; padding: 10px 26px; border: none;
            border-radius: 5px; font-size: 15px; font-weight: 600; cursor: pointer;
        }
        .promo-btn:hover { background: #d35400; }
        .promo-btn.cancel { background: #95a5a6; }
        .promo-btn.cancel:hover { background: #7f8c8d; }
        .promo-grid { width: 100%; border-collapse: collapse; background: white; }
        .promo-grid th { background: #34495e; color: white; padding: 10px; text-align: left; font-size: 13px; }
        .promo-grid td { padding: 8px 10px; border-bottom: 1px solid #eee; font-size: 13px; vertical-align: middle; }
        .promo-grid tr:hover { background: #faf6f0; }
        .promo-thumb { width: 80px; height: 54px; object-fit: cover; border-radius: 4px; border: 1px solid #ddd; }
        .badge { display: inline-block; padding: 2px 9px; border-radius: 11px; font-size: 11px; font-weight: 600; }
        .badge.on { background: #d4edda; color: #155724; }
        .badge.off { background: #f8d7da; color: #721c24; }
        .badge.home { background: #fdebd0; color: #9c640c; }
        .grid-act { background: #3498db; color: white; border: none; padding: 5px 10px; border-radius: 4px; cursor: pointer; font-size: 12px; }
        .grid-act.del { background: #e74c3c; }
        .grid-act.tog { background: #16a085; }
        .curimg { margin-top: 6px; }
        .curimg img { max-height: 60px; border-radius: 4px; border: 1px solid #ddd; }
        @media (max-width: 768px){ .form-group .inp, .form-group textarea { width: 100% !important; } }
    </style>

    <div class="promo-mgmt">
        <div class="promo-header"><h2>📢 จัดการโฆษณา / โปรโมชั่น</h2></div>

        <asp:Literal ID="lblMsg" runat="server" Visible="false"></asp:Literal>

        <!-- ฟอร์มเพิ่ม/แก้ไข -->
        <div class="promo-card">
            <h3><asp:Literal ID="litFormTitle" runat="server" Text="เพิ่มโปรโมชั่นใหม่"></asp:Literal></h3>
            <asp:HiddenField ID="hfEditId" runat="server" Value="0" />

            <div class="form-row">
                <div class="form-group" style="flex:2;">
                    <label>ชื่อโปรโมชั่น *</label>
                    <asp:TextBox ID="txtTitle" runat="server" CssClass="inp" Width="100%" placeholder="เช่น โปรเข้าพักฤดูฝน ลด 20%"></asp:TextBox>
                </div>
                <div class="form-group" style="flex:1;">
                    <label>ลำดับการแสดง</label>
                    <asp:TextBox ID="txtSortOrder" runat="server" CssClass="inp" Width="100%" TextMode="Number" Text="0"></asp:TextBox>
                </div>
            </div>

            <div class="form-row">
                <div class="form-group" style="flex:1; width:100%;">
                    <label>รายละเอียด</label>
                    <asp:TextBox ID="txtDescription" runat="server" CssClass="inp" TextMode="MultiLine" Width="100%"></asp:TextBox>
                </div>
            </div>

            <div class="form-row">
                <div class="form-group" style="flex:1;">
                    <label>รูปโปรโมชั่น (JPG/PNG, สูงสุด 10MB)</label>
                    <asp:FileUpload ID="fuImage" runat="server" CssClass="inp" />
                    <div class="curimg">
                        <asp:Image ID="imgCurrent" runat="server" Visible="false" />
                    </div>
                </div>
                <div class="form-group" style="flex:1;">
                    <label>ลิงก์เมื่อคลิก (ไม่บังคับ)</label>
                    <asp:TextBox ID="txtLinkUrl" runat="server" CssClass="inp" Width="100%" placeholder="https://... หรือ /Reservation"></asp:TextBox>
                </div>
            </div>

            <div class="form-row">
                <div class="form-group">
                    <label>เริ่มแสดง (ว่าง = ทันที)</label>
                    <asp:TextBox ID="txtStartDate" runat="server" CssClass="inp" TextMode="Date"></asp:TextBox>
                </div>
                <div class="form-group">
                    <label>สิ้นสุด (ว่าง = ไม่จำกัด)</label>
                    <asp:TextBox ID="txtEndDate" runat="server" CssClass="inp" TextMode="Date"></asp:TextBox>
                </div>
                <div class="form-group" style="justify-content:flex-end;">
                    <label>&nbsp;</label>
                    <label style="font-weight:500;"><asp:CheckBox ID="chkShowHome" runat="server" Checked="true" /> เด้งขึ้นหน้าแรก (popup)</label>
                </div>
                <div class="form-group" style="justify-content:flex-end;">
                    <label>&nbsp;</label>
                    <label style="font-weight:500;"><asp:CheckBox ID="chkActive" runat="server" Checked="true" /> เปิดใช้งาน</label>
                </div>
            </div>

            <div class="form-row">
                <asp:Button ID="btnSave" runat="server" Text="💾 บันทึก" CssClass="promo-btn" OnClick="btnSave_Click" />
                <asp:Button ID="btnCancel" runat="server" Text="ยกเลิก" CssClass="promo-btn cancel" OnClick="btnCancel_Click" CausesValidation="false" />
            </div>
        </div>

        <!-- รายการ -->
        <div class="promo-card">
            <h3>รายการโปรโมชั่นทั้งหมด</h3>
            <asp:GridView ID="gv" runat="server" CssClass="promo-grid" AutoGenerateColumns="False"
                DataKeyNames="ID" EmptyDataText="ยังไม่มีโปรโมชั่น"
                OnRowCommand="gv_RowCommand">
                <Columns>
                    <asp:TemplateField HeaderText="รูป">
                        <ItemTemplate>
                            <asp:Image ID="imgThumb" runat="server" CssClass="promo-thumb"
                                ImageUrl='<%# string.IsNullOrEmpty(Eval("Image_Url") as string) ? "/Images/Logo.png" : Convert.ToString(Eval("Image_Url")) %>' />
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:BoundField DataField="Title" HeaderText="ชื่อ" />
                    <asp:BoundField DataField="Sort_Order" HeaderText="ลำดับ" />
                    <asp:TemplateField HeaderText="ช่วงเวลา">
                        <ItemTemplate>
                            <%# FormatRange(Eval("Start_Date"), Eval("End_Date")) %>
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="สถานะ">
                        <ItemTemplate>
                            <span class='badge <%# (Eval("Status") as string) == "True" ? "on" : "off" %>'>
                                <%# (Eval("Status") as string) == "True" ? "เปิด" : "ปิด" %></span>
                            <%# Convert.ToBoolean(Eval("Show_On_Homepage")) ? "<span class='badge home'>หน้าแรก</span>" : "" %>
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="จัดการ">
                        <ItemTemplate>
                            <asp:Button runat="server" CssClass="grid-act" Text="แก้ไข" CommandName="EditPromo" CommandArgument='<%# Eval("ID") %>' CausesValidation="false" />
                            <asp:Button runat="server" CssClass="grid-act tog" Text="เปิด/ปิด" CommandName="TogglePromo" CommandArgument='<%# Eval("ID") %>' CausesValidation="false" />
                            <asp:Button runat="server" CssClass="grid-act del" Text="ลบ" CommandName="DeletePromo" CommandArgument='<%# Eval("ID") %>' CausesValidation="false"
                                OnClientClick="return confirm('ยืนยันลบโปรโมชั่นนี้?');" />
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
            </asp:GridView>
        </div>
    </div>
</asp:Content>
