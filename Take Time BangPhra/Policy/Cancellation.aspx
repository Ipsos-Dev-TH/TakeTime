<%@ Page Title="นโยบายการยกเลิกการจอง" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Cancellation.aspx.cs" Inherits="Take_Time_BangPhra.PolicyPages.Cancellation" %>
<%@ Register Src="~/Policy/PolicyView.ascx" TagPrefix="tt" TagName="PolicyView" %>
<%-- หน้าสาธารณะ: นโยบายการยกเลิกการจอง (Cancellation Policy) — /Policy/Cancellation · เนื้อหาวาดโดย PolicyView.ascx (ใช้ร่วมทุกหน้านโยบาย) --%>

<asp:Content ID="SeoContent" ContentPlaceHolderID="SeoHead" runat="server">
    <%= SeoHeadHtml %>
</asp:Content>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">
    <tt:PolicyView ID="PolicyView1" runat="server" />
</asp:Content>
