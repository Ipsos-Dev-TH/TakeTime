<%@ Page MaintainScrollPositionOnPostback="true" Async="True" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Reserve.aspx.cs" Inherits="Take_Time_BangPhra.Reserve" EnableEventValidation="false" %>
<%@ Register assembly="Microsoft.ReportViewer.WebForms" namespace="Microsoft.Reporting.WebForms" tagprefix="rsweb" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
    /* GridView Row Selection Styles */
.accom-row {
    transition: background-color 0.3s ease;
}

.accom-row td {
    background-color: inherit !important;
}

.accom-row-selected {
    background-color: #90EE90 !important; /* Light green when selected */
}

.accom-row-selected td {
    background-color: #90EE90 !important;
}

.accom-row-default {
    background-color: white !important;
}

.accom-row-default td {
    background-color: white !important;
}

.accom-row-hover {
    background-color: #F0F8FF !important; /* Light blue on hover */
}

.accom-row-hover td {
    background-color: #F0F8FF !important;
}
</style>

    <script type="text/javascript">
        function pageLoad() {
            // Initialize row selection functionality
            initGridViewSelection();
        }

        function initGridViewSelection() {
            // Attach events to all checkboxes in GridView1
            $('input[id*="chkSelect"]').each(function () {
                var checkbox = $(this);
                var row = checkbox.closest('tr');

                // Set initial state
                if (checkbox.is(':checked')) {
                    row.removeClass('accom-row-default accom-row-hover');
                    row.addClass('accom-row-selected');
                } else {
                    row.removeClass('accom-row-selected');
                    row.addClass('accom-row-default');
                }

                // Attach click event
                checkbox.on('click', function () {
                    setTimeout(function () {
                        if (checkbox.is(':checked')) {
                            row.removeClass('accom-row-default accom-row-hover');
                            row.addClass('accom-row-selected');
                        } else {
                            row.removeClass('accom-row-selected');
                            row.addClass('accom-row-default');
                        }
                    }, 10);
                });
            });

            // Add hover effects
            $('#' + '<%= GridView1.ClientID %>').find('tr').hover(
                function () {
                    if (!$(this).hasClass('accom-row-selected')) {
                        $(this).addClass('accom-row-hover');
                    }
                },
                function () {
                    $(this).removeClass('accom-row-hover');
                }
            );
        }

        // Initialize when document is ready
        $(document).ready(function () {
            initGridViewSelection();
        });

        // Re-initialize after async postback
        var prm = Sys.WebForms.PageRequestManager.getInstance();
        prm.add_endRequest(function () {
            initGridViewSelection();
        });
    </script>


    <style>
    /* Force background color inheritance for all GridView cells */
    #GridView1 tr td,
    #GridView1 tr th,
    #GridView1 tr {
        background-color: inherit !important;
        border-color: inherit !important;
    }

    .selected-row {
        background-color: #90EE90 !important;
    }

    .normal-row {
        background-color: white !important;
    }

    .hover-row {
        background-color: #F0F8FF !important;
    }
</style>

    <style type="text/css">

        /* Main color scheme */
        body {
            background-color: #F5F5F0; /* Cream background */
            color: #5D4037; /* Dark brown text */
            font-family: 'Prompt', Arial, sans-serif;
        }

        /* Form container */
        .reservation-container {
            background-color: #FFF8E1; /* Light cream */
            border-radius: 15px;
            padding: 20px;
            box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);
            margin: 20px auto;
            max-width: 1200px;
            box-sizing: border-box;
        }

        /* Header styles */
        .section-header {
            color: #6D4C41; /* Medium brown */
            font-weight: bold;
            margin: 15px 0 10px;
            padding-bottom: 5px;
            border-bottom: 2px solid #D7CCC8; /* Light brown */
        }

        /* Form elements */
        .rounded-textbox {
            border-radius: 8px;
            padding: 8px 12px;
            border: 1px solid #BCAAA4; /* Light brown border */
            background-color: #FFF;
            color: #5D4037;
            font-family: 'Prompt', Arial, sans-serif;
            box-sizing: border-box;
            max-width: 100%;
        }

        .rounded-textbox:focus {
            border-color: #8D6E63; /* Medium brown */
            outline: none;
            box-shadow: 0 0 5px rgba(141, 110, 99, 0.3);
        }

        /* Buttons */
        .reservation-button {
            background-color: #8D6E63; /* Medium brown */
            color: white;
            border: none;
            border-radius: 8px;
            padding: 10px 20px;
            font-weight: bold;
            cursor: pointer;
            transition: background-color 0.3s;
        }

        .reservation-button:hover {
            background-color: #6D4C41; /* Darker brown */
        }

        /* GridView styling */
        .mydatagrid {
            width: 100%;
            border: 1px solid #D7CCC8;
            border-collapse: collapse;
            margin: 10px 0;
        }

        .mydatagrid th {
            background-color: #D7CCC8; /* Light brown */
            color: #3E2723; /* Very dark brown */
            padding: 10px;
            text-align: center;
        }

        .mydatagrid td {
            padding: 8px;
            border: 1px solid #D7CCC8;
        }

        .mydatagrid tr:nth-child(even) {
            background-color: #EFEBE9; /* Very light cream */
        }

        .mydatagrid tr:hover {
            background-color: #E0E0E0;
        }

        /* Checkbox and radio button styling */
        .mycheckbox input[type="checkbox"] {
            margin-right: 5px;
            accent-color: #8D6E63; /* Medium brown */
        }

        .radioBL input[type="radio"] {
            margin-right: 10px;
            accent-color: #8D6E63; /* Medium brown */
        }

        /* Calendar styling */
        .myCalendar th.myCalendarDayHeader {
            height: 25px;
            border-bottom: outset 2px #EFEBE9;
            border-right: outset 2px #EFEBE9;
            background-color: #D7CCC8;
        }

        .myCalendar td.myCalendarDay {
            border: outset 2px #EFEBE9;
        }

        .myCalendar .myCalendarToday {
            background-color: #F5F5F0;
            -webkit-box-shadow: 0 0 7px 3px #E0E0E0;
            box-shadow: 0 0 7px 3px #E0E0E0;
        }

        /* Responsive adjustments */
        @media (max-width: 768px) {
            .reservation-container {
                padding: 10px;
            }

            .rounded-textbox, .reservation-button {
                width: 100% !important;
                margin-bottom: 10px;
            }

            /* ช่องที่กว้างเต็มแถวแล้วห้ามมีระยะซ้าย (inline margin-left) — กันหน้าล้นจอแนวนอน */
            .form-controls .rounded-textbox,
            .form-controls .reservation-button,
            .rv-submit .reservation-button { margin-left: 0 !important; }
        }

        /* Highlight important fields */
        .required-field {
            color: #D32F2F; /* Red for required fields */
            font-weight: bold;
        }

        /* Panel styling */
        .form-panel {
            background-color: #EFEBE9;
            padding: 15px;
            border-radius: 10px;
            margin: 10px 0;
            border: 1px solid #D7CCC8;
        }

        /* Price display */
        .price-display {
            font-weight: bold;
            color: #5D4037;
            font-size: 1.1em;
        }

        /* Rules section */
        .rules-section {
            background-color: #EFEBE9;
            padding: 15px;
            border-radius: 10px;
            margin: 15px 0;
            text-align: center;
        }

        /* Form row styling */
        .form-row {
            display: flex;
            flex-wrap: wrap;
            margin: 10px 0;
            align-items: center;
        }

        .form-label {
            width: 20%;
            text-align: right;
            padding-right: 15px;
            font-weight: bold;
            box-sizing: border-box;
        }

        .form-controls {
            width: 80%;
            box-sizing: border-box;
            min-width: 0;
        }

        @media (max-width: 768px) {
            .form-label, .form-controls {
                width: 100%;
                text-align: left;
            }

            .form-label {
                padding-right: 0;
                margin-bottom: 5px;
            }
        }

        /* ══════════ ขั้นตอนการจอง (rv-*) — ต่อยอดจากธีมเดิม ไม่แทนที่คลาสเดิม ══════════ */
        .reservation-container { counter-reset: rvstep; }
        .rv-step-h { display: flex; align-items: center; flex-wrap: wrap; gap: 4px 10px; }
        .rv-step-h::before {
            counter-increment: rvstep;
            content: counter(rvstep);
            display: inline-flex; align-items: center; justify-content: center;
            width: 30px; height: 30px; flex: 0 0 30px;
            border-radius: 50%; background: #5D4037; color: #fff; font-size: 0.9em;
        }
        .rv-step-h small { font-weight: normal; color: #8D6E63; font-size: 0.72em; }
        .rv-alt { background-color: #EFEBE9; padding: 8px 0; }
        .rv-hint { font-size: 0.88em; color: #8D6E63; margin: 4px 0 0; line-height: 1.5; }
        .rv-muted { color: #A1887F; font-weight: normal; font-size: 0.9em; }

        /* แถบขั้นตอน (ลูกค้า) */
        .rv-intro { display: none; }
        .rv-cust .rv-intro {
            display: block; background: #fff; border: 1px solid #D7CCC8; border-radius: 12px;
            padding: 10px 14px; margin-bottom: 6px; line-height: 1.7; font-size: 0.93em; color: #6D4C41;
        }
        .rv-intro b { color: #5D4037; }
        .rv-chips { display: flex; flex-wrap: wrap; gap: 6px; margin-top: 6px; }
        .rv-chips span { background: #EFEBE9; border-radius: 14px; padding: 3px 10px; font-size: 0.92em; white-space: nowrap; }
        .rv-chips span + span::before { content: "› "; color: #A1887F; }
        .rv-cust-only { display: none !important; }
        .rv-cust .rv-cust-only { display: flex !important; }

        /* สัตว์เลี้ยง */
        .rv-pet-step { background: #FFF3E0; border-color: #FFCC80; }
        .rv-pet-step .rv-step-h { color: #E65100; }
        .rv-check-lg { font-weight: bold; color: #E65100; font-size: 1.05em; }
        .rv-pet-rooms { margin: 10px 0 4px; display: grid; gap: 8px; }
        .rv-pet-line {
            display: flex; flex-wrap: wrap; align-items: center; gap: 6px 12px;
            background: #fff; border: 1px solid #FFE0B2; border-radius: 10px; padding: 10px 12px;
        }
        .rv-pet-room { font-weight: 600; flex: 1 1 160px; min-width: 0; word-break: break-word; }
        .rv-pet-ctrl { display: flex; align-items: center; gap: 8px; }
        .rv-pet-ctrl input { width: 90px !important; margin: 0 !important; text-align: center; min-height: 44px; }
        .rv-pet-line .rv-petinfo { flex-basis: 100%; font-size: 0.85em; color: #8D6E63; }
        .rv-pet-sub { font-weight: 600; color: #E65100; white-space: nowrap; }
        .rv-pet-summary { margin-top: 6px; font-size: 0.92em; color: #6D4C41; line-height: 1.65; }
        .rv-pet-total { margin-top: 4px; }
        .rv-pets-moved .rv-petcol { display: none !important; }
        .rv-pet-policy-box {
            background: #fff; border-left: 4px solid #FB8C00; border-radius: 6px; padding: 10px 14px;
            max-height: 220px; overflow: auto; line-height: 1.7; color: #4E342E; font-size: 0.95em;
        }

        /* สรุปยอด */
        .rv-sum-card {
            background: #fff; border: 2px solid #D7CCC8; border-radius: 12px; padding: 12px 16px;
            margin: 10px 0; max-width: 560px;
        }
        .rv-sum-row { display: flex; justify-content: space-between; align-items: baseline; gap: 12px; padding: 6px 0; border-bottom: 1px dashed #EFEBE9; }
        .rv-sum-row > span:first-child { min-width: 0; }
        .rv-sum-row b { white-space: nowrap; }
        .rv-sum-disc { color: #2E7D32; font-size: 0.92em; }
        .rv-sum-total { border-bottom: 0; border-top: 2px solid #D7CCC8; margin-top: 4px; padding-top: 10px; font-size: 1.15em; color: #3E2723; }
        .rv-sum-dep { border-bottom: 0; color: #E65100; }
        .rv-sum-note { font-size: 0.85em; color: #8D6E63; margin-top: 6px; line-height: 1.55; }
        .rv-sum-empty { color: #8D6E63; text-align: center; padding: 10px 0; line-height: 1.7; }
        .rv-cust .rv-total-row { display: none; }

        /* ปุ่มเลือกยอดโอนเร็ว */
        .rv-quick { flex-wrap: wrap; gap: 8px; margin: 8px 0 2px; }
        .rv-quick button {
            min-height: 44px; padding: 8px 14px; border-radius: 22px; border: 1px solid #8D6E63;
            background: #fff; color: #5D4037; cursor: pointer; font-family: inherit; font-size: 0.95em;
        }
        .rv-quick button:hover { background: #EFEBE9; }

        /* ยืนยันเงื่อนไข */
        .rv-sub { margin: 6px 0 10px; }
        .rv-accept { background: #fff; border: 2px solid #D7CCC8; border-radius: 12px; padding: 4px 14px; margin-top: 12px; }
        .rv-accept-title { font-weight: bold; color: #5D4037; padding: 10px 0 2px; }
        .rv-check { display: flex; align-items: flex-start; gap: 10px; padding: 10px 0; cursor: pointer; line-height: 1.6; color: #5D4037; }
        .rv-accept .rv-check, .rv-accept > div > .rv-check { border-top: 1px dashed #EFEBE9; }
        .rv-check input[type="checkbox"] { width: 22px; height: 22px; flex: 0 0 22px; margin: 2px 0 0; accent-color: #8D6E63; cursor: pointer; }
        .rv-check .mycheckbox { flex: 0 0 auto; }
        .rv-check.rv-invalid { background: #FFEBEE; border-radius: 8px; }

        /* ข้อความแจ้งใกล้ช่อง (แทน alert) */
        .rv-msg { border-radius: 6px; padding: 8px 10px; margin: 6px 0; font-size: 0.92em; white-space: pre-line; line-height: 1.55; text-align: left; }
        .rv-err { color: #B71C1C; background: #FFEBEE; border-left: 4px solid #E53935; }
        .rv-ok { color: #1B5E20; background: #E8F5E9; border-left: 4px solid #43A047; }
        .rv-invalid { border-color: #E53935 !important; box-shadow: 0 0 0 3px rgba(229, 57, 53, .15) !important; }

        /* ปุ่มยืนยัน + บอกว่ายังขาดอะไร */
        .rv-submit { text-align: center; margin-top: 18px; }
        .rv-recap { font-size: 1.05em; color: #3E2723; margin-bottom: 8px; }
        .rv-recap-sep { margin: 0 8px; color: #BCAAA4; }
        .rv-gate-hint, .rv-form-error {
            text-align: left; margin: 10px auto; max-width: 560px; border-radius: 10px; padding: 10px 14px; font-size: 0.95em; line-height: 1.6;
        }
        .rv-gate-hint { background: #FFF8E1; border: 1px solid #FFE082; color: #6D4C41; }
        .rv-gate-hint ul { margin: 4px 0 0; padding-left: 20px; }
        .rv-gate-hint a { color: #5D4037; text-decoration: underline; }
        .rv-gate-title { font-weight: bold; }
        .rv-form-error { background: #FFEBEE; border: 1px solid #EF9A9A; color: #B71C1C; white-space: pre-line; }
        .rv-submit-btn { min-height: 56px; min-width: 280px; font-size: 1.15em; padding: 12px 24px; }
        .rv-submit-btn.rv-btn-off { background-color: #BCAAA4; }
        .rv-submit-note { font-size: 0.85em; color: #8D6E63; margin-top: 8px; }
        .rv-shake { animation: rvShake .45s; }
        @keyframes rvShake { 0%, 100% { transform: translateX(0); } 25% { transform: translateX(-6px); } 75% { transform: translateX(6px); } }

        /* กำลังบันทึก */
        .rv-busy {
            position: fixed; left: 0; top: 0; right: 0; bottom: 0; z-index: 10001; background: rgba(62, 39, 35, .55);
            display: none; align-items: center; justify-content: center; padding: 16px;
        }
        .rv-busy-box { background: #fff; border-radius: 14px; padding: 22px 26px; max-width: 360px; text-align: center; color: #5D4037; line-height: 1.6; }
        .rv-spin {
            width: 40px; height: 40px; margin: 0 auto 10px; border-radius: 50%;
            border: 4px solid #EFEBE9; border-top-color: #8D6E63; animation: rvSpin 0.9s linear infinite;
        }
        @keyframes rvSpin { to { transform: rotate(360deg); } }

        /* ตารางห้องพัก */
        .mydatagrid input[type="checkbox"] { width: 22px; height: 22px; cursor: pointer; }

        @media (max-width: 600px) {
            .reservation-container { margin: 8px 0; padding: 10px 8px; border-radius: 12px; }
            .form-panel { padding: 12px 10px; }
            .form-row { margin: 8px 0; }
            .section-header { font-size: 1.15em; }
            .rounded-textbox { min-height: 44px; font-size: 16px; } /* 16px = iOS ไม่ซูมเข้าเองตอนแตะช่อง */
            textarea.rounded-textbox { min-height: 88px; }
            .reservation-button { min-height: 44px; }
            .rv-submit-btn { min-width: 0; }
            .rv-sum-card { padding: 10px 12px; }
            .rv-sum-total { font-size: 1.08em; }
            .rules-section { padding: 8px; }
            .rules-section img { width: 100% !important; }
            .rv-accept { padding: 2px 10px; }
            /* เป้าแตะ ≥ 44px: ช่องติ๊กใหญ่ขึ้น + ข้อความข้างช่องแตะได้ทั้งบรรทัด */
            .mydatagrid input[type="checkbox"] { width: 28px; height: 28px; }
            .mycheckbox input[type="checkbox"] { width: 22px; height: 22px; vertical-align: middle; }
            .mycheckbox label { display: inline-block; padding: 10px 0; }
            .rv-gate-hint li a { display: inline-block; padding: 8px 0; }
        }
    </style>

    <script>
        // 🔒 Prevent double-click on submit button
        var isSubmitting = false;

        function preventDoubleSubmit() {
            // ลูกค้าจองเอง: ยังกรอก/ติ๊กไม่ครบ → บอกว่าขาดอะไร + พาไปที่ช่องนั้น (server ตรวจซ้ำทุกข้อเสมอ)
            if (window.RV && RV.cust && typeof rvGateBlocks === 'function' && rvGateBlocks()) {
                return false;
            }

            // Check if already submitting
            if (isSubmitting) {
                alert('⚠️ กำลังดำเนินการบันทึก กรุณารอสักครู่...');
                return false; // Prevent form submission
            }

            // Mark as submitting
            isSubmitting = true;

            // Use setTimeout to allow postback to start
            setTimeout(function() {
                var btn = document.getElementById('<%= Button1.ClientID %>');
                if (btn) {
                    btn.disabled = true;
                    btn.value = '⏳ กำลังบันทึก...';
                }
                document.body.style.cursor = 'wait';
                var busy = document.getElementById('rvBusy');
                if (busy) busy.style.display = 'flex';
            }, 10);

            // Allow form submission
            return true;
        }

        // Legacy function (kept for compatibility)
        function setHourglass() {
            document.getElementById("MainContent_Button1").disabled = true;
            document.body.style.cursor = 'Wait';
        }
    </script>

    <div class="reservation-container ExampleFont <%= ContainerCss %>">

        <%-- แถบขั้นตอน — แสดงเฉพาะลูกค้าจองเอง (CSS .rv-cust) --%>
        <div class="rv-intro">
            <b>ขั้นตอนการจอง</b> <span class="rv-muted">(How to book)</span>
            <div class="rv-chips">
                <span>ห้องพัก/วันที่</span><span>ข้อมูลผู้เข้าพัก</span><span>สัตว์เลี้ยง (ถ้ามี)</span><span>สรุปยอด</span><span>ชำระเงิน/แนบสลิป</span><span>ยอมรับเงื่อนไข → ยืนยัน</span>
            </div>
        </div>

        <%-- ══ ขั้นที่ 1: ห้องพักและวันที่ ══ --%>
        <div class="form-panel rv-step" id="rvStepRoom">
            <h3 class="section-header rv-step-h">ห้องพักและวันที่ <small>Room &amp; Dates</small></h3>
            <div><asp:TextBox ID="TextBox11" runat="server" TextMode="DateTime" Visible="False"></asp:TextBox></div>
            <div class="form-row">
                <div class="form-label">วันที่เช็คอิน:<br />Check-In Date:</div>
                <div class="form-controls">
                    <asp:TextBox ID="TextBox12" runat="server" AutoPostBack="True" TextMode="Date" Width="200px" OnTextChanged="TextBox12_TextChanged" CssClass="rounded-textbox"></asp:TextBox>
                    <asp:Button ID="Button2" runat="server" Text="ล้างวันที่ (Clear)" OnClick="Button2_Click" CssClass="reservation-button" style="margin-left: 10px;"/>
                </div>
            </div>

            <div class="form-row rv-alt">
                <div class="form-label">จำนวนคืน:<br />Night(s):</div>
                <div class="form-controls">
                    <asp:DropDownList ID="DropDownList1" runat="server" CssClass="rounded-textbox" AutoPostBack="True" OnSelectedIndexChanged="DropDownList1_SelectedIndexChanged" Width="120px">
                        <asp:ListItem Value="1">1 คืน</asp:ListItem>
                        <asp:ListItem Value="2">2 คืน</asp:ListItem>
                        <asp:ListItem Value="3">3 คืน</asp:ListItem>
                        <asp:ListItem Value="4">4 คืน</asp:ListItem>
                        <asp:ListItem Value="5">5 คืน</asp:ListItem>
                        <asp:ListItem Value="6">6 คืน</asp:ListItem>
                        <asp:ListItem Value="7">7 คืน</asp:ListItem>
                    </asp:DropDownList>
                    <span style="margin-left: 15px; display: inline-block;">
                        <asp:Label ID="Label1" runat="server" Text="Check-Out: "></asp:Label>
                    </span>
                    <asp:Button ID="Button4" runat="server" Text="เลือกทั้งหมด" Visible="False" OnClick="Button4_Click" CssClass="reservation-button" style="margin-left: 15px;"/>
                    <asp:CheckBox ID="CheckBox6" runat="server" Text="แก้ไขราคาห้องพัก" Visible="false" AutoPostBack="True" CssClass="mycheckbox" style="margin-left: 15px;"/>
                </div>
            </div>

            <div class="form-row">
                <div class="form-label">เลือกห้องพัก:<br />Choose Room(s):</div>
                <div class="form-controls">
                    <div class="rv-hint rv-cust-only" style="margin-bottom: 4px;">ติ๊กห้องที่ต้องการ แล้วระบุจำนวนผู้เข้าพักของแต่ละห้อง (Tick the room(s) and enter the number of guests)</div>
                    <%-- คอลัมน์สุดท้ายของ GridView1 (index 6) = 🐾 สัตว์เลี้ยงต่อห้อง — ต่อท้ายเพื่อไม่ให้ index เดิม Cells[2..5]/Columns[5] เลื่อน
                         แสดงเฉพาะเมื่อเปิดฟีเจอร์และติ๊ก "มีสัตว์เลี้ยงเข้าพัก" — ApplyPetStay() คุม
                         มี JavaScript: ช่องกรอกถูกย้ายไปแสดงในขั้น "สัตว์เลี้ยง" (ชื่อฟอร์มเดิม ส่งค่าเหมือนเดิม) และซ่อนคอลัมน์นี้ --%>
                    <asp:GridView ID="GridView1" runat="server" Width="100%" AutoGenerateColumns="False" CssClass="mydatagrid ExampleFont" PagerStyle-CssClass="pager" HeaderStyle-CssClass="header" RowStyle-CssClass="rows" OnRowCancelingEdit="GridView1_RowCancelingEdit" OnRowEditing="GridView1_RowEditing" OnRowUpdating="GridView1_RowUpdating">
                        <Columns>
                            <asp:TemplateField HeaderText="เลือก (Select)" HeaderStyle-Width="5%" HeaderStyle-CssClass="ExampleFont" ItemStyle-CssClass="ExampleFont">
                                <ItemTemplate>
                                    <asp:CheckBox ID="chkSelect" runat="server" Width="100%" CommandName="Check" AutoPostBack="true"/>
                                </ItemTemplate>
                                <HeaderStyle Width="5%"></HeaderStyle>
                                <ItemStyle CssClass="ExampleFont"></ItemStyle>
                            </asp:TemplateField>
                            <asp:BoundField DataField="AccomName" HeaderText="ห้องพัก (Room)" HeaderStyle-CssClass="header-center ExampleFont" ItemStyle-CssClass="ExampleFont" ReadOnly="true">
                                <HeaderStyle CssClass="header-center"></HeaderStyle>
                                <ItemStyle CssClass="ExampleFont"></ItemStyle>
                            </asp:BoundField>
                            <asp:TemplateField HeaderText="จำนวนผู้เข้าพัก (Guests)" HeaderStyle-Width="20%" HeaderStyle-CssClass="header-center ExampleFont" ItemStyle-CssClass="header-center ExampleFont">
                                <ItemTemplate>
                                    <asp:TextBox ID="txtPeopleStay" runat="server" Width="100%" Text='0' TextMode="Number" AutoPostBack="true" OnTextChanged="txtPeopleStay_TextChanged" CssClass="rounded-textbox ExampleFont"/>
                                </ItemTemplate>
                                <HeaderStyle Width="20%"></HeaderStyle>
                                <ItemStyle CssClass="header-center"></ItemStyle>
                            </asp:TemplateField>
                            <asp:BoundField DataField="People" HeaderText="ผู้เข้าพักสูงสุด (Max)" HeaderStyle-CssClass="header-center ExampleFont" ItemStyle-CssClass="header-center ExampleFont" ReadOnly="true">
                                <HeaderStyle CssClass="header-center"></HeaderStyle>
                                <ItemStyle CssClass="header-center"></ItemStyle>
                            </asp:BoundField>
                            <asp:BoundField DataField="Price" HeaderText="ราคาต่อคืน (Per night)" HeaderStyle-CssClass="header-center ExampleFont" ItemStyle-CssClass="header-center ExampleFont">
                                <HeaderStyle CssClass="header-center"></HeaderStyle>
                                <ItemStyle CssClass="header-center"></ItemStyle>
                            </asp:BoundField>
                            <asp:CommandField ButtonType="Button" HeaderText="แก้ไข" ShowEditButton="True" ControlStyle-CssClass="ExampleFont" HeaderStyle-CssClass="header-center ExampleFont" ItemStyle-CssClass="header-center ExampleFont">
                                <ControlStyle CssClass="ExampleFont"></ControlStyle>
                                <HeaderStyle CssClass="header-center ExampleFont"></HeaderStyle>
                                <ItemStyle CssClass="header-center ExampleFont"></ItemStyle>
                            </asp:CommandField>
                            <%-- ไม่ AutoPostBack: กรอกแล้วกดยืนยันทันทีได้ (เดิม postback ของช่องนี้แย่งการกดปุ่ม) — ยอดคำนวณสดด้วย JS,
                                 server อ่านค่าจากฟอร์มแล้วคำนวณซ้ำทุกคำขอ --%>
                            <asp:TemplateField HeaderText="🐾 สัตว์เลี้ยง (ตัว)" Visible="false" HeaderStyle-CssClass="header-center ExampleFont rv-petcol" ItemStyle-CssClass="header-center ExampleFont rv-petcol">
                                <ItemTemplate>
                                    <asp:TextBox ID="txtPetCount" runat="server" Width="80px" Text='0' TextMode="Number" min="0" OnTextChanged="txtPetCount_TextChanged" CssClass="rounded-textbox ExampleFont"/>
                                    <asp:Label ID="lblPetInfo" runat="server" Text="" style="display:block; font-size:0.85em; color:#8D6E63; margin-top:3px;"></asp:Label>
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                        <HeaderStyle CssClass="header ExampleFont"></HeaderStyle>
                        <PagerStyle CssClass="pager ExampleFont"></PagerStyle>
                        <RowStyle CssClass="rows ExampleFont"></RowStyle>
                    </asp:GridView>
                </div>
            </div>

            <%-- เช่าอุปกรณ์เพิ่ม (ไม่บังคับ) — อยู่ในขั้นห้องพัก เพราะคิดราคาตามจำนวนคืนเดียวกัน --%>
            <div class="form-row rv-alt">
                <div class="form-label">เช่าอุปกรณ์เพิ่ม:<br />Rent Items:</div>
                <div class="form-controls">
                    <label class="rv-check" style="padding: 4px 0;">
                        <asp:CheckBox ID="CheckBox7" runat="server" AutoPostBack="True" OnCheckedChanged="CheckBox7_CheckedChanged" CssClass="mycheckbox"/>
                        <span>ต้องการเช่าอุปกรณ์ <span class="rv-muted">(ไม่บังคับ · optional)</span></span>
                    </label>
                </div>
            </div>

            <asp:Panel ID="Panel2" runat="server" Visible="false" CssClass="form-panel">
                <div class="form-row">
                    <div class="form-label">รูปภาพของเช่า:<br />Rent Items Picture:</div>
                    <div class="form-controls">
                        <asp:HyperLink ID="HyperLink1" runat="server" NavigateUrl="./Images/อุปกรณ์เช่า.png" Target="_blank" CssClass="reservation-button" style="display: inline-block; padding: 10px 14px; text-decoration: none;">กดเพื่อดูรูปภาพของเช่า (Click)</asp:HyperLink>
                    </div>
                </div>

                <div class="form-row rv-alt">
                    <div class="form-label">รายการของเช่า:<br />Rent Items:</div>
                    <div class="form-controls">
                        <asp:GridView ID="GridView2" runat="server" AutoGenerateColumns="False" CssClass="mydatagrid ExampleFont" PagerStyle-CssClass="pager" HeaderStyle-CssClass="header" RowStyle-CssClass="rows" OnRowCancelingEdit="GridView2_RowCancelingEdit" OnRowEditing="GridView2_RowEditing" OnRowUpdating="GridView2_RowUpdating">
                            <Columns>
                                <asp:TemplateField HeaderText="เลือก" HeaderStyle-Width="5%" HeaderStyle-CssClass="ExampleFont">
                                    <ItemTemplate>
                                        <asp:CheckBox ID="chkSelect" runat="server" Width="100%" CommandName="Check" AutoPostBack="true"/>
                                    </ItemTemplate>
                                    <HeaderStyle Width="5%"></HeaderStyle>
                                </asp:TemplateField>
                                <asp:BoundField DataField="ItemName" HeaderText="รายการของเช่า" HeaderStyle-CssClass="header-center ExampleFont" ItemStyle-CssClass="ExampleFont" ReadOnly="true">
                                    <HeaderStyle CssClass="header-center"></HeaderStyle>
                                    <ItemStyle CssClass="ExampleFont"></ItemStyle>
                                </asp:BoundField>
                                <asp:TemplateField HeaderText="จำนวนที่ต้องการเช่า" HeaderStyle-Width="20%" HeaderStyle-CssClass="header-center ExampleFont" ItemStyle-CssClass="header-center ExampleFont">
                                    <ItemTemplate>
                                        <asp:TextBox ID="txtAmount" runat="server" Width="100%" Text='0' TextMode="Number" Enabled="false" AutoPostBack="true" CssClass="rounded-textbox ExampleFont"/>
                                    </ItemTemplate>
                                    <HeaderStyle Width="20%"></HeaderStyle>
                                    <ItemStyle CssClass="header-center"></ItemStyle>
                                </asp:TemplateField>
                                <asp:BoundField DataField="Amount" HeaderText="จำนวนคงเหลือ" HeaderStyle-CssClass="header-center ExampleFont" ItemStyle-CssClass="header-center ExampleFont" ReadOnly="true">
                                    <HeaderStyle CssClass="header-center"></HeaderStyle>
                                    <ItemStyle CssClass="header-center"></ItemStyle>
                                </asp:BoundField>
                                <asp:BoundField DataField="Price" HeaderText="ราคาต่อชิ้น" HeaderStyle-CssClass="header-center ExampleFont" ItemStyle-CssClass="header-center ExampleFont">
                                    <HeaderStyle CssClass="header-center"></HeaderStyle>
                                    <ItemStyle CssClass="header-center"></ItemStyle>
                                </asp:BoundField>
                                <asp:CommandField ButtonType="Button" HeaderText="แก้ไข" ShowEditButton="True" ControlStyle-CssClass="ExampleFont" HeaderStyle-CssClass="header-center ExampleFont" ItemStyle-CssClass="header-center ExampleFont">
                                    <ControlStyle CssClass="ExampleFont"></ControlStyle>
                                    <HeaderStyle CssClass="header-center ExampleFont"></HeaderStyle>
                                    <ItemStyle CssClass="header-center ExampleFont"></ItemStyle>
                                </asp:CommandField>
                            </Columns>
                            <HeaderStyle CssClass="header ExampleFont" Font-Names="Chulabhorn Likit Text Medium"></HeaderStyle>
                            <PagerStyle CssClass="pager ExampleFont"></PagerStyle>
                            <RowStyle CssClass="rows ExampleFont" Font-Names="Chulabhorn Likit Text"></RowStyle>
                        </asp:GridView>
                    </div>
                </div>
            </asp:Panel>
        </div>

        <%-- ══ ขั้นที่ 2: ข้อมูลผู้เข้าพัก ══ --%>
        <div class="form-panel rv-step" id="rvStepGuest">
            <h3 class="section-header rv-step-h">ข้อมูลผู้เข้าพัก <small>Guest Information</small></h3>
            <div class="form-row rv-alt">
                <div class="form-label">เบอร์โทรศัพท์:<span class="required-field">*</span><br />Telephone Number:<span class="required-field">*</span></div>
                <div class="form-controls">
                    <asp:TextBox ID="TextBox1" runat="server" Width="220px" AutoPostBack="True" OnTextChanged="TextBox1_TextChanged" CssClass="rounded-textbox" TextMode="Phone" placeholder="เช่น 0812345678"></asp:TextBox>
                    <asp:Button ID="Button5" runat="server" OnClick="Button5_Click" Text="Button" Visible="False" CssClass="reservation-button" style="margin-left: 10px;"/>
                </div>
            </div>

            <div class="form-row">
                <div class="form-label">ชื่อ-นามสกุล หรือ ชื่อบริษัท:<span class="required-field">*</span><br />Full Name:<span class="required-field">*</span></div>
                <div class="form-controls">
                    <asp:DropDownList ID="DropDownList8" runat="server" Width="150px" AutoPostBack="True" OnSelectedIndexChanged="DropDownList8_SelectedIndexChanged" CssClass="rounded-textbox">
                    </asp:DropDownList>
                    <asp:TextBox ID="TextBox2" runat="server" Width="250px" CssClass="rounded-textbox" style="margin-left: 10px;" placeholder="ชื่อ นามสกุล (Full name)"></asp:TextBox>
                    <asp:TextBox ID="TextBox18" runat="server" Width="100px" PlaceHolder="รหัสสาขา" Visible="false" Text="00000" CssClass="rounded-textbox" style="margin-left: 10px;"></asp:TextBox>
                </div>
            </div>

            <div class="form-row rv-alt">
                <div class="form-label">ชื่อ Facebook หรือ ID Line:<br />Facebook Name/Line ID:</div>
                <div class="form-controls">
                    <asp:TextBox ID="TextBox3" runat="server" Width="300px" CssClass="rounded-textbox"></asp:TextBox>
                </div>
            </div>

            <div class="form-row">
                <div class="form-label">ใบกำกับภาษี:<br />Tax Invoice:</div>
                <div class="form-controls">
                    <asp:CheckBox ID="CheckBox3" runat="server" Text="ไม่รับใบกำกับภาษี (No tax invoice needed)" AutoPostBack="True" Checked="True" OnCheckedChanged="CheckBox3_CheckedChanged" CssClass="mycheckbox" />
                    <asp:CheckBox ID="CheckBox4" runat="server" Text="ไม่ออกใบกำกับภาษีในระบบ !!!" Visible="false" OnCheckedChanged="CheckBox4_CheckedChanged" AutoPostBack="True" CssClass="mycheckbox" style="margin-left: 20px;"/>
                </div>
            </div>

            <asp:Panel ID="Panel1" runat="server" Visible="False" CssClass="form-panel">
                <div class="form-row rv-alt">
                    <div class="form-label">ที่อยู่<br />Address:</div>
                    <div class="form-controls">
                        <asp:TextBox ID="TextBox8" runat="server" Width="100px" PlaceHolder="เลขที่" CssClass="rounded-textbox"></asp:TextBox>
                        <asp:TextBox ID="TextBox17" runat="server" Width="250px" PlaceHolder="หมู่ ซอย" CssClass="rounded-textbox" style="margin-left: 10px;"></asp:TextBox>
                    </div>
                </div>

                <div class="form-row">
                    <div class="form-label">รหัสไปรษณีย์<br />Zip Code:</div>
                    <div class="form-controls">
                        <asp:TextBox ID="TextBox16" CssClass="rounded-textbox" runat="server" Width="100px" AutoPostBack="True" OnTextChanged="TextBox16_TextChanged" TextMode="Number"></asp:TextBox>
                        <asp:Button ID="Button6" runat="server" Text="ค้นหา" Width="80px" OnClick="Button6_Click" CssClass="reservation-button" style="margin-left: 10px;"/>
                        <asp:Button ID="Button7" runat="server" Text="ยกเลิก" Width="80px" OnClick="Button7_Click" CssClass="reservation-button" style="margin-left: 10px;"/>
                    </div>
                </div>

                <div class="form-row rv-alt">
                    <div class="form-label">จังหวัด/อำเภอ/ตำบล</div>
                    <div class="form-controls">
                        <asp:DropDownList ID="DropDownList5" runat="server" Width="25%" AutoPostBack="True" OnSelectedIndexChanged="DropDownList5_SelectedIndexChanged" CssClass="rounded-textbox">
                        </asp:DropDownList>
                        <asp:DropDownList ID="DropDownList6" runat="server" Width="25%" AutoPostBack="True" OnSelectedIndexChanged="DropDownList6_SelectedIndexChanged" CssClass="rounded-textbox" style="margin-left: 10px;">
                        </asp:DropDownList>
                        <asp:DropDownList ID="DropDownList7" runat="server" Width="25%" AutoPostBack="true" CssClass="rounded-textbox" style="margin-left: 10px;">
                        </asp:DropDownList>
                    </div>
                </div>

                <div class="form-row">
                    <div class="form-label">เลขบัตรประชาชน<br />Passport Number:</div>
                    <div class="form-controls">
                        <asp:TextBox ID="TextBox9" runat="server" Width="300px" AutoPostBack="True" OnTextChanged="TextBox9_TextChanged" CssClass="rounded-textbox"></asp:TextBox>
                    </div>
                </div>

                <div class="form-row rv-alt">
                    <div class="form-label">อีเมล<br />Email:</div>
                    <div class="form-controls">
                        <asp:TextBox ID="TextBox13" runat="server" Width="300px" AutoPostBack="True" OnTextChanged="TextBox9_TextChanged" TextMode="Email" CssClass="rounded-textbox"></asp:TextBox>
                        <asp:CheckBox ID="CheckBox5" runat="server" AutoPostBack="True" Text="ต้องการรับ e tax invoice" Visible="False" OnCheckedChanged="CheckBox5_CheckedChanged" CssClass="mycheckbox" style="margin-left: 10px;"/>
                    </div>
                </div>
            </asp:Panel>

            <div class="form-row">
                <div class="form-label">หมายเหตุ / คำขอพิเศษ:<br />Remark / Special requests:</div>
                <div class="form-controls">
                    <asp:TextBox ID="TextBox6" runat="server" Width="90%" TextMode="MultiLine" Rows="3" CssClass="rounded-textbox" placeholder="เช่น เวลาที่คาดว่าจะมาถึง, ต้องการเตียงเสริม (Arrival time, extra bed ...)"></asp:TextBox>
                </div>
            </div>
        </div>

        <%-- ══ ขั้นที่ 3: 🐾 สัตว์เลี้ยงเข้าพัก (ตั้งค่าที่ ศูนย์ตั้งค่า → สัตว์เลี้ยงเข้าพัก) ══
             แสดงเฉพาะเมื่อเปิดฟีเจอร์ และห้องที่เลือกอย่างน้อย 1 ห้องรับสัตว์เลี้ยง — ปิดฟีเจอร์ = ไม่มีอะไรโผล่ (เลขขั้นถัดไปเลื่อนเอง)
             ค่าบริการลงเป็นรายการ "ค่าบริการสัตว์เลี้ยง" แยกจากค่าห้อง (บวกเข้ายอดสรุปแล้ว)
             นโยบายสัตว์เลี้ยง + ติ๊กยอมรับ อยู่ในขั้น "ยืนยันเงื่อนไข" (pnlPetPolicy) --%>
        <asp:Panel ID="pnlPetStay" runat="server" Visible="false" CssClass="form-panel rv-step rv-pet-step">
            <h3 class="section-header rv-step-h">สัตว์เลี้ยง <small>Pets (if any)</small></h3>
            <label class="rv-check rv-check-lg">
                <asp:CheckBox ID="chkHasPet" runat="server" AutoPostBack="true" OnCheckedChanged="chkHasPet_CheckedChanged" CssClass="mycheckbox" />
                <span>🐾 มีสัตว์เลี้ยงเข้าพัก (Bringing pets)</span>
            </label>
            <%-- จำนวนต่อห้อง: JS ย้ายช่องกรอกจากตารางห้องพักมาไว้ที่นี่ (ไม่มี JS = กรอกในคอลัมน์ 🐾 ของตาราง) --%>
            <div id="rvPetRooms" class="rv-pet-rooms" style="display:none;"></div>
            <asp:Literal ID="litPetSummary" runat="server" />
            <asp:Panel ID="pnlPetDetail" runat="server" Visible="false" style="margin-top:8px;">
                <span style="font-weight:600;">รายละเอียดสัตว์เลี้ยง (ชนิด / สายพันธุ์ / น้ำหนัก) <span class="rv-muted">Pet details</span>:</span>
                <asp:TextBox ID="txtPetNotes" runat="server" Width="100%" MaxLength="500" CssClass="rounded-textbox"
                    placeholder="เช่น สุนัขพันธุ์ชิสุ 1 ตัว 6 กก. / แมว 1 ตัว" />
            </asp:Panel>
        </asp:Panel>

        <!-- 🏨 Product Charges Section (for existing reservations) -->
        <div class="form-panel" id="divProductCharges" runat="server" visible="false">
            <h3 class="section-header">📦 รายการสินค้าที่ชาร์จเข้าห้อง / Product Charges</h3>

            <div style="margin-bottom: 15px;">
                <asp:Label ID="lblProductChargesSummary" runat="server" CssClass="price-display"
                    style="background-color: #FFF3CD; padding: 10px; border-radius: 5px; display: inline-block; border: 1px solid #FFC107;"></asp:Label>
            </div>

            <style>
                .product-charges-grid {
                    width: 100%;
                    border-collapse: collapse;
                    background: white;
                    border: 1px solid #D7CCC8;
                    margin: 10px 0;
                }
                .product-charges-grid th {
                    background-color: #8D6E63;
                    color: white;
                    font-weight: bold;
                    padding: 12px;
                    text-align: center;
                    border: 1px solid #6D4C41;
                }
                .product-charges-grid td {
                    padding: 10px;
                    border: 1px solid #D7CCC8;
                    text-align: center;
                }
                .product-charges-grid tr:nth-child(even) {
                    background-color: #F5F5F5;
                }
                .product-charges-grid tr:hover {
                    background-color: #EFEBE9;
                }
                .status-badge {
                    padding: 5px 12px;
                    border-radius: 4px;
                    color: white;
                    font-size: 12px;
                    font-weight: bold;
                    display: inline-block;
                }
                .status-pending {
                    background-color: #FF9800;
                }
                .status-paid {
                    background-color: #4CAF50;
                }
                .status-cancelled {
                    background-color: #9E9E9E;
                }
            </style>

            <asp:GridView ID="gvProductCharges" runat="server" CssClass="product-charges-grid ExampleFont"
                AutoGenerateColumns="False" EmptyDataText="ไม่มีรายการสินค้าที่ชาร์จเข้าห้อง"
                OnRowCommand="gvProductCharges_RowCommand">
                <Columns>
                    <asp:BoundField DataField="ChargedDate" HeaderText="วันที่"
                        DataFormatString="{0:dd/MM/yyyy HH:mm}" ItemStyle-Width="15%" />
                    <asp:BoundField DataField="Product_Name" HeaderText="รายการสินค้า"
                        ItemStyle-HorizontalAlign="Left" ItemStyle-Width="30%" />
                    <asp:BoundField DataField="Quantity" HeaderText="จำนวน"
                        DataFormatString="{0:N2}" ItemStyle-Width="10%" />
                    <asp:BoundField DataField="UnitPrice" HeaderText="ราคา/หน่วย"
                        DataFormatString="{0:N2} บาท" ItemStyle-Width="12%" />
                    <asp:BoundField DataField="TotalAmount" HeaderText="รวม"
                        DataFormatString="{0:N2} บาท" ItemStyle-Width="12%"
                        ItemStyle-Font-Bold="true" />
                    <asp:TemplateField HeaderText="สถานะ" ItemStyle-Width="12%">
                        <ItemTemplate>
                            <span class='status-badge <%# "status-" + Eval("Status").ToString().ToLower() %>'>
                                <%# Eval("Status").ToString() == "PENDING" ? "รอชำระ" :
                                    Eval("Status").ToString() == "PAID" ? "ชำระแล้ว" : "ยกเลิก" %>
                            </span>
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="จัดการ" ItemStyle-Width="9%">
                        <ItemTemplate>
                            <asp:Button ID="btnDeleteCharge" runat="server"
                                Text="ลบ" CssClass="reservation-button"
                                style="background-color: #f44336; padding: 6px 12px;"
                                CommandName="DeleteCharge"
                                CommandArgument='<%# Eval("ID") %>'
                                Visible='<%# Eval("Status").ToString() == "PENDING" %>' />
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
            </asp:GridView>

            <div style="margin-top: 10px; padding: 10px; background-color: #E3F2FD; border-radius: 5px; border-left: 4px solid #2196F3;">
                <strong>💡 คำอธิบาย:</strong><br />
                • <strong>รอชำระ:</strong> รายการที่ยังไม่ได้ชำระเงิน (สามารถลบได้)<br />
                • <strong>ชำระแล้ว:</strong> รายการที่ชำระเงินแล้ว (ไม่สามารถลบได้)<br />
                • การลบรายการจะ<strong>คืนสต๊อกสินค้า</strong>และ<strong>ลดยอดรวม</strong>โดยอัตโนมัติ
            </div>
        </div>

        <%-- ══ ขั้นที่ 4: สรุปยอด ══ --%>
        <div class="form-panel rv-step" id="rvStepSummary">
            <h3 class="section-header rv-step-h">สรุปยอด <small>Price Summary</small></h3>
            <div class="form-row rv-alt">
                <div class="form-label">รหัสส่วนลด:<br />Coupon Code:</div>
                <div class="form-controls">
                    <asp:TextBox ID="TextBox19" runat="server" Width="200px" CssClass="rounded-textbox" placeholder="ถ้ามี (optional)"></asp:TextBox>
                    <asp:Button ID="Button8" runat="server" Text="Submit" Width="100px" OnClick="Button8_Click" CssClass="reservation-button" style="margin-left: 10px;"/>
                </div>
            </div>

            <!-- 🎁 Loyalty Discount Display -->
            <asp:Panel ID="pnlLoyaltyDiscount" runat="server" Visible="false" CssClass="loyalty-discount-panel">
                <div class="form-row" style="background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 15px; border-radius: 8px; margin-bottom: 15px;">
                    <div style="display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 10px; width: 100%;">
                        <div style="flex: 1 1 180px; min-width: 0;">
                            <div style="font-size: 18px; font-weight: bold; margin-bottom: 8px;">
                                <i class="fa fa-star" style="color: #FFD700;"></i>
                                <asp:Label ID="lblLoyaltyTierName" runat="server" Text=""></asp:Label>
                                Member Discount
                            </div>
                            <div style="font-size: 14px; opacity: 0.9;">
                                ส่วนลดสมาชิก <asp:Label ID="lblDiscountPercent" runat="server" Text=""></asp:Label>%
                            </div>
                        </div>
                        <div style="text-align: right;">
                            <div style="font-size: 14px; opacity: 0.9; text-decoration: line-through;">
                                ราคาเดิม: ฿<asp:Label ID="lblOriginalPrice" runat="server" Text="0.00"></asp:Label>
                            </div>
                            <div style="font-size: 24px; font-weight: bold; color: #FFD700; margin-top: 5px;">
                                -฿<asp:Label ID="lblDiscountAmount" runat="server" Text="0.00"></asp:Label>
                            </div>
                        </div>
                    </div>
                </div>
            </asp:Panel>

            <%-- ══ ใบจอง OTA ที่ระบบยังไม่แน่ใจว่าใครเก็บค่าห้อง (โผล่เฉพาะโหมดเช็คอิน) ══
                 ต้องให้พนักงานตรวจอีเมล OTA แล้วเลือกก่อนเช็คอิน + ระบุเหตุผล
                 "OTA เก็บเงินแล้ว" (หยุดเก็บเงินลูกค้า) ใช้ได้เฉพาะ Admin/Owner --%>
            <asp:Panel ID="pnlCollectMode" runat="server" Visible="false" CssClass="form-row"
                style="display:block; background:#FFF8E1; border:1px solid #FFB300; border-radius:10px; padding:14px; margin:10px 0;">
                <div style="font-weight:bold; color:#E65100; margin-bottom:6px;">
                    ⚪ ยังไม่ชัดว่าใครเก็บค่าห้อง — ตรวจอีเมล OTA / Extranet แล้วเลือกก่อนเช็คอิน
                </div>
                <div style="margin-bottom:8px;">
                    <span style="font-weight:600;">เหตุผล (บังคับ):</span>
                    <asp:TextBox ID="txtCollectReason" runat="server" Width="100%" MaxLength="500"
                        CssClass="rounded-textbox" placeholder="เช่น อีเมล Agoda แจ้ง Prepaid / ลูกค้าแจ้งว่าจ่ายที่โรงแรม" />
                </div>
                <div style="display:flex; gap:10px; flex-wrap:wrap;">
                    <asp:Button ID="btnCollectChannel" runat="server" Text="OTA เก็บเงินแล้ว"
                        OnClick="btnCollectChannel_Click" CausesValidation="false" CssClass="reservation-button" />
                    <asp:Button ID="btnCollectHotel" runat="server" Text="เก็บเงินหน้างาน"
                        OnClick="btnCollectHotel_Click" CausesValidation="false" CssClass="reservation-button" />
                </div>
                <asp:Label ID="lblCollectModeNote" runat="server" Text=""
                    style="display:block; margin-top:6px; font-size:0.9em; color:#8D6E63;"></asp:Label>
                <asp:Literal ID="litCollectModeMsg" runat="server" />
            </asp:Panel>

            <%-- การ์ดสรุปยอด (ลูกค้าจองเอง): ค่าห้อง + ของเช่า + ค่าสัตว์เลี้ยง = รวม, มัดจำขั้นต่ำ — สร้างตอน PreRender --%>
            <asp:Literal ID="litPriceSummary" runat="server" />

            <div class="form-row rv-total-row">
                <div class="form-label">ราคารวม:<br />Total price:</div>
                <div class="form-controls">
                    <asp:TextBox ID="TextBox4" runat="server" Enabled="False"  Width="200px" TextMode="Number" CssClass="rounded-textbox price-display">0</asp:TextBox>
                    <span style="margin-left: 10px;">บาท</span>
                    <asp:Label ID="lblAfterDiscount" runat="server" Text="" Visible="false"
                        style="margin-left: 10px; color: #27ae60; font-weight: bold;">
                        (หลังหักส่วนลดสมาชิก)
                    </asp:Label>
                    <%-- ใบจอง OTA: ป้ายใครเก็บเงิน + ยอดตามอีเมล OTA (ว่างสำหรับใบจองปกติ) --%>
                    <asp:Literal ID="litOtaTotalInfo" runat="server" />
                    <div style="margin-top: 5px;">
                        ยอดมัดจำจองขั้นต่ำ Minimum Deposit:
                        <asp:Label ID="Label2" runat="server" Text="" CssClass="price-display"></asp:Label>
                    </div>
                </div>
            </div>
        </div>

        <%-- ══ ขั้นที่ 5: การชำระเงิน ══ --%>
        <div class="form-panel rv-step" id="rvStepPay">
            <h3 class="section-header rv-step-h">การชำระเงิน <small>Payment</small></h3>
            <div class="form-row rv-alt">
                <div class="form-label">วิธีชำระเงิน:<br />Payment Method:</div>
                <div class="form-controls">
                    <asp:DropDownList ID="DropDownList2" Width="320px" runat="server" CssClass="rounded-textbox" Enabled="False" AutoPostBack="True" OnSelectedIndexChanged="DropDownList2_SelectedIndexChanged" AppendDataBoundItems="true">
                    </asp:DropDownList>
                    <asp:SqlDataSource ID="SqlDataSource1" runat="server" ConnectionString="<%$ ConnectionStrings:TaketimeConnectionString %>" SelectCommand="SELECT * FROM [Account_Paid_How] WHERE ([Status] = 'True')">
                        <SelectParameters>
                            <asp:Parameter DefaultValue="True" Name="Status" Type="Boolean" />
                        </SelectParameters>
                    </asp:SqlDataSource>
                    <%-- รายละเอียดของช่องทางที่ลูกค้าเลือก (QR/บัญชี, เงื่อนไขบัตร, นโยบายยกเลิก) — ว่าง = ไม่แสดงอะไร --%>
                    <asp:Literal ID="litChannelInfo" runat="server" />
                </div>
            </div>

            <div class="form-row">
                <div class="form-label">ยอดเงินที่โอน/ชำระแล้ว:<br />Amount Paid:</div>
                <div class="form-controls">
                    <asp:TextBox ID="TextBox5" runat="server" AutoPostBack="True" Width="200px" TextMode="Number" OnTextChanged="TextBox5_TextChanged" CssClass="rounded-textbox" inputmode="decimal">0</asp:TextBox>
                    <span style="margin-left: 10px;">บาท</span>
                    <%-- ลูกค้า: แตะเพื่อใส่ยอดมัดจำขั้นต่ำ/เต็มจำนวนให้อัตโนมัติ (ตัวเลขอัปเดตสดจากการ์ดสรุปยอด) --%>
                    <div class="rv-quick rv-cust-only" id="rvQuick">
                        <button type="button" onclick="rvFillDeposit('min'); return false;">มัดจำขั้นต่ำ ฿<span data-rv="mindep">0</span></button>
                        <button type="button" onclick="rvFillDeposit('full'); return false;">ชำระเต็มจำนวน ฿<span data-rv="total">0.00</span></button>
                    </div>
                    <asp:Label ID="Label7" runat="server" Text="" Visible="false" style="display:block; margin-top:6px;"></asp:Label>
                    <%-- ใบจอง OTA ตอนเช็คอิน: "ไม่มียอดต้องเก็บ" / "ต้องเก็บ ฿…" --%>
                    <asp:Literal ID="litCheckinDueBanner" runat="server" />
                </div>
            </div>

            <div class="form-row">
                <div class="form-label">&nbsp;</div>
                <div class="form-controls">
                    <asp:CheckBox ID="CheckBox2" runat="server" Text="มัดจำเพิ่ม" AutoPostBack="True" OnCheckedChanged="CheckBox2_CheckedChanged" Visible="False" CssClass="mycheckbox"/>
                    <asp:TextBox ID="TextBox10" runat="server" TextMode="Number" Visible="False" AutoPostBack="True" CssClass="rounded-textbox" OnTextChanged="TextBox10_TextChanged" style="margin-left: 10px;">จำนวนเงิน</asp:TextBox>
                </div>
            </div>

            <%-- ══ ลูกค้าเลือกจ่ายด้วยบัตร/QR ทันที (โผล่เฉพาะเมื่อเปิดสวิตช์) ══
                 ติ๊กแล้วไม่ต้องโอน+แนบสลิป — กดยืนยันจองแล้วระบบพาไปหน้าจ่ายเงินต่อ
                 ปิดสวิตช์ = ทั้งบล็อกไม่แสดง หน้าจองทำงานเหมือนเดิมทุกประการ --%>
            <asp:Panel ID="pnlPayNow" runat="server" Visible="false" CssClass="form-row"
                style="display:block; background:#E8F5E9; border-radius:10px; padding:14px; margin:10px 0;">
                <label style="display:flex; align-items:flex-start; gap:10px; cursor:pointer;">
                    <asp:CheckBox ID="chkPayNow" runat="server" CssClass="mycheckbox"
                        AutoPostBack="true" OnCheckedChanged="chkPayNow_CheckedChanged" />
                    <span>
                        <b style="color:#2E7D32; font-size:1.05em;">💳 จ่ายด้วยบัตรเครดิต / QR ทันที (ไม่ต้องโอนและแนบสลิป)</b><br />
                        <span style="font-size:0.9em; color:#558B2F;">
                            กดยืนยันการจองแล้วระบบจะพาไปหน้าชำระเงินต่อ — จ่ายสำเร็จเมื่อไหร่ การจองยืนยันทันที
                            ไม่ต้องรอเจ้าหน้าที่ตรวจสลิป<br />
                            <b>ห้องจะถูกกันไว้ให้</b> ระหว่างรอชำระ หากไม่ชำระภายในเวลาที่กำหนด การจองจะถูกยกเลิกอัตโนมัติ
                        </span>
                    </span>
                </label>
            </asp:Panel>

            <div class="form-row rv-alt" id="rowSlip" runat="server">
                <div class="form-label">แนบสลิปการโอน:<br />Transfer Slip:</div>
                <div class="form-controls">
                    <div>
                        <asp:FileUpload ID="FileUpload1" runat="server" OnDataBinding="FileUpload1_DataBinding" Width="350px" CssClass="rounded-textbox" accept="image/*"/>
                        <asp:Button ID="Button3" runat="server" Text="อัปโหลดสลิป (Upload)" Width="170px" OnClick="Button3_Click" CssClass="reservation-button" style="margin-left: 10px;"/>
                    </div>
                    <div id="divSlipHint" runat="server" class="rv-hint">
                        *เลือกรูปสลิปแล้วกด "ยืนยันการจอง" ได้เลย หรือกดอัปโหลดเพื่อดูรูปก่อน — โอนยอดมัดจำขั้นต่ำ หรือโอนชำระเต็มจำนวนก็ได้ค่ะ
                    </div>
                    <div style="margin-top: 10px;">
                        <asp:Image ID="Image1" runat="server" Width="90%" style="max-width: 500px; border: 1px solid #D7CCC8; border-radius: 5px;"/>
                    </div>

                    <!-- Payment History GridView (shown in CheckIn/Edit/CheckOut modes) -->
                    <div style="margin-top: 20px;" id="divPaymentHistory" runat="server" visible="false">
                        <h4 style="color: #5D4037; margin-bottom: 10px;">📋 ประวัติการชำระเงิน</h4>

                        <style>
                            .payment-history-table {
                                width: 100%;
                                border-collapse: collapse;
                                background: white;
                                border: 1px solid #D7CCC8;
                            }
                            .payment-history-table th {
                                background-color: #5D4037;
                                color: white;
                                font-weight: bold;
                                padding: 10px;
                                text-align: left;
                                border: 1px solid #4E342E;
                            }
                            .payment-history-table td {
                                padding: 8px;
                                border: 1px solid #D7CCC8;
                            }
                            .payment-history-table tr:nth-child(even) {
                                background-color: #F5F5F5;
                            }
                            .payment-history-table tr:hover {
                                background-color: #EFEBE9;
                            }
                        </style>

                        <asp:GridView ID="gvPaymentHistory" runat="server" CssClass="payment-history-table"
                            AutoGenerateColumns="False" EmptyDataText="ยังไม่มีประวัติการชำระเงิน">
                            <Columns>
                                <asp:BoundField DataField="PaymentDate" HeaderText="วันที่ชำระ" DataFormatString="{0:dd/MM/yyyy HH:mm}" />
                                <asp:BoundField DataField="PaymentAmount" HeaderText="จำนวนเงิน" DataFormatString="{0:N2}" ItemStyle-HorizontalAlign="Right" />
                                <asp:TemplateField HeaderText="ประเภท">
                                    <ItemTemplate>
                                        <span style="padding: 4px 8px; border-radius: 4px; background-color: #2196F3; color: white; font-size: 12px;">
                                            <%# Eval("PaymentType") %>
                                        </span>
                                    </ItemTemplate>
                                </asp:TemplateField>
                                <asp:BoundField DataField="PaymentMethod" HeaderText="วิธีชำระ" />
                                <asp:BoundField DataField="ReceiptNumber" HeaderText="เลขที่ใบเสร็จ" />
                                <asp:TemplateField HeaderText="สลิป">
                                    <ItemTemplate>
                                        <%# GetSlipLink(Eval("SlipFileURL")) %>
                                    </ItemTemplate>
                                </asp:TemplateField>
                                <asp:TemplateField HeaderText="สถานะ">
                                    <ItemTemplate>
                                        <span style="padding: 4px 8px; border-radius: 4px; background-color: <%# Eval("Status").ToString() == "COMPLETED" ? "#4CAF50" : "#FF9800" %>; color: white; font-size: 12px;">
                                            <%# Eval("Status").ToString() == "COMPLETED" ? "สำเร็จ" : "รอดำเนินการ" %>
                                        </span>
                                    </ItemTemplate>
                                </asp:TemplateField>
                                <asp:BoundField DataField="ProcessedBy" HeaderText="ผู้ทำรายการ" />
                            </Columns>
                        </asp:GridView>
                    </div>
                </div>
            </div>

            <%-- ══ เก็บเงินออนไลน์ + เงินประกัน (โผล่เฉพาะโหมดเช็คอิน และเมื่อเปิดฟีเจอร์) ══
                 ส่วนนี้เป็นส่วนเสริมล้วน ๆ ไม่แตะตรรกะบันทึกจอง/เช็คอินเดิมเลย
                 ปิดฟีเจอร์เมื่อไหร่ pnlOnlinePay จะ Visible=false ทั้งบล็อก --%>
            <asp:Panel ID="pnlOnlinePay" runat="server" Visible="false" CssClass="form-row"
                style="display:block; background:#F1F8E9; border-radius:10px; padding:16px; margin-top:14px;">
                <h4 style="color:#33691E; margin:0 0 4px;">💳 เก็บเงินออนไลน์ (ลูกค้าสแกน/กรอกบัตรเอง)</h4>
                <div style="font-size:0.9em; color:#7CB342; margin-bottom:12px;">
                    ไม่ต้องรอสลิป — ระบบรู้ผลเอง แล้วลงบัญชีให้อัตโนมัติ
                </div>

                <div style="display:flex; gap:10px; flex-wrap:wrap; align-items:center;">
                    <span style="font-weight:600;">ยอดที่จะเก็บ</span>
                    <asp:TextBox ID="txtPayAmount" runat="server" TextMode="Number" Width="150px"
                        CssClass="rounded-textbox" />
                    <span>บาท</span>
                    <asp:Button ID="btnMakePayLink" runat="server" Text="สร้าง QR / ลิงก์ให้ลูกค้าจ่าย"
                        OnClick="btnMakePayLink_Click" CausesValidation="false" CssClass="reservation-button" />
                </div>

                <asp:Panel ID="pnlPayLink" runat="server" Visible="false" style="margin-top:14px;">
                    <div style="display:flex; gap:20px; flex-wrap:wrap; align-items:flex-start;">
                        <div id="rvPayQr" style="background:#fff; padding:10px; border-radius:8px;"></div>
                        <div style="flex:1 1 220px; min-width:0;">
                            <label style="font-weight:600; font-size:0.9em;">ลิงก์สำหรับลูกค้า (ส่งทางแชทได้)</label>
                            <div style="display:flex; gap:6px;">
                                <asp:TextBox ID="txtPayLinkUrl" runat="server" ReadOnly="true" Width="100%"
                                    CssClass="rounded-textbox" />
                                <button type="button" onclick="rvCopy('<%= txtPayLinkUrl.ClientID %>',this)"
                                    style="padding:8px 14px;border:0;border-radius:8px;background:#558B2F;color:#fff;cursor:pointer;min-height:44px;">คัดลอก</button>
                            </div>
                            <div style="font-size:0.85em; color:#7CB342; margin-top:6px;">
                                ลิงก์นี้<b>ไม่หมดอายุ</b> — เปิดวันไหนระบบคิดยอดคงเหลือ ณ ตอนนั้นให้เอง
                            </div>
                            <div id="rvPayStatus" style="margin-top:10px; font-weight:600; color:#F57F17;">⏳ รอลูกค้าชำระเงิน…</div>
                        </div>
                    </div>
                </asp:Panel>

                <%-- ── เงินประกันความเสียหาย ── --%>
                <asp:Panel ID="pnlDeposit" runat="server" Visible="false"
                    style="margin-top:18px; border-top:1px dashed #C5E1A5; padding-top:14px;">
                    <h4 style="color:#33691E; margin:0 0 4px;">🛡 เงินประกันความเสียหาย</h4>
                    <div style="font-size:0.9em; color:#7CB342; margin-bottom:12px;">
                        โอน = ลูกค้าโอนเข้าบัญชีโรงแรม บันทึกเลขอ้างอิงไว้ในระบบ (นอกเกตเวย์) ·
                        เงินสด = บันทึกรับไว้ในระบบ · เช็คเอาท์ค่อยคืนหรือหักเฉพาะที่เสียหายจริง
                    </div>
                    <div style="display:flex; gap:10px; flex-wrap:wrap; align-items:center;">
                        <asp:DropDownList ID="ddlDepositMethod" runat="server" CssClass="rounded-textbox" Width="220px"
                            onchange="rvDepMode(this)">
                            <asp:ListItem Value="TRANSFER" Text="รับเงินประกันโดยโอน" />
                            <asp:ListItem Value="CASH" Text="รับเป็นเงินสด" />
                        </asp:DropDownList>
                        <asp:TextBox ID="txtDepositAmount" runat="server" TextMode="Number" Width="150px"
                            CssClass="rounded-textbox" />
                        <span>บาท</span>
                        <asp:Button ID="btnMakeDeposit" runat="server" Text="รับเงินประกัน"
                            OnClick="btnMakeDeposit_Click" CausesValidation="false" CssClass="reservation-button" />
                    </div>
                    <%-- ข้อมูลบัญชีรับโอน (จากแคตตาล็อกช่องทาง) + เลขอ้างอิงการโอน — แสดงเมื่อเลือก "โอน" --%>
                    <asp:Panel ID="pnlDepositTransfer" runat="server" Visible="false"
                        style="margin-top:10px; padding:10px 13px; border-radius:8px; background:#fff;">
                        <asp:Literal ID="litDepositTransferInfo" runat="server" />
                        <div style="display:flex; gap:8px; flex-wrap:wrap; align-items:center; margin-top:8px;">
                            <span style="font-weight:600; font-size:0.9em;">เลขอ้างอิงการโอน</span>
                            <asp:TextBox ID="txtDepositRef" runat="server" Width="280px" CssClass="rounded-textbox"
                                MaxLength="100" placeholder="เช่น เลขที่รายการ / เวลาโอน / ธนาคารผู้โอน" />
                        </div>
                    </asp:Panel>
                    <script>
                        function rvDepMode(sel) {
                            var p = document.getElementById('<%= pnlDepositTransfer.ClientID %>');
                            if (p) p.style.display = (sel && sel.value === 'TRANSFER') ? '' : 'none';
                        }
                        (function () {
                            var s = document.getElementById('<%= ddlDepositMethod.ClientID %>');
                            if (s) rvDepMode(s);
                        })();
                    </script>
                    <asp:Literal ID="litDepositMsg" runat="server" />
                    <asp:Panel ID="pnlDepositLink" runat="server" Visible="false" style="margin-top:14px;">
                        <div style="display:flex; gap:20px; flex-wrap:wrap; align-items:flex-start;">
                            <div id="rvHoldQr" style="background:#fff; padding:10px; border-radius:8px;"></div>
                            <div style="flex:1 1 220px; min-width:0;">
                                <label style="font-weight:600; font-size:0.9em;">ลิงก์ให้ลูกค้ากรอกบัตร (กันวงเงิน)</label>
                                <div style="display:flex; gap:6px;">
                                    <asp:TextBox ID="txtDepositLink" runat="server" ReadOnly="true" Width="100%"
                                        CssClass="rounded-textbox" />
                                    <button type="button" onclick="rvCopy('<%= txtDepositLink.ClientID %>',this)"
                                        style="padding:8px 14px;border:0;border-radius:8px;background:#558B2F;color:#fff;cursor:pointer;min-height:44px;">คัดลอก</button>
                                </div>
                                <div id="rvHoldStatus" style="margin-top:10px; font-weight:600; color:#F57F17;">⏳ รอลูกค้ากรอกบัตร…</div>
                            </div>
                        </div>
                    </asp:Panel>
                </asp:Panel>

                <input type="hidden" id="rvPayRef" value="<%= PayRefJs %>" />
                <input type="hidden" id="rvHoldRef" value="<%= HoldRefJs %>" />
                <input type="hidden" id="rvPayUrl" value="<%= PayUrlJs %>" />
                <input type="hidden" id="rvHoldUrl" value="<%= HoldUrlJs %>" />
            </asp:Panel>
        </div>

        <%-- ══ ขั้นที่ 6: ยืนยันเงื่อนไข — นโยบาย + กติกา + ติ๊กยอมรับ (รวมไว้ที่เดียว) ══
             ลูกค้าจองเอง: ต้องติ๊กครบทุกข้อ (ตรวจซ้ำฝั่ง server: ValidateCustomerBookingGate / ValidatePetStay)
             พนักงานลงจอง: ไม่มีอะไรในขั้นนี้ → PreRender ซ่อนทั้งขั้น --%>
        <div class="form-panel rv-step" id="divTermsStep" runat="server">
            <h3 class="section-header rv-step-h">ยืนยันเงื่อนไข <small>Terms &amp; Policies</small></h3>

            <%-- นโยบายการจอง (แก้ข้อความที่ ศูนย์ตั้งค่า → นโยบายการจอง) — แสดงเฉพาะลูกค้าในโหมดจองใหม่ --%>
            <asp:Panel ID="pnlPolicySection" runat="server" Visible="false" CssClass="rv-sub">
                <style>
                    .rv-pol-summary { background:#FFF8E1; border-left:4px solid #FFB300; border-radius:6px;
                                      padding:10px 14px; color:#5D4037; line-height:1.7; font-size:0.95em; }
                    .rv-pol-links { margin-top:10px; display:flex; flex-wrap:wrap; gap:8px; }
                    .rv-pol-links a { display:inline-flex; align-items:center; min-height:40px; padding:6px 14px; border-radius:20px; background:#fff;
                                      border:1px solid #D7CCC8; color:#5D4037; text-decoration:none; font-size:0.92em; }
                    .rv-pol-links a:hover { background:#D7CCC8; }
                    .rv-pol-overlay { display:none; position:fixed; left:0; top:0; right:0; bottom:0; z-index:10000;
                                      background:rgba(0,0,0,.55); align-items:center; justify-content:center; padding:12px; }
                    .rv-pol-box { background:#fff; border-radius:12px; width:100%; max-width:760px; max-height:88vh;
                                  display:flex; flex-direction:column; box-shadow:0 10px 40px rgba(0,0,0,.3); }
                    .rv-pol-tabs { display:flex; flex-wrap:wrap; gap:6px; padding:12px 14px 8px; border-bottom:1px solid #eee; }
                    .rv-pol-tabs button { border:0; border-radius:16px; padding:8px 12px; min-height:36px; background:#EFEBE9;
                                          color:#5D4037; cursor:pointer; font-size:0.9em; }
                    .rv-pol-tabs button.on { background:#5D4037; color:#fff; }
                    .rv-pol-body { overflow-y:auto; padding:12px 16px; line-height:1.75; color:#3E2723; font-size:0.95em; }
                    .rv-pol-pane h4 { margin:0 0 8px; color:#5D4037; }
                    .rv-pol-foot { padding:10px 14px; border-top:1px solid #eee; text-align:right; }
                </style>
                <asp:Literal ID="litPolicySummary" runat="server" />
                <div class="rv-pol-links">
                    <a href="#" onclick="rvPolicyOpen('terms');return false;">📜 ข้อกำหนดและเงื่อนไข</a>
                    <a href="#" onclick="rvPolicyOpen('privacy');return false;">🔒 นโยบายความเป็นส่วนตัว</a>
                    <a href="#" onclick="rvPolicyOpen('refund');return false;">💸 นโยบายการคืนเงิน</a>
                    <a href="#" onclick="rvPolicyOpen('cancel');return false;">📅 นโยบายการยกเลิก</a>
                </div>

                <div id="rvPolicyModal" class="rv-pol-overlay" onclick="if (event.target === this) rvPolicyClose();">
                    <div class="rv-pol-box" role="dialog" aria-modal="true">
                        <div class="rv-pol-tabs">
                            <button type="button" data-pol="terms" onclick="rvPolicyOpen('terms')">ข้อกำหนดและเงื่อนไข</button>
                            <button type="button" data-pol="privacy" onclick="rvPolicyOpen('privacy')">ความเป็นส่วนตัว</button>
                            <button type="button" data-pol="refund" onclick="rvPolicyOpen('refund')">การคืนเงิน</button>
                            <button type="button" data-pol="cancel" onclick="rvPolicyOpen('cancel')">การยกเลิก</button>
                        </div>
                        <div class="rv-pol-body">
                            <asp:Literal ID="litPolicyModal" runat="server" />
                        </div>
                        <div class="rv-pol-foot">
                            <button type="button" class="reservation-button" onclick="rvPolicyClose()" style="padding:8px 22px; min-height:44px;">ปิด / Close</button>
                        </div>
                    </div>
                </div>
                <script>
                    function rvPolicyOpen(key) {
                        var m = document.getElementById('rvPolicyModal');
                        if (!m) return;
                        var panes = m.querySelectorAll('.rv-pol-pane');
                        for (var i = 0; i < panes.length; i++)
                            panes[i].style.display = panes[i].getAttribute('data-pol') === key ? 'block' : 'none';
                        var tabs = m.querySelectorAll('.rv-pol-tabs button');
                        for (var j = 0; j < tabs.length; j++)
                            tabs[j].className = tabs[j].getAttribute('data-pol') === key ? 'on' : '';
                        m.style.display = 'flex';
                    }
                    function rvPolicyClose() {
                        var m = document.getElementById('rvPolicyModal');
                        if (m) m.style.display = 'none';
                    }
                    document.addEventListener('keydown', function (e) {
                        if (e.key === 'Escape' || e.keyCode === 27) rvPolicyClose();
                    });
                </script>
            </asp:Panel>

            <div class="rules-section" id="divRules" runat="server">
                <img src="./Images/กฏระเบียบ.png" width="90%" style="max-width: 800px;" alt="กติกาการเข้าพัก (Resort rules)"/>
            </div>

            <%-- ติ๊กยอมรับทั้งหมดอยู่ในกล่องเดียว — แต่ละข้อยังเป็นช่องแยก (บันทึกเวลา/ฉบับที่ยอมรับแยกกัน) --%>
            <div class="rv-accept">
                <div class="rv-accept-title">กรุณาอ่านและติ๊กยอมรับทุกข้อ <span class="rv-muted">(Please read and accept all)</span></div>

                <%-- ลูกค้าจองเอง: ต้องยอมรับนโยบายก่อน (บันทึกเวลา + ฉบับที่ยอมรับไว้บนใบจอง) --%>
                <asp:Panel ID="pnlPolicyAccept" runat="server" Visible="false">
                    <label class="rv-check">
                        <asp:CheckBox ID="chkAcceptPolicy" runat="server" OnCheckedChanged="chkAcceptPolicy_CheckedChanged" CssClass="mycheckbox"/>
                        <span>ข้าพเจ้าได้อ่านและยอมรับ
                            <a href="#" onclick="rvPolicyOpen('terms');return false;">ข้อกำหนดและเงื่อนไข</a>,
                            <a href="#" onclick="rvPolicyOpen('privacy');return false;">นโยบายความเป็นส่วนตัว</a>,
                            <a href="#" onclick="rvPolicyOpen('refund');return false;">นโยบายการคืนเงิน</a> และ
                            <a href="#" onclick="rvPolicyOpen('cancel');return false;">นโยบายการยกเลิกการจอง</a><span class="required-field">*</span>
                            <span class="rv-muted">(I accept the Terms &amp; Conditions, Privacy, Refund and Cancellation policies)</span></span>
                    </label>
                </asp:Panel>

                <label class="rv-check">
                    <asp:CheckBox ID="CheckBox1" runat="server" OnCheckedChanged="CheckBox1_CheckedChanged" CssClass="mycheckbox"/>
                    <span>ข้าพเจ้ายอมรับกติกาของรีสอร์ตด้านบน และรับทราบเรื่องการห้ามใช้เสียงดังหลัง 22.30 น.<span class="required-field">*</span>
                        <span class="rv-muted">(I accept the resort rules, including quiet hours after 22:30)</span></span>
                </label>

                <%-- 🐾 ลูกค้าจองเองที่มีสัตว์เลี้ยง: นโยบายสัตว์เลี้ยง + ต้องติ๊กยอมรับ (พนักงานลงจองไม่ต้อง) — ApplyPetStay() คุม --%>
                <asp:Panel ID="pnlPetPolicy" runat="server" Visible="false" style="border-top:1px dashed #EFEBE9; padding-top:10px;">
                    <div class="rv-pet-policy-box">
                        <asp:Literal ID="litPetPolicy" runat="server" />
                    </div>
                    <label class="rv-check" style="border-top:0;">
                        <asp:CheckBox ID="chkAcceptPetPolicy" runat="server" OnCheckedChanged="chkAcceptPetPolicy_CheckedChanged" CssClass="mycheckbox" />
                        <span style="color:#BF360C; font-weight:600;">ข้าพเจ้ายอมรับนโยบายการนำสัตว์เลี้ยงเข้าพัก<span class="required-field">*</span>
                            <span class="rv-muted">(I accept the Pet Policy)</span></span>
                    </label>
                </asp:Panel>
            </div>
        </div>

        <%-- ══ ปุ่มยืนยัน ══ --%>
        <div class="rv-submit">
            <%= SubmitRecapHtml %>
            <div id="rvGateHint" class="rv-gate-hint" style="display:none;" aria-live="polite"></div>
            <div id="rvFormError" class="rv-form-error" style="display:none;" role="alert"></div>
            <div>
                <asp:Button ID="Button1" runat="server" Text="ยืนยันการจอง (Confirm Booking)" OnClick="Button1_Click" OnClientClick="return preventDoubleSubmit();" Enabled="False" CssClass="reservation-button rv-submit-btn"/>
                <asp:Button ID="btnPostpone" runat="server" Text="เลื่อนเข้าพัก" Height="60px" Width="200px" OnClick="btnPostpone_Click" OnClientClick="return confirm('ยืนยันการเลื่อนเข้าพัก? วันเข้าพักจะถูกลบออก และการจองจะถูกย้ายไปรายการเลื่อนเข้าพัก');" Visible="False" CssClass="reservation-button" style="font-size: 1.2em; margin-left: 15px; background: linear-gradient(135deg, #f0ad4e 0%, #ec971f 100%); color: white;"/>
            </div>
            <div class="rv-submit-note rv-cust-only" style="justify-content:center;">กดยืนยันแล้วระบบจะบันทึกการจองและแสดงหน้ายืนยันการจองให้บันทึกเก็บไว้</div>
        </div>
    </div>

    <%-- กำลังบันทึก (แสดงหลังกดยืนยันการจอง) --%>
    <div id="rvBusy" class="rv-busy" role="status" aria-live="assertive">
        <div class="rv-busy-box">
            <div class="rv-spin"></div>
            <b>กำลังบันทึกการจอง…</b><br />
            <span class="rv-muted">กรุณาอย่าปิดหรือรีเฟรชหน้านี้ (Saving your booking, please wait)</span>
        </div>
    </div>

    <rsweb:reportviewer Visible="false" ID="ReportViewer2" runat="server" Width="100%" BackColor="" ClientIDMode="AutoID" HighlightBackgroundColor="" InternalBorderColor="204, 204, 204" InternalBorderStyle="Solid" InternalBorderWidth="1px" LinkActiveColor="" LinkActiveHoverColor="" LinkDisabledColor="" PrimaryButtonBackgroundColor="" PrimaryButtonForegroundColor="" PrimaryButtonHoverBackgroundColor="" PrimaryButtonHoverForegroundColor="" SecondaryButtonBackgroundColor="" SecondaryButtonForegroundColor="" SecondaryButtonHoverBackgroundColor="" SecondaryButtonHoverForegroundColor="" SplitterBackColor="" ToolbarDividerColor="" ToolbarForegroundColor="" ToolbarForegroundDisabledColor="" ToolbarHoverBackgroundColor="" ToolbarHoverForegroundColor="" ToolBarItemBorderColor="" ToolBarItemBorderStyle="Solid" ToolBarItemBorderWidth="1px" ToolBarItemHoverBackColor="" ToolBarItemPressedBorderColor="51, 102, 153" ToolBarItemPressedBorderStyle="Solid" ToolBarItemPressedBorderWidth="1px" ToolBarItemPressedHoverBackColor="153, 187, 226" Height="500px" OnClientClick="this.disabled = true; this.value = 'Processing...';" UseSubmitBehavior="false">
        <LocalReport ReportPath="Account\Report\Receipt.rdlc" EnableExternalImages="True">
        </LocalReport>
    </rsweb:reportviewer>

    <%-- ── ขั้นตอนการจอง: ย้ายช่องสัตว์เลี้ยง + ยอดสด + บอกว่ายังขาดอะไร + ข้อความใกล้ช่อง (ES5 — มือถือรุ่นเก่าใช้ได้) ──
         ส่วนแสดงผลล้วน: ทุกเงื่อนไข/ยอดเงินคำนวณซ้ำฝั่ง server ตอนกดยืนยันเสมอ --%>
    <script>
        var RV = {
            cust: <%= CustomerGateJs %>,
            livePets: <%= LivePetsJs %>,
            cur: { total: 0, minDep: 0 },
            ids: {
                grid: '<%= GridView1.ClientID %>',
                phone: '<%= TextBox1.ClientID %>',
                name: '<%= TextBox2.ClientID %>',
                total: '<%= TextBox4.ClientID %>',
                minDep: '<%= Label2.ClientID %>',
                amount: '<%= TextBox5.ClientID %>',
                file: '<%= FileUpload1.ClientID %>',
                slipImg: '<%= Image1.ClientID %>',
                rowSlip: '<%= rowSlip.ClientID %>',
                rules: '<%= CheckBox1.ClientID %>',
                policy: '<%= chkAcceptPolicy.ClientID %>',
                petPolicy: '<%= chkAcceptPetPolicy.ClientID %>',
                hasPet: '<%= chkHasPet.ClientID %>',
                submit: '<%= Button1.ClientID %>'
            }
        };

        (function () {
            function el(id) { return id ? document.getElementById(id) : null; }
            function num(v) {
                var s = (v === null || v === undefined) ? '' : String(v);
                s = s.replace(/,/g, '').replace(/^\s+|\s+$/g, '');
                var n = parseFloat(s);
                return isNaN(n) ? 0 : n;
            }
            function r2(n) { return Math.round(n * 100) / 100; }
            function ceilBaht(n) { return Math.ceil(r2(n) - 0.000001); }
            function groups(s) { return s.replace(/\B(?=(\d{3})+(?!\d))/g, ','); }
            function money(n) { var p = r2(n).toFixed(2).split('.'); return groups(p[0]) + '.' + p[1]; }
            function baht0(n) { return groups(String(Math.round(n))); }
            function hasCls(e, c) { return e && (' ' + e.className + ' ').indexOf(' ' + c + ' ') >= 0; }
            function addCls(e, c) { if (e && !hasCls(e, c)) e.className = (e.className ? e.className + ' ' : '') + c; }
            function rmCls(e, c) { if (e && hasCls(e, c)) e.className = (' ' + e.className + ' ').replace(' ' + c + ' ', ' ').replace(/^\s+|\s+$/g, ''); }
            function up(e, sel) {
                // closest() แบบเรียบง่าย (รองรับเบราว์เซอร์เก่า) — sel = ชื่อคลาสเดียว
                while (e && e.nodeType === 1) { if (hasCls(e, sel)) return e; e = e.parentNode; }
                return null;
            }
            function setAll(key, text) {
                var list = document.querySelectorAll('[data-rv="' + key + '"]');
                for (var i = 0; i < list.length; i++) list[i].textContent = text;
            }
            function goTo(target) {
                if (!target) return;
                try { target.scrollIntoView({ behavior: 'smooth', block: 'center' }); } catch (e1) { try { target.scrollIntoView(); } catch (e2) { } }
                setTimeout(function () {
                    try { if (target.focus && !target.disabled) target.focus({ preventScroll: true }); } catch (e3) { try { target.focus(); } catch (e4) { } }
                }, 350);
            }

            // ── 🐾 ย้ายช่องจำนวนสัตว์เลี้ยงจากตารางห้องพักมาไว้ในขั้น "สัตว์เลี้ยง" (ชื่อฟอร์มเดิม → server อ่านได้เหมือนเดิม) ──
            function movePets() {
                var host = document.getElementById('rvPetRooms');
                var grid = el(RV.ids.grid);
                if (!host || !grid || !grid.rows) return;
                var moved = 0;
                for (var r = 0; r < grid.rows.length; r++) {
                    var row = grid.rows[r];
                    var inp = row.querySelector('input[data-pet="1"]');
                    var info = row.querySelector('.rv-petinfo');
                    var src = inp || info;
                    if (!src || src.getAttribute('data-sel') !== '1') continue;
                    var line = document.createElement('div');
                    line.className = 'rv-pet-line';
                    var nm = document.createElement('div');
                    nm.className = 'rv-pet-room';
                    nm.textContent = src.getAttribute('data-room') || '';
                    line.appendChild(nm);
                    if (inp) {
                        var ctrl = document.createElement('div');
                        ctrl.className = 'rv-pet-ctrl';
                        ctrl.appendChild(inp);
                        var unit = document.createElement('span');
                        unit.textContent = 'ตัว';
                        ctrl.appendChild(unit);
                        var sub = document.createElement('span');
                        sub.className = 'rv-pet-sub';
                        ctrl.appendChild(sub);
                        inp.rvSub = sub;
                        line.appendChild(ctrl);
                    }
                    if (info) line.appendChild(info);
                    host.appendChild(line);
                    moved++;
                }
                if (moved > 0) {
                    host.style.display = '';
                    addCls(grid, 'rv-pets-moved');
                    var srv = document.querySelectorAll('.rv-pet-srvlines');
                    for (var s = 0; s < srv.length; s++) srv[s].style.display = 'none';
                }
            }

            function petInputs() { return document.querySelectorAll('input[data-pet="1"]'); }

            function clampPet(inp) {
                var max = parseInt(inp.getAttribute('data-max') || '0', 10);
                var n = parseInt(inp.value, 10);
                var line = up(inp, 'rv-pet-line') || inp.parentNode;
                if (inp.value !== '' && (isNaN(n) || n < 0)) { inp.value = '0'; }
                else if (max > 0 && n > max) {
                    inp.value = String(max);
                    showMsg(inp, 'ห้องนี้รับสัตว์เลี้ยงได้สูงสุด ' + max + ' ตัว', 'rv-err', line);
                    return;
                }
                clearMsgs(line);
            }

            function petFeeNow() {
                var fee = 0, list = petInputs();
                for (var i = 0; i < list.length; i++) {
                    var inp = list[i];
                    if (inp.getAttribute('data-sel') !== '1' || inp.disabled || !inp.getAttribute('data-fee')) continue;
                    var max = parseInt(inp.getAttribute('data-max') || '0', 10);
                    var n = parseInt(inp.value, 10);
                    if (isNaN(n) || n < 0) n = 0;
                    if (max > 0 && n > max) n = max;
                    var line = r2(n * num(inp.getAttribute('data-fee')) * num(inp.getAttribute('data-mult') || '1'));
                    fee += line;
                    if (inp.rvSub) inp.rvSub.textContent = n > 0 ? '= ฿' + money(line) : '';
                }
                return r2(fee);
            }

            // ── ยอดสด: ยอดในหน้า (คำนวณจาก server) ± ส่วนต่างค่าสัตว์เลี้ยงที่เปลี่ยนในเบราว์เซอร์ ──
            var base = null;
            function readBase() {
                var tb = el(RV.ids.total), lb = el(RV.ids.minDep), meta = document.getElementById('rvPetMeta');
                base = {
                    totalRaw: tb ? tb.value : '',
                    total0: tb ? num(tb.value) : 0,
                    minDepRaw: lb ? lb.textContent : '',
                    minDep0: lb ? num(lb.textContent) : 0,
                    fee0: meta ? num(meta.getAttribute('data-fee0')) : 0,
                    lock: !!(meta && meta.getAttribute('data-lock') === '1'),
                    mode: meta ? meta.getAttribute('data-mode') : '',
                    hasMeta: !!meta
                };
            }
            function refreshNumbers() {
                if (!base) readBase();
                var total = base.total0, minDep = base.minDep0, fee = base.fee0;
                if (RV.livePets && base.hasMeta && !base.lock) {
                    var hp = el(RV.ids.hasPet);
                    fee = (hp && hp.checked) ? petFeeNow() : 0;
                    var d = r2(fee - base.fee0);
                    total = r2(base.total0 + d);
                    if (base.mode === 'reserve') minDep = base.minDep0 + ceilBaht(fee) - ceilBaht(base.fee0);
                    var tb = el(RV.ids.total), lb = el(RV.ids.minDep);
                    if (tb) tb.value = Math.abs(d) < 0.005 ? base.totalRaw : total.toFixed(2);
                    if (lb && base.mode === 'reserve') lb.textContent = Math.abs(d) < 0.005 ? base.minDepRaw : String(minDep);
                }
                setAll('total', money(total));
                setAll('mindep', baht0(minDep));
                setAll('petfee', money(fee));
                RV.cur = { total: total, minDep: minDep };
                var q = document.getElementById('rvQuick');
                if (q) q.style.visibility = total > 0 ? 'visible' : 'hidden';
            }

            // ── ข้อความใกล้ช่อง (แทน alert) ──
            function msgAnchor(target) {
                if (!target) return null;
                if (hasCls(target, 'form-panel')) return target;   // ทั้งขั้น (เช่น ขั้นสัตว์เลี้ยง) → แสดงท้ายขั้นนั้น
                return up(target, 'rv-check') || up(target, 'rv-pet-line') || up(target, 'form-controls') || target.parentNode;
            }
            function clearMsgs(anchor) {
                if (!anchor) return;
                var host = hasCls(anchor, 'rv-check') ? anchor.parentNode : anchor;
                var old = host ? host.querySelectorAll('.rv-msg') : [];
                for (var i = 0; i < old.length; i++) old[i].parentNode.removeChild(old[i]);
            }
            function showMsg(target, msg, cls, anchorOverride) {
                var anchor = anchorOverride || msgAnchor(target);
                if (!anchor) return null;
                clearMsgs(anchor);
                var d = document.createElement('div');
                d.className = 'rv-msg ' + cls;
                d.textContent = msg;
                if (hasCls(anchor, 'rv-check') || hasCls(anchor, 'rv-pet-line')) anchor.parentNode.insertBefore(d, anchor.nextSibling);
                else anchor.appendChild(d);
                return d;
            }
            function markInvalid(target) {
                if (!target) return;
                var chk = target.type === 'checkbox' ? up(target, 'rv-check') : null;
                addCls(chk || target, 'rv-invalid');
            }
            function clearInvalid(target) {
                if (!target) return;
                var chk = target.type === 'checkbox' ? up(target, 'rv-check') : null;
                var box = chk || target;
                if (hasCls(box, 'rv-invalid')) {
                    rmCls(box, 'rv-invalid');
                    clearMsgs(msgAnchor(target));
                }
            }

            window.rvShowError = function (id, msg) {
                var target = el(id);
                var banner = document.getElementById('rvFormError');
                if (banner) { banner.textContent = msg; banner.style.display = ''; }
                if (target && target.offsetParent !== null) {
                    showMsg(target, msg, 'rv-err');
                    markInvalid(target);
                } else {
                    target = null;
                }
                var jump = function () { goTo(target || banner); };
                setTimeout(jump, 60);
                // MaintainScrollPositionOnPostback คืนตำแหน่งเดิมตอน window load → เลื่อนไปที่ข้อความอีกครั้งหลังจากนั้น
                if (document.readyState !== 'complete') {
                    window.addEventListener('load', function () { setTimeout(jump, 150); });
                }
            };
            window.rvShowOk = function (id, msg) {
                var target = el(id);
                if (target) showMsg(target, msg, 'rv-ok');
            };

            // ── ด่าน "ยังขาดอะไร" (ลูกค้าจองเอง) ──
            function gateMissing() {
                var miss = [];
                var grid = el(RV.ids.grid), anyRoom = false;
                if (grid) {
                    var cbs = grid.querySelectorAll('input[type="checkbox"]');
                    for (var i = 0; i < cbs.length; i++) if (cbs[i].checked && cbs[i].id.indexOf('chkSelect') >= 0) anyRoom = true;
                }
                if (!anyRoom) miss.push({ t: 'เลือกวันที่และห้องพัก (ขั้นที่ 1)', e: grid || document.getElementById('rvStepRoom') });
                var ph = el(RV.ids.phone);
                if (ph && ph.value.replace(/\s/g, '') === '') miss.push({ t: 'กรอกเบอร์โทรศัพท์', e: ph });
                var nm = el(RV.ids.name);
                if (nm && nm.value.replace(/\s/g, '') === '') miss.push({ t: 'กรอกชื่อ-นามสกุล', e: nm });
                var hp = el(RV.ids.hasPet);
                if (hp && hp.checked) {
                    var pets = petInputs(), cnt = 0, sum = 0, first = null;
                    for (var p = 0; p < pets.length; p++) {
                        if (pets[p].getAttribute('data-sel') !== '1' || pets[p].disabled) continue;
                        cnt++; if (!first) first = pets[p];
                        sum += parseInt(pets[p].value, 10) || 0;
                    }
                    if (cnt > 0 && sum <= 0) miss.push({ t: 'ระบุจำนวนสัตว์เลี้ยง (หรือเอาเครื่องหมาย "มีสัตว์เลี้ยง" ออก)', e: first });
                }
                if (el(RV.ids.rowSlip)) {
                    // โอนเงินเอง: ยอดโอน + สลิป (ช่องทางชำระออนไลน์ไม่มีแถวนี้ — ไปจ่ายต่อหลังยืนยัน)
                    var amt = el(RV.ids.amount);
                    var a = amt ? num(amt.value) : 0, md = RV.cur ? RV.cur.minDep : 0;
                    if (amt && !amt.disabled && (a <= 0 || (md > 0 && a < md * 0.8)))
                        miss.push({ t: 'ระบุยอดเงินที่โอน (มัดจำขั้นต่ำ ฿' + baht0(md) + ')', e: amt });
                    var f = el(RV.ids.file), img = el(RV.ids.slipImg);
                    var hasSlip = (f && f.value) || (img && img.getAttribute('data-uploaded') === '1');
                    if (!hasSlip && f) miss.push({ t: 'แนบสลิปการโอนเงิน', e: f });
                }
                var rc = el(RV.ids.rules);
                if (rc && !rc.checked) miss.push({ t: 'ติ๊กยอมรับกติกาของรีสอร์ต', e: rc });
                var pc = el(RV.ids.policy);
                if (pc && !pc.checked) miss.push({ t: 'ติ๊กยอมรับข้อกำหนดและนโยบายการจอง', e: pc });
                var ppc = el(RV.ids.petPolicy);
                if (ppc && !ppc.checked) miss.push({ t: 'ติ๊กยอมรับนโยบายสัตว์เลี้ยง', e: ppc });
                return miss;
            }

            function renderGate() {
                if (!RV.cust) return [];
                var miss = gateMissing();
                var btn = el(RV.ids.submit), box = document.getElementById('rvGateHint');
                if (btn) {
                    if (miss.length) { addCls(btn, 'rv-btn-off'); btn.setAttribute('aria-disabled', 'true'); }
                    else { rmCls(btn, 'rv-btn-off'); btn.removeAttribute('aria-disabled'); }
                }
                if (!box) return miss;
                box.innerHTML = '';
                if (!miss.length) { box.style.display = 'none'; return miss; }
                var h = document.createElement('div');
                h.className = 'rv-gate-title';
                h.textContent = 'ก่อนกดยืนยัน ยังเหลืออีก ' + miss.length + ' อย่าง (Still needed):';
                box.appendChild(h);
                var ul = document.createElement('ul');
                for (var i = 0; i < miss.length; i++) {
                    var li = document.createElement('li');
                    var a = document.createElement('a');
                    a.href = '#';
                    a.textContent = miss[i].t;
                    a.onclick = (function (target) { return function () { goTo(target); return false; }; })(miss[i].e);
                    li.appendChild(a);
                    ul.appendChild(li);
                }
                box.appendChild(ul);
                box.style.display = '';
                return miss;
            }

            // preventDoubleSubmit เรียก: true = ยังไม่ครบ ห้ามส่ง
            window.rvGateBlocks = function () {
                var miss = renderGate();
                if (!miss || !miss.length) return false;
                var box = document.getElementById('rvGateHint');
                if (box) { rmCls(box, 'rv-shake'); void box.offsetWidth; addCls(box, 'rv-shake'); }
                markInvalid(miss[0].e);
                goTo(miss[0].e);
                return true;
            };

            // ปุ่มเลือกยอดโอนเร็ว
            window.rvFillDeposit = function (kind) {
                var amt = el(RV.ids.amount);
                if (!amt || amt.disabled) return;
                var v = kind === 'full' ? RV.cur.total : RV.cur.minDep;
                if (!(v > 0)) return;
                amt.value = (Math.round(v) === v) ? String(v) : r2(v).toFixed(2);
                clearInvalid(amt);
                renderGate();
            };

            function onAnyInput(ev) {
                var t = ev.target;
                if (!t || !t.getAttribute) return;
                if (t.getAttribute('data-pet') === '1') { clampPet(t); refreshNumbers(); }
                clearInvalid(t);
                if (RV.cust) renderGate();
            }

            function init() {
                try {
                    // กล่องติ๊กยอมรับที่ไม่มีช่องให้ติ๊ก (พนักงาน/โหมดแก้ไข) → ไม่ต้องแสดงหัวข้อว่าง ๆ
                    var acc = document.querySelectorAll('.rv-accept');
                    for (var a = 0; a < acc.length; a++)
                        if (!acc[a].querySelector('input[type="checkbox"]')) acc[a].style.display = 'none';
                } catch (e0) { }
                try { movePets(); } catch (e1) { }
                try { readBase(); refreshNumbers(); } catch (e2) { }
                try { renderGate(); } catch (e3) { }
                document.addEventListener('input', onAnyInput, true);
                document.addEventListener('change', onAnyInput, true);
            }
            if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
            else init();
        })();
    </script>

    <%-- ── QR + ตามสถานะการจ่ายแบบสด (เฉพาะตอนมีลิงก์จริง) ── --%>
    <script src="https://cdn.jsdelivr.net/npm/qrcodejs@1.0.0/qrcode.min.js"></script>
    <script>
        function rvCopy(id, btn) {
            var el = document.getElementById(id);
            if (!el) return;
            el.select(); el.setSelectionRange(0, 99999);
            try { document.execCommand('copy'); } catch (e) { }
            if (navigator.clipboard) { try { navigator.clipboard.writeText(el.value); } catch (e) { } }
            var old = btn.textContent; btn.textContent = '✓ คัดลอกแล้ว';
            setTimeout(function () { btn.textContent = old; }, 1600);
        }

        (function () {
            function val(id) { var e = document.getElementById(id); return e ? e.value : ''; }
            function drawQr(boxId, text) {
                var box = document.getElementById(boxId);
                if (!box || !text || typeof QRCode === 'undefined') return;
                box.innerHTML = '';
                new QRCode(box, { text: text, width: 168, height: 168, correctLevel: QRCode.CorrectLevel.M });
            }

            drawQr('rvPayQr', val('rvPayUrl'));
            drawQr('rvHoldQr', val('rvHoldUrl'));

            // ถามสถานะเป็นระยะ — พนักงานเห็นทันทีว่าลูกค้าจ่ายแล้ว ไม่ต้องกดรีเฟรชเอง
            function watch(ref, statusId, okText, okStates) {
                if (!ref) return;
                var box = document.getElementById(statusId);
                if (!box) return;
                var timer = setInterval(function () {
                    fetch('<%= ResolveUrl("~/API/PaymentStatus.ashx") %>?ref=' + encodeURIComponent(ref) + '&_=' + Date.now())
                        .then(function (r) { return r.json(); })
                        .then(function (d) {
                            var s = (d && d.status || '').toUpperCase();
                            if (okStates.indexOf(s) >= 0) {
                                box.textContent = okText;
                                box.style.color = '#2E7D32';
                                clearInterval(timer);
                            } else if (['FAILED', 'EXPIRED', 'CANCELLED'].indexOf(s) >= 0) {
                                box.textContent = '❌ ' + (d.thai || s);
                                box.style.color = '#C62828';
                                clearInterval(timer);
                            }
                        })
                        .catch(function () { });
                }, 4000);
            }

            watch(val('rvPayRef'), 'rvPayStatus', '✅ ลูกค้าชำระเงินแล้ว — กดรีเฟรชหน้าเพื่อดูยอดล่าสุด', ['PAID']);
            watch(val('rvHoldRef'), 'rvHoldStatus', '✅ กันวงเงินเรียบร้อยแล้ว', ['HELD', 'PAID']);
        })();
    </script>
</asp:Content>
