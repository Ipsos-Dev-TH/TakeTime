<%@ Page Title="รายงานระบบ" Language="C#" MaintainScrollPositionOnPostback="true"
    MasterPageFile="~/Site.Master" AutoEventWireup="true"
    CodeBehind="Report.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Report.ReportMain" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <div class="report-container">
        <div class="page-header">
            <h1><i class="fas fa-chart-bar"></i> ระบบรายงาน</h1>
            <p class="text-muted">จัดการและดูรายงานต่างๆ ของระบบ</p>
        </div>

        <asp:UpdatePanel ID="updatePanel" runat="server" UpdateMode="Conditional">
            <ContentTemplate>
                <!-- Alert Messages -->
                <div id="alertMessage" runat="server" class="alert alert-danger" visible="false" role="alert">
                    <asp:Literal ID="litAlertMessage" runat="server"></asp:Literal>
                </div>

                <div class="card report-controls">
                    <div class="card-header">
                        <h5><i class="fas fa-filter"></i> ตัวกรองรายงาน</h5>
                    </div>
                    <div class="card-body">
                        <div class="row mb-3">
                            <div class="col-md-6">
                                <label class="form-label">เลือกประเภทรายงาน</label>
                                <asp:DropDownList ID="ddlReportType" runat="server" CssClass="form-select" 
                                    AutoPostBack="true" OnSelectedIndexChanged="ddlReportType_SelectedIndexChanged">
                                    <asp:ListItem Value="">--- กรุณาเลือกรายงาน ---</asp:ListItem>
                                    <asp:ListItem Value="1">📋 รายการจองล่าสุด</asp:ListItem>
                                    <asp:ListItem Value="2">📊 รายงานการเช่าอุปกรณ์รายเดือน</asp:ListItem>
                                    <asp:ListItem Value="3">🏨 รายงานการจองห้องพัก</asp:ListItem>
                                    <asp:ListItem Value="4">📈 รายงานอัตราการจองห้องพัก</asp:ListItem>
                                    <asp:ListItem Value="5">🛍️ รายงานขายของตามการขาย</asp:ListItem>
                                    <asp:ListItem Value="6">💰 รายงานขายของแบบสรุป</asp:ListItem>
                                    <asp:ListItem Value="7">🤝 การจองโดย Affiliate</asp:ListItem>
                                </asp:DropDownList>
                            </div>
                            <div class="col-md-6 d-flex align-items-end">
                                <asp:Button ID="btnExport" runat="server" Text="📥 Export to Excel" 
                                    CssClass="btn btn-success me-2" OnClick="btnExport_Click" Visible="false" />
                                <asp:Button ID="btnPrint" runat="server" Text="🖨️ Print" 
                                    CssClass="btn btn-info" OnClientClick="window.print(); return false;" Visible="false" />
                            </div>
                        </div>

                        <div id="dateRangeSection" runat="server" class="row mb-3" visible="false">
                            <div class="col-md-3">
                                <label class="form-label">วันที่เริ่มต้น</label>
                                <asp:TextBox ID="txtStartDate" runat="server" CssClass="form-control" 
                                    TextMode="Date" AutoPostBack="true" OnTextChanged="txtStartDate_TextChanged"></asp:TextBox>
                                <small class="form-text text-muted">รูปแบบ: YYYY-MM-DD</small>
                            </div>
                            <div class="col-md-3">
                                <label class="form-label">วันที่สิ้นสุด</label>
                                <asp:TextBox ID="txtEndDate" runat="server" CssClass="form-control" 
                                    TextMode="Date"></asp:TextBox>
                                <small class="form-text text-muted">รูปแบบ: YYYY-MM-DD</small>
                            </div>
                            <div class="col-md-3 d-flex align-items-end">
                                <asp:Button ID="btnGenerateReport" runat="server" Text="สร้างรายงาน" 
                                    CssClass="btn btn-primary me-2" OnClick="btnGenerateReport_Click" />
                                <asp:Button ID="btnClear" runat="server" Text="ล้าง" 
                                    CssClass="btn btn-secondary" OnClick="btnClear_Click" />
                            </div>
                            <div class="col-md-3 d-flex align-items-end">
                                <div class="form-text">
                                    <asp:Label ID="lblDateInfo" runat="server" CssClass="text-info"></asp:Label>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- สรุปผลลัพธ์ -->
                <!-- สรุปผลลัพธ์ -->
<div id="summarySection" runat="server" class="row mt-4" visible="false">
    <div class="col-12">
        <div class="card">
            <div class="card-header bg-primary text-white d-flex justify-content-between align-items-center">
                <h5 class="mb-0"><i class="fas fa-chart-pie"></i> สรุปผลรายงาน</h5>
                <asp:Label ID="lblTotalRevenue" runat="server" CssClass="badge bg-light text-dark fs-6"></asp:Label>
            </div>
            <div class="card-body">
                <div class="row">
                    <div class="col-md-6 col-lg-4 mb-3">
                        <div class="summary-card summary-dome">
                            <asp:Label ID="lblSummary1" runat="server" CssClass="summary-item"></asp:Label>
                        </div>
                    </div>
                    <div class="col-md-6 col-lg-4 mb-3">
                        <div class="summary-card summary-room">
                            <asp:Label ID="lblSummary2" runat="server" CssClass="summary-item"></asp:Label>
                        </div>
                    </div>
                    <div class="col-md-6 col-lg-4 mb-3">
                        <div class="summary-card summary-villa">
                            <asp:Label ID="lblSummary3" runat="server" CssClass="summary-item"></asp:Label>
                        </div>
                    </div>
                    <div class="col-md-6 col-lg-4 mb-3">
                        <div class="summary-card summary-tent">
                            <asp:Label ID="lblSummary4" runat="server" CssClass="summary-item"></asp:Label>
                        </div>
                    </div>
                    <div class="col-md-6 col-lg-4 mb-3">
                        <div class="summary-card summary-camping">
                            <asp:Label ID="lblSummary5" runat="server" CssClass="summary-item"></asp:Label>
                        </div>
                    </div>
                    <div class="col-md-6 col-lg-4 mb-3">
                        <div class="summary-card summary-cabin">
                            <asp:Label ID="lblSummary6" runat="server" CssClass="summary-item"></asp:Label>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </div>
</div>

                <!-- ตารางแสดงผล -->
                <div class="card mt-4">
                    <div class="card-header d-flex justify-content-between align-items-center bg-dark text-white">
                        <h5><i class="fas fa-table"></i> ผลลัพธ์รายงาน 
                            <small class="text-light">
                                <asp:Label ID="lblRecordCount" runat="server" Text=""></asp:Label>
                            </small>
                        </h5>
                        <div>
                            <asp:Label ID="lblReportTitle" runat="server" CssClass="badge bg-info fs-6"></asp:Label>
                        </div>
                    </div>
                    <div class="card-body">
                        <div class="table-responsive" style="max-height: 600px; overflow-y: auto;">
                            <asp:GridView ID="gvReport" runat="server" 
                                CssClass="table table-striped table-bordered table-hover table-sm"
                                AutoGenerateColumns="true" 
                                EmptyDataText="<div class='text-center py-4'><i class='fas fa-inbox fa-3x text-muted mb-3'></i><br><span class='text-muted'>ไม่พบข้อมูล</span></div>"
                                OnRowDataBound="gvReport_RowDataBound"
                                ShowHeaderWhenEmpty="true"
                                AllowSorting="false"
                                GridLines="None">
                                <HeaderStyle CssClass="table-dark" />
                                <RowStyle CssClass="align-middle" />
                                <AlternatingRowStyle CssClass="align-middle" />
                            </asp:GridView>
                        </div>
                    </div>
                </div>
            </ContentTemplate>
            <Triggers>
                <asp:PostBackTrigger ControlID="btnExport" />
                <asp:AsyncPostBackTrigger ControlID="ddlReportType" EventName="SelectedIndexChanged" />
                <asp:AsyncPostBackTrigger ControlID="txtStartDate" EventName="TextChanged" />
                <asp:AsyncPostBackTrigger ControlID="btnGenerateReport" EventName="Click" />
                <asp:AsyncPostBackTrigger ControlID="btnClear" EventName="Click" />
            </Triggers>
        </asp:UpdatePanel>

        <!-- Loading Indicator -->
        <asp:UpdateProgress ID="updateProgress" runat="server" AssociatedUpdatePanelID="updatePanel" DisplayAfter="100">
            <ProgressTemplate>
                <div class="loading-overlay">
                    <div class="loading-content">
                        <div class="spinner-border text-primary" style="width: 3rem; height: 3rem;" role="status">
                            <span class="visually-hidden">กำลังโหลด...</span>
                        </div>
                        <div class="loading-text mt-3">กำลังสร้างรายงาน...</div>
                    </div>
                </div>
            </ProgressTemplate>
        </asp:UpdateProgress>
    </div>

    <style>
       .summary-card {
    background: linear-gradient(135deg, #f8f9fa 0%, #e9ecef 100%);
    border-radius: 10px;
    padding: 15px;
    margin: 5px 0;
    border-left: 5px solid #007bff;
    box-shadow: 0 3px 6px rgba(0,0,0,0.1);
    min-height: 80px;
    display: flex;
    align-items: center;
    transition: transform 0.2s ease, box-shadow 0.2s ease;
}

.summary-card:hover {
    transform: translateY(-2px);
    box-shadow: 0 5px 15px rgba(0,0,0,0.15);
}

.summary-item {
    display: block;
    font-weight: 600;
    color: #495057;
    font-size: 14px;
    line-height: 1.4;
}

/* สีสำหรับประเภทต่างๆ */
.summary-dome { 
    border-left-color: #28a745;
    background: linear-gradient(135deg, #f0fff4 0%, #e6ffed 100%);
}
.summary-room { 
    border-left-color: #17a2b8;
    background: linear-gradient(135deg, #f0fdff 0%, #e6f7ff 100%);
}
.summary-villa { 
    border-left-color: #ffc107;
    background: linear-gradient(135deg, #fffdf0 0%, #fff9e6 100%);
}
.summary-tent { 
    border-left-color: #6f42c1;
    background: linear-gradient(135deg, #f8f0ff 0%, #f2e6ff 100%);
}
.summary-camping { 
    border-left-color: #e83e8c;
    background: linear-gradient(135deg, #fff0f7 0%, #ffe6f2 100%);
}
.summary-cabin { 
    border-left-color: #fd7e14;
    background: linear-gradient(135deg, #fff4e6 0%, #ffe8cc 100%);
}

/* สำหรับหน้าจอขนาดเล็ก */
@media (max-width: 768px) {
    .summary-card {
        padding: 12px;
        min-height: 70px;
    }
    
    .summary-item {
        font-size: 13px;
    }
}

/* ไอคอนและข้อความ */
.summary-icon {
    font-size: 18px;
    margin-right: 8px;
}

.summary-amount {
    font-weight: 700;
    color: #2c3e50;
    font-size: 15px;
}

/* สำหรับการแสดงผลแบบหลายบรรทัด */
.multi-line-summary {
    line-height: 1.6;
    font-size: 12px;
}

/* สีสำหรับประเภทต่างๆ */
.summary-dome { border-left-color: #28a745; }
.summary-room { border-left-color: #17a2b8; }
.summary-villa { border-left-color: #ffc107; }
.summary-tent { border-left-color: #6f42c1; }
.summary-camping { border-left-color: #e83e8c; }
.summary-cabin { border-left-color: #fd7e14; }
.summary-total { 
    border-left-color: #dc3545; 
    background: linear-gradient(135deg, #fff5f5 0%, #fed7d7 100%);
    font-weight: 700;
}
        .report-container {
            max-width: 1400px;
            margin: 0 auto;
            padding: 20px;
        }
        .page-header {
            margin-bottom: 30px;
            border-bottom: 2px solid #dee2e6;
            padding-bottom: 15px;
        }
        .summary-card {
            background: linear-gradient(135deg, #f8f9fa 0%, #e9ecef 100%);
            border-radius: 8px;
            padding: 15px;
            margin: 5px 0;
            border-left: 4px solid #007bff;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }
        .summary-item {
            display: block;
            font-weight: 600;
            color: #495057;
            font-size: 14px;
        }
        .loading-overlay {
            position: fixed;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            background: rgba(0,0,0,0.7);
            display: flex;
            flex-direction: column;
            justify-content: center;
            align-items: center;
            z-index: 9999;
        }
        .loading-content {
            background: white;
            padding: 30px;
            border-radius: 10px;
            text-align: center;
            box-shadow: 0 4px 6px rgba(0,0,0,0.1);
        }
        .loading-text {
            color: #495057;
            font-size: 16px;
            font-weight: 500;
        }
        .table th {
            background-color: #343a40 !important;
            color: white;
            position: sticky;
            top: 0;
            z-index: 10;
        }
        .table-responsive {
            border: 1px solid #dee2e6;
            border-radius: 5px;
        }
        .badge {
            font-size: 0.9em;
        }
        
        /* Print Styles */
        @media print {
            .report-controls, .btn, .alert, .loading-overlay {
                display: none !important;
            }
            .card-header {
                background-color: #000 !important;
                color: #000 !important;
                -webkit-print-color-adjust: exact;
            }
            .table th {
                background-color: #000 !important;
                color: #fff !important;
                -webkit-print-color-adjust: exact;
            }
        }
    </style>
</asp:Content>