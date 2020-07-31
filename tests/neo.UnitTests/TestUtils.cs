using FluentAssertions;
using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets.NEP6;
using System;
using System.IO;
using System.Linq;

namespace Neo.UnitTests
{
    public static class TestUtils
    {
        public static readonly Random TestRandom = new Random(1337); // use fixed seed for guaranteed determinism

        public static ContractManifest CreateDefaultManifest(UInt160 hash)
        {
            return new ContractManifest()
            {
                Groups = new ContractGroup[0],
                Features = ContractFeatures.NoProperty,
                SupportedStandards = Array.Empty<string>(),
                Abi = new ContractAbi()
                {
                    Hash = hash,
                    Events = new ContractEventDescriptor[0],
                    Methods = new ContractMethodDescriptor[0]
                },
                Permissions = new[] { ContractPermission.DefaultPermission },
                Trusts = WildcardContainer<UInt160>.Create(),
                SafeMethods = WildcardContainer<string>.Create(),
                Extra = null
            };
        }

        public static ContractManifest CreateManifest(UInt160 hash, string method, ContractParameterType returnType, params ContractParameterType[] parameterTypes)
        {
            ContractManifest manifest = CreateDefaultManifest(hash);
            manifest.Abi.Methods = new ContractMethodDescriptor[]
            {
                new ContractMethodDescriptor()
                {
                    Name = method,
                    Parameters = parameterTypes.Select((p, i) => new ContractParameterDefinition
                    {
                        Name = $"p{i}",
                        Type = p
                    }).ToArray(),
                    ReturnType = returnType
                }
            };
            return manifest;
        }

        public static StorageKey CreateStorageKey(this NativeContract contract, byte prefix, ISerializable key)
        {
            return new StorageKey
            {
                Id = contract.Id,
                Key = key.ToArray().Prepend(prefix).ToArray()
            };
        }

        public static byte[] GetByteArray(int length, byte firstByte)
        {
            byte[] array = new byte[length];
            array[0] = firstByte;
            for (int i = 1; i < length; i++)
            {
                array[i] = 0x20;
            }
            return array;
        }

        public static NEP6Wallet GenerateTestWallet()
        {
            JObject wallet = new JObject();
            wallet["name"] = "noname";
            wallet["version"] = new Version("3.0").ToString();
            wallet["scrypt"] = new ScryptParameters(0, 0, 0).ToJson();
            wallet["accounts"] = new JArray();
            wallet["extra"] = null;
            wallet.ToString().Should().Be("{\"name\":\"noname\",\"version\":\"3.0\",\"scrypt\":{\"n\":0,\"r\":0,\"p\":0},\"accounts\":[],\"extra\":null}");
            return new NEP6Wallet(wallet);
        }

        public static Transaction GetTransaction(UInt160 sender)
        {
            return new Transaction
            {
                Script = new byte[1],
                Attributes = Array.Empty<TransactionAttribute>(),
                Signers = new[]{ new Signer()
                {
                    Account = sender,
                    Scopes = WitnessScope.CalledByEntry
                } },
                Witnesses = new Witness[]{ new Witness
                {
                    InvocationScript = new byte[0],
                    VerificationScript = new byte[0]
                } }
            };
        }

        internal static ContractState GetContract(string method = "test", int parametersCount = 0)
        {
            return new ContractState
            {
                Id = 0x43000000,
                Script = new byte[] { 0x01, 0x01, 0x01, 0x01 },
                Manifest = CreateManifest(UInt160.Parse("0xa400ff00ff00ff00ff00ff00ff00ff00ff00ff01"), method, ContractParameterType.Any, Enumerable.Repeat(ContractParameterType.Any, parametersCount).ToArray())
            };
        }

        internal static ContractState GetContract(byte[] script)
        {
            return new ContractState
            {
                Id = 1,
                Script = script,
                Manifest = CreateDefaultManifest(script.ToScriptHash())
            };
        }

        internal static StorageItem GetStorageItem(byte[] value)
        {
            return new StorageItem
            {
                Value = value
            };
        }

        internal static StorageKey GetStorageKey(int id, byte[] keyValue)
        {
            return new StorageKey
            {
                Id = id,
                Key = keyValue
            };
        }

        public static void SetupHeaderWithValues(Header header, UInt256 val256, out UInt256 merkRootVal, out UInt160 val160, out ulong timestampVal, out uint indexVal, out Witness scriptVal)
        {
            setupBlockBaseWithValues(header, val256, out merkRootVal, out val160, out timestampVal, out indexVal, out scriptVal);
        }

        public static void SetupBlockWithValues(Block block, UInt256 val256, out UInt256 merkRootVal, out UInt160 val160, out ulong timestampVal, out uint indexVal, out Witness scriptVal, out Transaction[] transactionsVal, int numberOfTransactions)
        {
            setupBlockBaseWithValues(block, val256, out merkRootVal, out val160, out timestampVal, out indexVal, out scriptVal);

            transactionsVal = new Transaction[numberOfTransactions];
            if (numberOfTransactions > 0)
            {
                for (int i = 0; i < numberOfTransactions; i++)
                {
                    transactionsVal[i] = TestUtils.GetTransaction(UInt160.Zero);
                }
            }

            block.ConsensusData = new ConsensusData();
            block.Transactions = transactionsVal;
            block.MerkleRoot = merkRootVal = Block.CalculateMerkleRoot(block.ConsensusData.Hash, block.Transactions.Select(p => p.Hash));
        }

        private static void setupBlockBaseWithValues(BlockBase bb, UInt256 val256, out UInt256 merkRootVal, out UInt160 val160, out ulong timestampVal, out uint indexVal, out Witness scriptVal)
        {
            bb.PrevHash = val256;
            merkRootVal = UInt256.Parse("0x6226416a0e5aca42b5566f5a19ab467692688ba9d47986f6981a7f747bba2772");
            bb.MerkleRoot = merkRootVal;
            timestampVal = new DateTime(1980, 06, 01, 0, 0, 1, 001, DateTimeKind.Utc).ToTimestampMS(); // GMT: Sunday, June 1, 1980 12:00:01.001 AM
            bb.Timestamp = timestampVal;
            indexVal = 0;
            bb.Index = indexVal;
            val160 = UInt160.Zero;
            bb.NextConsensus = val160;
            scriptVal = new Witness
            {
                InvocationScript = new byte[0],
                VerificationScript = new[] { (byte)OpCode.PUSH1 }
            };
            bb.Witness = scriptVal;
        }

        public static Transaction CreateRandomHashTransaction()
        {
            var randomBytes = new byte[16];
            TestRandom.NextBytes(randomBytes);
            return new Transaction
            {
                Script = randomBytes,
                Attributes = Array.Empty<TransactionAttribute>(),
                Signers = new Signer[] { new Signer() { Account = UInt160.Zero } },
                Witnesses = new[]
                {
                    new Witness
                    {
                        InvocationScript = new byte[0],
                        VerificationScript = new byte[0]
                    }
                }
            };
        }

        public static T CopyMsgBySerialization<T>(T serializableObj, T newObj) where T : ISerializable
        {
            using (MemoryStream ms = new MemoryStream(serializableObj.ToArray(), false))
            using (BinaryReader reader = new BinaryReader(ms))
            {
                newObj.Deserialize(reader);
            }

            return newObj;
        }

        public static void DeleteFile(string file)
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }
}
