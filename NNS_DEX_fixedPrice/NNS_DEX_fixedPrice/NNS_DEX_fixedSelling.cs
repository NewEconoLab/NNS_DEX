using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using Helper = Neo.SmartContract.Framework.Helper;
using System;
using System.Numerics;
using System.ComponentModel;

namespace NNS_DEX
{
    //一口价卖出合约
    public class NNS_DEX_fixedSelling : SmartContract
    {
        //初始管理員(测试专用)
        static readonly byte[] superAdmin = Helper.ToScriptHash("AeaWf2v7MHGpzxH4TtBAu5kJRp5mRq2DQG");

        //域名中心跳板合约地址
        [Appcall("348387116c4a75e420663277d9c02049907128c7")]
        static extern object centerCall(string method, object[] arr);

        // NNC合约地址
        // NNC转账
        [Appcall("fc732edee1efdf968c23c20a9628eaa5a6ccb934")]
        static extern object nncCall(string method, object[] arr);

        //通知 余额增加（充值）
        public delegate void deleSetMoneyIn(byte[] who, BigInteger value, byte[] txid);
        [DisplayName("setMoneyIn")]
        public static event deleSetMoneyIn onSetMoneyIn;

        //通知 余额减少（提现）
        public delegate void deleGetMoneyBack(byte[] who, BigInteger value);
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
        //balanceMap key=addr 地址合约内账户余额
        //fixedSellingInfoMap key=namehash  出售信息
        //txidVerifiedMap key=txid setmoneyin的txid是否被用于增加钱过


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
            public BigInteger price;
        }

        public static BigInteger BalanceOf(byte[] who)
        {
            //查看用户在本合约的账户余额
            StorageMap balanceMap = Storage.CurrentContext.CreateMap("balanceMap");
            return balanceMap.Get(who).AsBigInteger();
        }

        public static OwnerInfo GetOwnerInfo(byte[] fullHash)
        {
            var ownerInfo = centerCall("getOwnerInfo", new object[1] { fullHash }) as OwnerInfo;
            return ownerInfo;
        }

        static TransferInfo getTxIn(byte[] txid)
        {
            StorageMap txidVerifiedMap = Storage.CurrentContext.CreateMap("txidVerifiedMap");
            var v = txidVerifiedMap.Get(txid).AsBigInteger();
            if (v == 0)//如果這個交易已經處理過,就當get不到
            {
                object[] _p = new object[1];
                _p[0] = txid;
                var info = nncCall("getTxInfo", _p);
                if (((object[])info).Length == 3)
                    return info as TransferInfo;
            }
            var tInfo = new TransferInfo();
            tInfo.from = new byte[0];
            return tInfo;
        }

        private static byte[] getFullNamehashForArray(string[] domainArray)
        {
            return centerCall("nameHashArray", new object[] { domainArray }) as byte[];
        }

        private static string getFullDomainForArray(string[] domainArray)
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

        //存储一口价销售信息
        private static void saveFixedSellingInfo(string[] domainArray, byte[] seller, BigInteger price)
        {   
            byte[] namehash = getFullNamehashForArray(domainArray);

            fixedSellingInfo fixedSellingInfo = new fixedSellingInfo();
            fixedSellingInfo.fullHash = namehash;
            fixedSellingInfo.fullDomain = getFullDomainForArray(domainArray);
            fixedSellingInfo.seller = seller;
            fixedSellingInfo.price = price;

            StorageMap fixedSellingInfoMap = Storage.CurrentContext.CreateMap("fixedSellingInfoMap");
            fixedSellingInfoMap.Put(namehash, Helper.Serialize(fixedSellingInfo));

            //通知
            onLaunched(fixedSellingInfo);
        }

        //用namehash获取一口价销售信息
        public static fixedSellingInfo getfixedSellingInfoByFullhash(byte[] fullHash)
        {
            StorageMap fixedSellingInfoMap = Storage.CurrentContext.CreateMap("fixedSellingInfoMap");
            var bytes = fixedSellingInfoMap.Get(fullHash);
            if (bytes.Length > 0)
                return Helper.Deserialize(bytes) as fixedSellingInfo;
            else
                return new fixedSellingInfo();
        }

        ////用NNS明文获取一口价销售信息
        //public static fixedSellingInfo getfixedSellingInfoByDomainarray(string[] domainArray)
        //{
        //    return getfixedSellingInfoByFullhash(getFullNamehashForArray(domainArray));
        //}

        //上架
        public static object launch(string[] domainArray, BigInteger price)
        {
            //售价必须大于0
            if (price <= 0) return false;

            //合约限制最小价格为0.1 并且小数点后面不能超过一位
            if (price < 10000000 || price % 10000000 > 0)
                return false;

            byte[] fullhash = getFullNamehashForArray(domainArray);

            //先获取这个域名的拥有者
            OwnerInfo ownerInfo = GetOwnerInfo(fullhash);
            //域名没有初始化不能上架
            if (ownerInfo.owner.Length == 0)
                return false;
            var seller = ownerInfo.owner;
            //验证所有者签名
            if (!Runtime.CheckWitness(seller))
                return false;
            //域名已经到期了不能上架
            if (!verifyExpires(ownerInfo.TTL))
                return false;

            //将域名抵押给本合约
            var result = (byte[])centerCall("owner_SetOwner", new object[3] { seller, fullhash, ExecutionEngine.ExecutingScriptHash });
            if (result.AsBigInteger() != 1)
                return false;

            //存储上架信息，并通知
            saveFixedSellingInfo(domainArray , seller, price);
        
            return true;
        }

        //下架
        public static object discontinue(byte[] fullHash)
        {
            StorageMap fixedSellingInfoMap = Storage.CurrentContext.CreateMap("fixedSellingInfoMap");
            fixedSellingInfo fsi = getfixedSellingInfoByFullhash(fullHash);

            //无出售信息不能下架
            if (fsi == new fixedSellingInfo()) return false;
            //非出售者不可以下架
            if (!Runtime.CheckWitness(fsi.seller)) return false;

            //var price = fsi.price;
            //var seller = fsi.seller;
            //if (fsi.price == 0)
            //    return false;
            
            //将域名所有权转回出售者
            var result = (byte[])centerCall("owner_SetOwner", new object[3] { ExecutionEngine.ExecutingScriptHash, fullHash, fsi.seller });
            if (result.AsBigInteger() != 1)
                return false;
            //清楚出售信息
            fixedSellingInfoMap.Delete(fullHash);
            //通知
            onDiscontinued(fsi);

            return true;
        }

        public static object Buy(byte[] who, byte[] fullHash)
        {
            //只能用本人的钱购买
            if (!Runtime.CheckWitness(who))
                return false;

            //先获取这个域名的信息
            OwnerInfo ownerInfo = GetOwnerInfo(fullHash);
            //域名没有初始化不能买
            if (ownerInfo.owner.Length == 0)
                return false;
            //域名已经到期了不能购买
            if (!verifyExpires(ownerInfo.TTL))
                return false;

            StorageMap balanceMap = Storage.CurrentContext.CreateMap("balanceMap");

            fixedSellingInfo fsi = getfixedSellingInfoByFullhash(fullHash);
            var price = fsi.price;           
            var balanceOfBuyer = balanceMap.Get(who).AsBigInteger();
            
            //钱不够不能买
            if (balanceOfBuyer < price)
                return false;

            //进行域名的转让操作
            var resut = (byte[])centerCall("owner_SetOwner", new object[3] { ExecutionEngine.ExecutingScriptHash, fsi.fullHash, who });
            if (resut.AsBigInteger() != 1) //如果域名转账操作gg 返回
                return false;
            /*
             * 域名转让成功 开始算钱
             */
            var seller = fsi.seller;
            var balanceOfSeller = balanceMap.Get(seller).AsBigInteger();

            //给卖方增加钱
            balanceMap.Put(seller, balanceOfSeller + price);
            //给买方减少钱
            if (balanceOfBuyer == price)
                balanceMap.Delete(who);
            else
                balanceMap.Put(who, balanceOfBuyer - price);

            //完成 把售卖信息删除
            StorageMap fixedSellingInfoMap = Storage.CurrentContext.CreateMap("fixedSellingInfoMap");
            fixedSellingInfoMap.Delete(fullHash);

            //通知
            onBuy(who, fsi);

            return true;
        }

        public static bool SetMoneyIn(byte[] txid)
        {
            StorageMap txidVerifiedMap = Storage.CurrentContext.CreateMap("txidVerifiedMap");
            StorageMap balanceMap = Storage.CurrentContext.CreateMap("balanceMap");

            var tx = getTxIn(txid);
            if (tx.from.Length == 0)
                return false;

            if (tx.to.AsBigInteger() == ExecutionEngine.ExecutingScriptHash.AsBigInteger())
            {
                var n = txidVerifiedMap.Get(txid).AsBigInteger();
                if (n == 1)//这笔txid已经被用掉了
                    return false;
                //存錢
                var money = balanceMap.Get(tx.from).AsBigInteger();
                money += tx.value;
                balanceMap.Put(tx.from, money);

                onSetMoneyIn(tx.from, tx.value, txid);
                //記錄這個txid處理過了,只處理一次
                txidVerifiedMap.Put(txid, 1);
                return true;
            }
            return false;
        }

        public static bool GetMoneyBack(byte[] who, BigInteger amount)
        {
            StorageMap balanceMap = Storage.CurrentContext.CreateMap("balanceMap");

            if (!Runtime.CheckWitness(who) && !Runtime.CheckWitness(superAdmin))
                return false;
            //多判断总比少判断好
            if (amount <= 0)
                return false;
            if (who.Length != 20)
                return false;

            var money = balanceMap.Get(who).AsBigInteger();
            if (money < amount)
                return false;

            //存錢
            object[] trans = new object[3];
            trans[0] = ExecutionEngine.ExecutingScriptHash;
            trans[1] = who;
            trans[2] = amount;

            bool succ = (bool)nncCall("transfer", trans);
            if (succ)
            {
                money -= amount;
                balanceMap.Put(who, money);

                onGetMoneyBack(who, amount);
                return true;
            }

            return false;
        }

        public static object Main(string method, object[] args)
        {
            //任意鉴权请求都为否，UTXO转账都不允许
            if (Runtime.Trigger == TriggerType.Verification)
            {
                return false;
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                var callScript = ExecutionEngine.CallingScriptHash;
                if (method == "balanceOf")
                {
                    return BalanceOf((byte[])args[0]);
                }
                if (method == "getFixedSellingInfo")
                {
                    return getfixedSellingInfoByFullhash((byte[])args[0]);
                }
                //上架
                if (method == "launch")
                {
                    return launch((string[])args[0], (BigInteger)args[1]);
                }
                //下架
                if (method == "discontinue")
                {
                    return discontinue((byte[])args[0]);
                }
                //购买
                if (method == "buy")
                {
                    return Buy((byte[])args[0], (byte[])args[1]);
                }
                //提现
                if (method == "getMoneyBack")
                {
                    return GetMoneyBack((byte[])args[0], (BigInteger)args[1]);
                }
                //充值
                if (method == "setMoneyIn")
                {
                    return SetMoneyIn((byte[])args[0]);
                }
            }
            return false;
        }
    }

}
