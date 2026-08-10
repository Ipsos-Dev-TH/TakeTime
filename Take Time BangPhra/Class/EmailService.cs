// EmailService.cs
using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;

namespace Take_Time_BangPhra.Services
{
    public class EmailService
    {
        private readonly string _smtpServer;
        private readonly int _port;
        private readonly bool _enableSsl;
        private readonly bool _useDefaultCredentials;
        private readonly string _fromEmail;
        private readonly string _password;
        private readonly string _ccEmail;

        public EmailService()
        {
            _smtpServer = AppCfg.Get("SMTP");
            _port = Convert.ToInt32(AppCfg.Get("SMTP_Port"));
            _enableSsl = Convert.ToBoolean(AppCfg.Get("SMTP_EnableSsl"));
            _useDefaultCredentials = Convert.ToBoolean(AppCfg.Get("SMTP_UseDefaultCredentials"));
            _fromEmail = AppCfg.Get("Email_From");
            _password = AppCfg.Get("Email_Password_From");
            _ccEmail = AppCfg.Get("Email_CC");
        }

        public void SendEmail(string to, string subject, string body, Attachment[] attachments = null)
        {
            SendEmail(to, null, subject, body, attachments);
        }

        /// <summary>ส่งอีเมลพร้อมระบุ CC เอง (คั่นหลายอีเมลด้วย , หรือ ;). null/ว่าง → ใช้ CC จาก AppSettings เดิม</summary>
        public void SendEmail(string to, string cc, string subject, string body, Attachment[] attachments = null)
        {
            using (MailMessage mail = new MailMessage(_fromEmail, to))
            using (SmtpClient client = new SmtpClient(_smtpServer, _port))
            {
                client.EnableSsl = _enableSsl;
                client.UseDefaultCredentials = _useDefaultCredentials;
                client.Credentials = new NetworkCredential(_fromEmail, _password);

                // CC ที่ระบุมาเอง (หน้าส่ง e-Tax) — ถ้าไม่ระบุ fallback CC กลางจาก AppSettings
                string ccToUse = !string.IsNullOrWhiteSpace(cc) ? cc : _ccEmail;
                if (!string.IsNullOrWhiteSpace(ccToUse))
                {
                    foreach (var addr in ccToUse.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string a = addr.Trim();
                        if (a.Length > 0 && a.Contains("@"))
                            try { mail.CC.Add(a); } catch { }
                    }
                }

                mail.Subject = subject;
                mail.Body = body;
                mail.IsBodyHtml = true;

                if (attachments != null)
                {
                    foreach (var attachment in attachments)
                    {
                        mail.Attachments.Add(attachment);
                    }
                }

                client.Send(mail);
            }
        }

        public void SendReceiptEmail(string toEmail, string receiptId, DateTime docDate, string pdfFilePath)
        {
            string docCreateThaiDate = docDate.ToString("ddMM") + (docDate.Year + 543).ToString();
            string subject = $"[{docCreateThaiDate}][INV][{receiptId}]";
            string body = @"เรียน ลูกค้าผู้มีอุปการะคุณ <br /><br />
                          หจก.แอม แฮปปี้เนส (Take Time) ได้แนบใบกำกับภาษี/ใบเสร็จรับเงินมาพร้อมกับอีเมล์ฉบับนี้ 
                          ท่านสามารถเปิดดูได้โดยคลิกไฟล์แนบ (PDF File)<br />
                          ขอแสดงความนับถือ<br />
                          หจก.แอม แฮปปี้เนส (Take Time)";

            byte[] bytes = System.IO.File.ReadAllBytes(pdfFilePath);
            var memoryStream = new System.IO.MemoryStream(bytes);
            var attachment = new Attachment(memoryStream, $"{receiptId}_etax.pdf");

            SendEmail(toEmail, subject, body, new Attachment[] { attachment });
        }
    }
}