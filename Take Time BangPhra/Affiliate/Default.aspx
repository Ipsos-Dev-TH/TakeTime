<%@ Page Title="" Language="C#" MaintainScrollPositionOnPostback="true" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="Take_Time_BangPhra.Affiliate.Default" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
     <style>

                 .mycheckbox input[type="checkbox"] 
{ 
    margin-right: 5px; 
}

         .radioBL input[type="radio"]
    {
        margin-right:10px;
    }
        .rounded-textbox {
    border-radius: 10px; /* Adjust the value for more/less rounding */
    padding: 5px;
    border: 1px solid #ccc; /* Optional: Border styling */
    outline: none; /* Optional: Remove focus outline */
}
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
            height: 46px;
        }
        .auto-style2 {
            text-align: left;
            height: 46px;
        }
        </style>
    <br />
    <div class="ExampleFont">
    <table style="width: 100%;">
        <tr>
            <td style="height: 20px;width:20%" class="text-right">ชื่อ: </td>
            <td style="height: 20px;width:80%" class="text-left">
                <asp:TextBox ID="TextBox1" runat="server" Width="80%" Enabled="False" CssClass="rounded-textbox"></asp:TextBox>
            </td>
           
        </tr>
        <tr style="background-color:whitesmoke;">
            <td class="text-right">ธนาคาร:</td>
            <td class="text-left">
                <asp:TextBox ID="TextBox2" runat="server" Width="80%" Enabled="False" CssClass="rounded-textbox"></asp:TextBox>
            </td>
            
        </tr>
    <center>
        <tr>
            <td class="text-right">หมายเลขบัญชี:</td>
        </center>
            <td class="text-left">
                <asp:TextBox ID="TextBox3" runat="server" Width="80%" Enabled="False" CssClass="rounded-textbox"></asp:TextBox>
            </td>
           
        </tr>
        <tr style="background-color:whitesmoke;">
            <td class="text-right">รหัสคูปองของฉัน: </td>
            <td class="text-left">
                <asp:TextBox ID="TextBox4" runat="server" Width="80%" Enabled="False"  CssClass="rounded-textbox"></asp:TextBox>
            </td>
           
        </tr>
        <tr>
            <td class="auto-style1">ลิงค์สำหรับลงจองด้วยคูปองของฉัน: </td>
            <td class="auto-style2">
                <asp:TextBox ID="TextBox5" runat="server" Width="80%" Enabled="False" TextMode="MultiLine" CssClass="rounded-textbox"></asp:TextBox>
            </td>
           
        </tr>
        <tr style="background-color:whitesmoke;">
            <td class="text-right">&nbsp;</td>
            <td class="text-left">
                <asp:TextBox ID="TextBox6" runat="server" Width="80%" Enabled="False" CssClass="rounded-textbox" ></asp:TextBox>
            </td>
           
        </tr>
        <tr style="background-color:whitesmoke;">
            <td class="text-right">&nbsp;</td>
            <td class="text-left">
                <asp:DropDownList ID="DropDownList1" runat="server" Width="60%" CssClass="rounded-textbox">
                    <asp:ListItem>--- กรุณาเลือกรายงานที่ต้องการ ---</asp:ListItem>
                    <asp:ListItem Value="1">รายการจองที่ยังไม่ได้เข้าพัก</asp:ListItem>
                    <asp:ListItem Value="2">รายการจองที่เข้าพักแล้วรอรอบโอนเงิน</asp:ListItem>
                    <asp:ListItem Value="3">รายการจองที่ได้รับเงินแล้ว</asp:ListItem>
                    <asp:ListItem Value="4">รายการจองที่ยกเลิกการเข้าพัก</asp:ListItem>
                    
                </asp:DropDownList>
                &nbsp;<asp:Button ID="Button2" runat="server" Text="ค้นหา" Width="30%" OnClick="Button2_Click" CssClass="rounded-textbox"/>
            </td>
           
        </tr>
        <tr style="background-color:whitesmoke;">
            <td class="text-right">&nbsp;</td>
            <td class="text-left">
                <asp:GridView ID="GridView1" runat="server" AllowPaging="True" OnPageIndexChanging="GridView1_PageIndexChanging" PageSize="20" OnRowCommand="GridView1_RowCommand">
                    <Columns>
                        <asp:ButtonField ButtonType="Button" Text="ใบสำคัญจ่าย" CommandName="REC" />
                        <asp:ButtonField ButtonType="Button" Text="สลิป" CommandName="Slip"/>
                        <asp:ButtonField ButtonType="Button" Text="ใบหักภาษี" CommandName="TAX" />
                    </Columns>
                </asp:GridView>
            </td>
           
        </tr>
    </table>
        </div>
        </asp:Content>
