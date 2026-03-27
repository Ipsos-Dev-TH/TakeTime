<%@ Page Title="" Language="C#" MaintainScrollPositionOnPostback="true" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Edit_Data.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Edit_Data" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .edit-data-page {
            padding: 20px;
            max-width: 1400px;
            margin: 0 auto;
        }

        .edit-data-header {
            display: flex;
            align-items: center;
            gap: 15px;
            margin-bottom: 20px;
            flex-wrap: wrap;
        }

        .edit-data-header .btn-back {
            background: #EFEBE9;
            color: #5D4037;
            border: none;
            padding: 10px 16px;
            border-radius: 8px;
            font-size: 14px;
            cursor: pointer;
            text-decoration: none;
            transition: background 0.2s;
            font-family: 'Prompt', sans-serif;
        }

        .edit-data-header .btn-back:hover {
            background: #D7CCC8;
            text-decoration: none;
            color: #3E2723;
        }

        .edit-data-title {
            flex: 1;
            font-size: 18px;
            font-weight: 600;
            color: #5D4037;
        }

        .edit-data-page .btn-new {
            background: linear-gradient(135deg, #4CAF50 0%, #388E3C 100%);
            color: white;
            border: none;
            padding: 12px 30px;
            border-radius: 8px;
            font-weight: 500;
            cursor: pointer;
            font-size: 14px;
            font-family: 'Prompt', sans-serif;
            transition: transform 0.2s, box-shadow 0.2s;
        }

        .edit-data-page .btn-new:hover {
            transform: translateY(-1px);
            box-shadow: 0 4px 12px rgba(76, 175, 80, 0.3);
        }

        /* GridView Styling */
        .mobile-table-wrapper {
            overflow-x: auto;
            -webkit-overflow-scrolling: touch;
            border-radius: 10px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.08);
        }

        .gridview-table {
            width: 100%;
            border-collapse: collapse;
            background: white;
            font-size: 13px;
        }

        .gridview-table th {
            background: linear-gradient(135deg, #5D4037, #8D6E63);
            color: white;
            padding: 12px 10px;
            text-align: left;
            font-weight: 500;
            white-space: nowrap;
            position: sticky;
            top: 0;
        }

        .gridview-table td {
            padding: 10px;
            border-bottom: 1px solid #EFEBE9;
            vertical-align: middle;
        }

        .gridview-table tr:hover td {
            background: #FFF8E1;
        }

        .gridview-table tr:nth-child(even) td {
            background: #FAFAFA;
        }

        .gridview-table tr:nth-child(even):hover td {
            background: #FFF8E1;
        }

        .gridview-table input[type="text"] {
            width: 100%;
            min-width: 80px;
            padding: 6px 8px;
            border: 1px solid #D7CCC8;
            border-radius: 4px;
            font-size: 13px;
            font-family: 'Prompt', sans-serif;
        }

        .gridview-table input[type="text"]:focus {
            border-color: #8D6E63;
            outline: none;
            box-shadow: 0 0 0 2px rgba(141, 110, 99, 0.2);
        }

        .gridview-table input[type="submit"] {
            padding: 6px 14px;
            border: none;
            border-radius: 5px;
            cursor: pointer;
            font-size: 12px;
            font-family: 'Prompt', sans-serif;
            margin: 2px;
            transition: background 0.2s;
        }

        /* Row count info */
        .row-count-info {
            text-align: right;
            font-size: 13px;
            color: #8D6E63;
            margin-bottom: 10px;
        }

        @media (max-width: 768px) {
            .edit-data-page {
                padding: 10px;
            }
            .edit-data-page .btn-new {
                width: 100%;
                min-height: 44px;
            }
            .edit-data-header {
                flex-direction: column;
                align-items: stretch;
            }
        }
    </style>
    <div class="edit-data-page">
        <!-- Header with back button -->
        <div class="edit-data-header">
            <a href="DatabaseManagement.aspx" class="btn-back">
                <i class="fas fa-arrow-left"></i> กลับ
            </a>
            <div class="edit-data-title">
                <i class="fas fa-table"></i>
                <asp:Label ID="Label1" runat="server" Text=""></asp:Label>
            </div>
        </div>

        <center>
        <asp:Button ID="Button1" runat="server" Text="+ เพิ่มข้อมูลใหม่" OnClick="Button1_Click" CssClass="btn-new" />
        <br /><br />
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
