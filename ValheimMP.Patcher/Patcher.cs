using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace ValheimMP.Patcher
{
    public static class Patcher
    {
        // List of assemblies to patch
        public static IEnumerable<string> TargetDLLs { get; } = new[] { "assembly_valheim.dll" };



        // Patches the assemblies
        public static void Patch(AssemblyDefinition assembly)
        {
            var valheim = assembly.MainModule;

            // valheim types
            var ZNetType = valheim.GetType("ZNet");
            var ZRpcType = valheim.GetType("ZRpc");
            var ZDOIDType = valheim.GetType("ZDOID");
            var ZNetPeerType = valheim.GetType("ZNetPeer");
            var CharacterType = valheim.GetType("Character");
            var PlayerType = valheim.GetType("Player");
            var PlayerProfileType = valheim.GetType("PlayerProfile");
            var ZPackageType = valheim.GetType("ZPackage");
            var ZDOType = valheim.GetType("ZDO");
            var ZNetViewType = valheim.GetType("ZNetView");
            var ZDOManType = valheim.GetType("ZDOMan");
            var ZDOPeerType = valheim.GetType("ZDOMan/ZDOPeer");
            var HumanoidType = valheim.GetType("Humanoid");
            var HitDataType = valheim.GetType("HitData");

            TypeReference Vector2Type, Vector2iType, Vector3Type;
            // Seems try get works differently from Get and actually returns something for things that do not directly belong to assembly-valheim.dll
            valheim.TryGetTypeReference("UnityEngine.Vector2", out Vector2Type);
            valheim.TryGetTypeReference("Vector2i", out Vector2iType);
            valheim.TryGetTypeReference("UnityEngine.Vector3", out Vector3Type);

            // system types
            var BoolType = assembly.MainModule.TypeSystem.Boolean;
            var FloatType = assembly.MainModule.TypeSystem.Single;
            var VoidType = assembly.MainModule.TypeSystem.Void;
            var IntType = assembly.MainModule.TypeSystem.Int32;
            var LongType = assembly.MainModule.TypeSystem.Int64;
            var StringType = assembly.MainModule.TypeSystem.String;
            var ObjectType = assembly.MainModule.TypeSystem.Object;
            var ByteArrayType = assembly.MainModule.ImportReference(typeof(byte[]));


            // Player
            {
                var m_delayedDamage = new FieldDefinition("m_delayedDamage", FieldAttributes.Private | FieldAttributes.NotSerialized,
                    assembly.MainModule.ImportReference(typeof(System.Collections.Generic.Queue<>))
                        .MakeGenericInstanceType(assembly.MainModule.ImportReference(typeof(System.Collections.Generic.KeyValuePair<,>))
                            .MakeGenericInstanceType(FloatType, HitDataType)));
                PlayerType.Fields.Add(m_delayedDamage);
                PlayerType.Fields.Add(new FieldDefinition("m_lastMoveFlags", FieldAttributes.Private, IntType));
                PlayerType.Fields.Add(new FieldDefinition("m_lastMoveDir", FieldAttributes.Private, Vector3Type));
                PlayerType.Fields.Add(new FieldDefinition("m_clientPosition", FieldAttributes.Private, Vector3Type));
            }

            // ZNetPeer
            {
                ZNetPeerType.Fields.Add(new FieldDefinition("m_player", FieldAttributes.Private, PlayerType));
                ZNetPeerType.Fields.Add(new FieldDefinition("m_playerProfile", FieldAttributes.Private, PlayerProfileType));
                ZNetPeerType.Fields.Add(new FieldDefinition("m_requestRespawn", FieldAttributes.Private, BoolType));
                ZNetPeerType.Fields.Add(new FieldDefinition("m_respawnWait", FieldAttributes.Private, FloatType));
                ZNetPeerType.Fields.Add(new FieldDefinition("m_haveSpawned", FieldAttributes.Private, BoolType));
                ZNetPeerType.Fields.Add(new FieldDefinition("m_firstSpawn", FieldAttributes.Private, BoolType));
                ZNetPeerType.Fields.Add(new FieldDefinition("m_lastMoveFlags", FieldAttributes.Private, IntType));
                ZNetPeerType.Fields.Add(new FieldDefinition("m_lastMoveDir", FieldAttributes.Private, Vector3Type));

                var m_loadedSectors = new FieldDefinition("m_loadedSectors", FieldAttributes.Private,
                    assembly.MainModule.ImportReference(typeof(System.Collections.Generic.Dictionary<,>))
                    .MakeGenericInstanceType(Vector2iType,
                        assembly.MainModule.ImportReference(typeof(System.Collections.Generic.KeyValuePair<,>))
                        .MakeGenericInstanceType(IntType, BoolType)
                    ));
                ZNetPeerType.Fields.Add(m_loadedSectors);
                ZNetPeerType.Fields.Add(new FieldDefinition("m_loadedSectorsTouch", FieldAttributes.Private, IntType));

                var m_solidObjectQueue = new FieldDefinition("m_solidObjectQueue", FieldAttributes.Private,
                    assembly.MainModule.ImportReference(typeof(System.Collections.Generic.Dictionary<,>))
                    .MakeGenericInstanceType(ZDOIDType, ZDOType));

                ZNetPeerType.Fields.Add(m_solidObjectQueue);
            }


            // ZDOMan/ZDOPeer
            {
                var m_zdoImage = new FieldDefinition("m_zdoImage", FieldAttributes.Private,
                    assembly.MainModule.ImportReference(typeof(System.Collections.Generic.Dictionary<,>))
                    .MakeGenericInstanceType(ZDOIDType, ZDOType));

                ZDOPeerType.Fields.Add(m_zdoImage);
            }


            // ZDO
            {
                ZDOType.Fields.Add(new FieldDefinition("m_nview", FieldAttributes.Private, ZNetViewType));
                ZDOType.Fields.Add(new FieldDefinition("m_originator", FieldAttributes.Private, LongType));
                ZDOType.Fields.Add(new FieldDefinition("m_zdoType", FieldAttributes.Private, IntType));

                ZDOType.Fields.Add(new FieldDefinition("m_fieldTypes", FieldAttributes.Private,
                    assembly.MainModule.ImportReference(typeof(System.Collections.Generic.Dictionary<,>))
                    .MakeGenericInstanceType(IntType, IntType)));

                ZDOType.Fields.Add(new FieldDefinition("m_zdoEvents", FieldAttributes.Private,
                    assembly.MainModule.ImportReference(typeof(System.Collections.Generic.Dictionary<,>))
                    .MakeGenericInstanceType(IntType, assembly.MainModule.ImportReference(typeof(System.Action<>))
                    .MakeGenericInstanceType(ZDOType))));
            }


            // SEMan
            {
                var SEManType = valheim.GetType("SEMan");
                SEManType.Fields.Add(new FieldDefinition("m_clientStatus", FieldAttributes.Private, ObjectType));
                SEManType.Fields.Add(new FieldDefinition("m_clientStatusSyncTime", FieldAttributes.Private, FloatType));
            }

            // HitData
            {
                HitDataType.Fields.Add(new FieldDefinition("m_attackerCharacter", FieldAttributes.Private, CharacterType));
            }

            // Container
            {
                var ContainerType = valheim.GetType("Container");
                var m_onTakeAllSuccess2 = new FieldDefinition("m_onTakeAllSuccess2", FieldAttributes.Private,
                    assembly.MainModule.ImportReference(typeof(System.Action<>))
                    .MakeGenericInstanceType(HumanoidType));

                ContainerType.Fields.Add(m_onTakeAllSuccess2);
            }

            // Ladder
            {
                var LadderType = valheim.GetType("Ladder");

                {
                    LadderType.Methods.Add(CreateEmptyMethod("Awake", VoidType));
                }
            }

            // Chair
            {
                var ChairType = valheim.GetType("Chair");

                {
                    ChairType.Methods.Add(CreateEmptyMethod("Awake", VoidType));
                }
            }

            // Teleport
            {
                var TeleportType = valheim.GetType("Teleport");

                {
                    TeleportType.Methods.Add(CreateEmptyMethod("Start", VoidType));
                }
            }

            // OfferingBowl
            {
                var OfferingBowlType = valheim.GetType("OfferingBowl");

                {
                    OfferingBowlType.Methods.Add(CreateEmptyMethod("Start", VoidType));
                }
            }

            // ZSyncAnimation
            {
                var ZSyncAnimation = valheim.GetType("ZSyncAnimation");

                {
                    ZSyncAnimation.Methods.Add(CreateEmptyMethod("OnDestroy", VoidType));
                }
            }

            // Inventory
            {
                var InventoryType = valheim.GetType("Inventory");
                InventoryType.Fields.Add(new FieldDefinition("m_nview", FieldAttributes.Private, ZNetViewType));
                InventoryType.Fields.Add(new FieldDefinition("m_inventoryIndex", FieldAttributes.Private, IntType));

            }

            // ItemDrop/ItemData
            {
                var ItemDataType = valheim.GetType("ItemDrop/ItemData");
                ItemDataType.Fields.Add(new FieldDefinition("m_id", FieldAttributes.Private | FieldAttributes.NotSerialized, IntType));
                ItemDataType.Fields.Add(new FieldDefinition("m_customData", FieldAttributes.Private | FieldAttributes.NotSerialized,
                    assembly.MainModule.ImportReference(typeof(System.Collections.Generic.Dictionary<,>))
                    .MakeGenericInstanceType(IntType, ByteArrayType)));
            }

            // ZRoutedRpc/RoutedRPCData
            {
                var RoutedRPCDataType = valheim.GetType("ZRoutedRpc/RoutedRPCData");
                RoutedRPCDataType.Fields.Add(new FieldDefinition("m_range", FieldAttributes.Private | FieldAttributes.NotSerialized, FloatType));
                RoutedRPCDataType.Fields.Add(new FieldDefinition("m_position", FieldAttributes.Private | FieldAttributes.NotSerialized, Vector3Type));
            }

            // ZRpc
            {
                ZRpcType.Fields.Add(new FieldDefinition("m_ping", FieldAttributes.Private | FieldAttributes.NotSerialized, FloatType));
                ZRpcType.Fields.Add(new FieldDefinition("m_averagePing", FieldAttributes.Private | FieldAttributes.NotSerialized, FloatType));
                ZRpcType.Fields.Add(new FieldDefinition("m_pingTime", FieldAttributes.Private | FieldAttributes.NotSerialized, FloatType));
            }
        }


        private static MethodDefinition CreateEmptyMethod(string name, TypeReference returnType, bool retNull = false)
        {
            return CreateEmptyMethod(new MethodDefinition(name, MethodAttributes.Public, returnType));
        }

        private static MethodDefinition CreateEmptyMethod(MethodDefinition method, bool retNull = false)
        {
            var p = method.Body.GetILProcessor();
            var i = method.Body.Instructions;
            i.Add(p.Create(OpCodes.Nop));
            if (retNull)
                i.Add(p.Create(OpCodes.Ldnull));
            i.Add(p.Create(OpCodes.Ret));
            return method;
        }
    }
}
