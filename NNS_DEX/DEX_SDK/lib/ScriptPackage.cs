using Newtonsoft.Json.Linq;
using ThinNeo;
using System.Security.Cryptography;
using System.Numerics;

namespace DEX_SDK
{
    public abstract class ScriptPackage
    {
        protected Hash160 contractHash;

        protected ScriptPackage(string _contractHash)
        {
            contractHash =new Hash160(_contractHash);
        }

        protected byte[] GetScript(ThinNeo.Hash160 contractHash, JArray JA)
        {
            ThinNeo.ScriptBuilder tmpSb = new ThinNeo.ScriptBuilder();
            byte[] randombytes = new byte[32];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randombytes);
            }
            BigInteger randomNum = new BigInteger(randombytes);
            tmpSb.EmitPushNumber(randomNum);
            tmpSb.Emit(ThinNeo.VM.OpCode.DROP);
            for (int i = JA.Count - 1; i >= 0; i--)
            {
                tmpSb.EmitParamJson(JA[i]);
            }
            tmpSb.EmitAppCall(contractHash);
            return tmpSb.ToArray();
        }

        protected string GetHexStrScript(ThinNeo.Hash160 contractHash, JArray JA)
        {
            return ThinNeo.Helper.Bytes2HexString(GetScript(contractHash, JA));
        }

    }
}
