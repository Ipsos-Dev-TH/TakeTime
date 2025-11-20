<%@ Page MaintainScrollPositionOnPostback="true" Title="" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="Take_Time_BangPhra.Voucher.Default" %>
<%@ Register assembly="Microsoft.ReportViewer.WebForms" namespace="Microsoft.Reporting.WebForms" tagprefix="rsweb" %>
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
                    width: 20%;
                    height: 32px;
                }
                .auto-style2 {
                    height: 32px;
                }
                .auto-style3 {
                    color: #FF0000;
                }
                .auto-style4 {
                    width: 20%;
                    height: 30px;
                }
                .auto-style5 {
                    height: 30px;
                }
                .auto-style6 {
                    width: 20%;
                    height: 35px;
                }
                .auto-style7 {
                    height: 35px;
                }
                .auto-style8 {
                    width: 20%;
                    height: 37px;
                }
                .auto-style9 {
                    height: 37px;
                }
                </style>

    
    <p>
        &nbsp;</p>
    <p class="text-center">
        <strong>ขาย Voucher</strong><div class="text-center">
            <br />
        </div>
        <table style="width:100%;">
            <tr style="background-color:whitesmoke;">
                <td class="modal-sm" style="width: 20%;  text-align: right">วันที่ขาย Voucher<span class="auto-style3">*</span>: </td>
                <td>
                    <asp:TextBox ID="TextBox8" runat="server" Width="30%" TextMode="Date"></asp:TextBox>
                 </td>
              
            </tr>
            <tr>
                <td class="modal-sm" style="width: 20%;  text-align: right">เบอร์โทร<span class="auto-style3">*</span>:</td>
                <td>
                    <asp:TextBox ID="TextBox13" runat="server" Width="60%" OnTextChanged="TextBox13_TextChanged" AutoPostBack="True"></asp:TextBox>
                 </td>
              
            </tr>
            <tr style="background-color:whitesmoke;">
                <td class="auto-style1" style="text-align: right">ชื่อลูกค้า<span class="auto-style3">*</span>:</td>
                <td class="auto-style2">
                    <asp:DropDownList ID="DropDownList8" runat="server" Width="20%" AutoPostBack="True" OnSelectedIndexChanged="DropDownList8_SelectedIndexChanged">
                    </asp:DropDownList>
                    <asp:TextBox ID="TextBox10" runat="server" Width="40%"></asp:TextBox>
                    <asp:TextBox ID="TextBox7" runat="server" Width="20%" PlaceHolder="รหัสสาขา" text="00000"></asp:TextBox>
                            
                 </td>
              
            </tr>
            <tr>
                <td class="modal-sm" style="width: 20%;  text-align: right">ที่อยู่:</td>
                <td>
                    <asp:TextBox ID="TextBox11" runat="server" Width="20%"></asp:TextBox>
                    <asp:TextBox ID="TextBox18" runat="server" Width="40%"></asp:TextBox>
                 </td>
              
            </tr>
            <tr style="background-color:whitesmoke;">
                <td class="modal-sm" style="width: 20%;  text-align: right">รหัสไปรษณีย์</td>
                <td>
                    <asp:TextBox ID="TextBox16" runat="server" Width="30%" AutoPostBack="True" OnTextChanged="TextBox16_TextChanged" TextMode="Number"></asp:TextBox>
                 &nbsp;&nbsp;
                    <asp:Button ID="Button5" runat="server" Text="ค้นหา" Width="20%" OnClick="Button5_Click" />
                 &nbsp;&nbsp;
                    <asp:Button ID="Button6" runat="server" Text="ยกเลิก" Width="20%" OnClick="Button6_Click" />
                 </td>
              
            </tr>
            <tr>
                <td class="modal-sm" style="width: 20%;  text-align: right">จังหวัด/อำเภอ/ตำบล</td>
                <td>
                    <asp:DropDownList ID="DropDownList5" runat="server" Width="25%" AutoPostBack="True" OnSelectedIndexChanged="DropDownList5_SelectedIndexChanged">
                    </asp:DropDownList>
                &nbsp;&nbsp;
                    <asp:DropDownList ID="DropDownList6" runat="server" Width="25%" AutoPostBack="True" OnSelectedIndexChanged="DropDownList6_SelectedIndexChanged">
                    </asp:DropDownList>
                &nbsp;&nbsp;
                    <asp:DropDownList ID="DropDownList7" runat="server" Width="25%">
                    </asp:DropDownList>
                </td>
              
            </tr>
            <tr style="background-color:whitesmoke;">
                <td class="modal-sm" style="width: 20%;  text-align: right">เลขประจำตัวผู้เสียภาษี:</td>
                <td>
                    <asp:TextBox ID="TextBox12" runat="server" Width="60%" AutoPostBack="True" OnTextChanged="TextBox12_TextChanged"></asp:TextBox>
                 </td>
              
            </tr>
            <tr>
                <td class="modal-sm" style="width: 20%;  text-align: right">อีเมล์</td>
                <td>
                    <asp:TextBox ID="TextBox17" runat="server" Width="60%"></asp:TextBox>
                 </td>
              
            </tr>

            <tr style="background-color:whitesmoke;">
                 <td class="modal-sm" style="width: 20%; text-align: right">&nbsp;</td>
                <td>
                                <asp:CheckBox ID="CheckBox4" runat="server" Text="ไม่ออกใบกำกับภาษีในระบบ !!!" OnCheckedChanged="CheckBox4_CheckedChanged" AutoPostBack="True" CssClass="mycheckbox"/>
                 </td>
            
            </tr>

            <tr>
                 <td class="modal-sm" style="width: 20%; text-align: right">&nbsp;</td>
                <td>
                    <asp:CheckBox ID="CheckBox3" Text="ประสงค์ไม่ระบุชื่อในใบกำกับภาษี" runat="server" AutoPostBack="True" OnCheckedChanged="CheckBox3_CheckedChanged" Checked="true" />
                 </td>
            
            </tr>

            <tr style="background-color:whitesmoke;">
                 <td class="modal-sm" style="width: 20%; text-align: right">&nbsp;</td>
                <td>
                    <asp:CheckBox ID="CheckBox5" Text="ต้องการรับ e tax invoice" runat="server" AutoPostBack="True" OnCheckedChanged="CheckBox5_CheckedChanged" />
                 &nbsp;&nbsp;
                    </td>
            
            </tr>

            <tr>
                 <td class="modal-sm" style="width: 20%; text-align: right">&nbsp;</td>
                <td>&nbsp;</td>
            
            </tr>

            <tr style="background-color:whitesmoke;">
                 <td class="modal-sm" style="width: 20%; text-align: right">ประเภทเรทที่พัก:</td>
                <td>&nbsp;<asp:DropDownList ID="DropDownList3" runat="server" Width="60%" DataSourceID="SqlDataSource3" DataTextField="Group_Name" DataValueField="GroupID" AppendDataBoundItems="true" >
                <asp:ListItem>---โปรดเลือก---</asp:ListItem>    
                </asp:DropDownList>
                    <asp:SqlDataSource ID="SqlDataSource3" runat="server" ConnectionString="<%$ ConnectionStrings:TaketimeConnectionString %>" SelectCommand="SELECT Distinct([Group_Name]),GroupID FROM [Taketime].[dbo].[Accommodation_RatePlan_Group] Where [Status] = 'True'">
                    </asp:SqlDataSource>
                 </td>
            
            </tr>

            

           

            <tr>
                 <td class="modal-sm" style="width: 20%; text-align: right">ราคาที่ต้องจ่ายเพิ่มเมื่อใช้ Voucher: </td>
                <td>
                    &nbsp;<asp:TextBox ID="TextBox2" runat="server" Width="30%" TextMode="Number">0</asp:TextBox>
                 &nbsp;บาท</td>
            
            </tr>

            <tr style="background-color:whitesmoke;">
                 <td class="modal-sm" style="width: 20%; text-align: right">&nbsp;</td>
                <td>
                    &nbsp;<asp:Button ID="Button2" runat="server" Text="เพิ่ม" Width="75px" OnClick="Button2_Click" />
                 </td>
            
            </tr>

            <tr>
                <td class="modal-sm" style="width: 20%; text-align: right">&nbsp;</td>
                 <td style="text-align: right">
                     <asp:GridView ID="GridView1" runat="server" AutoGenerateColumns="False" OnRowDeleting="GridView1_RowDeleting">
                         <Columns>
                             <asp:BoundField DataField="Number" HeaderText="ลำดับ" />
                             <asp:BoundField DataField="RatePlan_Group" HeaderText="ประเภทเรทที่พัก" />
                             <asp:BoundField DataField="PriceTo" HeaderText="ราคาที่ต้องจ่ายเพิ่ม" />
                             <asp:CommandField ShowDeleteButton="True" ButtonType="Button" />
                         </Columns>
                     </asp:GridView>
                     </td>
            
            </tr>

            <tr style="background-color:whitesmoke;">
                 <td class="modal-sm" style="width: 20%; text-align: right">วิธีชำระเงิน<span class="auto-style3">*</span>:</td>
                <td>&nbsp;<asp:DropDownList ID="DropDownList2" runat="server" Width="60%" AppendDataBoundItems="true">
                <asp:ListItem>---โปรดเลือก---</asp:ListItem>    
                </asp:DropDownList>
                    <asp:SqlDataSource ID="SqlDataSource2" runat="server" ConnectionString="<%$ ConnectionStrings:TaketimeConnectionString %>" SelectCommand="SELECT * FROM [Account_Paid_How] WHERE ([Status] = 'True')">
                        <SelectParameters>
                            <asp:Parameter DefaultValue="True" Name="Status" Type="Boolean" />
                        </SelectParameters>
                    </asp:SqlDataSource>
                 </td>
            </tr>

            <tr>
                 <td class="modal-sm" style="width: 20%; text-align: right">ประเภทภาษี<span class="auto-style3">*</span>: </td>
                <td>
                    &nbsp;<asp:DropDownList ID="DropDownList4" runat="server" Width="60%" OnSelectedIndexChanged="DropDownList4_SelectedIndexChanged" AppendDataBoundItems="true" AutoPostBack="True">
                    <asp:ListItem>---โปรดเลือก---</asp:ListItem>
                    </asp:DropDownList>
                    <asp:SqlDataSource ID="SqlDataSource4" runat="server" ConnectionString="<%$ ConnectionStrings:TaketimeConnectionString %>" SelectCommand="SELECT * FROM [Account_Vat_Type] WHERE ([Status] = 'True')">
                        <SelectParameters>
                            <asp:Parameter DefaultValue="True" Name="Status" Type="Boolean" />
                        </SelectParameters>
                    </asp:SqlDataSource>
                 </td>
            
            </tr>

            <tr style="background-color:whitesmoke;">
                 <td class="modal-sm" style="width: 20%; text-align: right">หมายเหตุ:</td>
                <td>
                    <asp:TextBox ID="TextBox20" runat="server" Width="80%" Enabled="true" ></asp:TextBox>
                    </td>
            </tr>

            <tr style="background-color:whitesmoke;">
                 <td class="modal-sm" style="width: 20%; text-align: right">ราคาขายต่อ1ใบ<span class="auto-style3">*</span>: </td>
                <td>
                    <asp:TextBox ID="TextBox6" runat="server" Width="30%" Enabled="true" OnTextChanged="TextBox6_TextChanged" AutoPostBack="True"></asp:TextBox>
                    &nbsp;บาท</td>
            </tr>

            <tr>
                 <td class="auto-style8" style="text-align: right">จำนวนใบที่ขาย<span class="auto-style3">*</span>: </td>
                <td class="auto-style9">
                    <asp:TextBox ID="TextBox19" runat="server" Width="30%" Enabled="true" AutoPostBack="True" TextMode="Number" Text="1" OnTextChanged="TextBox19_TextChanged"></asp:TextBox>
                    &nbsp;ใบ</td>
            </tr>

            <tr style="background-color:whitesmoke;">
                 <td class="auto-style6" style="text-align: right">จำนวนรวมก่อนภาษีมุลค่าเพิ่ม: </td>
                <td class="auto-style7">
                    &nbsp;<asp:TextBox ID="TextBox3" runat="server" Width="30%" Enabled="false" OnTextChanged="TextBox3_TextChanged"></asp:TextBox>
                 &nbsp;บาท</td>
            </tr>
            <tr>
                 <td class="modal-sm" style="width: 20%; text-align: right">
                     <asp:Label ID="Label1" runat="server" Text=""></asp:Label>: </td>
                <td>
                    &nbsp;<asp:TextBox ID="TextBox4" runat="server" Width="30%" Enabled="False" Height="16px"></asp:TextBox>
                    &nbsp;บาท</td>
            </tr>

            <tr style="background-color:whitesmoke;">
                 <td class="auto-style4" style="text-align: right">ราคาขาย Voucher สุทธิ: </td>
                <td class="auto-style5">
                    &nbsp;
                    <asp:TextBox ID="TextBox1" runat="server" Width="30%" Enabled="False"></asp:TextBox>
                    &nbsp;บาท</td>
            </tr>

            <tr style="background-color:whitesmoke;">
                 <td class="auto-style4" style="text-align: right">Voucher หมดอายุ: </td>
                <td class="auto-style5">
                    <asp:TextBox ID="TextBox21" runat="server" Width="30%" TextMode="Date"></asp:TextBox>
                    </td>
            </tr>

            <tr style="background-color:whitesmoke;">
                 <td class="auto-style4" style="text-align: right">อัพโหลดสลิป</td>
                <td class="auto-style5">
                    <asp:FileUpload ID="FileUpload1" runat="server"  Width="30%" CssClass="rounded-textbox"/>
                                            <asp:Button ID="Button7" runat="server" Text="Upload Picture" Width="127px" CssClass="rounded-textbox" OnClick="Button7_Click" />
                                            <asp:Image ID="Image1" runat="server" width="90%" />
                                        </td>
            </tr>

            <tr>
                 <td class="modal-sm" style="width: 20%; text-align: right">&nbsp;</td>
                <td>
                    &nbsp;<asp:Button ID="Button3" runat="server" Text="บันทึก" Width="100px" Height="33px" OnClick="Button3_Click" OnClientClick="return mydialog(this)" />
                        <div id="mypopdiv" style="display:none">
    <br /><h2>Voucher Number = <asp:Label ID="LabelVoucher" runat="server" Text=""></asp:Label></h2>
</div>

<script>
    var myok = false
    function mydialog(btn) {

        if (myok)
            return true

        var mydiv = $("#mypopdiv")
        mydiv.dialog({
            modal: true, appendTo: "form",
            title: "Submit", closeText: "",
            dialogClass: "dialogWithDropShadow",
            position: { my: 'left top', at: 'right bottom', of: btn },
            buttons: {
                Ok: function () {
                    mydiv.dialog('close')
                    myok = true
                    btn.click()
                }
            }
        });

        return false
    }
</script>
                 </td>
            </tr>

        </table>
    </p>
    <rsweb:reportviewer Visible="false" ID="ReportViewer2" runat="server" BackColor="" ClientIDMode="AutoID" HighlightBackgroundColor="" InternalBorderColor="204, 204, 204" InternalBorderStyle="Solid" InternalBorderWidth="1px" LinkActiveColor="" LinkActiveHoverColor="" LinkDisabledColor="" PrimaryButtonBackgroundColor="" PrimaryButtonForegroundColor="" PrimaryButtonHoverBackgroundColor="" PrimaryButtonHoverForegroundColor="" SecondaryButtonBackgroundColor="" SecondaryButtonForegroundColor="" SecondaryButtonHoverBackgroundColor="" SecondaryButtonHoverForegroundColor="" SplitterBackColor="" ToolbarDividerColor="" ToolbarForegroundColor="" ToolbarForegroundDisabledColor="" ToolbarHoverBackgroundColor="" ToolbarHoverForegroundColor="" ToolBarItemBorderColor="" ToolBarItemBorderStyle="Solid" ToolBarItemBorderWidth="1px" ToolBarItemHoverBackColor="" ToolBarItemPressedBorderColor="51, 102, 153" ToolBarItemPressedBorderStyle="Solid" ToolBarItemPressedBorderWidth="1px" ToolBarItemPressedHoverBackColor="153, 187, 226" Height="500px" CssClass="auto-style5" style="margin-top: 10px">
        <LocalReport EnableExternalImages="true" ReportPath="Account\Report\Receipt.rdlc">
            
        </LocalReport>
</rsweb:reportviewer>

    <asp:Panel ID="Panel1" runat="server" Visible="False"><center><iframe id="myFrame" runat="server" width="640" height="480" frameborder="0"></iframe></center></asp:Panel>
</asp:Content>
