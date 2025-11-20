<%@ Page Title="" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Report.aspx.cs" Inherits="Take_Time_BangPhra.Account.Report.Report" %>
<%@ Register assembly="Microsoft.ReportViewer.WebForms" namespace="Microsoft.Reporting.WebForms" tagprefix="rsweb" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
      <table>
          <tr>
      <link rel="stylesheet" href="/Content/jquery-ui.css">
  <link rel="stylesheet" href="/Content/style.css">
  <script src="/Scripts/jquery-1.12.4.js"></script>
  <script src="/Scripts/jquery-ui.js"></script>

  <script>
  $( function() {
      $( "#MainContent_datepicker1" ).datepicker();
  });
      $(function () {
          $("#MainContent_datepicker2").datepicker();
      });
  </script>
    <table style="width:100%;">
        <tr>
            <td>
                <br />

                <asp:Panel ID="Panel6" runat="server" Visible="True">
                <rsweb:reportviewer ID="ReportViewer2" runat="server" Width="100%" BackColor="" ClientIDMode="AutoID" HighlightBackgroundColor="" InternalBorderColor="204, 204, 204" InternalBorderStyle="Solid" InternalBorderWidth="1px" LinkActiveColor="" LinkActiveHoverColor="" LinkDisabledColor="" PrimaryButtonBackgroundColor="" PrimaryButtonForegroundColor="" PrimaryButtonHoverBackgroundColor="" PrimaryButtonHoverForegroundColor="" SecondaryButtonBackgroundColor="" SecondaryButtonForegroundColor="" SecondaryButtonHoverBackgroundColor="" SecondaryButtonHoverForegroundColor="" SplitterBackColor="" ToolbarDividerColor="" ToolbarForegroundColor="" ToolbarForegroundDisabledColor="" ToolbarHoverBackgroundColor="" ToolbarHoverForegroundColor="" ToolBarItemBorderColor="" ToolBarItemBorderStyle="Solid" ToolBarItemBorderWidth="1px" ToolBarItemHoverBackColor="" ToolBarItemPressedBorderColor="51, 102, 153" ToolBarItemPressedBorderStyle="Solid" ToolBarItemPressedBorderWidth="1px" ToolBarItemPressedHoverBackColor="153, 187, 226" Height="500px">
        <LocalReport ReportPath="Account\Report\Receipt.rdlc">
            
        </LocalReport>
</rsweb:reportviewer></asp:Panel>
                 
                </td>
        </tr>
    </table>
          </tr>
</table>
</asp:Content>
