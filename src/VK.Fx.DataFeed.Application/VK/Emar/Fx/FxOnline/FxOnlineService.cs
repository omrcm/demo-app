using BOA.Common.Types;
using BOA.Types.InternetBanking;
using BOA.Types.Kernel.FX;
using BOA.Types.Kernel.General;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VK.Emar.Application.Services;
using VK.Emar.Extensions.DependencyInjection;
using VK.Emar.Mapping;
using VK.Fx.DataFeed.Application.Contract;
using VK.Fx.DataFeed.BoaProxy;
using VK.Fx.DataFeed.Domain.Shared;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace VK.Fx.DataFeed.Application
{
    public class FxOnlineService : ApplicationService, IFxOnlineService
    {

        private readonly IFxOnlineBoaService _fxOnlineBoaService;
        private readonly IMapper _mapper;
        private readonly IRabbitService _rabbitService;
        private readonly IDistributedCache _cacheManager;

        public FxOnlineService(ILazyServiceProvider lazyServiceProvider,
            IFxOnlineBoaService fxOnlineBoaService,
            IMapper mapper,
            IRabbitService rabbitService,
            IDistributedCache cacheManager
            ) : base(lazyServiceProvider)
        {
            _fxOnlineBoaService = fxOnlineBoaService;
            _mapper = mapper;
            _rabbitService = rabbitService;
            _cacheManager = cacheManager;
        }

        public async Task GetSymbolFeedAsync()
        {
            try
            {
                var uniqueKey = Guid.NewGuid().ToString();
                var responseFECList = GetFecList();//1 günlük cache
                var responseFxParityList = GetFxParityList();//1 günlük cache
                var responseFxDefinitions = GetFxDefinitions();//1 satlik cache
                var responseFxRateList = GetFxRateList();
                var responseParameter = GetParameters();//1 günlük cache
                IList<FXSymbolGroupDto> groups = GetSymbolGroupList();//3 saatlik cache
                var fecList1 = new List<short>();
                var fecList2 = new List<short>();
                foreach (var parameter in responseParameter.OrderBy(x => int.Parse(x.ParamCode)))
                {
                    var fec1 = short.Parse(parameter.ParamValue);
                    var fec2 = short.Parse(parameter.ParamValue2);
                    //if (fec1 == 60 || fec2 == 60) continue;
                    fecList1.Add(fec1);
                    fecList2.Add(fec2);
                }
                if (responseFECList != null && responseFxRateList != null && responseFxRateList.Value != null)
                {
                    var definitions = new FxOnlineContract()
                    {
                        FECList = responseFECList,
                        FXPartiyList = responseFxParityList,
                        FXDefinitionList = responseFxDefinitions.FXDefinitionList,
                        FXIndexList = responseFxDefinitions.FXIndexList,
                        FXLimitList = responseFxDefinitions.FXLimitList,
                        IsAdditionalSpreadActive = responseFxDefinitions.IsAdditionalSpreadActive,
                        FXRateList = responseFxRateList.Value.FXRateList
                    };



                    groups = groups.ToList();
                    await Task.Run(() =>
                    {
                        Parallel.ForEach(groups, group =>
                        {
                            IList<FXSymbolRateDto> calculatedFxRateList = null;
                            try
                            {
                                calculatedFxRateList = GetFXRateList(uniqueKey, definitions, group.GroupAdvantage, fecList1, fecList2, 1, false, 0, DateTime.Now.Date);
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError("CalcError:" + ex.Message);
                                return;

                            }
                            if (calculatedFxRateList == null || calculatedFxRateList.Count == 0)
                            {
                                Logger.LogError("CalcError: Kur hesaplama hatası oluştu!" + uniqueKey);

                                //return await response;
                                return;
                            }

                            group.FxRateList = calculatedFxRateList.ToList();

                            SetCache<FXSymbolGroupDto>("Unique_" + group.GroupId + "_" + uniqueKey, group, TimeSpan.FromMinutes(2));
                            var symbols = _mapper.MapTo<List<FXSymbolDto>>(group.FxRateList);

                            var resp = _rabbitService.BathcPost(symbols, group.GroupId.ToString());
                            if (!string.IsNullOrEmpty(resp))
                            {

                                Logger.LogError("Rabbit Error: " + resp);
                            }

                            //foreach (var symbol in symbols)
                            //{

                            //    //FXSymbolDto symbolItem = _symbolService.GetDailyDetail(new GetFXSymbolQuery()
                            //    //{
                            //    //    Fec1 = symbol.Fec1,
                            //    //    Fec2 = symbol.Fec2
                            //    //}, symbolItem, group.GroupId);
                            //    string topicname = string.Format("{0}/{1}_{2}", group.GroupId, symbol.Fec1, symbol.Fec2);

                            //    _rabbitService.Post(group.GroupId.ToString(),topicname, symbol);

                            //}
                        });
                    });
                }



            }
            catch (Exception Ex)
            {

            }
        }
        private List<FECContract> GetFecList()
        {
            string cacheKey = "FecList";


            var cache = GetCache<List<FECContract>>(cacheKey);
            if (cache != null)
                return cache;
            else
            {
                var resp = _fxOnlineBoaService.GetFecList();

                if (resp.Value != null && resp.Value.FECList != null && resp.Value.FECList.Any())
                {
                    SetCache(cacheKey, resp.Value.FECList, TimeSpan.FromDays(1));//1 günlük cache
                    return resp.Value.FECList;

                }
                else
                    return null;
            }
        }

        private List<FXParityBaseContract> GetFxParityList()
        {
            string cacheKey = "FxParityList";
            var cache = GetCache<List<FXParityBaseContract>>(cacheKey);
            if (cache != null)
                return cache;
            else
            {
                var resp = _fxOnlineBoaService.GetFxParityList();

                if (resp.Value != null && resp.Value.FXPartiyList != null && resp.Value.FXPartiyList.Any())
                {
                    SetCache(cacheKey, resp.Value.FXPartiyList, TimeSpan.FromDays(1));//1 günlük cache
                    return resp.Value.FXPartiyList;

                }
                else
                    return null;
            }

        }
        private FxOnlineContract GetFxDefinitions()
        {
            string cacheKey = "FxDefinitions";
            var cache = GetCache<FxOnlineContract>(cacheKey);
            if (cache != null)
                return cache;
            else
            {
                var resp = _fxOnlineBoaService.GetFxDefinitions();

                if (resp.Value != null)
                {
                    SetCache(cacheKey, resp.Value, TimeSpan.FromMinutes(60));//1 saatlik cache
                    return resp.Value;

                }
                else
                    return null;
            }
        }

        private GenericResponse<FxOnlineContract> GetFxRateList()
        {
            return _fxOnlineBoaService.GetFxRateList();
        }

        private List<ParameterContract> GetParameters()
        {
            string cacheKey = "Parameters";
            var cache = GetCache<List<ParameterContract>>(cacheKey);
            if (cache != null)
                return cache;
            else
            {
                var resp = _fxOnlineBoaService.GetParameters();

                if (resp.Value != null)
                {
                    SetCache(cacheKey, resp.Value, TimeSpan.FromDays(1));//1 günlük cache
                    return resp.Value;

                }
                else
                    return null;
            }
        }

        private List<FXSymbolGroupDto> GetSymbolGroupList()
        {
            string cacheKey = "FxGroups";
            var cache = GetCache<List<FXSymbolGroupDto>>(cacheKey);
            if (cache != null)
                return cache;
            else
            {
                var resp = _fxOnlineBoaService.GetSymbolGroupList();

                if (resp != null)
                {
                    SetCache(cacheKey, resp, TimeSpan.FromHours(3));//3 saat cache
                    return resp;

                }
                else
                    return null;
            }
        }
        #region SOKET KUR HESAPLAMASI
        private IList<FXSymbolRateDto> GetFXRateList(string uniqueKey,
  FxOnlineContract definitions, BOA.Types.Kernel.FX.FXSpecificBranchRateGroupContract groupAdvantage, List<short> FEC1List,
  List<short> FEC2List, decimal TranAmount, bool IsEffectiveTransaction,
  short FXKey, DateTime MaturityDate)
        {
            FXSymbolRateDto response;
            var returnObject = new List<FXSymbolRateDto>();
            for (int i = 0; i < FEC1List.Count; i++)
            {
                try
                {
                    var fec1 = FEC1List[i];
                    var fec2 = FEC2List[i];

                    string resourceCode = GetResourceCode(definitions, fec1, fec2);
                    var responseSellRate = CalculateFXRate(definitions, groupAdvantage, fec1, fec2, TranAmount, resourceCode, IsEffectiveTransaction, FXKey, MaturityDate);
                    if (responseSellRate == null)
                    {
                        Logger.LogError("SymbolService_GetFXRateList_CalculateFXRate fec1=" + fec1 + ",fec2=" + fec2);
                        continue;
                    }

                    resourceCode = GetResourceCode(definitions, fec2, fec1);
                    var responseBuyRate = CalculateFXRate(definitions, groupAdvantage, fec2, fec1, TranAmount, resourceCode, IsEffectiveTransaction, FXKey, MaturityDate);
                    if (responseBuyRate == null)
                    {
                        //returnObject.Results.AddRange(responseFXRate.Results);
                        Logger.LogError("SymbolService_GetFXRateList_CalculateFXRate fec1=" + fec1 + ",fec2=" + fec2);
                        continue;
                    }
                    //rnd.Next((int)responseSellRate.TranFXRate - 3, (int)responseSellRate.TranFXRate - 3),
                    //if (_env.IsEnvironment("Development"))
                    //{
                    //    Random rnd = new Random();
                    //    response = new FXSymbolRateDto()
                    //    {
                    //        UniqueKey = uniqueKey,
                    //        BaseFECId = responseBuyRate.BaseFEC.Value,
                    //        Fec1 = fec1,
                    //        Fec2 = fec2,
                    //        Buy = decimal.Round(rnd.Next((int)responseBuyRate.TranFXRate, (int)responseBuyRate.TranFXRate + 4), 2),
                    //        BuyBranchCost = responseBuyRate.BranchCostRate,
                    //        Sell = decimal.Round(rnd.Next((int)responseSellRate.TranFXRate, (int)responseSellRate.TranFXRate + 4), 2),
                    //        SellBranchCost = responseSellRate.BranchCostRate,
                    //        BuyFullRate = responseBuyRate,
                    //        SellFullRate = responseSellRate
                    //    };
                    //}
                    //else
                    //{
                    response = new FXSymbolRateDto()
                    {
                        UniqueKey = uniqueKey,
                        BaseFECId = responseBuyRate.BaseFEC.Value,
                        Fec1 = fec1,
                        Fec2 = fec2,
                        Buy = responseBuyRate.TranFXRate,
                        BuyBranchCost = responseBuyRate.BranchCostRate,
                        Sell = responseSellRate.TranFXRate,
                        SellBranchCost = responseSellRate.BranchCostRate,
                        BuyFullRate = responseBuyRate,
                        SellFullRate = responseSellRate
                    };
                    //}

                    returnObject.Add(response);
                }
                catch (Exception ex)
                {
                    var tracce = string.Empty;
                    if (!string.IsNullOrEmpty(ex.StackTrace))
                    {
                        tracce = " StackTrace: " + ex.StackTrace;
                    }
                    Logger.LogError("SymbolService_GetFXRateList2 index=" + i + ", " + ex.Message + tracce);
                }
            }

            if (returnObject != null && returnObject.Count > 0)
            {
                for (int i = 0; i < returnObject.Count; i++)
                {
                    var fec1 = returnObject[i].BaseFECId;
                    var fec2 = returnObject[i].Fec2;
                    var baseFecTLRate = returnObject.FirstOrDefault(x => x.BaseFECId == fec1 && x.Fec2 == (short)FECEnum.TL);
                    if (baseFecTLRate != null)
                    {
                        returnObject[i].BaseFECTLRate = baseFecTLRate.Buy;
                    }
                    var otherFecTLRate = returnObject.FirstOrDefault(x => x.BaseFECId == fec2 && x.Fec2 == (short)FECEnum.TL);
                    if (otherFecTLRate != null)
                    {
                        returnObject[i].OtherFECTLRate = otherFecTLRate.Buy;
                    }
                }
            }

            return returnObject;
        }

        private string GetResourceCode(FxOnlineContract definitions, short fec1, short fec2)
        {
            string resourceCode = string.Empty;

            var fecDef1 = definitions.FECList.FirstOrDefault(x => x.FecId == fec1);
            var fecDef2 = definitions.FECList.FirstOrDefault(x => x.FecId == fec2);

            if (fecDef1.FecGroup == 1 && fecDef2.FecGroup == 1)
            {
                resourceCode = "INTTARBITR";
            }
            else if (fecDef1.FecGroup == 2 && fecDef2.FecGroup != 2)
            {
                resourceCode = "INTTKMSTIS";
            }
            else if (fecDef1.FecGroup != 2 && fecDef2.FecGroup == 2)
            {
                resourceCode = "INTTKMALIS";
            }
            else if (fecDef1.FecGroup == 1 && fecDef2.FecGroup == 0)
            {
                resourceCode = "INTTDVZSTS";
            }
            else if (fecDef1.FecGroup == 0 && fecDef2.FecGroup == 1)
            {
                resourceCode = "INTTDVZALS";
            }

            return resourceCode;
        }

        private FXComponentContract CalculateFXRate(FxOnlineContract definitions, BOA.Types.Kernel.FX.FXSpecificBranchRateGroupContract groupAdvantage,
    short FEC1, short FEC2, decimal TranAmount, string ResourceCode,
    bool IsEffectiveTransaction, short FXKey, DateTime MaturityDate)
        {
            if (FEC1 == FEC2)
            {
                //returnObject.Results.Add(new Result() { ErrorMessage = BOA.Messages.FX.DoNotTransactionOfSameFEC, Severity = Severity.Error, ErrorCode = BOA.Messages.FX.DoNotTransactionOfSameFECCode });
                return null;
            }

            var FXCC = new FXComponentContract();

            short channel = (short)ChannelContract.Internet;
            bool IsBase = false;

            #region 1. Baz döviz ve karşı döviz bulunur

            short pairFEC = -1;
            var fxParityBase = GetFXParity(definitions, FEC1, FEC2);
            if (fxParityBase == null)
            {
                //returnObject.Results.Add(new Result() { ErrorMessage = Messages.FX.FXNotFoundOfParityBaseList, Severity = Severity.Error });
                return null;
            }
            if (fxParityBase.ParityBase == 1)
            {
                FXCC.BaseFEC = FEC1;
                pairFEC = FEC2;
            }
            else
            {
                FXCC.BaseFEC = FEC2;
                pairFEC = FEC1;
            }
            if (fxParityBase.InUse == 0)
            {
                //returnObject.Results.Add(new Result() { ErrorMessage = Messages.FX.DoNotTransactionOfSelectedFEC, Severity = Severity.Error, ErrorCode = Messages.FX.DoNotTransactionOfSelectedFECCode });
                return null;
            }

            #endregion

            #region 2. İşlem tipi ve grubuna karar verebilmek için TranType ve FECGroup Bulunur

            short FEC1Group = definitions.FECList.Find(a => a.FecId == FEC1).FecGroup;
            short FEC2Group = definitions.FECList.Find(a => a.FecId == FEC2).FecGroup;
            if (FEC1Group == 0 && FEC2Group == 1)
            {
                FXCC.FECGroup = 0; FXCC.TranType = 2;
            }
            else if (FEC1Group == 1 && FEC2Group == 0)
            {
                FXCC.FECGroup = 0; FXCC.TranType = 1;
            }
            else if (FEC1Group == 1 && FEC2Group == 1)
            {
                FXCC.FECGroup = 1;
                if (FEC1 == FXCC.BaseFEC)
                    FXCC.TranType = 1;
                else if (FEC2 == FXCC.BaseFEC)
                    FXCC.TranType = 2;
            }
            else if (FEC1Group == 2 && FEC2Group == 1)
            {
                FXCC.FECGroup = 2; FXCC.TranType = 1;
            }
            else if (FEC1Group == 1 && FEC2Group == 2)
            {
                FXCC.FECGroup = 2; FXCC.TranType = 2;
            }
            else if (FEC1Group == 2 && FEC2Group == 2)
            {
                FXCC.FECGroup = 2;
                if (FEC1 == FXCC.BaseFEC)
                    FXCC.TranType = 1;
                if (FEC2 == FXCC.BaseFEC)
                    FXCC.TranType = 2;
            }
            else if (FEC1Group == 0 && FEC2Group == 2)
            {
                FXCC.FECGroup = 0; FXCC.TranType = 2;
            }
            else if (FEC1Group == 2 && FEC2Group == 0)
            {
                FXCC.FECGroup = 0; FXCC.TranType = 1;
            }

            #endregion

            #region 3. Baz işlemmi değilmi karar verilir (IsBase)

            IsBase = FEC1 == FXCC.BaseFEC;

            #endregion

            #region 4. Ekran,Kanal,İşlem Tipi ve Döviz Grubu bilgilerine göre kur tanımı bulunur

            if (FXKey < 2)
                FXKey = 1;

            var fxDC = GetFXDefinition(definitions, ResourceCode, FXCC.TranType, channel, FXKey, FXCC.FECGroup);
            if (fxDC == null)
            {
                //returnObject.Results.Add(new Result() { ErrorMessage = BOA.Messages.FX.DoNotDefinedFXDefinition, ErrorCode = BOA.Messages.FX.DoNotDefinedFXDefinitionCode, Severity = Severity.Error });
                return null;
            }
            FXCC.FXID = fxDC.FXId;
            FXCC.ProfitOrLossCoef = fxDC.ProfitOrLossCoef;
            //FXCC.UseBranchRateForLimitOnRef = fxDC.UseBranchRateForLimitOnRef;

            #endregion

            #region 5. Baz Kura göre FXRate tablosundan kurlar bulunur

            List<FXEngineContract> FXEngineContractList;
            FXEngineContract FXECLowAmountRate;
            GenericResponse<List<FXEngineContract>> responseFXEngine = null;

            #region Ek marjsız kur isteniyorsa ve ek marj açık ise sistemde yer alan en son ki marjsız kur bilgisi getirilir.

            /*
            if (isSpreadActive && fxDC.UseAdditionalCoefficient)
            {
                //FXRATEDELTA tablosundan marjsız verilen  en son ki kur değerlerini getirir.hkarada  01062015
                responseFXEngine = boFXEngine.GetLastFXDeltaWithNoMargine(FXCC.BaseFEC.Value);
                if (!responseFXEngine.Success)
                {
                    returnObject.Results.AddRange(responseFXEngine.Results);
                    return returnObject;
                }
                FXEngineContractList = responseFXEngine.Value;

                if (FXEngineContractList == null || FXEngineContractList.Count == 0)
                {
                    returnObject.Results.Add(new Result() { ErrorMessage = BOA.Messages.FX.FXNotFound, ErrorCode = BOA.Messages.FX.FXNotFoundCode, Severity = Severity.Error });
                    return returnObject;
                }
                FXCC.FXEngineContractList = FXEngineContractList;
                //FXECBankRate = FXEngineContractList.Find(a => a.IndexType == (short)Contract.FXIndexContract.BanksPrice);
                //if (FXECBankRate == null)
                //{
                //    returnObject.Results.Add(new Result() { ErrorMessage = BOA.Messages.FX.FXNotFoundOfBankRate, ErrorCode = BOA.Messages.FX.FXNotFoundOfBankRateCode, Severity = Severity.Error });
                //    return returnObject;
                //}
                FXECLowAmountRate = FXEngineContractList.Find(a => a.IndexType == (short)Contract.FXIndexContract.SmallAmountRate);
                if (FXECLowAmountRate == null)
                {
                    returnObject.Results.Add(new Result() { ErrorMessage = BOA.Messages.FX.FXNotFoundOfLowAmountRate, ErrorCode = BOA.Messages.FX.FXNotFoundOfLowAmountRateCode, Severity = Severity.Error });
                    return returnObject;
                }
            }
            else*/
            //Sistemde yer alan kur bilgisini(marjlı/matjsız) olduğu gibi döner.
            {
                FXEngineContractList = GetFXList(definitions, FXCC.BaseFEC.Value);
                if (FXEngineContractList == null || FXEngineContractList.Count == 0)
                {
                    //returnObject.Results.Add(new Result() { ErrorMessage = BOA.Messages.FX.FXNotFound, ErrorCode = BOA.Messages.FX.FXNotFoundCode, Severity = Severity.Error });
                    return null;
                }
                FXCC.FXEngineContractList = FXEngineContractList;
                //FXECCentralBankRate = FXEngineContractList.Find(a => a.IndexType == (short)Contract.FXIndexContract.CentralBankPrice);//3
                //if (FXECCentralBankRate == null)
                //{
                //    returnObject.Results.Add(new Result() { ErrorMessage = BOA.Messages.FX.FXNotFoundOfCentralBankRate, ErrorCode = BOA.Messages.FX.FXNotFoundOfCentralBankRateCode, Severity = Severity.Error });
                //    return returnObject;
                //}
                //FXECBankRate = FXEngineContractList.Find(a => a.IndexType == (short)Contract.FXIndexContract.BanksPrice);//5
                //if (FXECBankRate == null)
                //{
                //    returnObject.Results.Add(new Result() { ErrorMessage = BOA.Messages.FX.FXNotFoundOfBankRate, ErrorCode = BOA.Messages.FX.FXNotFoundOfBankRateCode, Severity = Severity.Error });
                //    return returnObject;
                //}
                //IndxType=7	Düşük Miktarlı Döviz İşlem Kuru bfixte bu değer ezilmiyor buraya dikkat ediniz.
                FXECLowAmountRate = FXEngineContractList.Find(a => a.IndexType == (short)Contract.FXIndexContract.SmallAmountRate);//7
                if (FXECLowAmountRate == null)
                {
                    //returnObject.Results.Add(new Result() { ErrorMessage = BOA.Messages.FX.FXNotFoundOfLowAmountRate, ErrorCode = BOA.Messages.FX.FXNotFoundOfLowAmountRateCode, Severity = Severity.Error });
                    return null;
                }
            }

            #endregion

            #endregion

            #region 6. Min ve Max refrerans işlem limitlerinin karşılıkları bulunur

            decimal usdEqu = GetUsdEquivalent(definitions, FXCC.BaseFEC.Value);
            FXCC.USDEquivalentRate = usdEqu;
            FXCC.MinTranAmountForRef = usdEqu != 0 ? fxDC.MinTranAmountForRef / usdEqu : fxDC.MinTranAmountForRef;
            FXCC.MaxTranAmountForRef = usdEqu != 0 ? fxDC.MaxTranAmountForRef / usdEqu : fxDC.MaxTranAmountForRef;

            #endregion

            #region 9. Kur tanımına ve Baz Kura  bağlı olarak Limit ve Index Kayıtları Getirilir Coef katsayıları bulunur

            var fxLimit = GetFXLimit(definitions, fxDC.FXId, FXCC.BaseFEC.Value);
            var fxIndex = GetFXIndex(definitions, fxDC.FXId, FXCC.AccountClass);
            if (fxIndex.IndexType == 0)
                fxIndex.IndexType = 5;
            if (fxIndex.CostIndexType == 0)
                fxIndex.CostIndexType = 2;
            if (fxIndex.CostIndexCoef == 0)
                fxIndex.CostIndexCoef = 1;
            if (fxLimit.CoefForMinTranAmount == 0)
                fxLimit.CoefForMinTranAmount = 1;
            decimal coef = 1;
            // NOTE : IsBase değil ise TranAmount doğrudan böyle kontrol edilemez. -erkan
            if (TranAmount <= fxLimit.MinAmountForIndex)
            {
                coef = fxLimit.CoefForMinTranAmount;
            }
            else
            {
                coef = fxIndex.IndexCoef;
            }

            // Marjlar açık ise ve varsayılan ek marjlar özelleştirilmek isteniyorsa
            if (definitions.IsAdditionalSpreadActive && fxDC.UseAdditionalCoefficient)
            {
                coef *= fxDC.AdditionalCoefficient;
            }
            FXCC.IndexCoef = fxIndex.IndexCoef;
            FXCC.CostIndexCoef = fxIndex.CostIndexCoef;
            FXCC.CostIndexType = fxIndex.CostIndexType;
            FXCC.CoefForMinTranAmount = fxLimit.CoefForMinTranAmount;
            FXCC.MinAmountForIndex = fxLimit.MinAmountForIndex;
            FXCC.IndexType = (short)Contract.FXIndexContract.FxOnlineMarketPrice;
            //Şube Maliyet Kuru Bulunur
            var FXECBranchCostRate = FXEngineContractList.Find(a => a.IndexType == FXCC.CostIndexType);
            if (FXECBranchCostRate == null)
            {
                //returnObject.Results.Add(new Result() { ErrorMessage = BOA.Messages.FX.FXNotFoundOfBranchFXRate, ErrorCode = BOA.Messages.FX.FXNotFoundOfBranchFXRateCode, Severity = Severity.Error });
                return null;
            }

            #endregion

            #region 11. AccountNumber a göre Müşreti Avantajı Bulunur

            FXCC.CustomerAdvantage = 0;

            #region Müşteriye özel şube maliyet kuru varsa bulunur.

            decimal SpecialBranchRateCoef = 0;
            decimal SpecialTranFXRateBidCoef = 0;
            decimal SpecialTranFXRateAskCoef = 0;
            if (fxDC.UseCustomerAdvantage && !definitions.IsAdditionalSpreadActive)// marjlar açık iken avantajlı kur verme.(hkarada)
            {
                if (groupAdvantage != null)
                {
                    SpecialBranchRateCoef = groupAdvantage.BranchRateCoef;
                    SpecialTranFXRateBidCoef = FXCC.TranType == 1 ? groupAdvantage.SpecialTranFXRateCoefBid : groupAdvantage.SpecialTranFXRateCoefBid;
                    SpecialTranFXRateAskCoef = FXCC.TranType == 1 ? groupAdvantage.SpecialTranFXRateCoefAsk : groupAdvantage.SpecialTranFXRateCoefAsk;
                }
            }

            #endregion

            #endregion

            #region 12. İşlem Arbitraj ise Parity Değeri ve ParityBase tablosundan kurlar bulunur

            decimal parity = 0;
            decimal effectiveCoef = 0;
            var fxFEC1 = definitions.FECList.Find(a => a.FecId == FEC1);
            var fxFEC2 = definitions.FECList.Find(a => a.FecId == FEC2);
            if (FXCC.FECGroup != 0)//Arbitraj işlemi
            {
                var fxRateFEC1 = GetFX(definitions, Contract.FXIndexContract.FxOnlineMarketPrice, FEC1);
                var fxRateFEC2 = GetFX(definitions, Contract.FXIndexContract.FxOnlineMarketPrice, FEC2);
                if (fxRateFEC1 == null)
                {
                    //returnObject.Results.Add(new Result() { ErrorMessage = string.Format(BOA.Messages.FX.FXNotFoundOfBranchFXRateFEC, FEC1.ToString()), ErrorCode = BOA.Messages.FX.FXNotFoundOfBranchFXRateFECCode, Severity = Severity.Error });
                    return null;
                }
                if (fxRateFEC2 == null)
                {
                    //returnObject.Results.Add(new Result() { ErrorMessage = string.Format(BOA.Messages.FX.FXNotFoundOfBranchFXRateFEC, FEC2.ToString()), ErrorCode = BOA.Messages.FX.FXNotFoundOfBranchFXRateFECCode, Severity = Severity.Error });
                    return null;
                }
                if (fxFEC1.ParityBase == 1 && fxFEC2.ParityBase == 1)
                    parity = fxRateFEC2.Parity / fxRateFEC1.Parity;
                else if (fxFEC1.ParityBase == 1 && fxFEC2.ParityBase == 0)
                    parity = 1 / (fxRateFEC1.Parity * fxRateFEC2.Parity);
                else if (fxFEC1.ParityBase == 0 && fxFEC2.ParityBase == 1)
                    parity = fxRateFEC1.Parity * fxRateFEC2.Parity;
                else if (fxFEC1.ParityBase == 0 && fxFEC2.ParityBase == 0)
                    parity = fxRateFEC1.Parity / fxRateFEC2.Parity;
                if (fxParityBase.ParityBase == 2)
                    parity = 1 / parity;
                FXCC.EffectiveBidBranchParity = parity * (1 - (fxParityBase.CoefBidBranch.Value * fxParityBase.CoefficientOfVariation.Value)) * (1 - fxParityBase.CoefBidEffective.Value);
                FXCC.EffectiveAskBranchParity = parity * (1 + (fxParityBase.CoefAskBranch.Value * fxParityBase.CoefficientOfVariation.Value)) * (1 + fxParityBase.CoefAskEffective.Value);
                FXCC.BidBranchParity = parity * (1 - fxParityBase.CoefBidBranch.Value * fxParityBase.CoefficientOfVariation.Value);
                FXCC.AskBranchParity = parity * (1 + fxParityBase.CoefAskBranch.Value * fxParityBase.CoefficientOfVariation.Value);
                FXCC.EffectiveBidBankParity = FXCC.EffectiveBidBranchParity * (1 - fxParityBase.CoefBidBank.Value);
                FXCC.EffectiveAskBankParity = FXCC.EffectiveAskBranchParity * (1 + fxParityBase.CoefAskBank.Value);
                FXCC.BidBankParity = FXCC.BidBranchParity * (1 - fxParityBase.CoefBidBank.Value);
                FXCC.AskBankParity = FXCC.AskBranchParity * (1 + fxParityBase.CoefAskBank.Value);
                FXCC.EffectiveBidLowAmountParity = FXCC.EffectiveBidBankParity * (1 - fxParityBase.CoefBidLowAmount.Value);
                FXCC.EffectiveAskLowAmountParity = FXCC.EffectiveAskBankParity * (1 + fxParityBase.CoefAskLowAmount.Value);
                FXCC.BidLowAmountParity = FXCC.BidBankParity * (1 - fxParityBase.CoefBidLowAmount.Value);
                FXCC.AskLowAmountParity = FXCC.AskBankParity * (1 + fxParityBase.CoefAskLowAmount.Value);
                /*iş biriminden gelen arbitraj işlemlerdeki düşük montanlı işlemlerdeki hata nın sebebi olarak yorumlandı, yeni hali yukarıdaki gibidir ... mukose - 2014/05/30 */
                //FXCC.EffectiveBidLowAmountParity = FXCC.EffectiveBidBranchParity * (1 - fxParityBase.CoefBidLowAmount.Value);
                //FXCC.EffectiveAskLowAmountParity = FXCC.EffectiveAskBranchParity * (1 + fxParityBase.CoefAskLowAmount.Value);
                //FXCC.BidLowAmountParity = FXCC.BidBranchParity * (1 - fxParityBase.CoefBidLowAmount.Value);
                //FXCC.AskLowAmountParity = FXCC.AskBranchParity * (1 + fxParityBase.CoefAskLowAmount.Value);
            }

            #endregion

            #region 13. İşlem Tiplerine göre Avantajlı ve Avantajsız Kurlar Belirlenir.(A)Avantajlı (Z)Avantajsız

            decimal ALowAmountParity = 0;
            decimal ATranRate = 0;
            decimal ABranchCostRate = 0;
            decimal ZLowAmountParity = 0;
            decimal ZLowAmountParityBid = 0;
            decimal ZLowAmountParityAsk = 0;
            decimal ZTranRate = 0;
            decimal ZTranRateBid = 0;
            decimal ZTranRateAsk = 0;
            decimal ZBranchCostRate = 0;

            var FXECTransactionIndexRate = FXEngineContractList.Find(a => a.IndexType == fxIndex.IndexType);
            if (FXCC.TranType == 1)//Alış
            {
                if (FXCC.FECGroup != 0)//Arbitraj
                {
                    #region Avantajsız Kurlar

                    ZLowAmountParity = (IsEffectiveTransaction ? FXCC.EffectiveBidLowAmountParity : FXCC.BidLowAmountParity) * coef;
                    ZLowAmountParityBid = (IsEffectiveTransaction ? FXCC.EffectiveBidLowAmountParity : FXCC.BidLowAmountParity) * coef;
                    ZLowAmountParityAsk = (IsEffectiveTransaction ? FXCC.EffectiveAskLowAmountParity : FXCC.AskLowAmountParity) * coef;
                    if (fxIndex.IndexType == 2)//Şube Maliyet
                    {
                        ZTranRate = (IsEffectiveTransaction ? FXCC.EffectiveBidBranchParity : FXCC.BidBranchParity) * coef;
                        ZTranRateBid = (IsEffectiveTransaction ? FXCC.EffectiveBidBranchParity : FXCC.BidBranchParity) * coef;
                        ZTranRateAsk = (IsEffectiveTransaction ? FXCC.EffectiveAskBranchParity : FXCC.AskBranchParity) * coef;
                    }
                    else if (fxIndex.IndexType == 5) //Banka Kuru
                    {
                        ZTranRate = (IsEffectiveTransaction ? FXCC.EffectiveBidBankParity : FXCC.BidBankParity) * coef;
                        ZTranRateBid = (IsEffectiveTransaction ? FXCC.EffectiveBidBankParity : FXCC.BidBankParity) * coef;
                        ZTranRateAsk = (IsEffectiveTransaction ? FXCC.EffectiveAskBankParity : FXCC.AskBankParity) * coef;
                    }
                    else if (fxIndex.IndexType == 7) //DüşükMontan
                    {
                        ZTranRate = (IsEffectiveTransaction ? FXCC.EffectiveBidLowAmountParity : FXCC.BidLowAmountParity) * coef;
                        ZTranRateBid = (IsEffectiveTransaction ? FXCC.EffectiveBidLowAmountParity : FXCC.BidLowAmountParity) * coef;
                        ZTranRateAsk = (IsEffectiveTransaction ? FXCC.EffectiveAskLowAmountParity : FXCC.AskLowAmountParity) * coef;
                    }
                    ZBranchCostRate = (IsEffectiveTransaction ? FXCC.EffectiveBidBranchParity : FXCC.BidBranchParity) * FXCC.CostIndexCoef;

                    #endregion

                    #region Avantajlı Kurlar

                    ALowAmountParity = ZLowAmountParity + ((ZBranchCostRate - ZLowAmountParity) / 100) * FXCC.CustomerAdvantage;
                    ATranRate = ZTranRate + ((ZBranchCostRate - ZTranRate) / 100) * FXCC.CustomerAdvantage;
                    ABranchCostRate = ZBranchCostRate;

                    #endregion
                }
                else // Alış-Satıs İşlemi
                {
                    #region Avantajsız Kurlar

                    ZLowAmountParity = (IsEffectiveTransaction ? FXECLowAmountRate.EffectiveBid : FXECLowAmountRate.CurrencyBid) * coef;
                    ZLowAmountParityBid = (IsEffectiveTransaction ? FXECLowAmountRate.EffectiveBid : FXECLowAmountRate.CurrencyBid) * coef;
                    ZLowAmountParityAsk = (IsEffectiveTransaction ? FXECLowAmountRate.EffectiveAsk : FXECLowAmountRate.CurrencyAsk) * coef;
                    ZTranRate = (IsEffectiveTransaction ? FXECTransactionIndexRate.EffectiveBid : FXECTransactionIndexRate.CurrencyBid) * coef;
                    ZTranRateBid = (IsEffectiveTransaction ? FXECTransactionIndexRate.EffectiveBid : FXECTransactionIndexRate.CurrencyBid) * coef;
                    ZTranRateAsk = (IsEffectiveTransaction ? FXECTransactionIndexRate.EffectiveAsk : FXECTransactionIndexRate.CurrencyAsk) * coef;
                    ZBranchCostRate = (IsEffectiveTransaction ? FXECBranchCostRate.EffectiveBid : FXECBranchCostRate.CurrencyBid) * FXCC.CostIndexCoef;

                    #endregion

                    FXCC.BidBankParity = ZTranRateBid;
                    FXCC.AskBankParity = ZTranRateAsk;

                    #region Avantajlı Kurlar

                    ALowAmountParity = ZLowAmountParity + ((ZBranchCostRate - ZLowAmountParity) / 100) * FXCC.CustomerAdvantage;
                    ATranRate = ZTranRate + ((ZBranchCostRate - ZTranRate) / 100) * FXCC.CustomerAdvantage;
                    ABranchCostRate = ZBranchCostRate;

                    #endregion
                }
            }
            else if (FXCC.TranType == 2)//Satış
            {
                if (FXCC.FECGroup != 0)//Arbitraj
                {
                    #region Avantajsız Kurlar

                    ZLowAmountParity = (IsEffectiveTransaction ? FXCC.EffectiveAskLowAmountParity : FXCC.AskLowAmountParity) * coef;
                    ZLowAmountParityBid = (IsEffectiveTransaction ? FXCC.EffectiveBidLowAmountParity : FXCC.BidLowAmountParity) * coef;
                    ZLowAmountParityAsk = (IsEffectiveTransaction ? FXCC.EffectiveAskLowAmountParity : FXCC.AskLowAmountParity) * coef;
                    if (fxIndex.IndexType == 2)//Şube Maliyet
                    {
                        ZTranRate = (IsEffectiveTransaction ? FXCC.EffectiveAskBranchParity : FXCC.AskBranchParity) * coef;
                        ZTranRateBid = (IsEffectiveTransaction ? FXCC.EffectiveBidBranchParity : FXCC.BidBranchParity) * coef;
                        ZTranRateAsk = (IsEffectiveTransaction ? FXCC.EffectiveAskBranchParity : FXCC.AskBranchParity) * coef;
                    }
                    else if (fxIndex.IndexType == 5) //Banka Kuru
                    {
                        ZTranRate = (IsEffectiveTransaction ? FXCC.EffectiveAskBankParity : FXCC.AskBankParity) * coef;
                        ZTranRateBid = (IsEffectiveTransaction ? FXCC.EffectiveBidBankParity : FXCC.BidBankParity) * coef;
                        ZTranRateAsk = (IsEffectiveTransaction ? FXCC.EffectiveAskBankParity : FXCC.AskBankParity) * coef;
                    }
                    else if (fxIndex.IndexType == 7) //DüşükMontan
                    {
                        ZTranRate = (IsEffectiveTransaction ? FXCC.EffectiveAskLowAmountParity : FXCC.AskLowAmountParity) * coef;
                        ZTranRateBid = (IsEffectiveTransaction ? FXCC.EffectiveBidLowAmountParity : FXCC.BidLowAmountParity) * coef;
                        ZTranRateAsk = (IsEffectiveTransaction ? FXCC.EffectiveAskLowAmountParity : FXCC.AskLowAmountParity) * coef;
                    }
                    ZBranchCostRate = (IsEffectiveTransaction ? FXCC.EffectiveAskBranchParity : FXCC.AskBranchParity) * FXCC.CostIndexCoef;

                    #endregion

                    #region Avantajlı Kurlar

                    ALowAmountParity = ZLowAmountParity + ((ZLowAmountParity - ZBranchCostRate) / 100) * FXCC.CustomerAdvantage;
                    ATranRate = ZTranRate + ((ZTranRate - ZBranchCostRate) / 100) * FXCC.CustomerAdvantage;
                    ABranchCostRate = ZBranchCostRate;

                    #endregion
                }
                else // Alış-Satıs İşlemi
                {
                    #region Avantajsız Kurlar

                    ZLowAmountParity = (IsEffectiveTransaction ? FXECLowAmountRate.EffectiveAsk : FXECLowAmountRate.CurrencyAsk) * coef;
                    ZLowAmountParityBid = (IsEffectiveTransaction ? FXECLowAmountRate.EffectiveBid : FXECLowAmountRate.CurrencyBid) * coef;
                    ZLowAmountParityAsk = (IsEffectiveTransaction ? FXECLowAmountRate.EffectiveAsk : FXECLowAmountRate.CurrencyAsk) * coef;
                    ZTranRate = (IsEffectiveTransaction ? FXECTransactionIndexRate.EffectiveAsk : FXECTransactionIndexRate.CurrencyAsk) * coef;
                    ZTranRateBid = (IsEffectiveTransaction ? FXECTransactionIndexRate.EffectiveBid : FXECTransactionIndexRate.CurrencyBid) * coef;
                    ZTranRateAsk = (IsEffectiveTransaction ? FXECTransactionIndexRate.EffectiveAsk : FXECTransactionIndexRate.CurrencyAsk) * coef;
                    ZBranchCostRate = (IsEffectiveTransaction ? FXECBranchCostRate.EffectiveAsk : FXECBranchCostRate.CurrencyAsk) * FXCC.CostIndexCoef;

                    #endregion

                    FXCC.BidBankParity = ZTranRateBid;
                    FXCC.AskBankParity = ZTranRateAsk;

                    #region Avantajlı Kurlar

                    ALowAmountParity = ZLowAmountParity + ((ZLowAmountParity - ZBranchCostRate) / 100) * FXCC.CustomerAdvantage;
                    ATranRate = ZTranRate + ((ZTranRate - ZBranchCostRate) / 100) * FXCC.CustomerAdvantage;
                    ABranchCostRate = ZBranchCostRate;

                    #endregion
                }
            }

            #region Müşteri özel şube maliyet kuru şube maliyet kuruna aktarılır.

            FXEngineContract FXECMarketDataRate = FXEngineContractList.Find(a => a.IndexType == (short)Contract.FXIndexContract.MarketData);
            if (SpecialBranchRateCoef > 0)
            {
                decimal BankMiddleRate = 0;
                if (FXCC.FECGroup != 0)//Arbitraj ise
                {
                    BankMiddleRate = parity;
                }
                else
                {
                    BankMiddleRate = FXCC.TranType == 1 ? FXECMarketDataRate.CurrencyBid : FXECMarketDataRate.CurrencyAsk;
                }

                FXCC.ProfitOrLossCoef = fxDC.ProfitOrLossCoef;
                if (FXCC.TranType == 1)
                {
                    ABranchCostRate = ABranchCostRate + (BankMiddleRate - ABranchCostRate) * (SpecialBranchRateCoef / 100);
                    //internet  ve mobil şubede işlem kuru internet/mobil şube avantaj marjına göre hesaplanır...
                    if (SpecialTranFXRateBidCoef > 0)
                    {
                        ATranRate = ABranchCostRate - ABranchCostRate * Math.Round(SpecialTranFXRateBidCoef, 5);
                    }
                }
                else
                {
                    ABranchCostRate = ABranchCostRate - (ABranchCostRate - BankMiddleRate) * (SpecialBranchRateCoef / 100);
                    //internet ve mobil şubede işlem kuru internet/mobil şube avantaj marjına göre hesaplanır...
                    if (SpecialTranFXRateAskCoef > 0)
                    {
                        ATranRate = ABranchCostRate + ABranchCostRate * Math.Round(SpecialTranFXRateAskCoef, 5);
                    }
                }
            }
            ZBranchCostRate = ABranchCostRate;
            FXCC.BranchCostRate = ABranchCostRate;
            FXCC.BranchCostRateBase = ABranchCostRate;

            #endregion

            #endregion

            #region 14. Baz işlem olup olmamasına göre KUR bulunur

            decimal ATranFXRate = 0;
            decimal ZTranFXRate = 0;
            decimal ZTranFXRateBid = 0;
            decimal ZTranFXRateAsk = 0;

            if (IsBase)
            {
                if (TranAmount > FXCC.MinAmountForIndex)
                {
                    ATranFXRate = ATranRate;
                    ZTranFXRate = ZTranRate;
                    ZTranFXRateAsk = ZTranRateAsk;
                    ZTranFXRateBid = ZTranRateBid;
                }
                else
                {
                    ATranFXRate = ALowAmountParity;
                    ZTranFXRate = ZLowAmountParity;
                    ZTranFXRateBid = ZLowAmountParityBid;
                    ZTranFXRateAsk = ZLowAmountParityAsk;
                }
            }
            else
            {
                if ((TranAmount / ALowAmountParity) <= FXCC.MinAmountForIndex)
                {
                    ATranFXRate = ALowAmountParity;
                    ZTranFXRate = ZLowAmountParity;
                    ZTranFXRateBid = ZLowAmountParityBid;
                    ZTranFXRateAsk = ZLowAmountParityAsk;
                }
                else
                {
                    ATranFXRate = ATranRate;
                    ZTranFXRate = ZTranRate;
                    ZTranFXRateAsk = ZTranRateAsk;
                    ZTranFXRateBid = ZTranRateBid;
                }
            }

            //döviz ve kıymetli maden işlerimlerinde kar \zarar tutrları 5 haneye yuvarladıktan sonra hesaplanmıştır. - kasım geçişleri -metin
            FXCC.SpotRate = Math.Round(ATranFXRate, 5);
            FXCC.TranFXRate = Math.Round(ATranFXRate, 5);
            FXCC.TranFXRateAsk = Math.Round(ZTranFXRateAsk, 5);
            FXCC.TranFXRateBid = Math.Round(ZTranFXRateBid, 5);
            FXCC.AskLowAmountParity = Math.Round(ZLowAmountParityAsk, 5);
            FXCC.BidLowAmountParity = Math.Round(ZLowAmountParityBid, 5);

            #endregion

            #region 15. Forward işlemmi mi kontrol edilir. Forward işlem ise GetForwardFXRate metoduna gönderilerek forward kur hesaplanır.

            FXCC.ProposedRate = FXCC.TranFXRate;
            FXCC.MaturityDate = MaturityDate;

            #endregion

            #region 16. Karşılık Amountlar Bulunur

            if (FXCC.TranType == 1)//Alış
            {
                FXCC.FEC2TranAmount = TranAmount * Math.Round(FXCC.TranFXRate, 5);
                FXCC.FEC1TranAmount = TranAmount;
                FXCC.TRYEquivalentRate = FXECBranchCostRate.CurrencyBid;
            }
            else if (FXCC.TranType == 2)//Satış
            {
                FXCC.FEC2TranAmount = TranAmount / Math.Round(FXCC.TranFXRate, 5);
                FXCC.FEC1TranAmount = TranAmount;
                FXCC.TRYEquivalentRate = FXECBranchCostRate.CurrencyAsk;
            }

            #endregion

            #region 17. Forward şube maliyet kuru bulunur

            FXCC.BranchCostRate = Math.Round(FXCC.BranchCostRate, 5);
            FXCC.BranchCostRateForward = FXCC.BranchCostRate;

            #endregion

            #region 18. İşlem Tiplerine Göre Kullanıcı Marjları Bulunur. (Bu işlemlerde avantajsız Şube maliyet ve işlem kurları kullanılır.)

            if (FXCC.TranType == 1) //Alış
            {
                FXCC.ChangeRangeDownAmount = Math.Round(ZTranFXRate - ((ZBranchCostRate - ZTranFXRate) / 100) * FXCC.ChangeRangeDown, 5);
                FXCC.ChangeRangeUpAmount = Math.Round(ZTranFXRate + ((ZBranchCostRate - ZTranFXRate) / 100) * FXCC.ChangeRangeUp, 5);
            }
            else if (FXCC.TranType == 2) //Satış
            {
                FXCC.ChangeRangeDownAmount = Math.Round(ZTranFXRate - ((ZTranFXRate - ZBranchCostRate) / 100) * FXCC.ChangeRangeDown, 5);
                FXCC.ChangeRangeUpAmount = Math.Round(ZTranFXRate + ((ZTranFXRate - ZBranchCostRate) / 100) * FXCC.ChangeRangeUp, 5);
            }

            #endregion

            #region 19. Kar/Zarar hesaplanır

            #region Müşteri İşlemleri İçin Kar/Zarar Piyasa Verisi Üzerinden Hesaplanır

            var marketDataRateResponse = GetMarketDataRate(definitions, FXCC, FXECMarketDataRate, fxFEC1, fxFEC2, fxParityBase);
            FXCC.MarketDataRate = marketDataRateResponse;
            if (FXCC.MarketDataRate > 0)
            {
                FXCC.IsProfitLossRateMarketData = true;
                FXCC.ProfitOrLossFXRate = FXCC.MarketDataRate;
            }
            else
            {
                FXCC.IsProfitLossRateMarketData = false;
                FXCC.ProfitOrLossFXRate = FXCC.BranchCostRate;
            }

            #endregion

            //SPL = BaseAmount*(d-b)*(IfBaseAmountSell; -1; 1)*TRYEvaluationRate(TCMB)						
            //DPL =	BaseAmount*[(c-a)-(d-b)]*(IfBaseAmountSell; -1; 1)*TRYEvaluationRate(TCMB)						

            var smPips = FXCC.ProfitOrLossFXRate - FXCC.SpotRate.GetValueOrDefault(0); // spot marjin pipsi
                                                                                       //var dmPips = (FXCC.BranchCostRateForward - FXCC.TranFXRate) - smPips; // türev marjin pipsi
                                                                                       //var smPips = FXCC.BranchCostRate - FXCC.SpotRate.GetValueOrDefault(0); // spot marjin pipsi
                                                                                       //var dmPips = (FXCC.BranchCostRateForward - FXCC.TranFXRate) - smPips; // türev marjin pipsi

            var baseAmount = (decimal)0;
            var ifBaseAmountSell = 1;

            if (FEC1 == FXCC.BaseFEC)
            {
                baseAmount = FXCC.FEC1TranAmount;
                ifBaseAmountSell = 1;
            }
            if (FEC2 == FXCC.BaseFEC)
            {
                baseAmount = FXCC.FEC2TranAmount;
                ifBaseAmountSell = -1;
            }

            FXCC.ProfitOrLossValue = Math.Round((baseAmount * smPips * ifBaseAmountSell), 2);

            #region Kar/Zarar TL'ye çevirilir

            if (FEC1 != (short)FECEnum.TL && FEC2 != (short)FECEnum.TL) //İşlem Arbitraj ise
            {
                var fxrateContract = GetFX(definitions, Contract.FXIndexContract.CentralBankPrice, pairFEC);
                if (fxrateContract != null)//YENI EKLENDİ ERS
                {
                    FXCC.BidCentralBankParity = IsEffectiveTransaction ? fxrateContract.EffectiveBid : fxrateContract.CurrencyBid;
                    FXCC.AskCentralBankParity = IsEffectiveTransaction ? fxrateContract.EffectiveAsk : fxrateContract.CurrencyAsk;

                    #region Kambiyo kar veya zararını TL'ye çevir

                    if (FXCC.ProfitOrLossValue >= 0)
                    {
                        FXCC.ProfitOrLossValue = Math.Round(FXCC.ProfitOrLossValue * Math.Round(FXCC.BidCentralBankParity, 5), 2);
                    }
                    else
                    {
                        FXCC.ProfitOrLossValue = Math.Round(FXCC.ProfitOrLossValue * Math.Round(FXCC.AskCentralBankParity, 5), 2);
                    }

                    #endregion

                    #region Türev kar veya zararını TL'ye çevir

                    if (FXCC.DerivativeProfitOrLossValue >= 0)
                    {
                        FXCC.DerivativeProfitOrLossValue = Math.Round(FXCC.DerivativeProfitOrLossValue * Math.Round(FXCC.BidCentralBankParity, 5), 2);
                    }
                    else
                    {
                        FXCC.DerivativeProfitOrLossValue = Math.Round(FXCC.DerivativeProfitOrLossValue * Math.Round(FXCC.AskCentralBankParity, 5), 2);
                    }

                    #endregion
                }

            }

            #endregion

            if (!FXCC.IsProfitLossRateMarketData && FXCC.ProfitOrLossValue > 0 && SpecialBranchRateCoef == 0)
            {
                FXCC.ProfitOrLossValue = Math.Round(FXCC.ProfitOrLossValue * FXCC.ProfitOrLossCoef, 2);
            }
            if (FXCC.DerivativeProfitOrLossValue > 0 && SpecialBranchRateCoef == 0)
            {
                FXCC.DerivativeProfitOrLossValue = Math.Round(FXCC.DerivativeProfitOrLossValue * FXCC.ProfitOrLossCoef, 2);
            }

            #endregion

            #region 20. FXJournal property si doldurulur

            FXCC.FXJournal = new FXJournalContract();
            FXCC.FXJournal.AccountNumber = 0;
            FXCC.FXJournal.ResourceCode = ResourceCode;
            FXCC.FXJournal.AccountNumber = 0;
            FXCC.FXJournal.TranDate = DateTime.Now.Date;
            FXCC.FXJournal.BranchId = 1;
            FXCC.FXJournal.TranFXRate = FXCC.TranFXRate;
            FXCC.FXJournal.FECBid = FEC1;
            FXCC.FXJournal.FECAsk = FEC2;
            FXCC.FXJournal.FECBidTranAmount = FXCC.FEC1TranAmount;
            FXCC.FXJournal.FECAskTranAmount = FXCC.FEC2TranAmount;
            FXCC.FXJournal.ChangeRangeDown = FXCC.ChangeRangeDown;
            FXCC.FXJournal.ChangeRangeUp = FXCC.ChangeRangeUp;
            FXCC.FXJournal.ChangeRangeDownAmount = FXCC.ChangeRangeDownAmount;
            FXCC.FXJournal.ChangeRangeUpAmount = FXCC.ChangeRangeUpAmount;
            FXCC.FXJournal.ProfitOrLossValue = FXCC.ProfitOrLossValue;
            FXCC.FXJournal.MinTranAmount = FXCC.MinAmountForIndex;
            FXCC.FXJournal.CoefForMinTranAmount = FXCC.CoefForMinTranAmount;
            FXCC.FXJournal.IndexCoef = FXCC.IndexCoef;
            FXCC.FXJournal.FXId = FXCC.FXID;
            FXCC.FXJournal.Description = FXCC.Info;
            FXCC.FXJournal.CustomerAdvantage = FXCC.CustomerAdvantage;
            FXCC.FXJournal.BankRate = FXCC.TranType == 1 ? FXCC.BidBankParity : FXCC.AskBankParity;
            FXCC.FXJournal.LowAmountRate = FXCC.TranType == 1 ? FXCC.BidLowAmountParity : FXCC.AskLowAmountParity;
            FXCC.FXJournal.BranchRate = FXCC.BranchCostRate;
            FXCC.FXJournal.FromAmountForLimit = FXCC.FromAmountForLimit;
            FXCC.FXJournal.ToAmountForLimit = FXCC.ToAmountForLimit;
            FXCC.FXJournal.MinTranAmountForRef = FXCC.MinTranAmountForRef;
            FXCC.FXJournal.MaxTranAmountForRef = FXCC.MaxTranAmountForRef;
            FXCC.FXJournal.IndexType = FXCC.IndexType;
            FXCC.FXJournal.CostIndexType = FXCC.CostIndexType;
            FXCC.FXJournal.CostIndexCoef = FXCC.CostIndexCoef;
            FXCC.FXJournal.ProfitOrLossCoef = FXCC.ProfitOrLossCoef;
            FXCC.FXJournal.TranFXRateAsk = FXCC.TranFXRateAsk;
            FXCC.FXJournal.TranFXRateBid = FXCC.TranFXRateBid;
            FXCC.FXJournal.TRYEquivalentRate = FXCC.TRYEquivalentRate;
            FXCC.FXJournal.USDEquivalentRate = FXCC.USDEquivalentRate;
            FXCC.FXJournal.BranchRateBase = FXCC.BranchCostRateBase;
            FXCC.FXJournal.MaturityDate = MaturityDate;
            FXCC.FXJournal.SpotRate = FXCC.SpotRate;
            FXCC.FXJournal.BaseFEC = FXCC.BaseFEC;
            FXCC.FXJournal.DerivativeProfitOrLossValue = FXCC.DerivativeProfitOrLossValue;
            FXCC.FXJournal.BranchRateForward = FXCC.BranchCostRateForward;
            FXCC.FXJournal.MarketDataRate = FXCC.MarketDataRate;
            FXCC.FXJournal.IsProfitLossRateMarketData = FXCC.IsProfitLossRateMarketData;

            #endregion

            #region 21. UnitMultiplier (Çeyrek Altın)

            // 22 ayar altınlar için birim çarpan değeri (1 Adet = 1.75 gr gibi) diğer FEC'ler için değeri: 1
            FXCC.FEC1UnitMultiplier = definitions.FECList.Find(a => a.FecId == FEC1).UnitMultiplier;
            FXCC.FEC2UnitMultiplier = definitions.FECList.Find(a => a.FecId == FEC2).UnitMultiplier;

            #endregion

            //returnObject = FXCC;
            return FXCC;
        }

        private FXEngineContract GetFX(FxOnlineContract definitions, Contract.FXIndexContract fXIndex, short fecId)
        {
            var fxRateList = definitions.FXRateList;
            if (fxRateList != null)
            {
                var fxRate = fxRateList.FirstOrDefault(x => x.CurrencyCode == fecId && x.IndexType == (short)fXIndex);
                return fxRate;
            }

            return null;
        }

        private decimal GetMarketDataRate(FxOnlineContract definitions, FXComponentContract FXCC, FXEngineContract FXECMarketDataRate, FECContract FEC1, FECContract FEC2, FXParityBaseContract fxParityBase)
        {
            decimal marketDataRate = 0;
            try
            {
                if (FXCC.FECGroup != 0) //Arbitraj
                {
                    decimal marketDataParity = 0;
                    var fxMarketDataRateFEC1 = GetFX(definitions, Contract.FXIndexContract.MarketData, (short)FEC1.FecId);
                    var fxMarketDataRateFEC2 = GetFX(definitions, Contract.FXIndexContract.MarketData, (short)FEC2.FecId);

                    if (fxMarketDataRateFEC1 == null)
                    {
                        Logger.LogError("FXNotFoundOfMarketDataRateFEC1-" + FEC1.FecId, null);
                        return 0;
                    }

                    if (fxMarketDataRateFEC2 == null)
                    {
                        Logger.LogError("FXNotFoundOfMarketDataRateFEC2-" + FEC2.FecId, null);
                        return 0;
                    }

                    if (FEC1.ParityBase == 1 && FEC2.ParityBase == 1)
                    {
                        marketDataParity = fxMarketDataRateFEC2.Parity / fxMarketDataRateFEC1.Parity;
                    }
                    else if (FEC1.ParityBase == 1 && FEC2.ParityBase == 0)
                    {
                        marketDataParity = 1 / (fxMarketDataRateFEC1.Parity * fxMarketDataRateFEC2.Parity);
                    }
                    else if (FEC1.ParityBase == 0 && FEC2.ParityBase == 1)
                    {
                        marketDataParity = fxMarketDataRateFEC1.Parity * fxMarketDataRateFEC2.Parity;
                    }
                    else if (FEC1.ParityBase == 0 && FEC2.ParityBase == 0)
                    {
                        marketDataParity = fxMarketDataRateFEC1.Parity / fxMarketDataRateFEC2.Parity;
                    }
                    if (fxParityBase.ParityBase == 2)
                    {
                        marketDataParity = 1 / marketDataParity;
                    }

                    if (FXCC.TranType == 1)
                    {
                        marketDataRate = marketDataParity * (1 - fxParityBase.CoefBidBranch.Value * fxParityBase.CoefficientOfVariation.Value);
                    }
                    else if (FXCC.TranType == 2)
                    {
                        marketDataRate = marketDataParity * (1 - fxParityBase.CoefAskBranch.Value * fxParityBase.CoefficientOfVariation.Value);
                    }
                }
                else
                {
                    if (FXECMarketDataRate == null)
                    {
                        Logger.LogError("FXNotFoundOfMarketDataRateFEC-" + FXCC.BaseFEC.Value, null);
                        return 0;
                    }

                    if (FXCC.TranType == 1) //Alış
                    {
                        marketDataRate = FXECMarketDataRate.CurrencyBid;
                    }
                    else if (FXCC.TranType == 2) //Satış
                    {
                        marketDataRate = FXECMarketDataRate.CurrencyAsk;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "GetMarketDataRate:" + FEC1.FecId + "-" + FEC2.FecId, null);
                return 0;
            }

            return marketDataRate;
        }

        private FXLimitsContract GetFXLimit(FxOnlineContract definitions, int fxId, short fec)
        {
            var fxLimitList = definitions.FXLimitList;
            if (fxLimitList != null)
            {
                var fxLimit = fxLimitList.OrderByDescending(x => x.FXId)
                                         .FirstOrDefault(x =>
                                             x.FXId == fxId &&
                                             (x.FEC == fec || x.FEC == -1));
                return fxLimit;
            }

            return null;
        }

        private FEIndexContract GetFXIndex(FxOnlineContract definitions, int fxId, short accountClass)
        {
            var fxIndexList = definitions.FXIndexList;
            if (fxIndexList != null)
            {
                var fxIndex = fxIndexList.OrderByDescending(x => x.AccountClass)
                                         .FirstOrDefault(x =>
                                             x.FXId == fxId &&
                                             (x.AccountClass == accountClass || x.AccountClass == 0));
                return fxIndex;
            }

            return null;
        }

        private decimal GetUsdEquivalent(FxOnlineContract definitions, short fecId)
        {
            var fxRate = GetFX(definitions, Contract.FXIndexContract.FxOnlineMarketPrice, fecId);
            if (fxRate != null)
            {
                var parity = fxRate.Parity;
                if (fxRate.ParityBase == 1)
                    parity = 1 / parity;

                return parity;
            }

            return 0;
        }
        private List<FXEngineContract> GetFXList(FxOnlineContract definitions, short fecId)
        {
            var fxRateList = definitions.FXRateList;
            if (fxRateList != null)
            {
                var fxRate = fxRateList.Where(x => x.CurrencyCode == fecId);
                return fxRate.ToList();
            }

            return null;
        }

        private FXDefinitionContract GetFXDefinition(FxOnlineContract definitions, string resourceCode, short tranType, short channelId, short fxKey, short fecGroup)
        {
            var fxDefinitionList = definitions.FXDefinitionList;
            if (fxDefinitionList != null)
            {
                var fxDefinition = fxDefinitionList.FirstOrDefault(x =>
                                                        x.ChannelId == channelId &&
                                                        x.ResourceCode.Equals(resourceCode) &&
                                                        x.TranType == tranType &&
                                                        x.FXKey == fxKey &&
                                                        x.FECGroup == fecGroup);
                if (fxDefinition != null)
                {
                    return fxDefinition;
                }
                else
                {
                    fxDefinition = fxDefinitionList.OrderByDescending(x => x.ChannelId)
                                                  .FirstOrDefault(x =>
                                                        (x.ChannelId == channelId || x.ChannelId == 0) &&
                                                        x.ResourceCode.Equals("Root") &&
                                                        x.TranType == tranType &&
                                                        x.FXKey == fxKey &&
                                                        x.FECGroup == fecGroup);
                    return fxDefinition;
                }
            }

            return null;
        }
        private FXParityBaseContract GetFXParity(FxOnlineContract definitions, short fecBid, short fecAsk)
        {
            var fxParityList = definitions.FXPartiyList;
            if (fxParityList != null)
            {
                var fxParity = fxParityList.FirstOrDefault(x => x.FECBid == fecBid && x.FECAsk == fecAsk);
                return fxParity;
            }

            return null;
        }


        #endregion



        public void SetCache<T>(string key, T value, TimeSpan timeout)
        {
            try
            {
                var jsonString = JsonSerializer.Serialize(value);
                _cacheManager.SetString(key, jsonString, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = timeout });
            }
            catch (Exception ex)
            {

            }

            //_cacheManager.Set(key, value, new Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions
            //{
            //    AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(lifeTimeMinute)
            //});
        }

        private T GetCache<T>(string key)
        {
            try
            {
                var jsonString = _cacheManager.GetString(key);
                return jsonString == null ? default(T) : JsonSerializer.Deserialize<T>(jsonString);
            }
            catch (Exception ex)
            {

                return default(T);
            }

        }
    }
}
