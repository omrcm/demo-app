using BOA.Common.Types;
using BOA.Proxy;
using BOA.Types.InternetBanking;
using BOA.Types.InternetBanking.FX;
using BOA.Types.Kernel.BusinessHelper;
using BOA.Types.Kernel.FX;
using BOA.Types.Kernel.General;
using System.Collections.Generic;
using VK.Fx.DataFeed.Domain.Shared;

namespace VK.Fx.DataFeed.BoaProxy
{
    public class FxOnlineBoaService : IFxOnlineBoaService
    {

        private IBoaClient boaClient;
        public FxOnlineBoaService(IBoaClient boaClient)
        {
            this.boaClient = boaClient;
        }


        public GenericResponse<FxOnlineContract> GetFecList()
        {
            FxOnlineRequest request = new FxOnlineRequest
            {
                MethodName = "GetFecList"
            };
            var resp = boaClient.Execute<FxOnlineRequest, FxOnlineContract>(request);
            return resp;
        }
        public GenericResponse<FxOnlineContract> GetFxParityList()
        {
            FxOnlineRequest request = new FxOnlineRequest
            {
                MethodName = "GetFxParityList"
            };
            var resp = boaClient.Execute<FxOnlineRequest, FxOnlineContract>(request);
            return resp;
        }

        public GenericResponse<FxOnlineContract> GetFxDefinitions()
        {
            FxOnlineRequest request = new FxOnlineRequest
            {
                MethodName = "GetFxDefinitions"
            };
            var resp = boaClient.Execute<FxOnlineRequest, FxOnlineContract>(request);
            return resp;
        }

        public GenericResponse<FxOnlineContract> GetFxRateList()
        {
            FxOnlineRequest request = new FxOnlineRequest
            {
                MethodName = "GetFxRateList"
            };
            var resp = boaClient.Execute<FxOnlineRequest, FxOnlineContract>(request);
            return resp;
        }

        public GenericResponse<List<ParameterContract>> GetParameters()
        {
            var requestParameter = new ParameterRequest()
            {
                MethodName = "GetParameters",
                ParameterContract = new ParameterContract()
                {
                    ParamType = "FXFecList"
                }
            };
            var responseParameter = boaClient.Execute<ParameterRequest, List<ParameterContract>>(requestParameter);

            if (responseParameter.Success && responseParameter.Value != null && responseParameter.Value.Count > 0)
            {
                var willRemoveList = new List<ParameterContract>();
                foreach (var param in responseParameter.Value)
                {
                    if (!string.IsNullOrEmpty(param.ParamValue4))
                        param.ParamValue4 = "";
                    if (string.IsNullOrEmpty(param.ParamValue3) || !param.ParamValue3.Equals("1"))
                        willRemoveList.Add(param);
                }
                if (willRemoveList.Count > 0)
                {
                    responseParameter.Value.RemoveAll(x => willRemoveList.Exists(y => y.ParamCode.Equals(x.ParamCode)));
                }
            }
            return responseParameter;
        }



        public List<FXSymbolGroupDto> GetSymbolGroupList()
        {
            FxOnlineRequest request = new FxOnlineRequest
            {
                MethodName = "GetFxGroups"
            };
            var responseSymbolGroupList = boaClient.Execute<FxOnlineRequest, List<FXSpecificBranchRateGroupContract>>(request);
            var fxGroupList = new List<FXSymbolGroupDto>() {
                   new FXSymbolGroupDto() { Id = 0, GroupCode = "Default", GroupId = 0 , GroupAdvantage = null }
                };

            if (responseSymbolGroupList.Value != null && responseSymbolGroupList.Value.Count > 0)
            {
                foreach (var group in responseSymbolGroupList.Value)
                {
                    fxGroupList.Add(new FXSymbolGroupDto() { Id = fxGroupList.Count, GroupCode = group.GroupCode, GroupId = group.FXSpecificBranchRateGroupId, GroupAdvantage = group });
                }
            }
            return fxGroupList;
        }
    }
}
