using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;
using Take_Time_BangPhra.Helpers;

namespace Take_Time_BangPhra.API
{
    public class VendorAPI : IHttpHandler
    {
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();
        private readonly code _code = new code();
        private readonly string _connectionString;

        private static readonly HttpClient _httpClient;

        static VendorAPI()
        {
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) => true;
            _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
        }

        public VendorAPI()
        {
            _connectionString = DatabaseHelper.GetConnectionString();
        }

        public void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "application/json; charset=utf-8";

            if (context.Session["permission"]?.ToString() != "True")
            {
                WriteJson(context, new { success = false, message = "Unauthorized" }, 401);
                return;
            }

            string action = context.Request.QueryString["action"] ?? context.Request.Form["action"] ?? "";

            switch (action.ToLower())
            {
                case "lookup":
                    HandleTaxIdLookup(context);
                    break;
                case "search":
                    HandleSearch(context);
                    break;
                default:
                    WriteJson(context, new { success = false, message = "Unknown action" }, 400);
                    break;
            }
        }

        private void HandleTaxIdLookup(HttpContext context)
        {
            string taxId = (context.Request.QueryString["taxId"] ?? context.Request.Form["taxId"] ?? "").Trim();

            if (string.IsNullOrEmpty(taxId) || taxId.Length != 13)
            {
                WriteJson(context, new { success = false, message = "Tax ID must be 13 digits" });
                return;
            }

            var result = new Dictionary<string, object> { { "taxId", taxId } };
            string source = "";

            var localParams = new Dictionary<string, object> { { "@IDNumber", taxId } };
            DataTable dtLocal = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT V.*, A.Province, A.District, A.SubDistrict, A.PostalCode,
                         CT.Customer_Type AS TypeName
                  FROM Vendor V
                  LEFT JOIN Address A ON A.ID = V.Address_ID
                  LEFT JOIN Customer_Type CT ON CT.ID = V.Vendor_Type_ID
                  WHERE V.IDNumber = @IDNumber AND V.Status = 'True'",
                localParams);

            if (dtLocal != null && dtLocal.Rows.Count > 0)
            {
                DataRow row = dtLocal.Rows[0];
                result["found"] = true;
                result["existsInDb"] = true;
                result["vendorId"] = row["ID"]?.ToString();
                result["name"] = row["Name"]?.ToString() ?? "";
                result["branchNumber"] = row["Branch_Number"]?.ToString() ?? "00000";
                result["vendorTypeId"] = row["Vendor_Type_ID"]?.ToString() ?? "";
                result["vendorTypeName"] = row["TypeName"]?.ToString() ?? "";
                result["vendorGroup"] = row["Vendor_Group"]?.ToString() ?? "";
                result["phone"] = row["Phone_Number"]?.ToString() ?? "";
                result["address"] = row["Address"]?.ToString() ?? "";
                result["address1"] = row["Address1"]?.ToString() ?? "";
                result["province"] = row["Province"]?.ToString() ?? "";
                result["district"] = row["District"]?.ToString() ?? "";
                result["subDistrict"] = row["SubDistrict"]?.ToString() ?? "";
                result["postalCode"] = row["PostalCode"]?.ToString() ?? "";
                result["email"] = row.Table.Columns.Contains("Email") && row["Email"] != DBNull.Value ? row["Email"].ToString() : "";
                result["contactPerson"] = row.Table.Columns.Contains("Contact_Person") && row["Contact_Person"] != DBNull.Value ? row["Contact_Person"].ToString() : "";
                result["remark"] = row.Table.Columns.Contains("Remark") && row["Remark"] != DBNull.Value ? row["Remark"].ToString() : "";
                source = "local";
            }
            else
            {
                var rdResult = LookupFromRevenueService(taxId);
                if (rdResult != null && rdResult.ContainsKey("found") && (bool)rdResult["found"])
                {
                    foreach (var kv in rdResult)
                        result[kv.Key] = kv.Value;
                    result["existsInDb"] = false;
                    source = "rd";
                }
                else
                {
                    result["found"] = false;
                    result["existsInDb"] = false;
                    source = "none";
                }
            }

            result["source"] = source;
            WriteJson(context, new { success = true, data = result });
        }

        private Dictionary<string, object> LookupFromRevenueService(string taxId)
        {
            try
            {
                string soapBody = string.Format(
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
                    <soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/""
                                   xmlns:tns=""https://rdws.rd.go.th/serviceRD3/checktinpinservice"">
                        <soap:Body>
                            <tns:ServiceTIN>
                                <tns:username>anonymous</tns:username>
                                <tns:password>anonymous</tns:password>
                                <tns:TIN>{0}</tns:TIN>
                            </tns:ServiceTIN>
                        </soap:Body>
                    </soap:Envelope>", taxId);

                var request = new HttpRequestMessage(HttpMethod.Post,
                    "https://rdws.rd.go.th/serviceRD3/checktinpinservice.asmx");
                request.Content = new StringContent(soapBody, System.Text.Encoding.UTF8, "text/xml");
                request.Headers.Add("SOAPAction", "https://rdws.rd.go.th/serviceRD3/checktinpinservice/ServiceTIN");

                var response = _httpClient.SendAsync(request).GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                    return null;

                string xml = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                var result = new Dictionary<string, object>();

                string titleName = ExtractXmlValue(xml, "vTitleName");
                string name = ExtractXmlValue(xml, "vName");
                string surName = ExtractXmlValue(xml, "vSurName");
                string buildingName = ExtractXmlValue(xml, "vBuildingName");
                string floorNumber = ExtractXmlValue(xml, "vFloorNumber");
                string villageName = ExtractXmlValue(xml, "vVillageName");
                string roomNumber = ExtractXmlValue(xml, "vRoomNumber");
                string houseNumber = ExtractXmlValue(xml, "vHouseNumber");
                string mooNumber = ExtractXmlValue(xml, "vMooNumber");
                string soiName = ExtractXmlValue(xml, "vSoiName");
                string streetName = ExtractXmlValue(xml, "vStreetName");
                string thambolName = ExtractXmlValue(xml, "vThambolName");
                string amphurName = ExtractXmlValue(xml, "vAmphurName");
                string provinceName = ExtractXmlValue(xml, "vProvinceName");
                string postCode = ExtractXmlValue(xml, "vPostCode");
                string msgerr = ExtractXmlValue(xml, "vmsgerr");

                string fullName = (titleName + name + " " + surName).Trim();
                if (string.IsNullOrWhiteSpace(fullName) || fullName == " ")
                    return null;

                string addressLine = BuildAddressLine(houseNumber, buildingName, floorNumber,
                    roomNumber, villageName, mooNumber, soiName, streetName);

                result["found"] = true;
                result["name"] = fullName;
                result["branchNumber"] = "00000";
                result["address"] = addressLine;
                result["province"] = provinceName ?? "";
                result["district"] = amphurName ?? "";
                result["subDistrict"] = thambolName ?? "";
                result["postalCode"] = postCode ?? "";
                result["rdMessage"] = msgerr ?? "";

                return result;
            }
            catch
            {
                return null;
            }
        }

        private static string ExtractXmlValue(string xml, string tag)
        {
            string openTag = "<" + tag + ">";
            string closeTag = "</" + tag + ">";
            int start = xml.IndexOf(openTag, StringComparison.Ordinal);
            if (start < 0) return "";
            start += openTag.Length;
            int end = xml.IndexOf(closeTag, start, StringComparison.Ordinal);
            if (end < 0) return "";
            return xml.Substring(start, end - start).Trim();
        }

        private static string BuildAddressLine(string houseNumber, string buildingName,
            string floorNumber, string roomNumber, string villageName,
            string mooNumber, string soiName, string streetName)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(houseNumber)) parts.Add(houseNumber);
            if (!string.IsNullOrEmpty(buildingName)) parts.Add("อาคาร" + buildingName);
            if (!string.IsNullOrEmpty(floorNumber)) parts.Add("ชั้น " + floorNumber);
            if (!string.IsNullOrEmpty(roomNumber)) parts.Add("ห้อง " + roomNumber);
            if (!string.IsNullOrEmpty(villageName)) parts.Add("หมู่บ้าน" + villageName);
            if (!string.IsNullOrEmpty(mooNumber)) parts.Add("หมู่ " + mooNumber);
            if (!string.IsNullOrEmpty(soiName)) parts.Add("ซอย" + soiName);
            if (!string.IsNullOrEmpty(streetName)) parts.Add("ถนน" + streetName);
            return string.Join(" ", parts);
        }

        private void HandleSearch(HttpContext context)
        {
            string q = (context.Request.QueryString["q"] ?? "").Trim();
            if (string.IsNullOrEmpty(q) || q.Length < 2)
            {
                WriteJson(context, new { success = true, data = new object[0] });
                return;
            }

            var parameters = new Dictionary<string, object> { { "@Search", "%" + q + "%" } };
            DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT TOP 10 V.ID, V.IDNumber, V.Name, V.Branch_Number, V.Vendor_Group, V.Phone_Number
                  FROM Vendor V
                  WHERE V.Status = 'True' AND (V.Name LIKE @Search OR V.IDNumber LIKE @Search)
                  ORDER BY V.Name",
                parameters);

            var results = new List<Dictionary<string, string>>();
            if (dt != null)
            {
                foreach (DataRow row in dt.Rows)
                {
                    results.Add(new Dictionary<string, string>
                    {
                        { "id", row["ID"].ToString() },
                        { "taxId", row["IDNumber"]?.ToString() ?? "" },
                        { "name", row["Name"]?.ToString() ?? "" },
                        { "branch", row["Branch_Number"]?.ToString() ?? "" },
                        { "group", row["Vendor_Group"]?.ToString() ?? "" },
                        { "phone", row["Phone_Number"]?.ToString() ?? "" }
                    });
                }
            }

            WriteJson(context, new { success = true, data = results });
        }

        private void WriteJson(HttpContext context, object data, int statusCode = 200)
        {
            context.Response.StatusCode = statusCode;
            context.Response.Write(_serializer.Serialize(data));
        }

        public bool IsReusable => false;
    }
}
