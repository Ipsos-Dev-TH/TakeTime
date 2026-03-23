<%@ Page Title="" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="PostponeList.aspx.cs" Inherits="Take_Time_BangPhra.PostponeList" validateRequest="false" enableEventValidation="false"  %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link rel="stylesheet" href="/Content/jquery-ui.css">
    <link rel="stylesheet" href="/Content/style.css">
    <link rel="stylesheet" type="text/css" href="/Content/GridView2.css">
    <style type="text/css">
        .wrap { white-space: normal; width: 100px; }

        .header-center { text-align: center; }
        .header-right { text-align: right; }

        th, td { padding: 5px; }

        .auto-style1 { width: 25%; height: 35px; }
        .auto-style2 { width: 75%; height: 35px; }

        .page-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 15px;
            flex-wrap: wrap;
            gap: 10px;
        }
        .page-header h2 { margin: 0; }

        .stat-badge {
            display: inline-block;
            background: #f0ad4e;
            color: #fff;
            padding: 4px 12px;
            border-radius: 12px;
            font-size: 14px;
            font-weight: bold;
        }

        .reschedule-count {
            display: inline-block;
            background: #5bc0de;
            color: #fff;
            padding: 2px 8px;
            border-radius: 10px;
            font-size: 12px;
        }

        .btn-edit {
            background-color: #337ab7;
            color: white;
            border: none;
            padding: 6px 14px;
            border-radius: 4px;
            cursor: pointer;
            font-size: 13px;
        }
        .btn-edit:hover { background-color: #286090; }

        .btn-cancel {
            background-color: #d9534f;
            color: white;
            border: none;
            padding: 6px 14px;
            border-radius: 4px;
            cursor: pointer;
            font-size: 13px;
        }
        .btn-cancel:hover { background-color: #c9302c; }

        .btn-refresh {
            background-color: #5cb85c;
            color: white;
            border: none;
            padding: 6px 14px;
            border-radius: 4px;
            cursor: pointer;
            font-size: 13px;
        }
        .btn-refresh:hover { background-color: #449d44; }

        .reason-text {
            color: #888;
            font-size: 12px;
            font-style: italic;
        }

        /* Mobile responsive */
        @media (max-width: 768px) {
            .mydatagrid {
                display: block;
                overflow-x: auto;
                -webkit-overflow-scrolling: touch;
                font-size: 12px;
            }
            .mydatagrid th, .mydatagrid td {
                padding: 6px 4px !important;
                white-space: nowrap;
            }
            .mydatagrid input[type="submit"], .mydatagrid input[type="button"] {
                padding: 8px 12px;
                font-size: 13px;
            }
            .page-header {
                flex-direction: column;
                align-items: flex-start;
            }
        }
    </style>

    <div class="page-header">
        <div>
            <strong><span style="font-size: large">รายการผู้เลื่อนเข้าพัก</span></strong>
            <span class="stat-badge">
                <asp:Label ID="lblPostponeCount" runat="server" Text="0" /> รายการ
            </span>
        </div>
        <div>
            <asp:Button ID="btnRefresh" runat="server" Text="รีเฟรช" CssClass="btn-refresh" OnClick="btnRefresh_Click" />
        </div>
    </div>

    <center>
    <asp:GridView ID="GridView1" runat="server" OnRowCommand="GridView1_RowCommand"
        AutoGenerateColumns="False" CssClass="mydatagrid"
        PagerStyle-CssClass="pager" HeaderStyle-CssClass="header" RowStyle-CssClass="rows"
        EmptyDataText="ไม่มีรายการเลื่อนเข้าพัก">
        <Columns>
            <asp:ButtonField ButtonType="Button" CommandName="EditReservation" Text="ลงจอง"
                ControlStyle-CssClass="btn-edit" />

            <asp:BoundField DataField="ID" HeaderText="เลขที่จอง" HeaderStyle-CssClass="header-center"
                ItemStyle-CssClass="header-center">
                <HeaderStyle CssClass="header-center" />
                <ItemStyle CssClass="header-center" />
            </asp:BoundField>

            <asp:BoundField DataField="Name" HeaderText="ชื่อผู้จอง" HeaderStyle-CssClass="header-center">
                <HeaderStyle CssClass="header-center" />
            </asp:BoundField>

            <asp:BoundField DataField="NickName" HeaderText="ชื่อ Facebook" HeaderStyle-CssClass="header-center">
                <HeaderStyle CssClass="header-center" />
            </asp:BoundField>

            <asp:BoundField DataField="Customer_MobilePhone" HeaderText="เบอร์โทรศัพท์"
                HeaderStyle-CssClass="header-center" ItemStyle-CssClass="header-center">
                <HeaderStyle CssClass="header-center" />
                <ItemStyle CssClass="header-center" />
            </asp:BoundField>

            <asp:BoundField DataField="Deposit" HeaderText="ยอดมัดจำ"
                HeaderStyle-CssClass="header-center" ItemStyle-CssClass="header-center"
                DataFormatString="{0:N0}">
                <HeaderStyle CssClass="header-center" />
                <ItemStyle CssClass="header-center" />
            </asp:BoundField>

            <asp:BoundField DataField="TotalPrice" HeaderText="ยอดรวม"
                HeaderStyle-CssClass="header-center" ItemStyle-CssClass="header-center"
                DataFormatString="{0:N0}">
                <HeaderStyle CssClass="header-center" />
                <ItemStyle CssClass="header-center" />
            </asp:BoundField>

            <asp:TemplateField HeaderText="จำนวนครั้งที่เลื่อน" HeaderStyle-CssClass="header-center"
                ItemStyle-CssClass="header-center">
                <ItemTemplate>
                    <span class="reschedule-count">
                        <%# Eval("RescheduleCount") != DBNull.Value ? Eval("RescheduleCount") : "0" %> ครั้ง
                    </span>
                </ItemTemplate>
                <HeaderStyle CssClass="header-center" />
                <ItemStyle CssClass="header-center" />
            </asp:TemplateField>

            <asp:BoundField DataField="Remark" HeaderText="หมายเหตุ" HeaderStyle-CssClass="header-center">
                <HeaderStyle CssClass="header-center" />
            </asp:BoundField>

            <asp:TemplateField HeaderText="เหตุผลล่าสุด" HeaderStyle-CssClass="header-center">
                <ItemTemplate>
                    <span class="reason-text">
                        <%# Eval("LastRescheduleReason") != DBNull.Value ? Eval("LastRescheduleReason") : "-" %>
                    </span>
                </ItemTemplate>
                <HeaderStyle CssClass="header-center" />
            </asp:TemplateField>

            <asp:TemplateField HeaderStyle-Width="3%">
                <ItemTemplate>
                    <asp:Button ID="Button1" runat="server" Text="ยกเลิก" CssClass="btn-cancel"
                        CommandArgument='<%# Eval("ID") %>'
                        CommandName="CancelPostpone"
                        OnClientClick="return confirm('ยืนยันการยกเลิกรายการเลื่อนเข้าพักหรือไม่?');"
                        OnClick="DeleteButton_Click"/>
                </ItemTemplate>
                <HeaderStyle Width="3%" />
            </asp:TemplateField>
        </Columns>

        <HeaderStyle CssClass="header" />
        <PagerStyle CssClass="pager" />
        <RowStyle CssClass="rows" />
    </asp:GridView>
    </center>
</asp:Content>
