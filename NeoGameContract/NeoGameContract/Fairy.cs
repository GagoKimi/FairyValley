using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System;
using System.ComponentModel;
using System.Numerics;
using Helper = Neo.SmartContract.Framework.Helper;

namespace NeoGameContract
{
    public class Fairy : SmartContract
    {
        // the owner, super admin address
        public static readonly byte[] ContractOwner = "AdxQt4cy5diixsxBidtUDRRBQ1u4WWM1G2".ToScriptHash();

        delegate object deleDyncall(string method, object[] arr);

        [Appcall("c7816d11287c08135f4e5f907af9e39754910ba3")]
        public static extern object gasCall(string method, object[] arr);

        // notify new精灵通知
        public delegate void deleBirth(BigInteger tokenId, byte[] owner, BigInteger level, BigInteger blood, BigInteger attack, BigInteger defense, BigInteger speed, BigInteger rare, BigInteger attribute, BigInteger character, BigInteger birthTime);
        [DisplayName("birth")]
        public static event deleBirth Birthed;

        //精灵转移 通知
        public delegate void deleTransfer(byte[] from, byte[] to, BigInteger value);
        [DisplayName("transfer")]
        public static event deleTransfer Transferred;

        //notify 上架拍卖通知
        public delegate void deleAuction(byte[] owner, BigInteger tokenId, BigInteger price, int sellType, uint sellTime);
        [DisplayName("auction")]
        public static event deleAuction Auctioned;

        //notify 购买通知
        public delegate void deleAuctionBuy(byte[] buyer, BigInteger tokenId, BigInteger curBuyPrice, BigInteger fee, BigInteger nowtime);
        [DisplayName("auctionBuy")]
        public static event deleAuctionBuy AuctionBuy;

        //notify 取消拍卖通知
        public delegate void deleCancelAuction(byte[] owner, BigInteger tokenId);
        [DisplayName("cancelAuction")]
        public static event deleCancelAuction CancelAuctioned;

        //notify 购买门票
        public delegate void deleBuyTicket(byte[] owner, int amount1, int amount2, int amount3, int amount4, int amount5, int amount6, int amount7, int amount8);
        [DisplayName("buyTicket")]
        public static event deleBuyTicket BuyTicketed;

        //notify 使用门票
        public delegate void deleUseTicket(byte[] owner, int amount1, int amount2, int amount3, int amount4, int amount5, int amount6, int amount7, int amount8);
        [DisplayName("useTicket")]
        public static event deleUseTicket UseTicketed;

        private const ulong TX_MIN_FEE = 5000000;

        [Serializable]
        public class FairyInfo
        {
            public byte[] owner;//拥有者
            public BigInteger level;//等级
            public BigInteger blood;//血量
            public BigInteger attack;//攻击
            public BigInteger defense;//防御
            public BigInteger speed;//速度
            public BigInteger rare;//稀有
            public BigInteger attribute;//属性 1-雷 thunder 2-水 water 3-火 fire 4-光light 5-暗 dark
            public BigInteger character;//外形

            public BigInteger birthTime;

        }

        [Serializable]
        public class TicketInfo
        {
            public byte[] owner;//拥有者
            public BigInteger arena;//竞技场门票
            public BigInteger hunt;//狩猎场
            public BigInteger collect;//采集场

            public BigInteger ticket1;
            public BigInteger ticket2;
            public BigInteger ticket3;
            public BigInteger ticket4;
            public BigInteger ticket5;
        }

        public class TicketValue
        {
            public BigInteger arena;//竞技场门票
            public BigInteger hunt;//狩猎场
            public BigInteger collect;//采集场

            public BigInteger ticket1;
            public BigInteger ticket2;
            public BigInteger ticket3;
            public BigInteger ticket4;
            public BigInteger ticket5;
        }

        public class ElementInfo
        {
            public byte[] owner;//拥有者

            public BigInteger thunder;//雷
            public BigInteger water;//水
            public BigInteger fire;//火
            public BigInteger light;//光
            public BigInteger dark;//暗

            public BigInteger element1;
            public BigInteger element2;
            public BigInteger element3;
            public BigInteger element4;
            public BigInteger element5;
        }

        public class ElementValue
        {
            public BigInteger thunder;//雷
            public BigInteger water;//水
            public BigInteger fire;//火
            public BigInteger light;//光
            public BigInteger dark;//暗

            public BigInteger element1;
            public BigInteger element2;
            public BigInteger element3;
            public BigInteger element4;
            public BigInteger element5;
        }

        public class TransferInfo
        {
            public byte[] from;
            public byte[] to;
            public BigInteger value;
        }

        [Serializable]
        public class AuctionInfo
        {
            public byte[] owner;
            // 0-商城 1-玩家
            public int sellType;
            public uint sellTime;
            public BigInteger price;
        }

        // 拍卖成交记录
        public class AuctionRecord
        {
            public BigInteger tokenId;
            public byte[] seller;
            public byte[] buyer;
            public int sellType;
            public BigInteger sellPrice;
            public BigInteger sellTime;
        }

        public static string FairyNo() => "FairyNo";

        public static Object Main(string method, params object[] args)
        {
            if (Runtime.Trigger == TriggerType.Verification) //取钱才会涉及这里
            {
                if (ContractOwner.Length == 20)
                {
                    // if param ContractOwner is script hash
                    return Runtime.CheckWitness(ContractOwner);
                }
                else if (ContractOwner.Length == 33)
                {
                    // if param ContractOwner is public key
                    byte[] signature = method.AsByteArray();
                    return VerifySignature(signature, ContractOwner);
                }
            }
            else if (Runtime.Trigger == TriggerType.VerificationR)
            {
                return true;
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                //必须在入口函数取得callscript，调用脚本的函数，也会导致执行栈变化，再取callscript就晚了
                var callscript = ExecutionEngine.CallingScriptHash;

                if (method == "totalExchargeSgas") return TotalExchargeSgas();
                if (method == "version") return Version();
                if (method == "name") return Name();
               
                if (method == "balanceOf")
                {
                    //查询 游戏金币 对应 sgas
                    if (args.Length != 1) return 0;
                    byte[] account = (byte[])args[0];
                    return BalanceOf(account);
                }
                if (method == "balanceOfGas")
                {
                    //查询sgas合约下 sgas
                    if (args.Length != 1) return 0;
                    byte[] account = (byte[])args[0];
                    return BalanceOfGas(account);
                }
                if (method == "rechargeToken")
                {
                    //使用sgas 充值
                    if (args.Length != 2) return 0;
                    byte[] owner = (byte[])args[0];
                    byte[] txid = (byte[])args[1];
                    //var tx = ExecutionEngine.ScriptContainer as Transaction;
                    //byte[] txid = tx.Hash;
                    return RechargeToken(owner, txid);
                }
                if (method == "drawToken")
                {
                    //提币到sgas
                    if (args.Length != 2) return 0;
                    byte[] owner = (byte[])args[0];
                    BigInteger count = (BigInteger)args[1];

                    return DrawToken(owner, count);
                }
                if (method == "getTicketInfo")
                {
                    //获取 门票 0x01
                    if (args.Length != 1) return 0;
                    byte[] owner = (byte[])args[0];
                    return GetTicketInfo(owner);
                }
                if (method == "getElementInfo")
                {
                    //获取 元素 0x11
                    if (args.Length != 1) return 0;
                    byte[] owner = (byte[])args[0];
                    return GetElementInfo(owner);
                }
                if (method == "setTicketInfo")
                {
                    //增加或消耗门票
                    if (Runtime.CheckWitness(ContractOwner))
                    {
                        byte[] owner = (byte[])args[0];
                        BigInteger arena = (BigInteger)args[1];
                        BigInteger hunt = (BigInteger)args[2];
                        BigInteger collect = (BigInteger)args[3];

                        BigInteger ticket1 = (BigInteger)args[4];
                        BigInteger ticket2 = (BigInteger)args[5];
                        BigInteger ticket3 = (BigInteger)args[6];
                        BigInteger ticket4 = (BigInteger)args[7];
                        BigInteger ticket5 = (BigInteger)args[8];

                        return SetTicketInfo(owner, arena, hunt, collect, ticket1, ticket2, ticket3, ticket4, ticket5);
                    }
                }
                if (method == "setElementInfo")
                {
                    //增加或消耗门票
                    if (Runtime.CheckWitness(ContractOwner))
                    {
                        byte[] owner = (byte[])args[0];
                        BigInteger thunder = (BigInteger)args[1];
                        BigInteger water = (BigInteger)args[2];
                        BigInteger fire = (BigInteger)args[3];

                        BigInteger light = (BigInteger)args[4];
                        BigInteger dark = (BigInteger)args[5];
                        BigInteger element1 = (BigInteger)args[6];
                        BigInteger element2 = (BigInteger)args[7];
                        BigInteger element3 = (BigInteger)args[8];
                        BigInteger element4 = (BigInteger)args[9];
                        BigInteger element5 = (BigInteger)args[10];

                        return SetElementInfo(owner, thunder, water, fire, light, dark, element1, element2, element3, element4, element5);
                    }
                }
                if (method == "CreateFairy")
                {
                    if (Runtime.CheckWitness(ContractOwner))
                    {
                        byte[] to = (byte[])args[0];
                        BigInteger level = (BigInteger)args[1];
                        BigInteger blood = (BigInteger)args[2];
                        BigInteger attack = (BigInteger)args[3];
                        BigInteger defense = (BigInteger)args[4];
                        BigInteger speed = (BigInteger)args[5];
                        BigInteger rare = (BigInteger)args[6];
                        BigInteger attribute = (BigInteger)args[7];
                        BigInteger character = (BigInteger)args[8];

                        return CreateFairy(to, level, blood, attack, defense, speed, rare, attribute, character);
                    }
                }
                if (method == "getFairyInfo")
                {
                    BigInteger tokenId = (BigInteger)args[0];

                    return GetFairyInfo(tokenId);
                }
                if (method == "createSale")
                {
                    if (args.Length != 4) return 0;
                    byte[] tokenOwner = (byte[])args[0];

                    BigInteger tokenId = (BigInteger)args[1];
                    BigInteger price = (BigInteger)args[2];
                    int sellType = (int)args[3];
                    return CreateSale(tokenOwner, tokenId, price, sellType);
                }
                if (method == "buySale")
                {
                    if (args.Length != 2) return 0;
                    byte[] owner = (byte[])args[0];
                    BigInteger tokenId = (BigInteger)args[1];

                    return BuySale(owner, tokenId);
                }
                if (method == "cancelSale")
                {
                    if (args.Length != 2) return 0;
                    byte[] owner = (byte[])args[0];
                    BigInteger tokenId = (BigInteger)args[1];

                    return CancelSale(owner, tokenId);
                }
                if (method == "getAuctionInfo")
                {
                    if (args.Length != 1) return 0;
                    BigInteger tokenId = (BigInteger)args[0];

                    return GetAuctionInfo(tokenId);
                }
                if (method == "getAuctionRecord")
                {
                    if (args.Length != 1)
                        return 0;
                    byte[] txid = (byte[])args[0];
                    return GetAuctionRecord(txid);
                }
                if (method == "drawToContractOwner")
                {
                    if (args.Length != 1) return 0;
                    BigInteger count = (BigInteger)args[0];

                    return DrawToContractOwner(count);
                }
                if (method == "setTicketValue")
                {
                    if (args.Length != 8) return 0;
                    BigInteger value1 = (BigInteger)args[0];
                    BigInteger value2 = (BigInteger)args[1];
                    BigInteger value3 = (BigInteger)args[2];
                    BigInteger value4 = (BigInteger)args[3];
                    BigInteger value5 = (BigInteger)args[4];
                    BigInteger value6 = (BigInteger)args[5];
                    BigInteger value7 = (BigInteger)args[6];
                    BigInteger value8 = (BigInteger)args[7];

                    if (Runtime.CheckWitness(ContractOwner))
                    {
                        return SetTicketValue(value1, value2, value3, value4, value5, value6, value7, value8);

                    }
                    return false;
                }
                if (method == "getTicketValue")
                {
                    return GetTicketValue();
                }
                if (method == "buyTicket")
                {
                    if (args.Length != 9) return 0;
                    byte[] buyer = (byte[])args[0];
                    int amount1 = (int)args[1];
                    int amount2 = (int)args[2];
                    int amount3 = (int)args[3];
                    int amount4 = (int)args[4];
                    int amount5 = (int)args[5];
                    int amount6 = (int)args[6];
                    int amount7 = (int)args[7];
                    int amount8 = (int)args[8];
                    return BuyTicket(buyer, amount1, amount2, amount3, amount4, amount5, amount6, amount7, amount8);
                }
                if (method == "useTicket")
                {
                    if (args.Length != 9) return 0;
                    byte[] buyer = (byte[])args[0];
                    int amount1 = (int)args[1];
                    int amount2 = (int)args[2];
                    int amount3 = (int)args[3];
                    int amount4 = (int)args[4];
                    int amount5 = (int)args[5];
                    int amount6 = (int)args[6];
                    int amount7 = (int)args[7];
                    int amount8 = (int)args[8];
                    return UseTicket(buyer, amount1, amount2, amount3, amount4, amount5, amount6, amount7, amount8);
                }

            }
            return false;
        }

        /// <summary>
        /// 不包含收取的手续费在内，所有用户转到合约的钱
        /// </summary>
        /// <returns></returns>
        public static BigInteger TotalExchargeSgas()
        {
            return Storage.Get(Storage.CurrentContext, "totalExchargeSgas").AsBigInteger();
        }

        /// <summary>
        /// 版本
        /// </summary>
        /// <returns></returns>
        public static string Version()
        {
            return "1.0.0.0";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static string Name()
        {
            return "FairyValley";
        }

        /// <summary>
        /// 用户在拍卖所存储的代币
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public static BigInteger BalanceOf(byte[] address)
        {
            return Storage.Get(Storage.CurrentContext, address).AsBigInteger();
        }

        public static BigInteger BalanceOfGas(byte[] address)
        {
            object[] args = new object[1] { address };
            BigInteger res = (BigInteger)gasCall("balanceOf", args);

            return res;
        }

        /// <summary>
        /// 充值
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="txid"></param>
        /// <returns></returns>
        public static bool RechargeToken(byte[] owner, byte[] txid)
        {
            if (owner.Length != 20)
            {
                Runtime.Log("Owner error.");
                return false;
            }

            byte[] txinfo = Storage.Get(Storage.CurrentContext, txid);
            if (txinfo.Length > 0)
            {
                // 已经处理过了
                return false;
            }


            // 查询交易记录
            object[] args = new object[1] { txid };

            object[] res = (object[])gasCall("getTXInfo", args);

            if (res.Length > 0)
            {
                byte[] from = (byte[])res[0];
                byte[] to = (byte[])res[1];
                BigInteger value = (BigInteger)res[2];

                if (from == owner)
                {
                    if (to == ExecutionEngine.ExecutingScriptHash)
                    {
                        // 标记为处理
                        Storage.Put(Storage.CurrentContext, txid, value);

                        BigInteger nMoney = 0;
                        byte[] ownerMoney = Storage.Get(Storage.CurrentContext, owner);
                        if (ownerMoney.Length > 0)
                        {
                            nMoney = ownerMoney.AsBigInteger();
                        }
                        nMoney += value;

                        _addTotal(value);

                        // 记账
                        Storage.Put(Storage.CurrentContext, owner, nMoney.AsByteArray());
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 提币
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public static bool DrawToken(byte[] sender, BigInteger count)
        {
            if (sender.Length != 20)
            {
                Runtime.Log("Owner error.");
                return false;
            }

            if (Runtime.CheckWitness(sender))
            {
                BigInteger nMoney = 0;
                byte[] ownerMoney = Storage.Get(Storage.CurrentContext, sender);
                if (ownerMoney.Length > 0)
                {
                    nMoney = ownerMoney.AsBigInteger();
                }
                if (count <= 0 || count > nMoney)
                {
                    // 全部提走
                    count = nMoney;
                }

                // 转账
                object[] args = new object[3] { ExecutionEngine.ExecutingScriptHash, sender, count };
                //byte[] sgasHash = Storage.Get(Storage.CurrentContext, "sgas");
                //deleDyncall dyncall = (deleDyncall)sgasHash.ToDelegate();
                bool res = (bool)gasCall("transfer_app", args);
                if (!res)
                {
                    return false;
                }

                // 记账
                nMoney -= count;

                _subTotal(count);

                if (nMoney > 0)
                {
                    Storage.Put(Storage.CurrentContext, sender, nMoney.AsByteArray());
                }
                else
                {
                    Storage.Delete(Storage.CurrentContext, sender);
                }

                return true;
            }
            return false;
        }

        /// <summary>
        /// 存储增加的代币数量
        /// </summary>
        /// <param name="count"></param>
        private static void _addTotal(BigInteger count)
        {
            BigInteger total = Storage.Get(Storage.CurrentContext, "totalExchargeSgas").AsBigInteger();
            total += count;
            Storage.Put(Storage.CurrentContext, "totalExchargeSgas", total);
        }

        /// <summary>
        /// 存储减少的代币数总量
        /// </summary>
        /// <param name="count"></param>
        private static void _subTotal(BigInteger count)
        {
            BigInteger total = Storage.Get(Storage.CurrentContext, "totalExchargeSgas").AsBigInteger();
            total -= count;
            if (total > 0)
            {
                Storage.Put(Storage.CurrentContext, "totalExchargeSgas", total);
            }
            else
            {

                Storage.Delete(Storage.CurrentContext, "totalExchargeSgas");
            }
        }

        public static object[] GetTicketInfoObject(byte[] owner)
        {
            object[] result;
            byte[] v = Storage.Get(Storage.CurrentContext, new byte[] { 0x01 }.Concat(owner));
            if (v.Length == 0)
                result = new object[0];
            result = (object[])Helper.Deserialize(v);
            return result;
        }

        /// <summary>
        /// 获取门票信息
        /// </summary>
        /// <param name="owner"></param>
        /// <returns></returns>
        public static TicketInfo GetTicketInfo(byte[] owner)
        {
            object[] result;
            byte[] v = Storage.Get(Storage.CurrentContext, new byte[] { 0x01 }.Concat(owner));
            if (v.Length == 0)
                result = new object[0];
            result = (object[])Helper.Deserialize(v);
            TicketInfo info = (TicketInfo)(object)result;
            return info;



        }

        public static object[] GetElementInfoObject(byte[] owner)
        {
            object[] result;
            byte[] v = Storage.Get(Storage.CurrentContext, new byte[] { 0x11 }.Concat(owner));
            if (v.Length == 0)
                result = new object[0];
            result = (object[])Helper.Deserialize(v);
            return result;
        }

        /// <summary>
        /// 获取元素信息
        /// </summary>
        /// <param name="owner"></param>
        /// <returns></returns>
        public static ElementInfo GetElementInfo(byte[] owner)
        {
            object[] result;
            byte[] v = Storage.Get(Storage.CurrentContext, new byte[] { 0x11 }.Concat(owner));
            if (v.Length == 0)
                result = new object[0];
            result = (object[])Helper.Deserialize(v);
            ElementInfo info = (ElementInfo)(object)result;
            return info;
        }

        //调整门票
        private static bool SetTicketInfo(byte[] owner, BigInteger arena, BigInteger hunt, BigInteger collect, BigInteger ticket1, BigInteger ticket2, BigInteger ticket3, BigInteger ticket4, BigInteger ticket5)
        {
            TicketInfo info = new TicketInfo
            {
                owner = owner,
                arena = arena,
                hunt = hunt,
                collect = collect,
                ticket1 = ticket1,
                ticket2 = ticket2,
                ticket3 = ticket3,
                ticket4 = ticket4,
                ticket5 = ticket5
            };

            byte[] bytesInfo = Helper.Serialize(info);
            Storage.Put(Storage.CurrentContext, new byte[] { 0x01 }.Concat(owner), bytesInfo);
            return true;
        }

        //调整元素
        private static bool SetElementInfo(byte[] owner, BigInteger thunder, BigInteger water, BigInteger fire, BigInteger light, BigInteger dark, BigInteger element1, BigInteger element2, BigInteger element3, BigInteger element4, BigInteger element5)
        {
            ElementInfo info = new ElementInfo
            {
                owner = owner,
                thunder = thunder,
                water = water,
                fire = fire,
                light = light,
                dark = dark,
                element1 = element1,
                element2 = element2,
                element3 = element3,
                element4 = element4,
                element5 = element5
            };

            byte[] bytesInfo = Helper.Serialize(info);
            Storage.Put(Storage.CurrentContext, new byte[] { 0x11 }.Concat(owner), bytesInfo);
            return true;
        }

        //创建精灵
        private static BigInteger CreateFairy(byte[] to, BigInteger level, BigInteger blood, BigInteger attack, BigInteger defense, BigInteger speed, BigInteger rare, BigInteger attribute, BigInteger character)
        {
            if (to.Length != 20)
                return 0;

            if (Runtime.CheckWitness(ContractOwner))
            {
                byte[] tokenId = Storage.Get(Storage.CurrentContext, "FairyNo");

                BigInteger newToken = tokenId.AsBigInteger() + 1;
                tokenId = newToken.AsByteArray();

                FairyInfo fairy = new FairyInfo
                {
                    owner = to,
                    level = 1,
                    blood = blood,
                    attack = attack,
                    defense = defense,
                    speed = speed,
                    rare = rare,
                    attribute = attribute,
                    character = character,
                    birthTime = Blockchain.GetHeader(Blockchain.GetHeight()).Timestamp
                };

                byte[] fairyInfo = Helper.Serialize(fairy);

                Storage.Put(Storage.CurrentContext, tokenId, fairyInfo);
                Storage.Put(Storage.CurrentContext, "FairyNo", tokenId);

                Birthed(newToken, to, level, blood, attack, defense, speed, rare, attribute, character, fairy.birthTime);

                return newToken;
            }
            else
            {
                Runtime.Log("Only the contract owner may mint new tokens.");
                return 0;
            }
        }

        private static FairyInfo GetFairyInfo(BigInteger tokenId)
        {
            object[] result;
            byte[] v = Storage.Get(Storage.CurrentContext, tokenId.AsByteArray());
            if (v.Length == 0)
                result = new object[0];
            result = (object[])Helper.Deserialize(v);
            FairyInfo info = (FairyInfo)(object)result;
            return info;

        }
        private static object[] GetFairyInfoObject(BigInteger tokenId)
        {
            object[] result;
            byte[] v = Storage.Get(Storage.CurrentContext, tokenId.AsByteArray());
            if (v.Length == 0)
                result = new object[0];
            result = (object[])Helper.Deserialize(v);
            return result;

        }

        
        //创建拍卖
        
        public static bool CreateSale(byte[] tokenOwner, BigInteger tokenId, BigInteger price, int sellType)
        {
            if (tokenOwner.Length != 20)
            {
                Runtime.Log("Owner error.");
                return false;
            }
            if (!Runtime.CheckWitness(tokenOwner))
            {
                return false;
            }
            if (price < 0)
            {
                return false;
            }

            if (price < TX_MIN_FEE)
            {
                
                return false;
            }

            //if (Runtime.CheckWitness(tokenOwner))
            // 物品放在拍卖行
            //object[] args = new object[3] { tokenOwner, ExecutionEngine.ExecutingScriptHash, tokenId };
            bool res = TransferFairy(tokenOwner, ExecutionEngine.ExecutingScriptHash, tokenId);
            if (res)
            {
                var nowtime = Blockchain.GetHeader(Blockchain.GetHeight()).Timestamp;

                AuctionInfo info = new AuctionInfo();
                info.owner = tokenOwner;
                info.sellType = sellType;
                info.sellTime = nowtime;
                info.price = price;

                // 入库记录
                byte[] auctionInfo = Helper.Serialize(info);
                byte[] keyId = new byte[] { 0x13 }.Concat(tokenId.AsByteArray());
                Storage.Put(Storage.CurrentContext, keyId, auctionInfo);

                // notify
                Auctioned(tokenOwner, tokenId, price, sellType, nowtime);
                return true;
            }

            return false;
        }

        
        //从拍卖场购买,将钱划入合约，物品给买家
        
        public static bool BuySale(byte[] sender, BigInteger tokenId)
        {
            if (!Runtime.CheckWitness(sender))
            {
                //没有签名
                return false;
            }

            //byte[] auctionId = new byte[] { 0x13 }.Concat(tokenId.AsByteArray());
            object[] objInfo = getAuctionInfo(tokenId.AsByteArray());
            if (objInfo.Length > 0)
            {
                AuctionInfo info = (AuctionInfo)(object)objInfo;
                byte[] owner = info.owner;

                var nowtime = Blockchain.GetHeader(Blockchain.GetHeight()).Timestamp;

                BigInteger senderMoney = Storage.Get(Storage.CurrentContext, sender).AsBigInteger();
                BigInteger curBuyPrice = info.price;
                var fee = curBuyPrice * 50 / 1000;
                if (fee < TX_MIN_FEE)
                {
                    fee = TX_MIN_FEE;
                }
                if (curBuyPrice < fee)
                {
                    curBuyPrice = fee;
                }

                if (senderMoney < curBuyPrice)
                {
                    // 钱不够
                    return false;
                }

                // 转移物品
                //object[] args = new object[3] { ExecutionEngine.ExecutingScriptHash, sender, tokenId };
                bool res = TransferFairy(ExecutionEngine.ExecutingScriptHash, sender, tokenId);
                if (!res)
                {
                    return false;
                }

                // 扣钱
                Storage.Put(Storage.CurrentContext, sender, senderMoney - curBuyPrice);

                // 扣除手续费
                BigInteger sellPrice = curBuyPrice - fee;
                _subTotal(fee);

                // 钱记在卖家名下
                BigInteger nMoney = 0;
                byte[] salerMoney = Storage.Get(Storage.CurrentContext, owner);
                if (salerMoney.Length > 0)
                {
                    nMoney = salerMoney.AsBigInteger();
                }
                nMoney = nMoney + sellPrice;
                Storage.Put(Storage.CurrentContext, owner, nMoney);

                // 删除拍卖记录
                
                DeleteAuctionInfo(tokenId.AsByteArray());

                // 成交记录
                AuctionRecord record = new AuctionRecord();
                record.tokenId = tokenId;
                record.seller = owner;
                record.buyer = sender;
                record.sellType = 0;
                record.sellPrice = curBuyPrice;
                record.sellTime = nowtime;

                putAuctionRecord(tokenId.AsByteArray(), record);

                // notify
                AuctionBuy(sender, tokenId, curBuyPrice, fee, nowtime);
                return true;

            }
            return false;
        }

        private static bool TransferFairy(byte[] from, byte[] to, BigInteger tokenId)
        {
            if (from.Length != 20)
            {
                return false;
            }
            if (to.Length != 20)
            {
                return false;
            }

            StorageContext ctx = Storage.CurrentContext;

            if (from == to)
            {
                return true;
            }

            object[] objInfo = GetFairyInfoObject(tokenId);
            if (objInfo.Length == 0)
            {
                return false;
            }

            FairyInfo info = (FairyInfo)(object)objInfo;
            byte[] ownedBy = info.owner;

            if (from != ownedBy)
            {
                //Runtime.Log("Token is not owned by tx sender");
                return false;
            }

            info.owner = to;
            byte[] fairyInfo = Helper.Serialize(info);

            Storage.Put(Storage.CurrentContext, tokenId.AsByteArray(), fairyInfo);

            //记录交易信息
            setTxInfo(from, to, tokenId);

            Transferred(from, to, tokenId);
            return true;

        }

        /**
         * 存储交易信息
         */
        private static void setTxInfo(byte[] from, byte[] to, BigInteger value)
        {

            TransferInfo info = new TransferInfo();
            info.from = from;
            info.to = to;
            info.value = value;

            byte[] txinfo = Helper.Serialize(info);

            byte[] txid = (ExecutionEngine.ScriptContainer as Transaction).Hash;
            byte[] keytxid = new byte[] { 0x12 }.Concat(txid);
            Storage.Put(Storage.CurrentContext, keytxid, txinfo);
        }


        //获取拍卖信息
        private static object[] getAuctionInfo(byte[] tokenId)
        {
            byte[] auctionKeyId = new byte[] { 0x13 }.Concat(tokenId);
            byte[] v = Storage.Get(Storage.CurrentContext, auctionKeyId);
            if (v.Length == 0)
                return new object[0];
            return (object[])Helper.Deserialize(v);
        }


        //存储拍卖成交记录
        private static void putAuctionRecord(byte[] tokenId, AuctionRecord info)
        {

            byte[] txInfo = Helper.Serialize(info);

            var key = new byte[] { 0x14 }.Concat(tokenId);
            Storage.Put(Storage.CurrentContext, key, txInfo);
        }


        //取消拍卖
        public static bool CancelSale(byte[] sender, BigInteger tokenId)
        {
            object[] objInfo = getAuctionInfo(tokenId.AsByteArray());
            if (objInfo.Length > 0)
            {
                AuctionInfo info = (AuctionInfo)(object)objInfo;
                byte[] tokenOwner = info.owner;

                if (sender != tokenOwner)
                {
                    return false;
                }

                if (Runtime.CheckWitness(sender))
                {
                    object[] args = new object[3] { ExecutionEngine.ExecutingScriptHash, tokenOwner, tokenId };
                    bool res = TransferFairy(ExecutionEngine.ExecutingScriptHash, tokenOwner, tokenId);
                    if (res)
                    {
                        DeleteAuctionInfo(tokenId.AsByteArray());

                        CancelAuctioned(tokenOwner, tokenId);
                        return true;
                    }
                }
            }
            return false;
        }

        private static void DeleteAuctionInfo(byte[] tokenId)
        {
            byte[] auctionId = new byte[] { 0x13 }.Concat(tokenId);
            Storage.Delete(Storage.CurrentContext, auctionId);
        }


        //获取拍卖信息
        public static AuctionInfo GetAuctionInfo(BigInteger tokenId)
        {
            object[] objInfo = getAuctionInfo(tokenId.AsByteArray());
            AuctionInfo info = (AuctionInfo)(object)objInfo;

            return info;
        }


        //获取拍卖成交记录
        public static AuctionRecord GetAuctionRecord(byte[] tokenId)
        {
            object[] result;
            var key = new byte[] { 0x14 }.Concat(tokenId);
            byte[] v = Storage.Get(Storage.CurrentContext, key);
            if (v.Length == 0)
                result = new object[0];
            result = (object[])Helper.Deserialize(v);
            AuctionRecord info = (AuctionRecord)(object)result;
            return info;
        }

        
        //将收入提款到合约拥有者
        public static bool DrawToContractOwner(BigInteger count)
        {
            if (Runtime.CheckWitness(ContractOwner))
            {
                BigInteger nMoney = 0;
                // 查询余额

                BigInteger totalMoney = BalanceOfGas(ExecutionEngine.ExecutingScriptHash);
                BigInteger supplyMoney = Storage.Get(Storage.CurrentContext, "totalExchargeSgas").AsBigInteger();

                BigInteger canDrawMax = totalMoney - supplyMoney;
                if (count <= 0 || count > canDrawMax)
                {
                    // 全部提走
                    count = canDrawMax;
                }

                // 转账
                object[] args = new object[3] { ExecutionEngine.ExecutingScriptHash, ContractOwner, count };

                bool res = (bool)gasCall("transfer_app", args);
                if (!res)
                {
                    return false;
                }

                // 记账
                //_subTotal(count);??
                return true;
            }
            return false;
        }

        public static bool SetTicketValue(BigInteger value1, BigInteger value2, BigInteger value3, BigInteger value4, BigInteger value5, BigInteger value6, BigInteger value7, BigInteger value8)
        {
            TicketValue ticketValue = new TicketValue();
            ticketValue.arena = value1;
            ticketValue.hunt = value2;
            ticketValue.collect = value3;
            ticketValue.ticket1 = value4;
            ticketValue.ticket2 = value5;
            ticketValue.ticket3 = value6;
            ticketValue.ticket4 = value7;
            ticketValue.ticket5 = value8;

            byte[] ticketValueByte = Helper.Serialize(ticketValue);

            Storage.Put(Storage.CurrentContext, "TicketValue", ticketValueByte);
            return true;
        }

        public static TicketValue GetTicketValue()
        {
            object[] result;
            byte[] v = Storage.Get(Storage.CurrentContext, "TicketValue");
            if (v.Length == 0)
                result = new object[0];
            result = (object[])Helper.Deserialize(v);
            TicketValue value = (TicketValue)(object)result;
            return value;
        }
        private static object[] GetTicketValueObject()
        {
            object[] result;
            byte[] v = Storage.Get(Storage.CurrentContext, "TicketValue");
            if (v.Length == 0)
                result = new object[0];
            result = (object[])Helper.Deserialize(v);
            return result;
        }

        public static bool BuyTicket(byte[] buyer, int amount1, int amount2, int amount3, int amount4, int amount5, int amount6, int amount7, int amount8)
        {
            if (!Runtime.CheckWitness(buyer))
            {
                //没有签名
                return false;
            }
            var nowtime = Blockchain.GetHeader(Blockchain.GetHeight()).Timestamp;

            BigInteger buyerMoney = Storage.Get(Storage.CurrentContext, buyer).AsBigInteger();

            object[] ticketValueObject = GetTicketValueObject();
            if (ticketValueObject.Length > 0)
            {
                TicketValue ticketValue = (TicketValue)(object)ticketValueObject;
                BigInteger allValue = 0;
                if (ticketValue.arena > 0)
                {
                    allValue += ticketValue.arena * amount1;
                }
                else
                {
                    amount1 = 0;
                }
                if (ticketValue.hunt > 0)
                {
                    allValue += ticketValue.hunt * amount2;
                }
                else
                {
                    amount2 = 0;
                }
                if (ticketValue.collect > 0)
                {
                    allValue += ticketValue.collect * amount3;
                }
                else
                {
                    amount3 = 0;
                }
                if (ticketValue.ticket1 > 0)
                {
                    allValue += ticketValue.ticket1 * amount4;
                }
                else
                {
                    amount4 = 0;
                }
                if (ticketValue.ticket2 > 0)
                {
                    allValue += ticketValue.ticket2 * amount5;
                }
                else
                {
                    amount5 = 0;
                }
                if (ticketValue.ticket3 > 0)
                {
                    allValue += ticketValue.ticket3 * amount6;
                }
                else
                {
                    amount6 = 0;
                }
                if (ticketValue.ticket4 > 0)
                {
                    allValue += ticketValue.ticket4 * amount7;
                }
                else
                {
                    amount7 = 0;
                }
                if (ticketValue.ticket5 > 0)
                {
                    allValue += ticketValue.ticket5 * amount8;
                }
                else
                {
                    amount8 = 0;
                }
                if (buyerMoney < allValue)
                {
                    return false;
                }

                // 扣钱 买票
                Storage.Put(Storage.CurrentContext, buyer, buyerMoney - allValue);
                //减掉所有存的钱
                _subTotal(allValue);

                //给买家 票
                object[] ticketInfoObject = GetTicketInfoObject(buyer);
                if (ticketInfoObject.Length > 0)
                {
                    TicketInfo ticketInfo = (TicketInfo)(object)ticketInfoObject;
                    ticketInfo.arena += amount1;
                    ticketInfo.hunt += amount2;
                    ticketInfo.collect += amount3;
                    ticketInfo.ticket1 += amount4;
                    ticketInfo.ticket2 += amount5;
                    ticketInfo.ticket3 += amount6;
                    ticketInfo.ticket4 += amount7;
                    ticketInfo.ticket5 += amount8;

                    SetTicketInfo(buyer, ticketInfo.arena, ticketInfo.hunt, ticketInfo.collect, ticketInfo.ticket1, ticketInfo.ticket2, ticketInfo.ticket3, ticketInfo.ticket4, ticketInfo.ticket5);
                }
                else
                {
                    SetTicketInfo(buyer, amount1, amount2, amount3, amount4, amount5, amount6, amount7, amount8);
                }


                BuyTicketed(buyer, amount1, amount2, amount3, amount4, amount5, amount6, amount7, amount8);
                return true;
            }

            return false;
        }

        public static bool UseTicket(byte[] buyer, int amount1, int amount2, int amount3, int amount4, int amount5, int amount6, int amount7, int amount8)
        {
            if (!Runtime.CheckWitness(buyer))
            {
                //没有签名
                return false;
            }
            var nowtime = Blockchain.GetHeader(Blockchain.GetHeight()).Timestamp;

            if (amount1 < 0 || amount2 < 0 || amount3 < 0 || amount4 < 0 || amount5 < 0 || amount6 < 0 || amount7 < 0 || amount8 < 0)
            {
                return false;
            }

            object[] ticketInfoObject = GetTicketInfoObject(buyer);
            if (ticketInfoObject.Length > 0)
            {
                TicketInfo ticketInfo = (TicketInfo)(object)ticketInfoObject;

                if (ticketInfo.arena < amount1 || ticketInfo.hunt < amount2 || ticketInfo.collect < amount3 || ticketInfo.ticket1 < amount4 || ticketInfo.ticket2 < amount5 || ticketInfo.ticket3 < amount6 || ticketInfo.ticket4 < amount7 || ticketInfo.ticket5 < amount8)
                {

                    ticketInfo.arena -= amount1;
                    ticketInfo.hunt -= amount2;
                    ticketInfo.collect -= amount3;
                    ticketInfo.ticket1 -= amount4;
                    ticketInfo.ticket2 -= amount5;
                    ticketInfo.ticket3 -= amount6;
                    ticketInfo.ticket4 -= amount7;
                    ticketInfo.ticket5 -= amount8;

                    SetTicketInfo(buyer, ticketInfo.arena, ticketInfo.hunt, ticketInfo.collect, ticketInfo.ticket1, ticketInfo.ticket2, ticketInfo.ticket3, ticketInfo.ticket4, ticketInfo.ticket5);
                    UseTicketed(buyer, amount1, amount2, amount3, amount4, amount5, amount6, amount7, amount8);
                    return true;
                }
                else
                {
                    return false;
                }

            }
            return false;
        }
    }
}
