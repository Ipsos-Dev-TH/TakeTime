<%@ Page Title="ชำระเงินเพิ่ม" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="MakePayment.aspx.cs" Inherits="Take_Time_BangPhra.Payment.MakePayment" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .payment-container {
            max-width: 1200px;
            margin: 20px auto;
            padding: 20px;
        }

        .payment-card {
            background: white;
            border-radius: 8px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
            padding: 25px;
            margin-bottom: 20px;
        }

        .card-header {
            font-size: 20px;
            font-weight: bold;
            color: #2c3e50;
            margin-bottom: 20px;
            padding-bottom: 10px;
            border-bottom: 2px solid #3498db;
        }

        .info-row {
            display: flex;
            justify-content: space-between;
            padding: 10px 0;
            border-bottom: 1px solid #ecf0f1;
        }

        .info-label {
            font-weight: bold;
            color: #7f8c8d;
        }

        .info-value {
            color: #2c3e50;
        }

        .amount-highlight {
            font-size: 24px;
            font-weight: bold;
            color: #e74c3c;
        }

        .amount-paid {
            color: #27ae60;
        }

        .form-group {
            margin-bottom: 20px;
        }

        .form-label {
            display: block;
            font-weight: bold;
            margin-bottom: 8px;
            color: #2c3e50;
        }

        .form-control {
            width: 100%;
            padding: 10px;
            border: 1px solid #ddd;
            border-radius: 4px;
            font-size: 14px;
        }

        .form-control:focus {
            border-color: #3498db;
            outline: none;
            box-shadow: 0 0 0 3px rgba(52, 152, 219, 0.1);
        }

        .btn-primary {
            background-color: #3498db;
            color: white;
            padding: 12px 30px;
            border: none;
            border-radius: 4px;
            font-size: 16px;
            font-weight: bold;
            cursor: pointer;
            transition: background-color 0.3s;
        }

        .btn-primary:hover {
            background-color: #2980b9;
        }

        .btn-secondary {
            background-color: #95a5a6;
            color: white;
            padding: 12px 30px;
            border: none;
            border-radius: 4px;
            font-size: 16px;
            cursor: pointer;
            text-decoration: none;
            display: inline-block;
            margin-left: 10px;
        }

        .btn-secondary:hover {
            background-color: #7f8c8d;
        }

        .alert {
            padding: 15px;
            border-radius: 4px;
            margin-bottom: 20px;
        }

        .alert-success {
            background-color: #d4edda;
            border: 1px solid #c3e6cb;
            color: #155724;
        }

        .alert-danger {
            background-color: #f8d7da;
            border: 1px solid #f5c6cb;
            color: #721c24;
        }

        .alert-warning {
            background-color: #fff3cd;
            border: 1px solid #ffeaa7;
            color: #856404;
        }

        .payment-history-table {
            width: 100%;
            border-collapse: collapse;
            margin-top: 10px;
        }

        .payment-history-table th {
            background-color: #34495e;
            color: white;
            padding: 12px;
            text-align: left;
            font-weight: bold;
        }

        .payment-history-table td {
            padding: 10px;
            border-bottom: 1px solid #ecf0f1;
        }

        .payment-history-table tr:hover {
            background-color: #f8f9fa;
        }

        .badge {
            padding: 4px 8px;
            border-radius: 3px;
            font-size: 12px;
            font-weight: bold;
        }

        .badge-success {
            background-color: #27ae60;
            color: white;
        }

        .badge-warning {
            background-color: #f39c12;
            color: white;
        }

        .badge-info {
            background-color: #3498db;
            color: white;
        }
    </style>

    <div class="payment-container">
        <h2 style="color: #2c3e50; margin-bottom: 30px;">
            <i class="fa fa-credit-card"></i> ชำระเงินเพิ่ม
        </h2>

        <!-- Alert Messages -->
        <asp:Panel ID="pnlSuccess" runat="server" CssClass="alert alert-success" Visible="false">
            <asp:Label ID="lblSuccess" runat="server"></asp:Label>
        </asp:Panel>

        <asp:Panel ID="pnlError" runat="server" CssClass="alert alert-danger" Visible="false">
            <asp:Label ID="lblError" runat="server"></asp:Label>
        </asp:Panel>

        <!-- Reservation Information -->
        <div class="payment-card">
            <div class="card-header">
                <i class="fa fa-info-circle"></i> ข้อมูลการจอง
            </div>

            <div class="info-row">
                <span class="info-label">รหัสการจอง:</span>
                <span class="info-value"><asp:Label ID="lblReservationID" runat="server"></asp:Label></span>
            </div>

            <div class="info-row">
                <span class="info-label">ชื่อลูกค้า:</span>
                <span class="info-value"><asp:Label ID="lblCustomerName" runat="server"></asp:Label></span>
            </div>

            <div class="info-row">
                <span class="info-label">เบอร์โทร:</span>
                <span class="info-value"><asp:Label ID="lblCustomerPhone" runat="server"></asp:Label></span>
            </div>

            <div class="info-row">
                <span class="info-label">วันเช็คอิน:</span>
                <span class="info-value"><asp:Label ID="lblCheckinDate" runat="server"></asp:Label></span>
            </div>

            <div class="info-row">
                <span class="info-label">วันเช็คเอาท์:</span>
                <span class="info-value"><asp:Label ID="lblCheckoutDate" runat="server"></asp:Label></span>
            </div>

            <div class="info-row">
                <span class="info-label">ยอดรวมทั้งหมด:</span>
                <span class="info-value amount-paid">
                    <asp:Label ID="lblTotalPrice" runat="server"></asp:Label> บาท
                </span>
            </div>

            <div class="info-row">
                <span class="info-label">ชำระแล้ว:</span>
                <span class="info-value amount-paid">
                    <asp:Label ID="lblTotalPaid" runat="server"></asp:Label> บาท
                </span>
            </div>

            <div class="info-row" style="border-bottom: none; margin-top: 10px;">
                <span class="info-label" style="font-size: 18px;">ยอดคงเหลือ:</span>
                <span class="info-value amount-highlight">
                    <asp:Label ID="lblRemainingBalance" runat="server"></asp:Label> บาท
                </span>
            </div>
        </div>

        <!-- Payment Form -->
        <div class="payment-card">
            <div class="card-header">
                <i class="fa fa-money"></i> ข้อมูลการชำระเงิน
            </div>

            <div class="form-group">
                <label class="form-label">จำนวนเงินที่ต้องการชำระ (บาท) <span style="color: red;">*</span></label>
                <asp:TextBox ID="txtPaymentAmount" runat="server" CssClass="form-control" TextMode="Number"
                    min="1" step="0.01" placeholder="กรอกจำนวนเงิน"></asp:TextBox>
                <asp:RequiredFieldValidator ID="rfvAmount" runat="server" ControlToValidate="txtPaymentAmount"
                    ErrorMessage="กรุณากรอกจำนวนเงิน" ForeColor="Red" Display="Dynamic"></asp:RequiredFieldValidator>
                <asp:RangeValidator ID="rvAmount" runat="server" ControlToValidate="txtPaymentAmount"
                    MinimumValue="0.01" MaximumValue="1000000" Type="Double"
                    ErrorMessage="จำนวนเงินไม่ถูกต้อง" ForeColor="Red" Display="Dynamic"></asp:RangeValidator>
            </div>

            <div class="form-group">
                <label class="form-label">วิธีการชำระเงิน <span style="color: red;">*</span></label>
                <asp:DropDownList ID="ddlPaymentMethod" runat="server" CssClass="form-control" DataTextField="Paid_How" DataValueField="ID">
                    <asp:ListItem Value="">-- เลือกวิธีการชำระ --</asp:ListItem>
                </asp:DropDownList>
                <asp:RequiredFieldValidator ID="rfvPaymentMethod" runat="server" ControlToValidate="ddlPaymentMethod"
                    InitialValue="" ErrorMessage="กรุณาเลือกวิธีการชำระเงิน" ForeColor="Red" Display="Dynamic"></asp:RequiredFieldValidator>
            </div>

            <div class="form-group">
                <label class="form-label">อัพโหลดสลิปการโอนเงิน (JPG, PNG, PDF เท่านั้น, ไม่เกิน 5MB)</label>
                <asp:FileUpload ID="fuPaymentSlip" runat="server" CssClass="form-control" />
                <small style="color: #7f8c8d;">หมายเหตุ: หากชำระผ่านโอนเงิน กรุณาอัพโหลดสลิปเพื่อยืนยันการชำระเงิน</small>
            </div>

            <div class="form-group">
                <label class="form-label">หมายเหตุ</label>
                <asp:TextBox ID="txtNotes" runat="server" CssClass="form-control" TextMode="MultiLine"
                    Rows="3" placeholder="ระบุหมายเหตุเพิ่มเติม (ถ้ามี)"></asp:TextBox>
            </div>

            <div class="form-group">
                <asp:Button ID="btnSubmit" runat="server" Text="ชำระเงิน" CssClass="btn-primary" OnClick="btnSubmit_Click" />
                <asp:HyperLink ID="lnkCancel" runat="server" NavigateUrl="~/ReserveTable.aspx" CssClass="btn-secondary">
                    ยกเลิก
                </asp:HyperLink>
            </div>
        </div>

        <!-- ให้ลูกค้าจ่ายเอง (แสดงเฉพาะเมื่อเปิดฟีเจอร์รับชำระเงินออนไลน์) -->
        <asp:Panel ID="pnlOnlineLink" runat="server" CssClass="payment-card" Visible="false">
            <div class="card-header">
                <h3>ให้ลูกค้าชำระเงินเอง</h3>
            </div>
            <div style="padding:15px;">
                <p style="color:#7f8c8d;margin:0 0 10px;">
                    ส่งลิงก์นี้ให้ลูกค้าทาง LINE / SMS — ลูกค้าเลือกได้ว่าจะสแกน QR แนบสลิป หรือจ่ายด้วยบัตรเครดิต
                    เมื่อจ่ายสำเร็จระบบจะบันทึกเข้าการจองนี้ให้อัตโนมัติ
                </p>
                <asp:TextBox ID="txtPayLink" runat="server" ReadOnly="true"
                    style="width:100%;padding:9px 11px;border:1px solid #dbe3de;border-radius:8px;font-family:monospace;font-size:12.5px;" />
                <div style="margin-top:10px;">
                    <asp:HyperLink ID="lnkOpenPay" runat="server" Target="_blank" CssClass="btn-secondary">
                        เปิดหน้าชำระเงิน ↗
                    </asp:HyperLink>
                </div>
            </div>
        </asp:Panel>

        <!-- Payment History -->
        <div class="payment-card">
            <div class="card-header">
                <i class="fa fa-history"></i> ประวัติการชำระเงิน
            </div>

            <asp:GridView ID="gvPaymentHistory" runat="server" CssClass="payment-history-table"
                AutoGenerateColumns="False" EmptyDataText="ยังไม่มีประวัติการชำระเงิน">
                <Columns>
                    <asp:BoundField DataField="PaymentDate" HeaderText="วันที่ชำระ" DataFormatString="{0:dd/MM/yyyy HH:mm}" />
                    <asp:BoundField DataField="PaymentAmount" HeaderText="จำนวนเงิน" DataFormatString="{0:N2}" />
                    <asp:TemplateField HeaderText="ประเภท">
                        <ItemTemplate>
                            <span class="badge badge-info"><%# Eval("PaymentType") %></span>
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:BoundField DataField="PaymentMethod" HeaderText="วิธีชำระ" />
                    <asp:BoundField DataField="Receipt_ID" HeaderText="เลขที่ใบเสร็จ" />
                    <asp:TemplateField HeaderText="สลิป">
                        <ItemTemplate>
                            <%# !string.IsNullOrEmpty(Eval("SlipFileURL")?.ToString()) ?
                                "<a href='" + ResolveUrl("~/") + Eval("SlipFileURL") + "' target='_blank' style='color: #3498db; text-decoration: none;'><i class='fa fa-file-image-o'></i> ดูสลิป</a>" :
                                "<span style='color: #95a5a6;'>-</span>" %>
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="สถานะ">
                        <ItemTemplate>
                            <span class="badge <%# Eval("Status").ToString() == "COMPLETED" ? "badge-success" : "badge-warning" %>">
                                <%# Eval("Status").ToString() == "COMPLETED" ? "สำเร็จ" : "รอดำเนินการ" %>
                            </span>
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:BoundField DataField="RemainingBalance" HeaderText="ยอดคงเหลือ" DataFormatString="{0:N2}" />
                </Columns>
            </asp:GridView>
        </div>
    </div>
</asp:Content>
