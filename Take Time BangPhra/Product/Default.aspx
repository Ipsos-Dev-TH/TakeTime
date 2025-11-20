<%@ Page Title="" Language="C#" MaintainScrollPositionOnPostback="true" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="Take_Time_BangPhra.Product.Default" %>
<%@ Register assembly="Microsoft.ReportViewer.WebForms" namespace="Microsoft.Reporting.WebForms" tagprefix="rsweb" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
       <link rel="stylesheet" href="../Content/jquery-ui.css">
<link rel="stylesheet" href="../Content/style.css">
  <link rel="stylesheet" type="text/css" href="../Content/GridView.css">
        <style>
        body {
            font-family: Arial, sans-serif;
            background-color: #f0f0f0;
            color: #333;
        }

        h2 {
            color: #ff6600;
            text-align: center;
        }

        .table {
            width: 100%;

            border-collapse: collapse;
        }

        .table th, .table td {
            padding: 5px;
            border-bottom: 1px solid #ddd;
            text-align: left;
        }

        .table th {
            background-color: #ff6600;
            color: #fff;
        }

        .form-group {
            margin-bottom: 5px;
        }

        .form-group label {
            display: block;
            margin-bottom: 5px;
        }

        .form-control {
            width: 100%;
            padding: 10px;
            border: 1px solid #ccc;
            border-radius: 5px;
        }

        .btn {
    padding: 8px 16px;
    border-radius: 4px;
    text-decoration: none;
    font-weight: bold;
    transition: all 0.3s;
}

.btn-success {
    background-color: #8D6E63;
    color: white;
    border: 1px solid #6D4C41;
}

.btn-success:hover {
    background-color: #6D4C41;
}

        .btn-add {

            background-color: #28a745;
        }

        .btn-add:hover {
            background-color: #218838;
        }

        .gridview-container {
            margin-top: 10px;
        }
        
        /*the container must be positioned relative:*/
.autocomplete {
  position: relative;
  display: inline-block;
}

     .autocomplete-items {
  position: absolute;
  border: 1px solid #d4d4d4;
  border-bottom: none;
  border-top: none;
  z-index: 99;
  /*position the autocomplete items to be the same width as the container:*/
  top: 100%;
  left: 0;
  right: 0;
  background-color: lemonchiffon;
}
.autocomplete-items div {
  padding: 10px;
  cursor: pointer;
  border-bottom: 1px solid #d4d4d4;
}

.autocomplete-active {
  /*when navigating through the items using the arrow keys:*/
  background-color: DodgerBlue !important;
  color: #ffffff;
}
    </style>
    <style>
        tr.spaceUnder>td {
  padding-bottom: 1em;
}
        </style>
    <p>
        <br />


       
     <style>


 .header-center{
        text-align:center;
    }
  .header-right{
        text-align:right;
    }
         </style>
            <style>
            th, td {
  padding: 5px;
}
                </style>
    <style>


.myCalendar th.myCalendarDayHeader  
{
    height:25px;
    border-bottom: outset 2px #fbfbfb; 
    border-right: outset 2px #fbfbfb; 
}
.myCalendar td.myCalendarDay {  
    border: outset 2px #fbfbfb;
}  

.myCalendar .myCalendarNextPrev {  
    text-align: center;  
}  



.myCalendar .myCalendarDayHeader a,
.myCalendar .myCalendarDay a,   
.myCalendar .myCalendarSelector a,  
.myCalendar .myCalendarNextPrev a {  
    display: block;  
    line-height: 20px;  
}  
.myCalendar .myCalendarToday{  background-color: #f2f2f2; -webkit-box-shadow: 0 0 7px 3px #e5e5e5;
box-shadow: 0 0 7px 3px #e5e5e5;}
.myCalendar .myCalendarDay a:hover,   
.myCalendar .myCalendarSelector a:hover {  
    background-color: #25bae5;  
}
        .auto-style1 {
            text-align: right;
            width: 20%;
            height: 32px;
        }
        .auto-style2 {
            width: 80%;
            height: 32px;
        }
        .auto-style3 {
            text-align: right;
            width: 20%;
            height: 28px;
        }
        .auto-style4 {
            width: 80%;
            height: 28px;
        }
        </style>

        <script type="text/javascript">
            function checkEnter(event) {
                if (event.key === 'Enter') {
                    __doPostBack('<%= TextBox1.UniqueID %>', '');
                    event.preventDefault();
                }
            }
        </script>
         <table style="width: 100%;">
     <tr>
         <td style=width:20% class="text-right">
             &nbsp;</td>
         <td style=width:80%>
             &nbsp;</td>
                
            </tr>
            
     <tr>
         <td class="auto-style1">
             วันที่: </td>
         <td class="auto-style2">
             <asp:TextBox ID="TextBox12" runat="server" Width="80%" AutoPostBack="True" TextMode="Date" OnTextChanged="TextBox12_TextChanged"></asp:TextBox>
         </td>
                
            </tr>

     <!-- 🏨 Room Charge Feature: Guest Selection -->
     <tr>
         <td class="auto-style1" style="vertical-align: middle;">
             เลือกห้องพัก (Room Charge): </td>
         <td class="auto-style2">
             <asp:DropDownList ID="ddlGuestReservation" runat="server"
                 Width="100%"
                 Height="40px"
                 AutoPostBack="True"
                 OnSelectedIndexChanged="ddlGuestReservation_SelectedIndexChanged"
                 CssClass="form-control"
                 style="font-size: 14px; padding: 8px;">
             </asp:DropDownList>
             <br />
             <asp:Label ID="lblActiveGuestCount" runat="server" CssClass="text-muted"
                 style="font-size: 0.85em; color: #888; margin-top: 5px;"></asp:Label>
         </td>
     </tr>

     <!-- 🏨 Room Charge Feature: Guest Info Display -->
     <tr id="trGuestInfo" runat="server" visible="false">
         <td class="auto-style1"></td>
         <td class="auto-style2">
             <div style="background-color: #f8f9fa; padding: 10px; border-radius: 5px; border-left: 4px solid #8D6E63;">
                 <asp:Label ID="lblGuestInfo" runat="server" CssClass="text-info"
                     style="font-size: 0.95em; color: #333; line-height: 1.6;"></asp:Label>
             </div>
         </td>
     </tr>

     <!-- 🏨 Room Charge Feature: Charge Mode Selection (Hidden by default) -->
     <tr id="trChargeMode" runat="server" visible="false">
         <td class="auto-style1" style="vertical-align: top; padding-top: 15px;">
             โหมดการชำระเงิน: </td>
         <td class="auto-style2">
             <asp:RadioButtonList ID="rblChargeMode" runat="server" RepeatDirection="Horizontal" AutoPostBack="True" OnSelectedIndexChanged="rblChargeMode_SelectedIndexChanged">
                 <asp:ListItem Value="ROOM_CHARGE" Selected="True" style="margin-right: 20px;">
                     <strong>ชาร์จเข้าห้อง</strong> (ชำระทีหลัง)
                 </asp:ListItem>
                 <asp:ListItem Value="PAY_NOW">
                     <strong>ชำระเงินทันที</strong>
                 </asp:ListItem>
             </asp:RadioButtonList>
             <div class="text-muted" style="margin-top: 8px; font-size: 0.9em; color: #888; line-height: 1.6;">
                 💡 <strong>ชาร์จเข้าห้อง:</strong> ตัดสต๊อก แต่ไม่เก็บเงิน (รวมในบิลเช็คเอาท์)<br />
                 💡 <strong>ชำระเงินทันที:</strong> ตัดสต๊อกและเก็บเงินเลย + ออกใบเสร็จ
             </div>
         </td>
     </tr>

     <tr>
         <td class="auto-style3">
             รหัสหรือชื่อสินค้า: </td>
         <td class="auto-style4">
              <div class="autocomplete" style="width:100%">
             <asp:TextBox ID="TextBox1" runat="server" Width="40%" AutoPostBack="True" OnTextChanged="TextBox1_TextChanged" onkeydown="checkEnter(event)" autocomplete="off"></asp:TextBox>
                  &nbsp; <asp:Button ID="Button3" runat="server" Text="ADD" Width="20%" OnClick="Button3_Click" CssClass="btn btn-success" />
                  &nbsp; <button type="button" id="btnScanBarcode" runat="server" class="btn btn-primary btn-success" style="width:20%;text-align:center">Scan Barcode</button>

<div id="scannerModal" style="display:none; position:fixed; top:0; left:0; width:100%; height:100%; background:black; z-index:1000;">
    <div id="interactive" style="width:100%; height:80vh; position:relative;">
        <!-- This is where the camera feed will appear -->
        <video id="scannerVideo" autoplay playsinline style="width:100%; height:100%; object-fit:cover;"></video>
    </div>
    <button id="closeScanner" style="position:fixed; bottom:20px; left:50%; transform:translateX(-50%); padding:10px 20px;">
        Close Scanner
    </button>
</div>
                  </div>
           

<script src="https://cdnjs.cloudflare.com/ajax/libs/quagga/0.12.1/quagga.min.js"></script>
<script>
    // Global variables to track scanner state
    let quaggaInitialized = false;
    let mediaStream = null;

    document.getElementById('<%= btnScanBarcode.ClientID %>').addEventListener('click', function () {
        const modal = document.getElementById('scannerModal');
        const video = document.getElementById('scannerVideo');
        modal.style.display = 'block';

        // First test basic camera access
        navigator.mediaDevices.getUserMedia({
            video: {
                facingMode: 'environment',
                width: { ideal: 1280 },
                height: { ideal: 720 }
            }
        }).then(function (stream) {
            // Show camera feed immediately
            video.srcObject = stream;
            mediaStream = stream;

            video.onloadedmetadata = function () {
                video.play().catch(err => {
                    console.error("Video play error:", err);
                    alert("Couldn't start camera: " + err.message);
                });
            };

            // Now initialize Quagga
            initializeQuagga();

        }).catch(function (err) {
            console.error("Camera access error:", err);
            alert("Couldn't access camera: " + err.message);
            modal.style.display = 'none';
        });

        function initializeQuagga() {
            // Clear previous instance if exists
            if (quaggaInitialized) {
                Quagga.stop();
            }

            Quagga.init({
                inputStream: {
                    name: "Live",
                    type: "LiveStream",
                    target: video, // Use the video element directly
                    constraints: {
                        facingMode: "environment",
                        width: { min: 640 },
                        height: { min: 480 }
                    },
                },
                decoder: {
                    readers: ["code_128_reader", "ean_reader", "upc_reader"],
                    debug: {
                        drawBoundingBox: true,
                        showFrequency: false,
                        drawScanline: true,
                        showPattern: false
                    }
                },
                locate: true,
                numOfWorkers: 2,
                frequency: 10
            }, function (err) {
                if (err) {
                    console.error("Quagga init error:", err);
                    alert("Scanner initialization failed: " + err);
                    stopScanner();
                    return;
                }

                quaggaInitialized = true;
                Quagga.start();
                console.log("Quagga started successfully");
            });

            // Handle detected barcodes
            Quagga.onDetected(function (result) {
                console.log("Barcode detected:", result);
                if (result.codeResult) {
                    const code = result.codeResult.code;
                    document.getElementById('<%= TextBox1.ClientID %>').value = code;
                    
                    // Play success sound
                    const audio = new Audio('https://assets.mixkit.co/sfx/preview/mixkit-achievement-bell-600.mp3');
                    audio.play().catch(e => console.log("Audio play error:", e));
                    
                    // Stop scanner and close modal
                    stopScanner();
                    
                    // Trigger postback if needed
                    __doPostBack('<%= TextBox1.UniqueID %>', '');
                }
            });
        }

        // Close button handler
        document.getElementById('closeScanner').addEventListener('click', stopScanner);

        // Function to properly stop and clean up
        function stopScanner() {
            if (quaggaInitialized) {
                Quagga.stop();
                quaggaInitialized = false;
            }
            if (mediaStream) {
                mediaStream.getTracks().forEach(track => track.stop());
                mediaStream = null;
            }
            const video = document.getElementById('scannerVideo');
            if (video) {
                video.srcObject = null;
            }
            modal.style.display = 'none';
            console.log("Scanner stopped and cleaned up");
        }
    });
</script>

         </td>
                
            </tr>
            
     <tr>
         <td style=width:20% class="text-right">
             &nbsp;</td>
         <td style=width:80%>
             <asp:GridView ID="GridView1" CssClass="mydatagrid" PagerStyle-CssClass="pager" HeaderStyle-CssClass="header" RowStyle-CssClass="rows" runat="server" AutoGenerateColumns="False" OnRowCommand="GridView1_RowCommand" OnRowCancelingEdit=" GridView1_RowCancelingEdit" OnRowEditing="GridView1_RowEditing" OnRowUpdating="GridView1_RowUpdating">
                 <Columns>
                     <asp:CommandField ButtonType="Button" ShowEditButton="True" />
                     <asp:BoundField DataField="Product_Name" HeaderText="รายชื่อสินค้า" ReadOnly="True" />
                     <asp:BoundField DataField="Amount" HeaderText="จำนวน" />
                     <asp:BoundField DataField="Sell_Price" HeaderText="ราคาต่อชิ้น" />
                     <asp:BoundField DataField="Price_Total" HeaderText="ราคารวม" ReadOnly="True" />
                     <asp:ButtonField ButtonType="Button" CommandName="Add" Text="เพิ่มจำนวน" />
                     <asp:ButtonField ButtonType="Button" CommandName="Reduce" Text="ลดจำนวน" />
                     <asp:ButtonField ButtonType="Button" CommandName="DeleteItem" Text="ลบ" />
                 </Columns>
<HeaderStyle CssClass="header"></HeaderStyle>

<PagerStyle CssClass="pager"></PagerStyle>

<RowStyle CssClass="rows"></RowStyle>
             </asp:GridView>
         </td>
                
            </tr>
            
     <tr>
         <td style=width:20% class="text-right">
             ราคารวม: </td>
         <td style=width:80%>
             <asp:TextBox ID="TextBox2" runat="server" Width="80%" Enabled="false"></asp:TextBox>
         </td>
                
            </tr>
            
     <tr>
         <td style=width:20% class="text-right">
             ชำระเข้าบัญชี: </td>
         <td style=width:80%>
                                <asp:DropDownList ID="DropDownList1" Width="60%" runat="server" DataSourceID="SqlDataSource1" DataTextField="Paid_How" DataValueField="ID" AutoPostBack="True" OnSelectedIndexChanged="DropDownList1_SelectedIndexChanged" AppendDataBoundItems="true">
                                <asp:ListItem>--- โปรดเลือก ---</asp:ListItem>
                                </asp:DropDownList>
                                <asp:SqlDataSource ID="SqlDataSource1" runat="server" ConnectionString="<%$ ConnectionStrings:TaketimeConnectionString %>" SelectCommand="SELECT * FROM [Account_Paid_How] WHERE ([Status] = @Status)">
                                    <SelectParameters>
                                        <asp:Parameter DefaultValue="True" Name="Status" Type="Boolean" />
                                    </SelectParameters>
                                </asp:SqlDataSource>
                                </td>
                
            </tr>
            
     <tr>
         <td style=width:20% class="text-right">
             &nbsp;</td>
         <td style=width:80%>
                                <asp:CheckBox ID="CheckBox1" runat="server" Text="ออกใบกำกับภาษีในระบบ" AutoPostBack="True" OnCheckedChanged="CheckBox1_CheckedChanged1" />
                                <br />
                                <asp:CheckBox ID="CheckBox2" runat="server" Text="ระบุชื่อในใบกำกับภาษี" AutoPostBack="True" OnCheckedChanged="CheckBox2_CheckedChanged" />
                                </td>
                
            </tr>
            
     <tr>
         <td class="text-right" colspan="2">
                     <asp:Panel ID="Panel1" runat="server" Width="100%" Visible="false">

                <table style="width: 100%;">
<tr>
    <td style=width:20% class="text-right">
        เบอร์โทรลูกค้า: </td>
    <td style=width:80% class="text-left">
        <asp:TextBox ID="TextBox3" runat="server" Width="80%" AutoPostBack="True" OnTextChanged="TextBox3_TextChanged"></asp:TextBox>
    </td>
           
       </tr>
                    <tr>
                        <td class="text-right" style="width:20%">ชื่อ หรือ ชื่อบริษัท: </td>
                        <td style="width:80%" class="text-left">
                            <asp:DropDownList ID="DropDownList2" runat="server" AppendDataBoundItems="True" AutoPostBack="True" DataSourceID="SqlDataSource2" DataTextField="Customer_Type" DataValueField="ID" Width="25%" OnSelectedIndexChanged="DropDownList2_SelectedIndexChanged">
                                <asp:ListItem Value="0">--- โปรดเลือก ---</asp:ListItem>
                            </asp:DropDownList>
                            <asp:SqlDataSource ID="SqlDataSource2" runat="server" ConnectionString="<%$ ConnectionStrings:TaketimeConnectionString %>" SelectCommand="SELECT * FROM [Customer_Type]"></asp:SqlDataSource>
                            <asp:TextBox ID="TextBox4" runat="server" Width="50%"></asp:TextBox>
                            &nbsp;<asp:TextBox ID="TextBox5" runat="server" placeholder="รหัสสาขา" Width="20%" Visible="false" Text="00000"></asp:TextBox>
                        </td>
                    </tr>
                    <tr>
                        <td class="text-right" style="width:20%">เลขประจำคัวผู้เสียภาษี: </td>
                        <td style="width:80%" class="text-left">
                            <asp:TextBox ID="TextBox6" runat="server" Width="80%" AutoPostBack="True" OnTextChanged="TextBox6_TextChanged"></asp:TextBox>
                        </td>
                    </tr>
                    <tr>
                        <td class="text-right" style="width:20%">ที่อยู่: </td>
                        <td style="width:80%" class="text-left">
                            <asp:TextBox ID="TextBox7" runat="server" Width="15%" placeholder="เลขที่"></asp:TextBox>
                            &nbsp;<asp:TextBox ID="TextBox8" runat="server" placeholder="หมู่ อาคาร ถนน" Width="65%"></asp:TextBox>
                        </td>
                    </tr>
                    <tr>
                        <td class="text-right" style="width:20%">รหัสไปรษณีย์</td>
                        <td style="width:80%" class="text-left">
                            <asp:TextBox ID="TextBox9" runat="server" placeholder="เลขที่" Width="40%" AutoPostBack="True" OnTextChanged="TextBox9_TextChanged"></asp:TextBox>
                        </td>
                    </tr>
                    <tr>
                        <td class="text-right" style="width:20%">&nbsp;</td>
                        <td style="width:80%" class="text-left">
                            <asp:DropDownList ID="DropDownList3" runat="server" AutoPostBack="True"  Width="25%">
                            </asp:DropDownList>
                            &nbsp;&nbsp;&nbsp;<asp:DropDownList ID="DropDownList4" runat="server" AutoPostBack="True"  Width="25%">
                            </asp:DropDownList>
                            &nbsp;&nbsp;
                            <asp:DropDownList ID="DropDownList5" runat="server" Width="25%" AutoPostBack="True" >
                            </asp:DropDownList>
                        </td>
                    </tr>
                    <tr>
                        <td class="text-right" style="width:20%">อีเมล์</td>
                        <td style="width:80%" class="text-left">
                            <asp:TextBox ID="TextBox10" runat="server" Width="70%"></asp:TextBox>
                            &nbsp;<br /> <asp:CheckBox ID="CheckBox3" runat="server" Text="ต้องการรับ e-tax" />
                        </td>
                    </tr>
                    <tr>
                        <td class="text-right" style="width:20%">&nbsp;</td>
                        <td style="width:80%" class="text-left">&nbsp;</td>
                    </tr>
                    <tr>
                        <td class="text-right" style="width:20%">รายละเอียดใบกำกับภาษี</td>
                        <td style="width:80%" class="text-left">
                            <asp:TextBox ID="TextBox11" runat="server" Width="80%" Text="อาหารและเครื่องดื่ม"></asp:TextBox>
                        </td>
                    </tr>
                    </table>

        </asp:Panel></td>
                
            </tr>
            
     <tr>
         <td style=width:20% class="text-right">
             &nbsp;</td>
         <td style=width:80%>
                                <asp:Button ID="Button2" runat="server" Text="บันทึกข้อมูล" Width="50%" OnClick="Button2_Click" CssClass="btn btn-success" />
         </td>
                
            </tr>
            
        </table>
       
    </p>
    <script>
        function button_click(objTextBox, objBtnID) {
            if (window.event.keyCode == 13) {
                document.getElementById(objBtnID).focus();
                document.getElementById(objBtnID).click();
            }
        }
    </script>

    <script>
        function detectEnter(event) {
            if (event.key === "Enter") {
                event.preventDefault(); // Prevent default form submission
                // Trigger the server-side button click event
                __doPostBack('<%= Button3.ClientID %>', '');
            }
        }
    </script>
   
    <asp:Literal ID="Literal1" runat="server"></asp:Literal>
    <rsweb:reportviewer Visible="false" ID="ReportViewer2" runat="server" BackColor="" ClientIDMode="AutoID" HighlightBackgroundColor="" InternalBorderColor="204, 204, 204" InternalBorderStyle="Solid" InternalBorderWidth="1px" LinkActiveColor="" LinkActiveHoverColor="" LinkDisabledColor="" PrimaryButtonBackgroundColor="" PrimaryButtonForegroundColor="" PrimaryButtonHoverBackgroundColor="" PrimaryButtonHoverForegroundColor="" SecondaryButtonBackgroundColor="" SecondaryButtonForegroundColor="" SecondaryButtonHoverBackgroundColor="" SecondaryButtonHoverForegroundColor="" SplitterBackColor="" ToolbarDividerColor="" ToolbarForegroundColor="" ToolbarForegroundDisabledColor="" ToolbarHoverBackgroundColor="" ToolbarHoverForegroundColor="" ToolBarItemBorderColor="" ToolBarItemBorderStyle="Solid" ToolBarItemBorderWidth="1px" ToolBarItemHoverBackColor="" ToolBarItemPressedBorderColor="51, 102, 153" ToolBarItemPressedBorderStyle="Solid" ToolBarItemPressedBorderWidth="1px" ToolBarItemPressedHoverBackColor="153, 187, 226" Height="500px" CssClass="auto-style5" style="margin-top: 172px">
    <LocalReport EnableExternalImages="true" ReportPath="Account\Report\Receipt.rdlc">
        
    </LocalReport>
        </rsweb:reportviewer>
</asp:Content>
