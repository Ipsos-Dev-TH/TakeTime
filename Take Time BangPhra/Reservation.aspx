<%@ Page Title="" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Reservation.aspx.cs" Inherits="Take_Time_BangPhra.Reservation" %>
<%@ Register assembly="Microsoft.ReportViewer.WebForms" namespace="Microsoft.Reporting.WebForms" tagprefix="rsweb" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
     <link rel="stylesheet" href="/Content/jquery-ui.css">
  <link rel="stylesheet" href="/Content/style.css">
    <link rel="stylesheet" type="text/css" href="/Content/GridView.css">
    <script>
         function setHourglass()
         {
             document.getElementById("MainContent_Button1").disabled = true;
             document.body.style.cursor = 'Wait';
        }
    </script>


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
                .auto-style1 {
                    width: 25%;
                    height: 35px;
                }
                .auto-style2 {
                    width: 75%;
                    height: 35px;
                }
                .auto-style3 {
                    width: 25%;
                    height: 32px;
                }
                .auto-style4 {
                    width: 75%;
                    height: 32px;
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
        .auto-style5 {
            height: 10px;
        }
        </style>
     <br />
    <br />
    <center>
        <div class="ExampleFont">
    <div>
        <table style="width:100%;">
                     <tr>
                         <td>
                    <table style="width:100%;background-color:white;border-radius:10px">
                        <tr>
                            <td style="width:20%; height: 20px;text-align:right;">&nbsp;</td>
                            <td style="width:75%; height: 20px;" class="text-left">
                                <asp:TextBox ID="TextBox11" runat="server" TextMode="DateTime" Visible="False"></asp:TextBox>
                            </td>
                        </tr>
                        <tr>
                            <td style="width:20%; height: 20px;text-align:right;">วันที่จอง:<br />
                                Check-In Date: </td>
                            <td style="width:75%; height: 20px;text-align:left;">
                                <asp:TextBox ID="TextBox12" runat="server" AutoPostBack="True" TextMode="Date" Width="40%" OnTextChanged="TextBox12_TextChanged"></asp:TextBox>
                                &nbsp;<asp:Button ID="Button2" runat="server" Text="ยกเลิกการเลือกวัน" OnClick="Button2_Click" Width="20%" />
                            </td>
                        </tr>
                        <tr style="background-color:whitesmoke;">
                            <td style="width:20%; height: 20px;text-align:right;">กี่คืน:<br />
                                How many Night(s):</td>
                            <td style="width:75%; height: 20px;text-align:left;">
                                <asp:DropDownList ID="DropDownList1" runat="server" AutoPostBack="True" OnSelectedIndexChanged="DropDownList1_SelectedIndexChanged" Width="100px">
                                    <asp:ListItem Value="1">1 คืน</asp:ListItem>
                                    <asp:ListItem Value="2">2 คืน</asp:ListItem>
                                    <asp:ListItem Value="3">3 คืน</asp:ListItem>
                                    <asp:ListItem Value="4">4 คืน</asp:ListItem>
                                    <asp:ListItem Value="5">5 คืน</asp:ListItem>
                                    <asp:ListItem Value="6">6 คืน</asp:ListItem>
                                    <asp:ListItem Value="7">7 คืน</asp:ListItem>
                                </asp:DropDownList>
&nbsp;&nbsp;
                                <asp:Label ID="Label1" runat="server" Text="Check-Out: "></asp:Label>
                            &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;
                                <asp:Button ID="Button4" runat="server" Text="เหมาลาน" Visible="False" OnClick="Button4_Click" />
                            </td>
                        </tr>
                        <tr>
                            <td style="width:20%; height: 20px;text-align:right;">ประเภทที่พัก:<br />
                                Accommodation Type: </td>
                            <td style="width:75%; height: 20px;text-align:left;">
                    <asp:GridView ID="GridView1" runat="server" AutoGenerateColumns="False" CssClass="mydatagrid" PagerStyle-CssClass="pager" HeaderStyle-CssClass="header" RowStyle-CssClass="rows" OnRowCommand="GridView1_RowCommand" OnRowCancelingEdit="GridView1_RowCancelingEdit" OnRowEditing="GridView1_RowEditing" OnRowUpdating="GridView1_RowUpdating">
                        <Columns>
                            <asp:TemplateField HeaderText="เลือก" HeaderStyle-Width="5%">
                             <ItemTemplate>
                                 <asp:CheckBox ID="chkSelect" runat="server" Width="100%" CommandName="Check" AutoPostBack="true"/>

                            </ItemTemplate>
                                <HeaderStyle Width="5%"></HeaderStyle>
                            </asp:TemplateField>
                            <asp:BoundField DataField="AccomName" HeaderText="รายชื่อห้องพัก" HeaderStyle-CssClass="header-center" ReadOnly="true" >
<HeaderStyle CssClass="header-center"></HeaderStyle>
                            </asp:BoundField>
                            <asp:TemplateField HeaderText="จำนวนผู้เข้าพัก" HeaderStyle-Width="20%" HeaderStyle-CssClass="header-center" ItemStyle-CssClass="header-center">
                             <ItemTemplate>
                                <asp:TextBox ID="txtPeopleStay" runat="server" Width="100%" Text='0' TextMode="Number" Enabled="false" AutoPostBack="true"/>
                            </ItemTemplate>
                                <HeaderStyle Width="20%"></HeaderStyle>

<ItemStyle CssClass="header-center"></ItemStyle>
                            </asp:TemplateField>
                            <asp:BoundField DataField="People" HeaderText="จำนวนผู้เข้าพักสูงสุด" HeaderStyle-CssClass="header-center" ItemStyle-CssClass="header-center" ReadOnly="true">

<HeaderStyle CssClass="header-center"></HeaderStyle>

<ItemStyle CssClass="header-center"></ItemStyle>
                            </asp:BoundField>

                            <asp:BoundField DataField="Price" HeaderText="ราคาต่อคืน" HeaderStyle-CssClass="header-center" ItemStyle-CssClass="header-center">
<HeaderStyle CssClass="header-center"></HeaderStyle>

<ItemStyle CssClass="header-center"></ItemStyle>
                            </asp:BoundField>
                            <asp:CommandField ButtonType="Button" HeaderText="แก้ไข" ShowEditButton="True" HeaderStyle-CssClass="header-center" ItemStyle-CssClass="header-center" />
                        </Columns>

<HeaderStyle CssClass="header"></HeaderStyle>

<PagerStyle CssClass="pager"></PagerStyle>

<RowStyle CssClass="rows"></RowStyle>
                                </asp:GridView>
                            </td>
                        </tr>
                        <tr style="background-color:whitesmoke;">
                            <td style="width:20%; height: 20px;text-align:right;">เบอร์โทรศัพท์:<span style="color: #FF0000">*<br />
                                </span>Telephone Number:<span style="color: #FF0000">*</span></td>
                            <td style="width:75%; height: 20px;text-align:left;">
                                <asp:TextBox ID="TextBox1" runat="server" Width="60%" AutoPostBack="True" OnTextChanged="TextBox1_TextChanged"></asp:TextBox>
                            &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;
                                <asp:Button ID="Button5" runat="server" OnClick="Button5_Click" Text="Button" Visible="False" />
                            </td>
                        </tr>
                        <tr>
                            <td style="width:20%; height: 20px;text-align:right;">ชื่อ นามสกุล:<span style="color: #FF0000">*<br />
                                </span>Full Name:<span style="color: #FF0000">*</span></td>
                            <td style="width:75%; height: 20px;text-align:left;">
                    <asp:DropDownList ID="DropDownList8" runat="server" Width="20%" AutoPostBack="True" OnSelectedIndexChanged="DropDownList8_SelectedIndexChanged">
                    </asp:DropDownList>
                                <asp:TextBox ID="TextBox2" runat="server" Width="40%" ></asp:TextBox>
                            &nbsp;<asp:TextBox ID="TextBox18" runat="server" Width="20%" PlaceHolder="รหัสสาขา" Visible="false" ></asp:TextBox>
                            </td>
                        </tr>
                        <tr style="background-color:whitesmoke;">
                            <td style="width:20%; height: 20px;text-align:right;">ชื่อ Facebook หรือ ID Line:<br />Facebook Name/Line ID: </td>
                            <td style="width:75%; height: 20px;text-align:left;">
                                <asp:TextBox ID="TextBox3" runat="server" Width="60%"></asp:TextBox>
                             </td>
                        </tr>
                        <tr>
                            <td style="width:20%; height: 20px;text-align:right;">&nbsp;</td>
                            <td style="width:75%; height: 20px;text-align:left;">
                                <asp:CheckBox ID="CheckBox3" runat="server" Text="ไม่รับใบกำกับภาษี" AutoPostBack="True" Checked="True" OnCheckedChanged="CheckBox3_CheckedChanged" />
                                &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;
                                <br />
                                <asp:CheckBox ID="CheckBox4" runat="server" Text="ไม่ออกใบกำกับภาษีในระบบ !!!" Visible="false" OnCheckedChanged="CheckBox4_CheckedChanged" AutoPostBack="True"/>
                             </td>
                        </tr>
                         <tr style="background-color:whitesmoke;">
                            <td style="text-align:right;" class="auto-style5" colspan="2">
                                <asp:Panel ID="Panel1" runat="server" Visible="False">
                                    <table style="width:100%;">
                                        <tr style="background-color:whitesmoke;">
    <td style="text-align:right;" class="auto-style3">ที่อยู่ <br /> Address:</td>
    <td style="text-align:left;" class="auto-style4">
        <asp:TextBox ID="TextBox8" runat="server" Width="20%" PlaceHolder="เลขที่" ></asp:TextBox>
        &nbsp;<asp:TextBox ID="TextBox17" runat="server" Width="40%" PlaceHolder="หมู่ ซอย"></asp:TextBox>
     </td>
</tr>
                                        <tr style="background-color:whitesmoke;">
                                            <td class="auto-style3" style="text-align:right;">รหัสไปรษณีย์<br /> Zip Code:</td>
                                            <td class="auto-style4" style="text-align:left;">   <asp:TextBox ID="TextBox16" runat="server" Width="30%" AutoPostBack="True" OnTextChanged="TextBox16_TextChanged" TextMode="Number"></asp:TextBox>
&nbsp;&nbsp;
   <asp:Button ID="Button6" runat="server" Text="ค้นหา" Width="20%" OnClick="Button6_Click"   />
&nbsp;&nbsp;
   <asp:Button ID="Button7" runat="server" Text="ยกเลิก" Width="20%" OnClick="Button7_Click"  /></td>
                                        </tr>
                                        <tr style="background-color:whitesmoke;">
                                            <td class="auto-style3" style="text-align:right;">จังหวัด/อำเภอ/ตำบล</td>
                                            <td class="auto-style4" style="text-align:left;">    <asp:DropDownList ID="DropDownList5" runat="server" Width="25%" AutoPostBack="True" OnSelectedIndexChanged="DropDownList5_SelectedIndexChanged">
    </asp:DropDownList>
&nbsp;&nbsp;
    <asp:DropDownList ID="DropDownList6" runat="server" Width="25%" AutoPostBack="True" OnSelectedIndexChanged="DropDownList6_SelectedIndexChanged">
    </asp:DropDownList>
&nbsp;&nbsp;
    <asp:DropDownList ID="DropDownList7" runat="server" Width="25%">
    </asp:DropDownList></td>
                                        </tr>
 <tr>
    <td style="text-align:right;" class="auto-style3">เลขบัตรประชาชน<br /> Passport Number:</td>
    <td style="text-align:left;" class="auto-style4">
        <asp:TextBox ID="TextBox9" runat="server" Width="60%" AutoPostBack="True" OnTextChanged="TextBox9_TextChanged"></asp:TextBox>
     </td>
</tr>
 <tr style="background-color:whitesmoke;">
    <td style="text-align:right;" class="auto-style3">อีเมล<br />
        Email:</td>
    <td style="text-align:left;" class="auto-style4">
        <asp:TextBox ID="TextBox13" runat="server" Width="60%" AutoPostBack="True" OnTextChanged="TextBox9_TextChanged" TextMode="Email"></asp:TextBox>
        <asp:CheckBox ID="CheckBox5" runat="server" AutoPostBack="True"  Text="ต้องการรับ e tax invoice" Visible="False" OnCheckedChanged="CheckBox5_CheckedChanged" />
     </td>
</tr>
                                    </table>
                                </asp:Panel>
                             </td>
                        </tr>
                         <tr style="background-color:whitesmoke;">
                            <td style="width:20%;text-align:right;">รูปภาพของเช่า:<br />Rent Items Picture:</td>
                            <td style="width:75%;text-align:left;">
                                <asp:HyperLink ID="HyperLink1" runat="server" NavigateUrl="./Images/อุปกรณ์เช่า.png" Target="_blank">กดเพื่อดูรูปภาพของเช่า(Click)</asp:HyperLink></td>
                        </tr>
                         <tr>
                            <td style="width:20%;text-align:right; height: 127px;">รายการของเช่า:<br /> Rent Items:</td>
                            <td style="width:75%;text-align:left; height: 127px;">
                    <asp:GridView ID="GridView2" runat="server" AutoGenerateColumns="False" CssClass="mydatagrid" PagerStyle-CssClass="pager" HeaderStyle-CssClass="header" RowStyle-CssClass="rows" OnRowCancelingEdit="GridView2_RowCancelingEdit" OnRowEditing="GridView2_RowEditing" OnRowUpdating="GridView2_RowUpdating">
                        <Columns>
                             <asp:TemplateField HeaderText="เลือก" HeaderStyle-Width="5%">
                             <ItemTemplate>
                                 <asp:CheckBox ID="chkSelect" runat="server" Width="100%" CommandName="Check" AutoPostBack="true"/>

                            </ItemTemplate>
                                <HeaderStyle Width="5%"></HeaderStyle>
                            </asp:TemplateField>
                            <asp:BoundField DataField="ItemName" HeaderText="รายการของเช่า" HeaderStyle-CssClass="header-center" ReadOnly="true">
<HeaderStyle CssClass="header-center"></HeaderStyle>
                             </asp:BoundField>
                             <asp:TemplateField HeaderText="จำนวนที่ต้องการเช่า" HeaderStyle-Width="20%" HeaderStyle-CssClass="header-center" ItemStyle-CssClass="header-center">
                             <ItemTemplate>
                                <asp:TextBox ID="txtAmount" runat="server" Width="100%" Text='0' TextMode="Number" Enabled="false" AutoPostBack="true"/>
                            </ItemTemplate>
                                <HeaderStyle Width="20%"></HeaderStyle>

<ItemStyle CssClass="header-center"></ItemStyle>
                            </asp:TemplateField>
                            <asp:BoundField DataField="Amount" HeaderText="จำนวนคงเหลือ" HeaderStyle-CssClass="header-center" ItemStyle-CssClass="header-center" ReadOnly="true">
<HeaderStyle CssClass="header-center"></HeaderStyle>

<ItemStyle CssClass="header-center"></ItemStyle>
                             </asp:BoundField>
                            <asp:BoundField DataField="Price" HeaderText="ราคาต่อชิ้น" HeaderStyle-CssClass="header-center" ItemStyle-CssClass="header-center" >
<HeaderStyle CssClass="header-center"></HeaderStyle>

<ItemStyle CssClass="header-center"></ItemStyle>
                             </asp:BoundField>
                             <asp:CommandField ButtonType="Button" HeaderText="แก้ไข" ShowEditButton="True" HeaderStyle-CssClass="header-center" ItemStyle-CssClass="header-center"/>
                        </Columns>

<HeaderStyle CssClass="header"></HeaderStyle>

<PagerStyle CssClass="pager"></PagerStyle>

<RowStyle CssClass="rows"></RowStyle>
                    </asp:GridView>
                             </td>
                        </tr>
                         <tr style="background-color:whitesmoke;">
                            <td style="width:20%;text-align:right;">ราคารวม:<br />Total price:</td>
                            <td style="width:75%;text-align:left;">
                                <asp:TextBox ID="TextBox4" runat="server" Enabled="False" OnTextChanged="TextBox4_TextChanged" Width="60%" TextMode="Number">0</asp:TextBox>
                             &nbsp;บาท<br />
                                ยอดมัดจำจองขั้นต่ำ Minimum Deposit:
                                <asp:Label ID="Label2" runat="server" Text=""></asp:Label>
                             </td>
                        </tr>
                         <tr>
                            <td style="text-align:right;" class="auto-style1">ยอดเงินที่โอนเพื่อมัดจำจอง:<br />Deposit Amount:</td>
                            <td style="text-align:left;" class="auto-style2">
                                <asp:TextBox ID="TextBox5" runat="server" AutoPostBack="True" Width="60%" TextMode="Number" OnTextChanged="TextBox5_TextChanged">0</asp:TextBox>
                             &nbsp;บาท<br />
                                <asp:DropDownList ID="DropDownList2" Width="60%" runat="server" DataSourceID="SqlDataSource1" DataTextField="Paid_How" DataValueField="ID" Enabled="False" AutoPostBack="True" OnSelectedIndexChanged="DropDownList2_SelectedIndexChanged" AppendDataBoundItems="true">
                                </asp:DropDownList>
                                <asp:SqlDataSource ID="SqlDataSource1" runat="server" ConnectionString="<%$ ConnectionStrings:TaketimeConnectionString %>" SelectCommand="SELECT * FROM [Account_Paid_How] WHERE ([Status] = @Status)">
                                    <SelectParameters>
                                        <asp:Parameter DefaultValue="True" Name="Status" Type="Boolean" />
                                    </SelectParameters>
                                </asp:SqlDataSource>
                                <br />
                                <asp:Label ID="Label7" runat="server" Text="" Visible ="false"></asp:Label>
                             </td>
                        </tr>
                         <tr>
                            <td style="text-align:right;" class="auto-style1">&nbsp;</td>
                            <td style="text-align:left;" class="auto-style2">
                                <asp:CheckBox ID="CheckBox2" runat="server" Text="มัดจำเพิ่ม" AutoPostBack="True" OnCheckedChanged="CheckBox2_CheckedChanged" Visible="False" />
&nbsp;<asp:TextBox ID="TextBox10" runat="server" TextMode="Number" Visible="False" AutoPostBack="True" OnTextChanged="TextBox10_TextChanged">จำนวนเงิน</asp:TextBox>
                             </td>
                        </tr>
                         <tr>
                            <td style="width:20%;text-align:right;">อัพโหลดสลิป:<br />Transfer Slip Upload:</td>
                            <td style="width:75%;text-align:left;">
                                <table style="width:100%">
                                    <tr>
                                        <td style="width:100%"><asp:FileUpload ID="FileUpload1" runat="server" OnDataBinding="FileUpload1_DataBinding" Width="350px" />
                                            <asp:Button ID="Button3" runat="server" Text="Upload Picture" Width="127px" OnClick="Button3_Click" />
                                            <br />
&nbsp;*สามารถโอนยอดมัดจำจองขั้นต่ำ หรือ โอนชำระเต็มจำนวนได้เลยค่ะ<br />
                                            <asp:Image ID="Image1" runat="server" width="90%" />
                                        </td>
                                        <td style="width:0%">&nbsp;</td>
                                    </tr>
                                </table>
                                </td>
                        </tr>
                         <tr>
                            <td style="width:20%;text-align:right;">หมายเหตุ:<br />Remark:</td>
                            <td style="width:75%;text-align:left;">
                                <asp:TextBox ID="TextBox6" runat="server" Width="60%" TextMode="MultiLine"></asp:TextBox>
                                </td>
                        </tr>
                         <tr style="background-color:whitesmoke;">
                            <td style="width:20%;text-align:right;">&nbsp;</td>
                            <td style="width:75%;text-align:left;">
                                <img src="./Images/กฏระเบียบ.png" width="90%" /></td>
                        </tr>
                         <tr style="background-color:whitesmoke;">
                            <td style="width:20%;text-align:right;">&nbsp;</td>
                            <td style="width:75%;text-align:left;">
                                <asp:CheckBox ID="CheckBox1" runat="server" AutoPostBack="True" OnCheckedChanged="CheckBox1_CheckedChanged" />
                                &nbsp;&nbsp; ***ติ๊กเลือกเพื่อยอมรับกติกาด้านบน และรับทราบเรื่องการห้ามใช้เสียงดังหลัง 22.30 น. (Accept the rule)<br />
                                <br />
                                <asp:Button ID="Button1" runat="server" Text="ยืนยันการจอง(Submit)" Height="60px" Width="50%" OnClick="Button1_Click" Enabled="False" />
                                </td>
                        </tr>
                    </table>
                </td>
            </tr>
        </table>
    </div></div>
        </center>
    <br />
    <br />
    <rsweb:reportviewer Visible="false" ID="ReportViewer2" runat="server" Width="100%" BackColor="" ClientIDMode="AutoID" HighlightBackgroundColor="" InternalBorderColor="204, 204, 204" InternalBorderStyle="Solid" InternalBorderWidth="1px" LinkActiveColor="" LinkActiveHoverColor="" LinkDisabledColor="" PrimaryButtonBackgroundColor="" PrimaryButtonForegroundColor="" PrimaryButtonHoverBackgroundColor="" PrimaryButtonHoverForegroundColor="" SecondaryButtonBackgroundColor="" SecondaryButtonForegroundColor="" SecondaryButtonHoverBackgroundColor="" SecondaryButtonHoverForegroundColor="" SplitterBackColor="" ToolbarDividerColor="" ToolbarForegroundColor="" ToolbarForegroundDisabledColor="" ToolbarHoverBackgroundColor="" ToolbarHoverForegroundColor="" ToolBarItemBorderColor="" ToolBarItemBorderStyle="Solid" ToolBarItemBorderWidth="1px" ToolBarItemHoverBackColor="" ToolBarItemPressedBorderColor="51, 102, 153" ToolBarItemPressedBorderStyle="Solid" ToolBarItemPressedBorderWidth="1px" ToolBarItemPressedHoverBackColor="153, 187, 226" Height="500px">
        <LocalReport ReportPath="Account\Report\Receipt.rdlc" EnableExternalImages="True">
            
        </LocalReport>
</rsweb:reportviewer>
</asp:Content>
