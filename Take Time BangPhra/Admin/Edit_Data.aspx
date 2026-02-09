<%@ Page Title="" Language="C#" MaintainScrollPositionOnPostback="true" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Edit_Data.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Edit_Data" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .edit-data-page {
            padding: 20px;
            max-width: 1200px;
            margin: 0 auto;
        }
        .edit-data-page .btn-new {
            background: linear-gradient(135deg, #5D4037 0%, #8D6E63 100%);
            color: white;
            border: none;
            padding: 12px 30px;
            border-radius: 8px;
            font-weight: 500;
            cursor: pointer;
            margin-bottom: 20px;
        }
        @media (max-width: 768px) {
            .edit-data-page {
                padding: 10px;
            }
            .edit-data-page .btn-new {
                width: 100%;
                min-height: 44px;
            }
        }
    </style>
    <div class="edit-data-page">
        <center>
        <asp:Label ID="Label1" runat="server" Text=""></asp:Label>
        <br />
        <asp:Button ID="Button1" runat="server" Height="41px" Text="New" Width="153px" OnClick="Button1_Click" CssClass="btn-new" />
        <br />
        <br />
        <div class="mobile-table-wrapper">
            <asp:GridView ID="GridView2" runat="server" Visible="false" CssClass="gridview-table">
            </asp:GridView>
        </div>
        <br />
        <div class="mobile-table-wrapper">
            <asp:GridView ID="GridView1" runat="server" CssClass="gridview-table" OnRowCancelingEdit="GridView1_RowCancelingEdit" OnRowDeleting="GridView1_RowDeleting" OnRowEditing="GridView1_RowEditing" OnRowUpdating="GridView1_RowUpdating">
                <Columns>
                    <asp:CommandField ButtonType="Button" ShowEditButton="True" />
                    <asp:CommandField ShowDeleteButton="True" />
                </Columns>
            </asp:GridView>
        </div>
        </center>
    </div>
</asp:Content>

