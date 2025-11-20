using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra
{
    public partial class Gallery : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string folder = Request.QueryString["folder"];
            string imgfolder = ConfigurationSettings.AppSettings["ImagesFolderPath"].ToString();
            string[] files = Directory.GetFiles(Path.Combine(imgfolder, "Accommodation", folder));

            // Sort files alphabetically
            Array.Sort(files);

            StringBuilder sb = new StringBuilder();

            sb.Append("<div class=\"slideshow-container\">");

            // Create slides
            for (int i = 0; i < files.Length; i++)
            {
                string fileName = Path.GetFileName(files[i]);
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(files[i]);

                sb.Append($@"
                    <div class=""mySlides"">
                        <div class=""numbertext"">{i + 1} / {files.Length}</div>
                        <img src=""./Images/Accommodation/{folder}/{fileName}"" alt=""{fileNameWithoutExt}"">
                    </div>");
            }

            sb.Append($@"
                <a class=""prev"" onclick=""plusSlides(-1)"">❮</a>
                <a class=""next"" onclick=""plusSlides(1)"">❯</a>
                </div>
                <div class=""caption-container"">
                    <p id=""caption""></p>
                </div>
                <div class=""thumbnail-row"">");

            // Create thumbnails
            for (int i = 0; i < files.Length; i++)
            {
                string fileName = Path.GetFileName(files[i]);
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(files[i]);

                sb.Append($@"
                    <div class=""thumbnail-column"">
                        <img class=""thumbnail"" src=""./Images/Accommodation/{folder}/{fileName}"" 
                             onclick=""currentSlide({i + 1})"" alt=""{fileNameWithoutExt}"">
                    </div>");
            }

            sb.Append("</div>");

            Literal1.Mode = LiteralMode.PassThrough;
            Literal1.Text = sb.ToString();
        }
    }
}