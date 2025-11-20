<%@ Page Title="" Language="C#" MaintainScrollPositionOnPostback="true" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="SellReport.aspx.cs" Inherits="Take_Time_BangPhra.Product.SellReport" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
       <link rel="stylesheet" href="../Content/jquery-ui.css">
<link rel="stylesheet" href="../Content/style.css">
  <link rel="stylesheet" type="text/css" href="../Content/GridView.css">
        <style>
        body 
        {
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
        position: relative;
        display: inline-block;
        width: 100%;
        left: 2px;
        top: 4px;
    }
        </style>
         <table style="width: 100%;">
                 
     <tr>
         <td style=width:20% class="text-right">
             วันที่ </td>
         <td style=width:80%>
              <div class="auto-style1">
             <asp:TextBox ID="TextBox1" runat="server" Width="30%" TextMode="Date"></asp:TextBox>
                  &nbsp;-
             <asp:TextBox ID="TextBox2" runat="server" Width="30%" TextMode="Date"></asp:TextBox>
                  &nbsp; 
                  <asp:Button ID="Button3" runat="server" Text="Search" OnClick="Button3_Click1" Width="20%" CssClass="btn btn-success" />
                  &nbsp;</div>
         </td>
                
            </tr>
            
     <tr>
         <td style=width:20% class="text-right">
             &nbsp;</td>
         <td style=width:80%>
             <asp:GridView ID="GridView1" CssClass="mydatagrid" PagerStyle-CssClass="pager" HeaderStyle-CssClass="header" RowStyle-CssClass="rows" runat="server" AutoGenerateColumns="False" OnRowCommand="GridView1_RowCommand">
                 <Columns>
                     <asp:BoundField DataField="ID" HeaderText="ID" />
                     <asp:BoundField DataField="DateTime_Out" HeaderText="วันที่ขาย" />
                     <asp:BoundField DataField="Category_Name" HeaderText="หมวดหมู่สินค้า" />
                     <asp:BoundField DataField="Product_Name" HeaderText="รายชื่อสินค้า" ReadOnly="True" />
                     <asp:BoundField DataField="Amount" HeaderText="จำนวน" />
                     <asp:BoundField DataField="PricePerUnit" HeaderText="ราคาขายต่อชิ้น" />
                     <asp:BoundField DataField="Price_Total" HeaderText="ราคารวม" ReadOnly="True" />
                     <asp:BoundField DataField="Remark" HeaderText="หมายเหตุ" />
                     <asp:BoundField DataField="Paid_How" HeaderText="วิธีชำระเงิน" />
                     <asp:BoundField DataField="Account_Receipt_ID" HeaderText="หมายเลขใบกำกับ" />
                     <asp:ButtonField ButtonType="Button" CommandName="DeleteItem" Text="ลบ" Visible="False" />
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
                                &nbsp;</td>
                
            </tr>
            
        </table>
       
    </p>
    <script>
    function autocomplete(inp, arr) {
        /*the autocomplete function takes two arguments,
                  the text field element and an array of possible autocompleted values:*/
        var currentFocus;
        /*execute a function when someone writes in the text field:*/
        inp.addEventListener("input", function (e) {
            var a, b, i, val = this.value;
            /*close any already open lists of autocompleted values*/
            closeAllLists();
            if (!val) { return false; }
            currentFocus = -1;
            /*create a DIV element that will contain the items (values):*/
            a = document.createElement("DIV");
            a.setAttribute("id", this.id + "autocomplete-list");
            a.setAttribute("class", "autocomplete-items");
            /*append the DIV element as a child of the autocomplete container:*/
            this.parentNode.appendChild(a);
            /*for each item in the array...*/
            for (i = 0; i < arr.length; i++) {
                /*check if the item starts with the same letters as the text field value:*/
                if (arr[i].substr(0, val.length).toUpperCase() == val.toUpperCase()) {
                    /*create a DIV element for each matching element:*/
                    b = document.createElement("DIV");
                    /*make the matching letters bold:*/
                    b.innerHTML = "<strong>" + arr[i].substr(0, val.length) + "</strong>";
                    b.innerHTML += arr[i].substr(val.length);
                    /*insert a input field that will hold the current array item's value:*/
                    b.innerHTML += "<input type='hidden' value='" + arr[i] + "'>";
                    /*execute a function when someone clicks on the item value (DIV element):*/
                    b.addEventListener("click", function (e) {
                        /*insert the value for the autocomplete text field:*/
                        inp.value = this.getElementsByTagName("input")[0].value;
                        /*close the list of autocompleted values,
                        (or any other open lists of autocompleted values:*/
                        closeAllLists();
                    });
                    a.appendChild(b);
                }
            }
        });
        /*execute a function presses a key on the keyboard:*/
        inp.addEventListener("keydown", function (e) {
            var x = document.getElementById(this.id + "autocomplete-list");
            if (x) x = x.getElementsByTagName("div");
            if (e.keyCode == 40) {
                /*If the arrow DOWN key is pressed,
                increase the currentFocus variable:*/
                currentFocus++;
                /*and and make the current item more visible:*/
                addActive(x);
            } else if (e.keyCode == 38) { //up
                /*If the arrow UP key is pressed,
                decrease the currentFocus variable:*/
                currentFocus--;
                /*and and make the current item more visible:*/
                addActive(x);
            } else if (e.keyCode == 13) {
                /*If the ENTER key is pressed, prevent the form from being submitted,*/
                e.preventDefault();
                if (currentFocus > -1) {
                    /*and simulate a click on the "active" item:*/
                    if (x) x[currentFocus].click();
                }
            }
        });
        function addActive(x) {
            /*a function to classify an item as "active":*/
            if (!x) return false;
            /*start by removing the "active" class on all items:*/
            removeActive(x);
            if (currentFocus >= x.length) currentFocus = 0;
            if (currentFocus < 0) currentFocus = (x.length - 1);
            /*add class "autocomplete-active":*/
            x[currentFocus].classList.add("autocomplete-active");
        }
        function removeActive(x) {
            /*a function to remove the "active" class from all autocomplete items:*/
            for (var i = 0; i < x.length; i++) {
                x[i].classList.remove("autocomplete-active");
            }
        }
        function closeAllLists(elmnt) {
            /*close all autocomplete lists in the document,
                        except the one passed as an argument:*/
            var x = document.getElementsByClassName("autocomplete-items");
            for (var i = 0; i < x.length; i++) {
                if (elmnt != x[i] && elmnt != inp) {
                    x[i].parentNode.removeChild(x[i]);
                }
            }
        }
        /*execute a function when someone clicks in the document:*/
        document.addEventListener("click", function (e) {
            closeAllLists(e.target);
        });
    }

    </script>
    <asp:Literal ID="Literal1" runat="server"></asp:Literal>
    
</asp:Content>
