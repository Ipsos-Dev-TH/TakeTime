<%@ Page Title="" Language="C#" MaintainScrollPositionOnPostback="true" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Edit_Home.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Edit_Home" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <br />
    <br />
    <br />
    <p>
        
        <table style="width:100%;padding:10px" border="1">
            <tr>
                <td>รายชื่อประเภทที่พัก:</td>
                <td>
                    <asp:Button ID="Button1" runat="server" OnClick="Button1_Click" Text="EDIT" Height="70px" Width="250px" />
                </td>
            </tr>
            <tr>
                <td>&nbsp;</td>
                <td>&nbsp;</td>
            </tr>
            <tr>
                <td>ราคาวันหยุด</td>
                <td>
                    <asp:Button ID="Button2" runat="server" OnClick="Button2_Click" Text="EDIT" Height="70px" Width="250px" />
                </td>
            </tr>
             <tr>
                <td>&nbsp;</td>
                <td>&nbsp;</td>
            </tr>
            <tr>
                <td>ลูกค้า</td>
                <td>
                    <asp:Button ID="Button3" runat="server" OnClick="Button3_Click" Text="EDIT" Height="70px" Width="250px" />
                </td>
            </tr>
             <tr>
                <td>&nbsp;</td>
                <td>&nbsp;</td>
            </tr>
            <tr>
                <td>ของเช่า</td>
                <td>
                    <asp:Button ID="Button4" runat="server" OnClick="Button4_Click" Text="EDIT" Height="70px" Width="250px" />
                </td>
            </tr>
            <tr>
                <td>&nbsp;</td>
                <td>&nbsp;</td>
            </tr>
            <tr>
                <td>รหัสผู้ดูแล</td>
                <td>
                    <asp:Button ID="Button5" runat="server" OnClick="Button5_Click" Text="EDIT" Height="70px" Width="250px" />
                </td>
            </tr>
             <tr>
                <td style="height: 21px"></td>
                <td style="height: 21px"></td>
            </tr>
            <tr>
                <td>ข้อมูลบริษัท</td>
                <td>
                    <asp:Button ID="Button6" runat="server"  Text="EDIT" Height="70px" Width="250px" OnClick="Button6_Click" />
                </td>
            </tr>
             <tr>
                <td style="height: 21px"></td>
                <td style="height: 21px"></td>
            </tr>
            <tr>
                <td>Account_Paid_How</td>
                <td>
                    <asp:Button ID="Button7" runat="server"  Text="EDIT" Height="70px" Width="250px" OnClick="Button7_Click"  />
                </td>
            </tr>
             <tr>
                <td style="height: 21px"></td>
                <td style="height: 21px"></td>
            </tr>
            <tr>
                <td>Account_Paid_Type</td>
                <td>
                    <asp:Button ID="Button8" runat="server"  Text="EDIT" Height="70px" Width="250px" OnClick="Button8_Click"  />
                </td>
            </tr>
             <tr>
                <td style="height: 21px"></td>
                <td style="height: 21px"></td>
            </tr>
            <tr>
                <td>Account_Vat_Type</td>
                <td>
                    <asp:Button ID="Button9" runat="server"  Text="EDIT" Height="70px" Width="250px" OnClick="Button9_Click"  />
                </td>
            </tr>
             <tr>
                <td style="height: 21px"></td>
                <td style="height: 21px"></td>
            </tr>
            <tr>
                <td>Vendor</td>
                <td>
                    <asp:Button ID="Button10" runat="server"  Text="EDIT" Height="70px" Width="250px" OnClick="Button10_Click"  />
                </td>
            </tr>
             <tr>
                <td style="height: 21px"></td>
                <td style="height: 21px"></td>
            </tr>
            <tr>
                <td>Accommodation_DayType</td>
                <td>
                    <asp:Button ID="Button18" runat="server"  Text="EDIT" Height="70px" Width="250px" OnClick="Button18_Click"   />
                </td>
            </tr>
            <tr>
                <td>&nbsp;</td>
                <td>
                    &nbsp;</td>
            </tr>
            <tr>
                <td>Accommodation_RatePlan</td>
                <td>
                    <asp:Button ID="Button11" runat="server"  Text="EDIT" Height="70px" Width="250px" OnClick="Button11_Click"  />
                </td>
            </tr>
             <tr>
                <td style="height: 21px"></td>
                <td style="height: 21px"></td>
            </tr>
            <tr>
                <td>Accommodation_Holiday</td>
                <td>
                    <asp:Button ID="Button12" runat="server"  Text="EDIT" Height="70px" Width="250px" OnClick="Button12_Click"  />
                </td>
            </tr>
            <tr>
                <td>&nbsp;</td>
                <td>
                    &nbsp;</td>
            </tr>
            <tr>
                <td>Affiliate_Member</td>
                <td>
                    <asp:Button ID="Button16" runat="server"  Text="EDIT" Height="70px" Width="250px" OnClick="Button16_Click"    />
                </td>
            </tr>
            <tr>
                <td>&nbsp;</td>
                <td>
                    &nbsp;</td>
            </tr>
            <tr>
                <td>Affiliate_Reservation</td>
                <td>
                    <asp:Button ID="Button17" runat="server"  Text="EDIT" Height="70px" Width="250px" OnClick="Button17_Click"    />
                </td>
            </tr>
            <tr>
                <td>&nbsp;</td>
                <td>
                    &nbsp;</td>
            </tr>
            <tr>
                <td>Affiliate_Discount</td>
                <td>
                    <asp:Button ID="Button13" runat="server"  Text="EDIT" Height="70px" Width="250px" OnClick="Button13_Click"  />
                </td>
            </tr>
            <tr>
                <td>&nbsp;</td>
                <td>
                    &nbsp;</td>
            </tr>
            <tr>
                <td>Affiliate_Discount_RatePlan</td>
                <td>
                    <asp:Button ID="Button14" runat="server"  Text="EDIT" Height="70px" Width="250px" OnClick="Button14_Click"  />
                </td>
            </tr>
            <tr>
                <td>&nbsp;</td>
                <td>
                    &nbsp;</td>
            </tr>
            <tr>
                <td>Product</td>
                <td>
                    <asp:Button ID="Button15" runat="server"  Text="EDIT" Height="70px" Width="250px" OnClick="Button15_Click"   />
                </td>
            </tr>
            <tr>
                <td>&nbsp;</td>
                <td>
                    &nbsp;</td>
            </tr>
            <tr>
                <td>Voucher</td>
                <td>
                    <asp:Button ID="Button19" runat="server"  Text="EDIT" Height="70px" Width="250px" OnClick="Button19_Click"    />
                </td>
            </tr>
            <tr>
                <td>&nbsp;</td>
                <td>
                    &nbsp;</td>
            </tr>
            <tr>
                <td>Voucher_RatePlan_Group</td>
                <td>
                    <asp:Button ID="Button20" runat="server"  Text="EDIT" Height="70px" Width="250px" OnClick="Button20_Click"    />
                </td>
            </tr>
            <tr>
                <td>&nbsp;</td>
                <td>
                    &nbsp;</td>
            </tr>
            <tr>
                <td>MapDataWithSTAAH</td>
                <td>
                    <asp:Button ID="Button21" runat="server"  Text="EDIT" Height="70px" Width="250px" OnClick="Button21_Click"     />
                </td>
            </tr>
        </table>
    </p>
</asp:Content>
