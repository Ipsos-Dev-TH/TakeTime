<%@ Page Title="" Language="C#" MaintainScrollPositionOnPostback="true" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Vendor.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Vendor" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        tr.spaceUnder>td {
  padding-bottom: 1em;
}
        .auto-style1 {
            width: 90%;
            height: 39px;
        }
        .auto-style2 {
            height: 39px;
        }
    </style>
    <p>
        <br />
        <table style="width:100%;">
            <tr class="spaceUnder">
                <td class="auto-style1">
                    <br />
                    <br />
                </td>
                <td class="auto-style2">&nbsp;</td>
            </tr>
            <tr class="spaceUnder">
                <td class="auto-style1">
                    <asp:TextBox ID="TextBox1" runat="server" Width="40%" Placeholder="เลขประจำตัวผู้เสียภาษี 13 หลัก (ไม่บังคับ)" AutoPostBack="True" OnTextChanged="TextBox1_TextChanged" TextMode="Number"></asp:TextBox>
                &nbsp;<asp:DropDownList ID="DropDownList5" runat="server" Width="40%">
                    </asp:DropDownList>
                </td>
                <td class="auto-style2" style="color: #4caf50; font-size: 0.9em;">✓ เว้นว่างได้ถ้าไม่มีเลขผู้เสียภาษี</td>
            </tr>
            <tr class="spaceUnder">
                <td>
                    <asp:DropDownList ID="DropDownList1" runat="server" Width="30%">
                    </asp:DropDownList>
                    <asp:TextBox ID="TextBox2" runat="server" Width="50%" Placeholder="ชื่อผู้เสียภาษี / ชื่อบริษัท (บังคับ) *" required></asp:TextBox>
                </td>
                <td>
                    <span style="color: #d32f2f; font-weight: bold; font-size: 0.9em;">* บังคับ - ใช้เป็น unique key หลัก</span></td>
            </tr>
            <tr class="spaceUnder">
                <td>
                    <asp:TextBox ID="TextBox3" runat="server" Width="40%" Placeholder="เลขสาขา (00000 = สำนักงานใหญ่) *" AutoPostBack="True" OnTextChanged="TextBox3_TextChanged" TextMode="Number" required></asp:TextBox>
                    &nbsp;<asp:TextBox ID="TextBox7" runat="server" Width="40%" Placeholder="เบอร์โทรศัพท์ (ไม่บังคับ)" AutoPostBack="True" OnTextChanged="TextBox7_TextChanged"></asp:TextBox>
                </td>
                <td><span style="color: #4caf50; font-size: 0.9em;">✓ เบอร์โทรเว้นว่างได้ (เลขสาขาบังคับ)</span></td>
            </tr>
            <tr class="spaceUnder">
                <td>
                    <asp:TextBox ID="TextBox4" runat="server" Width="30%" Placeholder="บ้านเลขที่"></asp:TextBox>
                &nbsp;<asp:TextBox ID="TextBox5" runat="server" Width="50%" Placeholder="อาคาร หมู่ที่ ซอย"></asp:TextBox>
                </td>
                <td>&nbsp;</td>
            </tr>
            <tr class="spaceUnder">
                <td>
                    <asp:TextBox ID="TextBox6" runat="server" Width="30%" Placeholder="รหัสไปรษณีย์"></asp:TextBox>
                &nbsp;<asp:Button ID="Button5" runat="server" Text="ค้นหา" Width="20%" OnClick="Button5_Click" />
                 &nbsp;<asp:Button ID="Button6" runat="server" Text="ยกเลิก" Width="20%" OnClick="Button6_Click"  />
                </td>
                <td>&nbsp;</td>
            </tr>
            <tr class="spaceUnder">
                <td>
                    <asp:DropDownList ID="DropDownList2" runat="server" Width="30%" AutoPostBack="True" OnSelectedIndexChanged="DropDownList2_SelectedIndexChanged">
                    </asp:DropDownList>
                    &nbsp;<asp:DropDownList ID="DropDownList3" runat="server" Width="30%" AutoPostBack="True" OnSelectedIndexChanged="DropDownList3_SelectedIndexChanged">
                    </asp:DropDownList>
                    &nbsp;<asp:DropDownList ID="DropDownList4" runat="server" Width="30%">
                    </asp:DropDownList>
                    </td>
                <td>&nbsp;</td>
            </tr>
            <tr class="spaceUnder">
                <td>
                    <asp:Button ID="Button2" runat="server" Text="บันทึก / แก้ไข" Width="50%" Height="50px" OnClick="Button2_Click" />
                </td>
                <td>&nbsp;</td>
            </tr>
        </table>
    </p>
</asp:Content>
