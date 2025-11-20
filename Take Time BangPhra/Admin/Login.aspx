<%@ Page Title="" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Login.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Login" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <p class="text-center">
        <br />
        <asp:Label ID="Label9" runat="server" Text="User"></asp:Label>
        :
        <asp:TextBox ID="TextBox1" runat="server"></asp:TextBox>
    </p>
    <p class="text-center">
        <asp:Label ID="Label10" runat="server" Text="Password"></asp:Label>
        :<asp:TextBox ID="TextBox2" runat="server" TextMode="Password"></asp:TextBox>
</p>
    <p class="text-center">
        <asp:Button ID="Button1" runat="server" Text="เข้าสู่ระบบ" OnClick="Button1_Click" />
    &nbsp;<asp:Button ID="Button2" runat="server" Text="เปลี่ยนรหัสผ่าน" Visible="false" OnClick="Button2_Click" />
    </p>
</asp:Content>

