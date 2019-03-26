using Newtonsoft.Json.Linq;
using System.Numerics;
using ThinNeo;

namespace DEX_SDK
{
    public class Dex_ScriptPackage : ScriptPackage
    {
        public Dex_ScriptPackage(string _contractHash) : base(_contractHash)
        {

        }

        public byte[] GetScript_SetSysSetting_SuperAdmin(string key,string address)
        {
            JArray inputJA = JArray.Parse(string.Format(@"
                    [
	                    '(str)setSysSetting',
	                    [
		                    '(str){0}',
                            '(addr){1}'
	                    ]
                    ]", key, address));

            return GetScript(contractHash, inputJA);
        }

        public byte[] GetScript_SetSysSetting_DividingPoolAddr(string key, string address)
        {
            JArray inputJA = JArray.Parse(string.Format(@"
                    [
	                    '(str)setSysSetting',
	                    [
		                    '(str){0}',
                            '(addr){1}'
	                    ]
                    ]", key, address));

            return GetScript(contractHash, inputJA);
        }

        public byte[] GetScript_SetSysSetting_DomainCenterHash(string key, Hash160 hash)
        {
            JArray inputJA = JArray.Parse(string.Format(@"
                    [
	                    '(str)setSysSetting',
	                    [
		                    '(str){0}',
                            '(hex160){1}'
	                    ]
                    ]", key, hash));

            return GetScript(contractHash, inputJA);
        }

        public byte[] GetScript_SetSysSetting_MortgagePayments(string key, BigInteger mortgagePayments)
        {
            JArray inputJA = JArray.Parse(string.Format(@"
                    [
	                    '(str)setSysSetting',
	                    [
		                    '(str){0}',
                            '(int){1}'
	                    ]
                    ]", key, mortgagePayments));

            return GetScript(contractHash, inputJA);
        }

        public byte[] GetScript_SetSysSetting_Interval(string key,BigInteger interval)
        {
            JArray inputJA = JArray.Parse(string.Format(@"
                    [
	                    '(str)setSysSetting',
	                    [
		                    '(str){0}',
                            '(int){1}'
	                    ]
                    ]", key, interval));

            return GetScript(contractHash, inputJA);
        }

        public byte[] GetScript_GetSysSetting(string key)
        {
            JArray inputJA = JArray.Parse(string.Format(@"
                    [
	                    '(str)getSysSetting',
	                    [
		                    '(str){0}'
	                    ]
                    ]", key));

            return GetScript(contractHash, inputJA);
        }

        public byte[] GetScript_SetAssetSetting(Hash160 assetid,BigInteger enable,BigInteger min,BigInteger unit,BigInteger feeRate,string transferMethod)
        {
            JArray inputJA = JArray.Parse(string.Format(@"
                    [
	                    '(str)setAssetSetting',
	                    [
		                    '(hex160){0}',
                            '(int){1}',
                            '(int){2}',
                            '(int){3}',
                            '(int){4}',
                            '(string){5}',
	                    ]
                    ]", assetid, enable,min, unit, feeRate,transferMethod));

            return GetScript(contractHash, inputJA);
        }

        public byte[] GetScript_GetAssetSetting(Hash160 assetid)
        {
            JArray inputJA = JArray.Parse(string.Format(@"
                    [
	                    '(str)getAssetSetting',
	                    [
		                    '(hex160){0}'
	                    ]
                    ]", assetid));

            return GetScript(contractHash, inputJA);
        }

        public byte[] GetScript_GetBalanceOf(string address, Hash160 assetid)
        {
            JArray inputJA = JArray.Parse(string.Format(@"
                    [
	                    '(str)getBalanceOf',
	                    [
		                    '(addr){0}',
		                    '(hex160){1}'
	                    ]
                    ]", address, assetid));

            return GetScript(contractHash, inputJA);
        }

        public byte[] GetScript_GetOfferToBuyPrice(string address,Hash256 fullhash, Hash160 assetid)
        {
            JArray inputJA = JArray.Parse(string.Format(@"
                    [
	                    '(str)getOfferToBuyPrice',
	                    [
		                    '(addr){0}',
		                    '(hex256){1}',
                            '(hex160){2}'
	                    ]
                    ]", address, fullhash, assetid));

            return GetScript(contractHash, inputJA);
        }

        public byte[] GetScript_GetOfferToBuyInfo(string address, Hash256 fullhash, Hash160 assetid)
        {
            JArray inputJA = JArray.Parse(string.Format(@"
                    [
	                    '(str)getOfferToBuyPrice',
	                    [
		                    '(addr){0}',
		                    '(hex256){1}',
                            '(hex160){2}'
	                    ]
                    ]", address, fullhash, assetid));

            return GetScript(contractHash, inputJA);
        }

        public byte[] GetScript_GetAuctionInfo(Hash256 fullhash)
        {
            JArray inputJA = JArray.Parse(string.Format(@"
                    [
	                    '(str)getAuctionInfo',
	                    [
		                    '(hex256){0}'
	                    ]
                    ]",fullhash));

            return GetScript(contractHash, inputJA);
        }

        public byte[] GetScript_GetAuctionPrice(Hash256 fullhash)
        {
            JArray inputJA = JArray.Parse(string.Format(@"
                    [
	                    '(str)getAuctionPrice',
	                    [
		                    '(hex256){0}'
	                    ]
                    ]", fullhash));

            return GetScript(contractHash, inputJA);
        }

        public byte[] GetScript_SetMoneyIn(Hash256 txid,Hash160 assetid)
        {
            JArray inputJA = JArray.Parse(string.Format(@"
                    [
	                    '(str)setMoneyIn',
	                    [
		                    '(hex256){0}',
                            '(hex160){1}'
	                    ]
                    ]", txid, assetid));

            return GetScript(contractHash, inputJA);
        }

        public byte[] GetScript_TransferIn(string address, Hash160 assetid,BigInteger value)
        {
            JArray inputJA = JArray.Parse(string.Format(@"
                    [
	                    '(str)transferIn',
	                    [
		                    '(addr){0}',
                            '(hex160){1}',
                            '(int){2}'
	                    ]
                    ]", address, assetid, value));

            return GetScript(contractHash, inputJA);
        }

        public byte[] GetScript_GetMoneyBack(string address,Hash160 assetid,BigInteger value)
        {
            JArray inputJA = JArray.Parse(string.Format(@"
                    [
	                    '(str)getMoneyBack',
	                    [
		                    '(addr){0}',
                            '(hex160){1}',
                            '(int){2}'
	                    ]
                    ]", address, assetid, value));

            return GetScript(contractHash, inputJA);
        }

        public byte[] GetScript_GetMoneyBackAll(string address, Hash160 assetid)
        {
            JArray inputJA = JArray.Parse(string.Format(@"
                    [
	                    '(str)getMoneyBackAll',
	                    [
		                    '(addr){0}',
                            '(hex160){1}'
	                    ]
                    ]", address, assetid));

            return GetScript(contractHash, inputJA);
        }

        public byte[] GetScript_OfferToBuy(string address, string[] strs,Hash160 assetid,BigInteger price,BigInteger mortgagePayments)
        {
            JArray inputJA = JArray.Parse(string.Format(@"
                    [
	                    '(str)offerToBuy',
	                    [
		                    '(addr){0}',
		                   ['(str){1}','(str){2}'],
		                    '(hex160){3}',
		                    '(int){4}',
                            '(int){5}'
	                    ]
                    ]", address, strs[1],strs[0], assetid,price,mortgagePayments));

            return GetScript(contractHash, inputJA);
        }

        public byte[] GetScript_DiscontinueOfferToBuy(string address,Hash256 fullhash,Hash160 assetid)
        {
            JArray inputJA = JArray.Parse(string.Format(@"
                    [
	                    '(str)discontinueOfferToBuy',
	                    [
		                    '(addr){0}',
		                    '(hex256){1}',
                            '(hex160){2}'
	                    ]
                    ]", address, fullhash, assetid));

            return GetScript(contractHash, inputJA);
        }

        public byte[] GetScript_Sell(string address, Hash256 fullhash, Hash160 assetid)
        {
            JArray inputJA = JArray.Parse(string.Format(@"
                    [
	                    '(str)sell',
	                    [
		                    '(addr){0}',
		                    '(hex256){1}',
                            '(hex160){2}'
	                    ]
                    ]", address, fullhash, assetid));

            return GetScript(contractHash, inputJA);
        }

        public byte[] GetScript_GetSellMoney(Hash256 txid)
        {
            JArray inputJA = JArray.Parse(string.Format(@"
                    [
	                    '(str)getSellMoney',
	                    [
		                    '(hex256){0}'
	                    ]
                    ]", txid));

            return GetScript(contractHash, inputJA);
        }

        public byte[] GetScript_Auction(string[] domainArray, Hash160 assetid, BigInteger startPrice, BigInteger endPrice, BigInteger salePrice, BigInteger mortgagePayments)
        {
            JArray inputJA = JArray.Parse(string.Format(@"
                    [
	                    '(str)auction',
	                    [
		                    ['(str){0}','(str){1}'],
		                    '(hex160){2}',
                            '(int){3}',
                            '(int){4}',
                            '(int){5}',
                            '(int){6}'
	                    ]
                    ]", domainArray[1],domainArray[0], assetid, startPrice, endPrice, salePrice, mortgagePayments));

            return GetScript(contractHash, inputJA);
        }

        public byte[] GetScript_DiscontinueAuction(Hash256 hash256)
        {
            JArray inputJA = JArray.Parse(string.Format(@"
                    [
	                    '(str)discontinueAuction',
	                    [
		                    '(hex256){0}',
	                    ]
                    ]", hash256));

            return GetScript(contractHash, inputJA);
        }

        public byte[] GetScript_Bet(string address, Hash256 hash256,Hash160 assetid,BigInteger price)
        {
            JArray inputJA = JArray.Parse(string.Format(@"
                    [
	                    '(str)bet',
	                    [
		                    '(addr){0}',
		                    '(hex256){1}',
		                    '(hex160){2}',
		                    '(int){3}'
	                    ]
                    ]", address, hash256, assetid, price));

            return GetScript(contractHash, inputJA);
        }

        public byte[] GetScript_GetAuctionMoney(Hash256 txid)
        {
            JArray inputJA = JArray.Parse(string.Format(@"
                    [
	                    '(str)getAuctionMoney',
	                    [
		                    '(hex256){0}'
	                    ]
                    ]", txid));

            return GetScript(contractHash, inputJA);
        }

        public byte[] GetScript_GetDividendBalance(Hash160 assetid)
        {
            JArray inputJA = JArray.Parse(string.Format(@"
                    [
	                    '(str)getDividendBalance',
	                    [
		                    '(hex160){0}',
	                    ]
                    ]", assetid));

            return GetScript(contractHash, inputJA);
        }

        public byte[] GetScript_TransferNNC(string from, string to, BigInteger value)
        {
            JArray inputJA = JArray.Parse(string.Format(@"
                    [
	                    '(str)transfer',
	                    [
		                    '(addr){0}',
                            '(addr){1}',
                            '(int){2}'
	                    ]
                    ]", from, to, value * 100));
            return GetScript(new Hash160("0xfc732edee1efdf968c23c20a9628eaa5a6ccb934"), inputJA);
        }

        public byte[] GetScript_TransferCGAS(string from, string to, BigInteger value)
        {
            JArray inputJA = JArray.Parse(string.Format(@"
                    [
	                    '(str)transfer',
	                    [
		                    '(addr){0}',
                            '(addr){1}',
                            '(int){2}'
	                    ]
                    ]", from, to, value * 100000000));
            return GetScript(new Hash160("0x74f2dc36a68fdc4682034178eb2220729231db76"), inputJA);
        }
    }
}
