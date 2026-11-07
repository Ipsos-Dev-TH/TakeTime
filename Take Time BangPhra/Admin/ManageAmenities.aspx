<%@ Page Title="จัดการเบิกของใช้ในห้อง" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeFile="ManageAmenities.aspx.cs" Inherits="Take_Time_BangPhra.Admin.ManageAmenities" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .am-admin { padding: 20px; }
        .section-header {
            background: linear-gradient(135deg, #00b09b 0%, #4a7c59 100%);
            color: #fff; padding: 15px 20px; border-radius: 8px; margin-bottom: 20px;
            box-shadow: 0 2px 10px rgba(0,0,0,.1);
            display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 10px;
        }
        .section-header h2 { margin: 0; font-size: 22px; font-weight: 600; }
        .header-actions { display: flex; gap: 10px; flex-wrap: wrap; }

        .filter-section, .form-section, .grid-section {
            background: #fff; padding: 16px 20px; border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,.1); margin-bottom: 20px;
        }
        .form-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(230px, 1fr)); gap: 14px; }
        .form-group label { display: block; font-weight: 500; color: #555; font-size: 13.5px; margin-bottom: 5px; }
        .form-group input[type=text], .form-group textarea, .form-group select {
            width: 100%; padding: 8px 12px; border: 2px solid #e0e0e0; border-radius: 6px;
            font-family: inherit; font-size: 14px;
        }
        .required { color: #e53935; margin-left: 3px; }
        .form-actions { margin-top: 16px; display: flex; gap: 10px; flex-wrap: wrap; }
        .hint { font-size: 12.5px; color: #777; margin-top: 4px; }

        .btn-primary, .btn-success, .btn-secondary, .btn-warning, .btn-danger, .btn-info {
            padding: 9px 18px; border: none; border-radius: 8px; color: #fff;
            cursor: pointer; font-weight: 500; font-size: 13.5px; font-family: inherit;
        }
        .btn-primary { background: #1976d2; }
        .btn-success { background: #43a047; }
        .btn-secondary { background: #757575; }
        .btn-warning { background: #fb8c00; }
        .btn-danger { background: #e53935; }
        .btn-info { background: #00897b; }
        .btn-sm { padding: 4px 10px; font-size: 12px; }

        .am-table { width: 100%; border-collapse: collapse; }
        .am-table th { background: #f5f7f6; padding: 10px; text-align: left; font-size: 13px; color: #555; }
        .am-table td { padding: 10px; border-top: 1px solid #eee; font-size: 13.5px; vertical-align: middle; }
        .empty-data { text-align: center; padding: 40px; color: #999; }
        .alert { display: block; padding: 12px 16px; border-radius: 8px; margin-bottom: 16px; font-size: 14px; }
        .alert-success { background: #e8f5e9; color: #2e7d32; border: 1px solid #a5d6a7; }
        .alert-danger { background: #ffebee; color: #c62828; border: 1px solid #ef9a9a; }
    </style>

    <div class="am-admin">
        <asp:Label ID="lblMessage" runat="server" Visible="false"></asp:Label>

        <!-- ═══ รายการของใช้ ═══ -->
        <asp:Panel ID="pnlItems" runat="server" Visible="true">
            <div class="section-header">
                <h2>🧺 เบิกของใช้ในห้อง</h2>
                <div class="header-actions">
                    <asp:Button ID="btnTabRequests" runat="server" Text="📥 คำขอที่เข้ามา" CssClass="btn-info"
                        OnClick="btnTabRequests_Click" CausesValidation="false" />
                    <asp:Button ID="btnNewItem" runat="server" Text="+ เพิ่มรายการ" CssClass="btn-success"
                        OnClick="btnNewItem_Click" CausesValidation="false" />
                </div>
            </div>

            <div class="grid-section">
                <asp:GridView ID="gvItems" runat="server" AutoGenerateColumns="False" CssClass="am-table"
                    OnRowCommand="gvItems_RowCommand" ShowHeaderWhenEmpty="True" GridLines="None">
                    <Columns>
                        <asp:TemplateField HeaderText="รูป">
                            <ItemTemplate>
                                <%# string.IsNullOrEmpty(Eval("Image_Path") == null ? "" : Eval("Image_Path").ToString())
                                    ? "<span style='font-size:22px'>" + Eval("Icon") + "</span>"
                                    : "<img src='" + Eval("Image_Path") + "' style='width:46px;height:34px;object-fit:cover;border-radius:5px' />" %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="Name" HeaderText="ชื่อรายการ" />
                        <asp:BoundField DataField="Category" HeaderText="หมวด" NullDisplayText="-" />
                        <asp:TemplateField HeaderText="ค่าใช้จ่าย">
                            <ItemTemplate>
                                <%# ChargeText(Eval("Is_Free"), Eval("Price"), Eval("Free_Quota_Per_Stay"), Eval("Unit")) %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="Max_Per_Request" HeaderText="ครั้งละไม่เกิน" />
                        <asp:BoundField DataField="Sort_Order" HeaderText="ลำดับ" />
                        <asp:BoundField DataField="Status" HeaderText="สถานะ" />
                        <asp:TemplateField HeaderText="จัดการ">
                            <ItemTemplate>
                                <asp:Button runat="server" Text="แก้ไข" CssClass="btn-warning btn-sm"
                                    CommandName="EditItem" CommandArgument='<%# Eval("ID") %>' CausesValidation="false" />
                                <asp:Button runat="server" Text="ปิดใช้งาน" CssClass="btn-danger btn-sm"
                                    CommandName="DeleteItem" CommandArgument='<%# Eval("ID") %>' CausesValidation="false"
                                    OnClientClick="return confirm('ปิดใช้งานรายการนี้? (ใบเบิกเก่ายังอยู่ครบ)');" />
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                    <EmptyDataTemplate>
                        <div class="empty-data">📭 ยังไม่มีรายการ — กด "เพิ่มรายการ" หรือรันไมเกรชัน PHASE19_02 ก่อน</div>
                    </EmptyDataTemplate>
                </asp:GridView>
            </div>
        </asp:Panel>

        <!-- ═══ ฟอร์มรายการ ═══ -->
        <asp:Panel ID="pnlForm" runat="server" Visible="false">
            <div class="form-section">
                <h3><asp:Label ID="lblFormTitle" runat="server" Text="เพิ่มรายการของใช้"></asp:Label></h3>
                <asp:HiddenField ID="hfItemId" runat="server" />

                <div class="form-grid">
                    <div class="form-group">
                        <label>ชื่อรายการ<span class="required">*</span></label>
                        <asp:TextBox ID="txtName" runat="server" MaxLength="200" placeholder="เช่น ผ้าเช็ดตัวเพิ่ม" />
                    </div>
                    <div class="form-group">
                        <label>หมวด</label>
                        <asp:TextBox ID="txtCategory" runat="server" MaxLength="50" placeholder="ห้องน้ำ / เครื่องนอน / เครื่องดื่ม" />
                    </div>
                    <div class="form-group">
                        <label>อิโมจิ (ใช้เมื่อไม่มีรูป)</label>
                        <asp:TextBox ID="txtIcon" runat="server" MaxLength="50" placeholder="🧻" />
                    </div>
                    <div class="form-group">
                        <label>หน่วยนับ</label>
                        <asp:TextBox ID="txtUnit" runat="server" MaxLength="30" Text="ชิ้น" placeholder="ผืน / ขวด / ชุด" />
                    </div>
                    <div class="form-group">
                        <label>ราคาต่อหน่วย (บาท)</label>
                        <asp:TextBox ID="txtPrice" runat="server" MaxLength="10" Text="0" />
                        <div class="hint">ใช้เมื่อไม่ได้ติ๊ก "ฟรีเสมอ"</div>
                    </div>
                    <div class="form-group">
                        <label>ฟรีกี่ชิ้นแรกต่อการเข้าพัก</label>
                        <asp:TextBox ID="txtQuota" runat="server" MaxLength="5" Text="0" />
                        <div class="hint">0 = คิดเงินทุกชิ้น · เช่น ใส่ 2 = ฟรี 2 ชิ้นแรก เกินนั้นคิดตามราคา</div>
                    </div>
                    <div class="form-group">
                        <label>เบิกได้ครั้งละไม่เกิน</label>
                        <asp:TextBox ID="txtMaxPer" runat="server" MaxLength="5" Text="5" />
                    </div>
                    <div class="form-group">
                        <label>ลำดับการแสดง</label>
                        <asp:TextBox ID="txtSortOrder" runat="server" MaxLength="5" Text="0" />
                    </div>
                    <div class="form-group">
                        <label>เงื่อนไข</label>
                        <asp:CheckBox ID="chkFree" runat="server" Checked="true" Text=" ฟรีเสมอ (ไม่คิดเงิน)" /><br />
                        <asp:CheckBox ID="chkActive" runat="server" Checked="true" Text=" เปิดให้เบิก" />
                    </div>
                </div>

                <div class="form-group" style="margin-top:12px;">
                    <label>คำอธิบาย</label>
                    <asp:TextBox ID="txtDescription" runat="server" TextMode="MultiLine" Rows="2" MaxLength="500"
                        placeholder="รายละเอียดที่แขกจะเห็นบนการ์ด" />
                </div>

                <div class="form-group" style="margin-top:12px;">
                    <label>รูปภาพ</label>
                    <asp:FileUpload ID="fuImage" runat="server" accept="image/*" />
                    <asp:Panel ID="pnlCurrentImage" runat="server" Visible="false" style="margin-top:8px;">
                        <asp:Image ID="imgCurrent" runat="server" Style="max-width:180px; border-radius:8px; display:block;" />
                        <asp:CheckBox ID="chkRemoveImage" runat="server" Text=" ลบรูปนี้" />
                    </asp:Panel>
                </div>

                <div class="form-actions">
                    <asp:Button ID="btnSaveItem" runat="server" Text="บันทึก" CssClass="btn-success" OnClick="btnSaveItem_Click" />
                    <asp:Button ID="btnCancelItem" runat="server" Text="ยกเลิก" CssClass="btn-secondary"
                        OnClick="btnCancelItem_Click" CausesValidation="false" />
                </div>
            </div>
        </asp:Panel>

        <!-- ═══ คำขอที่เข้ามา ═══ -->
        <asp:Panel ID="pnlRequests" runat="server" Visible="false">
            <div class="section-header">
                <h2>📥 คำขอเบิกของใช้</h2>
                <div class="header-actions">
                    <asp:Button ID="btnTabItems" runat="server" Text="← กลับไปรายการของใช้" CssClass="btn-secondary"
                        OnClick="btnTabItems_Click" CausesValidation="false" />
                </div>
            </div>

            <div class="filter-section">
                <label style="font-weight:500; margin-right:8px;">สถานะ:</label>
                <asp:DropDownList ID="ddlStatus" runat="server">
                    <asp:ListItem Value="" Text="ที่ยังไม่จบงาน (รอรับเรื่อง + กำลังจัดของ)" />
                    <asp:ListItem Value="PENDING" Text="รอรับเรื่อง" />
                    <asp:ListItem Value="ACCEPTED" Text="กำลังจัดของ" />
                    <asp:ListItem Value="DELIVERED" Text="ส่งแล้ว" />
                    <asp:ListItem Value="CANCELLED" Text="ยกเลิก" />
                </asp:DropDownList>
                <asp:Button ID="btnFilterRequests" runat="server" Text="กรอง" CssClass="btn-primary"
                    OnClick="btnFilterRequests_Click" CausesValidation="false" />
            </div>

            <div class="grid-section">
                <asp:GridView ID="gvRequests" runat="server" AutoGenerateColumns="False" CssClass="am-table"
                    OnRowCommand="gvRequests_RowCommand" ShowHeaderWhenEmpty="True" GridLines="None">
                    <Columns>
                        <asp:BoundField DataField="Request_Number" HeaderText="เลขที่" />
                        <asp:BoundField DataField="RoomName" HeaderText="ห้อง" NullDisplayText="-" />
                        <asp:BoundField DataField="ItemSummary" HeaderText="รายการ" />
                        <asp:TemplateField HeaderText="ยอด">
                            <ItemTemplate>
                                <%# Convert.ToDecimal(Eval("Total_Amount")) > 0
                                    ? Convert.ToDecimal(Eval("Total_Amount")).ToString("N0") + " บาท"
                                    : "<span style='color:#2e7d32'>ฟรี</span>" %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="ข้อความ">
                            <ItemTemplate><%# Eval("Note") %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="เวลา">
                            <ItemTemplate><%# Convert.ToDateTime(Eval("Requested_Date")).ToString("dd/MM HH:mm") %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="สถานะ">
                            <ItemTemplate><%# StatusText(Eval("Status")) %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="จัดการ">
                            <ItemTemplate>
                                <asp:Button runat="server" Text="รับเรื่อง" CssClass="btn-primary btn-sm"
                                    CommandName="Accept" CommandArgument='<%# Eval("ID") %>' CausesValidation="false"
                                    Visible='<%# Eval("Status").ToString() == "PENDING" %>' />
                                <asp:Button runat="server" Text="ส่งแล้ว" CssClass="btn-success btn-sm"
                                    CommandName="Deliver" CommandArgument='<%# Eval("ID") %>' CausesValidation="false"
                                    Visible='<%# Eval("Status").ToString() == "PENDING" || Eval("Status").ToString() == "ACCEPTED" %>' />
                                <asp:Button runat="server" Text="ยกเลิก" CssClass="btn-danger btn-sm"
                                    CommandName="Cancel" CommandArgument='<%# Eval("ID") %>' CausesValidation="false"
                                    Visible='<%# Eval("Status").ToString() == "PENDING" || Eval("Status").ToString() == "ACCEPTED" %>'
                                    OnClientClick="return confirm('ยกเลิกคำขอนี้?');" />
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                    <EmptyDataTemplate>
                        <div class="empty-data">📭 ไม่มีคำขอในสถานะนี้</div>
                    </EmptyDataTemplate>
                </asp:GridView>
            </div>
        </asp:Panel>
    </div>
</asp:Content>
