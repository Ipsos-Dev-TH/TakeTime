<%@ Page Title="รายงานการขาย" Language="C#" MaintainScrollPositionOnPostback="true" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="SellReport.aspx.cs" Inherits="Take_Time_BangPhra.Product.SellReport" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link rel="stylesheet" href="../Content/Product.css">

    <div class="product-page">
        <!-- Page Header -->
        <div class="product-header">
            <h2>📊 รายงานการขาย</h2>
            <div class="header-actions">
                <a href="/Product/Default" class="btn btn-primary">🛒 ขายสินค้า</a>
                <a href="/Product/Stock" class="btn btn-outline" style="background: white; color: #e65100;">📦 ดูสต็อก</a>
            </div>
        </div>

        <!-- Navigation Tabs -->
        <div class="product-nav">
            <a href="/Product/Default" class="product-nav-item">🛒 ขายสินค้า</a>
            <a href="/Product/In" class="product-nav-item">📥 นำเข้า</a>
            <a href="/Product/Stock" class="product-nav-item">📦 สต็อก</a>
            <a href="/Product/SellReport" class="product-nav-item active">📊 รายงาน</a>
        </div>

        <!-- Search Filters -->
        <div class="product-card">
            <div class="product-card-header">
                <h3>🔍 ค้นหารายงาน</h3>
            </div>
            <div class="product-card-body">
                <div class="product-form">
                    <div class="form-row">
                        <div class="form-group">
                            <label>📅 วันที่เริ่มต้น</label>
                            <asp:TextBox ID="TextBox1" runat="server" CssClass="form-control"
                                TextMode="Date"></asp:TextBox>
                        </div>
                        <div class="form-group">
                            <label>📅 วันที่สิ้นสุด</label>
                            <asp:TextBox ID="TextBox2" runat="server" CssClass="form-control"
                                TextMode="Date"></asp:TextBox>
                        </div>
                        <div class="form-group" style="display: flex; align-items: flex-end;">
                            <asp:Button ID="Button3" runat="server" Text="🔍 ค้นหา"
                                OnClick="Button3_Click1" CssClass="btn btn-primary" />
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <!-- Sales Statistics -->
        <asp:Panel ID="pnlStats" runat="server" Visible="false">
            <div class="product-stats">
                <div class="stat-card">
                    <h4>💰 ยอดขายรวม</h4>
                    <div class="stat-value">
                        <asp:Label ID="lblTotalSales" runat="server" Text="0"></asp:Label>
                        <span class="stat-unit">บาท</span>
                    </div>
                </div>
                <div class="stat-card success">
                    <h4>📦 จำนวนรายการ</h4>
                    <div class="stat-value">
                        <asp:Label ID="lblTotalItems" runat="server" Text="0"></asp:Label>
                        <span class="stat-unit">รายการ</span>
                    </div>
                </div>
                <div class="stat-card warning">
                    <h4>🛒 จำนวนชิ้น</h4>
                    <div class="stat-value">
                        <asp:Label ID="lblTotalQuantity" runat="server" Text="0"></asp:Label>
                        <span class="stat-unit">ชิ้น</span>
                    </div>
                </div>
                <div class="stat-card" style="background: linear-gradient(135deg, #5d4037, #8d6e63);">
                    <h4 style="color: white;">📋 ใบเสร็จ</h4>
                    <div class="stat-value" style="color: white;">
                        <asp:Label ID="lblTotalReceipts" runat="server" Text="0"></asp:Label>
                        <span class="stat-unit" style="color: rgba(255,255,255,0.8);">ใบ</span>
                    </div>
                </div>
            </div>
        </asp:Panel>

        <!-- Sales Report Table -->
        <div class="product-card">
            <div class="product-card-header">
                <h3>📋 รายการขาย</h3>
            </div>
            <div class="product-card-body" style="padding: 0;">
                <asp:GridView ID="GridView1" runat="server" AutoGenerateColumns="False"
                    CssClass="product-table" GridLines="None"
                    OnRowCommand="GridView1_RowCommand">
                    <Columns>
                        <asp:BoundField DataField="ID" HeaderText="ID" />
                        <asp:TemplateField HeaderText="วันที่ขาย">
                            <ItemTemplate>
                                <%# Convert.ToDateTime(Eval("DateTime_Out")).ToString("dd/MM/yyyy HH:mm") %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="Category_Name" HeaderText="หมวดหมู่" />
                        <asp:BoundField DataField="Product_Name" HeaderText="ชื่อสินค้า" />
                        <asp:TemplateField HeaderText="จำนวน">
                            <ItemTemplate>
                                <%# Eval("Amount") %>
                            </ItemTemplate>
                            <ItemStyle HorizontalAlign="Right" />
                            <HeaderStyle HorizontalAlign="Right" />
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="ราคา/ชิ้น">
                            <ItemTemplate>
                                <%# string.Format("{0:N2}", Eval("PricePerUnit")) %>
                            </ItemTemplate>
                            <ItemStyle HorizontalAlign="Right" />
                            <HeaderStyle HorizontalAlign="Right" />
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="ราคารวม">
                            <ItemTemplate>
                                <%# string.Format("{0:N2}", Eval("Price_Total")) %>
                            </ItemTemplate>
                            <ItemStyle HorizontalAlign="Right" />
                            <HeaderStyle HorizontalAlign="Right" />
                        </asp:TemplateField>
                        <asp:BoundField DataField="Remark" HeaderText="หมายเหตุ" />
                        <asp:BoundField DataField="Paid_How" HeaderText="ชำระ" />
                        <asp:TemplateField HeaderText="ใบเสร็จ">
                            <ItemTemplate>
                                <%# Eval("Account_Receipt_ID") != DBNull.Value && !string.IsNullOrEmpty(Eval("Account_Receipt_ID").ToString()) && Eval("Account_Receipt_ID").ToString() != "0" ? Eval("Account_Receipt_ID") : "-" %>
                            </ItemTemplate>
                            <ItemStyle HorizontalAlign="Center" />
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="จัดการ" Visible="false">
                            <ItemTemplate>
                                <asp:Button ID="btnDelete" runat="server" Text="🗑️ ลบ"
                                    CommandName="DeleteItem" CommandArgument='<%# Container.DataItemIndex %>'
                                    CssClass="btn btn-danger btn-sm"
                                    OnClientClick="return confirm('ยืนยันการลบรายการนี้?');" />
                            </ItemTemplate>
                            <ItemStyle HorizontalAlign="Center" />
                        </asp:TemplateField>
                    </Columns>
                    <EmptyDataTemplate>
                        <div class="empty-state">
                            <div class="icon">📊</div>
                            <h4>ยังไม่มีข้อมูล</h4>
                            <p>กรุณาเลือกช่วงวันที่และกดค้นหา</p>
                        </div>
                    </EmptyDataTemplate>
                </asp:GridView>
            </div>
        </div>

        <!-- Info Panel -->
        <div class="alert-panel info">
            <div class="alert-icon">💡</div>
            <div class="alert-content">
                <div class="alert-title">คำแนะนำการใช้งาน</div>
                <div class="alert-text">
                    1. เลือกช่วงวันที่ที่ต้องการดูรายงาน<br>
                    2. กดปุ่ม "ค้นหา" เพื่อแสดงผลลัพธ์<br>
                    3. รายงานจะแสดงข้อมูลการขายทั้งหมดในช่วงเวลาที่เลือก
                </div>
            </div>
        </div>

        <asp:Panel ID="Panel1" runat="server" Visible="false"></asp:Panel>
    </div>

    <asp:Literal ID="Literal1" runat="server"></asp:Literal>
</asp:Content>
