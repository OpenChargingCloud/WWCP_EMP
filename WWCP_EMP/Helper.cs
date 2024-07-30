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

using org.GraphDefined.Vanaheimr.Illias;

using social.OpenData.UsersAPI;

using cloud.charging.open.protocols.WWCP.EMP;

#endregion

namespace cloud.charging.open.protocols.WWCP
{

    public static class LocalEMobilityServiceExtentions
    {

        public static Task<AddEMobilityProviderResult>

            CreateEMobilityServiceProvider(this RoamingNetwork                RoamingNetwork,
                                           EMobilityProvider_Id               Id,
                                           I18NString                         Name,
                                           I18NString?                        Description       = null,
                                           Action<IEMobilityProvider>?        Configurator      = null,
                                           //eMobilityProviderPriority          Priority          = null,
                                           EMobilityProviderAdminStatusTypes  AdminStatus       = EMobilityProviderAdminStatusTypes.Operational,
                                           EMobilityProviderStatusTypes       Status            = EMobilityProviderStatusTypes.Available,
                                           OnEMobilityProviderAddedDelegate?  OnCreated         = null,
                                           EventTracking_Id?                  EventTrackingId   = null,
                                           User_Id?                           CurrentUserId     = null)

                => RoamingNetwork.CreateEMobilityProvider(
                       Id,
                       Name,
                       Description,
                       //Priority,
                       Configurator,

                       // Remote EMP...
                       emp => new EMobilityServiceProvider(
                                  emp.Id,
                                  emp.RoamingNetwork
                              ),

                       AdminStatus,
                       Status,
                       OnCreated,
                       EventTrackingId,
                       CurrentUserId
                   );

    }

}
