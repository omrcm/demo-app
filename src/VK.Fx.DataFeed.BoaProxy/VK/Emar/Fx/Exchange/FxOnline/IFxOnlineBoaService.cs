using BOA.Common.Types;
using BOA.Types.InternetBanking;
using BOA.Types.InternetBanking.FX;
using BOA.Types.Kernel.General;
using System.Collections.Generic;
using VK.Fx.DataFeed.Domain.Shared;

namespace VK.Fx.DataFeed.BoaProxy
{
    public interface IFxOnlineBoaService
    {
        public GenericResponse<FxOnlineContract> GetFecList();

        public GenericResponse<FxOnlineContract> GetFxParityList();
        public GenericResponse<FxOnlineContract> GetFxDefinitions();
        public GenericResponse<FxOnlineContract> GetFxRateList();
        public GenericResponse<List<ParameterContract>> GetParameters();

        public List<FXSymbolGroupDto> GetSymbolGroupList();
    }
}
