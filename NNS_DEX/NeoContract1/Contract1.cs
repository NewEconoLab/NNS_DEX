using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System.ComponentModel;

namespace NeoContract1
{
    public class Contract1 : SmartContract
    {
        [Appcall("4b2e5123374633e71b054caa68ec5593fe59f0da")]
        static extern object call(string method, object[] arr);

        public static bool Main(byte[] signdata)
        {
            var hash = (ExecutionEngine.ScriptContainer as Transaction).Hash;

            byte[] pubKey = (byte[])call("getkey",new object[1] { hash });
            return VerifySignature(signdata, pubKey);
        }
    }
}
