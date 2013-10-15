using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Text;
using System.Security.Cryptography.X509Certificates;


    /// <summary>
    /// Encapsulates WebReader functions.
    /// </summary>
    public class HtmlReaderManager
    {
        public HtmlReaderManager()
        {
        }

        public HtmlReaderManager(string baseUri)
        {
            BaseUri = baseUri;
        }

        private X509Certificate _certificate;
        public X509Certificate Certificate
        {
            get { return _certificate; }
            set { _certificate = value; }
        }

        private string _baseUri = string.Empty;

        private float _pageSize;
        public float PageSize
        {
            get { return _pageSize; }
            set { _pageSize = value; }
        }

        public string LastPostLocation { get; set; }
        public string BaseUri
        {
            get { return _baseUri; }
            set { _baseUri = value; }
        }        

        private string _previousUri;
        public string PreviousUri
        {
            get { return _previousUri; }
            set { _previousUri = value; }
        }

        private CookieContainer _cookieContainer = new CookieContainer();
        public CookieContainer CookieContainer
        {
            get { return _cookieContainer; }
            set { _cookieContainer = value; }
        }

        private string _userAgent = @"Mozilla/5.0 (Windows; U; Windows NT 5.1; ru; rv:1.9.1.2) Gecko/20090729 Firefox/3.5.2";
        public string UserAgent
        {
            get { return _userAgent; }
            set { _userAgent = value; }
        }

        private string _accept =
            @"image/gif, image/x-xbitmap, image/jpeg, image/pjpeg, application/vnd.ms-excel, application/vnd.ms-powerpoint, */*";
        public string Accept
        {
            get { return _accept; }
            set { _accept = value; }
        }

        private Uri _requestUri;
        public Uri RequestUri
        {
            get { return _requestUri; }
            set { _requestUri = value; }
        }

        private string _responseUri;
        public string ResponseUri
        {
            get { return _responseUri; }
            set { _responseUri = value; }
        }

        private string _contentType = string.Empty;
        public string ContentType
        {
            get { return _contentType; }
            set { _contentType = value; }
        }

        private IWebProxy _proxy;
        public IWebProxy Proxy
        {
            get { return _proxy; }
            set { _proxy = value; }
        }

        private string _html;
        public string Html
        {
            get { return _html; }
        }

        private Hashtable _headers = new Hashtable();
        public Hashtable Headers
        {
            get { return _headers; }
        }

        private string _location;
        public string Location
        {
            get { return _location; }
        }

        private bool _sendReferer = true;
        public bool SendReferer
        {
            get { return _sendReferer; }
            set { _sendReferer = value; }
        }

        private HttpStatusCode _statusCode;
        public HttpStatusCode StatusCode
        {
            get { return _statusCode; }
        }

        private int _timeout;
        public int Timeout
        {
            get { return _timeout; }
            set { _timeout = value; }
        }

        public HttpStatusCode Request(string requestUri, string method, string postData)
        {
            string uri =  requestUri;

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);

            if (Proxy != null)
                request.Proxy = Proxy;

            request.CookieContainer = CookieContainer;
            request.UserAgent = UserAgent;
            request.Accept = Accept;
            request.Headers.Add("Accept-Language", "ru-ru,ru;q=0.8,en-us;q=0.5,en;q=0.3");
            
            request.Method = method;
            request.KeepAlive = true;
            
            if (SendReferer)
                request.Referer = PreviousUri != null ? PreviousUri : uri;

            foreach (string key in Headers.Keys)
                request.Headers.Add(key, Headers[key].ToString());

            if (method == "POST")
            {
                request.ContentType = "application/x-www-form-urlencoded";
                request.AllowAutoRedirect = false;
            }
            else
            {
                request.ContentType = ContentType;
                request.AllowAutoRedirect = true;
            }

            PreviousUri = uri;

            if (Certificate != null)
                request.ClientCertificates.Add(Certificate);

            if (Timeout != 0)
                request.Timeout = Timeout;

            if (postData != null)
            {
                
                using (Stream st = request.GetRequestStream())
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(postData);
                    st.Write(bytes, 0, bytes.Length);
                }
            }

            _html = "";

            using (HttpWebResponse resp = (HttpWebResponse)request.GetResponse())
            using (Stream sm = resp.GetResponseStream())
            using (StreamReader sr = new StreamReader(sm, Encoding.UTF8))
            {
                _statusCode = resp.StatusCode;
                _location = resp.Headers["Location"];
                try
                {
                   
                    _html = sr.ReadToEnd();
                    UTF8Encoding encoding = new UTF8Encoding();
                    
                    Byte[] bytes = encoding.GetBytes(_html);
                    float pageSizeInMb = (bytes.Length / 1024f) / 1024f;
                    PageSize = pageSizeInMb;
                    
                }
                catch (Exception ex)
                {
                    var s = ex;
                    
                }
                if (resp.ResponseUri.AbsoluteUri.StartsWith(BaseUri) == false)
                    BaseUri = resp.ResponseUri.Scheme + "://" + resp.ResponseUri.Host;

                _responseUri = resp.ResponseUri.ToString();

                CookieCollection cc = request.CookieContainer.GetCookies(request.RequestUri);

                // This code fixes the situation when a server sets a cookie without the 'path'.
                // IE takes this as the root ('/') value,
                // the HttpWebRequest class as the RequestUri.AbsolutePath value.
                //
                foreach (Cookie c in cc)
                {
                    if (c.Path == request.RequestUri.AbsolutePath)
                    {
                        CookieContainer.Add(new Cookie(c.Name, c.Value, "/", c.Domain));
                    }

                    string d = c.Domain;
                    int n = d.Length;
                }
            }

            RequestUri = request.RequestUri;

            return StatusCode;
        }

        public HttpStatusCode Get(string requestUri)
        {
            return Request(requestUri, "GET", null);
        }

        public HttpStatusCode Post(string requestUri, string postData)
        {
            Request(requestUri, "POST", postData);

            for (int i = 0; i < 10; i++)
            {
                bool post = false;

                switch (StatusCode)
                {
                    case HttpStatusCode.MultipleChoices:  // 300
                    case HttpStatusCode.MovedPermanently: // 301
                    case HttpStatusCode.Found:            // 302
                    case HttpStatusCode.SeeOther:         // 303
                        break;

                    case HttpStatusCode.TemporaryRedirect: // 307
                        post = true;
                        break;

                    default:
                        return StatusCode;
                }
                if (Location != null)
                    LastPostLocation = Location;
                if (Location == null)
                    break;

                Uri uri = new Uri(new Uri(PreviousUri), Location);

                //BaseUri = uri.Scheme + "://" + uri.Host;
                //requestUri = uri.AbsolutePath + uri.Query;

                Request(requestUri, post ? "POST" : "GET", post ? postData : null);
            }

            return StatusCode;
        }

        public void LoadCertificate(string fileName)
        {
            Certificate = X509Certificate.CreateFromCertFile(fileName);
        }
    }
