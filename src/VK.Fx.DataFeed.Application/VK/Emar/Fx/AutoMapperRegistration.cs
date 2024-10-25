using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VK.Fx.DataFeed.Domain.Shared;

namespace VK.Fx.DataFeed.Application
{
    public class AutoMapperRegistration : Profile
    {
        public AutoMapperRegistration()
        {
            CreateMap<FXSymbolRateDto, FXSymbolDto>()
                .ForMember(d => d.Fec1, o => o.MapFrom(s => s.Fec1))
                  .ForMember(d => d.Fec2, o => o.MapFrom(s => s.Fec2))
                  .ForMember(d => d.Bid, o => o.MapFrom(s => s.Sell))
                  .ForMember(d => d.Ask, o => o.MapFrom(s => s.Buy))
                  .ForMember(d => d.CreateDate, o => o.MapFrom(s => DateTime.Now.ToString()))
                  .ForMember(d => d.BaseFECTLRate, o => o.MapFrom(s => s.BaseFECTLRate))
                  .ForMember(d => d.OtherFECTLRate, o => o.MapFrom(s => s.OtherFECTLRate))
                .ReverseMap();
        }
    }
}
