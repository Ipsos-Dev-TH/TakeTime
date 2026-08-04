<%@ Page Title="จัดการกิจกรรม" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ActivityManagement.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Settings.ActivityManagement" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .am-wrap { max-width: 1250px; margin: 0 auto; padding: 18px 12px 50px; }
        .am-head {
            background: linear-gradient(135deg, #2e5d3a, #4a7c59); color: #fff;
            border-radius: 12px; padding: 22px 26px; margin-bottom: 22px;
        }
        .am-head h2 { margin: 0 0 6px; font-weight: 700; }
        .am-head p { margin: 0; opacity: .9; font-size: 14px; }
        .am-card { background: #fff; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,.08); padding: 20px; margin-bottom: 20px; }
        .am-card h3 { margin: 0 0 16px; font-size: 1.18em; color: #2e5d3a; font-weight: 700; }
        .am-tabs { display: flex; gap: 8px; margin-bottom: 18px; flex-wrap: wrap; }
        .am-tab { padding: 9px 20px; border-radius: 8px; border: 1px solid #cfe0d5; background: #fff; cursor: pointer; font-weight: 600; color: #2e5d3a; }
        .am-tab.active { background: #4a7c59; color: #fff; border-color: #4a7c59; }
        .am-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 14px; }
        .form-row { margin-bottom: 12px; }
        .form-row label { display: block; font-weight: 600; margin-bottom: 5px; font-size: 13.5px; color: #445; }
        .form-row .help { font-size: 12px; color: #8a9a90; margin-top: 3px; }
        .am-table { width: 100%; border-collapse: collapse; font-size: 13.5px; }
        .am-table th { background: #f2f7f4; padding: 10px; text-align: left; border-bottom: 2px solid #dbe8e0; white-space: nowrap; }
        .am-table td { padding: 10px; border-bottom: 1px solid #eef3f0; vertical-align: middle; }
        .am-table tr:hover td { background: #fafcfb; }
        .pill { display: inline-block; padding: 3px 10px; border-radius: 12px; font-size: 11.5px; font-weight: 700; color: #fff; }
        .p-green { background: #27ae60; } .p-orange { background: #e67e22; } .p-blue { background: #2980b9; }
        .p-grey { background: #95a5a6; } .p-red { background: #c0392b; }
        .bookable-only { display: none; }
        .bookable-only.show { display: block; }
    </style>

    <div class="am-wrap">
        <div class="am-head">
            <h2><i class="fas fa-person-hiking"></i> จัดการกิจกรรม</h2>
            <p>ตั้งค่ากิจกรรมในที่พัก · กิจกรรมที่ต้องจองเวลา (เช่น โต๊ะปิงปอง) · อนุมัติการจอง/ตรวจสลิป</p>
        </div>

        <asp:Panel ID="pnlMessage" runat="server" Visible="false" CssClass="am-card" style="padding:14px 18px;">
            <asp:Literal ID="litMessage" runat="server" />
        </asp:Panel>

        <div class="am-tabs">
            <asp:LinkButton ID="btnTabList" runat="server" CssClass="am-tab active" OnClick="ShowTab_Click" CommandArgument="list">
                <i class="fas fa-list"></i> รายการกิจกรรม
            </asp:LinkButton>
            <asp:LinkButton ID="btnTabEdit" runat="server" CssClass="am-tab" OnClick="ShowTab_Click" CommandArgument="edit">
                <i class="fas fa-plus"></i> เพิ่มกิจกรรมใหม่
            </asp:LinkButton>
            <asp:LinkButton ID="btnTabBookings" runat="server" CssClass="am-tab" OnClick="ShowTab_Click" CommandArgument="bookings">
                <i class="fas fa-calendar-check"></i> การจอง <asp:Literal ID="litPendingBadge" runat="server" />
            </asp:LinkButton>
        </div>

        <!-- ═══ รายการกิจกรรม ═══ -->
        <asp:Panel ID="pnlList" runat="server" CssClass="am-card">
            <h3><i class="fas fa-list"></i> กิจกรรมทั้งหมด</h3>
            <div style="overflow-x:auto;">
                <asp:GridView ID="gvActivities" runat="server" AutoGenerateColumns="false" CssClass="am-table"
                    GridLines="None" DataKeyNames="ID" OnRowCommand="gvActivities_RowCommand"
                    EmptyDataText="ยังไม่มีกิจกรรม — กดแท็บ 'เพิ่มกิจกรรมใหม่' เพื่อเริ่ม">
                    <Columns>
                        <asp:TemplateField HeaderText="กิจกรรม">
                            <ItemTemplate>
                                <b><%# Eval("ActivityName") %></b>
                                <div style="font-size:12px;color:#8a9a90;"><%# Eval("Location") %></div>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="ประเภท">
                            <ItemTemplate>
                                <%# Eval("Category").ToString() == "ON_PROPERTY" ? "ในที่พัก" : "ใกล้เคียง" %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="ราคา">
                            <ItemTemplate><%# FormatPrice(Container.DataItem) %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="จองเวลา">
                            <ItemTemplate><%# FormatBookable(Container.DataItem) %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="แสดงที่">
                            <ItemTemplate><%# FormatVisibility(Container.DataItem) %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="สถานะ">
                            <ItemTemplate>
                                <%# Convert.ToBoolean(Eval("IsActive"))
                                    ? "<span class='pill p-green'>เปิด</span>"
                                    : "<span class='pill p-grey'>ปิด</span>" %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="จัดการ">
                            <ItemTemplate>
                                <asp:LinkButton runat="server" CssClass="btn btn-primary btn-xs" CommandName="EditItem"
                                    CommandArgument='<%# Eval("ID") %>'><i class="fas fa-pen"></i> แก้ไข</asp:LinkButton>
                                <asp:LinkButton runat="server" CssClass="btn btn-danger btn-xs" CommandName="DeleteItem"
                                    CommandArgument='<%# Eval("ID") %>'
                                    OnClientClick="return confirm('ลบกิจกรรมนี้?\n(ถ้ามีการจองอยู่ ระบบจะปิดการใช้งานแทนการลบ)');">
                                    <i class="fas fa-trash"></i></asp:LinkButton>
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>
        </asp:Panel>

        <!-- ═══ เพิ่ม / แก้ไข ═══ -->
        <asp:Panel ID="pnlEdit" runat="server" CssClass="am-card" Visible="false">
            <h3><i class="fas fa-pen-to-square"></i> <asp:Literal ID="litEditTitle" runat="server" Text="เพิ่มกิจกรรมใหม่" /></h3>
            <asp:HiddenField ID="hfActivityId" runat="server" Value="0" />

            <div class="am-grid">
                <div class="form-row">
                    <label>ชื่อกิจกรรม <span style="color:#c0392b;">*</span></label>
                    <asp:TextBox ID="txtName" runat="server" CssClass="form-control" placeholder="เช่น โต๊ะปิงปอง" />
                </div>
                <div class="form-row">
                    <label>ประเภท</label>
                    <asp:DropDownList ID="ddlCategory" runat="server" CssClass="form-control">
                        <asp:ListItem Value="ON_PROPERTY" Text="ในที่พัก" />
                        <asp:ListItem Value="OFF_PROPERTY" Text="สถานที่ใกล้เคียง" />
                    </asp:DropDownList>
                </div>
                <div class="form-row">
                    <label>สถานที่</label>
                    <asp:TextBox ID="txtLocation" runat="server" CssClass="form-control" placeholder="เช่น บริเวณส่วนกลาง" />
                </div>
            </div>

            <div class="form-row">
                <label>คำอธิบายสั้น (แสดงบนการ์ด)</label>
                <asp:TextBox ID="txtShortDesc" runat="server" CssClass="form-control" placeholder="เช่น จองเป็นรายชั่วโมง มีให้บริการ 2 โต๊ะ" />
            </div>
            <div class="form-row">
                <label>รายละเอียด</label>
                <asp:TextBox ID="txtDescription" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="3" />
            </div>
            <div class="form-row">
                <label>กติกา / ข้อควรรู้ (แสดงตอนจอง)</label>
                <asp:TextBox ID="txtRules" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="3"
                    placeholder="• กรุณามาตรงเวลา&#10;• เก็บอุปกรณ์หลังใช้งาน" />
            </div>

            <div class="am-grid">
                <div class="form-row">
                    <label>รูปภาพหลัก</label>
                    <asp:FileUpload ID="fuImage" runat="server" CssClass="form-control" accept="image/*" />
                    <div class="help">ไม่เลือก = ใช้รูปเดิม / ถ้าไม่มีรูปจะแสดงเป็นไอคอน</div>
                </div>
                <div class="form-row">
                    <label>ไอคอน (Font Awesome)</label>
                    <asp:TextBox ID="txtIcon" runat="server" CssClass="form-control" placeholder="fa-table-tennis-paddle-ball" />
                    <div class="help">ใช้เมื่อไม่มีรูป เช่น fa-water-ladder, fa-bicycle</div>
                </div>
                <div class="form-row">
                    <label>ลำดับการแสดง</label>
                    <asp:TextBox ID="txtOrder" runat="server" CssClass="form-control" TextMode="Number" Text="0" />
                </div>
            </div>

            <div class="am-grid">
                <div class="form-row">
                    <label>รูปแบบราคา</label>
                    <asp:DropDownList ID="ddlPricingMode" runat="server" CssClass="form-control">
                        <asp:ListItem Value="FREE" Text="ฟรี ไม่มีค่าใช้จ่าย" />
                        <asp:ListItem Value="PER_HOUR" Text="คิดรายชั่วโมง" />
                        <asp:ListItem Value="PER_SESSION" Text="คิดต่อครั้ง" />
                        <asp:ListItem Value="PER_PERSON" Text="คิดต่อคน" />
                    </asp:DropDownList>
                </div>
                <div class="form-row">
                    <label>ราคา (บาท)</label>
                    <asp:TextBox ID="txtPrice" runat="server" CssClass="form-control" TextMode="Number" Text="0" />
                </div>
                <div class="form-row">
                    <label>ระยะเวลา (ข้อความ)</label>
                    <asp:TextBox ID="txtDuration" runat="server" CssClass="form-control" placeholder="เช่น รายชั่วโมง / ตลอดวัน" />
                </div>
            </div>

            <div class="form-row" style="background:#f5f9f6;padding:14px;border-radius:8px;">
                <label style="font-size:15px;">
                    <asp:CheckBox ID="chkBookable" runat="server" onclick="toggleBookable()" />
                    <b>ต้องจองช่วงเวลาก่อนใช้งาน</b> (เช่น โต๊ะปิงปอง / สนาม / อุปกรณ์ที่มีจำกัด)
                </label>

                <div id="bookableSection" class="bookable-only" style="margin-top:14px;">
                    <div class="am-grid">
                        <div class="form-row">
                            <label>รองรับพร้อมกัน (คิว)</label>
                            <asp:TextBox ID="txtCapacity" runat="server" CssClass="form-control" TextMode="Number" Text="1" />
                            <div class="help">เช่น มีโต๊ะ 2 ตัว = 2 (จองเวลาเดียวกันได้ 2 คิว)</div>
                        </div>
                        <div class="form-row">
                            <label>เปิดบริการ</label>
                            <asp:TextBox ID="txtOpenTime" runat="server" CssClass="form-control" TextMode="Time" Text="08:00" />
                        </div>
                        <div class="form-row">
                            <label>ปิดบริการ</label>
                            <asp:TextBox ID="txtCloseTime" runat="server" CssClass="form-control" TextMode="Time" Text="21:00" />
                        </div>
                        <div class="form-row">
                            <label>ความยาว 1 ช่วง (นาที)</label>
                            <asp:TextBox ID="txtSlotMinutes" runat="server" CssClass="form-control" TextMode="Number" Text="60" />
                        </div>
                        <div class="form-row">
                            <label>จองต่อเนื่องสูงสุด (ช่วง)</label>
                            <asp:TextBox ID="txtMaxSlots" runat="server" CssClass="form-control" TextMode="Number" Text="3" />
                        </div>
                        <div class="form-row">
                            <label>จองล่วงหน้าได้ (วัน)</label>
                            <asp:TextBox ID="txtAdvanceDays" runat="server" CssClass="form-control" TextMode="Number" Text="14" />
                        </div>
                        <div class="form-row">
                            <label>จำนวนคนสูงสุด/ครั้ง</label>
                            <asp:TextBox ID="txtMaxParticipants" runat="server" CssClass="form-control" TextMode="Number" Text="0" />
                            <div class="help">0 = ไม่จำกัด</div>
                        </div>
                    </div>
                    <label>
                        <asp:CheckBox ID="chkRequireApproval" runat="server" />
                        ต้องให้เจ้าหน้าที่ยืนยันก่อน (ไม่ติ๊ก = จองแล้วยืนยันทันที)
                    </label>
                </div>
            </div>

            <div class="am-grid">
                <div class="form-row">
                    <label><asp:CheckBox ID="chkShowWebsite" runat="server" Checked="true" /> แสดงบนเว็บไซต์ (หน้าแรก)</label>
                </div>
                <div class="form-row">
                    <label><asp:CheckBox ID="chkShowPortal" runat="server" Checked="true" /> แสดงใน Guest Portal</label>
                </div>
                <div class="form-row">
                    <label><asp:CheckBox ID="chkActive" runat="server" Checked="true" /> เปิดใช้งาน</label>
                </div>
            </div>

            <div class="am-grid">
                <div class="form-row">
                    <label>ข้อมูลติดต่อ</label>
                    <asp:TextBox ID="txtContact" runat="server" CssClass="form-control" />
                </div>
                <div class="form-row">
                    <label>ลิงก์แผนที่</label>
                    <asp:TextBox ID="txtMapUrl" runat="server" CssClass="form-control" placeholder="https://maps.google.com/..." />
                </div>
            </div>

            <div style="margin-top:18px;">
                <asp:Button ID="btnSave" runat="server" Text="💾 บันทึก" CssClass="btn btn-success btn-lg" OnClick="btnSave_Click" />
                <asp:Button ID="btnCancelEdit" runat="server" Text="ยกเลิก" CssClass="btn btn-default btn-lg"
                    OnClick="btnCancelEdit_Click" CausesValidation="false" />
            </div>
        </asp:Panel>

        <!-- ═══ การจอง ═══ -->
        <asp:Panel ID="pnlBookings" runat="server" CssClass="am-card" Visible="false">
            <h3><i class="fas fa-calendar-check"></i> การจองกิจกรรม</h3>
            <div style="display:flex;gap:10px;align-items:end;flex-wrap:wrap;margin-bottom:16px;">
                <div>
                    <label style="display:block;font-weight:600;font-size:13px;">ตั้งแต่วันที่</label>
                    <asp:TextBox ID="txtFrom" runat="server" CssClass="form-control" TextMode="Date" />
                </div>
                <div>
                    <label style="display:block;font-weight:600;font-size:13px;">ถึงวันที่</label>
                    <asp:TextBox ID="txtTo" runat="server" CssClass="form-control" TextMode="Date" />
                </div>
                <div>
                    <label style="display:block;font-weight:600;font-size:13px;">สถานะ</label>
                    <asp:DropDownList ID="ddlStatusFilter" runat="server" CssClass="form-control">
                        <asp:ListItem Value="" Text="ทุกสถานะ" />
                        <asp:ListItem Value="PENDING" Text="รอดำเนินการ" />
                        <asp:ListItem Value="CONFIRMED" Text="ยืนยันแล้ว" />
                        <asp:ListItem Value="CANCELLED" Text="ยกเลิก" />
                    </asp:DropDownList>
                </div>
                <asp:Button ID="btnFilter" runat="server" Text="🔍 ค้นหา" CssClass="btn btn-primary" OnClick="btnFilter_Click" />
            </div>

            <div style="overflow-x:auto;">
                <asp:GridView ID="gvBookings" runat="server" AutoGenerateColumns="false" CssClass="am-table"
                    GridLines="None" DataKeyNames="ID" OnRowCommand="gvBookings_RowCommand"
                    EmptyDataText="ไม่พบการจองในช่วงที่เลือก">
                    <Columns>
                        <asp:TemplateField HeaderText="กิจกรรม">
                            <ItemTemplate>
                                <b><%# Eval("ActivityName") %></b>
                                <div style="font-size:12px;color:#8a9a90;">#<%# Eval("ID") %></div>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="วัน-เวลา">
                            <ItemTemplate><%# FormatSlot(Container.DataItem) %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="ผู้จอง">
                            <ItemTemplate>
                                <%# Eval("GuestName") %>
                                <div style="font-size:12px;color:#8a9a90;"><%# FormatGuestRef(Container.DataItem) %></div>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="ยอด">
                            <ItemTemplate><%# Convert.ToDecimal(Eval("TotalAmount")) > 0
                                ? "฿" + Convert.ToDecimal(Eval("TotalAmount")).ToString("N2") : "ฟรี" %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="การชำระ">
                            <ItemTemplate><%# FormatPayment(Container.DataItem) %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="สถานะ">
                            <ItemTemplate><%# FormatStatus(Container.DataItem) %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="จัดการ">
                            <ItemTemplate>
                                <asp:LinkButton runat="server" CssClass="btn btn-success btn-xs" CommandName="ApproveItem"
                                    CommandArgument='<%# Eval("ID") %>'
                                    Visible='<%# NeedsReview(Container.DataItem) %>'><i class="fas fa-check"></i> ยืนยัน</asp:LinkButton>
                                <asp:LinkButton runat="server" CssClass="btn btn-warning btn-xs" CommandName="RejectItem"
                                    CommandArgument='<%# Eval("ID") %>'
                                    OnClientClick="return confirm('ปฏิเสธการจองนี้?');"
                                    Visible='<%# NeedsReview(Container.DataItem) %>'><i class="fas fa-xmark"></i></asp:LinkButton>
                                <asp:LinkButton runat="server" CssClass="btn btn-info btn-xs" CommandName="MarkPaidItem"
                                    CommandArgument='<%# Eval("ID") %>'
                                    Visible='<%# CanMarkPaid(Container.DataItem) %>'><i class="fas fa-money-bill"></i> รับเงินแล้ว</asp:LinkButton>
                                <asp:LinkButton runat="server" CssClass="btn btn-danger btn-xs" CommandName="CancelItem"
                                    CommandArgument='<%# Eval("ID") %>'
                                    OnClientClick="return confirm('ยกเลิกการจองนี้?');"
                                    Visible='<%# CanCancel(Container.DataItem) %>'><i class="fas fa-ban"></i></asp:LinkButton>
                                <asp:HyperLink runat="server" CssClass="btn btn-default btn-xs" Target="_blank"
                                    NavigateUrl='<%# Eval("SlipFileURL") %>'
                                    Visible='<%# HasSlip(Container.DataItem) %>'><i class="fas fa-receipt"></i> สลิป</asp:HyperLink>
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>
        </asp:Panel>
    </div>

    <script>
        function toggleBookable() {
            var cb = document.getElementById('<%= chkBookable.ClientID %>');
            var sec = document.getElementById('bookableSection');
            if (cb && sec) sec.classList.toggle('show', cb.checked);
        }
        document.addEventListener('DOMContentLoaded', toggleBookable);
    </script>
</asp:Content>
