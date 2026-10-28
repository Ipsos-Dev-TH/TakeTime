<%@ Page Title="คืนสินค้าให้ผู้ขาย" Language="C#" MaintainScrollPositionOnPostback="true" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="PurchaseReturn.aspx.cs" Inherits="Take_Time_BangPhra.Product.PurchaseReturn" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link rel="stylesheet" href="../Content/Product.css">

    <div class="product-page">
        <div class="product-header">
            <h2>↩️ คืนสินค้าให้ผู้ขาย / ใบลดหนี้จากผู้ขาย</h2>
            <div class="header-actions">
                <a href="/Product/In" class="btn btn-outline" style="background: white; color: #e65100;">📥 นำเข้าสินค้า</a>
            </div>
        </div>

        <div class="product-nav">
            <a href="/Product/Default" class="product-nav-item">🛒 ขายสินค้า</a>
            <a href="/Product/In" class="product-nav-item">📥 นำเข้า</a>
            <a href="/Product/PurchaseReturn" class="product-nav-item active">↩️ คืนผู้ขาย</a>
            <a href="/Product/Stock" class="product-nav-item">📦 สต็อก</a>
            <a href="/Product/SellReport" class="product-nav-item">📊 รายงาน</a>
        </div>

        <asp:Literal ID="litInfo" runat="server"></asp:Literal>

        <div class="product-card">
            <div class="product-card-header">
                <h3>1) เลือกการรับของที่จะคืน</h3>
            </div>
            <div class="product-card-body">
                <div class="form-row">
                    <div class="form-group" style="flex: 3;">
                        <label>การรับของ (เลขที่รับของ · ผู้ขาย · ใบกำกับ)</label>
                        <asp:DropDownList ID="ddlBatch" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlBatch_SelectedIndexChanged" />
                    </div>
                </div>
                <asp:Literal ID="litBatchInfo" runat="server"></asp:Literal>
                <div class="table-responsive">
                    <asp:GridView ID="gvLines" runat="server" AutoGenerateColumns="False" CssClass="product-table" GridLines="None" DataKeyNames="ProductId">
                        <Columns>
                            <asp:BoundField DataField="ProductName" HeaderText="สินค้า" />
                            <asp:BoundField DataField="Qty" HeaderText="รับเข้า" DataFormatString="{0:N2}" ItemStyle-HorizontalAlign="Right" />
                            <asp:BoundField DataField="ReturnedQty" HeaderText="คืนแล้ว" DataFormatString="{0:N2}" ItemStyle-HorizontalAlign="Right" />
                            <asp:BoundField DataField="ReturnableQty" HeaderText="คืนได้อีก" DataFormatString="{0:N2}" ItemStyle-HorizontalAlign="Right" />
                            <asp:BoundField DataField="UnitCost" HeaderText="ราคาซื้อ/หน่วย (สุทธิ)" DataFormatString="{0:N4}" ItemStyle-HorizontalAlign="Right" />
                            <asp:TemplateField HeaderText="จำนวนที่คืน">
                                <ItemTemplate>
                                    <asp:TextBox ID="txtReturnQty" runat="server" CssClass="form-control" Width="110px" placeholder="0" />
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                        <EmptyDataTemplate>
                            <div class="empty-state"><p>เลือกการรับของด้านบน</p></div>
                        </EmptyDataTemplate>
                    </asp:GridView>
                </div>
            </div>
        </div>

        <div class="product-card">
            <div class="product-card-header">
                <h3>2) ข้อมูลการคืน / การชดเชย</h3>
            </div>
            <div class="product-card-body">
                <div class="form-row">
                    <div class="form-group">
                        <label>วันที่คืน / วันที่ใบลดหนี้</label>
                        <asp:TextBox ID="txtReturnDate" runat="server" CssClass="form-control" TextMode="Date" />
                    </div>
                    <div class="form-group">
                        <label>เลขที่ใบลดหนี้ของผู้ขาย</label>
                        <asp:TextBox ID="txtSupplierCnNo" runat="server" CssClass="form-control" MaxLength="60" />
                    </div>
                    <div class="form-group" style="flex: 2;">
                        <label>เหตุผล</label>
                        <asp:TextBox ID="txtReason" runat="server" CssClass="form-control" MaxLength="300" placeholder="เช่น สินค้าชำรุด / ไม่ตรงตามสั่ง / หมดอายุ" />
                    </div>
                </div>
                <div class="form-row">
                    <div class="form-group" style="flex: 2;">
                        <label>ผู้ขายชดเชยอย่างไร</label>
                        <asp:DropDownList ID="ddlSettlement" runat="server" CssClass="form-control">
                            <asp:ListItem Value="OFFSET" Text="หักยอดที่ค้างจ่าย / รอใบลดหนี้ (ไม่ได้รับเงินคืน)" Selected="True" />
                            <asp:ListItem Value="REFUND" Text="ผู้ขายคืนเงินแล้ว" />
                        </asp:DropDownList>
                    </div>
                    <div class="form-group">
                        <label>รับเงินคืนเข้า (กรณีคืนเงิน)</label>
                        <asp:DropDownList ID="ddlRefundPaidHow" runat="server" CssClass="form-control" AppendDataBoundItems="true">
                            <asp:ListItem Value="" Text="— เลือกแหล่งเงิน —" />
                        </asp:DropDownList>
                    </div>
                    <div class="form-group" style="flex: 2; justify-content: flex-end;">
                        <asp:CheckBox ID="chkVatReversal" runat="server"
                            Text=" กลับภาษีซื้อ (ใบเดิมเคลม VAT แล้ว + ได้รับใบลดหนี้จากผู้ขาย — ม.82/10, ม.86/10)" />
                    </div>
                </div>
                <asp:Panel ID="pnlCnMode" runat="server" Visible="false" style="background:#f3f8ff;border:1px solid #bbdefb;border-radius:6px;padding:10px 12px;margin-top:6px;">
                    <label style="font-weight:600;">ใบลดหนี้จากผู้ขาย (เอกสารที่ลดภาษีซื้อใน ภ.พ.30 — §82/10)</label>
                    <asp:RadioButtonList ID="rblCnMode" runat="server" RepeatDirection="Vertical" AutoPostBack="true" OnSelectedIndexChanged="rblCnMode_SelectedIndexChanged" CssClass="cn-mode">
                        <asp:ListItem Value="PENDING" Text=" รอใบลดหนี้จากผู้ขาย — ได้ใบแล้วสแกนที่หน้า OCR (ชนิด ใบลดหนี้) แล้วผูกเลขคืนนี้ (แนะนำ)" Selected="True" />
                        <asp:ListItem Value="LINK" Text=" มีใบลดหนี้ที่บันทึกจากหน้า OCR แล้ว — เลือกใบด้านล่าง (หน้านี้ตัดจำนวน/ต้นทุนเท่านั้น)" />
                        <asp:ListItem Value="NONE" Text=" ผู้ขายไม่ออกใบลดหนี้ — ลงบัญชีครบที่หน้านี้ (ภ.พ.30 จะไม่ลดภาษีซื้อเพราะไม่มีเอกสาร)" />
                    </asp:RadioButtonList>
                    <asp:DropDownList ID="ddlLinkCn" runat="server" CssClass="form-control" Enabled="false" style="margin-top:6px;" />
                    <asp:Literal ID="litCnHint" runat="server"></asp:Literal>
                </asp:Panel>
                <asp:Literal ID="litPreview" runat="server"></asp:Literal>
            </div>
            <div class="product-card-footer">
                <asp:Button ID="btnPreview" runat="server" Text="🧮 ดูรายการบัญชี" CssClass="btn btn-outline" OnClick="btnPreview_Click" />
                <asp:Button ID="btnSave" runat="server" Text="💾 บันทึกคืนสินค้า" CssClass="btn btn-success" OnClick="btnSave_Click"
                    OnClientClick="return confirm('ยืนยันบันทึกคืนสินค้าให้ผู้ขาย? สต๊อกจะลดลงทันที');" />
            </div>
        </div>

        <asp:Panel ID="pnlRecent" runat="server" CssClass="product-card" Visible="false">
            <div class="product-card-header">
                <h3>🗂️ การคืนสินค้าล่าสุด</h3>
            </div>
            <div class="product-card-body">
                <div class="table-responsive">
                    <asp:GridView ID="gvRecent" runat="server" AutoGenerateColumns="False" CssClass="product-table" GridLines="None">
                        <Columns>
                            <asp:BoundField DataField="Purchase_Return_Ref" HeaderText="เลขที่คืน" />
                            <asp:BoundField DataField="Return_Receipt_Batch" HeaderText="การรับของ" />
                            <asp:BoundField DataField="Returned_At" HeaderText="วันที่" DataFormatString="{0:dd/MM/yyyy HH:mm}" />
                            <asp:BoundField DataField="Items" HeaderText="รายการ" />
                            <asp:BoundField DataField="Net_Total" HeaderText="มูลค่าคืน (สุทธิ)" DataFormatString="{0:N2}" ItemStyle-HorizontalAlign="Right" />
                            <asp:BoundField DataField="Reason" HeaderText="เหตุผล" />
                            <asp:BoundField DataField="Cn_Info" HeaderText="ใบลดหนี้ผู้ขาย" />
                            <asp:TemplateField HeaderText="NextAcc">
                                <ItemTemplate><div class="na-stack"><%# NaReturnBadge(Eval("Purchase_Return_Ref")) %></div></ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                    </asp:GridView>
                </div>
                <asp:Panel ID="pnlPendingClose" runat="server" Visible="false" style="margin-top:10px;">
                    <div class="form-row">
                        <div class="form-group" style="flex: 3;">
                            <label>ปิดรายการรอใบลดหนี้ (ผู้ขายยืนยันว่าไม่ออกใบลดหนี้)</label>
                            <asp:DropDownList ID="ddlPendingReturn" runat="server" CssClass="form-control" />
                        </div>
                        <div class="form-group" style="justify-content: flex-end;">
                            <asp:Button ID="btnCloseNoCn" runat="server" Text="ปิดรายการ" CssClass="btn btn-outline" OnClick="btnCloseNoCn_Click"
                                OnClientClick="return confirm('ปิดรายการรอใบลดหนี้? JE คืนสินค้าที่ลงไว้ (ไม่กลับภาษีซื้อ) จะเป็นรายการจริง — ภาษีซื้อคงเดิมเพราะไม่มีใบลดหนี้');" />
                        </div>
                    </div>
                </asp:Panel>
            </div>
        </asp:Panel>

        <div class="alert-panel info">
            <div class="alert-icon">💡</div>
            <div class="alert-content">
                <div class="alert-title">ลงบัญชีอย่างไร</div>
                <div class="alert-text">
                    ตัดสต๊อก ณ ต้นทุนเฉลี่ย (Cr สินค้าคงเหลือ) · คู่บัญชีตามสถานะ: ยังไม่วางบิล = Dr GRNI, วางบิล/ตั้งหนี้แล้ว = Dr เจ้าหนี้,
                    ผู้ขายคืนเงิน = Dr แหล่งเงิน · กลับภาษีซื้อเฉพาะใบที่เคยเคลมแล้ว · ส่วนต่างราคาซื้อกับต้นทุนเฉลี่ย → บัญชีส่งคืนสินค้า (PURCHASE_RETURNS)
                    <br />วางบิลแล้ว (PHASE19_39): <b>ใบลดหนี้ใน NextAcc เป็นรายการบัญชี/ภาษีตัวจริง</b> — ระหว่างรอใบ หน้านี้ลง JE ชั่วคราวแบบไม่กลับภาษีซื้อ
                    แล้วระบบกลับรายการนั้นทั้งใบ (PRT-…-REV) และปรับต้นทุนเฉลี่ย (PRT-…-DIF) ตอนผูกใบลดหนี้ — ยอดไม่ซ้อน · จำนวนสต๊อกฝั่ง NextAcc ส่งตามเดิม
                </div>
            </div>
        </div>
    </div>
</asp:Content>
