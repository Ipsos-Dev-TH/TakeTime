<%@ Page Title="ข้อกำหนดและเงื่อนไข" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Terms.aspx.cs" Inherits="Take_Time_BangPhra.PolicyPages.Terms" %>
<%@ Register Src="~/Policy/PolicyView.ascx" TagPrefix="tt" TagName="PolicyView" %>
<%-- หน้าสาธารณะ: ข้อกำหนดและเงื่อนไข (Terms &amp; Conditions) — /Policy/Terms · เนื้อหาวาดโดย PolicyView.ascx (ใช้ร่วมทุกหน้านโยบาย) --%>

<asp:Content ID="SeoContent" ContentPlaceHolderID="SeoHead" runat="server">
    <%= SeoHeadHtml %>
</asp:Content>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">
    <tt:PolicyView ID="PolicyView1" runat="server" />
</asp:Content>
