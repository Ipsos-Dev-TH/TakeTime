<%@ Page Title="" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Register.aspx.cs" Inherits="Take_Time_BangPhra.Affiliate.Register" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
        <link rel="stylesheet" href="../Content/jquery-ui.css">
  <link rel="stylesheet" href="../Content/style.css">
    <link rel="stylesheet" type="text/css" href="../Content/GridView.css">
    <style>
                    th, td {
  padding: 5px;
}
        tr.spaceUnder>td {
  padding-bottom: 1em;
}
        .auto-style1 {
            font-size: x-large;
        }

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
        .auto-style2 {
            width: 25%;
            height: 38px;
        }
        .auto-style3 {
            width: 55%;
            height: 38px;
        }
        .auto-style4 {
            width: 20%;
            height: 38px;
        }
        .auto-style5 {
            color: #FF0000;
        }

        /* Mobile responsive */
        @media (max-width: 768px) {
            table {
                font-size: 14px;
            }
            tr {
                display: block;
                margin-bottom: 4px;
                padding: 4px 0;
            }
            td {
                display: block;
                width: 100% !important;
                text-align: left !important;
                padding: 4px 10px !important;
                box-sizing: border-box;
                height: auto !important;
            }
            td.auto-style2, td.auto-style3, td.auto-style4 {
                width: 100% !important;
                height: auto !important;
            }
            td[style*="width:25%"], td[style*="width:33%"], td[style*="width:55%"], td[style*="width:20%"] {
                width: 100% !important;
            }
            input[type="text"], input[type="password"], input[type="number"], select, textarea {
                width: 100% !important;
                max-width: 100% !important;
                box-sizing: border-box;
                margin: 3px 0;
                padding: 10px 12px;
                font-size: 15px;
            }
            input[type="file"] {
                width: 100% !important;
                margin: 5px 0;
            }
            input[type="submit"], button {
                width: 100% !important;
                margin: 5px 0;
                padding: 12px;
                font-size: 15px;
                box-sizing: border-box;
            }
            .radioBL {
                width: 100% !important;
            }
        }
        </style>
    <p>
        <br />
        <div class="ExampleFont">

        <table style="width: 100%;">
            <tr>
                <td class="auto-style1" colspan="3">
                    <strong>กรุณากรอกข้อมูล</strong></td>
            </tr>
            <tr>
                <td colspan="3" style="align-content:center;align-items:center;align-self:center">
                    <asp:RadioButtonList ID="RadioButtonList1" CssClass="radioBL" runat="server" Width="80%" OnSelectedIndexChanged="RadioButtonList1_SelectedIndexChanged" AutoPostBack="True">
                        <asp:ListItem Selected="True" Value="2">บุคคลธรรมดา</asp:ListItem>
                        <asp:ListItem Value="1">นิติบุคคล</asp:ListItem>
                    </asp:RadioButtonList>
                </td>
            </tr>
            <tr>
                <td colspan="3">
                    <strong>ข้อมูลส่วนตัว</strong></td>
            </tr>
                        <tr>
                <td style=width:25%>
                    ชื่อ-สกุล/ชื่อบริษัท (Thai)<span class="auto-style5">*</span></td>
<td colspan="2">
                    <asp:TextBox ID="TextBox2" CssClass="rounded-textbox" runat="server" Width="60%" ></asp:TextBox>
                    &nbsp;<asp:TextBox ID="TextBox3" runat="server" Width="30%" CssClass="rounded-textbox" Visible="false" AutoPostBack="True" OnTextChanged="TextBox3_TextChanged" TextMode="Number" Text="00000"></asp:TextBox>
                </td>
            </tr>
            <tr>
                <td class="auto-style2">
                    ชื่อ-สกุลผู้สมัคร (Eng)<span class="auto-style5">*</span></td>
                <td class="auto-style3">
                    <asp:TextBox ID="TextBox10" CssClass="rounded-textbox" runat="server" Width="80%" ></asp:TextBox>
                </td>
                <td class="auto-style4"></td>
            </tr>
            <tr>
                <td style=width:25%>
                    เลขประจำตัวผู้เสียภาษี<span class="auto-style5">*</span></td>
                <td style=width:55%>
                    <asp:TextBox ID="TextBox1" runat="server" CssClass="rounded-textbox" Width="80%" AutoPostBack="True" OnTextChanged="TextBox1_TextChanged" TextMode="Number" ></asp:TextBox>
                </td>
                <td style=width:20%>&nbsp;</td>
            </tr>
            <tr>
                <td style=width:25%>
                    เบอร์โทรศัพท์<span class="auto-style5">*</span></td>
                <td style=width:55%><asp:TextBox ID="TextBox7" CssClass="rounded-textbox" runat="server" Width="80%" AutoPostBack="True" OnTextChanged="TextBox7_TextChanged"></asp:TextBox>
                </td>
                <td style=width:20%>&nbsp;</td>
                       
            </tr>
           
            <tr>
                <td style=width:25%>ที่อยู่ บ้านเลขที่<span class="auto-style5">*</span></td>
<td style=width:55%>
                    <asp:TextBox ID="TextBox4" runat="server" CssClass="rounded-textbox" Width="80%" ></asp:TextBox>
                </td>
<td style=width:20%>&nbsp;</td>
            </tr>
            <tr>
                <td style=width:25%>
                    อาคาร หมู่ที่ ซอย</td>
<td style=width:55%><asp:TextBox ID="TextBox5" runat="server" CssClass="rounded-textbox" Width="80%" ></asp:TextBox>
                </td>
<td style=width:20%>&nbsp;</td>
            </tr>
            <tr>
                <td style=width:33%>
                    รหัสไปรษณีย์<span class="auto-style5">*</span></td>
<td colspan="2">
                    <asp:TextBox ID="TextBox6" runat="server" CssClass="rounded-textbox" Width="60%" ></asp:TextBox>
                    &nbsp;<asp:Button ID="Button5" runat="server" CssClass="rounded-textbox" Text="ค้นหา" Width="16%" OnClick="Button5_Click" />
                 &nbsp;<asp:Button ID="Button6" runat="server" CssClass="rounded-textbox" Text="ยกเลิก" Width="16%" OnClick="Button6_Click"  />
                 </td>
            </tr>
            <tr>
                <td style=width:33%>
                    จังหวัด<span class="auto-style5">*</span></td>
<td style=width:33%>
                    <asp:DropDownList ID="DropDownList2" CssClass="rounded-textbox" runat="server" Width="80%" AutoPostBack="True" OnSelectedIndexChanged="DropDownList2_SelectedIndexChanged">
                    </asp:DropDownList>
                 &nbsp;</td>
<td style=width:33%>&nbsp;</td>
            </tr>
            <tr>
                <td style=width:33%>
                    อำเภอ/เขต<span class="auto-style5">*</span></td>
<td style=width:33%>
                 <asp:DropDownList ID="DropDownList3" CssClass="rounded-textbox" runat="server" Width="80%" AutoPostBack="True" OnSelectedIndexChanged="DropDownList3_SelectedIndexChanged">
                    </asp:DropDownList>
                 </td>
<td style=width:33%>&nbsp;</td>
            </tr>
            <tr>
                <td style=width:33%>
                    ตำบล/แขวง<span class="auto-style5">*</span></td>
<td style=width:33%>
                 <asp:DropDownList ID="DropDownList4" CssClass="rounded-textbox" runat="server" Width="80%">
                    </asp:DropDownList>
                 </td>
<td style=width:33%>&nbsp;</td>
            </tr>
            <tr>
                <td colspan="3">
                    <strong>แนบเอกสารเพิ่มเติม</strong></td>
            </tr>
            <tr>
                <td style=width:33%>บัตรประชาชน<span class="auto-style5">*</span></td>
<td><asp:FileUpload ID="FileUpload1" CssClass="rounded-textbox" runat="server" Width="80%"  />
                    </td>
<td>
                    <asp:Button ID="Button7" runat="server" Text="อัพโหลด" CssClass="rounded-textbox" Width="80%" OnClick="Button7_Click" />
                    </td>
            </tr>
            <tr>
                <td style=width:33%>
                    หน้า Book Bank<span class="auto-style5">*</span></td>
<td>
                    <asp:FileUpload ID="FileUpload2" CssClass="rounded-textbox" runat="server" Width="80%" />
                    </td>
<td>
                    <asp:Button ID="Button8" runat="server" CssClass="rounded-textbox"  Text="อัพโหลด" Width="80%" OnClick="Button8_Click" />
                    </td>
            </tr>
            <tr>
                <td style=width:33%>ธนาคาร<span class="auto-style5">*</span></td>
<td style=width:33%>
                    <asp:DropDownList ID="DropDownList6" CssClass="rounded-textbox" runat="server" Width="80%" AppendDataBoundItems="true" >
                        <asp:ListItem Value="0">--- กรุณาเลือกธนาคารของท่าน ---</asp:ListItem>
                        <asp:ListItem Value="BBL">Bangkok Bank (BBL) - ธนาคารกรุงเทพ</asp:ListItem>
                        <asp:ListItem Value="BAY">Bank of Ayudhya (BAY) - ธนาคารกรุงศรีอยุธยา</asp:ListItem>
                        <asp:ListItem Value="CCB">China Construction Bank (CCB) - ธนาคารเพื่อการก่อสร้างแห่งประเทศจีน (ไทย)</asp:ListItem>
                        <asp:ListItem Value="CIMB">CIMB Thai Bank (CIMBT) - ธนาคารซีไอเอ็มบีไทย</asp:ListItem>
                        <asp:ListItem Value="EXIM">Export-Import Bank of Thailand (EXIM) - ธนาคารเพื่อการส่งออกและนำเข้าแห่งประเทศไทย</asp:ListItem>
                        <asp:ListItem Value="GHB">Government Housing Bank (GHB) - ธนาคารอาคารสงเคราะห์</asp:ListItem>
                        <asp:ListItem Value="GSB">Government Savings Bank (GSB) - ธนาคารออมสิน</asp:ListItem>
                        <asp:ListItem Value="ICBC">Industrial and Commercial Bank of China (ICBC) - ธนาคารไอซีบีซี (ไทย)</asp:ListItem>
                        <asp:ListItem Value="Kbank">Kasikornbank (KBank) - ธนาคารกสิกรไทย</asp:ListItem>
                        <asp:ListItem Value="KTB">Krungthai Bank (KTB) - ธนาคารกรุงไทย</asp:ListItem>
                        <asp:ListItem Value="LH Bank">Land and Houses Bank (LH Bank) - ธนาคารแลนด์ แอนด์ เฮ้าส์</asp:ListItem>
                        <asp:ListItem Value="Mega ICBC">Mega International Commercial Bank (Mega ICBC) - ธนาคารเมกะ สากลพาณิชย์</asp:ListItem>
                        <asp:ListItem Value="SCB">Siam Commercial Bank (SCB) - ธนาคารไทยพาณิชย์</asp:ListItem>
                        <asp:ListItem Value="SCBT">Standard Chartered Bank (SCBT) - ธนาคารสแตนดาร์ดชาร์เตอร์ด (ไทย)</asp:ListItem>
                        <asp:ListItem Value="TCRB">Thai Credit Retail Bank (TCRB) - ธนาคารไทยเครดิตเพื่อรายย่อย</asp:ListItem>
                        <asp:ListItem Value="TTB">TMBThanachart Bank (TTB) - ธนาคารทหารไทยธนชาต</asp:ListItem>
                        <asp:ListItem Value="UOB">United Overseas Bank (UOB) - ธนาคารยูโอบี</asp:ListItem>
                    </asp:DropDownList>
                </td>
<td style=width:33%>&nbsp;</td>
            </tr>
            <tr>
                <td style=width:33%>เลขบัญชีธนาคาร<span class="auto-style5">*</span></td>
<td style=width:33%>
                    <asp:TextBox ID="TextBox12" CssClass="rounded-textbox" runat="server" Width="80%" ></asp:TextBox>
                </td>
<td style=width:33%>&nbsp;</td>
            </tr>
            <tr>
                <td colspan="3">
                    <strong>ตั้งรหัสผ่านของคุณ</strong></td>
            </tr>
            <tr>
                <td style=width:33%>รหัสผ่านของคุณ<span class="auto-style5">*</span></td>
<td style=width:33%>
                    <asp:TextBox ID="TextBox8" CssClass="rounded-textbox" runat="server" Width="80%"  TextMode="Password" OnTextChanged="TextBox8_TextChanged"></asp:TextBox>
                </td>
<td style=width:33%>&nbsp;</td>
            </tr>
            <tr>
                <td style=width:33%>
                    ระบุรหัสผ่านซ้ำอีกครั้ง<span class="auto-style5">*</span></td>
<td style=width:33%>
                    <asp:TextBox ID="TextBox9" CssClass="rounded-textbox" runat="server" Width="80%"   TextMode="Password" OnTextChanged="TextBox9_TextChanged"></asp:TextBox>
                </td>
<td style=width:33%>&nbsp;</td>
            </tr>
            <tr>
                <td style=width:33%>
                    &nbsp;</td>
<td style=width:33%>&nbsp;</td>
<td style=width:33%>&nbsp;</td>
            </tr>
            <tr>
                <td colspan="3">
                    <asp:TextBox ID="TextBox13" CssClass="rounded-textbox" runat="server" Width="100%" TextMode="MultiLine" Height="148px" Enabled="false" ></asp:TextBox>
                </td>
            </tr>
            <tr>
                <td colspan="3">
                    <asp:CheckBox ID="CheckBox1" CssClass="mycheckbox" runat="server" Text="ข้าพเจ้าได้อ่านและยอมรับข้อกำหนดและเงื่อนไขข้างต้น" AutoPostBack="True" OnCheckedChanged="CheckBox1_CheckedChanged" />
                </td>
            </tr>
            <tr>
                <td style=width:33%>
                    &nbsp;</td>
<td style=width:33%>&nbsp;</td>
<td style=width:33%>&nbsp;</td>
            </tr>
            <tr>
                <td class="text-center" colspan="3">
                    <asp:Button ID="Button2" runat="server" Text="สมัคร" Width="50%" Height="50px" OnClick="Button2_Click" CssClass="rounded-textbox" Enabled="False" />
                </td>
            </tr>
        </table>
            </div>
    </p>
</asp:Content>
