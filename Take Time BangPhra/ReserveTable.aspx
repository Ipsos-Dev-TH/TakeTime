<%@ Page Title="รายการผู้เข้าพักรายวัน" Language="C#" Async="True" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ReserveTable.aspx.cs" Inherits="Take_Time_BangPhra.ReserveTable" %>
<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">
    <link rel="stylesheet" href="/Content/jquery-ui.css">
    <link rel="stylesheet" href="/Content/style.css">
    <link rel="stylesheet" type="text/css" href="/Content/GridView2.css">
    
    <style type="text/css">
        /* Page Background */
        body {
            background: linear-gradient(135deg, #f5f5f5 0%, #e8e8e8 100%) !important;
            min-height: 100vh;
        }

        .wrap { white-space: normal; width: 100px; }
        th, td { padding: 8px; vertical-align: top; }

        /* 🎨 Header styles - สำหรับ header เท่านั้น */
        th.header-center {
            text-align: center;
            background: linear-gradient(135deg, #6d4c41 0%, #8d6e63 100%);
            color: white;
            font-weight: bold;
        }

        /* 📝 Cell center style - สำหรับเซลล์ปกติ (ไม่ใช่ header) */
        td.header-center {
            text-align: center;
            color: #2c3e50;
            font-weight: normal;
            background: transparent;
        }

        .header-right { text-align: right; }
        .print-only { display: none; }
        .no-print { display: block; }
        .hidden { display: none; }

        /* 📝 ล็อคความกว้างช่องหมายเหตุและตัดข้อความมาบรรทัดใหม่ */
        .remark-column {
            max-width: 150px;
            word-wrap: break-word;
            word-break: break-word;
            white-space: normal;
            overflow-wrap: break-word;
            vertical-align: top;
            font-size: 0.9em;
            line-height: 1.4;
        }

        /* แสดงห้องพักคนละบรรทัด */
        .room-list {
            white-space: pre-line;
            vertical-align: top;
            font-size: 0.9em;
            line-height: 1.5;
            max-width: 200px;
        }

        /* ปรับ GridView ให้สวยงามขึ้น */
        .mydatagrid {
            border-collapse: collapse;
            width: 100%;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            border-radius: 8px;
            overflow: hidden;
        }

        .mydatagrid th {
            padding: 12px 8px;
            border: 1px solid #d7ccc8;
        }

        .mydatagrid td {
            padding: 8px;
            border: 1px solid #e0e0e0;
            background: white;
        }

        .mydatagrid tr:nth-child(even) td {
            background: #fafafa;
        }

        .mydatagrid tr:hover td {
            background: #fff3e0;
            transition: background 0.2s ease;
        }

        /* ปรับรายการของเช่า/สินค้า */
        .items-column {
            max-width: 200px;
            word-wrap: break-word;
            white-space: normal;
            font-size: 0.9em;
            line-height: 1.4;
            vertical-align: top;
        }

        /* ชื่อผู้จอง */
        .name-column {
            max-width: 200px;
            word-wrap: break-word;
            white-space: normal;
            vertical-align: top;
        }
        
        @media print {
            @page {
                margin: 0.2cm;
                size: landscape;
            }
            body {
                margin: 0.2cm;
                padding: 0;
            }
            .no-print { display: none !important; }
            .print-only { display: table-cell !important; }
            .print-header { display: table-cell !important; }
            .jumbotron { padding: 10px !important; margin: 0 !important; }
            .mydatagrid {
                border: 1px solid #000 !important;
                width: 100%;
                border-collapse: collapse;
            }
            .mydatagrid th, .mydatagrid td {
                border: 1px solid #000 !important;
                padding: 3px !important;
                vertical-align: top;
            }
            .mydatagrid td {
                height: 74px !important;
            }
            /* แสดงห้องพักคนละบรรทัดตอน print */
            .room-list {
                white-space: pre-line !important;
            }
            /* ปรับความกว้างคอลัมน์สำหรับ print */
            .mydatagrid th:nth-child(1), .mydatagrid td:nth-child(1) { width: 3%; }
            .mydatagrid th:nth-child(2), .mydatagrid td:nth-child(2) { width: 14%; }
            .mydatagrid th:nth-child(3), .mydatagrid td:nth-child(3) { width: 10%; } /* รายชื่อห้องพัก - แคบลง */
            .mydatagrid th:nth-child(4), .mydatagrid td:nth-child(4) { width: 3%; }
            .mydatagrid th:nth-child(5), .mydatagrid td:nth-child(5) { width: 24%; } /* รายการของเช่า/สินค้า - กว้างขึ้น */
            .mydatagrid th:nth-child(6), .mydatagrid td:nth-child(6) { width: 4%; }
            .mydatagrid th:nth-child(7), .mydatagrid td:nth-child(7) { width: 4%; }
            .mydatagrid th:nth-child(8), .mydatagrid td:nth-child(8) { width: 4%; }
            .mydatagrid th:nth-child(9), .mydatagrid td:nth-child(9) { width: 10%; } /* หมายเหตุ - แคบลง */

            /* ตัดข้อความหมายเหตุมาบรรทัดใหม่ตอน print */
            .remark-column {
                word-wrap: break-word !important;
                word-break: break-word !important;
                white-space: normal !important;
            }

            /* ตัดข้อความรายการของเช่า/สินค้าตอน print */
            .items-column {
                word-wrap: break-word !important;
                word-break: break-word !important;
                white-space: normal !important;
            }
        }

        /* ปรับ Action Buttons ให้สวยงาม */
        .action-buttons {
            margin: 15px 0;
            padding: 10px;
            background: linear-gradient(135deg, #f5f5f5 0%, #e0e0e0 100%);
            border-radius: 8px;
        }

        .action-buttons .btn-primary {
            background: linear-gradient(135deg, #1976d2 0%, #2196f3 100%);
            border: none;
            padding: 10px 25px;
            font-weight: bold;
            box-shadow: 0 2px 5px rgba(0,0,0,0.2);
            transition: all 0.3s ease;
        }

        .action-buttons .btn-primary:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 8px rgba(0,0,0,0.3);
        }

        /* 🎨 Bootstrap-style button colors - เพิ่มสีปุ่มให้ชัดเจน */
        .btn {
            display: inline-block;
            padding: 6px 12px;
            margin-bottom: 0;
            font-size: 14px;
            font-weight: 600;
            line-height: 1.42857143;
            text-align: center;
            white-space: nowrap;
            vertical-align: middle;
            cursor: pointer;
            border: 1px solid transparent;
            border-radius: 4px;
            text-decoration: none;
            transition: all 0.15s ease-in-out;
        }

        /* Primary Button - สีน้ำเงิน */
        .btn-primary {
            color: white;
            background: linear-gradient(135deg, #007bff 0%, #0056b3 100%);
            border-color: #007bff;
        }

        .btn-primary:hover {
            background: linear-gradient(135deg, #0056b3 0%, #004085 100%);
            border-color: #004085;
        }

        /* Success Button - สีเขียว */
        .btn-success {
            color: white;
            background: linear-gradient(135deg, #28a745 0%, #1e7e34 100%);
            border-color: #28a745;
        }

        .btn-success:hover {
            background: linear-gradient(135deg, #1e7e34 0%, #155724 100%);
            border-color: #155724;
        }

        /* Info Button - สีฟ้า */
        .btn-info {
            color: white;
            background: linear-gradient(135deg, #17a2b8 0%, #117a8b 100%);
            border-color: #17a2b8;
        }

        .btn-info:hover {
            background: linear-gradient(135deg, #117a8b 0%, #0c5460 100%);
            border-color: #0c5460;
        }

        /* Warning Button - สีเหลือง/ส้ม */
        .btn-warning {
            color: #212529;
            background: linear-gradient(135deg, #ffc107 0%, #ff9800 100%);
            border-color: #ffc107;
        }

        .btn-warning:hover {
            background: linear-gradient(135deg, #ff9800 0%, #f57c00 100%);
            border-color: #f57c00;
        }

        /* Danger Button - สีแดง */
        .btn-danger {
            color: white;
            background: linear-gradient(135deg, #dc3545 0%, #c82333 100%);
            border-color: #dc3545;
        }

        .btn-danger:hover {
            background: linear-gradient(135deg, #c82333 0%, #bd2130 100%);
            border-color: #bd2130;
        }

        /* Secondary Button - สีเทา */
        .btn-secondary {
            color: white;
            background: linear-gradient(135deg, #6c757d 0%, #545b62 100%);
            border-color: #6c757d;
        }

        .btn-secondary:hover {
            background: linear-gradient(135deg, #545b62 0%, #3d4246 100%);
            border-color: #3d4246;
        }

        /* Button sizes */
        .btn-sm {
            padding: 5px 10px;
            font-size: 12px;
            line-height: 1.5;
            border-radius: 3px;
        }

        /* Calendar Container */
        .calendar-container {
            margin-bottom: 20px;
        }

        .calendar-container .card {
            border: none;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            border-radius: 10px;
            overflow: hidden;
        }

        .calendar-container .card-body {
            background: linear-gradient(135deg, #ffffff 0%, #f9f9f9 100%);
            padding: 20px;
        }

        /* Legend */
        .legend {
            font-size: 13px;
            margin: 15px 0;
            padding: 10px;
            background: #fff3e0;
            border-left: 4px solid #ff9800;
            border-radius: 4px;
        }

        .legend-red {
            color: #d32f2f;
            font-weight: bold;
            background: #ffebee;
            padding: 2px 8px;
            border-radius: 4px;
        }

        /* Button Group ใน GridView */
        .btn-group-vertical .btn {
            margin-bottom: 3px;
            font-size: 0.85em;
            padding: 4px 8px;
            border-radius: 4px;
            transition: all 0.2s ease;
        }

        .btn-group-vertical .btn:hover {
            transform: translateX(2px);
            box-shadow: 0 2px 5px rgba(0,0,0,0.2);
        }

        /* Calendar Style */
        .calendar-style {
            font-size: 14px;
            border: 1px solid #d7ccc8;
            border-radius: 8px;
            overflow: hidden;
        }

        /* Page Header */
        .container-fluid h2 {
            color: #5d4037;
            font-weight: bold;
            padding: 15px;
            background: linear-gradient(135deg, #f5f5f5 0%, #eeeeee 100%);
            border-radius: 10px;
            margin-bottom: 20px;
            box-shadow: 0 2px 5px rgba(0,0,0,0.1);
        }

        /* Table Container */
        .table-container {
            margin-top: 20px;
            padding: 15px;
            background: white;
            border-radius: 10px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
        }

        /* Label ที่แสดงวันที่ */
        .h4.text-primary {
            color: #1976d2 !important;
            font-weight: bold;
            padding: 10px;
            background: #e3f2fd;
            border-radius: 8px;
            display: inline-block;
            margin-bottom: 15px;
        }

        /* 📱 Mobile-Friendly Design - Simple & Easy to Use */
        @media screen and (max-width: 768px) {
            /* เพิ่ม zoom control สำหรับมือถือ */
            .mobile-zoom-hint {
                background: #fffbcc;
                border: 1px solid #ffc107;
                padding: 8px;
                margin: 10px 0;
                border-radius: 5px;
                font-size: 12px;
                text-align: center;
            }

            /* ให้ table scroll ได้แนวนอน แต่แสดงข้อมูลสำคัญ */
            .table-container {
                overflow-x: auto;
                -webkit-overflow-scrolling: touch;
                border: 1px solid #ddd;
                border-radius: 5px;
            }

            /* ปรับ font ให้เล็กลงแต่ยังอ่านได้ */
            .mydatagrid {
                font-size: 10px;
                white-space: nowrap;
            }

            .mydatagrid th, .mydatagrid td {
                padding: 4px 2px !important;
                font-size: 10px;
            }

            /* 📱 ปรับช่องสำคัญให้มีขนาดเท่ากัน - ลดความกว้างลง 25% (450px → 340px) */
            .name-column,
            .room-list,
            .items-column,
            .remark-column {
                max-width: 340px !important;
                min-width: 150px;
                word-wrap: break-word !important;
                word-break: break-word !important;
                white-space: pre-line !important;
                font-size: 10px;
                line-height: 1.4;
            }

            /* แสดงห้องพักคนละบรรทัดบนมือถือ */
            .room-list {
                white-space: pre-line !important;
                font-size: 10px;
            }

            /* ปรับปุ่มให้เล็กแต่กดได้ */
            .btn-sm {
                font-size: 9px;
                padding: 2px 4px;
                margin-bottom: 1px;
            }

            /* ปรับ calendar */
            .calendar-style {
                width: 100% !important;
                font-size: 11px;
            }

            h2 {
                font-size: 1.3rem;
            }

            /* ซ่อนเฉพาะคอลัมน์ที่ไม่จำเป็นบนมือถือ */
            .mydatagrid th:nth-child(4),  /* จำนวนคืน */
            .mydatagrid td:nth-child(4) {
                display: none;
            }

            /* 📱 แสดงคอลัมน์สำคัญ: ชื่อผู้จอง, รายชื่อห้องพัก, รายการของเช่า/สินค้า, หมายเหตุ */
            .mydatagrid th:nth-child(2),  /* ชื่อผู้จอง */
            .mydatagrid td:nth-child(2),
            .mydatagrid th:nth-child(3),  /* รายชื่อห้องพัก */
            .mydatagrid td:nth-child(3),
            .mydatagrid th:nth-child(5),  /* รายการของเช่า/สินค้า */
            .mydatagrid td:nth-child(5),
            .mydatagrid th:nth-child(9),  /* หมายเหตุ */
            .mydatagrid td:nth-child(9) {
                display: table-cell !important;
                max-width: 340px !important;
                min-width: 150px;
            }
        }
    </style>

    <!-- 📱 Viewport สำหรับมือถือ - อนุญาตให้ zoom ได้ -->
    <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=3.0, user-scalable=yes">

    <div class="container-fluid">
        <div class="row">
            <div class="col-md-12">
                <h2 class="text-center"><strong>รายการผู้เข้าพักรายวัน</strong></h2>
                
                <div class="action-buttons no-print text-right mb-3">
                    <asp:Button ID="btnPrint" runat="server" Text="พิมพ์ตารางรายวัน" 
                        CssClass="btn btn-primary" OnClientClick="printTable(); return false;" />
                </div>

                <div class="calendar-container">
                    <div class="card">
                        <div class="card-body">
                            <center>
                                <asp:Calendar ID="Calendar1" runat="server" Height="199px" Width="80%"
                                    OnDayRender="Calendar1_DayRender" OnSelectionChanged="Calendar1_SelectionChanged"
                                    FirstDayOfWeek="Sunday"
                                    CssClass="calendar-style"></asp:Calendar>
                            </center>
                            
                            <div class="legend text-center">
                                <span class="legend-red">ตัวหนังสือสีแดง</span> คือ ห้องพักทั้งหมดเต็ม
                            </div>
                        </div>
                    </div>
                </div>

                <div class="table-container">
                    <center>
                        <asp:Label ID="Label1" runat="server" Text="" CssClass="h4 text-primary mb-3"></asp:Label>
                        
                        <asp:GridView ID="GridView1" DataKeyNames="ID" runat="server" 
                            AutoGenerateColumns="False" CssClass="mydatagrid table-responsive"
                            HeaderStyle-CssClass="header" RowStyle-CssClass="rows" PagerStyle-CssClass="pager"
                            BorderStyle="Solid" OnRowCommand="GridView1_RowCommand">
                            <Columns>
                                <asp:TemplateField HeaderText="เคยมาแล้ว" HeaderStyle-Width="4%" HeaderStyle-CssClass="header-center">
                                    <ItemTemplate>
                                        <asp:Button ID="Button6" runat="server" Text='<%# Eval("CountReserved") + " ครั้ง" %>' 
                                            CommandArgument='<%# Eval("ID") %>' CommandName="CountReserved" 
                                            CssClass="btn btn-info btn-sm no-print" OnClientClick="aspnetForm.target ='_blank';"/>
                                        <span class="print-only" data-count='<%# Eval("CountReserved") %>'><%# Eval("CountReserved") %> ครั้ง</span>
                                    </ItemTemplate>
                                    <HeaderStyle Width="4%" CssClass="header-center" />
                                </asp:TemplateField>

                                <asp:BoundField DataField="Name" HeaderText="ชื่อผู้จอง"
                                    HeaderStyle-Width="12%" HeaderStyle-CssClass="header-center"
                                    ItemStyle-CssClass="name-column" />

                                <asp:BoundField DataField="AccomName" HeaderText="รายชื่อห้องพัก"
                                    HeaderStyle-Width="15%" HeaderStyle-CssClass="header-center"
                                    ItemStyle-CssClass="room-list" HtmlEncode="false" />

                                <asp:BoundField DataField="StayDays" HeaderText="จำนวนคืน" 
                                    HeaderStyle-Width="5%" HeaderStyle-CssClass="header-center" 
                                    ItemStyle-CssClass="header-center" />

                                <asp:BoundField DataField="Items" HeaderText="รายการของเช่า/สินค้า"
                                    HeaderStyle-Width="15%" HeaderStyle-CssClass="header-center"
                                    ItemStyle-CssClass="items-column" />

                                <asp:BoundField DataField="TotalPrice" HeaderText="ราคาทั้งหมด"
                                    HeaderStyle-Width="6%" HeaderStyle-CssClass="header-center"
                                    ItemStyle-CssClass="header-center"
                                    DataFormatString="{0:N0}" HtmlEncode="false" />

                                <asp:BoundField DataField="Deposit" HeaderText="ยอดเงินรับมา"
                                    HeaderStyle-Width="6%" HeaderStyle-CssClass="header-center"
                                    ItemStyle-CssClass="header-center"
                                    DataFormatString="{0:N0}" HtmlEncode="false" />

                                <asp:BoundField DataField="Remain" HeaderText="ส่วนที่เหลือ"
                                    HeaderStyle-Width="6%" HeaderStyle-CssClass="header-center"
                                    ItemStyle-CssClass="header-center"
                                    DataFormatString="{0:N0}" HtmlEncode="false" />

                                <asp:BoundField DataField="Remark" HeaderText="หมายเหตุ"
                                    HeaderStyle-Width="10%" HeaderStyle-CssClass="header-center"
                                    ItemStyle-CssClass="remark-column" />

                                <asp:BoundField DataField="Reserve_By" HeaderText="จองโดย" 
                                    HeaderStyle-Width="6%" HeaderStyle-CssClass="header-center" 
                                    ItemStyle-CssClass="header-center" />

                                <asp:BoundField DataField="Status" HeaderText="สถานะ" 
                                    HeaderStyle-Width="6%" HeaderStyle-CssClass="header-center" 
                                    ItemStyle-CssClass="header-center" />

                                <asp:TemplateField HeaderText="ดำเนินการ" HeaderStyle-Width="15%" HeaderStyle-CssClass="header-center">
                                    <ItemTemplate>
                                        <div class="btn-group-vertical btn-group-sm no-print">
                                            <asp:Button ID="Button1" runat="server" Text="เช็คอิน" 
                                                CommandArgument='<%# Eval("ID") %>' CommandName="Checkin"
                                                CssClass="btn btn-success btn-sm mb-1" 
                                                onclientclick="return confirm('ยืนยันการเช็คอินหรือไม่');"/>
                                            
                                            <asp:Button ID="Button2" runat="server" Text="แก้ไข" 
                                                CommandArgument='<%# Eval("ID") %>' CommandName="EditReservation"
                                                CssClass="btn btn-warning btn-sm mb-1" />
                                            
                                            <asp:Button ID="Button3" runat="server" Text="ยกเลิกไม่คืนเงิน" 
                                                CommandArgument='<%# Eval("ID") %>' CommandName="CancelNoRefund"
                                                CssClass="btn btn-danger btn-sm mb-1" 
                                                onclientclick="return confirm('ยืนยันการยกเลิกไม่คืนเงินหรือไม่');"/>
                                            
                                            <asp:Button ID="Button4" runat="server" Text="ยกเลิกคืนเงิน"
                                                CommandArgument='<%# Eval("ID") %>' CommandName="CancelRefund"
                                                CssClass="btn btn-secondary btn-sm mb-1"
                                                onclientclick="return confirm('ยืนยันการยกเลิกคืนเงินหรือไม่');"/>

                                            <asp:Button ID="btnCheckout" runat="server" Text="เช็คเอาท์"
                                                CommandArgument='<%# Eval("ID") %>' CommandName="Checkout"
                                                CssClass="btn btn-danger btn-sm mb-1"
                                                OnClientClick="return confirm('ยืนยันการเช็คเอาท์หรือไม่');" />

                                            <asp:Button ID="Button7" runat="server" Text="รายละเอียด"
                                                CommandArgument='<%# Eval("ID") %>' CommandName="Detail"
                                                CssClass="btn btn-primary btn-sm" />
                                        </div>
                                        <div class="print-only">
                                            &nbsp;
                                        </div>
                                    </ItemTemplate>
                                    <HeaderStyle Width="15%" CssClass="header-center" />
                                </asp:TemplateField>

                                <asp:TemplateField HeaderText="" HeaderStyle-CssClass="hidden print-header">
                                    <ItemTemplate>
                                        <div class="print-only">
                                            &nbsp;
                                        </div>
                                    </ItemTemplate>
                                    <HeaderStyle CssClass="hidden print-header" />
                                    <ItemStyle CssClass="hidden print-only" />
                                </asp:TemplateField>

                                <asp:TemplateField HeaderText="" HeaderStyle-CssClass="hidden print-header">
                                    <ItemTemplate>
                                        <div class="print-only">
                                            &nbsp;
                                        </div>
                                    </ItemTemplate>
                                    <HeaderStyle CssClass="hidden print-header" />
                                    <ItemStyle CssClass="hidden print-only" />
                                </asp:TemplateField>

                                <asp:BoundField DataField="ID" HeaderText="หมายเลขการจอง" 
                                    HeaderStyle-CssClass="hidden" ItemStyle-CssClass="hidden" />
                            </Columns>

                            <HeaderStyle CssClass="header" />
                            <PagerStyle CssClass="pager" />
                            <RowStyle CssClass="rows" />
                        </asp:GridView>
                    </center>
                </div>
            </div>
        </div>
    </div>

    <script type="text/javascript">
        function printTable() {
            // ✅ All platforms (including Android) now use the same HTML generation method
            // This ensures consistent print output across all devices

            var printWindow = window.open('', '_blank', 'width=1200,height=800');

            if (!printWindow) {
                alert('กรุณาอนุญาตให้เปิด popup window เพื่อพิมพ์ตาราง\n\nสำหรับ Android: ไปที่ Settings > Site Settings > Pop-ups and redirects > อนุญาต');
                return;
            }

            var tableHTML = '<table style="width: 100%; border-collapse: collapse; font-size: 10px; border: 1px solid #000; margin: 0; padding: 0;">';

            var headerRow = document.querySelector('.mydatagrid .header');
            if (headerRow) {
                tableHTML += '<thead><tr style="background-color: #f2f2f2; height: 25px;">';

                // ปรับความกว้างคอลัมน์: ลด รายชื่อห้องพัก และ หมายเหตุ, เพิ่ม รายการของเช่า
                tableHTML += '<th style="border: 1px solid #000; padding: 2px; text-align: center; font-weight: bold; width: 3%;">เคยมาแล้ว</th>';
                tableHTML += '<th style="border: 1px solid #000; padding: 2px; text-align: center; font-weight: bold; width: 14%;">ชื่อผู้จอง</th>';
                tableHTML += '<th style="border: 1px solid #000; padding: 2px; text-align: center; font-weight: bold; width: 10%;">รายชื่อห้องพัก</th>';
                tableHTML += '<th style="border: 1px solid #000; padding: 2px; text-align: center; font-weight: bold; width: 3%;">จำนวนคืน</th>';
                tableHTML += '<th style="border: 1px solid #000; padding: 2px; text-align: center; font-weight: bold; width: 24%;">รายการของเช่า/สินค้า</th>';
                tableHTML += '<th style="border: 1px solid #000; padding: 2px; text-align: center; font-weight: bold; width: 4%;">ราคาทั้งหมด</th>';
                tableHTML += '<th style="border: 1px solid #000; padding: 2px; text-align: center; font-weight: bold; width: 4%;">เงินมัดจำ</th>';
                tableHTML += '<th style="border: 1px solid #000; padding: 2px; text-align: center; font-weight: bold; width: 4%;">ส่วนที่เหลือ</th>';
                tableHTML += '<th style="border: 1px solid #000; padding: 2px; text-align: center; font-weight: bold; width: 10%;">หมายเหตุ</th>';
                tableHTML += '<th style="border: 1px solid #000; padding: 2px; text-align: center; font-weight: bold; width: 12%;">&nbsp;</th>';
                tableHTML += '<th style="border: 1px solid #000; padding: 2px; text-align: center; font-weight: bold; width: 12%;">&nbsp;</th>';

                tableHTML += '</tr></thead>';
            }

            tableHTML += '<tbody>';
            var rows = document.querySelectorAll('.mydatagrid .rows');

            for (var i = 0; i < rows.length; i++) {
                // ลดความสูงลงอีก 30% จาก 105px เหลือ 74px
                tableHTML += '<tr style="height: 74px;">';

                var cells = rows[i].querySelectorAll('td');
                var data = {
                    countReserved: '',
                    name: '',
                    accomName: '',
                    stayDays: '',
                    items: '',
                    totalPrice: '',
                    deposit: '',
                    remain: '',
                    remark: ''
                };

                // ดึงข้อมูลจากแต่ละเซลล์
                for (var j = 0; j < cells.length; j++) {
                    if (!cells[j].classList.contains('hidden')) {

                        // ดึงข้อมูล "เคยมาแล้ว" จาก data attribute
                        if (j === 0) { // คอลัมน์แรกคือ "เคยมาแล้ว"
                            var spanElement = cells[j].querySelector('.print-only');
                            if (spanElement) {
                                // ลองดึงจาก data attribute ก่อน
                                var countData = spanElement.getAttribute('data-count');
                                if (countData) {
                                    data.countReserved = countData + ' ครั้ง';
                                } else {
                                    // ถ้าไม่มี data attribute ให้ดึงจาก text content
                                    var spanText = spanElement.textContent || spanElement.innerText;
                                    if (spanText && spanText.trim() !== '') {
                                        data.countReserved = spanText;
                                    } else {
                                        // ถ้ายังไม่มี ให้ลองดึงจากปุ่ม
                                        var button = cells[j].querySelector('.btn');
                                        if (button) {
                                            data.countReserved = button.textContent || button.innerText;
                                        }
                                    }
                                }
                            }
                        }

                        // ดึงข้อมูลจาก header text สำหรับคอลัมน์อื่นๆ
                        var headerText = '';
                        if (headerRow && headerRow.querySelectorAll('th')[j]) {
                            headerText = headerRow.querySelectorAll('th')[j].innerText;
                        }

                        var cellContent = cells[j].textContent || cells[j].innerText;

                        if (headerText === 'ชื่อผู้จอง') data.name = cellContent;
                        else if (headerText === 'รายชื่อห้องพัก') data.accomName = cellContent;
                        else if (headerText === 'จำนวนคืน') data.stayDays = cellContent;
                        else if (headerText === 'รายการของเช่า/สินค้า') data.items = cellContent;
                        else if (headerText === 'ราคาทั้งหมด') data.totalPrice = cellContent;
                        else if (headerText === 'ยอดเงินรับมา') data.deposit = cellContent;
                        else if (headerText === 'ส่วนที่เหลือ') data.remain = cellContent;
                        else if (headerText === 'หมายเหตุ') data.remark = cellContent;
                    }
                }

                // สร้างแถวข้อมูลด้วยความกว้างใหม่ และความสูง 74px (ลดลงอีก 30% จาก 105px)
                tableHTML += '<td style="border: 1px solid #000; padding: 2px; text-align: center; width: 3%; height: 90px; vertical-align: top;">' + (data.countReserved || '&nbsp;') + '</td>';
                tableHTML += '<td style="border: 1px solid #000; padding: 2px; text-align: left; width: 10%; height: 90px; vertical-align: top;">' + (data.name || '&nbsp;') + '</td>';
                tableHTML += '<td style="border: 1px solid #000; padding: 2px; text-align: left; width: 10%; height: 90px; vertical-align: top;">' + (data.accomName || '&nbsp;') + '</td>';
                tableHTML += '<td style="border: 1px solid #000; padding: 2px; text-align: center; width: 3%; height: 90px; vertical-align: top;">' + (data.stayDays || '&nbsp;') + '</td>';
                tableHTML += '<td style="border: 1px solid #000; padding: 2px; text-align: left; width: 25%; height: 90px; vertical-align: top;">' + (data.items || '&nbsp;') + '</td>';
                tableHTML += '<td style="border: 1px solid #000; padding: 2px; text-align: center; width: 3; height: 90px; vertical-align: top;">' + (data.totalPrice || '&nbsp;') + '</td>';
                tableHTML += '<td style="border: 1px solid #000; padding: 2px; text-align: center; width: 3%; height: 90px; vertical-align: top;">' + (data.deposit || '&nbsp;') + '</td>';
                tableHTML += '<td style="border: 1px solid #000; padding: 2px; text-align: center; width: 3%; height: 90px; vertical-align: top;">' + (data.remain || '&nbsp;') + '</td>';
                tableHTML += '<td style="border: 1px solid #000; padding: 2px; text-align: left; width: 10%; height: 90px; vertical-align: top;">' + (data.remark || '&nbsp;') + '</td>';
                tableHTML += '<td style="border: 1px solid #000; padding: 2px; text-align: center; width: 15%; height: 90px; background-color: #f9f9f9; vertical-align: top;">&nbsp;</td>';
                tableHTML += '<td style="border: 1px solid #000; padding: 2px; text-align: center; width: 15%; height: 90px; background-color: #f9f9f9; vertical-align: top;">&nbsp;</td>';

                tableHTML += '</tr>';
            }
            tableHTML += '</tbody></table>';

            // เพิ่มข้อความ 3 อัน ในบรรทัดเดียวกัน กระจายเต็มความกว้าง
            var summaryTableHTML = `
                <div style="margin-top: 20px; font-size: 11px; display: flex; justify-content: space-between; align-items: center;">
                    <div style="flex: 1; text-align: left;">
                        <strong>ยอดเงินที่โอนเข้าบัญชีบริษัท:</strong>
                        <span style="display: inline-block; width: 80px; border-bottom: 1px dashed #666; margin-left: 5px;">&nbsp;</span>
                    </div>
                    <div style="flex: 1; text-align: center;">
                        <strong>ยอดเงินที่โอนเข้าบัญชีเงินสด:</strong>
                        <span style="display: inline-block; width: 80px; border-bottom: 1px dashed #666; margin-left: 5px;">&nbsp;</span>
                    </div>
                    <div style="flex: 1; text-align: right;">
                        <strong>เงินสด:</strong>
                        <span style="display: inline-block; width: 80px; border-bottom: 1px dashed #666; margin-left: 5px;">&nbsp;</span>
                    </div>
                </div>
            `;

            // Get date label text safely
            var dateLabel = document.getElementById('<%= Label1.ClientID %>');
            var dateLabelText = dateLabel ? dateLabel.innerText : '';

            printWindow.document.write(`
                <html>
                    <head>
                        <title>รายการผู้เข้าพักรายวัน - ${dateLabelText}</title>
                        <style>
                            @page {
                                margin: 0.5cm;
                                size: A4 landscape;
                            }
                            body { 
                                font-family: 'Tahoma', 'Sans-serif'; 
                                margin: 0;
                                padding: 0;
                                font-size: 10px;
                            }
                            h2, h3 { 
                                text-align: center; 
                                margin: 5px 0;
                                padding: 0;
                                line-height: 1.2;
                            }
                            h2 {
                                font-size: 14px;
                                margin-bottom: 2px;
                            }
                            h3 {
                                font-size: 12px;
                                margin-bottom: 5px;
                            }
                            table { 
                                width: 100%; 
                                border-collapse: collapse; 
                                font-size: 9px;
                                margin: 0;
                                padding: 0;
                                table-layout: fixed;
                            }
                            th, td {
                                border: 1px solid #000;
                                padding: 2px;
                                text-align: center;
                                word-wrap: break-word;
                                word-break: break-word;
                                white-space: normal;
                                overflow: hidden;
                            }
                            th {
                                background-color: #f2f2f2;
                                font-weight: bold;
                                height: 25px;
                            }
                            td {
                                height: 74px;
                                vertical-align: top;
                            }
                            .summary-table {
                                width: 100%;
                                border-collapse: collapse;
                                font-size: 11px;
                                margin-top: 20px;
                                margin-bottom: 10px;
                            }
                            .summary-table td {
                                vertical-align: top;
                                text-align: left;
                                padding: 1px;
                                border: 1px solid #000;
                            }
                            .dashed-line {
                                height: 10px;
                                border-bottom: 1px dashed #666;
                                margin-top: 1px;
                            }
                            .print-footer { 
                                margin-top: 10px; 
                                text-align: right; 
                                font-size: 9px;
                                padding: 0;
                            }
                            @media print {
                                body { 
                                    margin: 0.2cm;
                                    padding: 0;
                                }
                                table { 
                                    width: 100%; 
                                    page-break-inside: auto;
                                }
                                tr { 
                                    page-break-inside: avoid; 
                                    page-break-after: auto; 
                                }
                                * {
                                    -webkit-print-color-adjust: exact;
                                    color-adjust: exact;
                                }
                            }
                        </style>
                    </head>
                    <body>
                        <h2>รายการผู้เข้าพักรายวัน</h2>
                        <h3>${dateLabelText}</h3>
                        ${tableHTML}
                        ${summaryTableHTML}
                        <div class="print-footer">
                            พิมพ์เมื่อ: ${new Date().toLocaleDateString('th-TH')} ${new Date().toLocaleTimeString('th-TH', { hour: '2-digit', minute: '2-digit' })}
                        </div>
                    </body>
                </html>
            `);

            printWindow.document.close();

            // ✅ Wait for content to fully load before printing (critical for Android)
            printWindow.onload = function() {
                printWindow.focus();

                // Give browser extra time to render on slower devices
                setTimeout(function() {
                    printWindow.print();

                    // Don't auto-close - let user close after print/cancel
                    // This prevents "problem printing" error on Android
                    // printWindow.close();
                }, 250);
            };

            // Fallback for browsers that don't fire onload on document.write
            setTimeout(function() {
                if (printWindow.document.readyState === 'complete') {
                    printWindow.focus();
                    printWindow.print();
                }
            }, 1000);
        }
    </script>
</asp:Content>