/*
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

using System;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace cloud.charging.open.protocols.WWCP.EMP
{

    /// <summary>
    /// An e-mobility service provider.
    /// </summary>
    public class EMobilityServiceProvider : //IEMobilityProviderUserInterface,
                                            IRemoteEMobilityProvider
    {

        #region Data

        private readonly ConcurrentDictionary<ChargingPool_Id,     ChargingPool>                  ChargingPools;

        private readonly ConcurrentDictionary<LocalAuthentication, TokenAuthorizationResultType>  AuthorizationDatabase;
        private readonly ConcurrentDictionary<ChargingSession_Id,  SessionInfo>                   SessionDatabase;
        private readonly ConcurrentDictionary<ChargingSession_Id,  ChargeDetailRecord>            ChargeDetailRecordDatabase;

        #endregion

        #region Properties

        /// <summary>
        /// The unique identification of the e-mobility service provider.
        /// </summary>
        public EMobilityProvider_Id  Id                 { get; }

        IId IAuthorizeStartStop.AuthId
            => Id;

        public Boolean DisableAuthentication           { get; set; }
        public Boolean DisableSendChargeDetailRecords  { get; set; }


        #region AllTokens

        public IEnumerable<KeyValuePair<LocalAuthentication, TokenAuthorizationResultType>> AllTokens
            => AuthorizationDatabase;

        #endregion

        #region AuthorizedTokens

        public IEnumerable<KeyValuePair<LocalAuthentication, TokenAuthorizationResultType>> AuthorizedTokens
            => AuthorizationDatabase.Where(v => v.Value == TokenAuthorizationResultType.Authorized);

        #endregion

        #region NotAuthorizedTokens

        public IEnumerable<KeyValuePair<LocalAuthentication, TokenAuthorizationResultType>> NotAuthorizedTokens
            => AuthorizationDatabase.Where(v => v.Value == TokenAuthorizationResultType.NotAuthorized);

        #endregion

        #region BlockedTokens

        public IEnumerable<KeyValuePair<LocalAuthentication, TokenAuthorizationResultType>> BlockedTokens
            => AuthorizationDatabase.Where(v => v.Value == TokenAuthorizationResultType.Blocked);

        #endregion

        public HTTPClientLogger HTTPLogger { get; }

        #endregion

        #region Links

        /// <summary>
        /// The parent roaming network.
        /// </summary>
        public IRoamingNetwork RoamingNetwork { get; }

        public IEnumerable<ChargingReservation> ChargingReservations => throw new NotImplementedException();

        public IEnumerable<ChargingSession> ChargingSessions => throw new NotImplementedException();

        #endregion

        #region Events

        #region OnEVSEDataPush/-Pushed

        ///// <summary>
        ///// An event fired whenever new EVSE data will be send upstream.
        ///// </summary>
        //public event OnPushEVSEDataRequestDelegate OnPushEVSEDataRequest;

        ///// <summary>
        ///// An event fired whenever new EVSE data had been sent upstream.
        ///// </summary>
        //public event OnPushEVSEDataResponseDelegate OnPushEVSEDataResponse;

        #endregion

        #region OnEVSEStatusPush/-Pushed

        ///// <summary>
        ///// An event fired whenever new EVSE status will be send upstream.
        ///// </summary>
        //public event OnPushEVSEStatusRequestDelegate OnPushEVSEStatusRequest;

        ///// <summary>
        ///// An event fired whenever new EVSE status had been sent upstream.
        ///// </summary>
        //public event OnPushEVSEStatusResponseDelegate OnPushEVSEStatusResponse;

        #endregion


        #region OnReserve... / OnReserved...

        /// <summary>
        /// An event fired whenever a charging location is being reserved.
        /// </summary>
        public event OnReserveRequestDelegate?             OnReserveRequest;

        /// <summary>
        /// An event fired whenever a charging location was reserved.
        /// </summary>
        public event OnReserveResponseDelegate?            OnReserveResponse;

        /// <summary>
        /// An event fired whenever a new charging reservation was created.
        /// </summary>
        public event OnNewReservationDelegate?             OnNewReservation;


        /// <summary>
        /// An event fired whenever a charging reservation is being canceled.
        /// </summary>
        public event OnCancelReservationRequestDelegate?   OnCancelReservationRequest;

        /// <summary>
        /// An event fired whenever a charging reservation was canceled.
        /// </summary>
        public event OnCancelReservationResponseDelegate?  OnCancelReservationResponse;

        /// <summary>
        /// An event fired whenever a charging reservation was canceled.
        /// </summary>
        public event OnReservationCanceledDelegate?        OnReservationCanceled;

        #endregion

        // CancelReservation

        #region OnRemote...Start / OnRemote...Started

        /// <summary>
        /// An event fired whenever a remote start command was received.
        /// </summary>
        public event OnRemoteStartRequestDelegate?     OnRemoteStartRequest;

        /// <summary>
        /// An event fired whenever a remote start command completed.
        /// </summary>
        public event OnRemoteStartResponseDelegate?    OnRemoteStartResponse;


        /// <summary>
        /// An event fired whenever a remote stop command was received.
        /// </summary>
        public event OnRemoteStopRequestDelegate?      OnRemoteStopRequest;

        /// <summary>
        /// An event fired whenever a remote stop command completed.
        /// </summary>
        public event OnRemoteStopResponseDelegate?     OnRemoteStopResponse;


        /// <summary>
        /// An event fired whenever a new charging session was created.
        /// </summary>
        public event OnNewChargingSessionDelegate?     OnNewChargingSession;

        /// <summary>
        /// An event fired whenever a new charge detail record was created.
        /// </summary>
        public event OnNewChargeDetailRecordDelegate?  OnNewChargeDetailRecord;

        #endregion



        // Incoming events from the roaming network

        public event OnAuthorizeStartRequestDelegate?                  OnAuthorizeStartRequest;
        public event OnAuthorizeStartResponseDelegate?                 OnAuthorizeStartResponse;

        public event OnAuthorizeStopRequestDelegate?                   OnAuthorizeStopRequest;
        public event OnAuthorizeStopResponseDelegate?                  OnAuthorizeStopResponse;

        #endregion

        #region Constructor(s)

        internal EMobilityServiceProvider(EMobilityProvider_Id  Id,
                                          IRoamingNetwork       RoamingNetwork)
        {

            this.Id                          = Id;
            this.RoamingNetwork              = RoamingNetwork;

            this.ChargingPools               = new ConcurrentDictionary<ChargingPool_Id,     ChargingPool>();

            this.AuthorizationDatabase       = new ConcurrentDictionary<LocalAuthentication, TokenAuthorizationResultType>();
            this.SessionDatabase             = new ConcurrentDictionary<ChargingSession_Id,  SessionInfo>();
            this.ChargeDetailRecordDatabase  = new ConcurrentDictionary<ChargingSession_Id,  ChargeDetailRecord>();

        }

        #endregion


        #region User and credential management

        #region AddToken(LocalAuthentication, AuthenticationResult = AuthenticationResult.Allowed)

        public Boolean AddToken(LocalAuthentication           LocalAuthentication,
                                TokenAuthorizationResultType  AuthenticationResult = TokenAuthorizationResultType.Authorized)
        {

            if (!AuthorizationDatabase.ContainsKey(LocalAuthentication))
                return AuthorizationDatabase.TryAdd(LocalAuthentication, AuthenticationResult);

            return false;

        }

        #endregion

        #region RemoveToken(Token)

        public Boolean RemoveToken(LocalAuthentication LocalAuthentication)
        {
            return AuthorizationDatabase.TryRemove(LocalAuthentication, out TokenAuthorizationResultType _AuthorizationResult);
        }

        #endregion

        #endregion


        #region Incoming requests from the roaming network

        #region (Set/Add/Update/Delete) Roaming network(s)...

        #region AddRoamingNetwork           (RoamingNetwork,  ...)

        /// <summary>
        /// Add the given roaming network.
        /// </summary>
        /// <param name="RoamingNetwork">A roaming network to add.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        public virtual Task<AddRoamingNetworkResult>

            AddRoamingNetwork(IRoamingNetwork     RoamingNetwork,

                              DateTime?           Timestamp,
                              EventTracking_Id?   EventTrackingId,
                              TimeSpan?           RequestTimeout,
                              CancellationToken   CancellationToken)

                => Task.FromResult(
                       AddRoamingNetworkResult.NoOperation(
                           RoamingNetwork,
                           EventTrackingId,
                           Id,
                           this
                       )
                   );

        #endregion

        #region AddRoamingNetworkIfNotExists(RoamingNetwork,  ...)

        /// <summary>
        /// Add the given roaming network, if it does not already exist.
        /// </summary>
        /// <param name="RoamingNetwork">A roaming network to add, if it does not already exist.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        public virtual Task<AddRoamingNetworkResult>

            AddRoamingNetworkIfNotExists(IRoamingNetwork     RoamingNetwork,

                                         DateTime?           Timestamp,
                                         EventTracking_Id?   EventTrackingId,
                                         TimeSpan?           RequestTimeout,
                                         CancellationToken   CancellationToken)

                => Task.FromResult(
                       AddRoamingNetworkResult.NoOperation(
                           RoamingNetwork,
                           EventTrackingId,
                           Id,
                           this
                       )
                   );

        #endregion

        #region AddOrUpdateRoamingNetwork   (RoamingNetwork,  ...)

        /// <summary>
        /// Add or update the given roaming network.
        /// </summary>
        /// <param name="RoamingNetwork">A roaming network to add or update.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        public virtual Task<AddOrUpdateRoamingNetworkResult>

            AddOrUpdateRoamingNetwork(IRoamingNetwork     RoamingNetwork,

                                      DateTime?           Timestamp,
                                      EventTracking_Id?   EventTrackingId,
                                      TimeSpan?           RequestTimeout,
                                      CancellationToken   CancellationToken)

                => Task.FromResult(
                       AddOrUpdateRoamingNetworkResult.NoOperation(
                           RoamingNetwork,
                           EventTrackingId,
                           Id,
                           this
                       )
                   );

        #endregion

        #region UpdateRoamingNetwork        (RoamingNetwork,  PropertyName, NewValue, OldValue = null, DataSource = null, ...)

        /// <summary>
        /// Update the EVSE data of the given roaming network within the static EVSE data at the OICP server.
        /// </summary>
        /// <param name="RoamingNetwork">A roaming network.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        public virtual Task<UpdateRoamingNetworkResult>

            UpdateRoamingNetwork(IRoamingNetwork     RoamingNetwork,
                                 String              PropertyName,
                                 Object?             NewValue,
                                 Object?             OldValue,
                                 Context?            DataSource,

                                 DateTime?           Timestamp,
                                 EventTracking_Id?   EventTrackingId,
                                 TimeSpan?           RequestTimeout,
                                 CancellationToken   CancellationToken)

                => Task.FromResult(
                       UpdateRoamingNetworkResult.NoOperation(
                           RoamingNetwork,
                           EventTrackingId,
                           Id,
                           this
                       )
                   );

        #endregion

        #region DeleteRoamingNetwork        (RoamingNetwork,  ...)

        /// <summary>
        /// Delete the EVSE data of the given roaming network from the static EVSE data at the OICP server.
        /// </summary>
        /// <param name="RoamingNetwork">A roaming network to upload.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        public virtual Task<DeleteRoamingNetworkResult>

            DeleteRoamingNetwork(IRoamingNetwork     RoamingNetwork,

                                 DateTime?           Timestamp,
                                 EventTracking_Id?   EventTrackingId,
                                 TimeSpan?           RequestTimeout,
                                 CancellationToken   CancellationToken)

                => Task.FromResult(
                       DeleteRoamingNetworkResult.NoOperation(
                           RoamingNetwork,
                           EventTrackingId,
                           Id,
                           this
                       )
                   );

        #endregion


        #region AddRoamingNetworks          (RoamingNetworks, ...)

        /// <summary>
        /// Add the given enumeration of roaming networks.
        /// </summary>
        /// <param name="RoamingNetworks">An enumeration of roaming networks to add.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        public virtual Task<AddRoamingNetworksResult>

            AddRoamingNetworks(IEnumerable<IRoamingNetwork>  RoamingNetworks,

                               DateTime?                     Timestamp,
                               EventTracking_Id?             EventTrackingId,
                               TimeSpan?                     RequestTimeout,
                               CancellationToken             CancellationToken)

                => Task.FromResult(
                       AddRoamingNetworksResult.NoOperation(
                           RoamingNetworks,
                           Id,
                           this,
                           EventTrackingId
                       )
                   );

        #endregion

        #region AddRoamingNetworkIfNotExists(RoamingNetworks, ...)

        /// <summary>
        /// Add the given enumeration of roaming networks, if they do not already exist.
        /// </summary>
        /// <param name="RoamingNetwork">An enumeration of roaming networks to add, if they do not already exist.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        public virtual Task<AddRoamingNetworksResult>

            AddRoamingNetworksIfNotExist(IEnumerable<IRoamingNetwork>  RoamingNetworks,

                                         DateTime?                     Timestamp,
                                         EventTracking_Id?             EventTrackingId,
                                         TimeSpan?                     RequestTimeout,
                                         CancellationToken             CancellationToken)

                => Task.FromResult(
                       AddRoamingNetworksResult.NoOperation(
                           RoamingNetworks,
                           Id,
                           this,
                           EventTrackingId
                       )
                   );

        #endregion

        #region AddOrUpdateRoamingNetwork   (RoamingNetworks, ...)

        /// <summary>
        /// Add or update the given enumeration of roaming networks.
        /// </summary>
        /// <param name="RoamingNetwork">An enumeration of roaming networks to add or update.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        public virtual Task<AddOrUpdateRoamingNetworksResult>

            AddOrUpdateRoamingNetworks(IEnumerable<IRoamingNetwork>  RoamingNetworks,

                                       DateTime?                     Timestamp,
                                       EventTracking_Id?             EventTrackingId,
                                       TimeSpan?                     RequestTimeout,
                                       CancellationToken             CancellationToken)

                => Task.FromResult(
                       AddOrUpdateRoamingNetworksResult.NoOperation(
                           RoamingNetworks,
                           Id,
                           this,
                           EventTrackingId
                       )
                   );

        #endregion

        #region UpdateRoamingNetwork        (RoamingNetworks, ...)

        /// <summary>
        /// Update the EVSE data of the given roaming network within the static EVSE data at the OICP server.
        /// </summary>
        /// <param name="RoamingNetwork">A roaming network.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        public virtual Task<UpdateRoamingNetworksResult>

            UpdateRoamingNetworks(IEnumerable<IRoamingNetwork>  RoamingNetworks,

                                  DateTime?                     Timestamp,
                                  EventTracking_Id?             EventTrackingId,
                                  TimeSpan?                     RequestTimeout,
                                  CancellationToken             CancellationToken)

                => Task.FromResult(
                       UpdateRoamingNetworksResult.NoOperation(
                           RoamingNetworks,
                           Id,
                           this,
                           EventTrackingId
                       )
                   );

        #endregion

        #region DeleteRoamingNetwork        (RoamingNetworks, ...)

        /// <summary>
        /// Delete the given enumeration of roaming networks.
        /// </summary>
        /// <param name="RoamingNetwork">An enumeration of roaming networks to delete.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        public virtual Task<DeleteRoamingNetworksResult>

            DeleteRoamingNetworks(IEnumerable<IRoamingNetwork>  RoamingNetworks,

                                  DateTime?                     Timestamp,
                                  EventTracking_Id?             EventTrackingId,
                                  TimeSpan?                     RequestTimeout,
                                  CancellationToken             CancellationToken)

                => Task.FromResult(
                       DeleteRoamingNetworksResult.NoOperation(
                           RoamingNetworks,
                           Id,
                           this,
                           EventTrackingId
                       )
                   );

        #endregion


        #region UpdateRoamingNetworkAdminStatus(AdminStatusUpdates, ...)

        /// <summary>
        /// Update the given enumeration of roaming network admin status updates.
        /// </summary>
        /// <param name="AdminStatusUpdates">An enumeration of roaming network admin status updates.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        public virtual Task<PushRoamingNetworkAdminStatusResult>

            UpdateRoamingNetworkAdminStatus(IEnumerable<RoamingNetworkAdminStatusUpdate>  AdminStatusUpdates,

                                            DateTime?                                     Timestamp,
                                            EventTracking_Id?                             EventTrackingId,
                                            TimeSpan?                                     RequestTimeout,
                                            CancellationToken                             CancellationToken)

                => Task.FromResult(
                       PushRoamingNetworkAdminStatusResult.NoOperation(
                           Id,
                           this
                       )
                   );

        #endregion

        #region UpdateRoamingNetworkStatus     (StatusUpdates, ...)

        /// <summary>
        /// Update the given enumeration of roaming network status updates.
        /// </summary>
        /// <param name="StatusUpdates">An enumeration of roaming network status updates.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        public virtual Task<PushRoamingNetworkStatusResult>

            UpdateRoamingNetworkStatus(IEnumerable<RoamingNetworkStatusUpdate>  StatusUpdates,

                                       DateTime?                                Timestamp,
                                       EventTracking_Id?                        EventTrackingId,
                                       TimeSpan?                                RequestTimeout,
                                       CancellationToken                        CancellationToken)

                => Task.FromResult(
                       PushRoamingNetworkStatusResult.NoOperation(
                           Id,
                           this
                       )
                   );

        #endregion

        #endregion

        #region (Set/Add/Update/Delete) Charging station operator(s)...

        #region AddChargingStationOperator           (ChargingStationOperator,  ...)

        /// <summary>
        /// Add the given charging station operator.
        /// </summary>
        /// <param name="ChargingStationOperator">A charging station operator to add.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<AddChargingStationOperatorResult>

            AddChargingStationOperator(IChargingStationOperator  ChargingStationOperator,

                                       DateTime?                 Timestamp,
                                       EventTracking_Id?         EventTrackingId,
                                       TimeSpan?                 RequestTimeout,
                                       CancellationToken         CancellationToken)

                => Task.FromResult(
                       AddChargingStationOperatorResult.NoOperation(
                           ChargingStationOperator,
                           EventTrackingId,
                           Id,
                           this
                       )
                   );

        #endregion

        #region AddChargingStationOperatorIfNotExists(ChargingStationOperator,  ...)

        /// <summary>
        /// Add the given charging station operator, if it does not already exist.
        /// </summary>
        /// <param name="ChargingStationOperator">A charging station operator to add, if it does not already exist.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<AddChargingStationOperatorResult>

            AddChargingStationOperatorIfNotExists(IChargingStationOperator  ChargingStationOperator,

                                                  DateTime?                 Timestamp,
                                                  EventTracking_Id?         EventTrackingId,
                                                  TimeSpan?                 RequestTimeout,
                                                  CancellationToken         CancellationToken)

                => Task.FromResult(
                       AddChargingStationOperatorResult.NoOperation(
                           ChargingStationOperator,
                           EventTrackingId,
                           Id,
                           this
                       )
                   );

        #endregion

        #region AddOrUpdateChargingStationOperator   (ChargingStationOperator,  ...)

        /// <summary>
        /// Add or update the given charging station operator.
        /// </summary>
        /// <param name="ChargingStationOperator">A charging station operator to add or update.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<AddOrUpdateChargingStationOperatorResult>

            AddOrUpdateChargingStationOperator(IChargingStationOperator  ChargingStationOperator,

                                               DateTime?                 Timestamp,
                                               EventTracking_Id?         EventTrackingId,
                                               TimeSpan?                 RequestTimeout,
                                               CancellationToken         CancellationToken)

                => Task.FromResult(
                       AddOrUpdateChargingStationOperatorResult.NoOperation(
                           ChargingStationOperator,
                           EventTrackingId,
                           Id,
                           this
                       )
                   );

        #endregion

        #region UpdateChargingStationOperator        (ChargingStationOperator,  PropertyName, NewValue, OldValue = null, DataSource = null, ...)

        /// <summary>
        /// Update the given charging station operator.
        /// The charging station operator can be uploaded as a whole, or just a single property of the charging station operator.
        /// </summary>
        /// <param name="ChargingStationOperator">A charging station operator to update.</param>
        /// <param name="PropertyName">The name of the charging station operator property to update.</param>
        /// <param name="NewValue">The new value of the charging station operator property to update.</param>
        /// <param name="OldValue">The optional old value of the charging station operator property to update.</param>
        /// <param name="DataSource">An optional data source or context for the data change.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<UpdateChargingStationOperatorResult>

            UpdateChargingStationOperator(IChargingStationOperator  ChargingStationOperator,
                                          String                    PropertyName,
                                          Object?                   NewValue,
                                          Object?                   OldValue,
                                          Context?                  DataSource,

                                          DateTime?                 Timestamp,
                                          EventTracking_Id?         EventTrackingId,
                                          TimeSpan?                 RequestTimeout,
                                          CancellationToken         CancellationToken)

                => Task.FromResult(
                       UpdateChargingStationOperatorResult.NoOperation(
                           ChargingStationOperator,
                           EventTrackingId,
                           Id,
                           this
                       )
                   );

        #endregion

        #region DeleteChargingStationOperator        (ChargingStationOperator,  ...)

        /// <summary>
        /// Delete the given charging station operator.
        /// </summary>
        /// <param name="ChargingStationOperator">A charging station operator to delete.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<DeleteChargingStationOperatorResult>

            DeleteChargingStationOperator(IChargingStationOperator  ChargingStationOperator,

                                          DateTime?                 Timestamp,
                                          EventTracking_Id?         EventTrackingId,
                                          TimeSpan?                 RequestTimeout,
                                          CancellationToken         CancellationToken)

                => Task.FromResult(
                       DeleteChargingStationOperatorResult.NoOperation(
                           ChargingStationOperator,
                           EventTrackingId,
                           Id,
                           this
                       )
                   );

        #endregion


        #region AddChargingStationOperators          (ChargingStationOperators, ...)

        /// <summary>
        /// Add the given enumeration of charging station operators.
        /// </summary>
        /// <param name="ChargingStationOperators">An enumeration of charging station operators to add.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<AddChargingStationOperatorsResult>

            AddChargingStationOperators(IEnumerable<IChargingStationOperator>  ChargingStationOperators,

                                        DateTime?                              Timestamp,
                                        EventTracking_Id?                      EventTrackingId,
                                        TimeSpan?                              RequestTimeout,
                                        CancellationToken                      CancellationToken)

                => Task.FromResult(
                       AddChargingStationOperatorsResult.NoOperation(
                           ChargingStationOperators,
                           Id,
                           this,
                           EventTrackingId
                       )
                   );

        #endregion

        #region AddChargingStationOperatorsIfNotExist(ChargingStationOperators, ...)

        /// <summary>
        /// Add the given enumeration of charging station operators, if they do not already exist.
        /// </summary>
        /// <param name="ChargingStationOperators">An enumeration of charging station operators to add, if they do not already exist.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<AddChargingStationOperatorsResult>

            AddChargingStationOperatorsIfNotExist(IEnumerable<IChargingStationOperator>  ChargingStationOperators,

                                                  DateTime?                              Timestamp,
                                                  EventTracking_Id?                      EventTrackingId,
                                                  TimeSpan?                              RequestTimeout,
                                                  CancellationToken                      CancellationToken)

                => Task.FromResult(
                       AddChargingStationOperatorsResult.NoOperation(
                           ChargingStationOperators,
                           Id,
                           this,
                           EventTrackingId
                       )
                   );

        #endregion

        #region AddOrUpdateChargingStationOperators  (ChargingStationOperators, ...)

        /// <summary>
        /// Add or update the given enumeration of charging station operators.
        /// </summary>
        /// <param name="ChargingStationOperators">An enumeration of charging station operators to add or update.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<AddOrUpdateChargingStationOperatorsResult>

            AddOrUpdateChargingStationOperators(IEnumerable<IChargingStationOperator>  ChargingStationOperators,

                                                DateTime?                              Timestamp,
                                                EventTracking_Id?                      EventTrackingId,
                                                TimeSpan?                              RequestTimeout,
                                                CancellationToken                      CancellationToken)

                => Task.FromResult(
                       AddOrUpdateChargingStationOperatorsResult.NoOperation(
                           ChargingStationOperators,
                           Id,
                           this,
                           EventTrackingId
                       )
                   );

        #endregion

        #region UpdateChargingStationOperators       (ChargingStationOperators, ...)

        /// <summary>
        /// Update the given enumeration of charging station operators.
        /// </summary>
        /// <param name="ChargingStationOperators">An enumeration of charging station operators to update.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<UpdateChargingStationOperatorsResult>

            UpdateChargingStationOperators(IEnumerable<IChargingStationOperator>  ChargingStationOperators,

                                           DateTime?                              Timestamp,
                                           EventTracking_Id?                      EventTrackingId,
                                           TimeSpan?                              RequestTimeout,
                                           CancellationToken                      CancellationToken)

                => Task.FromResult(
                       UpdateChargingStationOperatorsResult.NoOperation(
                           ChargingStationOperators,
                           Id,
                           this,
                           EventTrackingId
                       )
                   );

        #endregion

        #region DeleteChargingStationOperators       (ChargingStationOperators, ...)

        /// <summary>
        /// Delete the given enumeration of charging station operators.
        /// </summary>
        /// <param name="ChargingStationOperators">An enumeration of charging station operators to delete.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<DeleteChargingStationOperatorsResult>

            DeleteChargingStationOperators(IEnumerable<IChargingStationOperator>  ChargingStationOperators,

                                           DateTime?                              Timestamp,
                                           EventTracking_Id?                      EventTrackingId,
                                           TimeSpan?                              RequestTimeout,
                                           CancellationToken                      CancellationToken)

                => Task.FromResult(
                       DeleteChargingStationOperatorsResult.NoOperation(
                           ChargingStationOperators,
                           Id,
                           this,
                           EventTrackingId
                       )
                   );

        #endregion


        #region UpdateChargingStationOperatorAdminStatus(AdminStatusUpdates, ...)

        /// <summary>
        /// Update the given enumeration of charging station operator admin status updates.
        /// </summary>
        /// <param name="AdminStatusUpdates">An enumeration of charging station operator admin status updates.</param>
        /// <param name="TransmissionType">Whether to send the charging station operator admin status updates directly or enqueue it for a while.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        public virtual Task<PushChargingStationOperatorAdminStatusResult>

            UpdateChargingStationOperatorAdminStatus(IEnumerable<ChargingStationOperatorAdminStatusUpdate>  AdminStatusUpdates,

                                                     DateTime?                                              Timestamp,
                                                     EventTracking_Id?                                      EventTrackingId,
                                                     TimeSpan?                                              RequestTimeout,
                                                     CancellationToken                                      CancellationToken)

                => Task.FromResult(
                       PushChargingStationOperatorAdminStatusResult.OutOfService(
                           Id,
                           this,
                           AdminStatusUpdates
                       )
                   );

        #endregion

        #region UpdateChargingStationOperatorStatus     (StatusUpdates, ...)

        /// <summary>
        /// Update the given enumeration of charging station operator status updates.
        /// </summary>
        /// <param name="StatusUpdates">An enumeration of charging station operator status updates.</param>
        /// <param name="TransmissionType">Whether to send the charging station operator status updates directly or enqueue it for a while.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        public virtual Task<PushChargingStationOperatorStatusResult>

            UpdateChargingStationOperatorStatus(IEnumerable<ChargingStationOperatorStatusUpdate>  StatusUpdates,

                                                DateTime?                                         Timestamp,
                                                EventTracking_Id?                                 EventTrackingId,
                                                TimeSpan?                                         RequestTimeout,
                                                CancellationToken                                 CancellationToken)

                => Task.FromResult(
                       PushChargingStationOperatorStatusResult.NoOperation(
                           Id,
                           this
                       )
                   );

        #endregion

        #endregion

        #region (Set/Add/Update/Delete) Charging pool(s)...

        #region AddChargingPool           (ChargingPool,  ...)

        /// <summary>
        /// Add the given charging pool.
        /// </summary>
        /// <param name="ChargingPool">A charging pool to add.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<AddChargingPoolResult>

            AddChargingPool(IChargingPool       ChargingPool,

                            DateTime?           Timestamp,
                            EventTracking_Id?   EventTrackingId,
                            TimeSpan?           RequestTimeout,
                            CancellationToken   CancellationToken)

                => Task.FromResult(
                       AddChargingPoolResult.NoOperation(
                           ChargingPool,
                           EventTrackingId,
                           Id,
                           this
                       )
                   );

        #endregion

        #region AddChargingPoolIfNotExists(ChargingPool,  ...)

        /// <summary>
        /// Add the given charging pool, if it does not already exist.
        /// </summary>
        /// <param name="ChargingPool">A charging pool to add, if it does not already exist.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<AddChargingPoolResult>

            AddChargingPoolIfNotExists(IChargingPool       ChargingPool,

                                       DateTime?           Timestamp,
                                       EventTracking_Id?   EventTrackingId,
                                       TimeSpan?           RequestTimeout,
                                       CancellationToken   CancellationToken)

                => Task.FromResult(
                       AddChargingPoolResult.NoOperation(
                           ChargingPool,
                           EventTrackingId,
                           Id,
                           this
                       )
                   );

        #endregion

        #region AddOrUpdateChargingPool   (ChargingPool,  ...)

        /// <summary>
        /// Add or update the given charging pool.
        /// </summary>
        /// <param name="ChargingPool">A charging pool to add or update.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<AddOrUpdateChargingPoolResult>

            AddOrUpdateChargingPool(IChargingPool       ChargingPool,

                                    DateTime?           Timestamp,
                                    EventTracking_Id?   EventTrackingId,
                                    TimeSpan?           RequestTimeout,
                                    CancellationToken   CancellationToken)

                => Task.FromResult(
                       AddOrUpdateChargingPoolResult.NoOperation(
                           ChargingPool,
                           EventTrackingId,
                           Id,
                           this
                       )
                   );

        #endregion

        #region UpdateChargingPool        (ChargingPool,  PropertyName, NewValue, OldValue = null, DataSource = null, ...)

        /// <summary>
        /// Update the given charging pool.
        /// The charging pool can be uploaded as a whole, or just a single property of the charging pool.
        /// </summary>
        /// <param name="ChargingPool">A charging pool to update.</param>
        /// <param name="PropertyName">The name of the charging pool property to update.</param>
        /// <param name="NewValue">The new value of the charging pool property to update.</param>
        /// <param name="OldValue">The optional old value of the charging pool property to update.</param>
        /// <param name="DataSource">An optional data source or context for the data change.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<UpdateChargingPoolResult>

            UpdateChargingPool(IChargingPool       ChargingPool,
                               String?             PropertyName,
                               Object?             NewValue,
                               Object?             OldValue,
                               Context?            DataSource,

                               DateTime?           Timestamp,
                               EventTracking_Id?   EventTrackingId,
                               TimeSpan?           RequestTimeout,
                               CancellationToken   CancellationToken)

                => Task.FromResult(
                       UpdateChargingPoolResult.NoOperation(
                           ChargingPool,
                           EventTrackingId,
                           Id,
                           this
                       )
                   );

        #endregion

        #region DeleteChargingPool        (ChargingPool,  ...)

        /// <summary>
        /// Delete the given charging pool.
        /// </summary>
        /// <param name="ChargingPool">A charging pool to delete.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<DeleteChargingPoolResult>

            DeleteChargingPool(IChargingPool       ChargingPool,

                               DateTime?           Timestamp,
                               EventTracking_Id?   EventTrackingId,
                               TimeSpan?           RequestTimeout,
                               CancellationToken   CancellationToken)

                => Task.FromResult(
                       DeleteChargingPoolResult.NoOperation(
                           ChargingPool,
                           EventTrackingId,
                           Id,
                           this
                       )
                   );

        #endregion


        #region AddChargingPools          (ChargingPools, ...)

        /// <summary>
        /// Add the given enumeration of charging pools.
        /// </summary>
        /// <param name="ChargingPools">An enumeration of charging pools to add.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<AddChargingPoolsResult>

            AddChargingPools(IEnumerable<IChargingPool>  ChargingPools,

                             DateTime?                   Timestamp,
                             EventTracking_Id?           EventTrackingId,
                             TimeSpan?                   RequestTimeout,
                             CancellationToken           CancellationToken)

                => Task.FromResult(
                       AddChargingPoolsResult.NoOperation(
                           ChargingPools,
                           Id,
                           this,
                           EventTrackingId
                       )
                   );

        #endregion

        #region AddChargingPoolsIfNotExist(ChargingPools, ...)

        /// <summary>
        /// Add the given enumeration of charging pools, if they do not already exist.
        /// </summary>
        /// <param name="ChargingPools">An enumeration of charging pools to add, if they do not already exist.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<AddChargingPoolsResult>

            AddChargingPoolsIfNotExist(IEnumerable<IChargingPool>  ChargingPools,

                                       DateTime?                   Timestamp,
                                       EventTracking_Id?           EventTrackingId,
                                       TimeSpan?                   RequestTimeout,
                                       CancellationToken           CancellationToken)

                => Task.FromResult(
                       AddChargingPoolsResult.NoOperation(
                           ChargingPools,
                           Id,
                           this,
                           EventTrackingId
                       )
                   );

        #endregion

        #region AddOrUpdateChargingPools  (ChargingPools, ...)

        /// <summary>
        /// Add or update the given enumeration of charging pools.
        /// </summary>
        /// <param name="ChargingPools">An enumeration of charging pools to add or update.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<AddOrUpdateChargingPoolsResult>

            AddOrUpdateChargingPools(IEnumerable<IChargingPool>  ChargingPools,

                                     DateTime?                   Timestamp,
                                     EventTracking_Id?           EventTrackingId,
                                     TimeSpan?                   RequestTimeout,
                                     CancellationToken           CancellationToken)

                => Task.FromResult(
                       AddOrUpdateChargingPoolsResult.NoOperation(
                           ChargingPools,
                           Id,
                           this,
                           EventTrackingId
                       )
                   );

        #endregion

        #region UpdateChargingPools       (ChargingPools, ...)

        /// <summary>
        /// Update the given enumeration of charging pools.
        /// </summary>
        /// <param name="ChargingPools">An enumeration of charging pools to update.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<UpdateChargingPoolsResult>

            UpdateChargingPools(IEnumerable<IChargingPool>  ChargingPools,

                                DateTime?                   Timestamp,
                                EventTracking_Id?           EventTrackingId,
                                TimeSpan?                   RequestTimeout,
                                CancellationToken           CancellationToken)

                => Task.FromResult(
                       UpdateChargingPoolsResult.NoOperation(
                           ChargingPools,
                           Id,
                           this,
                           EventTrackingId
                       )
                   );

        #endregion

        #region DeleteChargingPools       (ChargingPools, ...)

        /// <summary>
        /// Delete the given enumeration of charging pools.
        /// </summary>
        /// <param name="ChargingPools">An enumeration of charging pools to delete.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<DeleteChargingPoolsResult>

            DeleteChargingPools(IEnumerable<IChargingPool>  ChargingPools,

                                DateTime?                   Timestamp,
                                EventTracking_Id?           EventTrackingId,
                                TimeSpan?                   RequestTimeout,
                                CancellationToken           CancellationToken)

                => Task.FromResult(
                       DeleteChargingPoolsResult.NoOperation(
                           ChargingPools,
                           Id,
                           this,
                           EventTrackingId
                       )
                   );

        #endregion


        #region UpdateChargingPoolAdminStatus (AdminStatusUpdates, ...)

        /// <summary>
        /// Update the given enumeration of charging pool admin status updates.
        /// </summary>
        /// <param name="AdminStatusUpdates">An enumeration of charging pool admin status updates.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<PushChargingPoolAdminStatusResult>

            UpdateChargingPoolAdminStatus(IEnumerable<ChargingPoolAdminStatusUpdate>  AdminStatusUpdates,

                                          DateTime?                                   Timestamp,
                                          EventTracking_Id?                           EventTrackingId,
                                          TimeSpan?                                   RequestTimeout,
                                          CancellationToken                           CancellationToken)

                => Task.FromResult(
                       PushChargingPoolAdminStatusResult.OutOfService(
                           Id,
                           this,
                           AdminStatusUpdates
                       )
                   );

        #endregion

        #region UpdateChargingPoolStatus      (StatusUpdates, ...)

        /// <summary>
        /// Update the given enumeration of charging pool status updates.
        /// </summary>
        /// <param name="StatusUpdates">An enumeration of charging pool status updates.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        public virtual Task<PushChargingPoolStatusResult>

            UpdateChargingPoolStatus(IEnumerable<ChargingPoolStatusUpdate>  StatusUpdates,

                                     DateTime?                              Timestamp,
                                     EventTracking_Id?                      EventTrackingId,
                                     TimeSpan?                              RequestTimeout,
                                     CancellationToken                      CancellationToken)

                => Task.FromResult(
                       PushChargingPoolStatusResult.NoOperation(
                           Id,
                           this
                       )
                   );

        #endregion

        #region UpdateChargingPoolEnergyStatus(ChargingPoolEnergyStatusUpdates, ...)

        /// <summary>
        /// Update the given enumeration of charging pool energy status.
        /// </summary>
        /// <param name="ChargingPoolEnergyStatusUpdates">An enumeration of charging pool energy status updates.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        public virtual Task<PushChargingPoolEnergyStatusResult>

            UpdateChargingPoolEnergyStatus(IEnumerable<ChargingPoolEnergyStatusUpdate>  ChargingPoolEnergyStatusUpdates,

                                           DateTime?                                    Timestamp,
                                           EventTracking_Id?                            EventTrackingId,
                                           TimeSpan?                                    RequestTimeout,
                                           CancellationToken                            CancellationToken)

                => Task.FromResult(
                       PushChargingPoolEnergyStatusResult.OutOfService(
                           Id,
                           this,
                           ChargingPoolEnergyStatusUpdates
                       )
                   );

        #endregion

        #endregion

        #region (Set/Add/Update/Delete) Charging station(s)...

        #region AddChargingStation           (ChargingStation,  ...)

        /// <summary>
        /// Add the given charging station.
        /// </summary>
        /// <param name="ChargingStation">A charging station to add.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<AddChargingStationResult>

            AddChargingStation(IChargingStation   ChargingStation,

                               DateTime?          Timestamp,
                               EventTracking_Id?  EventTrackingId,
                               TimeSpan?          RequestTimeout,
                               CancellationToken  CancellationToken)

                => Task.FromResult(
                       AddChargingStationResult.NoOperation(
                           ChargingStation,
                           EventTrackingId,
                           Id,
                           this
                       )
                   );

        #endregion

        #region AddChargingStationIfNotExists(ChargingStation,  ...)

        /// <summary>
        /// Add the given charging station, if it does not already exist.
        /// </summary>
        /// <param name="ChargingStation">A charging station to add, if it does not already exist.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<AddChargingStationResult>

            AddChargingStationIfNotExists(IChargingStation   ChargingStation,

                                          DateTime?          Timestamp,
                                          EventTracking_Id?  EventTrackingId,
                                          TimeSpan?          RequestTimeout,
                                          CancellationToken  CancellationToken)

                => Task.FromResult(
                       AddChargingStationResult.NoOperation(
                           ChargingStation,
                           EventTrackingId,
                           Id,
                           this
                       )
                   );

        #endregion

        #region AddOrUpdateChargingStation   (ChargingStation,  ...)

        /// <summary>
        /// Add or update the given charging station.
        /// </summary>
        /// <param name="ChargingStation">A charging station to add or update.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<AddOrUpdateChargingStationResult>

            AddOrUpdateChargingStation(IChargingStation   ChargingStation,

                                       DateTime?          Timestamp,
                                       EventTracking_Id?  EventTrackingId,
                                       TimeSpan?          RequestTimeout,
                                       CancellationToken  CancellationToken)

                => Task.FromResult(
                       AddOrUpdateChargingStationResult.NoOperation(
                           ChargingStation,
                           EventTrackingId,
                           Id,
                           this
                       )
                   );

        #endregion

        #region UpdateChargingStation        (ChargingStation,  PropertyName, NewValue, OldValue = null, DataSource = null, ...)

        /// <summary>
        /// Update the given charging station.
        /// The charging station can be uploaded as a whole, or just a single property of the charging station.
        /// </summary>
        /// <param name="ChargingStation">A charging station to update.</param>
        /// <param name="PropertyName">The name of the charging station property to update.</param>
        /// <param name="NewValue">The new value of the charging station property to update.</param>
        /// <param name="OldValue">The optional old value of the charging station property to update.</param>
        /// <param name="DataSource">An optional data source or context for the data change.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<UpdateChargingStationResult>

            UpdateChargingStation(IChargingStation   ChargingStation,
                                  String?            PropertyName,
                                  Object?            OldValue,
                                  Object?            NewValue,
                                  Context?           DataSource,

                                  DateTime?          Timestamp,
                                  EventTracking_Id?  EventTrackingId,
                                  TimeSpan?          RequestTimeout,
                                  CancellationToken  CancellationToken)

                => Task.FromResult(
                       UpdateChargingStationResult.NoOperation(
                           ChargingStation,
                           EventTrackingId,
                           Id,
                           this
                       )
                   );

        #endregion

        #region DeleteChargingStation        (ChargingStation,  ...)

        /// <summary>
        /// Delete the given charging station.
        /// </summary>
        /// <param name="ChargingStation">A charging station to delete.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<DeleteChargingStationResult>

            DeleteChargingStation(IChargingStation    ChargingStation,

                                  DateTime?           Timestamp,
                                  EventTracking_Id?   EventTrackingId,
                                  TimeSpan?           RequestTimeout,
                                  CancellationToken   CancellationToken)

                => Task.FromResult(
                       DeleteChargingStationResult.NoOperation(
                           ChargingStation,
                           EventTrackingId,
                           Id,
                           this
                       )
                   );

        #endregion


        #region AddChargingStations          (ChargingStations, ...)

        /// <summary>
        /// Add the given enumeration of charging stations.
        /// </summary>
        /// <param name="ChargingStations">An enumeration of charging stations to add.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<AddChargingStationsResult>

            AddChargingStations(IEnumerable<IChargingStation>  ChargingStations,

                                DateTime?                      Timestamp,
                                EventTracking_Id?              EventTrackingId,
                                TimeSpan?                      RequestTimeout,
                                CancellationToken              CancellationToken)

                => Task.FromResult(
                       AddChargingStationsResult.NoOperation(
                           ChargingStations,
                           Id,
                           this,
                           EventTrackingId
                       )
                   );

        #endregion

        #region AddChargingStationsIfNotExist(ChargingStations, ...)

        /// <summary>
        /// Add the given enumeration of charging stations, if they do not already exist..
        /// </summary>
        /// <param name="ChargingStations">An enumeration of charging stations to add, if they do not already exist..</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<AddChargingStationsResult>

            AddChargingStationsIfNotExist(IEnumerable<IChargingStation>  ChargingStations,

                                          DateTime?                      Timestamp,
                                          EventTracking_Id?              EventTrackingId,
                                          TimeSpan?                      RequestTimeout,
                                          CancellationToken              CancellationToken)

                => Task.FromResult(
                       AddChargingStationsResult.NoOperation(
                           ChargingStations,
                           Id,
                           this,
                           EventTrackingId
                       )
                   );

        #endregion

        #region AddOrUpdateChargingStations  (ChargingStations, ...)

        /// <summary>
        /// Add or update the given enumeration of charging stations.
        /// </summary>
        /// <param name="ChargingStations">An enumeration of charging stations to add or update.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<AddOrUpdateChargingStationsResult>

            AddOrUpdateChargingStations(IEnumerable<IChargingStation>  ChargingStations,

                                        DateTime?                      Timestamp,
                                        EventTracking_Id?              EventTrackingId,
                                        TimeSpan?                      RequestTimeout,
                                        CancellationToken              CancellationToken)

                => Task.FromResult(
                       AddOrUpdateChargingStationsResult.NoOperation(
                           ChargingStations,
                           Id,
                           this,
                           EventTrackingId
                       )
                   );

        #endregion

        #region UpdateChargingStations       (ChargingStations, ...)

        /// <summary>
        /// Update the given enumeration of charging stations.
        /// </summary>
        /// <param name="ChargingStations">An enumeration of charging stations to update.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<UpdateChargingStationsResult>

            UpdateChargingStations(IEnumerable<IChargingStation>  ChargingStations,

                                   DateTime?                      Timestamp,
                                   EventTracking_Id?              EventTrackingId,
                                   TimeSpan?                      RequestTimeout,
                                   CancellationToken              CancellationToken)

                => Task.FromResult(
                       UpdateChargingStationsResult.NoOperation(
                           ChargingStations,
                           Id,
                           this,
                           EventTrackingId
                       )
                   );

        #endregion

        #region DeleteChargingStations       (ChargingStations, ...)

        /// <summary>
        /// Delete the given enumeration of charging stations.
        /// </summary>
        /// <param name="ChargingStations">An enumeration of charging stations to delete.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<DeleteChargingStationsResult>

            DeleteChargingStations(IEnumerable<IChargingStation>  ChargingStations,

                                   DateTime?                      Timestamp,
                                   EventTracking_Id?              EventTrackingId,
                                   TimeSpan?                      RequestTimeout,
                                   CancellationToken              CancellationToken)

                => Task.FromResult(
                       DeleteChargingStationsResult.NoOperation(
                           ChargingStations,
                           Id,
                           this,
                           EventTrackingId
                       )
                   );

        #endregion


        #region UpdateChargingStationAdminStatus (AdminStatusUpdates, ...)

        /// <summary>
        /// Update the given enumeration of charging station admin status updates.
        /// </summary>
        /// <param name="AdminStatusUpdates">An enumeration of charging station admin status updates.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<PushChargingStationAdminStatusResult>

            UpdateChargingStationAdminStatus(IEnumerable<ChargingStationAdminStatusUpdate>  AdminStatusUpdates,

                                             DateTime?                                      Timestamp,
                                             EventTracking_Id?                              EventTrackingId,
                                             TimeSpan?                                      RequestTimeout,
                                             CancellationToken                              CancellationToken)

                => Task.FromResult(
                       PushChargingStationAdminStatusResult.OutOfService(
                           Id,
                           this,
                           AdminStatusUpdates
                       )
                   );

        #endregion

        #region UpdateChargingStationStatus      (StatusUpdates, ...)

        /// <summary>
        /// Update the given enumeration of charging station status updates.
        /// </summary>
        /// <param name="StatusUpdates">An enumeration of charging station status updates.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        public virtual Task<PushChargingStationStatusResult>

            UpdateChargingStationStatus(IEnumerable<ChargingStationStatusUpdate>  StatusUpdates,

                                        DateTime?                                 Timestamp,
                                        EventTracking_Id?                         EventTrackingId,
                                        TimeSpan?                                 RequestTimeout,
                                        CancellationToken                         CancellationToken)

                => Task.FromResult(
                       PushChargingStationStatusResult.OutOfService(
                           Id,
                           this,
                           StatusUpdates
                       )
                   );

        #endregion

        #region UpdateChargingStationEnergyStatus(ChargingStationEnergyStatusUpdates, ...)

        /// <summary>
        /// Update the given enumeration of charging station energy status.
        /// </summary>
        /// <param name="ChargingStationEnergyStatusUpdates">An enumeration of charging station energy status updates.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        public virtual Task<PushChargingStationEnergyStatusResult>

            UpdateChargingStationEnergyStatus(IEnumerable<ChargingStationEnergyStatusUpdate>  ChargingStationEnergyStatusUpdates,

                                              DateTime?                                       Timestamp,
                                              EventTracking_Id?                               EventTrackingId,
                                              TimeSpan?                                       RequestTimeout,
                                              CancellationToken                               CancellationToken)

                => Task.FromResult(
                       PushChargingStationEnergyStatusResult.OutOfService(
                           Id,
                           this,
                           ChargingStationEnergyStatusUpdates
                       )
                   );

        #endregion

        #endregion

        #region (Set/Add/Update/Delete) EVSE(s)...

        #region AddEVSE           (EVSE,  ...)

        /// <summary>
        /// Add the given EVSE.
        /// </summary>
        /// <param name="EVSE">An EVSE to add.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<AddEVSEResult>

            AddEVSE(IEVSE              EVSE,

                    DateTime?          Timestamp,
                    EventTracking_Id?  EventTrackingId,
                    TimeSpan?          RequestTimeout,
                    CancellationToken  CancellationToken)

                => Task.FromResult(
                       AddEVSEResult.NoOperation(
                           EVSE,
                           EventTrackingId,
                           Id,
                           this
                       )
                   );

        #endregion

        #region AddEVSEIfNotExists(EVSE,  ...)

        /// <summary>
        /// Add the given EVSE, if it does not already exist.
        /// </summary>
        /// <param name="EVSE">An EVSE to add, if it does not already exist.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<AddEVSEResult>

            AddEVSEIfNotExists(IEVSE              EVSE,

                               DateTime?          Timestamp,
                               EventTracking_Id?  EventTrackingId,
                               TimeSpan?          RequestTimeout,
                               CancellationToken  CancellationToken)

                => Task.FromResult(
                       AddEVSEResult.NoOperation(
                           EVSE,
                           EventTrackingId,
                           Id,
                           this
                       )
                   );

        #endregion

        #region AddOrUpdateEVSE   (EVSE,  ...)

        /// <summary>
        /// Add or update the given EVSE.
        /// </summary>
        /// <param name="EVSE">An EVSE to add or update.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<AddOrUpdateEVSEResult>

            AddOrUpdateEVSE(IEVSE              EVSE,

                            DateTime?          Timestamp,
                            EventTracking_Id?  EventTrackingId,
                            TimeSpan?          RequestTimeout,
                            CancellationToken  CancellationToken)

                => Task.FromResult(
                       AddOrUpdateEVSEResult.NoOperation(
                           EVSE,
                           EventTrackingId,
                           Id,
                           this
                       )
                   );

        #endregion

        #region UpdateEVSE        (EVSE,  PropertyName, NewValue, OldValue = null, DataSource = null, ...)

        /// <summary>
        /// Update the given EVSE.
        /// The EVSE can be uploaded as a whole, or just a single property of the EVSE.
        /// </summary>
        /// <param name="EVSE">An EVSE to update.</param>
        /// <param name="PropertyName">The name of the EVSE property to update.</param>
        /// <param name="NewValue">The new value of the EVSE property to update.</param>
        /// <param name="OldValue">The optional old value of the EVSE property to update.</param>
        /// <param name="DataSource">An optional data source or context for the data change.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<UpdateEVSEResult>

            UpdateEVSE(IEVSE               EVSE,
                       String?             PropertyName,
                       Object?             OldValue,
                       Object?             NewValue,
                       Context?            DataSource,

                       DateTime?           Timestamp,
                       EventTracking_Id?   EventTrackingId,
                       TimeSpan?           RequestTimeout,
                       CancellationToken   CancellationToken)

                => Task.FromResult(
                       UpdateEVSEResult.NoOperation(
                           EVSE,
                           EventTrackingId,
                           Id,
                           this
                       )
                   );

        #endregion

        #region DeleteEVSE        (EVSE,  ...)

        /// <summary>
        /// Delete the given EVSE.
        /// </summary>
        /// <param name="EVSE">An EVSE to delete.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<DeleteEVSEResult>

            DeleteEVSE(IEVSE              EVSE,

                       DateTime?          Timestamp,
                       EventTracking_Id?  EventTrackingId,
                       TimeSpan?          RequestTimeout,
                       CancellationToken  CancellationToken)

                => Task.FromResult(
                       DeleteEVSEResult.NoOperation(
                           EVSE,
                           EventTrackingId,
                           Id,
                           this
                       )
                   );

        #endregion


        #region AddEVSEs          (EVSEs, ...)

        /// <summary>
        /// Add the given enumeration of EVSEs.
        /// </summary>
        /// <param name="EVSEs">An enumeration of EVSEs to add.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        public virtual Task<AddEVSEsResult>

            AddEVSEs(IEnumerable<IEVSE>  EVSEs,

                     DateTime?           Timestamp,
                     EventTracking_Id?   EventTrackingId,
                     TimeSpan?           RequestTimeout,
                     CancellationToken   CancellationToken)

                => Task.FromResult(
                       AddEVSEsResult.NoOperation(
                           EVSEs,
                           Id,
                           this,
                           EventTrackingId
                       )
                   );

        #endregion

        #region AddEVSEsIfNotExist(EVSEs, ...)

        /// <summary>
        /// Add the given enumeration of EVSEs, if they do not already exist.
        /// </summary>
        /// <param name="EVSEs">An enumeration of EVSEs to add, if they do not already exist.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        public virtual Task<AddEVSEsResult>

            AddEVSEsIfNotExist(IEnumerable<IEVSE>  EVSEs,

                               DateTime?           Timestamp,
                               EventTracking_Id?   EventTrackingId,
                               TimeSpan?           RequestTimeout,
                               CancellationToken   CancellationToken)

                => Task.FromResult(
                       AddEVSEsResult.NoOperation(
                           EVSEs,
                           Id,
                           this,
                           EventTrackingId
                       )
                   );

        #endregion

        #region AddOrUpdateEVSEs  (EVSEs, ...)

        /// <summary>
        /// Add or update the given enumeration of EVSEs.
        /// </summary>
        /// <param name="EVSEs">An enumeration of EVSEs to add or update.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<AddOrUpdateEVSEsResult>

            AddOrUpdateEVSEs(IEnumerable<IEVSE>  EVSEs,

                             DateTime?           Timestamp,
                             EventTracking_Id?   EventTrackingId,
                             TimeSpan?           RequestTimeout,
                             CancellationToken   CancellationToken)

                => Task.FromResult(
                       AddOrUpdateEVSEsResult.NoOperation(
                           EVSEs,
                           Id,
                           this,
                           EventTrackingId
                       )
                   );

        #endregion

        #region UpdateEVSEs       (EVSEs, ...)

        /// <summary>
        /// Update the given enumeration of EVSEs.
        /// </summary>
        /// <param name="EVSEs">An enumeration of EVSEs to update.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<UpdateEVSEsResult>

            UpdateEVSEs(IEnumerable<IEVSE>  EVSEs,

                        DateTime?           Timestamp,
                        EventTracking_Id?   EventTrackingId,
                        TimeSpan?           RequestTimeout,
                        CancellationToken   CancellationToken)

                => Task.FromResult(
                       UpdateEVSEsResult.NoOperation(
                           EVSEs,
                           Id,
                           this,
                           EventTrackingId
                       )
                   );

        #endregion

        #region DeleteEVSEs       (EVSEs, ...)

        /// <summary>
        /// Delete the given enumeration of EVSEs.
        /// </summary>
        /// <param name="EVSEs">An enumeration of EVSEs to delete.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<DeleteEVSEsResult>

            DeleteEVSEs(IEnumerable<IEVSE>  EVSEs,

                        DateTime?           Timestamp,
                        EventTracking_Id?   EventTrackingId,
                        TimeSpan?           RequestTimeout,
                        CancellationToken   CancellationToken)

                => Task.FromResult(
                       DeleteEVSEsResult.NoOperation(
                           EVSEs,
                           Id,
                           this,
                           EventTrackingId
                       )
                   );

        #endregion


        #region UpdateEVSEAdminStatus (AdminStatusUpdates, ...)

        /// <summary>
        /// Update the given enumeration of EVSE admin status updates.
        /// </summary>
        /// <param name="AdminStatusUpdates">An enumeration of EVSE admin status updates.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<PushEVSEAdminStatusResult>

            UpdateEVSEAdminStatus(IEnumerable<EVSEAdminStatusUpdate>  AdminStatusUpdates,

                                  DateTime?                           Timestamp,
                                  EventTracking_Id?                   EventTrackingId,
                                  TimeSpan?                           RequestTimeout,
                                  CancellationToken                   CancellationToken)

                => Task.FromResult(
                       PushEVSEAdminStatusResult.OutOfService(
                           Id,
                           this,
                           AdminStatusUpdates
                       )
                   );

        #endregion

        #region UpdateEVSEStatus      (StatusUpdates, ...)

        /// <summary>
        /// Update the given enumeration of EVSE status updates.
        /// </summary>
        /// <param name="StatusUpdates">An enumeration of EVSE status updates.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public virtual Task<PushEVSEStatusResult>

            UpdateEVSEStatus(IEnumerable<EVSEStatusUpdate>  StatusUpdates,

                             DateTime?                      Timestamp,
                             EventTracking_Id?              EventTrackingId,
                             TimeSpan?                      RequestTimeout,
                             CancellationToken              CancellationToken)

                => Task.FromResult(
                       PushEVSEStatusResult.NoOperation(
                           Id,
                           this
                       )
                   );


        #endregion

        #region UpdateEVSEEnergyStatus(EVSEEnergyStatusUpdates, ...)

        /// <summary>
        /// Update the given enumeration of EVSE energy status.
        /// </summary>
        /// <param name="EVSEEnergyStatusUpdates">An enumeration of EVSE energy status updates.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        public virtual Task<PushEVSEEnergyStatusResult>

            UpdateEVSEEnergyStatus(IEnumerable<EVSEEnergyStatusUpdate>  EVSEEnergyStatusUpdates,

                                   DateTime?                            Timestamp,
                                   EventTracking_Id?                    EventTrackingId,
                                   TimeSpan?                            RequestTimeout,
                                   CancellationToken                    CancellationToken)

                => Task.FromResult(
                       PushEVSEEnergyStatusResult.OutOfService(
                           Id,
                           this,
                           EVSEEnergyStatusUpdates
                       )
                   );

        #endregion

        #endregion


        #region Receive AuthorizeStarts/-Stops

        #region AuthorizeStart(LocalAuthentication, EVSEId,            ChargingProduct = null, SessionId = null, OperatorId = null, ...)

        /// <summary>
        /// Create an AuthorizeStart request at the given EVSE.
        /// </summary>
        /// <param name="LocalAuthentication">An user identification.</param>
        /// <param name="ChargingLocation">The charging location.</param>
        /// <param name="ChargingProduct">An optional charging product.</param>
        /// <param name="SessionId">An optional session identification.</param>
        /// <param name="CPOPartnerSessionId">An optional session identification of the CPO.</param>
        /// <param name="OperatorId">An optional charging station operator identification.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        async Task<AuthStartResult>

            IAuthorizeStartStop.AuthorizeStart(LocalAuthentication          LocalAuthentication,
                                               ChargingLocation?            ChargingLocation,
                                               ChargingProduct?             ChargingProduct,
                                               ChargingSession_Id?          SessionId,
                                               ChargingSession_Id?          CPOPartnerSessionId,
                                               ChargingStationOperator_Id?  OperatorId,

                                               DateTime?                    Timestamp,
                                               EventTracking_Id?            EventTrackingId,
                                               TimeSpan?                    RequestTimeout,
                                               CancellationToken            CancellationToken)

        {

            #region Initial checks

            if (LocalAuthentication  == null)
                throw new ArgumentNullException(nameof(LocalAuthentication),  "The given authentication token must not be null!");

            AuthStartResult result;

            #endregion

            #region Send OnAuthorizeStartRequest event

            var StartTime = DateTime.UtcNow;

            try
            {

                if (OnAuthorizeStartRequest != null)
                    await Task.WhenAll(OnAuthorizeStartRequest.GetInvocationList().
                                       Cast<OnAuthorizeStartRequestDelegate>().
                                       Select(e => e(StartTime,
                                                     Timestamp.Value,
                                                     this,
                                                     Id.ToString(),
                                                     EventTrackingId,
                                                     RoamingNetwork.Id,
                                                     null,
                                                     null,
                                                     OperatorId,
                                                     LocalAuthentication,
                                                     ChargingLocation,
                                                     ChargingProduct,
                                                     SessionId,
                                                     CPOPartnerSessionId,
                                                     new ISendAuthorizeStartStop[0],
                                                     RequestTimeout ?? RequestTimeout.Value))).
                                       ConfigureAwait(false);

            }
            catch (Exception e)
            {
                DebugX.LogException(e, nameof(EMobilityServiceProvider) + "." + nameof(OnAuthorizeStartRequest));
            }

            #endregion


            if (AuthorizationDatabase.TryGetValue(LocalAuthentication, out var authenticationResult))
            {

                #region Authorized

                if (authenticationResult == TokenAuthorizationResultType.Authorized)
                {

                    if (!SessionId.HasValue)
                        SessionId = ChargingSession_Id.NewRandom;

                    SessionDatabase.TryAdd(SessionId.Value, new SessionInfo(LocalAuthentication));

                    result = AuthStartResult.Authorized(Id,
                                                        this,
                                                        null,
                                                        SessionId,
                                                        ProviderId: Id);

                }

                #endregion

                #region Token is blocked!

                else if (authenticationResult == TokenAuthorizationResultType.Blocked)
                    result = AuthStartResult.Blocked(Id,
                                                     this,
                                                     ProviderId:   Id,
                                                     SessionId:    SessionId,
                                                     Description:  I18NString.Create(Languages.en, "Token is blocked!"));

                #endregion

                #region ...fall through!

                else
                    result = AuthStartResult.Unspecified(Id,
                                                         this,
                                                         null,
                                                         SessionId);

                #endregion

            }

            #region Unkown Token!

            else
                result = AuthStartResult.NotAuthorized(Id,
                                                           this,
                                                           ProviderId:   Id,
                                                           SessionId:    SessionId,
                                                           Description:  I18NString.Create(Languages.en, "Unkown token!"));

            #endregion


            #region Send OnAuthorizeEVSEStartResponse event

            var EndTime = DateTime.UtcNow;

            try
            {

                if (OnAuthorizeStartResponse != null)
                    await Task.WhenAll(OnAuthorizeStartResponse.GetInvocationList().
                                       Cast<OnAuthorizeStartResponseDelegate>().
                                       Select(e => e(EndTime,
                                                     Timestamp.Value,
                                                     this,
                                                     Id.ToString(),
                                                     EventTrackingId,
                                                     RoamingNetwork.Id,
                                                     null,
                                                     null,
                                                     OperatorId,
                                                     LocalAuthentication,
                                                     ChargingLocation,
                                                     ChargingProduct,
                                                     SessionId,
                                                     CPOPartnerSessionId,
                                                     new ISendAuthorizeStartStop[0],
                                                     RequestTimeout ?? RequestTimeout.Value,
                                                     result,
                                                     EndTime - StartTime))).
                                       ConfigureAwait(false);

            }
            catch (Exception e)
            {
                DebugX.LogException(e, nameof(EMobilityServiceProvider) + "." + nameof(OnAuthorizeStartResponse));
            }

            #endregion

            return result;

        }

        #endregion

        // UID => Not everybody can stop any session, but maybe another
        //        UID than the UID which started the session!
        //        (e.g. car sharing)

        #region AuthorizeStop (SessionId, LocalAuthentication, EVSEId,            OperatorId = null, ...)

        /// <summary>
        /// Create an authorize stop request at the given EVSE.
        /// </summary>
        /// <param name="SessionId">The session identification from the AuthorizeStart request.</param>
        /// <param name="LocalAuthentication">An user identification.</param>
        /// <param name="ChargingLocation">The charging location.</param>
        /// <param name="CPOPartnerSessionId">An optional session identification of the CPO.</param>
        /// <param name="OperatorId">An optional charging station operator identification.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        async Task<AuthStopResult>

            IAuthorizeStartStop.AuthorizeStop(ChargingSession_Id           SessionId,
                                              LocalAuthentication          LocalAuthentication,
                                              ChargingLocation?            ChargingLocation,
                                              ChargingSession_Id?          CPOPartnerSessionId,
                                              ChargingStationOperator_Id?  OperatorId,

                                              DateTime?                    Timestamp,
                                              EventTracking_Id?            EventTrackingId,
                                              TimeSpan?                    RequestTimeout,
                                              CancellationToken            CancellationToken)

        {

            #region Initial checks

            if (SessionId           == null)
                throw new ArgumentNullException(nameof(SessionId),           "The given charging session identification must not be null!");

            if (LocalAuthentication  == null)
                throw new ArgumentNullException(nameof(LocalAuthentication),  "The given authentication token must not be null!");

            #endregion

            #region Check session identification

            if (!SessionDatabase.TryGetValue(SessionId, out var sessionInfo))
                return AuthStopResult.InvalidSessionId(Id,
                                                       this,
                                                       null,
                                                       SessionId);

            #endregion

            if (AuthorizationDatabase.TryGetValue(LocalAuthentication, out var authenticationResult))
            {

                #region Token is authorized

                if (authenticationResult == TokenAuthorizationResultType.Authorized)
                {

                    // Authorized
                    if (sessionInfo.ListOfAuthStopTokens.Contains(LocalAuthentication))
                        return AuthStopResult.Authorized(Id,
                                                         this,
                                                         SessionId:   SessionId,
                                                         ProviderId:  Id);

                    // Invalid Token for SessionId!
                    else
                        return AuthStopResult.NotAuthorized(Id,
                                                                this,
                                                                SessionId:    SessionId,
                                                                ProviderId:   Id,
                                                                Description:  I18NString.Create(Languages.en, "Invalid token for given session identification!"));

                }

                #endregion

                #region Token is blocked

                else if (authenticationResult == TokenAuthorizationResultType.Blocked)
                    return AuthStopResult.Blocked(Id,
                                                      this,
                                                      SessionId:    SessionId,
                                                      ProviderId:   Id,
                                                      Description:  I18NString.Create(Languages.en,  "Token is blocked!"));

                #endregion

                #region ...fall through!

                else
                    return AuthStopResult.Unspecified(Id,
                                                      this,
                                                      null,
                                                      SessionId);

                #endregion

            }

            #region Unkown Token!

            return AuthStopResult.NotAuthorized(Id,
                                                    this,
                                                    SessionId:    SessionId,
                                                    ProviderId:   Id,
                                                    Description:  I18NString.Create(Languages.en, "Unkown token!"));

            #endregion

        }

        #endregion

        #endregion

        #region ReceiveChargeDetailRecords(ChargeDetailRecords, ...)

        /// <summary>
        /// Send a charge detail record.
        /// </summary>
        /// <param name="ChargeDetailRecords">An enumeration of charge detail records.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        Task<SendCDRsResult>

            IReceiveChargeDetailRecords.ReceiveChargeDetailRecords(IEnumerable<ChargeDetailRecord>  ChargeDetailRecords,

                                                                   DateTime?                        Timestamp,
                                                                   EventTracking_Id?                EventTrackingId,
                                                                   TimeSpan?                        RequestTimeout,
                                                                   CancellationToken                CancellationToken)
        {

            #region Initial checks

            if (ChargeDetailRecords == null)
                throw new ArgumentNullException(nameof(ChargeDetailRecords),  "The given charge detail records must not be null!");

            #endregion

            SessionInfo _SessionInfo = null;


            //Debug.WriteLine("Received a CDR: " + ChargeDetailRecord.SessionId.ToString());


            //if (ChargeDetailRecordDatabase.ContainsKey(ChargeDetailRecord.SessionId))
            //    return SendCDRResult.InvalidSessionId(AuthorizatorId);


            //if (ChargeDetailRecordDatabase.TryAdd(ChargeDetailRecord.SessionId, ChargeDetailRecord))
            //{

            //    SessionDatabase.TryRemove(ChargeDetailRecord.SessionId, out _SessionInfo);

            //    return SendCDRResult.Forwarded(AuthorizatorId);

            //}




            //roamingprovider.OnEVSEStatusPush   += (Timestamp, Sender, SenderId, RoamingNetworkId, ActionType, GroupedEVSEs, NumberOfEVSEs) => {
            //    Console.WriteLine("[" + Timestamp + "] " + RoamingNetworkId.ToString() + ": Pushing " + NumberOfEVSEs + " EVSE status towards " + SenderId + "(" + ActionType + ")");
            //};

            //    Console.WriteLine("[" + Timestamp + "] " + RoamingNetworkId.ToString() + ": Pushed "  + NumberOfEVSEs + " EVSE status towards " + SenderId + "(" + ActionType + ") => " + Result.Result + " (" + Duration.TotalSeconds + " sec)");

            //    if (Result.Result == false)
            //    {

            //        var EMailTask = API_SMTPClient.Send(HubjectEVSEStatusPushFailedEMailProvider(Timestamp,
            //                                                                                       Sender,
            //                                                                                       SenderId,
            //                                                                                       RoamingNetworkId,
            //                                                                                       ActionType,
            //                                                                                       GroupedEVSEs,
            //                                                                                       NumberOfEVSEs,
            //                                                                                       Result,
            //                                                                                       Duration));

            //        EMailTask.Wait(TimeSpan.FromSeconds(30));

            //    }

            //};

            return Task.FromResult(SendCDRsResult.OutOfService(DateTime.UtcNow,
                                                               Id,
                                                               this,
                                                               ChargeDetailRecords));

        }

        #endregion

        #endregion


        #region Outgoing requests towards the roaming network

        //ToDo: Send Tokens!
        //ToDo: Download CDRs!

        #region Reserve(...EVSEId, StartTime, Duration, ReservationId = null, ...)

        /// <summary>
        /// Reserve the possibility to charge at the given EVSE.
        /// </summary>
        /// <param name="EVSEId">The unique identification of the EVSE to be reserved.</param>
        /// <param name="StartTime">The starting time of the reservation.</param>
        /// <param name="Duration">The duration of the reservation.</param>
        /// <param name="ReservationId">An optional unique identification of the reservation. Mandatory for updates.</param>
        /// <param name="RemoteAuthentication">An optional unique identification of e-Mobility account/customer requesting this reservation.</param>
        /// <param name="ChargingProduct">The charging product to be reserved.</param>
        /// <param name="AuthTokens">A list of authentication tokens, who can use this reservation.</param>
        /// <param name="eMAIds">A list of eMobility account identifications, who can use this reservation.</param>
        /// <param name="PINs">A list of PINs, who can be entered into a pinpad to use this reservation.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public async Task<ReservationResult>

            Reserve(ChargingLocation                   ChargingLocation,
                    ChargingReservationLevel           ReservationLevel       = ChargingReservationLevel.EVSE,
                    DateTime?                          ReservationStartTime   = null,
                    TimeSpan?                          Duration               = null,
                    ChargingReservation_Id?            ReservationId          = null,
                    ChargingReservation_Id?            LinkedReservationId    = null,
                    EMobilityProvider_Id?              ProviderId             = null,
                    RemoteAuthentication?              RemoteAuthentication   = null,
                    ChargingProduct?                   ChargingProduct        = null,
                    IEnumerable<AuthenticationToken>?  AuthTokens             = null,
                    IEnumerable<EMobilityAccount_Id>?  eMAIds                 = null,
                    IEnumerable<UInt32>?               PINs                   = null,

                    DateTime?                          Timestamp              = null,
                    EventTracking_Id?                  EventTrackingId        = null,
                    TimeSpan?                          RequestTimeout         = null,
                    CancellationToken                  CancellationToken      = default)

        {

            #region Initial checks

            if (EventTrackingId == null)
                EventTrackingId = EventTracking_Id.New;

            #endregion

            #region Send OnReserveRequest event

            var Runtime = Stopwatch.StartNew();

            try
            {

                //OnReserveRequest?.Invoke(DateTime.UtcNow,
                //                         Timestamp.Value,
                //                         this,
                //                         EventTrackingId,
                //                         RoamingNetwork.Id,
                //                         ReservationId,
                //                         ChargingLocation,
                //                         ReservationStartTime,
                //                         Duration,
                //                         Id,
                //                         RemoteAuthentication,
                //                         ChargingProduct,
                //                         AuthTokens,
                //                         eMAIds,
                //                         PINs,
                //                         RequestTimeout);

            }
            catch (Exception e)
            {
                DebugX.LogException(e, nameof(EMobilityServiceProvider) + "." + nameof(OnReserveRequest));
            }

            #endregion


            var response = await RoamingNetwork.
                                     Reserve(ChargingLocation,
                                             ReservationLevel,
                                             ReservationStartTime,
                                             Duration,
                                             ReservationId,
                                             LinkedReservationId,
                                             Id,
                                             RemoteAuthentication,
                                             ChargingProduct,
                                             AuthTokens,
                                             eMAIds,
                                             PINs,

                                             Timestamp,
                                             EventTrackingId,
                                             RequestTimeout,
                                             CancellationToken);


            #region Send OnReserveResponse event

            Runtime.Stop();

            try
            {

                //OnReserveResponse?.Invoke(DateTime.UtcNow,
                //                          Timestamp.Value,
                //                          this,
                //                          EventTrackingId,
                //                          RoamingNetwork.Id,
                //                          ReservationId,
                //                          ChargingLocation,
                //                          ReservationStartTime,
                //                          Duration,
                //                          Id,
                //                          RemoteAuthentication,
                //                          ChargingProduct,
                //                          AuthTokens,
                //                          eMAIds,
                //                          PINs,
                //                          response,
                //                          Runtime.Elapsed,
                //                          RequestTimeout);

            }
            catch (Exception e)
            {
                DebugX.LogException(e, nameof(EMobilityServiceProvider) + "." + nameof(OnReserveResponse));
            }

            #endregion

            return response;

        }

        #endregion

        #region CancelReservation(...ReservationId, Reason, EVSEId = null, ...)

        /// <summary>
        /// Cancel the given charging reservation.
        /// </summary>
        /// <param name="ReservationId">The unique charging reservation identification.</param>
        /// <param name="Reason">A reason for this cancellation.</param>
        /// <param name="EVSEId">An optional identification of the EVSE.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public async Task<CancelReservationResult>

            CancelReservation(ChargingReservation_Id                 ReservationId,
                              ChargingReservationCancellationReason  Reason,

                              DateTime?                              Timestamp           = null,
                              EventTracking_Id?                      EventTrackingId     = null,
                              TimeSpan?                              RequestTimeout      = null,
                              CancellationToken                      CancellationToken   = default)

        {

            var response = await RoamingNetwork.CancelReservation(ReservationId,
                                                                  Reason,

                                                                  Timestamp,
                                                                  EventTrackingId,
                                                                  RequestTimeout,
                                                                  CancellationToken);


            //var OnCancelReservationResponseLocal = OnCancelReservationResponse;
            //if (OnCancelReservationResponseLocal != null)
            //    OnCancelReservationResponseLocal(DateTime.UtcNow,
            //                                this,
            //                                EventTracking_Id.New,
            //                                ReservationId,
            //                                Reason);

            return response;

        }

        #endregion


        #region RemoteStart(ChargingLocation, ChargingProduct = null, ReservationId = null, SessionId = null, RemoteAuthentication = null, ...)

        /// <summary>
        /// Start a charging session at the given EVSE.
        /// </summary>
        /// <param name="ChargingProduct">The choosen charging product.</param>
        /// <param name="ReservationId">The unique identification for a charging reservation.</param>
        /// <param name="SessionId">The unique identification for this charging session.</param>
        /// <param name="RemoteAuthentication">The unique identification of the e-mobility account.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public async Task<RemoteStartResult>

            RemoteStart(ChargingLocation         ChargingLocation,
                        ChargingProduct?         ChargingProduct        = null,
                        ChargingReservation_Id?  ReservationId          = null,
                        ChargingSession_Id?      SessionId              = null,
                        EMobilityProvider_Id?    ProviderId             = null,
                        RemoteAuthentication?    RemoteAuthentication   = null,

                        DateTime?                Timestamp              = null,
                        EventTracking_Id?        EventTrackingId        = null,
                        TimeSpan?                RequestTimeout         = null,
                        CancellationToken        CancellationToken      = default)

        {

            #region Initial checks

            if (EventTrackingId == null)
                EventTrackingId = EventTracking_Id.New;

            #endregion

            #region Send OnRemoteStartRequest event

            var StartTime = DateTime.UtcNow;

            try
            {

                OnRemoteStartRequest?.Invoke(StartTime,
                                             Timestamp.Value,
                                             this,
                                             EventTrackingId,
                                             RoamingNetwork.Id,
                                             ChargingLocation,
                                             ChargingProduct,
                                             ReservationId,
                                             SessionId,
                                             null,
                                             null,
                                             Id,
                                             RemoteAuthentication,
                                             RequestTimeout);

            }
            catch (Exception e)
            {
                DebugX.LogException(e, nameof(EMobilityServiceProvider) + "." + nameof(OnRemoteStartRequest));
            }

            #endregion


            var response = await RoamingNetwork.
                                     RemoteStart(ChargingLocation,
                                                 ChargingProduct,
                                                 ReservationId,
                                                 SessionId,
                                                 Id,
                                                 RemoteAuthentication,

                                                 Timestamp,
                                                 EventTrackingId,
                                                 RequestTimeout,
                                                 CancellationToken);


            #region Send OnRemoteStartResponse event

            var EndTime = DateTime.UtcNow;

            try
            {

                OnRemoteStartResponse?.Invoke(EndTime,
                                              Timestamp.Value,
                                              this,
                                              EventTrackingId,
                                              RoamingNetwork.Id,
                                              ChargingLocation,
                                              ChargingProduct,
                                              ReservationId,
                                              SessionId,
                                              null,
                                              null,
                                              Id,
                                              RemoteAuthentication,
                                              RequestTimeout,
                                              response,
                                              EndTime - StartTime);

            }
            catch (Exception e)
            {
                DebugX.LogException(e, nameof(EMobilityServiceProvider) + "." + nameof(OnRemoteStartResponse));
            }

            #endregion

            return response;

        }

        #endregion

        #region RemoteStop (SessionId, ReservationHandling, RemoteAuthentication = null, ...)

        /// <summary>
        /// Stop the given charging session at the given EVSE.
        /// </summary>
        /// <param name="SessionId">The unique identification for this charging session.</param>
        /// <param name="ReservationHandling">Whether to remove the reservation after session end, or to keep it open for some more time.</param>
        /// <param name="RemoteAuthentication">The unique identification of the e-mobility account.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public async Task<RemoteStopResult>

            RemoteStop(ChargingSession_Id     SessionId,
                       ReservationHandling?   ReservationHandling    = null,
                       EMobilityProvider_Id?  ProviderId             = null,
                       RemoteAuthentication?  RemoteAuthentication   = null,

                       DateTime?              Timestamp              = null,
                       EventTracking_Id?      EventTrackingId        = null,
                       TimeSpan?              RequestTimeout         = null,
                       CancellationToken      CancellationToken      = default)

        {

            #region Initial checks

            if (EventTrackingId == null)
                EventTrackingId = EventTracking_Id.New;

            #endregion

            #region Send OnRemoteStopRequest event

            var StartTime = DateTime.UtcNow;

            try
            {

                OnRemoteStopRequest?.Invoke(StartTime,
                                            Timestamp.Value,
                                            this,
                                            EventTrackingId,
                                            RoamingNetwork.Id,
                                            SessionId,
                                            ReservationHandling,
                                            null,
                                            null,
                                            Id,
                                            RemoteAuthentication,
                                            RequestTimeout);

            }
            catch (Exception e)
            {
                DebugX.LogException(e, nameof(EMobilityServiceProvider) + "." + nameof(OnRemoteStopRequest));
            }

            #endregion


            var response = await RoamingNetwork.RemoteStop(SessionId,
                                                           ReservationHandling,
                                                           Id,
                                                           RemoteAuthentication,

                                                           Timestamp,
                                                           EventTrackingId,
                                                           RequestTimeout,
                                                           CancellationToken);


            #region Send OnRemoteStopResponse event

            var EndTime = DateTime.UtcNow;

            try
            {

                OnRemoteStopResponse?.Invoke(EndTime,
                                             Timestamp.Value,
                                             this,
                                             EventTrackingId,
                                             RoamingNetwork.Id,
                                             SessionId,
                                             ReservationHandling,
                                             null,
                                             null,
                                             Id,
                                             RemoteAuthentication,
                                             RequestTimeout,
                                             response,
                                             EndTime - StartTime);

            }
            catch (Exception e)
            {
                DebugX.LogException(e, nameof(EMobilityServiceProvider) + "." + nameof(OnRemoteStopResponse));
            }

            #endregion

            return response;

        }

        #endregion

        public bool TryGetChargingReservationById(ChargingReservation_Id ReservationId, out ChargingReservation ChargingReservation)
        {
            throw new NotImplementedException();
        }

        public bool TryGetChargingSessionById(ChargingSession_Id ChargingSessionId, out ChargingSession ChargingSession)
        {
            throw new NotImplementedException();
        }

        #endregion


    }

}
