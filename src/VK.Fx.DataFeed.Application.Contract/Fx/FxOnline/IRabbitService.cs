using BOA.Types.InternetBanking.FX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BOA.Common.Types;
using BOA.Types.InternetBanking;
using BOA.Types.Kernel.General;
using VK.Fx.DataFeed.Domain.Shared;

namespace VK.Fx.DataFeed.Application.Contract
{
    public interface IRabbitService
    {
        public string Post(string group,string topic, FXSymbolDto symbolDto);
        public string BathcPost(List<FXSymbolDto> symbolDtoList, string group);
    }
}
