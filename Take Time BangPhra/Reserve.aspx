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
        }
        
        .form-controls {
            width: 80%;
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
    </style>

    <script>
        // 🔒 Prevent double-click on submit button
        var isSubmitting = false;

        function preventDoubleSubmit() {
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

    <div class="reservation-container ExampleFont">
        
        
        <div class="form-panel">
            <h3 class="section-header">Reservation Details</h3>
            <p class="section-header">
                                
                            </p>
            <div><asp:TextBox ID="TextBox11" runat="server" TextMode="DateTime" Visible="False"></asp:TextBox></div>
            <div class="form-row">
                
                <div class="form-label">วันที่จอง:<br />Check-In Date:</div>
                <div class="form-controls">
                    <asp:TextBox ID="TextBox12" runat="server" AutoPostBack="True" TextMode="Date" Width="200px" OnTextChanged="TextBox12_TextChanged" CssClass="rounded-textbox"></asp:TextBox>
                    <asp:Button ID="Button2" runat="server" Text="ยกเลิกการเลือกวัน" OnClick="Button2_Click" CssClass="reservation-button" style="margin-left: 10px;"/>&nbsp;
                    </div>
            </div>
            
            <div class="form-row" style="background-color: #EFEBE9; padding: 8px 0;">
                <div class="form-label">กี่คืน:<br />How many Night(s):</div>
                <div class="form-controls">
                    <asp:DropDownList ID="DropDownList1" runat="server" CssClass="rounded-textbox" AutoPostBack="True" OnSelectedIndexChanged="DropDownList1_SelectedIndexChanged" Width="100px">
                        <asp:ListItem Value="1">1 คืน</asp:ListItem>
                        <asp:ListItem Value="2">2 คืน</asp:ListItem>
                        <asp:ListItem Value="3">3 คืน</asp:ListItem>
                        <asp:ListItem Value="4">4 คืน</asp:ListItem>
                        <asp:ListItem Value="5">5 คืน</asp:ListItem>
                        <asp:ListItem Value="6">6 คืน</asp:ListItem>
                        <asp:ListItem Value="7">7 คืน</asp:ListItem>
                    </asp:DropDownList>
                    <span style="margin-left: 15px;">
                        <asp:Label ID="Label1" runat="server" Text="Check-Out: "></asp:Label>
                    </span>
                    <asp:Button ID="Button4" runat="server" Text="เลือกทั้งหมด" Visible="False" OnClick="Button4_Click" CssClass="reservation-button" style="margin-left: 15px;"/>
                    <asp:CheckBox ID="CheckBox6" runat="server" Text="แก้ไขราคาห้องพัก" Visible="false" AutoPostBack="True" CssClass="mycheckbox" style="margin-left: 15px;"/>
                </div>
            </div>
            
            <div class="form-row">
                <div class="form-label">ประเภทที่พัก:<br />Accommodation Type:</div>
                <div class="form-controls">
                    <asp:GridView ID="GridView1" runat="server" Width="100%" AutoGenerateColumns="False" CssClass="mydatagrid ExampleFont" PagerStyle-CssClass="pager" HeaderStyle-CssClass="header" RowStyle-CssClass="rows" OnRowCancelingEdit="GridView1_RowCancelingEdit" OnRowEditing="GridView1_RowEditing" OnRowUpdating="GridView1_RowUpdating">
                        <Columns>
                            <asp:TemplateField HeaderText="เลือก" HeaderStyle-Width="5%" HeaderStyle-CssClass="ExampleFont" ItemStyle-CssClass="ExampleFont">
                                <ItemTemplate>
                                    <asp:CheckBox ID="chkSelect" runat="server" Width="100%" CommandName="Check" AutoPostBack="true"/>
                                </ItemTemplate>
                                <HeaderStyle Width="5%"></HeaderStyle>
                                <ItemStyle CssClass="ExampleFont"></ItemStyle>
                            </asp:TemplateField>
                            <asp:BoundField DataField="AccomName" HeaderText="รายชื่อห้องพัก" HeaderStyle-CssClass="header-center ExampleFont" ItemStyle-CssClass="ExampleFont" ReadOnly="true">
                                <HeaderStyle CssClass="header-center"></HeaderStyle>
                                <ItemStyle CssClass="ExampleFont"></ItemStyle>
                            </asp:BoundField>
                            <asp:TemplateField HeaderText="จำนวนผู้เข้าพัก" HeaderStyle-Width="20%" HeaderStyle-CssClass="header-center ExampleFont" ItemStyle-CssClass="header-center ExampleFont">
                                <ItemTemplate>
                                    <asp:TextBox ID="txtPeopleStay" runat="server" Width="100%" Text='0' TextMode="Number" AutoPostBack="true" OnTextChanged="txtPeopleStay_TextChanged" CssClass="rounded-textbox ExampleFont"/>
                                </ItemTemplate>
                                <HeaderStyle Width="20%"></HeaderStyle>
                                <ItemStyle CssClass="header-center"></ItemStyle>
                            </asp:TemplateField>
                            <asp:BoundField DataField="People" HeaderText="จำนวนผู้เข้าพักสูงสุด" HeaderStyle-CssClass="header-center ExampleFont" ItemStyle-CssClass="header-center ExampleFont" ReadOnly="true">
                                <HeaderStyle CssClass="header-center"></HeaderStyle>
                                <ItemStyle CssClass="header-center"></ItemStyle>
                            </asp:BoundField>
                            <asp:BoundField DataField="Price" HeaderText="ราคาต่อคืน" HeaderStyle-CssClass="header-center ExampleFont" ItemStyle-CssClass="header-center ExampleFont">
                                <HeaderStyle CssClass="header-center"></HeaderStyle>
                                <ItemStyle CssClass="header-center"></ItemStyle>
                            </asp:BoundField>
                            <asp:CommandField ButtonType="Button" HeaderText="แก้ไข" ShowEditButton="True" ControlStyle-CssClass="ExampleFont" HeaderStyle-CssClass="header-center ExampleFont" ItemStyle-CssClass="header-center ExampleFont">
                                <ControlStyle CssClass="ExampleFont"></ControlStyle>
                                <HeaderStyle CssClass="header-center ExampleFont"></HeaderStyle>
                                <ItemStyle CssClass="header-center ExampleFont"></ItemStyle>
                            </asp:CommandField>
                        </Columns>
                        <HeaderStyle CssClass="header ExampleFont"></HeaderStyle>
                        <PagerStyle CssClass="pager ExampleFont"></PagerStyle>
                        <RowStyle CssClass="rows ExampleFont"></RowStyle>
                    </asp:GridView>
                </div>
            </div>
        </div>
        
        <div class="form-panel">
            <h3 class="section-header">Guest Information</h3>
            <div class="form-row" style="background-color: #EFEBE9; padding: 8px 0;">
                <div class="form-label">เบอร์โทรศัพท์:<span class="required-field">*</span><br />Telephone Number:<span class="required-field">*</span></div>
                <div class="form-controls">
                    <asp:TextBox ID="TextBox1" runat="server" Width="200px" AutoPostBack="True" OnTextChanged="TextBox1_TextChanged" CssClass="rounded-textbox"></asp:TextBox>
                    <asp:Button ID="Button5" runat="server" OnClick="Button5_Click" Text="Button" Visible="False" CssClass="reservation-button" style="margin-left: 10px;"/>
                </div>
            </div>
            
            <div class="form-row">
                <div class="form-label">ชื่อ นามสกุล หรือ ชื่อบริษัท:<span class="required-field">*</span><br />Full Name:<span class="required-field">*</span></div>
                <div class="form-controls">
                    <asp:DropDownList ID="DropDownList8" runat="server" Width="150px" AutoPostBack="True" OnSelectedIndexChanged="DropDownList8_SelectedIndexChanged" CssClass="rounded-textbox">
                    </asp:DropDownList>
                    <asp:TextBox ID="TextBox2" runat="server" Width="250px" CssClass="rounded-textbox" style="margin-left: 10px;"></asp:TextBox>
                    <asp:TextBox ID="TextBox18" runat="server" Width="100px" PlaceHolder="รหัสสาขา" Visible="false" Text="00000" CssClass="rounded-textbox" style="margin-left: 10px;"></asp:TextBox>
                </div>
            </div>
            
            <div class="form-row" style="background-color: #EFEBE9; padding: 8px 0;">
                <div class="form-label">ชื่อ Facebook หรือ ID Line:<br />Facebook Name/Line ID:</div>
                <div class="form-controls">
                    <asp:TextBox ID="TextBox3" runat="server" Width="300px" CssClass="rounded-textbox"></asp:TextBox>
                </div>
            </div>
            
            <div class="form-row">
                <div class="form-label">&nbsp;</div>
                <div class="form-controls">
                    <asp:CheckBox ID="CheckBox3" runat="server" Text="ไม่รับใบกำกับภาษี" AutoPostBack="True" Checked="True" OnCheckedChanged="CheckBox3_CheckedChanged" CssClass="mycheckbox" />
                    <asp:CheckBox ID="CheckBox4" runat="server" Text="ไม่ออกใบกำกับภาษีในระบบ !!!" Visible="false" OnCheckedChanged="CheckBox4_CheckedChanged" AutoPostBack="True" CssClass="mycheckbox" style="margin-left: 20px;"/>
                </div>
            </div>
            
            <asp:Panel ID="Panel1" runat="server" Visible="False" CssClass="form-panel">
                <div class="form-row" style="background-color: #EFEBE9;">
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
                
                <div class="form-row" style="background-color: #EFEBE9;">
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
                
                <div class="form-row" style="background-color: #EFEBE9;">
                    <div class="form-label">อีเมล<br />Email:</div>
                    <div class="form-controls">
                        <asp:TextBox ID="TextBox13" runat="server" Width="300px" AutoPostBack="True" OnTextChanged="TextBox9_TextChanged" TextMode="Email" CssClass="rounded-textbox"></asp:TextBox>
                        <asp:CheckBox ID="CheckBox5" runat="server" AutoPostBack="True" Text="ต้องการรับ e tax invoice" Visible="False" OnCheckedChanged="CheckBox5_CheckedChanged" CssClass="mycheckbox" style="margin-left: 10px;"/>
                    </div>
                </div>
            </asp:Panel>
        </div>
        
        <div class="form-panel">
            <h3 class="section-header">Additional Services</h3>
            <div class="form-row" style="background-color: #EFEBE9; padding: 8px 0;">
                <div class="form-label">เช่าของ:<br />Rent Item:</div>
                <div class="form-controls">
                    <asp:CheckBox ID="CheckBox7" runat="server" AutoPostBack="True" OnCheckedChanged="CheckBox7_CheckedChanged" CssClass="mycheckbox"/>
                </div>
            </div>
            
            <asp:Panel ID="Panel2" runat="server" Visible="false" CssClass="form-panel">
                <div class="form-row">
                    <div class="form-label">รูปภาพของเช่า:<br />Rent Items Picture:</div>
                    <div class="form-controls">
                        <asp:HyperLink ID="HyperLink1" runat="server" NavigateUrl="./Images/อุปกรณ์เช่า.png" Target="_blank" CssClass="reservation-button" style="display: inline-block; padding: 5px 10px; text-decoration: none;">กดเพื่อดูรูปภาพของเช่า(Click)</asp:HyperLink>
                    </div>
                </div>
                
                <div class="form-row" style="background-color: #EFEBE9;">
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

        <div class="form-panel">
            <h3 class="section-header">Payment Information</h3>
            <div class="form-row" style="background-color: #EFEBE9; padding: 8px 0;">
                <div class="form-label">รหัสส่วนลด:<br />Coupon Code:</div>
                <div class="form-controls">
                    <asp:TextBox ID="TextBox19" runat="server" Width="200px" CssClass="rounded-textbox"></asp:TextBox>
                    <asp:Button ID="Button8" runat="server" Text="Submit" Width="100px" OnClick="Button8_Click" CssClass="reservation-button" style="margin-left: 10px;"/>
                </div>
            </div>
            
            <div class="form-row">
                <div class="form-label">ราคารวม:<br />Total price:</div>
                <div class="form-controls">
                    <asp:TextBox ID="TextBox4" runat="server" Enabled="False"  Width="200px" TextMode="Number" CssClass="rounded-textbox price-display">0</asp:TextBox>
                    <span style="margin-left: 10px;">บาท</span>
                    <div style="margin-top: 5px;">
                        ยอดมัดจำจองขั้นต่ำ Minimum Deposit:
                        <asp:Label ID="Label2" runat="server" Text="" CssClass="price-display"></asp:Label>
                    </div>
                </div>
            </div>
            
            <div class="form-row" style="background-color: #EFEBE9; padding: 8px 0;">
                <div class="form-label">ยอดเงินที่โอนเพื่อมัดจำจอง:<br />Deposit Amount:</div>
                <div class="form-controls">
                    <asp:TextBox ID="TextBox5" runat="server" AutoPostBack="True" Width="200px" TextMode="Number" OnTextChanged="TextBox5_TextChanged" CssClass="rounded-textbox">0</asp:TextBox>
                    <span style="margin-left: 10px;">บาท</span>
                    <div style="margin-top: 10px;">
                        <asp:DropDownList ID="DropDownList2" Width="300px" runat="server" CssClass="rounded-textbox" Enabled="False" AutoPostBack="True" OnSelectedIndexChanged="DropDownList2_SelectedIndexChanged" AppendDataBoundItems="true">
                        </asp:DropDownList>
                        <asp:SqlDataSource ID="SqlDataSource1" runat="server" ConnectionString="<%$ ConnectionStrings:TaketimeConnectionString %>" SelectCommand="SELECT * FROM [Account_Paid_How] WHERE ([Status] = 'True')">
                            <SelectParameters>
                                <asp:Parameter DefaultValue="True" Name="Status" Type="Boolean" />
                            </SelectParameters>
                        </asp:SqlDataSource>
                    </div>
                    <asp:Label ID="Label7" runat="server" Text="" Visible="false"></asp:Label>
                </div>
            </div>
            
            <div class="form-row">
                <div class="form-label">&nbsp;</div>
                <div class="form-controls">
                    <asp:CheckBox ID="CheckBox2" runat="server" Text="มัดจำเพิ่ม" AutoPostBack="True" OnCheckedChanged="CheckBox2_CheckedChanged" Visible="False" CssClass="mycheckbox"/>
                    <asp:TextBox ID="TextBox10" runat="server" TextMode="Number" Visible="False" AutoPostBack="True" CssClass="rounded-textbox" OnTextChanged="TextBox10_TextChanged" style="margin-left: 10px;">จำนวนเงิน</asp:TextBox>
                </div>
            </div>
            
            <div class="form-row" style="background-color: #EFEBE9; padding: 8px 0;">
                <div class="form-label">อัพโหลดสลิป:<br />Transfer Slip Upload:</div>
                <div class="form-controls">
                    <div>
                        <asp:FileUpload ID="FileUpload1" runat="server" OnDataBinding="FileUpload1_DataBinding" Width="350px" CssClass="rounded-textbox"/>
                        <asp:Button ID="Button3" runat="server" Text="Upload Picture" Width="150px" OnClick="Button3_Click" CssClass="reservation-button" style="margin-left: 10px;"/>
                    </div>
                    <div style="margin-top: 5px; font-size: 0.9em; color: #8D6E63;">
                        *สามารถโอนยอดมัดจำจองขั้นต่ำ หรือ โอนชำระเต็มจำนวนได้เลยค่ะ
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
            
            <div class="form-row">
                <div class="form-label">หมายเหตุ:<br />Remark:</div>
                <div class="form-controls">
                    <asp:TextBox ID="TextBox6" runat="server" Width="90%" TextMode="MultiLine" Rows="3" CssClass="rounded-textbox"></asp:TextBox>
                </div>
            </div>
        </div>
        
        <div class="rules-section">
            <img src="./Images/กฏระเบียบ.png" width="90%" style="max-width: 800px;"/>
        </div>
        
        <div class="form-row" style="justify-content: center; margin-top: 20px;">
            <div style="text-align: center; width: 100%;">
                <asp:CheckBox ID="CheckBox1" runat="server" AutoPostBack="True" OnCheckedChanged="CheckBox1_CheckedChanged" CssClass="mycheckbox" style="margin-right: 10px;"/>
                <span style="font-size: 1.1em; color: #5D4037;">***ติ๊กเลือกเพื่อยอมรับกติกาด้านบน และรับทราบเรื่องการห้ามใช้เสียงดังหลัง 22.30 น. (Accept the rule)</span>
                <div style="margin-top: 20px;">
                    <asp:Button ID="Button1" runat="server" Text="ยืนยันการจอง(Submit)" Height="60px" Width="300px" OnClick="Button1_Click" OnClientClick="return preventDoubleSubmit();" Enabled="False" CssClass="reservation-button" style="font-size: 1.2em;"/>
                </div>
            </div>
        </div>
    </div>

    <rsweb:reportviewer Visible="false" ID="ReportViewer2" runat="server" Width="100%" BackColor="" ClientIDMode="AutoID" HighlightBackgroundColor="" InternalBorderColor="204, 204, 204" InternalBorderStyle="Solid" InternalBorderWidth="1px" LinkActiveColor="" LinkActiveHoverColor="" LinkDisabledColor="" PrimaryButtonBackgroundColor="" PrimaryButtonForegroundColor="" PrimaryButtonHoverBackgroundColor="" PrimaryButtonHoverForegroundColor="" SecondaryButtonBackgroundColor="" SecondaryButtonForegroundColor="" SecondaryButtonHoverBackgroundColor="" SecondaryButtonHoverForegroundColor="" SplitterBackColor="" ToolbarDividerColor="" ToolbarForegroundColor="" ToolbarForegroundDisabledColor="" ToolbarHoverBackgroundColor="" ToolbarHoverForegroundColor="" ToolBarItemBorderColor="" ToolBarItemBorderStyle="Solid" ToolBarItemBorderWidth="1px" ToolBarItemHoverBackColor="" ToolBarItemPressedBorderColor="51, 102, 153" ToolBarItemPressedBorderStyle="Solid" ToolBarItemPressedBorderWidth="1px" ToolBarItemPressedHoverBackColor="153, 187, 226" Height="500px" OnClientClick="this.disabled = true; this.value = 'Processing...';" UseSubmitBehavior="false">
        <LocalReport ReportPath="Account\Report\Receipt.rdlc" EnableExternalImages="True">
        </LocalReport>
    </rsweb:reportviewer>
</asp:Content>