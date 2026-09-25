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
            
            <!-- 🎁 Loyalty Discount Display -->
            <asp:Panel ID="pnlLoyaltyDiscount" runat="server" Visible="false" CssClass="loyalty-discount-panel">
                <div class="form-row" style="background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 15px; border-radius: 8px; margin-bottom: 15px;">
                    <div style="display: flex; align-items: center; justify-content: space-between;">
                        <div style="flex: 1;">
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

            <div class="form-row">
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
                    <%-- รายละเอียดของช่องทางที่ลูกค้าเลือก (QR/บัญชี, เงื่อนไขบัตร, นโยบายยกเลิก) — ว่าง = ไม่แสดงอะไร --%>
                    <asp:Literal ID="litChannelInfo" runat="server" />
                    <asp:Label ID="Label7" runat="server" Text="" Visible="false"></asp:Label>
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

            <div class="form-row" id="rowSlip" runat="server" style="background-color: #EFEBE9; padding: 8px 0;">
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
                        <div style="flex:1; min-width:260px;">
                            <label style="font-weight:600; font-size:0.9em;">ลิงก์สำหรับลูกค้า (ส่งทางแชทได้)</label>
                            <div style="display:flex; gap:6px;">
                                <asp:TextBox ID="txtPayLinkUrl" runat="server" ReadOnly="true" Width="100%"
                                    CssClass="rounded-textbox" />
                                <button type="button" onclick="rvCopy('<%= txtPayLinkUrl.ClientID %>',this)"
                                    style="padding:8px 14px;border:0;border-radius:8px;background:#558B2F;color:#fff;cursor:pointer;">คัดลอก</button>
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
                            <div style="flex:1; min-width:260px;">
                                <label style="font-weight:600; font-size:0.9em;">ลิงก์ให้ลูกค้ากรอกบัตร (กันวงเงิน)</label>
                                <div style="display:flex; gap:6px;">
                                    <asp:TextBox ID="txtDepositLink" runat="server" ReadOnly="true" Width="100%"
                                        CssClass="rounded-textbox" />
                                    <button type="button" onclick="rvCopy('<%= txtDepositLink.ClientID %>',this)"
                                        style="padding:8px 14px;border:0;border-radius:8px;background:#558B2F;color:#fff;cursor:pointer;">คัดลอก</button>
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

            <div class="form-row">
                <div class="form-label">หมายเหตุ:<br />Remark:</div>
                <div class="form-controls">
                    <asp:TextBox ID="TextBox6" runat="server" Width="90%" TextMode="MultiLine" Rows="3" CssClass="rounded-textbox"></asp:TextBox>
                </div>
            </div>
        </div>

        <%-- ══ นโยบายการจอง (แก้ข้อความที่ ศูนย์ตั้งค่า → นโยบายการจอง) — แสดงเฉพาะโหมดจองใหม่ ══ --%>
        <asp:Panel ID="pnlPolicySection" runat="server" Visible="false" CssClass="form-panel">
            <style>
                .rv-pol-summary { background:#FFF8E1; border-left:4px solid #FFB300; border-radius:6px;
                                  padding:10px 14px; color:#5D4037; line-height:1.7; font-size:0.95em; }
                .rv-pol-links { margin-top:10px; display:flex; flex-wrap:wrap; gap:8px; }
                .rv-pol-links a { display:inline-block; padding:6px 12px; border-radius:16px; background:#EFEBE9;
                                  color:#5D4037; text-decoration:none; font-size:0.92em; }
                .rv-pol-links a:hover { background:#D7CCC8; }
                .rv-pol-overlay { display:none; position:fixed; left:0; top:0; right:0; bottom:0; z-index:10000;
                                  background:rgba(0,0,0,.55); align-items:center; justify-content:center; padding:12px; }
                .rv-pol-box { background:#fff; border-radius:12px; width:100%; max-width:760px; max-height:88vh;
                              display:flex; flex-direction:column; box-shadow:0 10px 40px rgba(0,0,0,.3); }
                .rv-pol-tabs { display:flex; flex-wrap:wrap; gap:6px; padding:12px 14px 8px; border-bottom:1px solid #eee; }
                .rv-pol-tabs button { border:0; border-radius:16px; padding:6px 12px; background:#EFEBE9;
                                      color:#5D4037; cursor:pointer; font-size:0.9em; }
                .rv-pol-tabs button.on { background:#5D4037; color:#fff; }
                .rv-pol-body { overflow-y:auto; padding:12px 16px; line-height:1.75; color:#3E2723; font-size:0.95em; }
                .rv-pol-pane h4 { margin:0 0 8px; color:#5D4037; }
                .rv-pol-foot { padding:10px 14px; border-top:1px solid #eee; text-align:right; }
            </style>
            <h3 class="section-header">เงื่อนไขและนโยบายการจอง (Booking Policies)</h3>
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
                        <button type="button" class="reservation-button" onclick="rvPolicyClose()" style="padding:8px 22px;">ปิด / Close</button>
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

        <div class="rules-section">
            <img src="./Images/กฏระเบียบ.png" width="90%" style="max-width: 800px;"/>
        </div>
        
        <div class="form-row" style="justify-content: center; margin-top: 20px;">
            <div style="text-align: center; width: 100%;">
                <%-- ลูกค้าจองเอง: ต้องยอมรับนโยบายก่อน (บันทึกเวลา + ฉบับที่ยอมรับไว้บนใบจอง) --%>
                <asp:Panel ID="pnlPolicyAccept" runat="server" Visible="false" style="margin-bottom: 12px;">
                    <asp:CheckBox ID="chkAcceptPolicy" runat="server" AutoPostBack="True" OnCheckedChanged="chkAcceptPolicy_CheckedChanged" CssClass="mycheckbox" style="margin-right: 10px;"/>
                    <span style="font-size: 1.1em; color: #5D4037;">***ข้าพเจ้าได้อ่านและยอมรับ
                        <a href="#" onclick="rvPolicyOpen('terms');return false;">ข้อกำหนดและเงื่อนไข</a>,
                        <a href="#" onclick="rvPolicyOpen('privacy');return false;">นโยบายความเป็นส่วนตัว</a>,
                        <a href="#" onclick="rvPolicyOpen('refund');return false;">นโยบายการคืนเงิน</a> และ
                        <a href="#" onclick="rvPolicyOpen('cancel');return false;">นโยบายการยกเลิกการจอง</a>
                        (I accept the Terms &amp; Conditions, Privacy, Refund and Cancellation policies)</span>
                </asp:Panel>
                <asp:CheckBox ID="CheckBox1" runat="server" AutoPostBack="True" OnCheckedChanged="CheckBox1_CheckedChanged" CssClass="mycheckbox" style="margin-right: 10px;"/>
                <span style="font-size: 1.1em; color: #5D4037;">***ติ๊กเลือกเพื่อยอมรับกติกาด้านบน และรับทราบเรื่องการห้ามใช้เสียงดังหลัง 22.30 น. (Accept the rule)</span>
                <div style="margin-top: 20px;">
                    <asp:Button ID="Button1" runat="server" Text="ยืนยันการจอง(Submit)" Height="60px" Width="300px" OnClick="Button1_Click" OnClientClick="return preventDoubleSubmit();" Enabled="False" CssClass="reservation-button" style="font-size: 1.2em;"/>
                    <asp:Button ID="btnPostpone" runat="server" Text="เลื่อนเข้าพัก" Height="60px" Width="200px" OnClick="btnPostpone_Click" OnClientClick="return confirm('ยืนยันการเลื่อนเข้าพัก? วันเข้าพักจะถูกลบออก และการจองจะถูกย้ายไปรายการเลื่อนเข้าพัก');" Visible="False" CssClass="reservation-button" style="font-size: 1.2em; margin-left: 15px; background: linear-gradient(135deg, #f0ad4e 0%, #ec971f 100%); color: white;"/>
                </div>
            </div>
        </div>
    </div>

    <rsweb:reportviewer Visible="false" ID="ReportViewer2" runat="server" Width="100%" BackColor="" ClientIDMode="AutoID" HighlightBackgroundColor="" InternalBorderColor="204, 204, 204" InternalBorderStyle="Solid" InternalBorderWidth="1px" LinkActiveColor="" LinkActiveHoverColor="" LinkDisabledColor="" PrimaryButtonBackgroundColor="" PrimaryButtonForegroundColor="" PrimaryButtonHoverBackgroundColor="" PrimaryButtonHoverForegroundColor="" SecondaryButtonBackgroundColor="" SecondaryButtonForegroundColor="" SecondaryButtonHoverBackgroundColor="" SecondaryButtonHoverForegroundColor="" SplitterBackColor="" ToolbarDividerColor="" ToolbarForegroundColor="" ToolbarForegroundDisabledColor="" ToolbarHoverBackgroundColor="" ToolbarHoverForegroundColor="" ToolBarItemBorderColor="" ToolBarItemBorderStyle="Solid" ToolBarItemBorderWidth="1px" ToolBarItemHoverBackColor="" ToolBarItemPressedBorderColor="51, 102, 153" ToolBarItemPressedBorderStyle="Solid" ToolBarItemPressedBorderWidth="1px" ToolBarItemPressedHoverBackColor="153, 187, 226" Height="500px" OnClientClick="this.disabled = true; this.value = 'Processing...';" UseSubmitBehavior="false">
        <LocalReport ReportPath="Account\Report\Receipt.rdlc" EnableExternalImages="True">
        </LocalReport>
    </rsweb:reportviewer>

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