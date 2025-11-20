<%@ Page Title="" Async="true" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Voucher_Confirmed.aspx.cs" Inherits="Take_Time_BangPhra.Voucher.Voucher_Confirmed" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <p>
        <br />
      <style type="text/css">
          .auto-style1 {
              height: 47px;
          }
      </style><div class="ExampleFont">
        <table style="width: 100%;">
                    <tr>
                        <td class="text-center" colspan="2">
                    <strong><span style=" color: #009933">
                    <asp:Label ID="Label10" runat="server" Text=""></asp:Label>
                    </span>
                    </strong>
                        </td>
                    </tr>
                    
                    <tr>
                        <td class="text-center" colspan="2">
                            <strong>
                    <span style="font-size: small">*** กรุณาบันทึกภาพหน้าจอนี้เพื่อใช้ยืนยันการซื้อ Voucher (Please capture this screen for confirm)</span></strong></td>
                    </tr>
                    <tr>
                        <td>&nbsp;</td>
                        <td>&nbsp;</td>
                    </tr>
                </table>
        
        <table style="width: 100%;">
    <tr>
        <td style="width:75%"><strong>Voucher Number:
            <br />
                    </strong>
                    <asp:Label ID="Label1" runat="server" style="font-size: small"></asp:Label>
                    </td>
        <td rowspan="9">
                            <asp:Image ID="Image1" runat="server" Width="98%" /></td>
    </tr>
    <tr>
        <td><strong>Name: </strong>
                    <asp:Label ID="Label2" runat="server" style="font-size: small"></asp:Label>
            <strong>&nbsp;- </strong>
            <asp:Label ID="Label3" runat="server" style="font-size: small"></asp:Label>
                    </td>
    </tr>
    <tr>
        <td><strong>Tel:</strong>
                    <asp:Label ID="Label4" runat="server" style="font-size: small"></asp:Label>
                        </td>
    </tr>
    <tr>
        <td ><strong>ราคาขายต่อ 1 ใบ: </strong>
                    <asp:Label ID="Label5" runat="server" style="font-size: small"></asp:Label>
                        &nbsp;บาท</td>
    </tr>
    <tr>
        <td><strong>จำนวน:</strong>
                    <asp:Label ID="Label16" runat="server" style="font-size: small"></asp:Label>
                        &nbsp;ใบ</td>
    </tr>
    <tr>
        <td><strong>ราคารวม:</strong>
                    <asp:Label ID="Label15" runat="server" style="font-size: small"></asp:Label>
                        &nbsp;บาท</td>
    </tr>
    <tr>
        <td>
                    <strong>
                    Remark:
                    </strong>
                    <asp:Label ID="Label14" runat="server" style="font-size: small"></asp:Label>
                        </td>
    </tr>
    <tr>
        <td>
                    &nbsp;</td>
    </tr>
    <tr>
        <td>&nbsp;</td>
    </tr>
    <tr>
        <td colspan="2">&nbsp;</td>
    </tr>
    <tr>
        <td colspan="2">&nbsp;</td>
    </tr>
    <tr>
        <td colspan="2">&nbsp;</td>
    </tr>
    <tr>
        <td colspan="2">
            &nbsp;</td>
    </tr>
    <tr>
        <td colspan="2">
                    &nbsp;</td>
    </tr>
    <tr>
        <td>&nbsp;</td>
        <td>&nbsp;</td>
    </tr>
</table>

    </p></div>
</asp:Content>


