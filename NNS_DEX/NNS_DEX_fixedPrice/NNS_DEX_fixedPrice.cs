using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using Helper = Neo.SmartContract.Framework.Helper;
using System;
using System.Numerics;
using System.ComponentModel;

namespace NNS_DEX_fixedPrice
{
    //NNS域名交易市场V1.0 - 一口价出售合约
    public class NNS_DEX_fixedPrice : SmartContract
    {
        //除了初始管理员，全动态配置
        //初始管理員,设置新的superAdminAddr后，无效
        static readonly byte[] initSuperAdminAddr = Helper.ToScriptHash("ALjSnMZidJqd18iQaoCgFun6iqWRm2cVtj");

        //修正数
        static readonly BigInteger fixedNumber = 10000;

        //动态合约调用委托
        delegate object deleDyncall(string method, object[] arr);

        //SysSetting
        //superAdminAddr
        //dividingPoolAddr
        //domainCenterHash

        //通知系统设置事件
        public delegate void deleSetSysSetting(string key, byte[] value);
        [DisplayName("setSysSetting")]
        public static event deleSetSysSetting onSetSysSetting;

        //通知资产设置事件
        public delegate void deleSetAssetSetting(byte[] assetHash, assetSetting assetSetting);
        [DisplayName("setAssetSetting")]
        public static event deleSetAssetSetting onSetAssetSetting;

        //通知 余额增加（充值）
        public delegate void deleSetMoneyIn(byte[] who, byte[] assetHash, BigInteger value, byte[] txid);
        [DisplayName("setMoneyIn")]
        public static event deleSetMoneyIn onSetMoneyIn;

        //通知 余额减少（提现）
        public delegate void deleGetMoneyBack(byte[] who, byte[] assetHash, BigInteger value);
        [DisplayName("getMoneyBack")]
        public static event deleGetMoneyBack onGetMoneyBack;

        //通知 合约内资金的变更
        public delegate void deleDexTransfer(byte[] from,byte[] to,byte[] assetHash,BigInteger value);
        [DisplayName("dexTransfer")]
        public static event deleDexTransfer onDexTransfer;

        //通知 求购
        public delegate void deleNNSofferToBuy(OfferToBuyInfo offerToBuyInfo);
        [DisplayName("NNSofferToBuy")]
        public static event deleNNSofferToBuy onOfferToBuy;

        //通知 取消求购
        public delegate void deleNNSofferToBuyDiscontinued(OfferToBuyInfo offerToBuyInfo);
        [DisplayName("NNSofferToBuyDiscontinued")]
        public static event deleNNSofferToBuyDiscontinued onOfferToBuyDiscontinued;

        //通知 出售给求购者
        public delegate void deleNNSsell(byte[] addr,OfferToBuyInfo offerToBuyInfo);
        [DisplayName("NNSsell")]
        public static event deleNNSsell onSell;

        //通知 开始拍卖
        public delegate void deleNNSauction(AuctionInfo auctionInfo);
        [DisplayName("NNSauction")]
        public static event deleNNSauction onAuction;

        //通知 取消拍卖
        public delegate void deleNNSauctionDiscontinued(AuctionInfo auctionInfo);
        [DisplayName("NNSauctionDiscontinued")]
        public static event deleNNSauctionDiscontinued onAuctionDiscontinued;

        //通知 竞标  price为最终价格
        public delegate void deleNNSbet(AuctionInfo auctionInfo,byte[] buyer,BigInteger price);
        [DisplayName("NNSbet")]
        public static event deleNNSbet onBet;


        //存储区前缀
        //balanceMap key=addr+assetHash 地址合约内账户余额
        //fixedSellingInfoMap key=namehash  出售信息
        //txidVerifiedMap key=txid setmoneyin的txid是否被用于增加钱过

        //资产设置类
        public class assetSetting
        {
            public BigInteger enable;//是否允许使用
            public BigInteger valueMin;//最小使用量
            public BigInteger valueUnit;//最小使用单位（能被此整除）
            public BigInteger handlingFeeRate;//手续费比率*10000
            public string appTransferMethod;//资产的转账方法
        }

        public class OwnerInfo
        {
            public byte[] owner;//如果长度=0 表示没有初始化
            public byte[] register;
            public byte[] resolver;
            public BigInteger TTL;
            public byte[] parentOwner;//当此域名注册时，他爹的所有者，记录这个，则可以检测域名的爹变了
            //nameinfo 整合到一起
            public string domain;//如果长度=0 表示没有初始化
            public byte[] parenthash;
            public BigInteger root;//是不是根合约
        }

        public class TransferInfo
        {
            public byte[] from;
            public byte[] to;
            public BigInteger value;
        }

        //求购信息类
        public class OfferToBuyInfo
        {
            public byte[] offerid;
            public byte[] fullHash;
            public string fullDomain;
            public byte[] buyer;
            public byte[] assetHash;
            public BigInteger price;
            public BigInteger mortgagePayments;
        }

        //拍卖类
        public class AuctionInfo
        {
            public byte[] auctionid;
            public byte[] fullHash;
            public string fullDomain;
            public byte[] auctioner;
            public BigInteger startTimeStamp; //开始拍卖的时间
            public byte[] assetHash;
            public BigInteger startPrice; //设置的起始价格
            public BigInteger endPrice; //最终价格
            public BigInteger salePrice;    //每轮降价的数额
            public BigInteger mortgagePayments; //抵押金
        }

        #region 域名转hash算法
        //域名转hash算法
        //aaa.bb.test =>{"test","bb","aa"}
        static byte[] NameHash(string domain)
        {
            return SmartContract.Sha256(domain.AsByteArray());
        }
        static byte[] NameHashSub(byte[] roothash, string subdomain)
        {
            var bs = subdomain.AsByteArray();
            if (bs.Length == 0)
                return roothash;

            var domain = SmartContract.Sha256(bs).Concat(roothash);
            return SmartContract.Sha256(domain);
        }
        static byte[] NameHashWithSubHash(byte[] roothash, byte[] subhash)
        {
            var domain = subhash.Concat(roothash);
            return SmartContract.Sha256(domain);
        }
        static byte[] NameHashArray(string[] domainarray)
        {
            byte[] hash = NameHash(domainarray[0]);
            for (var i = 1; i < domainarray.Length; i++)
            {
                hash = NameHashSub(hash, domainarray[i]);
            }
            return hash;
        }

        #endregion

        public static BigInteger GetBalanceOf(byte[] who, byte[] assetHash)
        {
            //查看用户在本合约的账户余额
            StorageMap balanceMap = Storage.CurrentContext.CreateMap("balanceMap");
            return balanceMap.Get(who.Concat(assetHash)).AsBigInteger();
        }

        public static OwnerInfo GetOwnerInfo(byte[] fullHash)
        {
            deleDyncall centerCall = (deleDyncall)getSysSetting("domainCenterHash").ToDelegate();

            var ownerInfo = centerCall("getOwnerInfo", new object[1] { fullHash }) as OwnerInfo;
            return ownerInfo;
        }

        static TransferInfo getTxIn(byte[] txid, byte[] assetHash)
        {
            StorageMap txidVerifiedMap = Storage.CurrentContext.CreateMap("txidVerifiedMap");

            if (txidVerifiedMap.Get(txid).AsBigInteger() == 0)//如果這個交易已經處理過,就當get不到
            {
                object[] _p = new object[1];
                _p[0] = txid;
                deleDyncall dyncall = (deleDyncall)assetHash.ToDelegate();
                var info = dyncall("getTxInfo", _p);
                if (((object[])info).Length == 3)

                return info as TransferInfo;
            }
            var tInfo = new TransferInfo();
            tInfo.from = new byte[0];
            return tInfo;
        }

        private static string getFullStrForArray(string[] domainArray)
        {
            //根域名
            string fullDomainStr = domainArray[0];
            //其它逐级拼接
            for (var i = 1; i < domainArray.Length; i++)
            {
                fullDomainStr = string.Concat(domainArray[i], string.Concat(".", fullDomainStr));
            }

            return fullDomainStr;
        }

        //假为过期
        private static bool verifyExpires(BigInteger TTL)
        {
            var curTime = (BigInteger)Blockchain.GetHeader(Blockchain.GetHeight()).Timestamp;
            if (curTime <= TTL)
                return true;
            else
                throw new InvalidOperationException("error");
        }

        private static bool NEP5transfer(byte[] from, byte[] to, byte[] assetHash, BigInteger amount)
        {
            StorageMap balanceMap = Storage.CurrentContext.CreateMap("balanceMap");
            assetSetting assetSetting = getAssetSetting(assetHash);

            //多判断总比少判断好
            if (amount <= 0)
                throw new InvalidOperationException("error");
            if (from.Length != 20 || to.Length != 20)
                throw new InvalidOperationException("error");

            //构造入参
            object[] transInput = new object[3];
            transInput[0] = from;
            transInput[1] = to;
            transInput[2] = amount;

            //动态调用执行转账
            deleDyncall dyncall = (deleDyncall)assetHash.ToDelegate();
            bool result = (bool)dyncall(assetSetting.appTransferMethod, transInput);

            return result;
        }

        private static bool checkSpuerAdmin()
        {
            byte[] superAdminAddr = getSysSetting("superAdminAddr");
            if (superAdminAddr.Length == 0)
            {
                if (!Runtime.CheckWitness(initSuperAdminAddr))
                    throw new InvalidOperationException("error");
            }
            else
            {
                if (!Runtime.CheckWitness(superAdminAddr))
                    throw new InvalidOperationException("error");
            }

            return true;
        }

        //SysSetting
        //superAdminAddr
        //dividingPoolAddr
        //domainCenterHash
        public static bool setSysSetting(string key, byte[] value)
        {
            //只有管理员地址可以操作此方法
            if(!checkSpuerAdmin()) throw new InvalidOperationException("error");

            StorageMap sysSettingMap = Storage.CurrentContext.CreateMap("sysSettingMap");
            sysSettingMap.Put(key, value);

            onSetSysSetting(key, value);

            return true;
        }

        public static byte[] getSysSetting(string key)
        {
            StorageMap sysSettingMap = Storage.CurrentContext.CreateMap("sysSettingMap");
            return sysSettingMap.Get(key);
        }

        public static object setAssetSetting(byte[] assetHash, BigInteger enable, BigInteger valueMin, BigInteger valueUnit, BigInteger handlingFeeRate, string transferMethod)
        {
            //只有管理员地址可以操作此方法
            if (!checkSpuerAdmin()) throw new InvalidOperationException("error");

            if (valueMin < 0 || valueUnit < 0 || handlingFeeRate < 0)
                throw new InvalidOperationException("error");
            if (enable != 0 && enable != 1)
                throw new InvalidOperationException("error");

            assetSetting assetSetting = new assetSetting();
            assetSetting.enable = enable;
            assetSetting.valueMin = valueMin;
            assetSetting.valueUnit = valueUnit;
            assetSetting.handlingFeeRate = handlingFeeRate;
            assetSetting.appTransferMethod = transferMethod;

            StorageMap assetSettingMap = Storage.CurrentContext.CreateMap("assetSettingMap");
            assetSettingMap.Put(assetHash, assetSetting.Serialize());

            //通知资产设置事件
            onSetAssetSetting(assetHash, assetSetting);

            return true;
        }

        public static assetSetting getAssetSetting(byte[] assetHash)
        {
            StorageMap assetSettingMap = Storage.CurrentContext.CreateMap("assetSettingMap");
            byte[] data = assetSettingMap.Get(assetHash);
            assetSetting assetSetting = new assetSetting();
            assetSetting.enable = 0;
            if (data.Length > 0)
            {
                assetSetting = data.Deserialize() as assetSetting;
            }

            return assetSetting;
        }

        public static bool SetMoneyIn(byte[] txid, byte[] assetHash)
        {
            StorageMap txidVerifiedMap = Storage.CurrentContext.CreateMap("txidVerifiedMap");
            StorageMap balanceMap = Storage.CurrentContext.CreateMap("balanceMap");

            var tx = getTxIn(txid, assetHash);
            
            //没有这个NEP5转账交易
            if (tx.from.Length == 0)
                throw new InvalidOperationException("error");
            //NEP5转账交易目标必须是本合约
            if (tx.to.AsBigInteger() == ExecutionEngine.ExecutingScriptHash.AsBigInteger())
            {
                var isVerified = txidVerifiedMap.Get(txid).AsBigInteger();
                //这笔txid已经被用掉了,不能重复使用
                if (isVerified == 1)
                    throw new InvalidOperationException("error");
                //转账金额大于0才能操作
                if (tx.value <=0)
                    throw new InvalidOperationException("error");
                //存錢
                var balance = balanceMap.Get(tx.from.Concat(assetHash)).AsBigInteger();
                balance += tx.value;
                balanceMap.Put(tx.from.Concat(assetHash), balance);

                onSetMoneyIn(tx.from, assetHash, tx.value, txid);
                //記錄這個txid處理過了,只處理一次
                txidVerifiedMap.Put(txid, 1);
                return true;
            }
            throw new InvalidOperationException("error");
        }

        public static bool TransferIn(byte[] from, byte[] assetHash, BigInteger value)
        {
            //验证from的权限
            if (!Runtime.CheckWitness(from))
                throw new InvalidOperationException("error");
            if (value < 0)
                throw new InvalidOperationException("error");
            //明确to是本合约
            byte[] to = ExecutionEngine.ExecutingScriptHash;

            //获取转入资产的信息
            deleDyncall dyncall = (deleDyncall)assetHash.ToDelegate();
            object[] _p = new object[3];
            _p[0] = from;
            _p[1] = to;
            _p[2] = value;
            if (!(bool)dyncall("transfer", _p))//转账失败
                throw new Exception("param is wrong");

            StorageMap txidVerifiedMap = Storage.CurrentContext.CreateMap("txidVerifiedMap");
            StorageMap balanceMap = Storage.CurrentContext.CreateMap("balanceMap");

            //存錢
            byte[] txid = (ExecutionEngine.ScriptContainer as Transaction).Hash;
            var balance = balanceMap.Get(from.Concat(assetHash)).AsBigInteger();
            balance += value;
            balanceMap.Put(from.Concat(assetHash), balance);
            onSetMoneyIn(from, assetHash, value, txid);
            //記錄這個txid處理過了,只處理一次
            txidVerifiedMap.Put(txid, 1);
            return true;
        }

        public static bool GetMoneyBack(byte[] who, byte[] assetHash, BigInteger amount)
        {
            StorageMap balanceMap = Storage.CurrentContext.CreateMap("balanceMap");
            assetSetting assetSetting = getAssetSetting(assetHash);

            if (!Runtime.CheckWitness(who) && !checkSpuerAdmin())
                throw new InvalidOperationException("error");
            //多判断总比少判断好
            if (amount <= 0)
                throw new InvalidOperationException("error");
            if (who.Length != 20)
                throw new InvalidOperationException("error");

            var balance = balanceMap.Get(who.Concat(assetHash)).AsBigInteger();
            if (balance < amount)
                throw new InvalidOperationException("error");

            //NEP5转出
            bool result = NEP5transfer(ExecutionEngine.ExecutingScriptHash, who, assetHash, amount);
            if (result)
            {
                if (balance == amount)
                {
                    balanceMap.Delete(who.Concat(assetHash));
                }
                else
                {
                    balance -= amount;
                    balanceMap.Put(who.Concat(assetHash), balance);
                }

                onGetMoneyBack(who, assetHash, amount);
                return true;
            }

            throw new InvalidOperationException("error");
        }

        //退回全部余额
        public static bool GetMoneyBackAll(byte[] who, byte[] assetHash)
        {
            StorageMap balanceMap = Storage.CurrentContext.CreateMap("balanceMap");
            var amount = balanceMap.Get(who.Concat(assetHash)).AsBigInteger();

            return GetMoneyBack(who, assetHash, amount);
        }

        /// <summary>
        /// 求购
        /// </summary>
        /// <param name="fullhash"></param>
        /// <param name="price"></param>
        /// <returns></returns>
        public static bool OfferToBuy(byte[] buyer, string[] domainArray, byte[] assetHash, BigInteger price,BigInteger MortgagePayments)
        {
            if (!Runtime.CheckWitness(buyer))
                throw new InvalidOperationException("error");
            if (price <= 0)
                throw new InvalidOperationException("error");

            //不允许的资产不能定价
            assetSetting assetSetting = getAssetSetting(assetHash);
            if (assetSetting.enable != 1)
                throw new InvalidOperationException("error");
            //价格必须大于0
            if (price <= 0) throw new InvalidOperationException("error");
            //支付的抵押金数额必须大于
            BigInteger minMortgagePayments = getSysSetting("minMortgagePayments").AsBigInteger();
            if (MortgagePayments < minMortgagePayments)
                throw new InvalidOperationException("error");

            //合约限制最小价格为0.1,并且小数点后面不能超过一位（按照精度2换算），NNC为10
            if (price < assetSetting.valueMin || price % assetSetting.valueUnit > 0)
                throw new InvalidOperationException("error");
            //获取域名的fullhash
            byte[] fullHash = NameHashArray(domainArray);
            //获取这个交易的txid
            byte[] offerid = (ExecutionEngine.ScriptContainer as Transaction).Hash;
            //获取求购信息
            OfferToBuyInfo offerToBuyInfo = GetOfferToBuyInfo(offerid);
            if (offerToBuyInfo.fullHash.Length != 0)//已经有了这个资产的求购信息  同资产只能有一个求购
                throw new InvalidOperationException("error");
            //先获取这个域名的信息
            OwnerInfo ownerInfo = GetOwnerInfo(fullHash);
            //域名没有初始化或者已经到期了不能求购    ？？？？？？？先写着  再想想有没有必要限制
            if (ownerInfo.owner.Length == 0|| !verifyExpires(ownerInfo.TTL))
                throw new InvalidOperationException("error");
            //不能求购属于自己的合约
            if (buyer == ownerInfo.owner)
                throw new InvalidOperationException("error");
            //看看有没有这么多钱
            StorageMap balanceMap = Storage.CurrentContext.CreateMap("balanceMap");
            var balanceOfBuyer = balanceMap.Get(buyer.Concat(assetHash)).AsBigInteger();
            balanceOfBuyer = balanceOfBuyer - price;
            //扣钱 
            if (balanceOfBuyer > 0)
                balanceMap.Put(buyer.Concat(assetHash), balanceOfBuyer);
            else if (balanceOfBuyer == 0)
                balanceMap.Delete(buyer.Concat(assetHash));
            else
                throw new InvalidOperationException("error");
            onDexTransfer(buyer,new byte[] { }, assetHash, price);

            //扣除抵押金
            var mortgageAssetHash = getSysSetting("mortgageAssetHash");
            var nncBalanceOfBuyer = balanceMap.Get(buyer.Concat(mortgageAssetHash)).AsBigInteger();
            //var nncBalanceOfContract = balanceMap.Get(ExecutionEngine.ExecutingScriptHash.Concat(nncHash)).AsBigInteger();
            nncBalanceOfBuyer = nncBalanceOfBuyer - MortgagePayments;
            if (nncBalanceOfBuyer > 0)
            {
                balanceMap.Put(buyer.Concat(mortgageAssetHash), nncBalanceOfBuyer);
            }
            else if(nncBalanceOfBuyer == 0)
                balanceMap.Delete(buyer.Concat(mortgageAssetHash));
            else
                throw new InvalidOperationException("error");
            onDexTransfer(buyer, new byte[] { }, mortgageAssetHash, MortgagePayments);

            //更新这个域名的求购信息
            offerToBuyInfo = new OfferToBuyInfo {offerid = offerid, fullHash = fullHash , buyer = buyer, assetHash = assetHash, price = price,fullDomain= getFullStrForArray(domainArray),mortgagePayments = MortgagePayments}; 
            PutOfferToBuyInfo(offerid, offerToBuyInfo);
            onOfferToBuy(offerToBuyInfo);
            return true;
        }

        /// <summary>
        /// 撤销上架的求购
        /// </summary>
        /// <param name="buyer"></param>
        /// <param name="fullHash"></param>
        /// <param name="assetHash"></param>
        /// <returns></returns>
        public static bool DiscontinueOfferToBuy(byte[] offerid)
        {

            //获取求购信息
            OfferToBuyInfo  offerToBuyInfo =  GetOfferToBuyInfo(offerid);
            if (offerToBuyInfo.fullHash.Length == 0)//没求购过别浪费时间了
                throw new InvalidOperationException("error");
            var buyer = offerToBuyInfo.buyer;
            var assetHash = offerToBuyInfo.assetHash;
            var fullHash = offerToBuyInfo.fullHash;
            //验证权限
            if (!Runtime.CheckWitness(buyer) && !checkSpuerAdmin())
                throw new InvalidOperationException("error");
            //把钱退给求购者
            var price = offerToBuyInfo.price;
            StorageMap balanceMap = Storage.CurrentContext.CreateMap("balanceMap");
            //var balanceOfContract = balanceMap.Get(ExecutionEngine.ExecutingScriptHash.Concat(assetHash)).AsBigInteger();//合约
            var balanceOfBuyer = balanceMap.Get(buyer.Concat(assetHash)).AsBigInteger(); //求购者
            //玩家加钱
            balanceOfBuyer = balanceOfBuyer + price;
            balanceMap.Put(buyer.Concat(assetHash), balanceOfBuyer);
            onDexTransfer(new byte[] { },buyer,assetHash,price);
            //归还抵押金
            var mortgageAssetHash = getSysSetting("mortgageAssetHash");
            var mortgagePayments = offerToBuyInfo.mortgagePayments;
            var nncBalanceOfBuyer = balanceMap.Get(buyer.Concat(mortgageAssetHash)).AsBigInteger(); //求购者
            nncBalanceOfBuyer = nncBalanceOfBuyer + mortgagePayments;
            balanceMap.Put(buyer.Concat(mortgageAssetHash), nncBalanceOfBuyer);
            onDexTransfer(new byte[] { }, buyer, mortgageAssetHash, mortgagePayments);

            onOfferToBuyDiscontinued(offerToBuyInfo);//通知
            return DeleteOfferToBuyInfo(offerid);
        }

        /// <summary>
        /// 出售
        /// </summary>
        /// <param name="buyer">求购者</param>
        /// <param name="fullHash"></param>
        /// <param name="assetHash"></param>
        /// <returns></returns>
        public static bool Sell(byte[] offerid)
        {
            //获取求购信息
            OfferToBuyInfo offerToBuyInfo = GetOfferToBuyInfo(offerid);
            if (offerToBuyInfo.fullHash.Length == 0)//没求购过别浪费时间了
                throw new InvalidOperationException("error");
            var fullHash = offerToBuyInfo.fullHash;
            var buyer = offerToBuyInfo.buyer;
            var assetHash = offerToBuyInfo.assetHash;
            //先获取这个域名的信息
            OwnerInfo ownerInfo = GetOwnerInfo(fullHash);
            if (ownerInfo.owner.Length == 0 || !verifyExpires(ownerInfo.TTL))//验证域名是否有效
                throw new InvalidOperationException("error");
            //验证权限
            var seller = ownerInfo.owner;
            if (!Runtime.CheckWitness(seller))
                throw new InvalidOperationException("error");
            //进行域名的转让操作（域名所有权:卖家=>买家）
            deleDyncall centerCall = (deleDyncall)getSysSetting("domainCenterHash").ToDelegate();

            var result = (byte[])centerCall("owner_SetOwner", new object[3] { seller, fullHash, buyer });
            if (result.AsBigInteger() != 1) //如果域名所有权转移失败，返回失败
                throw new InvalidOperationException("error");

            //把钱转给卖家
            assetSetting assetSetting = getAssetSetting(assetHash);
            BigInteger handlingFee = offerToBuyInfo.price * assetSetting.handlingFeeRate / fixedNumber;//handlingFeeRate是事先乘10000存储的
            StorageMap balanceMap = Storage.CurrentContext.CreateMap("balanceMap");
            var balanceOfSeller = balanceMap.Get(seller.Concat(assetHash)).AsBigInteger();//卖家
            balanceOfSeller = balanceOfSeller + offerToBuyInfo.price - handlingFee;
            //给卖方增加钱(扣除手续费)
            balanceMap.Put(seller.Concat(assetHash), balanceOfSeller);
            onDexTransfer(new byte[] { }, seller, assetHash, offerToBuyInfo.price - handlingFee);

            //转移手续费
            //发送手续费到分红池
            if (handlingFee > 0)
            {
                if (!NEP5transfer(ExecutionEngine.ExecutingScriptHash, getSysSetting("dividingPoolAddr"), assetHash, handlingFee))
                    throw new Exception("NEP5transfer is wrong");
            }

            //把押金还给买家
            var mortgageAssetHash = getSysSetting("mortgageAssetHash");
            var nncBalanceOfOffer = balanceMap.Get(buyer.Concat(mortgageAssetHash)).AsBigInteger();
            nncBalanceOfOffer += offerToBuyInfo.mortgagePayments;
            balanceMap.Put(buyer.Concat(mortgageAssetHash), nncBalanceOfOffer);
            onDexTransfer(new byte[] { },buyer,mortgageAssetHash,offerToBuyInfo.mortgagePayments);
            //删除此条求购信息
            DeleteOfferToBuyInfo( offerid);
            //通知
            onSell(seller, offerToBuyInfo);
            return true;
        }

        /// <summary>
        /// 存储求购信息
        /// </summary>
        /// <param name="fullHash"></param>
        /// <param name="map"></param>
        /// <returns></returns>
        public static bool PutOfferToBuyInfo(byte[] offerid, OfferToBuyInfo map)
        {
            StorageMap offerToBuyInfoMap = Storage.CurrentContext.CreateMap("offerToBuyInfoMap");
            offerToBuyInfoMap.Put(offerid, map.Serialize());
            return true;
        }

        /// <summary>
        /// 获取求购信息
        /// </summary>
        /// <param name="fullHash"></param>
        /// <returns></returns>
        public static OfferToBuyInfo GetOfferToBuyInfo(byte[] offerid)
        {
            StorageMap offerToBuyInfoMap = Storage.CurrentContext.CreateMap("offerToBuyInfoMap");
            byte[] bytes = offerToBuyInfoMap.Get(offerid);
            if (bytes.Length == 0)
                return new OfferToBuyInfo() {offerid=new byte[] { },  fullHash = new byte[] { } ,price = 0};
            return bytes.Deserialize() as OfferToBuyInfo;
        }

        /// <summary>
        /// 删除求购信息
        /// </summary>
        /// <param name="buyer"></param>
        /// <param name="fullHash"></param>
        /// <param name="assetHash"></param>
        /// <returns></returns>
        public static bool DeleteOfferToBuyInfo(byte[] offerid)
        {
            StorageMap offerToBuyInfoMap = Storage.CurrentContext.CreateMap("offerToBuyInfoMap");
            if (offerToBuyInfoMap.Get(offerid).Length == 0)
                throw new InvalidOperationException("error");
            offerToBuyInfoMap.Delete(offerid);
            return true;
        }

        /// <summary>
        /// 拍卖某个域名
        /// </summary>
        /// <param name="domainArray"></param>
        /// <param name="startPrice"></param>
        /// <param name="endPrice"></param>
        /// <param name="ratio"></param>
        /// <returns></returns>
        public static bool Auction(string[] domainArray, byte[] assetHash, BigInteger startPrice,BigInteger endPrice, BigInteger salePrice,BigInteger mortgagePayments)
        {
            //不允许的资产不能定价
            assetSetting assetSetting = getAssetSetting(assetHash);
            if (assetSetting.enable != 1)
                throw new InvalidOperationException("error");

            //价格必须大于0
            if (startPrice <= 0|| endPrice<=0|| endPrice > startPrice) throw new InvalidOperationException("error");

            //合约限制最小价格为0.1,并且小数点后面不能超过一位（按照精度2换算），NNC为10
            if (startPrice < assetSetting.valueMin || startPrice % assetSetting.valueUnit > 0|| endPrice < assetSetting.valueMin || endPrice % assetSetting.valueUnit > 0)
                throw new InvalidOperationException("error");
            //限制每次降价的精度
            if ((salePrice < assetSetting.valueMin || salePrice % assetSetting.valueUnit > 0)&&salePrice!=0)
                throw new InvalidOperationException("error");

            //验证押金
            BigInteger minMortgagePayments = getSysSetting("minMortgagePayments").AsBigInteger();
            if (mortgagePayments < minMortgagePayments) throw new InvalidOperationException("error");

            byte[] fullHash = NameHashArray(domainArray);
            //先获取这个域名的信息
            OwnerInfo ownerInfo = GetOwnerInfo(fullHash);
            var auctioner = ownerInfo.owner;
            //域名没有初始化不能上架
            if (ownerInfo.owner.Length == 0)
                throw new InvalidOperationException("error");
            //域名已经到期了不能上架
            if (!verifyExpires(ownerInfo.TTL))
                throw new InvalidOperationException("error");
            //验证权限
            if (!Runtime.CheckWitness(auctioner))
                throw new InvalidOperationException("error");

            //将域名抵押给本合约（域名所有权:卖家=>DEX合约）
            //本合约的scripthash
            byte[] scriptHash = ExecutionEngine.ExecutingScriptHash;
            deleDyncall centerCall = (deleDyncall)getSysSetting("domainCenterHash").ToDelegate();
            var result = (byte[])centerCall("owner_SetOwner", new object[3] { auctioner, fullHash, scriptHash });
            if (result.AsBigInteger() != 1)
                throw new InvalidOperationException("error");

            //扣除押金
            var mortgageAssetHash = getSysSetting("mortgageAssetHash");
            StorageMap balanceMap = Storage.CurrentContext.CreateMap("balanceMap");
            var balanceOfBuyer = balanceMap.Get(auctioner.Concat(mortgageAssetHash)).AsBigInteger();
            balanceOfBuyer = balanceOfBuyer - mortgagePayments;
            if (balanceOfBuyer > 0)
            {
                balanceMap.Put(auctioner.Concat(mortgageAssetHash), balanceOfBuyer);
            }
            else if (balanceOfBuyer == 0)
            {
                balanceMap.Delete(auctioner.Concat(mortgageAssetHash));
            }
            else
            {
                throw new Exception("no money");
            }
            onDexTransfer(auctioner,new byte[] { },mortgageAssetHash,mortgagePayments);
            //获取开始拍卖的时间戳
            var timeStamp = Blockchain.GetHeader(Blockchain.GetHeight()).Timestamp;
            //获取交易id
            var auctionid = (ExecutionEngine.ScriptContainer as Transaction).Hash;
            //记录拍卖的信息
            AuctionInfo auctionInfo = new AuctionInfo()
            {
                auctionid = auctionid,
                fullDomain = getFullStrForArray(domainArray) ,
                fullHash = fullHash,
                auctioner = auctioner ,
                startPrice = startPrice,
                endPrice = endPrice,
                salePrice = salePrice,
                startTimeStamp = timeStamp,
                assetHash = assetHash,
                mortgagePayments = mortgagePayments,
            };
            StorageMap auctionInfoMap = Storage.CurrentContext.CreateMap("auctionInfoMap");
            //如果已经存在这个id报错
            if (auctionInfoMap.Get(auctionid).Length != 0)
                throw new Exception("error");
            auctionInfoMap.Put(auctionid, auctionInfo.Serialize());
            //记录当前这个域名正在拍卖的场次是什么
            StorageMap auctionInfoCurrentMap = Storage.CurrentContext.CreateMap("auctionInfoCurrentMap");
            auctionInfoCurrentMap.Put(fullHash,auctionid);
            onAuction(auctionInfo);
            return true;
        }

        /// <summary>
        /// 取消域名拍卖
        /// </summary>
        /// <param name="fullhash"></param>
        /// <returns></returns>
        public static bool DiscontinueAuction(byte[] auctionid)
        {
            //获取域名的拍卖情况
            StorageMap auctionInfoMap = Storage.CurrentContext.CreateMap("auctionInfoMap");
            var bytes = auctionInfoMap.Get(auctionid);
            if (bytes.Length == 0)
                throw new InvalidOperationException("error");
            AuctionInfo auctionInfo = bytes.Deserialize() as AuctionInfo;
            //验证权限
            if (!Runtime.CheckWitness(auctionInfo.auctioner)&& !checkSpuerAdmin())
                throw new InvalidOperationException("error");
            //将域名退回给卖家（域名所有权:DEX合约=>卖家）
            byte[] scriptHash = ExecutionEngine.ExecutingScriptHash;
            deleDyncall centerCall = (deleDyncall)getSysSetting("domainCenterHash").ToDelegate();
            var result = (byte[])centerCall("owner_SetOwner", new object[3] { scriptHash, auctionInfo.fullHash, auctionInfo.auctioner });
            //if (result.AsBigInteger() != 1)
            //    throw new InvalidOperationException("error");

            //归还押金
            var mortgageAssetHash = getSysSetting("mortgageAssetHash");
            StorageMap balanceMap = Storage.CurrentContext.CreateMap("balanceMap");
            var balanceOfBuyer = balanceMap.Get(auctionInfo.auctioner.Concat(mortgageAssetHash)).AsBigInteger();
            balanceOfBuyer = balanceOfBuyer + auctionInfo.mortgagePayments;
            balanceMap.Put(auctionInfo.auctioner.Concat(mortgageAssetHash), balanceOfBuyer);
            onDexTransfer(new byte[] { }, auctionInfo.auctioner,  mortgageAssetHash, auctionInfo.mortgagePayments);

            auctionInfoMap.Delete(auctionid);
            onAuctionDiscontinued(auctionInfo);
            return true;
        }

        /// <summary>
        /// 竞拍
        /// </summary>
        /// <param name="fullHash"></param>
        /// <param name="price">价格，价格合约内计算，但是要求传入是以防前端计算错误误导用户</param>
        /// <returns></returns>
        public static bool Bet(byte[] buyer,byte[] auctionid, byte[] assetHash, BigInteger price)
        {
            if (!Runtime.CheckWitness(buyer))
                throw new InvalidOperationException("error");
            //获取域名的拍卖情况
            AuctionInfo auctionInfo = GetAuctionInfo(auctionid);
            //自己不能竞拍自己上架的域名
            if (buyer == auctionInfo.auctioner)
                throw new InvalidOperationException("error");
            if (auctionInfo.fullDomain == "")
                throw new InvalidOperationException("error");
            //验证资产种类
            if (auctionInfo.assetHash != assetHash)
                throw new InvalidOperationException("error");
            //获取当前的时间戳
            var currentTimeStamp = Blockchain.GetHeader(Blockchain.GetHeight()).Timestamp;
            //距离开始拍卖已经过了多久
            var timeStamp = currentTimeStamp - auctionInfo.startTimeStamp;

            //获取降价的间隔
            var interval = getSysSetting("interval").AsBigInteger();

            //X小时一轮 开拍为0阶段  X小时后为1阶段 X*2小时后为2阶段    X*60*60
            var phase = timeStamp / interval;
            //计算当前的价格
            var currentPrice = auctionInfo.startPrice - auctionInfo.salePrice* phase;
            if (currentPrice < auctionInfo.endPrice)
                currentPrice = auctionInfo.endPrice;
            //对比下用户从前端获取到的价格
            if (price != currentPrice)
                throw new InvalidOperationException("error");
            //检查用户钱够不够
            StorageMap balanceMap = Storage.CurrentContext.CreateMap("balanceMap");
            var balanceOfBuyer = balanceMap.Get(buyer.Concat(assetHash)).AsBigInteger();//买家
            if (balanceOfBuyer < price)
                throw new InvalidOperationException("error");

            //合约将域名转让给买家
            byte[] scriptHash = ExecutionEngine.ExecutingScriptHash;
            deleDyncall centerCall = (deleDyncall)getSysSetting("domainCenterHash").ToDelegate();
            var result = (byte[])centerCall("owner_SetOwner", new object[3] { scriptHash, auctionInfo.fullHash, buyer });
            if (result.AsBigInteger() != 1)
                throw new InvalidOperationException("error");

            //购买者扣钱
            balanceOfBuyer = balanceOfBuyer - price;
            if (balanceOfBuyer == 0)
                balanceMap.Delete(buyer.Concat(assetHash));
            else
                balanceMap.Put(buyer.Concat(assetHash),balanceOfBuyer);
            onDexTransfer(buyer,new byte[] { },assetHash,price);

            //转钱给拍卖者
            //计算手续费
            assetSetting assetSetting = getAssetSetting(assetHash);
            BigInteger handlingFee = price * assetSetting.handlingFeeRate / fixedNumber;//handlingFeeRate是事先乘10000存储的
            //给卖方增加钱(扣除手续费)
            var auctioner = auctionInfo.auctioner;
            var balanceOfAuctioner = balanceMap.Get(auctioner.Concat(assetHash)).AsBigInteger();
            balanceMap.Put(auctioner.Concat(assetHash), balanceOfAuctioner + price - handlingFee);
            onDexTransfer(new byte[] { }, auctioner, assetHash, price - handlingFee);
            //归还卖方的抵押金
            var mortgageAssetHash = getSysSetting("mortgageAssetHash");
            var nncBalanceOfAuctioner = balanceMap.Get(auctioner.Concat(mortgageAssetHash)).AsBigInteger();
            balanceMap.Put(auctioner.Concat(mortgageAssetHash), nncBalanceOfAuctioner + auctionInfo.mortgagePayments);
            onDexTransfer(new byte[] { }, auctioner, mortgageAssetHash, auctionInfo.mortgagePayments);

            if (handlingFee > 0)
            {
                if (!NEP5transfer(ExecutionEngine.ExecutingScriptHash, getSysSetting("dividingPoolAddr"), assetHash, handlingFee))
                    throw new Exception("NEP5transfer is wrong");
            }


            //删除拍卖信息
            StorageMap auctionInfoMap = Storage.CurrentContext.CreateMap("auctionInfoMap");
            auctionInfoMap.Delete(auctionid);
            onBet(auctionInfo, buyer ,price);
            return true;
        }

        public static AuctionInfo GetAuctionInfoByFullhash(byte[] fullhash)
        {
            StorageMap auctionInfoCurrentMap = Storage.CurrentContext.CreateMap("auctionInfoCurrentMap");
            byte[] auctionid = auctionInfoCurrentMap.Get(fullhash);
            return GetAuctionInfo(auctionid);
        }

        public static BigInteger GetAuctionPriceByFullhash(byte[] fullhash)
        {
            StorageMap auctionInfoCurrentMap = Storage.CurrentContext.CreateMap("auctionInfoCurrentMap");
            byte[] auctionid = auctionInfoCurrentMap.Get(fullhash);
            return GetAuctionPrice(auctionid);
        }

        public static AuctionInfo GetAuctionInfo(byte[] auctionid)
        {
            //获取域名的拍卖情况
            StorageMap auctionInfoMap = Storage.CurrentContext.CreateMap("auctionInfoMap");
            var bytes = auctionInfoMap.Get(auctionid);
            AuctionInfo auctionInfo = new AuctionInfo() { fullHash=new byte[] { } , assetHash= new byte[] { }, auctioner =new byte[] { }, endPrice=0, fullDomain="", salePrice = 0, startPrice=0, startTimeStamp=0 };
            if (bytes.Length == 0)
                return auctionInfo;
            auctionInfo = bytes.Deserialize() as AuctionInfo;
            return auctionInfo;
        }

        public static BigInteger GetAuctionPrice(byte[] auctionid)
        {
            //获取域名的拍卖情况
            AuctionInfo auctionInfo = GetAuctionInfo(auctionid);
            if (auctionInfo.fullDomain == "")
                return 0;
            //获取当前的时间戳
            var currentTimeStamp = Blockchain.GetHeader(Blockchain.GetHeight()).Timestamp;
            //距离开始拍卖已经过了多久
            var timeStamp = currentTimeStamp - auctionInfo.startTimeStamp;
            //获取降价的间隔
            var interval = getSysSetting("interval").AsBigInteger();
            var phase = timeStamp / interval;
            //计算当前的价格
            var currentPrice = auctionInfo.startPrice - auctionInfo.salePrice * phase;
            if (currentPrice < auctionInfo.endPrice)
                currentPrice = auctionInfo.endPrice;
            return currentPrice;
        }

        public static object Main(string method, object[] args)
        {
            string magic = "20190527";

            //UTXO转账转入转出都不允许
            if (Runtime.Trigger == TriggerType.Verification || Runtime.Trigger == TriggerType.VerificationR)
            {
                throw new InvalidOperationException("error");
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                var callScript = ExecutionEngine.CallingScriptHash;

                //充值
                if (method == "setMoneyIn")
                {
                    return SetMoneyIn((byte[])args[0], (byte[])args[1]);
                }
                if (method == "transferIn")
                {
                    return TransferIn((byte[])args[0], (byte[])args[2], (BigInteger)args[1]);
                }
                //提现
                if (method == "getMoneyBack")
                {
                    return GetMoneyBack((byte[])args[0], (byte[])args[1], (BigInteger)args[2]);
                }
                //提现
                if (method == "getMoneyBackAll")
                {
                    return GetMoneyBackAll((byte[])args[0], (byte[])args[1]);
                }
                //求购
                if (method == "offerToBuy")
                {
                    return OfferToBuy((byte[])args[0], (string[])args[1], (byte[])args[2], (BigInteger)args[3], (BigInteger)args[4]);
                }
                //取消求购
                if (method == "discontinueOfferToBuy")
                {
                    return DiscontinueOfferToBuy((byte[])args[0]);
                }
                //出售给求购者
                if (method == "sell")
                {
                    return Sell((byte[])args[0]);
                }
                //用户开始荷兰拍
                if (method == "auction")
                {
                    return Auction((string[])args[0], (byte[])args[1], (BigInteger)args[2], (BigInteger)args[3], (BigInteger)args[4], (BigInteger)args[5]);
                }
                //取消拍卖
                if (method == "discontinueAuction")
                {
                    return DiscontinueAuction((byte[])args[0]);
                }
                //竞拍
                if (method == "bet")
                {
                    return Bet((byte[])args[0], (byte[])args[1], (byte[])args[2], (BigInteger)args[3]);
                }
                #region 合约设置方法
                if (method == "setSysSetting")
                {
                    return setSysSetting((string)args[0], (byte[])args[1]);
                }
                if (method == "getSysSetting")
                {
                    return getSysSetting((string)args[0]);
                }
                if (method == "setAssetSetting")
                {
                    return setAssetSetting((byte[])args[0], (BigInteger)args[1], (BigInteger)args[2], (BigInteger)args[3], (BigInteger)args[4],(string) args[5]);
                }
                if (method == "getAssetSetting")
                {
                    return getAssetSetting((byte[])args[0]);
                }
                #endregion

                if (method == "getBalanceOf")
                {
                    return GetBalanceOf((byte[])args[0], (byte[])args[1]);
                }
                if (method == "getOfferToBuyerInfo")
                {
                    var info = GetOfferToBuyInfo((byte[])args[0]);
                    return info;
                }
                if (method == "getOfferToBuyPrice")
                {
                    var info = GetOfferToBuyInfo((byte[])args[0]);
                    return info.price;
                }
                if (method == "getAuctionInfo")
                {
                    return GetAuctionInfo((byte[])args[0]);
                }
                if (method == "getAuctionPrice")
                {
                    return GetAuctionPrice((byte[])args[0]);
                }
                if (method == "getAuctionInfoByFullhash")
                {
                    return GetAuctionInfoByFullhash((byte[])args[0]);
                }
                if (method == "getAuctionPriceByFullhash")
                {
                    return GetAuctionPriceByFullhash((byte[])args[0]);
                }
            }
            throw new InvalidOperationException("error");
        }
    }

}
