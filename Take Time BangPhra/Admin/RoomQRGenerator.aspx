<%@ Page Title="Room QR Code Generator" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RoomQRGenerator.aspx.cs" Inherits="Take_Time_BangPhra.Admin.RoomQRGenerator" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .qr-header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 30px;
            border-radius: 10px 10px 0 0;
            margin: -20px -20px 0 -20px;
        }

        .qr-header h2 {
            margin: 0;
            font-size: 28px;
            font-weight: 600;
        }

        .qr-header p {
            margin: 10px 0 0 0;
            opacity: 0.9;
        }

        .qr-container {
            background: white;
            padding: 30px;
            border-radius: 0 0 10px 10px;
            box-shadow: 0 5px 20px rgba(0,0,0,0.1);
            margin-bottom: 30px;
        }

        .qr-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
            gap: 20px;
            margin-top: 20px;
        }

        .qr-card {
            background: white;
            border: 2px solid #e0e0e0;
            border-radius: 10px;
            padding: 20px;
            text-align: center;
            transition: all 0.3s ease;
        }

        .qr-card:hover {
            transform: translateY(-5px);
            box-shadow: 0 10px 25px rgba(0,0,0,0.15);
            border-color: #667eea;
        }

        .qr-card h4 {
            margin: 0 0 15px 0;
            color: #333;
            font-size: 18px;
            font-weight: 600;
        }

        .qr-code-image {
            width: 200px;
            height: 200px;
            margin: 10px auto;
            padding: 10px;
            background: white;
            border: 2px solid #f0f0f0;
            border-radius: 8px;
        }

        .qr-info {
            background: #f8f9fa;
            padding: 15px;
            border-radius: 8px;
            margin: 15px 0;
            font-size: 12px;
            text-align: left;
        }

        .qr-info div {
            margin: 5px 0;
            color: #666;
        }

        .qr-info strong {
            color: #333;
        }

        .btn-generate {
            background: linear-gradient(135deg, #667eea, #764ba2);
            color: white;
            border: none;
            padding: 12px 30px;
            border-radius: 25px;
            font-weight: 600;
            cursor: pointer;
            transition: all 0.3s ease;
            margin: 5px;
        }

        .btn-generate:hover {
            transform: translateY(-2px);
            box-shadow: 0 5px 15px rgba(102, 126, 234, 0.4);
        }

        .btn-download {
            background: linear-gradient(135deg, #4CAF50, #2E7D32);
            color: white;
            border: none;
            padding: 8px 20px;
            border-radius: 20px;
            font-size: 13px;
            font-weight: 500;
            cursor: pointer;
            text-decoration: none;
            display: inline-block;
            margin: 5px;
            transition: all 0.3s ease;
        }

        .btn-download:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(76, 175, 80, 0.3);
            color: white;
            text-decoration: none;
        }

        .btn-print {
            background: linear-gradient(135deg, #FF9800, #F57C00);
            color: white;
            border: none;
            padding: 8px 20px;
            border-radius: 20px;
            font-size: 13px;
            font-weight: 500;
            cursor: pointer;
            margin: 5px;
        }

        .status-badge {
            display: inline-block;
            padding: 5px 12px;
            border-radius: 12px;
            font-size: 11px;
            font-weight: 600;
            margin: 5px 0;
        }

        .status-active {
            background: #e8f5e9;
            color: #2e7d32;
        }

        .status-inactive {
            background: #ffebee;
            color: #c62828;
        }

        .alert {
            padding: 15px 20px;
            border-radius: 8px;
            margin: 20px 0;
            display: none;
        }

        .alert-success {
            background: #e8f5e9;
            color: #2e7d32;
            border-left: 4px solid #4CAF50;
        }

        .alert-error {
            background: #ffebee;
            color: #c62828;
            border-left: 4px solid #f44336;
        }

        .action-buttons {
            margin: 20px 0;
        }

        @media print {
            .no-print {
                display: none !important;
            }

            .qr-card {
                break-inside: avoid;
                page-break-inside: avoid;
            }
        }
    </style>

    <div class="qr-header no-print">
        <h2><i class="fas fa-qrcode"></i> Room QR Code Generator</h2>
        <p>Generate and manage QR codes for guest portal access</p>
    </div>

    <div class="qr-container">
        <!-- Alert Messages -->
        <asp:Panel ID="pnlAlert" runat="server" Visible="false" CssClass="alert">
            <asp:Label ID="lblAlert" runat="server"></asp:Label>
        </asp:Panel>

        <!-- Action Buttons -->
        <div class="action-buttons no-print">
            <asp:Button ID="btnGenerateAll" runat="server" Text="Generate All QR Codes"
                CssClass="btn-generate" OnClick="btnGenerateAll_Click" />
            <asp:Button ID="btnRefresh" runat="server" Text="Refresh"
                CssClass="btn-generate" OnClick="btnRefresh_Click" />
            <button type="button" class="btn-print" onclick="window.print()">
                <i class="fas fa-print"></i> Print All QR Codes
            </button>
        </div>

        <!-- QR Codes Grid -->
        <div class="qr-grid">
            <asp:Repeater ID="rptQRCodes" runat="server" OnItemCommand="rptQRCodes_ItemCommand">
                <ItemTemplate>
                    <div class="qr-card">
                        <h4><%# Eval("Accommodation_Name") %></h4>

                        <div class="qr-code-image">
                            <asp:Image ID="imgQR" runat="server"
                                ImageUrl='<%# GenerateQRCodeImage(Eval("QR_Data").ToString()) %>'
                                Width="180px" Height="180px" />
                        </div>

                        <div class="qr-info no-print">
                            <div><strong>Token:</strong> <%# Eval("QR_Token").ToString().Substring(0, 20) %>...</div>
                            <div><strong>Generated:</strong> <%# Eval("Generated_Date", "{0:dd MMM yyyy}") %></div>
                            <div><strong>Scans:</strong> <%# Eval("Scan_Count") %></div>
                            <div>
                                <span class='status-badge <%# Convert.ToBoolean(Eval("Is_Active")) ? "status-active" : "status-inactive" %>'>
                                    <%# Convert.ToBoolean(Eval("Is_Active")) ? "Active" : "Inactive" %>
                                </span>
                            </div>
                        </div>

                        <div class="no-print">
                            <asp:LinkButton runat="server" CommandName="Regenerate"
                                CommandArgument='<%# Eval("Accommodation_ID") %>'
                                CssClass="btn-generate">
                                <i class="fas fa-sync-alt"></i> Regenerate
                            </asp:LinkButton>
                            <a href='<%# GenerateQRCodeImage(Eval("QR_Data").ToString()) %>'
                               download='QR_<%# Eval("Accommodation_Name") %>.png'
                               class="btn-download">
                                <i class="fas fa-download"></i> Download
                            </a>
                        </div>

                        <!-- Print-only: Room name below QR -->
                        <div class="print-only" style="margin-top: 10px; font-size: 14px; font-weight: bold;">
                            <%# Eval("Accommodation_Name") %>
                        </div>
                    </div>
                </ItemTemplate>
            </asp:Repeater>
        </div>

        <!-- No Data Message -->
        <asp:Label ID="lblNoData" runat="server" Visible="false"
            Text="No QR codes found. Click 'Generate All QR Codes' to create them."
            CssClass="alert alert-error"></asp:Label>
    </div>

    <script>
        // Show alert if exists
        window.addEventListener('load', function() {
            var alert = document.querySelector('.alert');
            if (alert && alert.style.display !== 'none') {
                alert.style.display = 'block';
                setTimeout(function() {
                    alert.style.display = 'none';
                }, 5000);
            }
        });
    </script>
</asp:Content>
