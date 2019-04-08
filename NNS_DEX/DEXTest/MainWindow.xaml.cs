using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using DEX_SDK;
using Newtonsoft.Json.Linq;
using ThinNeo;

namespace DEXTest
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private Dex_ScriptPackage dex_ScriptPackage;
        private Wallet wallet;
        public MainWindow()
        {
            InitializeComponent();
            wallet = new Wallet("", this.tb_api.Text);
            dex_ScriptPackage = new Dex_ScriptPackage(this.tb_ContractHash.Text);
            //获取高度
            Task task = new Task(() => {
                UpdateInfo();
            });
            task.Start();

        }

        long height = 0;
        private void UpdateInfo()
        {
            while (true)
            {
                var _height = wallet.nelApiHelper.GetBlockCount() - 1;
                if (_height != height)
                {
                    Dispatcher.Invoke((Action)delegate () {
                        this.lb_blockHeight.Content = _height.ToString();
                        height = _height;
                    });

                }
                System.Threading.Thread.Sleep(5000);
            }
        }

        private void SignUp(object sender, RoutedEventArgs e)
        {
            try
            {
                // 使用一个IntPtr类型值来存储加密字符串的起始点  
                IntPtr p = System.Runtime.InteropServices.Marshal.SecureStringToBSTR(this.wif.SecurePassword);

                // 使用.NET内部算法把IntPtr指向处的字符集合转换成字符串  
                string wif = System.Runtime.InteropServices.Marshal.PtrToStringBSTR(p);

                wallet = new Wallet(wif, this.tb_api.Text);

                this.address.Content = wallet.Address;
            }
            catch
            {
                MessageBox.Show("请输入正确的wif");
            }
        }


        private void Invoke(byte[] script)
        {
            JObject result = wallet.nelApiHelper.InvokeScript(Helper.Bytes2HexString(script));
            details.Text = result.ToString();
        }

        private string MakeTran(byte[] script)
        {
            //如果没有wif不能发送交易
            if (wallet.PriKey == null)
            {
                MessageBox.Show("请先登入wif");
                return "";
            }

            if ((bool)multSign.IsChecked)//是多签
            {
                return "";
            }
            else
            {
                Transaction tran = wallet.MakeTran(script, int.Parse(tb_netfee.Text));
                Console.WriteLine(Helper.Bytes2HexString(tran.GetRawData()));
                JObject result = wallet.nelApiHelper.SendRawTransaction(Helper.Bytes2HexString(tran.GetRawData()));
                //根据结果做出不同的操作
                return AnalysisResult(result);
            }
        }

        private string AnalysisResult(JObject result)
        {
            details.Text = result.ToString();
            if ((bool)result["result"][0]["sendrawtransactionresult"])
            {//交易发送成功
                string txid = result["result"][0]["txid"].ToString();
                //把txid加入交易详情
                Label lb = new Label();
                lb.Content = txid;
                lb.MouseDown += GetNotify;
                this.txs.Items.Add(lb);
                return txid;
            }
            else
            {//交易发送失败
                return "";
            }
        }

        private void GetNotify(object sender, RoutedEventArgs e)
        {
            Label lb = sender as Label;
            if (lb == null)
                return;
            var txid = lb.Content.ToString();
            this.details.Text = wallet.nelApiHelper.GetNotify(txid).ToString();
        }

        private void UpdateScriptHash(object sender, RoutedEventArgs e)
        {
            dex_ScriptPackage = new Dex_ScriptPackage(this.tb_ContractHash.Text);
        }

        private void SetCenterHash(object sender, RoutedEventArgs e)
        {
            string key = this.tb_centerhash_key.Text;
            Hash160 value = new Hash160(this.tb_centerhash_value.Text);
            byte[] script = dex_ScriptPackage.GetScript_SetSysSetting_DomainCenterHash(key,value);
            MakeTran(script);
        }

        private void SetMinMortgagePayments(object sender, RoutedEventArgs e)
        {
            string key = this.tb_minMortgagePayments_key.Text;
            BigInteger value = BigInteger.Parse(this.tb_minMortgagePayments_value.Text);
            byte[] script = dex_ScriptPackage.GetScript_SetSysSetting_MortgagePayments(key, value);
            MakeTran(script);
        }

        private void SetSuperAdmin(object sender, RoutedEventArgs e)
        {
            string key = this.tb_superAdmin_key.Text;
            string value = this.tb_superAdmin_value.Text;
            byte[] script = dex_ScriptPackage.GetScript_SetSysSetting_SuperAdmin(key, value);
            MakeTran(script);
        }

        private void SetPoolAddress(object sender, RoutedEventArgs e)
        {
            string key = this.tb_poolAddr_key.Text;
            string value = this.tb_poolAddr_value.Text;
            byte[] script = dex_ScriptPackage.GetScript_SetSysSetting_DividingPoolAddr(key,value);
            MakeTran(script);
        }

        private void SetInterval(object sender, RoutedEventArgs e)
        {
            string key = this.tb_interval_key.Text;
            BigInteger value = BigInteger.Parse(this.tb_interval_value.Text);
            byte[] script = dex_ScriptPackage.GetScript_SetSysSetting_Interval(key,value);
            MakeTran(script);
        }

        private void SetMortgageAssetHash(object sender, RoutedEventArgs e)
        {
            string key = this.tb_mortgageAssetHash_key.Text;
            Hash160 value = new Hash160(this.tb_mortgageAssetHash_value.Text);
            byte[] script = dex_ScriptPackage.GetScript_SetSysSetting_MortgageAssetHash(key, value);
            MakeTran(script);
        }

        private void QuerySystemSetting(object sender, RoutedEventArgs e)
        {
            var key = this.tb_query_key.Text;
            byte[] script = dex_ScriptPackage.GetScript_GetSysSetting(key);
            Invoke(script);
        }

        private void SetAssetInfo(object sender, RoutedEventArgs e)
        {
            Hash160 assetid = new Hash160(this.tb_assetsetting_hash.Text);
            BigInteger enable = this.tb_assetsetting_enable.IsChecked == true?1:0;
            BigInteger min = BigInteger.Parse(this.tb_assetsetting_valuemin.Text);
            BigInteger unit = BigInteger.Parse(this.tb_assetsetting_valueUnit.Text);
            BigInteger feeratio = BigInteger.Parse(this.tb_assetsetting_feeRate.Text);
            string transferMethod = this.tb_assetsetting_transferMethod.Text;
            byte[] script = dex_ScriptPackage.GetScript_SetAssetSetting(assetid,enable,min,unit,feeratio,transferMethod);
            MakeTran(script);
        }

        private void QueryAssetInfo(object sender, RoutedEventArgs e)
        {
            Hash160 assetid  = new Hash160(this.tb_assetsetting_hash.Text);
            byte[] script = dex_ScriptPackage.GetScript_GetAssetSetting(assetid);
            Invoke(script);
        }

        string txid_cgas;
        private void Transfer_CGAS(object sender, RoutedEventArgs e)
        {
            string scAddress = ThinNeo.Helper_NEO.GetAddress_FromScriptHash(new Hash160(this.tb_ContractHash.Text));
            byte[] script = dex_ScriptPackage.GetScript_TransferCGAS(wallet.Address, scAddress, BigInteger.Parse(this.tb_amount_cgas.Text));
            txid_cgas = MakeTran(script);
        }

        private void SetMoneyIn_CGAS(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(txid_cgas))
            {
                byte[] script = dex_ScriptPackage.GetScript_SetMoneyIn(new Hash256(txid_cgas), new Hash160("0x74f2dc36a68fdc4682034178eb2220729231db76"));
                MakeTran(script);
            }
        }

        private void GetMoneyBack_CGAS(object sender, RoutedEventArgs e)
        {
            byte[] script = dex_ScriptPackage.GetScript_GetMoneyBack(wallet.Address, new Hash160("0x74f2dc36a68fdc4682034178eb2220729231db76"), BigInteger.Parse(this.tb_amount_cgas.Text) * 100000000);
            MakeTran(script);
        }

        string txid_nnc;
        private void Transfer_NNC(object sender, RoutedEventArgs e)
        {
            string scAddress = ThinNeo.Helper_NEO.GetAddress_FromScriptHash(new Hash160(this.tb_ContractHash.Text));
            byte[] script = dex_ScriptPackage.GetScript_TransferNNC(wallet.Address, scAddress, BigInteger.Parse(this.tb_amount_nnc.Text));
            txid_nnc = MakeTran(script);
        }

        private void SetMoneyIn_NNC(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(txid_nnc))
            {
                byte[] script = dex_ScriptPackage.GetScript_SetMoneyIn(new Hash256(txid_nnc), new Hash160("0xfc732edee1efdf968c23c20a9628eaa5a6ccb934"));
                MakeTran(script);
            }
        }

        private void GetMoneyBack_NNC(object sender, RoutedEventArgs e)
        {
            byte[] script = dex_ScriptPackage.GetScript_GetMoneyBack(wallet.Address, new Hash160("0xfc732edee1efdf968c23c20a9628eaa5a6ccb934"),  BigInteger.Parse(this.tb_amount_nnc.Text) * 100);
            MakeTran(script);
        }

        private void BalanceOfCGAS(object sender, RoutedEventArgs e)
        {
            byte[] script = dex_ScriptPackage.GetScript_GetBalanceOf(wallet.Address, new Hash160("0x74f2dc36a68fdc4682034178eb2220729231db76"));
            Invoke(script);
        }

        private void BalanceOfNNC(object sender, RoutedEventArgs e)
        {
            byte[] script = dex_ScriptPackage.GetScript_GetBalanceOf(wallet.Address, new Hash160("0xfc732edee1efdf968c23c20a9628eaa5a6ccb934"));
            Invoke(script);
        }

        private readonly System.Security.Cryptography.SHA256 sha256 = System.Security.Cryptography.SHA256.Create();
        private byte[] NameHashArray(string[] domainarray)
        {
            byte[] hash = NameHash(domainarray[0]);
            for (var i = 1; i < domainarray.Length; i++)
            {
                hash = NameHashSub(hash, domainarray[i]);
            }
            return hash;
        }
        public Hash256 NameHash(string domain)
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes(domain);
            return new Hash256(sha256.ComputeHash(data));
        }

        public Hash256 NameHashSub(byte[] roothash, string subdomain)
        {
            var bs = System.Text.Encoding.UTF8.GetBytes(subdomain);
            if (bs.Length == 0)
                return roothash;

            var domain = sha256.ComputeHash(bs).Concat(roothash).ToArray();
            return new Hash256(sha256.ComputeHash(domain));
        }

        public Hash256 GetFullHash(string[] domains)
        {
            Hash256 rootHash = NameHash(domains[domains.Length - 1]);
            Hash256 fullHash = rootHash;
            for (var i = domains.Length - 2; i == 0; i--)
            {
                fullHash = NameHashSub(rootHash, domains[i]);
                if (i != 0)
                    rootHash = fullHash;
            }
            return fullHash;
        }

        private void GetOfferToBuyerInfo(object sender, RoutedEventArgs e)
        {
            string address = wallet.Address;
            Hash160 assetid = new Hash160(this.tb_offerToBuyer_assetid.Text);
            string[] domains = this.tb_offerToBuyer_domain.Text.Split('.');
            Hash256 fullHash = GetFullHash(domains);

            byte[] script = dex_ScriptPackage.GetScript_GetOfferToBuyInfo(address, fullHash, assetid);
            Invoke(script);
        }

        private void OfferToBuyer(object sender, RoutedEventArgs e)
        {
            string addres = wallet.Address;
            string[] domains = this.tb_offerToBuyer_domain.Text.Split('.');
            Hash160 assetid = new Hash160(this.tb_offerToBuyer_assetid.Text);
            BigInteger price = BigInteger.Parse(this.tb_offerToBuyer_price.Text);
            BigInteger mortgage = BigInteger.Parse(this.tb_offerToBuyer_mortgage.Text);
            byte[] script = dex_ScriptPackage.GetScript_OfferToBuy(addres,domains,assetid,price,mortgage);
            MakeTran(script);
        }

        private void DiscontinueOfferToBuy(object sender, RoutedEventArgs e)
        {
            string address = wallet.Address;
            Hash160 assetid = new Hash160(this.tb_offerToBuyer_assetid.Text);
            string[] domains = this.tb_offerToBuyer_domain.Text.Split('.');
            Hash256 fullHash = GetFullHash(domains);

            byte[] script = dex_ScriptPackage.GetScript_DiscontinueOfferToBuy(address,fullHash,assetid);

            MakeTran(script);
        }

        private void GetAuctionInfo(object sender, RoutedEventArgs e)
        {
            string[] domains = this.tb_bet_domain.Text.Split('.');
            Hash256 fullHash = GetFullHash(domains);
            byte[] script = dex_ScriptPackage.GetScript_GetAuctionInfo(fullHash);
            Invoke(script);
        }

        private void GetAuctionPrice(object sender, RoutedEventArgs e)
        {
            string[] domains = this.tb_bet_domain.Text.Split('.');
            Hash256 fullHash = GetFullHash(domains);
            byte[] script = dex_ScriptPackage.GetScript_GetAuctionPrice(fullHash);
            Invoke(script);
        }

        private void Bet(object sender, RoutedEventArgs e)
        {
            string address = wallet.Address;
            string[] domains = this.tb_bet_domain.Text.Split('.');
            Hash256 fullHash = GetFullHash(domains);
            Hash160 assetid = new Hash160(this.tb_bet_assetid.Text);
            BigInteger price = BigInteger.Parse(this.tb_bet_price.Text);
            byte[] script = dex_ScriptPackage.GetScript_Bet(address,fullHash,assetid,price);
            MakeTran(script);
        }

        private void GetOfferToBuyerPrice(object sender, RoutedEventArgs e)
        {
            string address = wallet.Address;
            Hash160 assetid = new Hash160(this.tb_sell_assetid.Text);
            string[] domains = this.tb_sell_domain.Text.Split('.');
            Hash256 fullHash = GetFullHash(domains);

            byte[] script = dex_ScriptPackage.GetScript_GetOfferToBuyPrice(address, fullHash, assetid);
            Invoke(script);
        }

        private void Sell(object sender, RoutedEventArgs e)
        {
            string address = this.tb_sell_address.Text;
            string[] domains = this.tb_sell_domain.Text.Split('.');
            Hash256 fullhash = GetFullHash(domains);
            Hash160 assetid = new Hash160(this.tb_sell_assetid.Text);
            byte[] script = dex_ScriptPackage.GetScript_Sell(address,fullhash,assetid);
            MakeTran(script);
        }

        private void Auction(object sender, RoutedEventArgs e)
        {
            string[] domains = this.tb_auction_domain.Text.Split('.');
            Hash160 assetid = new Hash160(this.tb_auction_assetid.Text);
            BigInteger startPrice = BigInteger.Parse(this.tb_auction_startPrice.Text);
            BigInteger endPrice = BigInteger.Parse(this.tb_auction_endPrice.Text);
            BigInteger salePrice = BigInteger.Parse(this.tb_auction_salePrice.Text);
            BigInteger mortgage = BigInteger.Parse(this.tb_auction_mortgage.Text);        
            byte[] script = dex_ScriptPackage.GetScript_Auction(domains,assetid,startPrice,endPrice,salePrice,mortgage);
            MakeTran(script);
        }

        private void DiscontinueAuction(object sender, RoutedEventArgs e)
        {
            string[] domains = this.tb_auction_domain.Text.Split('.');
            Hash256 fullhash = GetFullHash(domains);
            byte[] script = dex_ScriptPackage.GetScript_DiscontinueAuction(fullhash);
            MakeTran(script);
        }

        private void GetDividendBalance(object sender, RoutedEventArgs e)
        {
            Hash160 assetid = new Hash160(this.tb_dividendBalance_assetid.Text);
            byte[] script = dex_ScriptPackage.GetScript_GetDividendBalance(assetid);
            Invoke(script);
        }

        private void GetSellMoney(object sender, RoutedEventArgs e)
        {
            Hash256 txid = new Hash256(this.tb_sell_txid.Text);
            byte[] script = dex_ScriptPackage.GetScript_GetSellMoney(txid);
            MakeTran(script);
        }

        private void GetBetMoney(object sender, RoutedEventArgs e)
        {
            Hash256 txid = new Hash256(this.tb_bet_txid.Text);
            byte[] script = dex_ScriptPackage.GetScript_GetAuctionMoney(txid);
            MakeTran(script);
        }

    }
}
