<%@ Page Title="" Language="C#" MaintainScrollPositionOnPostback="true" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="HolidayPrice.aspx.cs" Inherits="Take_Time_BangPhra.Admin.HolidayPrice" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
        <link rel="stylesheet" href="/Content/jquery-ui.css">
  <link rel="stylesheet" href="/Content/style.css">
    <link rel="stylesheet" type="text/css" href="/Content/GridView.css">
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
    </style>
    <p>
        <br />
        <center>
            <asp:Calendar ID="Calendar1" runat="server" Width="80%" OnDayRender="Calendar1_DayRender" OnSelectionChanged="Calendar1_SelectionChanged"></asp:Calendar>
            <br />


                    <div class="mobile-table-wrapper">
                    <asp:GridView ID="GridView1" runat="server" AutoGenerateColumns="False" CssClass="mydatagrid gridview-table" PagerStyle-CssClass="pager" HeaderStyle-CssClass="header" RowStyle-CssClass="rows"  >
                        <Columns>
                            <asp:TemplateField HeaderText="เลือก" HeaderStyle-Width="5%">
                             <ItemTemplate>
                                 <asp:CheckBox ID="chkSelect" runat="server" Width="100%" CommandName="Check" AutoPostBack="true"/>

                            </ItemTemplate>
                                <HeaderStyle Width="5%"></HeaderStyle>
                            </asp:TemplateField>
                            <asp:BoundField DataField="ID" HeaderText="ID" HeaderStyle-CssClass="header-center"/>
                            <asp:BoundField DataField="AccomName" HeaderText="รายชื่อห้องพัก" HeaderStyle-CssClass="header-center" ReadOnly="true" >
<HeaderStyle CssClass="header-center"></HeaderStyle>
                            </asp:BoundField>

                              <asp:TemplateField HeaderText="ราคา"  HeaderStyle-CssClass="header-center" ItemStyle-CssClass="header-center">
                             <ItemTemplate>
                                <asp:TextBox ID="txtPrice" runat="server" Width="100%" Text='0' TextMode="Number" AutoPostBack="true"/>
                            </ItemTemplate>
                                <ItemStyle CssClass="header-center"></ItemStyle>
                            </asp:TemplateField>

                            <asp:TemplateField HeaderText="หมายเหตุ"  HeaderStyle-CssClass="header-center" ItemStyle-CssClass="header-center">
                             <ItemTemplate>
                                <asp:TextBox ID="txtRemark" runat="server" Width="100%" AutoPostBack="true"/>
                            </ItemTemplate>
                                <ItemStyle CssClass="header-center"></ItemStyle>
                            </asp:TemplateField>
                        </Columns>

<HeaderStyle CssClass="header"></HeaderStyle>

<PagerStyle CssClass="pager"></PagerStyle>

<RowStyle CssClass="rows"></RowStyle>
                                </asp:GridView>
                    </div>
            <br />
            <asp:Button ID="Button1" runat="server" Text="Submit" Width="166px" Height="48px" OnClick="Button1_Click" />
        </center>
    <center>
        <br />
        <asp:Button ID="Button2" runat="server" Text="Show Holiday Price" Width="159px" OnClick="Button2_Click" />
    </center>
    
    </p>
    <asp:Panel ID="Panel1" runat="server" Visible="False"><center>

                                          <div class="mobile-table-wrapper">
                                          <asp:GridView ID="GridView2" runat="server" AutoGenerateColumns="False" OnRowDeleting="GridView2_RowDeleting" CssClass="mydatagrid gridview-table" PagerStyle-CssClass="pager" HeaderStyle-CssClass="header" RowStyle-CssClass="rows">
                                              <Columns>
                                                  <asp:BoundField DataField="Accommodation_ID" HeaderText="Accommodation_ID" />
                                                  <asp:BoundField DataField="DateNewPrice" HeaderText="Date" DataFormatString="{0:dd/MMM/yyyy}" />
                                                  <asp:BoundField DataField="Price" HeaderText="Price" />
                                                  <asp:BoundField DataField="Remark" HeaderText="Remark" />
                                                  <asp:CommandField ShowDeleteButton="True" ButtonType="Button" />
                                              </Columns>
                                              <HeaderStyle CssClass="header" />
                                              <PagerStyle CssClass="pager" />
                                              <RowStyle CssClass="rows" />
                                          </asp:GridView>
                                          </div>

                                          </center>
    </asp:Panel>
</asp:Content>

