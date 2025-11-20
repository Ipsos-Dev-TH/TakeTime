<%@ Page Title="ตรวจสอบสลิปการชำระเงิน" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="SlipVerification.aspx.cs" Inherits="Take_Time_BangPhra.Account.SlipVerification" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .slip-verification-container {
            max-width: 98%;
            margin: 20px auto;
            padding: 20px;
            background: linear-gradient(135deg, #EFEBE9 0%, #F5F5F5 100%);
            border-radius: 10px;
        }

        .page-header {
            background: linear-gradient(135deg, #5D4037 0%, #795548 100%);
            color: white;
            padding: 20px;
            border-radius: 8px;
            margin-bottom: 25px;
            box-shadow: 0 4px 8px rgba(0,0,0,0.1);
        }

        .page-header h2 {
            margin: 0;
            font-size: 24px;
            font-weight: 600;
        }

        .filter-panel {
            background: white;
            padding: 20px;
            border-radius: 8px;
            margin-bottom: 20px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.05);
        }

        .filter-row {
            display: flex;
            gap: 15px;
            align-items: center;
            flex-wrap: wrap;
            margin-bottom: 10px;
        }

        .filter-group {
            display: flex;
            flex-direction: column;
            gap: 5px;
        }

        .filter-label {
            font-weight: 600;
            color: #5D4037;
            font-size: 13px;
        }

        .filter-control {
            padding: 8px 12px;
            border: 1px solid #D7CCC8;
            border-radius: 5px;
            font-size: 14px;
        }

        .slip-grid {
            background: white;
            border-radius: 8px;
            padding: 15px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.05);
        }

        .slip-table {
            width: 100%;
            border-collapse: collapse;
        }

        .slip-table th {
            background-color: #5D4037;
            color: white;
            padding: 12px 10px;
            text-align: left;
            font-weight: 600;
            border: 1px solid #4E342E;
            font-size: 13px;
        }

        .slip-table td {
            padding: 10px;
            border: 1px solid #D7CCC8;
            font-size: 13px;
        }

        .slip-table tr:nth-child(even) {
            background-color: #F5F5F5;
        }

        .slip-table tr:hover {
            background-color: #EFEBE9;
        }

        .slip-image-thumbnail {
            width: 80px;
            height: 80px;
            object-fit: cover;
            border-radius: 5px;
            cursor: pointer;
            border: 2px solid #D7CCC8;
            transition: transform 0.2s;
        }

        .slip-image-thumbnail:hover {
            transform: scale(1.1);
            border-color: #5D4037;
        }

        .status-badge {
            padding: 4px 10px;
            border-radius: 12px;
            font-size: 11px;
            font-weight: 600;
            display: inline-block;
        }

        .status-pending {
            background-color: #FFF3E0;
            color: #E65100;
        }

        .status-success {
            background-color: #E8F5E9;
            color: #2E7D32;
        }

        .status-failed {
            background-color: #FFEBEE;
            color: #C62828;
        }

        .status-manual-review {
            background-color: #FFF9C4;
            color: #F57F17;
        }

        .status-approved {
            background-color: #C8E6C9;
            color: #1B5E20;
        }

        .status-rejected {
            background-color: #FFCDD2;
            color: #B71C1C;
        }

        .action-buttons {
            display: flex;
            gap: 5px;
        }

        .btn-approve {
            background-color: #4CAF50;
            color: white;
            border: none;
            padding: 6px 12px;
            border-radius: 4px;
            cursor: pointer;
            font-size: 12px;
            font-weight: 600;
        }

        .btn-approve:hover {
            background-color: #45A049;
        }

        .btn-reject {
            background-color: #F44336;
            color: white;
            border: none;
            padding: 6px 12px;
            border-radius: 4px;
            cursor: pointer;
            font-size: 12px;
            font-weight: 600;
        }

        .btn-reject:hover {
            background-color: #DA190B;
        }

        .btn-view {
            background-color: #2196F3;
            color: white;
            border: none;
            padding: 6px 12px;
            border-radius: 4px;
            cursor: pointer;
            font-size: 12px;
            font-weight: 600;
        }

        .btn-view:hover {
            background-color: #0B7DDA;
        }

        .summary-cards {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 15px;
            margin-bottom: 20px;
        }

        .summary-card {
            background: white;
            padding: 15px;
            border-radius: 8px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.05);
            border-left: 4px solid;
        }

        .summary-card.pending {
            border-left-color: #FF9800;
        }

        .summary-card.success {
            border-left-color: #4CAF50;
        }

        .summary-card.failed {
            border-left-color: #F44336;
        }

        .summary-card.manual {
            border-left-color: #FFC107;
        }

        .summary-card-title {
            font-size: 13px;
            color: #757575;
            margin-bottom: 5px;
        }

        .summary-card-value {
            font-size: 28px;
            font-weight: 700;
            color: #5D4037;
        }

        .confidence-bar {
            display: inline-block;
            width: 60px;
            height: 8px;
            background-color: #E0E0E0;
            border-radius: 4px;
            overflow: hidden;
            vertical-align: middle;
        }

        .confidence-fill {
            height: 100%;
            background: linear-gradient(90deg, #F44336 0%, #FFC107 50%, #4CAF50 100%);
        }

        .amount-match {
            color: #4CAF50;
            font-weight: 600;
        }

        .amount-mismatch {
            color: #F44336;
            font-weight: 600;
        }

        /* Modal for slip preview */
        .modal {
            display: none;
            position: fixed;
            z-index: 1000;
            left: 0;
            top: 0;
            width: 100%;
            height: 100%;
            background-color: rgba(0,0,0,0.9);
        }

        .modal-content {
            margin: auto;
            display: block;
            max-width: 90%;
            max-height: 90%;
            margin-top: 2%;
        }

        .modal-close {
            position: absolute;
            top: 20px;
            right: 35px;
            color: #f1f1f1;
            font-size: 40px;
            font-weight: bold;
            cursor: pointer;
        }
    </style>

    <div class="slip-verification-container">
        <!-- Page Header -->
        <div class="page-header">
            <h2>📋 ตรวจสอบสลิปการชำระเงิน (OCR Verification)</h2>
            <p style="margin: 5px 0 0 0; font-size: 14px; opacity: 0.9;">
                ระบบตรวจสอบสลิปอัตโนมัติด้วย OCR | กรณีที่ OCR อ่านไม่ได้หรือยอดไม่ตรง กรุณาตรวจสอบด้วยตนเอง
            </p>
        </div>

        <!-- Summary Cards -->
        <div class="summary-cards">
            <div class="summary-card pending">
                <div class="summary-card-title">รอตรวจสอบ</div>
                <div class="summary-card-value">
                    <asp:Label ID="lblPendingCount" runat="server" Text="0"></asp:Label>
                </div>
            </div>
            <div class="summary-card success">
                <div class="summary-card-title">OCR สำเร็จ</div>
                <div class="summary-card-value">
                    <asp:Label ID="lblSuccessCount" runat="server" Text="0"></asp:Label>
                </div>
            </div>
            <div class="summary-card failed">
                <div class="summary-card-title">OCR ล้มเหลว</div>
                <div class="summary-card-value">
                    <asp:Label ID="lblFailedCount" runat="server" Text="0"></asp:Label>
                </div>
            </div>
            <div class="summary-card manual">
                <div class="summary-card-title">ต้องตรวจสอบด้วยตนเอง</div>
                <div class="summary-card-value">
                    <asp:Label ID="lblManualReviewCount" runat="server" Text="0"></asp:Label>
                </div>
            </div>
        </div>

        <!-- Filter Panel -->
        <div class="filter-panel">
            <div class="filter-row">
                <div class="filter-group">
                    <label class="filter-label">สถานะ OCR:</label>
                    <asp:DropDownList ID="ddlOCRStatus" runat="server" CssClass="filter-control" AutoPostBack="True" OnSelectedIndexChanged="ApplyFilter">
                        <asp:ListItem Value="" Selected="True">ทั้งหมด</asp:ListItem>
                        <asp:ListItem Value="PENDING">รอดำเนินการ</asp:ListItem>
                        <asp:ListItem Value="SUCCESS">สำเร็จ</asp:ListItem>
                        <asp:ListItem Value="FAILED">ล้มเหลว</asp:ListItem>
                        <asp:ListItem Value="MANUAL_REVIEW">ต้องตรวจสอบ</asp:ListItem>
                    </asp:DropDownList>
                </div>

                <div class="filter-group">
                    <label class="filter-label">สถานะการตรวจสอบ:</label>
                    <asp:DropDownList ID="ddlVerificationStatus" runat="server" CssClass="filter-control" AutoPostBack="True" OnSelectedIndexChanged="ApplyFilter">
                        <asp:ListItem Value="" Selected="True">ทั้งหมด</asp:ListItem>
                        <asp:ListItem Value="PENDING">รอตรวจสอบ</asp:ListItem>
                        <asp:ListItem Value="APPROVED">อนุมัติแล้ว</asp:ListItem>
                        <asp:ListItem Value="REJECTED">ปฏิเสธแล้ว</asp:ListItem>
                    </asp:DropDownList>
                </div>

                <div class="filter-group">
                    <label class="filter-label">วันที่เริ่มต้น:</label>
                    <asp:TextBox ID="txtStartDate" runat="server" TextMode="Date" CssClass="filter-control"></asp:TextBox>
                </div>

                <div class="filter-group">
                    <label class="filter-label">วันที่สิ้นสุด:</label>
                    <asp:TextBox ID="txtEndDate" runat="server" TextMode="Date" CssClass="filter-control"></asp:TextBox>
                </div>

                <div class="filter-group" style="padding-top: 20px;">
                    <asp:Button ID="btnSearch" runat="server" Text="ค้นหา" CssClass="btn-view" OnClick="ApplyFilter" />
                </div>
            </div>
        </div>

        <!-- Slips Grid -->
        <div class="slip-grid">
            <asp:GridView ID="gvSlips" runat="server" CssClass="slip-table"
                AutoGenerateColumns="False"
                EmptyDataText="ไม่พบรายการสลิป"
                OnRowCommand="gvSlips_RowCommand"
                DataKeyNames="SlipID">
                <Columns>
                    <asp:TemplateField HeaderText="รูปสลิป">
                        <ItemTemplate>
                            <img src='<%# ResolveUrl(Eval("SlipFileURL").ToString()) %>'
                                class="slip-image-thumbnail"
                                onclick='showSlipModal("<%# ResolveUrl(Eval("SlipFileURL").ToString()) %>")' />
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:BoundField DataField="SlipID" HeaderText="ID" />
                    <asp:BoundField DataField="ReservationID" HeaderText="รหัสจอง" />
                    <asp:BoundField DataField="CustomerName" HeaderText="ชื่อลูกค้า" />
                    <asp:BoundField DataField="CustomerPhone" HeaderText="เบอร์โทร" />
                    <asp:BoundField DataField="UploadedDate" HeaderText="วันที่อัพโหลด" DataFormatString="{0:dd/MM/yyyy HH:mm}" />

                    <asp:TemplateField HeaderText="ยอดที่ชำระ (บาท)">
                        <ItemTemplate>
                            <%# string.Format("{0:N2}", Eval("PaymentAmount")) %>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="OCR อ่านได้ (บาท)">
                        <ItemTemplate>
                            <%# Eval("OCR_Amount") != DBNull.Value
                                ? string.Format("{0:N2}", Eval("OCR_Amount"))
                                : "<span style='color: #999;'>-</span>" %>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="ความมั่นใจ">
                        <ItemTemplate>
                            <%# Eval("OCR_Confidence") != DBNull.Value
                                ? string.Format("<div class='confidence-bar'><div class='confidence-fill' style='width: {0}%'></div></div> {1:N0}%", Eval("OCR_Confidence"), Eval("OCR_Confidence"))
                                : "<span style='color: #999;'>-</span>" %>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="สถานะ OCR">
                        <ItemTemplate>
                            <span class='status-badge status-<%# Eval("OCR_Status").ToString().ToLower().Replace("_", "-") %>'>
                                <%# GetOCRStatusText(Eval("OCR_Status").ToString()) %>
                            </span>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="สถานะตรวจสอบ">
                        <ItemTemplate>
                            <span class='status-badge status-<%# Eval("VerificationStatus").ToString().ToLower() %>'>
                                <%# GetVerificationStatusText(Eval("VerificationStatus").ToString()) %>
                            </span>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="ตรวจสอบโดย">
                        <ItemTemplate>
                            <%# Eval("VerifiedByName") ?? "<span style='color: #999;'>-</span>" %>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="จัดการ">
                        <ItemTemplate>
                            <div class="action-buttons">
                                <asp:Button runat="server"
                                    CommandName="ApproveSlip"
                                    CommandArgument='<%# Eval("SlipID") %>'
                                    Text="✓ อนุมัติ"
                                    CssClass="btn-approve"
                                    Visible='<%# Eval("VerificationStatus").ToString() == "PENDING" %>'
                                    OnClientClick="return confirm('ยืนยันการอนุมัติสลิปนี้?');" />

                                <asp:Button runat="server"
                                    CommandName="RejectSlip"
                                    CommandArgument='<%# Eval("SlipID") %>'
                                    Text="✗ ปฏิเสธ"
                                    CssClass="btn-reject"
                                    Visible='<%# Eval("VerificationStatus").ToString() == "PENDING" %>'
                                    OnClientClick="return confirm('ยืนยันการปฏิเสธสลิปนี้? กรุณาระบุเหตุผล');" />
                            </div>
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
            </asp:GridView>
        </div>

        <!-- Message Label -->
        <div style="margin-top: 15px; text-align: center;">
            <asp:Label ID="lblMessage" runat="server" Text="" ForeColor="Green" Font-Bold="True"></asp:Label>
        </div>
    </div>

    <!-- Image Modal -->
    <div id="slipModal" class="modal" onclick="this.style.display='none'">
        <span class="modal-close">&times;</span>
        <img class="modal-content" id="modalImage">
    </div>

    <script>
        function showSlipModal(imageUrl) {
            document.getElementById('slipModal').style.display = 'block';
            document.getElementById('modalImage').src = imageUrl;
        }
    </script>
</asp:Content>
