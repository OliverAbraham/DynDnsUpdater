//-------------------------------------------------------------------------------------------------
// Klasse zum Downloaden einer Internetseite
// Oliver Abraham, 4/2010
// Siehe auch: http://www.codeproject.com/KB/cs/SeansDownloader.aspx
//-------------------------------------------------------------------------------------------------


using System.Collections.Generic;
using System.Text;
using System.Net;
using System.IO;
using System.Web;

namespace Abraham.Internet
{
	public class HTTPClient
    {
        public string ContentType { get; set; } = "text/json";

        public string SendGetRequest(string url, int timeout = -1)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.CookieContainer = new CookieContainer();
            request.AllowAutoRedirect = true;
            if (timeout != -1)
                request.Timeout = timeout;
            string Seiteninhalt = "";

            HttpWebResponse httpResponse = (HttpWebResponse)request.GetResponse();
            if (httpResponse.StatusCode == HttpStatusCode.OK)
            {
                byte[] httpHeaderData = httpResponse.Headers.ToByteArray();
                Stream httpContentData = httpResponse.GetResponseStream();
                using (httpContentData)
                {
                    Encoding enc = Encoding.UTF8;
                    int AnzahlGelesen;
                    byte[] seiteninhalt = new byte[10000];
                    do
                    {
                        AnzahlGelesen = httpContentData.Read(seiteninhalt, 0, seiteninhalt.Length);
                        Seiteninhalt += enc.GetString(seiteninhalt, 0, AnzahlGelesen);
                    }
                    while (AnzahlGelesen > 0);
                }
            }
            httpResponse.Close();
            return Seiteninhalt;
        }

		public string SendPostRequest(string url, Dictionary<string,string> postParameters, int timeout = -1)
		{
			string authorization = "";

			foreach (string key in postParameters.Keys)
			{
				//postData += HttpUtility.UrlEncode(key) + "=" + HttpUtility.UrlEncode(postParameters[key]) + "&";
				authorization += key + " " + postParameters[key];
			}

			var request = (HttpWebRequest)HttpWebRequest.Create(url);
			request.Method = "POST";
			if (timeout != -1)
                request.Timeout = timeout;

            request.Accept = "*/*";
            request.Headers.Add("Authorization", authorization);

			//byte[] data = Encoding.ASCII.GetBytes(authorization);
			//request.ContentType = ContentType;
			//request.ContentLength = data.Length;
			//Stream requestStream = request.GetRequestStream();
			//requestStream.Write(data, 0, data.Length);
			//requestStream.Close();

			HttpWebResponse myHttpWebResponse = (HttpWebResponse)request.GetResponse();
			Stream responseStream = myHttpWebResponse.GetResponseStream();
			StreamReader myStreamReader = new StreamReader(responseStream, Encoding.Default);
			string pageContent = myStreamReader.ReadToEnd();

			myStreamReader.Close();
			responseStream.Close();
			myHttpWebResponse.Close();

			return pageContent;
		}
    }
}
