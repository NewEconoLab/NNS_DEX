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
        static readonly byte[] initSuperAdminAddr = Helper.ToScriptHash("AMNFdmGuBrU1iaMbYd63L1zucYMdU9hvQU");

        ////分红池应该用统一地址，测试先另外地址
        //static readonly byte[] dividingPool = Helper.ToScriptHash("AeaWf2v7MHGpzxH4TtBAu5kJRp5mRq2DQG");

        ////域名中心跳板合约地址
        //[Appcall("348387116c4a75e420663277d9c02049907128c7")]
        //static extern object centerCall(string method, object[] arr);

        //动态合约调用委托
        delegate object deleDyncall(string method, object[] arr);

        //// NNC合约地址
        //// NNC转账
        //[Appcall("fc732edee1efdf968c23c20a9628eaa5a6ccb934")]
        //static extern object nncCall(string method, object[] arr);

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

        //通知 上架
        public delegate void deleNNSfixedSellingLaunched(fixedSellingInfo fixedSellingInfo);
        [DisplayName("NNSfixedSellingLaunched")]
        public static event deleNNSfixedSellingLaunched onLaunched;

        //通知 下架
        public delegate void deleNNSfixedSellingDiscontinued(fixedSellingInfo fixedSellingInfo);
        [DisplayName("NNSfixedSellingDiscontinued")]
        public static event deleNNSfixedSellingLaunched onDiscontinued;

        //通知 购买（售出）
        public delegate void deleNNSfixedSellingBuy(byte[] addr, fixedSellingInfo fixedSellingInfo);
        [DisplayName("NNSfixedSellingBuy")]
        public static event deleNNSfixedSellingBuy onBuy;

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
            public string transferMethod;//资产的转账方法
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

        public class fixedSellingInfo
        {
            public byte[] fullHash;
            public string fullDomain;
            public byte[] seller;
            public byte[] assetHash;
            public BigInteger price;
            //public BigInteger TTL; 怕引起误会，认为TTL是不变的，应该统一从域名中心取
        }

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

        private static byte[] getFullNamehashForArray(string[] domainArray)
        {
            deleDyncall centerCall = (deleDyncall)getSysSetting("domainCenterHash").ToDelegate();

            return centerCall("nameHashArray", new object[] { domainArray }) as byte[];
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
                return false;
        }

        private static bool NEP5transfer(byte[] from, byte[] to, byte[] assetHash, BigInteger amount)
        {
            StorageMap balanceMap = Storage.CurrentContext.CreateMap("balanceMap");
            assetSetting assetSetting = getAssetSetting(assetHash);

            //多判断总比少判断好
            if (amount <= 0)
                return false;
            if (from.Length != 20 || to.Length != 20)
                return false;

            //构造入参
            object[] transInput = new object[3];
            transInput[0] = from;
            transInput[1] = to;
            transInput[2] = amount;

            //动态调用执行转账
            deleDyncall dyncall = (deleDyncall)assetHash.ToDelegate();
            bool result = (bool)dyncall(assetSetting.transferMethod, transInput);

            return result;
        }

        //存储一口价销售信息
        private static void saveFixedSellingInfo(string[] domainArray, byte[] seller, byte[] assetHash, BigInteger price)
        {   
            byte[] namehash = getFullNamehashForArray(domainArray);

            fixedSellingInfo fixedSellingInfo = new fixedSellingInfo();
            fixedSellingInfo.fullHash = namehash;
            fixedSellingInfo.fullDomain = getFullStrForArray(domainArray);
            fixedSellingInfo.seller = seller;
            fixedSellingInfo.assetHash = assetHash;
            fixedSellingInfo.price = price;
            //fixedSellingInfo.TTL = TTL;

            StorageMap fixedSellingInfoMap = Storage.CurrentContext.CreateMap("fixedSellingInfoMap");
            fixedSellingInfoMap.Put(namehash, fixedSellingInfo.Serialize());

            //通知
            onLaunched(fixedSellingInfo);
        }

        //用namehash获取一口价销售信息
        private static fixedSellingInfo getfixedSellingInfoByFullhash(byte[] fullHash)
        {
            StorageMap fixedSellingInfoMap = Storage.CurrentContext.CreateMap("fixedSellingInfoMap");
            var info = fixedSellingInfoMap.Get(fullHash);
            if (info.Length > 0)
                return info.Deserialize() as fixedSellingInfo;
            else
                return new fixedSellingInfo();
        }

        ////用NNS明文获取一口价销售信息
        //public static fixedSellingInfo getfixedSellingInfoByDomainarray(string[] domainArray)
        //{
        //    return getfixedSellingInfoByFullhash(getFullNamehashForArray(domainArray));
        //}

        private static bool checkSpuerAdmin()
        {
            byte[] superAdminAddr = getSysSetting("superAdminAddr");
            if (superAdminAddr.Length == 0)
            {
                if (!Runtime.CheckWitness(initSuperAdminAddr))
                    return false;
            }
            else
            {
                if (!Runtime.CheckWitness(superAdminAddr))
                    return false;
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
            if(!checkSpuerAdmin()) return false;

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
            if (!checkSpuerAdmin()) return false;

            if (valueMin < 0 || valueUnit < 0 || handlingFeeRate < 0)
                return false;
            if (enable != 0 && enable != 1)
                return false;

            assetSetting assetSetting = new assetSetting();
            assetSetting.enable = enable;
            assetSetting.valueMin = valueMin;
            assetSetting.valueUnit = valueUnit;
            assetSetting.handlingFeeRate = handlingFeeRate;
            assetSetting.transferMethod = transferMethod;

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

        //上架
        public static object Launch(string[] domainArray,byte[] assetHash, BigInteger price)
        {
            //不允许的资产不能定价
            assetSetting assetSetting = getAssetSetting(assetHash);
            if (assetSetting.enable != 1)
                return false;

            //售价必须大于0
            if (price <= 0) return false;

            //合约限制最小价格为0.1,并且小数点后面不能超过一位（按照精度2换算），NNC为10
            if (price < assetSetting.valueMin || price % assetSetting.valueUnit > 0)
                return false;

            byte[] fullhash = getFullNamehashForArray(domainArray);

            //先获取这个域名的拥有者
            OwnerInfo ownerInfo = GetOwnerInfo(fullhash);
            //域名没有初始化不能上架
            if (ownerInfo.owner.Length == 0)
                return false;
            var seller = ownerInfo.owner;
            //没有域名所有权不能上架
            if (!Runtime.CheckWitness(seller))
                return false;
            //域名已经到期了不能上架
            if (!verifyExpires(ownerInfo.TTL))
                return false;

            //将域名抵押给本合约（域名所有权:卖家=>DEX合约）
            deleDyncall centerCall = (deleDyncall)getSysSetting("domainCenterHash").ToDelegate();

            var result = (byte[])centerCall("owner_SetOwner", new object[3] { seller, fullhash, ExecutionEngine.ExecutingScriptHash });
            if (result.AsBigInteger() != 1)
                return false;

            //存储上架信息，并通知
            saveFixedSellingInfo(domainArray , seller, assetHash, price);
        
            return true;
        }

        //下架
        public static object Discontinue(byte[] fullHash)
        {
            StorageMap fixedSellingInfoMap = Storage.CurrentContext.CreateMap("fixedSellingInfoMap");
            fixedSellingInfo FSI = getfixedSellingInfoByFullhash(fullHash);

            //无出售信息不能下架
            if (FSI.fullHash.Length == 0) return false;
            //非出售者或超级管理员不可以下架
            if (!Runtime.CheckWitness(FSI.seller) && !checkSpuerAdmin()) return false;

            //var price = FSI.price;
            //var seller = FSI.seller;
            //if (FSI.price == 0)
            //    return false;

            //将域名所有权转回出售者（域名所有权:DEX合约=>卖家）
            deleDyncall centerCall = (deleDyncall)getSysSetting("domainCenterHash").ToDelegate();

            var result = (byte[])centerCall("owner_SetOwner", new object[3] { ExecutionEngine.ExecutingScriptHash, fullHash, FSI.seller });
            if (result.AsBigInteger() != 1)
                return false;
            //清除出售信息
            fixedSellingInfoMap.Delete(fullHash);
            //通知
            onDiscontinued(FSI);

            return true;
        }

        public static object Buy(byte[] buyer, byte[] fullHash)
        {
            //只能用本人的钱购买
            if (!Runtime.CheckWitness(buyer))
                return false;

            //先获取这个域名的信息
            OwnerInfo ownerInfo = GetOwnerInfo(fullHash);
            //域名没有初始化不能买
            if (ownerInfo.owner.Length == 0)
                return false;
            //域名已经到期了不能购买
            if (!verifyExpires(ownerInfo.TTL))
                return false;          

            //获取出售信息
            fixedSellingInfo FSI = getfixedSellingInfoByFullhash(fullHash);
            //无出售信息不能买
            if (FSI.fullHash.Length == 0) return false;

            var seller = FSI.seller;
            //不允许自己买自己的NNS
            if (seller.AsBigInteger() == buyer.AsBigInteger()) return false;

            var assetHash = FSI.assetHash;
            var price = FSI.price;          

            StorageMap balanceMap = Storage.CurrentContext.CreateMap("balanceMap");
            var balanceOfBuyer = balanceMap.Get(buyer.Concat(assetHash)).AsBigInteger();
            
            //钱不够不能买
            if (balanceOfBuyer < price)
                return false;

            //进行域名的转让操作（域名所有权:DEX合约=>买家）
            deleDyncall centerCall = (deleDyncall)getSysSetting("domainCenterHash").ToDelegate();

            var result = (byte[])centerCall("owner_SetOwner", new object[3] { ExecutionEngine.ExecutingScriptHash, FSI.fullHash, buyer });
            if (result.AsBigInteger() != 1) //如果域名所有权转移失败，返回失败
                return false;
            /*
             * 域名转让成功 开始算钱
             */
            
            var balanceOfSeller = balanceMap.Get(seller.Concat(assetHash)).AsBigInteger();

            //给买方减少钱
            if (balanceOfBuyer == price)//如果交易金额是全部余额，则清除账户
                balanceMap.Delete(buyer.Concat(assetHash));
            else
                balanceMap.Put(buyer.Concat(assetHash), balanceOfBuyer - price);

            //计算手续费
            assetSetting assetSetting = getAssetSetting(assetHash);
            BigInteger handlingFee = price * assetSetting.handlingFeeRate / 10000;//handlingFeeRate是事先乘10000存储的

            //给卖方增加钱(扣除手续费)
            balanceMap.Put(seller, balanceOfSeller + price - handlingFee);

            //发送手续费到分红池
            if (handlingFee > 0)
            {
                if (!NEP5transfer(ExecutionEngine.ExecutingScriptHash, getSysSetting("dividingPoolAddr"), assetHash, handlingFee))
                    throw new Exception("NEP5transfer is wrong");
            }

            //完成 把售卖信息删除
            StorageMap fixedSellingInfoMap = Storage.CurrentContext.CreateMap("fixedSellingInfoMap");
            fixedSellingInfoMap.Delete(fullHash);

            //通知
            onBuy(buyer, FSI);

            return true;
        }

        public static bool SetMoneyIn(byte[] txid, byte[] assetHash)
        {
            StorageMap txidVerifiedMap = Storage.CurrentContext.CreateMap("txidVerifiedMap");
            StorageMap balanceMap = Storage.CurrentContext.CreateMap("balanceMap");

            var tx = getTxIn(txid, assetHash);
            
            //没有这个NEP5转账交易
            if (tx.from.Length == 0)
                return false;
            //NEP5转账交易目标必须是本合约
            if (tx.to.AsBigInteger() == ExecutionEngine.ExecutingScriptHash.AsBigInteger())
            {
                var isVerified = txidVerifiedMap.Get(txid).AsBigInteger();
                //这笔txid已经被用掉了,不能重复使用
                if (isVerified == 1)
                    return false;
                //转账金额大于0才能操作
                if (tx.value <=0)
                    return false;
                //存錢
                var balance = balanceMap.Get(tx.from.Concat(assetHash)).AsBigInteger();
                balance += tx.value;
                balanceMap.Put(tx.from.Concat(assetHash), balance);

                onSetMoneyIn(tx.from, assetHash, tx.value, txid);
                //記錄這個txid處理過了,只處理一次
                txidVerifiedMap.Put(txid, 1);
                return true;
            }
            return false;
        }

        public static bool GetMoneyBack(byte[] who, byte[] assetHash, BigInteger amount)
        {
            StorageMap balanceMap = Storage.CurrentContext.CreateMap("balanceMap");
            assetSetting assetSetting = getAssetSetting(assetHash);

            if (!Runtime.CheckWitness(who) && !checkSpuerAdmin())
                return false;
            //多判断总比少判断好
            if (amount <= 0)
                return false;
            if (who.Length != 20)
                return false;

            var balance = balanceMap.Get(who.Concat(assetHash)).AsBigInteger();
            if (balance < amount)
                return false;

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

            return false;
        }

        //退回全部余额
        public static bool GetMoneyBackAll(byte[] who, byte[] assetHash)
        {
            StorageMap balanceMap = Storage.CurrentContext.CreateMap("balanceMap");
            var amount = balanceMap.Get(who.Concat(assetHash)).AsBigInteger();

            return GetMoneyBack(who, assetHash, amount);
        }

        public static object Main(string method, object[] args)
        {
            string magic = "20181031";

            //UTXO转账转入转出都不允许
            if (Runtime.Trigger == TriggerType.Verification || Runtime.Trigger == TriggerType.VerificationR)
            {
                return false;
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                var callScript = ExecutionEngine.CallingScriptHash;
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
                if (method == "getFixedSellingInfo")
                {
                    return getfixedSellingInfoByFullhash((byte[])args[0]);
                }
                //充值
                if (method == "setMoneyIn")
                {
                    return SetMoneyIn((byte[])args[0], (byte[])args[1]);
                }
                //提现
                if (method == "getMoneyBack")
                {
                    return GetMoneyBack((byte[])args[0], (byte[]) args[1], (BigInteger)args[2]);
                }
                //提现
                if (method == "getMoneyBackAll")
                {
                    return GetMoneyBackAll((byte[])args[0], (byte[])args[1]);
                }
                //上架
                if (method == "launch")
                {
                    return Launch((string[])args[0], (byte[])args[1], (BigInteger)args[1]);
                }
                //下架
                if (method == "discontinue")
                {
                    return Discontinue((byte[])args[0]);
                }
                //购买
                if (method == "buy")
                {
                    return Buy((byte[])args[0], (byte[])args[1]);
                }
            }
            return false;
        }
    }

}
