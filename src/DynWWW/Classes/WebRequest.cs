﻿using Autodesk.DesignScript.Runtime;
using DynWWW.Helpers;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using RestSharp.Serializers;

namespace DSCore.Web
{
    //[IsVisibleInDynamoLibrary(false)]
    public class WebRequest : IRestRequest
    {
        #region constants

        /// <summary>
        /// The default timeout to wait for a request to return, expressed in miliseconds
        /// </summary>
        private const int defaultRequestTimeout = 1500;

        /// <summary>
        /// The default number of times to retry a failed request
        /// </summary>
        private const int defaultRequestAttempts = 3;

        /// <summary>
        /// The default security protocol to default to.
        /// The protocol is required for interaction with HTTPS endpoints, irrespective of using System.Net or RestSharp libraries.
        /// </summary>
        private const SecurityProtocolType defaultSecurityProtocol = SecurityProtocolType.Tls12;

        #endregion

        #region private properties

        /// <summary>
        /// The encapsulated Restsharp web request
        /// </summary>
        private RestRequest restRequest = new RestRequest();

        /// <summary>
        /// The encapsulated response from the server
        /// </summary>
        private WebResponse response = new WebResponse(new RestResponse());

        private System.TimeSpan timeToComplete;
        private Uri url;

        #endregion

        #region public properties

        /// <summary>
        /// The response the server returned on the last execution of the request.
        /// </summary>
        public WebResponse Respose => this.response;

        /// <summary>
        /// The URL for the request
        /// </summary>
        public string URL
        {
            get => url.ToString();
            set { url = WebHelpers.ParseUriFromString(value.ToString()); }
        }

        /// <summary>
        /// The time it took for the request to complete
        /// </summary>
        public System.TimeSpan Time { get => this.timeToComplete; }

        /// <summary>
        /// Set this property to true to force requests to go through a security protocol (TLS1.2).
        /// This is required when interacting with servers and APIs that require use of HTTPS as a protocol rather than HTTP.
        /// </summary>
        public bool ForceSecurityProtocol = false;

        #endregion

        #region IRestRequest interface fields

        /// <summary>
        /// Container of all HTTP parameters to be passed with the request.
        /// See <see cref="AddParameter(Parameter)"/> for explanation of the types of parameters that can be passed
        /// </summary>
        public List<Parameter> Parameters => restRequest.Parameters;

        /// <summary>
        /// Container of all the files to be uploaded with the request.
        /// </summary>
        public List<FileParameter> Files => restRequest.Files;

        /// <summary>
        /// Determines what HTTP method to use for this request.
        /// Supported methods: GET, POST, PUT, DELETE, HEAD, OPTIONS
        /// Default is GET
        /// </summary>
        public Method Method
        {
            get => restRequest.Method;
            set => restRequest.Method = value;
        }

        /// <summary>
        /// The Resource URL to make the request against, should not include the scheme or domain.
        /// Do not include leading slash. Combined with web client BaseUrl to assemble final URL:
        /// {BaseUrl}/{Resource} (BaseUrl is scheme + domain, e.g. http://example.com)
        /// </summary>
        public string Resource
        {
            get => restRequest.Resource;
            set => restRequest.Resource = value;
        }

        /// <summary>
        /// Used by the default deserializers to determine where to start deserializing from.
        /// Can be used to skip container or root elements that do not have corresponding deserialzation targets.
        /// Example : most APIs return values wrapped in a root "data" element.
        /// </summary>
        public string RootElement
        {
            get => restRequest.RootElement;
            set => restRequest.RootElement = value;
        }

        /// <summary>
        /// Timeout in milliseconds to be used for the request.
        /// This timeout value overrides a timeout set on the  Web Client.
        /// </summary>
        public int Timeout
        {
            get => restRequest.Timeout;
            set => restRequest.Timeout = value;
        }

        /// <summary>
        /// The number of milliseconds before the writing or reading times out.
        /// This timeout value overrides a timeout set on a Web Client.
        /// </summary>
        public int ReadWriteTimeout
        {
            get => restRequest.ReadWriteTimeout;
            set => restRequest.ReadWriteTimeout = value;
        }

        /// <summary>
        /// How many attempts were made to send this Request?
        /// This Number is incremented each time the web client sends the request.
        /// Useful when using Asynchronous Execution with Callbacks
        /// </summary>
        public int Attempts => restRequest.Attempts;

        [IsVisibleInDynamoLibrary(false)]
        public bool AlwaysMultipartFormData
        {
            get => restRequest.AlwaysMultipartFormData;
            set => restRequest.AlwaysMultipartFormData = value;
        }

        [IsVisibleInDynamoLibrary(false)]
        public ISerializer JsonSerializer
        {
            get => restRequest.JsonSerializer;
            set => restRequest.JsonSerializer = value;
        }

        [IsVisibleInDynamoLibrary(false)]
        public ISerializer XmlSerializer
        {
            get => restRequest.XmlSerializer;
            set => restRequest.XmlSerializer = value;
        }

        [IsVisibleInDynamoLibrary(false)]
        public Action<Stream> ResponseWriter
        {
            get => restRequest.ResponseWriter;
            set => restRequest.ResponseWriter = value;
        }

        [IsVisibleInDynamoLibrary(false)]
        public DataFormat RequestFormat
        {
            get => restRequest.RequestFormat;
            set => restRequest.RequestFormat = value;
        }

        [IsVisibleInDynamoLibrary(false)]
        public string DateFormat
        {
            get => restRequest.DateFormat;
            set => restRequest.DateFormat = value;
        }

        [IsVisibleInDynamoLibrary(false)]
        public string XmlNamespace
        {
            get => restRequest.XmlNamespace;
            set => restRequest.XmlNamespace = value;
        }

        [IsVisibleInDynamoLibrary(false)]
        public ICredentials Credentials
        {
            get => restRequest.Credentials;
            set => restRequest.Credentials = value;
        }

        [IsVisibleInDynamoLibrary(false)]
        public bool UseDefaultCredentials
        {
            get => restRequest.UseDefaultCredentials;
            set => restRequest.UseDefaultCredentials = value;
        }

        [IsVisibleInDynamoLibrary(false)]
        public Action<IRestResponse> OnBeforeDeserialization
        {
            get => restRequest.OnBeforeDeserialization;
            set => restRequest.OnBeforeDeserialization = value;
        }

        #endregion

        #region constructor methods

        /// <summary>
        /// Build a simple GET web request to the specified URL
        /// </summary>
        /// <param name="url">The URL to send the request to.</param>
        /// <returns>The request object, ready for execution.</returns>
        public WebRequest(string url)
        {
            URL = url;
            restRequest = new RestRequest(this.URL, Method.GET);
            restRequest.Resource = "";
        }
        #endregion

        #region extension methods

        /// <summary>
        /// Sets the URL of the request.
        /// </summary>
        /// <param name="url">The URL to set for the request.</param>
        /// <returns>The request with an updated URL.</returns>
        public WebRequest CustomSetUrl(string url)
        {
            this.URL = url;
            return this;
        }

        /// <summary>
        /// Adds a parameter to the web request
        /// </summary>
        /// <param name="name">The name of the parameter to add.</param>
        /// <param name="value">The value of the parameter to pass along.</param>
        /// <param name="type">The type of parameter.</param>
        /// <returns></returns>
        public WebRequest CustomAddParameter(string name, object value, ParameterType type)
        {
            if (System.String.IsNullOrEmpty(name) || value == null)
            {
                throw new ArgumentNullException(DynWWW.Properties.Resources.WebRequestParameterNullMessage);
            }

            this.restRequest.AddParameter(name, value, type);

            return this;
        }

        public IRestRequest AddFile(string name, string path, string contentType = null)
        {
            restRequest.AddFile(name, path, contentType);
            return this;
        }

        [IsVisibleInDynamoLibrary(false)]
        public IRestRequest AddFile(string name, byte[] bytes, string fileName, string contentType = null)
        {
            restRequest.AddFile(name, bytes, fileName, contentType);
            return this;
        }

        [IsVisibleInDynamoLibrary(false)]
        public IRestRequest AddFile(string name, Action<Stream> writer, string fileName, string contentType = null)
        {
            restRequest.AddFile(name, writer, fileName, contentType);
            return this;
        }

        [IsVisibleInDynamoLibrary(false)]
        public IRestRequest AddFileBytes(string name, byte[] bytes, string filename, string contentType = "application/x-gzip")
        {
            restRequest.AddFileBytes(name, bytes, filename, contentType);
            return this;
        }

        [IsVisibleInDynamoLibrary(false)]
        public IRestRequest AddBody(object obj, string xmlNamespace)
        {
            restRequest.AddBody(obj, xmlNamespace);
            return this;
        }

        [IsVisibleInDynamoLibrary(false)]
        public IRestRequest AddBody(object obj)
        {
            restRequest.AddBody(obj);
            return this;
       }

        /// <summary>
        /// Serializes obj to JSON format and adds it to the request body.
        /// </summary>
        /// <param name="obj">The object to serialize</param>
        /// <returns>This request</returns>
        public IRestRequest AddJsonBody(object obj)
        {
            restRequest.AddJsonBody(obj);
            return this;
        }

        /// <summary>
        /// Serializes obj to XML format and adds it to the request body.
        /// </summary>
        /// <param name="obj">The object to serialize</param>
        /// <returns>This request</returns>
        public IRestRequest AddXmlBody(object obj)
        {
            restRequest.AddXmlBody(obj);
            return this;
        }

        [IsVisibleInDynamoLibrary(false)]
        public IRestRequest AddXmlBody(object obj, string xmlNamespace)
        {
            restRequest.AddXmlBody(obj, xmlNamespace);
            return this;
        }

        [IsVisibleInDynamoLibrary(false)]
        public IRestRequest AddObject(object obj, params string[] includedProperties)
        {
            restRequest.AddObject(obj, includedProperties);
            return this;
        }

        [IsVisibleInDynamoLibrary(false)]
        public IRestRequest AddObject(object obj)
        {
            restRequest.AddObject(obj);
            return this;
        }

        [IsVisibleInDynamoLibrary(false)]
        public IRestRequest AddParameter(Parameter p)
        {
            restRequest.AddParameter(p);
            return this;
        }

        /// <summary>
        /// Adds a HTTP parameter to the request.
        /// Uses QueryString for GET, DELETE, OPTIONS and HEAD, Encoded form for POST and PUT
        /// </summary>
        /// <param name="name">Name of the parameter</param>
        /// <param name="value">Value of the parameter</param>
        /// <returns></returns>
        public IRestRequest AddParameter(string name, object value)
        {
            restRequest.AddParameter(name, value);
            return this;
        }

        [IsVisibleInDynamoLibrary(false)]
        IRestRequest IRestRequest.AddParameter(string name, object value, ParameterType type)
        {
            restRequest.AddParameter(name, value, type);
            return this;
        }

        [IsVisibleInDynamoLibrary(false)]
        public IRestRequest AddParameter(string name, object value, string contentType, ParameterType type)
        {
            restRequest.AddParameter(name, value, contentType, type);
            return this;
        }

        /// <summary>
        /// Shortcut to AddParameter(name, value, HttpHeader)
        /// </summary>
        /// <param name="name">Name of the header to add</param>
        /// <param name="value">Value of the header to add</param>
        /// <returns></returns>
        public IRestRequest AddHeader(string name, string value)
        {
            restRequest.AddHeader(name, value);
            return this;
        }

        /// <summary>
        /// Shortcut to AddParameter(name, value, Cookie)
        /// </summary>
        /// <param name="name">Name of the cookie to add</param>
        /// <param name="value">Value of the cookie to add</param>
        /// <returns></returns>
        public IRestRequest AddCookie(string name, string value)
        {
            restRequest.AddCookie(name, value);
            return this;
        }

        /// <summary>
        /// Shortcut to AddParameter(name, value, UrlSegment)
        /// </summary>
        /// <param name="name">Name of the segment to add</param>
        /// <param name="value">Value of the segment to add</param>
        /// <returns></returns>
        public IRestRequest AddUrlSegment(string name, string value)
        {
            restRequest.AddUrlSegment(name, value);
            return this;
        }

        /// <summary>
        /// Shortcut to AddParameter(name, value, QueryString)
        /// </summary>
        /// <param name="name">Name of the parameter to add</param>
        /// <param name="value">Value of the parameter to add</param>
        /// <returns></returns>
        public IRestRequest AddQueryParameter(string name, string value)
        {
            restRequest.AddQueryParameter(name, value);
            return this;
        }

        [IsVisibleInDynamoLibrary(false)]
        public void IncreaseNumAttempts()
        {
            restRequest.IncreaseNumAttempts();
        }

        #endregion

        #region Execution

        /// <summary>
        /// Executes a WebRequest and returns the response from the server.
        /// </summary>
        /// <param name="request">The web request to execute.</param>
        /// <returns>The response from the server as a WebResponse object.</returns>
        public static WebResponse Execute(WebRequest request)
        {
            // build a client to execute the request, recording start & end time
            var startTime = DateTime.Now;
            var client = new RestClient(request.URL);
            var responseFromServer = client.Execute(request.restRequest);
            var endTime = DateTime.Now;

            // if a network error occured, the request never reached the recipient server
            // in that case, expose the error in the UI through an Exception
            if (responseFromServer.ResponseStatus == ResponseStatus.Error)
            {
                throw new InvalidOperationException(DynWWW.Properties.Resources.WebRequestExecutionNetworkErrorMessage);
            }

            // update the request properties with response data
            request.response = new WebResponse(responseFromServer);
            request.timeToComplete = endTime - startTime;

            return request.response;
        }

        /// <summary>
        /// Deserialises a web response to the type of a supplied object.
        /// Accepts JSON and XML as valid response contents.
        /// </summary>
        /// <typeparam name="T">The object type to deserialize to.</typeparam>
        /// <param name="response">The response from the server that needs to be deserialised.</param>
        /// <param name="obj">The object that will be used to determine what type to deserialise to.</param>
        /// <returns>The response deserialised as same type as supplied object.</returns>
        public T Deserialize<T>(WebResponse response, T obj) where T : new()
        {
            return DeserializeObject<T>(response);
        }

        /// <summary>
        /// Deserialises a web response the specified type.
        /// </summary>
        /// <typeparam name="T">The object type to deserialize to.</typeparam>
        /// <param name="response">The response from the server that needs to be deserialised.</param>
        /// <returns>The deserialised object.</returns>
        private T DeserializeObject<T>(WebResponse response) where T : new()
        {
            var responseData = response.Content;

            /// We don't want the deserialisation to break if some properties are empty.
            /// So we need to specify the behaviour when such values are encountered.
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.NullValueHandling = NullValueHandling.Ignore;
            settings.MissingMemberHandling = MissingMemberHandling.Ignore;

            return JsonConvert.DeserializeObject<T>(responseData, settings);
        }

        #endregion


    }
}
