<%@ Page Title="" Language="C#" MaintainScrollPositionOnPostback="true" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Edit_Data.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Edit_Data" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
     <p>
        <br /><center>
        <asp:Label ID="Label1" runat="server" Text=""></asp:Label>
        <br />
        <asp:Button ID="Button1" runat="server" Height="41px" Text="New" Width="153px" OnClick="Button1_Click" />
         <br />
         <br />
         <asp:GridView ID="GridView2" runat="server" Visible="false">
         </asp:GridView>
        <br />
        <br />
        <asp:GridView ID="GridView1" runat="server" OnRowCancelingEdit="GridView1_RowCancelingEdit" OnRowDeleting="GridView1_RowDeleting" OnRowEditing="GridView1_RowEditing" OnRowUpdating="GridView1_RowUpdating">
            <Columns>
                <asp:CommandField ButtonType="Button" ShowEditButton="True" />
                <asp:CommandField ShowDeleteButton="True" />
            </Columns>
        </asp:GridView>
    </center>
    </p>
</asp:Content>

