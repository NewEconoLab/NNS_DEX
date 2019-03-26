using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DEX_SDK
{
    public class NelApiHelper
    {
        string nelApiUrl;
        public NelApiHelper(string url)
        {
            nelApiUrl = url;
        }

        HttpHelper hh = new HttpHelper();

        public Int64 GetBlockCount()
        {
            string input = @"{
	            'jsonrpc': '2.0',
                'method': 'getblockcount',
	            'params': [],
	            'id': '1'
            }";
            string result = hh.Post(nelApiUrl, input, System.Text.Encoding.UTF8, 1);
            JObject resultJ = (JObject)JObject.Parse(result)["result"][0];

            var blockCount = (Int64)resultJ["blockcount"];

            return blockCount;
        }

        public string GetBlockHash(Int64 blockIndex)
        {
            string input = @"{
	            'jsonrpc': '2.0',
                'method': 'getblock',
	            'params': [#],
	            'id': '1'
            }";
            input = input.Replace("#", blockIndex.ToString());

            string result = hh.Post(nelApiUrl, input, System.Text.Encoding.UTF8, 1);
            JObject resultJ = (JObject)JObject.Parse(result)["result"][0];

            var blockHash = (string)resultJ["hash"];

            return blockHash;
        }

        public Int64 GetBlockTimeStamp(Int64 blockIndex)
        {
            string input = @"{
	            'jsonrpc': '2.0',
                'method': 'getblock',
	            'params': [#],
	            'id': '1'
            }";
            input = input.Replace("#", blockIndex.ToString());
            string result = hh.Post(nelApiUrl, input, System.Text.Encoding.UTF8, 1);
            JObject resultJ = (JObject)JObject.Parse(result)["result"][0];

            var r = (Int64)resultJ["time"];

            return r;
        }

        public JObject GetBalance(string addr)
        {
            string input = @"{
	            'jsonrpc': '2.0',
                'method': 'getbalance',
	            'params': ['#'],
	            'id': '1'
            }";
            input = input.Replace("#", addr);
            string result = hh.Post(nelApiUrl, input, System.Text.Encoding.UTF8, 1);
            JObject resultJ = JObject.Parse(result);

            return resultJ;
        }

        public string GetNep5BalanceOfAddress(string assetid,string addr)
        {
            string input = @"{
	            'jsonrpc': '2.0',
                'method': 'getnep5balanceofaddress',
	            'params': ['#','$'],
	            'id': '1'
            }";
            input = input.Replace("#", assetid);
            input = input.Replace("$", addr);
            string result = hh.Post(nelApiUrl, input, System.Text.Encoding.UTF8, 1);
            JObject resultJ = (JObject)JObject.Parse(result)["result"][0];
            var balance = (string)resultJ["nep5balance"];

            return balance;
        }

        public JObject GetUTXO(string addr)
        {
            string input = @"{
	            'jsonrpc': '2.0',
                'method': 'getutxo',
	            'params': ['#'],
	            'id': '1'
            }";
            input = input.Replace("#", addr);
            string result = hh.Post(nelApiUrl, input, System.Text.Encoding.UTF8, 1);
            JObject resultJ = JObject.Parse(result);

            return resultJ;
        }

        public JObject SendRawTransaction(string txHex)
        {
            string input = @"{
	            'jsonrpc': '2.0',
                'method': 'sendrawtransaction',
	            'params': ['#'],
	            'id': '1'
            }";
            input = input.Replace("#", txHex);
            string result = hh.Post(nelApiUrl, input, System.Text.Encoding.UTF8, 1);
            JObject resultJ = JObject.Parse(result);

            return resultJ;
        }

        public JObject GetNotify(string txid)
        {
            string input = @"{
	            'jsonrpc': '2.0',
                'method': 'getnotify',
	            'params': ['#'],
	            'id': '1'
            }";
            input = input.Replace("#", txid);
            string result = hh.Post(nelApiUrl, input, System.Text.Encoding.UTF8, 1);
            JObject resultJ = JObject.Parse(result);

            return resultJ;
        }

        public JObject InvokeScript(string script)
        {
            string input = @"{
	            'jsonrpc': '2.0',
                'method': 'invokescript',
	            'params': ['#'],
	            'id': '1'
            }";
            input = input.Replace("#", script);
            string result = hh.Post(nelApiUrl, input, System.Text.Encoding.UTF8, 1);
            JObject resultJ = JObject.Parse(result);

            return resultJ;
        }
    }
}
