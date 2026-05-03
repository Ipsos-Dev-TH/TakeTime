using System;
using System.Collections.Generic;
using System.IO;
using System.Web;

namespace Take_Time_BangPhra.API
{
    public class FileHandler : IHttpHandler
    {
        private static readonly Dictionary<string, string> MimeTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { ".jpg", "image/jpeg" },
            { ".jpeg", "image/jpeg" },
            { ".png", "image/png" },
            { ".gif", "image/gif" },
            { ".pdf", "application/pdf" },
            { ".doc", "application/msword" },
            { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
            { ".xls", "application/vnd.ms-excel" },
            { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" }
        };

        public void ProcessRequest(HttpContext context)
        {
            string filePath = context.Request.QueryString["path"];

            if (string.IsNullOrEmpty(filePath))
            {
                context.Response.StatusCode = 400;
                context.Response.Write("Missing path parameter");
                return;
            }

            // Security: decode and normalize to catch URL-encoded traversal attempts
            filePath = Uri.UnescapeDataString(filePath);

            // Security: only allow files under Documents folder
            if (!filePath.StartsWith("~/Documents/", StringComparison.OrdinalIgnoreCase) &&
                !filePath.StartsWith("Documents/", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 403;
                context.Response.Write("Access denied");
                return;
            }

            // Security: prevent directory traversal
            if (filePath.Contains("..") || filePath.Contains("\0"))
            {
                context.Response.StatusCode = 403;
                context.Response.Write("Invalid path");
                return;
            }

            string physicalPath = context.Server.MapPath(filePath);

            // Security: verify resolved path is within the Documents directory
            string documentsRoot = context.Server.MapPath("~/Documents/");
            string resolvedFull = Path.GetFullPath(physicalPath);
            if (!resolvedFull.StartsWith(Path.GetFullPath(documentsRoot), StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 403;
                context.Response.Write("Access denied");
                return;
            }

            if (!File.Exists(physicalPath))
            {
                context.Response.StatusCode = 404;
                context.Response.Write("File not found");
                return;
            }

            string extension = Path.GetExtension(physicalPath);

            if (!MimeTypes.TryGetValue(extension, out string mimeType))
            {
                context.Response.StatusCode = 403;
                context.Response.Write("File type not allowed");
                return;
            }

            context.Response.ContentType = mimeType;
            context.Response.Cache.SetCacheability(HttpCacheability.Public);
            context.Response.Cache.SetMaxAge(TimeSpan.FromHours(1));
            context.Response.TransmitFile(physicalPath);
        }

        public bool IsReusable
        {
            get { return true; }
        }
    }
}
