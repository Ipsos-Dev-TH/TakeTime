<%@ Page Title="" Language="C#" MaintainScrollPositionOnPostback="true" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="In.aspx.cs" Inherits="Take_Time_BangPhra.Product.In" %>

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
        </style>
         <table style="width: 100%;">
                 
     <tr>
         <td style=width:20% class="text-right">
             &nbsp;</td>
         <td style=width:80%>
              &nbsp;</td>
                
            </tr>
            
     <tr>
         <td style=width:20% class="text-right">
             รหัสหรือชื่อสินค้า: </td>
         <td style=width:80%>
              <div class="autocomplete" style="width:100%">
             <asp:TextBox ID="TextBox1" runat="server" Width="50%" autocomplete="off"></asp:TextBox>
                  &nbsp; 
                  <button id="btnScanBarcode" runat="server" class="btn btn-primary btn-success" style="width:30%;text-align:center" type="button">
                      Scan Barcode
                  </button>
                  <div id="scannerModal" style="display: none; position: fixed; top: 0; left: 0; width: 100%; height: 100%; background: black; z-index: 1000;">
                      <div id="interactive" style="width:100%; height:80vh; position:relative;">
        <!-- This is where the camera feed will appear -->
                          <video id="scannerVideo" autoplay="" playsinline="" style="width:100%; height:100%; object-fit:cover;">
                          </video>
                      </div>
                      <button id="closeScanner" style="position:fixed; bottom:20px; left:50%; transform:translateX(-50%); padding:10px 20px;">
                          Close Scanner
                      </button>
                  </div>
                  </div>
         </td>
                
            </tr>
            
     <tr>
         <td style=width:20% class="text-right">
             จำนวน: </td>
         <td style=width:80%>
             <asp:TextBox ID="TextBox2" runat="server" Width="50%" TextMode="Number"></asp:TextBox>
                  </td>
                
            </tr>
            
     <tr>
         <td style=width:20% class="text-right">
             ราคาต่อชิ้น: </td>
         <td style=width:80%>
             <asp:TextBox ID="TextBox3" runat="server" Width="50%"></asp:TextBox>
                  </td>
                
            </tr>
            
     <tr>
         <td style=width:20% class="text-right">
             &nbsp;</td>
         <td style=width:80%>
              <asp:Button ID="Button3" runat="server" Text="ADD" Width="30%" OnClick="Button3_Click" CssClass="btn btn-success"/>
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
         <td class="text-right" colspan="2">
                     <asp:Panel ID="Panel1" runat="server" Width="100%" Visible="false">

        </asp:Panel></td>
                
            </tr>
            
     <tr>
         <td style=width:20% class="text-right">
             &nbsp;</td>
         <td style=width:80%>
                                <asp:Button ID="Button2" runat="server" Text="บันทึกข้อมูล" Width="50%" OnClick="Button2_Click" CssClass="btn btn-success"  />
         </td>
                
            </tr>
            
        </table>
       
    </p>
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
    <asp:Literal ID="Literal1" runat="server"></asp:Literal>
    
</asp:Content>
