﻿/*
 * Copyright (c) 2014-2023 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of WWCP Cloud <https://git.graphdefined.com/OpenChargingCloud/WWCP_Cloud>
 *
 * Licensed under the Affero GPL license, Version 3.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.gnu.org/licenses/agpl.html
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#region Usings

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;

#endregion

namespace cloud.charging.open.protocols.WWCP.EMP
{

    public class ProviderAPI : HTTPAPI
    {

        #region Data

        private                HTTPEventSource<JObject>  DebugLog;

        private const          String           HTTPRoot                            = "org.GraphDefined.WWCP.EMSP.HTTPRoot";


        /// <summary>
        /// The default HTTP server name.
        /// </summary>
        public const           String           DefaultHTTPServerName               = "GraphDefined Provider API HTTP Service v0.3";

        /// <summary>
        /// The default HTTP server TCP port.
        /// </summary>
        public static readonly IPPort           DefaultHTTPServerPort               = IPPort.Parse(3200);

        /// <summary>
        /// The default HTTP server URI prefix.
        /// </summary>
        public static readonly HTTPPath          DefaultURLPrefix                    = HTTPPath.Parse("/emsp");

        /// <summary>
        /// The default HTTP logfile.
        /// </summary>
        public const           String           DefaultLogfileName                  = "ProviderMap_HTTPAPI.log";


        public  const           String          HTTPLogin                           = "chargingmap";
        public  const           String          HTTPPassword                        = "gf0c31j08ufgw3j9w3t";

        public const            String          WWWAuthenticationRealm              = "Open Charging Cloud";

        public readonly static  HTTPMethod      RESERVE                             = HTTPMethod.Register("RESERVE",     IsSafe: false, IsIdempotent: true)!.Value;
        public readonly static  HTTPMethod      REMOTESTART                         = HTTPMethod.Register("REMOTESTART", IsSafe: false, IsIdempotent: true)!.Value;
        public readonly static  HTTPMethod      REMOTESTOP                          = HTTPMethod.Register("REMOTESTOP",  IsSafe: false, IsIdempotent: true)!.Value;

        #endregion

        #region Properties

        /// <summary>
        /// The attached e-mobility service provider.
        /// </summary>
        public EMobilityServiceProvider   EMSP            { get; }

        /// <summary>
        /// The HTTP server of the API.
        /// </summary>
        public HTTPServer                 HTTPServer      { get; }

        /// <summary>
        /// The HTTP hostname for all URIs within this API.
        /// </summary>
        public HTTPHostname               HTTPHostname    { get; }

        /// <summary>
        /// A common URI prefix for all URIs within this API.
        /// </summary>
        public HTTPPath                   URLPrefix       { get; }

        #endregion

        #region Events

        #region OnReserveEVSE

        /// <summary>
        /// An event sent whenever an EVSE reservation request was received.
        /// </summary>
        public event RequestLogHandler      OnReserveEVSELog;

        /// <summary>
        /// An event sent whenever an EVSE is reserved.
        /// </summary>
        public event OnReserveEVSEDelegate  OnReserveEVSE;

        /// <summary>
        /// An event sent whenever an EVSE reservation request response was sent.
        /// </summary>
        public event AccessLogHandler       OnEVSEReservedLog;

        #endregion

        #region OnCancelReservation

        /// <summary>
        /// An event sent whenever a reservation will be canceled by an EVSE operator.
        /// </summary>
        public event RequestLogHandler            OnReservationCancel;

        /// <summary>
        /// An event sent whenever a reservation will be canceled by an EVSE operator.
        /// </summary>
        public event OnCancelReservationDelegate  OnCancelReservation;

        /// <summary>
        /// An event sent whenever a reservation was canceled by an EVSE operator.
        /// </summary>
        public event AccessLogHandler             OnCancelReservationResponse;

        #endregion


        #region OnRemoteStartEVSE

        /// <summary>
        /// An event sent whenever a remote start EVSE request was received.
        /// </summary>
        public event RequestLogHandler      OnRemoteStartEVSELog;

        /// <summary>
        /// An event sent whenever an EVSE should start charging.
        /// </summary>
        public event OnRemoteStartDelegate  OnRemoteStartEVSE;

        /// <summary>
        /// An event sent whenever a remote start EVSE response was sent.
        /// </summary>
        public event AccessLogHandler       OnEVSERemoteStartedLog;

        #endregion

        #region OnRemoteStopEVSE

        /// <summary>
        /// An event sent whenever a remote stop EVSE request was received.
        /// </summary>
        public event RequestLogHandler     OnRemoteStopEVSELog;

        /// <summary>
        /// An event sent whenever an EVSE should stop charging.
        /// </summary>
        public event OnRemoteStopDelegate  OnRemoteStopEVSE;

        /// <summary>
        /// An event sent whenever a remote stop EVSE response was sent.
        /// </summary>
        public event AccessLogHandler      OnEVSERemoteStoppedLog;

        #endregion


        #region Generic HTTP/SOAP server logging

        /// <summary>
        /// An event called whenever a HTTP request came in.
        /// </summary>
        public HTTPRequestLogEvent   RequestLog    = new HTTPRequestLogEvent();

        /// <summary>
        /// An event called whenever a HTTP request could successfully be processed.
        /// </summary>
        public HTTPResponseLogEvent  ResponseLog   = new HTTPResponseLogEvent();

        /// <summary>
        /// An event called whenever a HTTP request resulted in an error.
        /// </summary>
        public HTTPErrorLogEvent     ErrorLog      = new HTTPErrorLogEvent();

        #endregion

        #endregion

        #region Constructor(s)

        #region ProviderAPI(HTTPServerName = DefaultHTTPServerName, ...)

        public ProviderAPI(EMobilityServiceProvider          EMSP,

                           String                            HTTPServerName                    = DefaultHTTPServerName,
                           IPPort?                           HTTPServerPort                    = null,
                           HTTPHostname?                     HTTPHostname                      = null,
                           HTTPPath?                         URLPrefix                         = null,

                           String?                           ServerThreadName                  = null,
                           ThreadPriority                    ServerThreadPriority              = ThreadPriority.AboveNormal,
                           Boolean                           ServerThreadIsBackground          = true,
                           ConnectionIdBuilder?              ConnectionIdBuilder               = null,
                           TimeSpan?                         ConnectionTimeout                 = null,
                           UInt32                            MaxClientConnections              = TCPServer.__DefaultMaxClientConnections,

                           DNSClient?                        DNSClient                         = null,
                           Boolean                           AutoStart                         = false)

            : this(EMSP,
                   new HTTPServer(HTTPPort:                  HTTPServerPort ?? DefaultHTTPServerPort,
                                  DefaultServerName:         HTTPServerName,
                                  ServerThreadName:          ServerThreadName,
                                  ServerThreadPriority:      ServerThreadPriority,
                                  ServerThreadIsBackground:  ServerThreadIsBackground,
                                  ConnectionIdBuilder:       ConnectionIdBuilder,
                                  ConnectionTimeout:         ConnectionTimeout,
                                  MaxClientConnections:      MaxClientConnections,
                                  DNSClient:                 DNSClient,
                                  AutoStart:                 false),
                   HTTPHostname,
                   URLPrefix ?? DefaultURLPrefix)

        {

            if (AutoStart)
                HTTPServer.Start();

        }

        #endregion

        #region (private) ProviderAPI(HTTPServer, HTTPHostname = "*", URLPrefix = "/", ...)

        private ProviderAPI(EMobilityServiceProvider  EMSP,
                            HTTPServer                HTTPServer,
                            HTTPHostname?             Hostname    = null,
                            HTTPPath?                 URLPrefix   = null)

            : base(HTTPServer:                  HTTPServer,
                   HTTPHostname:                Hostname,
                   ExternalDNSName:             null,
                   HTTPServiceName:             null,
                   BasePath:                    null,

                   URLPathPrefix:               URLPrefix,
                   HTMLTemplate:                null,
                   APIVersionHashes:            null,

                   DisableMaintenanceTasks:     null,
                   MaintenanceInitialDelay:     null,
                   MaintenanceEvery:            null,

                   DisableWardenTasks:          null,
                   WardenInitialDelay:          null,
                   WardenCheckEvery:            null,

                   IsDevelopment:               null,
                   DevelopmentServers:          null,
                   DisableLogging:              null,
                   LoggingPath:                 null,
                   LogfileName:                 null,
                   LogfileCreator:              null,
                   AutoStart:                   false)

        {

            this.EMSP          = EMSP       ?? throw new ArgumentNullException(nameof(EMSP),       "The given e-mobility service provider must not be null!");
            this.HTTPServer    = HTTPServer ?? throw new ArgumentNullException(nameof(HTTPServer), "The given HTTP server must not be null!");
            this.HTTPHostname  = Hostname   ?? HTTPHostname.Any;
            this.URLPrefix     = URLPrefix  ?? DefaultURLPrefix;

            // Link HTTP events...
            HTTPServer.RequestLog   += (HTTPProcessor, ServerTimestamp, Request)                                 => RequestLog. WhenAll(HTTPProcessor, ServerTimestamp, Request);
            HTTPServer.ResponseLog  += (HTTPProcessor, ServerTimestamp, Request, Response)                       => ResponseLog.WhenAll(HTTPProcessor, ServerTimestamp, Request, Response);
            HTTPServer.ErrorLog     += (HTTPProcessor, ServerTimestamp, Request, Response, Error, LastException) => ErrorLog.   WhenAll(HTTPProcessor, ServerTimestamp, Request, Response, Error, LastException);

            RegisterURLTemplates();

        }

        #endregion

        #endregion


        #region (static) AttachToHTTPAPI(HTTPServer, HTTPHostname = "*", URLPrefix = "/", ...)

        /// <summary>
        /// Attach this HTTP API to the given HTTP server.
        /// </summary>
        public static ProviderAPI AttachToHTTPAPI(EMobilityServiceProvider  EMSP,
                                                  HTTPServer                HTTPServer,
                                                  HTTPHostname?             Hostname   = null,
                                                  HTTPPath?                 URLPrefix  = null)

            => new (EMSP,
                    HTTPServer,
                    Hostname,
                    URLPrefix);

        #endregion

        #region (private) RegisterURLTemplates()

        private void RegisterURLTemplates()
        {

            //DebugLog  = HTTPServer.AddJSONEventSource(EventIdentification:      HTTPEventSource_Id.Parse("DebugLog"),
            //                                          MaxNumberOfCachedEvents:  50000,
            //                                          RetryIntervall:           TimeSpan.FromSeconds(5),
            //                                          URLTemplate:              URLPrefix + "/DebugLog");

            #region / (HTTPRoot)

            //HTTPServer.RegisterResourcesFolder(HTTPHostname,
            //                                   URLPrefix,
            //                                   HTTPRoot,
            //                                   DefaultFilename: "index.html");

            #endregion


            #region RESERVE      ~/EVSEs/{EVSEId}

            #region Documentation

            // RESERVE ~/ChargingStations/DE*822*S123456789  // optional
            // RESERVE ~/EVSEs/DE*822*E123456789*1
            // 
            // {
            //     "ReservationId":      "5c24515b-0a88-1296-32ea-1226ce8a3cd0",                   // optional
            //     "StartTime":          "2015-10-20T11:25:43.511Z",                               // optional; default: current timestamp
            //     "Duration":           3600,                                                     // optional; default: 900 [seconds]
            //     "IntendedCharging":   {                                                         // optional; (good for energy management)
            //                               "StartTime":          "2015-10-20T11:30:00.000Z",     // optional; default: reservation start time
            //                               "Duration":           1800,                           // optional; default: reservation duration [seconds]
            //                               "ChargingProductId":  "AC1"                           // optional; default: Default product
            //                               "Plug":               "TypeFSchuko|Type2Outlet|...",  // optional;
            //                               "Consumption":        20,                             // optional; [kWh]
            //                               "ChargePlan":         "fastest"                       // optional;
            //                           },
            //     "AuthorizedIds":      {                                                         // optional; List of authentication methods...
            //                               "AuthTokens",  ["012345ABCDEF", ...],                    // optional; List of RFID Ids
            //                               "eMAIds",   ["DE*ICE*I00811*1", ...],                 // optional; List of eMA Ids
            //                               "PINs",     ["123456", ...],                          // optional; List of keypad Pins
            //                               "Liste",    [...]                                     // optional; List of known (white-)lists
            //                           }
            // }

            #endregion

            // -----------------------------------------------------------------------
            // curl -v -X RESERVE -H "Content-Type: application/json" \
            //                    -H "Accept:       application/json"  \
            //      -d "{ \"eMAId\":         \"DE*BSI*I00811*1\", \
            //            \"StartTime\":     \"2015-10-20T11:25:43.511Z\", \
            //            \"Duration\":        3600, \
            //            \"IntendedCharging\": { \
            //                                 \"Consumption\": 20, \
            //                                 \"Plug\":        \"TypeFSchuko\" \
            //                               }, \
            //            \"AuthorizedIds\": { \
            //                                 \"AuthTokens\": [\"1AA234BB\", \"012345ABCDEF\"], \
            //                                 \"eMAIds\":  [\"DE*ICE*I00811*1\"], \
            //                                 \"PINs\":    [\"1234\", \"6789\"] \
            //                               } \
            //          }" \
            //      http://127.0.0.1:3004/RNs/Prod/EVSEs/49*822*066268034*1
            // -----------------------------------------------------------------------
            AddMethodCallback(HTTPHostname,
                                         RESERVE,
                                         URLPrefix + "/EVSEs/{EVSEId}",
                                         HTTPContentType.JSON_UTF8,
                                         HTTPDelegate: async Request => {

                                             SendReserveEVSE(Request);

                                             #region Check HTTP Basic Authentication

                                             if (Request.Authorization          == null        ||
                                                (Request.Authorization as HTTPBasicAuthentication).Username != HTTPLogin   ||
                                                (Request.Authorization as HTTPBasicAuthentication).Password != HTTPPassword)
                                             {

                                                 return SendEVSERemoteStarted(
                                                     new HTTPResponse.Builder(Request) {
                                                         HTTPStatusCode   = HTTPStatusCode.Unauthorized,
                                                         WWWAuthenticate  = @"Basic realm=""" + WWWAuthenticationRealm + @"""",
                                                         Server           = HTTPServer.DefaultServerName,
                                                         Date             = DateTime.Now,
                                                         Connection       = "close"
                                                     });

                                             }

                                             #endregion

                                             #region Get EVSEId URI parameter

                                             if (!Request.ParseEVSEId(DefaultHTTPServerName,
                                                                      out var evseId,
                                                                      out var httpResponse))
                                             {
                                                 return SendEVSERemoteStarted(httpResponse);
                                             }

                                             #endregion

                                             #region Parse JSON  [mandatory]

                                             DateTime?                StartTime          = null;
                                             TimeSpan?                Duration           = null;
                                             EMobilityAccount_Id      eMAId              = default;
                                             ChargingReservation_Id?  ReservationId      = null;

                                             // IntendedCharging
                                             DateTime?                ChargingStartTime  = null;
                                             TimeSpan?                CharingDuration    = null;
                                             ChargingProduct_Id?      ChargingProductId  = null;
                                             ChargingPlugTypes?       Plug               = null;
                                             var                      Consumption        = 0U;

                                             // AuthorizedIds
                                             var                      AuthTokens         = new List<AuthenticationToken>();
                                             var                      eMAIds             = new List<EMobilityAccount_Id>();
                                             var                      PINs               = new List<UInt32>();

                                             if (Request.TryParseJObjectRequestBody(out var JSON,
                                                                                    out httpResponse,
                                                                                    AllowEmptyHTTPBody: true))
                                             {

                                                 #region Check StartTime            [optional]

                                                 if (JSON.ParseOptional("StartTime",
                                                                        "Reservation start time",
                                                                        HTTPServer.DefaultHTTPServerName,
                                                                        out StartTime,
                                                                        Request,
                                                                        out httpResponse))
                                                 {

                                                     if (httpResponse != null)
                                                        return SendEVSEReserved(httpResponse);

                                                     if (StartTime <= DateTime.Now)
                                                         return SendEVSEReserved(
                                                             new HTTPResponse.Builder(Request) {
                                                                 HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                                                 ContentType     = HTTPContentType.JSON_UTF8,
                                                                 Content         = new JObject(new JProperty("description", "The starting time must be in the future!")).ToUTF8Bytes()
                                                             });

                                                 }

                                                 #endregion

                                                 #region Check Duration             [optional]

                                                 if (JSON.ParseOptional("Duration",
                                                                        "Reservation duration",
                                                                        HTTPServer.DefaultHTTPServerName,
                                                                        out Duration,
                                                                        Request,
                                                                        out httpResponse))
                                                 {

                                                     if (httpResponse != null)
                                                         return SendEVSEReserved(httpResponse);

                                                 }

                                                 #endregion

                                                 #region Check ReservationId        [optional]

                                                 if (JSON.ParseOptionalStruct2("ReservationId",
                                                                              "Charging reservation identification",
                                                                              HTTPServer.DefaultServerName,
                                                                              ChargingReservation_Id.TryParse,
                                                                              out ReservationId,
                                                                              Request,
                                                                              out httpResponse))
                                                 {

                                                     if (httpResponse != null)
                                                         return SendEVSEReserved(httpResponse);

                                                 }

                                                 #endregion

                                                 #region Parse eMAId                [mandatory]

                                                 if (!JSON.ParseMandatory("eMAId",
                                                                          "e-Mobility account identification",
                                                                          HTTPServer.DefaultServerName,
                                                                          EMobilityAccount_Id.TryParse,
                                                                          out eMAId,
                                                                          Request,
                                                                          out httpResponse))

                                                     return SendEVSEReserved(httpResponse);

                                                 #endregion


                                                 #region Check IntendedCharging     [optional] -> ...

                                                 if (JSON.ParseOptional("IntendedCharging",
                                                                        "IntendedCharging",
                                                                        HTTPServer.DefaultServerName,
                                                                        out JObject IntendedChargingJSON,
                                                                        Request,
                                                                        out httpResponse))
                                                 {

                                                     if (httpResponse != null)
                                                         return SendEVSEReserved(httpResponse);

                                                     #region Check ChargingStartTime    [optional]

                                                     if (IntendedChargingJSON.ParseOptional("StartTime",
                                                                                            "IntendedCharging/StartTime",
                                                                                            HTTPServer.DefaultServerName,
                                                                                            out ChargingStartTime,
                                                                                            Request,
                                                                                            out httpResponse))
                                                     {

                                                         if (httpResponse != null)
                                                             return SendEVSEReserved(httpResponse);

                                                     }

                                                     #endregion

                                                     #region Check Duration             [optional]

                                                     if (IntendedChargingJSON.ParseOptional("Duration",
                                                                                            "IntendedCharging/Duration",
                                                                                            HTTPServer.DefaultServerName,
                                                                                            out CharingDuration,
                                                                                            Request,
                                                                                            out httpResponse))
                                                     {

                                                         if (httpResponse != null)
                                                             return SendEVSEReserved(httpResponse);

                                                     }

                                                     #endregion

                                                     #region Check ChargingProductId    [optional]

                                                     if (JSON.ParseOptionalStruct2("ChargingProductId",
                                                                                  "Charging product identification",
                                                                                  HTTPServer.DefaultServerName,
                                                                                  ChargingProduct_Id.TryParse,
                                                                                  out ChargingProductId,
                                                                                  Request,
                                                                                  out httpResponse))
                                                     {

                                                         if (httpResponse != null)
                                                             return SendEVSEReserved(httpResponse);

                                                     }

                                                     #endregion

                                                     #region Check Plug                 [optional]

                                                     if (IntendedChargingJSON.ParseOptional("Plug",
                                                                                            "IntendedCharging/Plug",
                                                                                            HTTPServer.DefaultServerName,
                                                                                            out Plug,
                                                                                            Request,
                                                                                            out httpResponse))
                                                     {

                                                         if (httpResponse != null)
                                                             return SendEVSEReserved(httpResponse);

                                                     }

                                                     #endregion

                                                     #region Check Consumption          [optional, kWh]

                                                     if (IntendedChargingJSON.ParseOptional("Consumption",
                                                                                            "IntendedCharging/Consumption",
                                                                                            HTTPServer.DefaultServerName,
                                                                                            UInt32.Parse,
                                                                                            out Consumption,
                                                                                            Request,
                                                                                            out httpResponse))
                                                     {

                                                         if (httpResponse != null)
                                                             return SendEVSEReserved(httpResponse);

                                                     }

                                                     #endregion

                                                 }

                                                 #endregion

                                                 #region Check AuthorizedIds        [optional] -> ...

                                                 if (JSON.ParseOptional("AuthorizedIds",
                                                                        "AuthorizedIds",
                                                                        HTTPServer.DefaultServerName,
                                                                        out JObject AuthorizedIdsJSON,
                                                                        Request,
                                                                        out httpResponse))
                                                 {

                                                     #region Check RFIDIds      [optional]

                                                     if (AuthorizedIdsJSON.ParseOptional("RFIDIds",
                                                                                         "RFIDIds",
                                                                                         HTTPServer.DefaultServerName,
                                                                                         out JArray AuthTokensJSON,
                                                                                         Request,
                                                                                         out httpResponse))
                                                     {

                                                         foreach (var jtoken in AuthTokensJSON)
                                                         {

                                                             if (!AuthenticationToken.TryParse(jtoken.Value<String>(), out AuthenticationToken AuthToken))
                                                                 return SendEVSEReserved(
                                                                     new HTTPResponse.Builder(Request) {
                                                                         HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                                                         ContentType     = HTTPContentType.JSON_UTF8,
                                                                         Content         = new JObject(new JProperty("description", "Invalid AuthorizedIds/RFIDId '" + jtoken.Value<String>() + "' section!")).ToUTF8Bytes()
                                                                     });

                                                             AuthTokens.Add(AuthToken);

                                                         }

                                                     }

                                                     #endregion

                                                     #region Check eMAIds       [optional]

                                                     if (AuthorizedIdsJSON.ParseOptional("eMAIds",
                                                                                         "AuthorizedIds/eMAIds",
                                                                                         HTTPServer.DefaultServerName,
                                                                                         out JArray eMAIdsJSON,
                                                                                         Request,
                                                                                         out httpResponse))
                                                     {

                                                         if (httpResponse != null)
                                                             return SendEVSEReserved(httpResponse);

                                                         foreach (var jtoken in eMAIdsJSON)
                                                         {

                                                             if (!EMobilityAccount_Id.TryParse(jtoken.Value<String>(), out var eMAId2))
                                                                 return SendEVSEReserved(
                                                                     new HTTPResponse.Builder(Request) {
                                                                         HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                                                         ContentType     = HTTPContentType.JSON_UTF8,
                                                                         Content         = new JObject(new JProperty("description", "Invalid AuthorizedIds/eMAIds '" + jtoken.Value<String>() + "' section!")).ToUTF8Bytes()
                                                                     });

                                                             eMAIds.Add(eMAId2);

                                                         }

                                                     }

                                                     #endregion

                                                     #region Check PINs         [optional]

                                                     //if (AuthorizedIdsJSON.TryGetValue("PINs", out JSONToken))
                                                     //{

                                                     //    var PINsJSON = JSONToken as JArray;

                                                     //    if (PINsJSON == null)
                                                     //        return SendEVSEReserved(
                                                     //            new HTTPResponse.Builder(Request) {
                                                     //                HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                                     //                ContentType     = HTTPContentType.JSON_UTF8,
                                                     //                Content         = new JObject(new JProperty("description", "Invalid AuthorizedIds/PINs section!")).ToUTF8Bytes()
                                                     //            });

                                                     //    foreach (var jtoken in PINsJSON)
                                                     //    {

                                                     //        UInt32 PIN = 0;

                                                     //        if (!UInt32.TryParse(jtoken.Value<String>(), out PIN))
                                                     //            return SendEVSEReserved(
                                                     //                new HTTPResponse.Builder(Request) {
                                                     //                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                                     //                    ContentType     = HTTPContentType.JSON_UTF8,
                                                     //                    Content         = new JObject(new JProperty("description", "Invalid AuthorizedIds/PINs '" + jtoken.Value<String>() + "' section!")).ToUTF8Bytes()
                                                     //                });

                                                     //        PINs.Add(PIN);

                                                     //    }

                                                     //}

                                                     #endregion

                                                 }

                                                 #endregion

                                             }

                                             #endregion


                                             var result = await EMSP.Reserve(ChargingLocation.FromEVSEId(evseId),
                                                                             ChargingReservationLevel.EVSE,
                                                                             StartTime,
                                                                             Duration,
                                                                             ReservationId,
                                                                             null,
                                                                             null,
                                                                             RemoteAuthentication.FromRemoteIdentification(eMAId),
                                                                             ChargingProductId.HasValue    // of IntendedCharging
                                                                                 ? new ChargingProduct(ChargingProductId.Value)
                                                                                 : null,
                                                                             AuthTokens,
                                                                             eMAIds,
                                                                             PINs,

                                                                             Request.Timestamp,
                                                                             Request.EventTrackingId,
                                                                             Request.Timeout,
                                                                             Request.CancellationToken);


                                             switch (result.Result)
                                             {

                                                 #region Success

                                                 case ReservationResultType.Success:
                                                     return SendEVSEReserved(
                                                         new HTTPResponse.Builder(Request) {
                                                             HTTPStatusCode             = HTTPStatusCode.Created,
                                                             Server                     = HTTPServer.DefaultServerName,
                                                             Date                       = DateTime.Now,
                                                             AccessControlAllowOrigin   = "*",
                                                             AccessControlAllowMethods  = new[] { "POST" },
                                                             AccessControlAllowHeaders  = new[] { "Content-Type", "Accept", "Authorization" },
                                                             Location                   = Location.Parse("~/ext/BoschEBike/Reservations/" + result.Reservation.Id.ToString()),
                                                             Connection                 = "close",
                                                             ContentType                = HTTPContentType.JSON_UTF8,
                                                             Content                    = JSONObject.Create(
                                                                                              new JProperty("ReservationId",           result.Reservation.Id.       ToString()),
                                                                                              new JProperty("StartTime",               result.Reservation.StartTime.ToIso8601()),
                                                                                              new JProperty("Duration",       (UInt32) result.Reservation.Duration. TotalSeconds),
                                                                                              //new JProperty("Level",                   result.Reservation.ReservationLevel.ToString()),
                                                                                              //new JProperty("EVSEId",                  result.Reservation.EVSEId.   ToString()),

                                                                                              (result.Reservation.AuthTokens.Any() ||
                                                                                               result.Reservation.eMAIds.    Any() ||
                                                                                               result.Reservation.PINs.      Any())
                                                                                                   ? new JProperty("AuthorizedIds", JSONObject.Create(

                                                                                                         result.Reservation.AuthTokens.Any()
                                                                                                             ? new JProperty("RFIDIds", new JArray(result.Reservation.AuthTokens.Select(v => v.ToString())))
                                                                                                             : null,

                                                                                                         result.Reservation.eMAIds.Any()
                                                                                                             ? new JProperty("eMAIds",  new JArray(result.Reservation.eMAIds. Select(v => v.ToString())))
                                                                                                             : null,

                                                                                                         result.Reservation.PINs.Any()
                                                                                                             ? new JProperty("PINs",    new JArray(result.Reservation.PINs.   Select(v => v.ToString())))
                                                                                                             : null))

                                                                                                   : null

                                                                                          ).ToUTF8Bytes()
                                                     });

                                                 #endregion

                                                 #region InvalidCredentials

                                                 case ReservationResultType.InvalidCredentials:
                                                     return SendEVSEReserved(
                                                         new HTTPResponse.Builder(Request) {
                                                             HTTPStatusCode             = HTTPStatusCode.Unauthorized,
                                                             Server                     = HTTPServer.DefaultServerName,
                                                             Date                       = DateTime.Now,
                                                             AccessControlAllowOrigin   = "*",
                                                             AccessControlAllowMethods  = new[] { "POST" },
                                                             AccessControlAllowHeaders  = new[] { "Content-Type", "Accept", "Authorization" },
                                                             ContentType                = HTTPContentType.JSON_UTF8,
                                                             Content                    = JSONObject.Create(
                                                                                              new JProperty("description",  "Unauthorized remote start or invalid credentials!")
                                                                                          ).ToUTF8Bytes()
                                                         });

                                                 #endregion

                                                 #region UnknownChargingReservationId

                                                 case ReservationResultType.UnknownChargingReservationId:
                                                     return SendEVSEReserved(
                                                         new HTTPResponse.Builder(Request) {
                                                             HTTPStatusCode             = HTTPStatusCode.NotFound,
                                                             Server                     = HTTPServer.DefaultServerName,
                                                             Date                       = DateTime.Now,
                                                             AccessControlAllowOrigin   = "*",
                                                             AccessControlAllowMethods  = new[] { "POST" },
                                                             AccessControlAllowHeaders  = new[] { "Content-Type", "Accept", "Authorization" },
                                                             ContentType                = HTTPContentType.JSON_UTF8,
                                                             Content                    = JSONObject.Create(
                                                                                              new JProperty("description",  "Unknown reservation identification!")
                                                                                          ).ToUTF8Bytes(),
                                                             Connection                 = "close"
                                                         });

                                                 #endregion

                                                 #region UnknownEVSE

                                                 case ReservationResultType.UnknownLocation:
                                                     return SendEVSEReserved(
                                                         new HTTPResponse.Builder(Request) {
                                                             HTTPStatusCode             = HTTPStatusCode.NotFound,
                                                             Server                     = HTTPServer.DefaultServerName,
                                                             Date                       = DateTime.Now,
                                                             AccessControlAllowOrigin   = "*",
                                                             AccessControlAllowMethods  = new[] { "POST" },
                                                             AccessControlAllowHeaders  = new[] { "Content-Type", "Accept", "Authorization" },
                                                             ContentType                = HTTPContentType.JSON_UTF8,
                                                             Content                    = JSONObject.Create(
                                                                                              new JProperty("description",  "Unknown EVSE!")
                                                                                          ).ToUTF8Bytes(),
                                                             Connection                 = "close"
                                                         });

                                                 #endregion

                                                 #region AlreadyReserved

                                                 case ReservationResultType.AlreadyReserved:
                                                     return SendEVSEReserved(
                                                         new HTTPResponse.Builder(Request) {
                                                             HTTPStatusCode             = HTTPStatusCode.Conflict,
                                                             Server                     = HTTPServer.DefaultServerName,
                                                             Date                       = DateTime.Now,
                                                             AccessControlAllowOrigin   = "*",
                                                             AccessControlAllowMethods  = new[] { "POST" },
                                                             AccessControlAllowHeaders  = new[] { "Content-Type", "Accept", "Authorization" },
                                                             ContentType                = HTTPContentType.JSON_UTF8,
                                                             Content                    = JSONObject.Create(
                                                                                              new JProperty("description",  "The EVSE is already reserved!")
                                                                                          ).ToUTF8Bytes(),
                                                             Connection                 = "close"
                                                         });

                                                 #endregion

                                                 #region AlreadyInUse

                                                 case ReservationResultType.AlreadyInUse:
                                                     return SendEVSEReserved(
                                                         new HTTPResponse.Builder(Request) {
                                                             HTTPStatusCode             = HTTPStatusCode.Conflict,
                                                             Server                     = HTTPServer.DefaultServerName,
                                                             Date                       = DateTime.Now,
                                                             AccessControlAllowOrigin   = "*",
                                                             AccessControlAllowMethods  = new[] { "POST" },
                                                             AccessControlAllowHeaders  = new[] { "Content-Type", "Accept", "Authorization" },
                                                             ContentType                = HTTPContentType.JSON_UTF8,
                                                             Content                    = JSONObject.Create(
                                                                                              new JProperty("description",  "The EVSE is already in use!")
                                                                                          ).ToUTF8Bytes(),
                                                             Connection                 = "close"
                                                         });

                                                 #endregion

                                                 #region OutOfService

                                                 case ReservationResultType.OutOfService:
                                                     return SendEVSEReserved(
                                                         new HTTPResponse.Builder(Request) {
                                                             HTTPStatusCode             = HTTPStatusCode.Conflict,
                                                             Server                     = HTTPServer.DefaultServerName,
                                                             Date                       = DateTime.Now,
                                                             AccessControlAllowOrigin   = "*",
                                                             AccessControlAllowMethods  = new[] { "POST" },
                                                             AccessControlAllowHeaders  = new[] { "Content-Type", "Accept", "Authorization" },
                                                             ContentType                = HTTPContentType.JSON_UTF8,
                                                             Content                    = JSONObject.Create(
                                                                                              new JProperty("description",  "The EVSE is out of service!")
                                                                                          ).ToUTF8Bytes(),
                                                             Connection                 = "close"
                                                         });

                                                 #endregion

                                                 #region Timeout

                                                 case ReservationResultType.Timeout:
                                                     return SendEVSEReserved(
                                                         new HTTPResponse.Builder(Request) {
                                                             HTTPStatusCode             = HTTPStatusCode.RequestTimeout,
                                                             Server                     = HTTPServer.DefaultServerName,
                                                             Date                       = DateTime.Now,
                                                             AccessControlAllowOrigin   = "*",
                                                             AccessControlAllowMethods  = new[] { "POST" },
                                                             AccessControlAllowHeaders  = new[] { "Content-Type", "Accept", "Authorization" },
                                                             ContentType                = HTTPContentType.JSON_UTF8,
                                                             Content                    = JSONObject.Create(
                                                                                              new JProperty("description",  "The request did not succeed within the given time!")
                                                                                          ).ToUTF8Bytes(),
                                                             Connection                 = "close"
                                                         });

                                                 #endregion

                                                 #region default => BadRequest

                                                 default:
                                                     return SendEVSEReserved(
                                                         new HTTPResponse.Builder(Request) {
                                                             HTTPStatusCode             = HTTPStatusCode.BadRequest,
                                                             Server                     = HTTPServer.DefaultServerName,
                                                             Date                       = DateTime.Now,
                                                             AccessControlAllowOrigin   = "*",
                                                             AccessControlAllowMethods  = new[] { "POST" },
                                                             AccessControlAllowHeaders  = new[] { "Content-Type", "Accept", "Authorization" },
                                                             ContentType                = HTTPContentType.JSON_UTF8,
                                                             Content                    = JSONObject.Create(
                                                                                              new JProperty("description",  "No reservation was possible!")
                                                                                          ).ToUTF8Bytes(),
                                                             Connection                 = "close"
                                                         });

                                                 #endregion

                                             }

                                         });

            #endregion

            #region REMOTESTART  ~/EVSEs/{EVSEId}

            // -----------------------------------------------------------------------
            // curl -v -k \
            //      -X REMOTESTART \
            //      -u chargingmap:gf0c31j08ufgw3j9w3t \
            //      -H "Content-Type: application/json" \
            //      -H "Accept:       application/json" \
            //      -d "{ \"eMAId\": \"DE*ICE*I00811*1\" }" \
            //      http://127.0.0.1:3004/RNs/Prod/EVSEs/DE*822*555555*100*1
            // -----------------------------------------------------------------------
            AddMethodCallback(HTTPHostname,
                              REMOTESTART,
                              URLPrefix + "/EVSEs/{EVSEId}",
                              HTTPContentType.JSON_UTF8,
                              HTTPDelegate: async Request => {

                                  SendRemoteStartEVSE(Request);

                                  #region Check HTTP Basic Authentication

                                  if (Request.Authorization          == null        ||
                                     (Request.Authorization as HTTPBasicAuthentication).Username != HTTPLogin   ||
                                     (Request.Authorization as HTTPBasicAuthentication).Password != HTTPPassword)
                                  {

                                      return SendEVSERemoteStarted(
                                          new HTTPResponse.Builder(Request) {
                                              HTTPStatusCode   = HTTPStatusCode.Unauthorized,
                                              WWWAuthenticate  = @"Basic realm=""" + WWWAuthenticationRealm + @"""",
                                              Server           = HTTPServer.DefaultServerName,
                                              Date             = DateTime.Now,
                                              Connection       = "close"
                                          });

                                  }

                                  #endregion

                                  #region Get EVSEId URI parameter

                                  if (!Request.ParseEVSEId(DefaultHTTPServerName,
                                                           out var evseId,
                                                           out var httpResponse))
                                  {
                                      return SendEVSERemoteStarted(httpResponse);
                                  }

                                  #endregion

                                  #region Parse JSON  [mandatory]

                                  ChargingProduct_Id?      ChargingProductId  = null;
                                  ChargingReservation_Id?  ReservationId      = null;
                                  ChargingSession_Id?      SessionId          = default;
                                  EMobilityAccount_Id      eMAId;

                                  if (!Request.TryParseJObjectRequestBody(out var JSON,
                                                                          out httpResponse,
                                                                          AllowEmptyHTTPBody: false))

                                  {

                                      #region Check ChargingProductId  [optional]

                                      if (!JSON.ParseOptionalStruct2("ChargingProductId",
                                                                    "Charging product identification",
                                                                    HTTPServer.DefaultServerName,
                                                                    ChargingProduct_Id.TryParse,
                                                                    out ChargingProductId,
                                                                    Request,
                                                                    out httpResponse))
                                      {
                                          return SendEVSERemoteStarted(httpResponse);
                                      }

                                      #endregion

                                      #region Check ReservationId      [optional]

                                      if (!JSON.ParseOptionalStruct2("ReservationId",
                                                                    "Charging reservation identification",
                                                                    HTTPServer.DefaultServerName,
                                                                    ChargingReservation_Id.TryParse,
                                                                    out ReservationId,
                                                                    Request,
                                                                    out httpResponse))
                                      {
                                          return SendEVSERemoteStarted(httpResponse);
                                      }

                                      #endregion

                                      #region Parse SessionId          [optional]

                                      if (!JSON.ParseOptional("SessionId",
                                                              "Charging session identification",
                                                              HTTPServer.DefaultServerName,
                                                              ChargingSession_Id.TryParse,
                                                              out SessionId,
                                                              Request,
                                                              out httpResponse))

                                          return SendEVSERemoteStarted(httpResponse);

                                      #endregion

                                      #region Parse eMAId              [mandatory]

                                      if (!JSON.ParseMandatory("eMAId",
                                                               "e-Mobility account identification",
                                                               HTTPServer.DefaultServerName,
                                                               EMobilityAccount_Id.TryParse,
                                                               out eMAId,
                                                               Request,
                                                               out httpResponse))

                                          return SendEVSERemoteStarted(httpResponse);

                                      #endregion

                                  }

                                  else
                                      return SendEVSERemoteStarted(httpResponse);

                                  #endregion


                                  var response = await EMSP.RemoteStart(ChargingLocation.FromEVSEId(evseId),
                                                                        ChargingProductId.HasValue
                                                                            ? new ChargingProduct(ChargingProductId.Value)
                                                                            : null,
                                                                        ReservationId,
                                                                        SessionId,
                                                                        null,
                                                                        RemoteAuthentication.FromRemoteIdentification(eMAId),

                                                                        Request.Timestamp,
                                                                        Request.EventTrackingId,
                                                                        Request.Timeout,
                                                                        Request.CancellationToken);


                                  switch (response.Result)
                                  {

                                      #region Success

                                      case RemoteStartResultTypes.Success:
                                          return SendEVSERemoteStarted(
                                              new HTTPResponse.Builder(Request) {
                                                  HTTPStatusCode             = HTTPStatusCode.Created,
                                                  Server                     = HTTPServer.DefaultServerName,
                                                  Date                       = DateTime.Now,
                                                  AccessControlAllowOrigin   = "*",
                                                  AccessControlAllowMethods  = new[] { "POST" },
                                                  AccessControlAllowHeaders  = new[] { "Content-Type", "Accept", "Authorization" },
                                                  ContentType                = HTTPContentType.JSON_UTF8,
                                                  Content                    = JSONObject.Create(
                                                                                   new JProperty("SessionId",  response?.Session?.Id.ToString())
                                                                               ).ToUTF8Bytes()
                                              });

                                      #endregion

                                      #region InvalidCredentials

                                      case RemoteStartResultTypes.InvalidCredentials:
                                          return SendEVSERemoteStarted(
                                              new HTTPResponse.Builder(Request) {
                                                  HTTPStatusCode             = HTTPStatusCode.Unauthorized,
                                                  Server                     = HTTPServer.DefaultServerName,
                                                  Date                       = DateTime.Now,
                                                  AccessControlAllowOrigin   = "*",
                                                  AccessControlAllowMethods  = new[] { "POST" },
                                                  AccessControlAllowHeaders  = new[] { "Content-Type", "Accept", "Authorization" },
                                                  ContentType                = HTTPContentType.JSON_UTF8,
                                                  Content                    = JSONObject.Create(
                                                                                   new JProperty("description",  "Unauthorized remote start or invalid credentials!")
                                                                               ).ToUTF8Bytes()
                                              });

                                      #endregion

                                      #region AlreadyInUse

                                      case RemoteStartResultTypes.AlreadyInUse:
                                          return SendEVSERemoteStarted(
                                              new HTTPResponse.Builder(Request) {
                                                  HTTPStatusCode             = HTTPStatusCode.Conflict,
                                                  Server                     = HTTPServer.DefaultServerName,
                                                  Date                       = DateTime.Now,
                                                  AccessControlAllowOrigin   = "*",
                                                  AccessControlAllowMethods  = new[] { "POST" },
                                                  AccessControlAllowHeaders  = new[] { "Content-Type", "Accept", "Authorization" },
                                                  ContentType                = HTTPContentType.JSON_UTF8,
                                                  Content                    = JSONObject.Create(
                                                                                   new JProperty("description",  "The EVSE is already in use!")
                                                                               ).ToUTF8Bytes()
                                              });

                                      #endregion

                                      #region Reserved

                                      case RemoteStartResultTypes.Reserved:
                                          return SendEVSERemoteStarted(
                                              new HTTPResponse.Builder(Request) {
                                                  HTTPStatusCode             = HTTPStatusCode.Conflict,
                                                  Server                     = HTTPServer.DefaultServerName,
                                                  Date                       = DateTime.Now,
                                                  AccessControlAllowOrigin   = "*",
                                                  AccessControlAllowMethods  = new[] { "POST" },
                                                  AccessControlAllowHeaders  = new[] { "Content-Type", "Accept", "Authorization" },
                                                  ContentType                = HTTPContentType.JSON_UTF8,
                                                  Content                    = JSONObject.Create(
                                                                                   new JProperty("description", response.Description.IsNeitherNullNorEmpty() ? response.Description : I18NString.Create(Languages.en, "The EVSE is reserved!"))
                                                                               ).ToUTF8Bytes()
                                              });

                                      #endregion

                                      #region OutOfService

                                      case RemoteStartResultTypes.OutOfService:
                                          return SendEVSERemoteStarted(
                                              new HTTPResponse.Builder(Request) {
                                                  HTTPStatusCode             = HTTPStatusCode.Conflict,
                                                  Server                     = HTTPServer.DefaultServerName,
                                                  Date                       = DateTime.Now,
                                                  AccessControlAllowOrigin   = "*",
                                                  AccessControlAllowMethods  = new[] { "POST" },
                                                  AccessControlAllowHeaders  = new[] { "Content-Type", "Accept", "Authorization" },
                                                  ContentType                = HTTPContentType.JSON_UTF8,
                                                  Content                    = JSONObject.Create(
                                                                                   new JProperty("description",  "The EVSE is out of service!")
                                                                               ).ToUTF8Bytes()
                                              });

                                      #endregion

                                      #region Timeout

                                      case RemoteStartResultTypes.Timeout:
                                          return SendEVSERemoteStarted(
                                              new HTTPResponse.Builder(Request) {
                                                  HTTPStatusCode             = HTTPStatusCode.RequestTimeout,
                                                  Server                     = HTTPServer.DefaultServerName,
                                                  Date                       = DateTime.Now,
                                                  AccessControlAllowOrigin   = "*",
                                                  AccessControlAllowMethods  = new[] { "POST" },
                                                  AccessControlAllowHeaders  = new[] { "Content-Type", "Accept", "Authorization" },
                                                  ContentType                = HTTPContentType.JSON_UTF8,
                                                  Content                    = JSONObject.Create(
                                                                                   new JProperty("description",  "The request did not succeed within the given period of time!")
                                                                               ).ToUTF8Bytes()
                                              });

                                      #endregion

                                      #region default => BadRequest

                                      default:
                                          return SendEVSERemoteStarted(
                                              new HTTPResponse.Builder(Request) {
                                                  HTTPStatusCode             = HTTPStatusCode.BadRequest,
                                                  Server                     = HTTPServer.DefaultServerName,
                                                  Date                       = DateTime.Now,
                                                  AccessControlAllowOrigin   = "*",
                                                  AccessControlAllowMethods  = new[] { "POST" },
                                                  AccessControlAllowHeaders  = new[] { "Content-Type", "Accept", "Authorization" },
                                                  ContentType                = HTTPContentType.JSON_UTF8,
                                                  Content                    = JSONObject.Create(
                                                                                   response.Session != null
                                                                                       ? new JProperty("SessionId",  response.Session.Id.ToString())
                                                                                       : null,
                                                                                   new JProperty("Result",      response.Result.ToString()),
                                                                                   new JProperty("description", response.Description.IsNeitherNullNorEmpty() ? response.Description : I18NString.Create(Languages.en, "General error!"))
                                                                               ).ToUTF8Bytes()
                                              });

                                      #endregion

                                  }

                              });

            #endregion

            #region REMOTESTOP   ~/EVSEs/{EVSEId}

            // -----------------------------------------------------------------------
            // curl -v -k \
            //      -X REMOTESTOP \
            //      -u chargingmap:gf0c31j08ufgw3j9w3t \
            //      -H "Content-Type: application/json" \
            //      -H "Accept:       application/json" \
            //      -d "{ \"SessionId\": \"60ce73f6-0a88-1296-3d3d-623fdd276ddc\" }" \
            //      http://127.0.0.1:3004/RNs/Prod/EVSEs/DE*822*555555*100*1
            // -----------------------------------------------------------------------
            AddMethodCallback(HTTPHostname,
                                         REMOTESTOP,
                                         URLPrefix + "/EVSEs/{EVSEId}",
                                         HTTPContentType.JSON_UTF8,
                                         HTTPDelegate: async Request => {

                                             SendRemoteStopEVSE(Request);

                                             #region Check HTTP Basic Authentication

                                             if (Request.Authorization          == null        ||
                                                 (Request.Authorization as HTTPBasicAuthentication).Username != HTTPLogin   ||
                                                 (Request.Authorization as HTTPBasicAuthentication).Password != HTTPPassword)
                                             {

                                                 return SendEVSERemoteStopped(
                                                     new HTTPResponse.Builder(Request) {
                                                         HTTPStatusCode   = HTTPStatusCode.Unauthorized,
                                                         WWWAuthenticate  = @"Basic realm=""" + WWWAuthenticationRealm + @"""",
                                                         Server           = HTTPServer.DefaultServerName,
                                                         Date             = DateTime.Now,
                                                         Connection       = "close"
                                                     });

                                             }

                                             #endregion

                                             #region Get EVSEId URI parameter

                                             if (!Request.ParseEVSEId(DefaultHTTPServerName,
                                                                      out var evseId,
                                                                      out var httpResponse))
                                             {
                                                 return SendEVSERemoteStarted(httpResponse);
                                             }

                                             #endregion

                                             #region Parse JSON  [mandatory]

                                             ChargingSession_Id    SessionId  = default;
                                             EMobilityAccount_Id?  eMAId      = null;

                                             if (!Request.TryParseJObjectRequestBody(out var JSON,
                                                                                     out httpResponse,
                                                                                     AllowEmptyHTTPBody: false))

                                             {

                                                 #region Parse SessionId         [mandatory]

                                                 if (!JSON.ParseMandatory("SessionId",
                                                                          "Charging session identification",
                                                                          HTTPServer.DefaultServerName,
                                                                          ChargingSession_Id.TryParse,
                                                                          out SessionId,
                                                                          Request,
                                                                          out httpResponse))

                                                     return SendEVSERemoteStarted(httpResponse);

                                                 #endregion

                                                 #region Parse eMAId              [optional]

                                                 if (!JSON.ParseOptionalStruct2("eMAId",
                                                                               "e-Mobility account identification",
                                                                               HTTPServer.DefaultServerName,
                                                                               EMobilityAccount_Id.TryParse,
                                                                               out eMAId,
                                                                               Request,
                                                                               out httpResponse))
                                                 {
                                                     return SendEVSERemoteStarted(httpResponse);
                                                 }

                                                 #endregion

                                                 // ReservationHandling

                                             }

                                             else
                                                 return SendEVSERemoteStarted(httpResponse);

                                             #endregion


                                             var response = await EMSP.RemoteStop(//EVSEId,
                                                                                  SessionId,
                                                                                  ReservationHandling.Close, //ReservationHandling.KeepAlive(TimeSpan.FromMinutes(1)), // ToDo: Parse this property!
                                                                                  null,
                                                                                  RemoteAuthentication.FromRemoteIdentification(eMAId),

                                                                                  Request.Timestamp,
                                                                                  Request.EventTrackingId,
                                                                                  Request.Timeout,
                                                                                  Request.CancellationToken);


                                             switch (response.Result)
                                             {

                                                 #region Success

                                                 case RemoteStopResultTypes.Success:

                                                     if (response.ReservationHandling.IsKeepAlive == false)
                                                         return SendEVSERemoteStopped(
                                                             new HTTPResponse.Builder(Request) {
                                                                 HTTPStatusCode             = HTTPStatusCode.NoContent,
                                                                 Server                     = HTTPServer.DefaultServerName,
                                                                 Date                       = DateTime.Now,
                                                                 AccessControlAllowOrigin   = "*",
                                                                 AccessControlAllowMethods  = new[] { "POST" },
                                                                 AccessControlAllowHeaders  = new[] { "Content-Type", "Accept", "Authorization" }
                                                             });

                                                     else
                                                         return SendEVSERemoteStopped(
                                                             new HTTPResponse.Builder(Request) {
                                                                 HTTPStatusCode             = HTTPStatusCode.OK,
                                                                 Server                     = HTTPServer.DefaultServerName,
                                                                 Date                       = DateTime.Now,
                                                                 AccessControlAllowOrigin   = "*",
                                                                 AccessControlAllowMethods  = new[] { "POST" },
                                                                 AccessControlAllowHeaders  = new[] { "Content-Type", "Accept", "Authorization" },
                                                                 ContentType                = HTTPContentType.JSON_UTF8,
                                                                 Content                    = new JObject(
                                                                                                  new JProperty("KeepAlive", (Int32) response.ReservationHandling.KeepAliveTime.TotalSeconds)
                                                                                              ).ToUTF8Bytes()
                                                             });

                                                 #endregion

                                                 #region InvalidCredentials

                                                 case RemoteStopResultTypes.InvalidCredentials:
                                                     return SendEVSERemoteStopped(
                                                         new HTTPResponse.Builder(Request) {
                                                             HTTPStatusCode             = HTTPStatusCode.Unauthorized,
                                                             Server                     = HTTPServer.DefaultServerName,
                                                             Date                       = DateTime.Now,
                                                             AccessControlAllowOrigin   = "*",
                                                             AccessControlAllowMethods  = new[] { "POST" },
                                                             AccessControlAllowHeaders  = new[] { "Content-Type", "Accept", "Authorization" },
                                                             ContentType                = HTTPContentType.JSON_UTF8,
                                                             Content                    = new JObject(
                                                                                              new JProperty("description", "Unauthorized remote start or invalid credentials!")
                                                                                          ).ToUTF8Bytes()
                                                         });

                                                 #endregion

                                                 #region InvalidSessionId

                                                 case RemoteStopResultTypes.InvalidSessionId:
                                                     return SendEVSERemoteStopped(
                                                         new HTTPResponse.Builder(Request) {
                                                             HTTPStatusCode             = HTTPStatusCode.Conflict,
                                                             Server                     = HTTPServer.DefaultServerName,
                                                             Date                       = DateTime.Now,
                                                             AccessControlAllowOrigin   = "*",
                                                             AccessControlAllowMethods  = new[] { "POST" },
                                                             AccessControlAllowHeaders  = new[] { "Content-Type", "Accept", "Authorization" },
                                                             ContentType                = HTTPContentType.JSON_UTF8,
                                                             Content                    = new JObject(
                                                                                              new JProperty("description", "Invalid SessionId!")
                                                                                          ).ToUTF8Bytes()
                                                         });

                                                 #endregion

                                                 #region OutOfService

                                                 case RemoteStopResultTypes.OutOfService:
                                                     return SendEVSERemoteStopped(
                                                         new HTTPResponse.Builder(Request) {
                                                             HTTPStatusCode             = HTTPStatusCode.Conflict,
                                                             Server                     = HTTPServer.DefaultServerName,
                                                             Date                       = DateTime.Now,
                                                             AccessControlAllowOrigin   = "*",
                                                             AccessControlAllowMethods  = new[] { "POST" },
                                                             AccessControlAllowHeaders  = new[] { "Content-Type", "Accept", "Authorization" },
                                                             ContentType                = HTTPContentType.JSON_UTF8,
                                                             Content                    = new JObject(
                                                                                              new JProperty("description", "EVSE is out of service!")
                                                                                          ).ToUTF8Bytes()
                                                         });

                                                 #endregion

                                                 #region Offline

                                                 case RemoteStopResultTypes.Offline:
                                                     return SendEVSERemoteStopped(
                                                         new HTTPResponse.Builder(Request) {
                                                             HTTPStatusCode             = HTTPStatusCode.Conflict,
                                                             Server                     = HTTPServer.DefaultServerName,
                                                             Date                       = DateTime.Now,
                                                             AccessControlAllowOrigin   = "*",
                                                             AccessControlAllowMethods  = new[] { "POST" },
                                                             AccessControlAllowHeaders  = new[] { "Content-Type", "Accept", "Authorization" },
                                                             ContentType                = HTTPContentType.JSON_UTF8,
                                                             Content                    = new JObject(
                                                                                              new JProperty("description", "EVSE is offline!")
                                                                                          ).ToUTF8Bytes()
                                                         });

                                                 #endregion

                                                 #region default => BadRequest

                                                 default:
                                                     return SendEVSERemoteStopped(
                                                         new HTTPResponse.Builder(Request) {
                                                             HTTPStatusCode             = HTTPStatusCode.BadRequest,
                                                             Server                     = HTTPServer.DefaultServerName,
                                                             Date                       = DateTime.Now,
                                                             AccessControlAllowOrigin   = "*",
                                                             AccessControlAllowMethods  = new[] { "POST" },
                                                             AccessControlAllowHeaders  = new[] { "Content-Type", "Accept", "Authorization" },
                                                             ContentType                = HTTPContentType.JSON_UTF8,
                                                             Content                    = JSONObject.Create(
                                                                                              response.SessionId != null
                                                                                                  ? new JProperty("SessionId",  response.SessionId.ToString())
                                                                                                  : null,
                                                                                              new JProperty("Result",      response.Result.ToString()),
                                                                                              new JProperty("description", response.Description.IsNeitherNullNorEmpty() ? response.Description : I18NString.Create(Languages.en, "General error!"))
                                                                                          ).ToUTF8Bytes()
                                                         });

                                                 #endregion

                                             }

                                         });

            #endregion


        }

        #endregion



        #region (internal) SendReserveEVSE(Request)

        internal HTTPRequest SendReserveEVSE(HTTPRequest Request)
        {

            OnReserveEVSELog?.Invoke(Request.Timestamp,
                                     this.HTTPServer,
                                     Request);

            return Request;

        }

        #endregion

        #region (internal) SendEVSEReserved(Response)

        internal HTTPResponse SendEVSEReserved(HTTPResponse Response)
        {

            OnEVSEReservedLog?.Invoke(Response.Timestamp,
                                      this.HTTPServer,
                                      Response.HTTPRequest,
                                      Response);

            return Response;

        }

        #endregion


        #region (protected internal) SendReservationCancel(Request)

        protected internal HTTPRequest SendReservationCancel(HTTPRequest Request)
        {

            OnReservationCancel?.Invoke(Request.Timestamp,
                                        this.HTTPServer,
                                        Request);

            return Request;

        }

        #endregion

        #region (internal) SendCancelReservation(...)

        internal async Task<CancelReservationResult>

            SendCancelReservation(DateTime                               Timestamp,
                                  CancellationToken                      CancellationToken,
                                  EventTracking_Id?                      EventTrackingId,
                                  ChargingReservation_Id                 ReservationId,
                                  ChargingReservationCancellationReason  Reason,
                                  TimeSpan?                              RequestTimeout  = null)

        {

            var OnCancelReservationLocal = OnCancelReservation;
            if (OnCancelReservationLocal == null)
                return CancelReservationResult.Error(ReservationId,
                                                     Reason);

            var results = await Task.WhenAll(OnCancelReservationLocal.
                                                 GetInvocationList().
                                                 Select(subscriber => (subscriber as OnCancelReservationDelegate)
                                                     (Timestamp,
                                                      this,
                                                      CancellationToken,
                                                      EventTrackingId,
                                                      ReservationId,
                                                      Reason,
                                                      RequestTimeout)));

            return results.
                   //    Where(result => result.Result != RemoteStopResultTypes.Unspecified).
                       First();

        }

        #endregion

        #region (protected internal) SendReservationCancelled(Response)

        protected internal HTTPResponse SendReservationCancelled(HTTPResponse Response)
        {

            OnCancelReservationResponse?.Invoke(Response.Timestamp,
                                           this.HTTPServer,
                                           Response.HTTPRequest,
                                           Response);

            return Response;

        }

        #endregion


        #region (protected internal) SendRemoteStartEVSE(Request)

        protected internal HTTPRequest SendRemoteStartEVSE(HTTPRequest Request)
        {

            OnRemoteStartEVSELog?.Invoke(Request.Timestamp,
                                         this.HTTPServer,
                                         Request);

            return Request;

        }

        #endregion

        #region (protected internal) SendEVSERemoteStarted(Response)

        protected internal HTTPResponse SendEVSERemoteStarted(HTTPResponse Response)
        {

            OnEVSERemoteStartedLog?.Invoke(Response.Timestamp,
                                           this.HTTPServer,
                                           Response.HTTPRequest,
                                           Response);

            return Response;

        }

        #endregion


        #region (protected internal) SendRemoteStopEVSE(Request)

        protected internal HTTPRequest SendRemoteStopEVSE(HTTPRequest Request)
        {

            OnRemoteStopEVSELog?.Invoke(Request.Timestamp,
                                        this.HTTPServer,
                                        Request);

            return Request;

        }

        #endregion

        #region (protected internal) SendEVSERemoteStopped(Response)

        protected internal HTTPResponse SendEVSERemoteStopped(HTTPResponse Response)
        {

            OnEVSERemoteStoppedLog?.Invoke(Response.Timestamp,
                                           this.HTTPServer,
                                           Response.HTTPRequest,
                                           Response);

            return Response;

        }

        #endregion


        public void Start()
        {
            HTTPServer.Start();
        }

    }

}
