<%@ Page Title="" Language="C#" MasterPageFile="~/Site2.Master" AutoEventWireup="true" CodeBehind="Login.aspx.cs" Inherits="Take_Time_BangPhra.Affiliate.Login" %>
<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">
    <!--===============================================================================================-->	
	<link rel="icon" type="image/png" href="../IMG/icons/favicon.ico"/>
<!--===============================================================================================-->
	<link rel="stylesheet" type="text/css" href="../Content/vendor/bootstrap/css/bootstrap.min.css">
<!--===============================================================================================-->
	<link rel="stylesheet" type="text/css" href="../fonts/font-awesome-4.7.0/css/font-awesome.min.css">
<!--===============================================================================================-->
	<link rel="stylesheet" type="text/css" href="../fonts/Linearicons-Free-v1.0.0/icon-font.min.css">
<!--===============================================================================================-->
	<link rel="stylesheet" type="text/css" href="../Content/vendor/animate/animate.css">
<!--===============================================================================================-->	
	<link rel="stylesheet" type="text/css" href="../Content/vendor/css-hamburgers/hamburgers.min.css">
<!--===============================================================================================-->
	<link rel="stylesheet" type="text/css" href="../Content/vendor/animsition/css/animsition.min.css">
<!--===============================================================================================-->
	<link rel="stylesheet" type="text/css" href="../Content/vendor/select2/select2.min.css">
<!--===============================================================================================-->	
	<link rel="stylesheet" type="text/css" href="../Content/vendor/daterangepicker/daterangepicker.css">
<!--===============================================================================================-->
	<link rel="stylesheet" type="text/css" href="../Content/util.css">
	<link rel="stylesheet" type="text/css" href="../Content/main.css">
<!--===============================================================================================-->
     <style>

                 .mycheckbox input[type="checkbox"] 
{ 
    margin-right: 5px; 
}

         .radioBL input[type="radio"]
    {
        margin-right:10px;
    }
        .rounded-textbox {
    border-radius: 10px; /* Adjust the value for more/less rounding */
    padding: 5px;
    border: 1px solid #ccc; /* Optional: Border styling */
    outline: none; /* Optional: Remove focus outline */
}
 .header-center{
        text-align:center;
    }
  .header-right{
        text-align:right;
    }
         </style>
	<center>
    <div class="limiter">
		<div class="container-login100">
			<div class="wrap-login100">
				<div class="login100-form-title" style="background-image: url(../Images/Affiliate.png);">
					<span class="login100-form-title-1">
						Affiliate
					</span>
				</div>

				<div class="login100-form validate-form">
					<div class="wrap-input100 validate-input m-b-26" data-validate="Username is required">
						<span class="label-input100">Username</span>
						<asp:TextBox ID="TextBox1" runat="server" placeholder="รหัสบัตรประชาชน" CssClass="input100" name="username"></asp:TextBox> 
						<span class="focus-input100"></span>
					</div>

					<div class="wrap-input100 validate-input m-b-18" data-validate = "Password is required">
						<span class="label-input100">Password</span>
						<asp:TextBox ID="TextBox2" runat="server" TextMode="Password" placeholder="รหัสผ่าน" CssClass="input100"></asp:TextBox>
						<span class="focus-input100"></span>
					</div>

					<div class="flex-sb-m w-full p-b-30">
						<div class="contact100-form-checkbox">
							</div>

						<div>
							<a href="https://ipsos.service-now.com/ess" class="txt1">
								</a></div>
					</div>

					<div class="container-login100-form-btn">
						<asp:Button ID="Button1" runat="server" Text="Login" OnClick="Button1_Click" class="login100-form-btn" /> &nbsp;&nbsp; <a href="Register.aspx" style="font-size: large"><strong>สมัครสมาชิก</strong></a>
					</div>
					
				</div>
				
			</div>
		</div>
	
	</div>
	
<!--===============================================================================================-->
	<script src="Content/vendor/jquery/jquery-3.2.1.min.js"></script>
<!--===============================================================================================-->
	<script src="Content/vendor/animsition/js/animsition.min.js"></script>
<!--===============================================================================================-->
	<script src="Content/vendor/bootstrap/js/popper.js"></script>
	<script src="Content/vendor/bootstrap/js/bootstrap.min.js"></script>
<!--===============================================================================================-->
	<script src="Content/vendor/select2/select2.min.js"></script>
<!--===============================================================================================-->
	<script src="Content/vendor/daterangepicker/moment.min.js"></script>
	<script src="Content/vendor/daterangepicker/daterangepicker.js"></script>
<!--===============================================================================================-->
	<script src="Content/vendor/countdowntime/countdowntime.js"></script>
<!--===============================================================================================-->
	<script src="Scripts/main.js"></script>
            </center>
</asp:Content>


