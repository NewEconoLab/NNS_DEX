using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using ThinNeo;

namespace DEX_SDK
{
    public class Utxo
    {
        //txid[n] 是utxo的属性
        public ThinNeo.Hash256 txid;
        public int n;

        //asset资产、addr 属于谁，value数额，这都是查出来的
        public string addr;
        public string asset;
        public decimal value;
        public Utxo(string _addr, ThinNeo.Hash256 _txid, string _asset, decimal _value, int _n)
        {
            this.addr = _addr;
            this.txid = _txid;
            this.asset = _asset;
            this.value = _value;
            this.n = _n;
        }
    }

    public class Wallet
    {
        private const string id_GAS = "0x602c79718b16e442de58778e148d0b1084e3b2dffd5de6b7b16cee7969282de7";

        public NelApiHelper nelApiHelper;

        public string Address { get; }
        public byte[] PubKey {get; }
        public byte[] PriKey {get; }
        public byte[] SciptHash {get; }

        public Wallet(string wif,string nelApi)
        {
            nelApiHelper = new NelApiHelper(nelApi);
            if (string.IsNullOrEmpty(wif))
                return;
            PriKey = ThinNeo.Helper_NEO.GetPrivateKeyFromWIF(wif);
            PubKey = ThinNeo.Helper_NEO.GetPublicKey_FromPrivateKey(PriKey);
            Address = ThinNeo.Helper_NEO.GetAddress_FromPublicKey(PubKey);
            SciptHash = ThinNeo.Helper_NEO.GetScriptHash_FromAddress(Address);
        }

        public List<Utxo> GetUtxo(string addr,string assetid)
        {
            JObject response = nelApiHelper.GetUTXO(addr);
            JArray resJA = (JArray)response["result"];
            Dictionary<string, List<Utxo>> _dir = new Dictionary<string, List<Utxo>>();
            foreach (JObject j in resJA)
            {
                Utxo utxo = new Utxo(j["addr"].ToString(), new ThinNeo.Hash256(j["txid"].ToString()), j["asset"].ToString(), decimal.Parse(j["value"].ToString()), int.Parse(j["n"].ToString()));
                if (_dir.ContainsKey(j["asset"].ToString()))
                {
                    _dir[j["asset"].ToString()].Add(utxo);
                }
                else
                {
                    List<Utxo> l = new List<Utxo>();
                    l.Add(utxo);
                    _dir[j["asset"].ToString()] = l;
                }

            }
            if (_dir.ContainsKey(assetid))
                return _dir[assetid];
            else
                return new List<Utxo>() { };
        }

        public Transaction MakeTran(byte[] script,decimal netfee,bool sign = true)
        {
            Transaction tran = new ThinNeo.Transaction();
            tran.type = TransactionType.InvocationTransaction;
            tran.version = 0;
            tran.extdata = new InvokeTransData();
            (tran.extdata as InvokeTransData).script = script;
            (tran.extdata as InvokeTransData).gas = 0;
            //没有系统费用就不用utxo模式的交易发放了
            if (netfee <= 0)
            {
                MakeTran_NoNetfee(tran);
            }
            else
            {
                MakeTran_HasNetfee(tran, netfee);
            }

            if (sign)
                SignTran(tran);

            return tran;
        }

        private Transaction MakeTran_NoNetfee(Transaction tran)
        {
            tran.inputs = new ThinNeo.TransactionInput[0];
            tran.outputs = new ThinNeo.TransactionOutput[0];
            tran.attributes = new ThinNeo.Attribute[1];
            tran.attributes[0] = new ThinNeo.Attribute();
            tran.attributes[0].usage = TransactionAttributeUsage.Script;
            tran.attributes[0].data = SciptHash;
            return tran;
        }

        private Transaction SignTran(Transaction tran)
        {
            var signdata = ThinNeo.Helper_NEO.Sign(tran.GetMessage(),PriKey);
            tran.AddWitness(signdata,PubKey,Address);
            return tran;
        }

        private Transaction MakeTran_HasNetfee(Transaction tran,decimal netfee,decimal sendcount = 0)
        {
            string assetid = id_GAS;
            List<Utxo> utxos=GetUtxo(Address, assetid);
            tran.attributes = new ThinNeo.Attribute[0];
            utxos.Sort((a, b) =>
            {
                if (a.value > b.value)
                    return 1;
                else if (a.value < b.value)
                    return -1;
                else
                    return 0;
            });
            decimal count = decimal.Zero;
            List<ThinNeo.TransactionInput> list_inputs = new List<ThinNeo.TransactionInput>();
            for (var i = 0; i < utxos.Count; i++)
            {
                ThinNeo.TransactionInput input = new ThinNeo.TransactionInput();
                input.hash = utxos[i].txid;
                input.index = (ushort)utxos[i].n;
                list_inputs.Add(input);
                count += utxos[i].value;
                if (count >= sendcount+netfee)
                {
                    break;
                }
            }

            tran.inputs = list_inputs.ToArray();
            if (count >= sendcount)//输入大于等于输出
            {
                List<ThinNeo.TransactionOutput> list_outputs = new List<ThinNeo.TransactionOutput>();

                //找零
                decimal change = count - sendcount - netfee;

                if (change > decimal.Zero)
                {
                    ThinNeo.TransactionOutput outputchange = new ThinNeo.TransactionOutput();
                    outputchange.toAddress = SciptHash;
                    outputchange.value = change;
                    outputchange.assetId = ThinNeo.Helper.HexString2Bytes(id_GAS);
                    list_outputs.Add(outputchange);
                }
                tran.outputs = list_outputs.ToArray();
            }
            else
            {
                throw new Exception("no enough money.");
            }
            return tran;
        }

    }
}
